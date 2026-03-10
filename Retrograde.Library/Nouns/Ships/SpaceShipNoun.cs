using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Retrograde.Nouns
{
    // Note: This class doesn't seem to be working currently.
    // Ships created don't seem to be boardable.
    // Root cause is unknown.
    public class SpaceShipNoun : INoun<IGenericBaseFormGetter>
    {
        public string Name { get; set; }
        public uint FactionID { get; set; }
        public uint ShipID { get; set; }

        public GenericBaseForm instance { get; set; }
        public IGenericBaseFormGetter Result => instance;
        string? INoun.EditorID => instance.EditorID;
        FormKey INoun.FormKey => instance.FormKey;

        public SpaceShipNoun(string ShipName, uint ShipFormID, uint ShipFaction)
        {
            var starfieldMod = RetrogradeContext.Current.StarfieldMod;
            var starfieldModKey = RetrogradeContext.Current.StarfieldModKey;
            var targetMod = RetrogradeContext.Current.TargetMod;

            Name=ShipName;
            FactionID = ShipFaction;
            ShipID = ShipFormID;

            var ship = starfieldMod.GenericBaseForms[new FormKey(starfieldModKey, ShipFormID)].DeepCopy();
            instance = new GenericBaseForm(targetMod)
            {
                EditorID = "encship_" + Guid.NewGuid().ToString().Substring(0, 8),
                ObjectBounds = ship.ObjectBounds,
                DirtinessScale = ship.DirtinessScale,
                ObjectPaletteDefaults = ship.ObjectPaletteDefaults,
                Components = ship.Components,
                Filter = ship.Filter,
                ObjectTemplateInstanceData = ship.ObjectTemplateInstanceData,
                ObjectTemplates = ship.ObjectTemplates,
                VirtualMachineAdapter = ship.VirtualMachineAdapter,
                NavmeshGeometry = ship.NavmeshGeometry,
            };
            // Template is IFormLinkNullable — must be set after construction (CLAUDE.md rule)
            if (!ship.Template.IsNull)
                instance.Template.SetTo(ship.Template.FormKey);

            bool setFaction = false;
            foreach (var component in instance.Components)
            {
                if (component is ExternalDataSourceComponent externalDataSourceComponent)
                {
                    var formkey = new FormKey(starfieldModKey, ShipFaction);
                    var ShipTemplate = starfieldMod.LeveledBaseForms[formkey];

                    foreach (var source in externalDataSourceComponent.Sources)
                    {
                        if (source.Name is "FACTIONS" or "AIDATA" or "TRAITS")
                        {
                            source.Source = ShipTemplate.ToLink<IExternalBaseTemplateGetter>();
                            if (source.Name == "FACTIONS") setFaction = true;
                        }
                    }
                    if (!setFaction)
                    {
                        externalDataSourceComponent.Sources.Add(new ExternalDataSource()
                        {
                            Name = "FACTIONS",
                            Source = ShipTemplate.ToLink<IExternalBaseTemplateGetter>()
                        });
                    }
                }
                if (component is FullNameComponent fullName)
                {
                    fullName.Name = ShipName;
                }
            }



            targetMod.GenericBaseForms.Add(instance);
            RetrogradeContext.NounRegistry.Add(this);
        }

    }
}
