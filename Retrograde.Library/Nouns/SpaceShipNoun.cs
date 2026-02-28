using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Retrograde.Nouns
{
    public class SpaceShipNoun
    {
        public string Name { get; set; }
        public uint FactionID { get; set; }
        public uint ShipID { get; set; }

        public GenericBaseForm instance { get; set; }

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
                var typestring = component.GetType().ToString();
                //Console.WriteLine(component.GetType().ToString());
                if (typestring == "Mutagen.Bethesda.Starfield.ExternalDataSourceComponent")
                {
                    var formkey = new FormKey(starfieldModKey, ShipFaction);
                    var ShipTemplate = starfieldMod.LeveledBaseForms[formkey];

                    ExternalDataSourceComponent externalDataSourceComponent = (ExternalDataSourceComponent)component;
                    foreach (var source in externalDataSourceComponent.Sources)
                    {
                        //Console.WriteLine(source.Name);
                        if (source.Name == "FACTIONS")
                        {
                            source.Source = ShipTemplate.ToLink<IExternalBaseTemplateGetter>();
                            setFaction = true;
                        }
                        if (source.Name == "AIDATA")
                        {
                            source.Source = ShipTemplate.ToLink<IExternalBaseTemplateGetter>();
                        }
                        if (source.Name == "TRAITS")
                        {
                            source.Source = ShipTemplate.ToLink<IExternalBaseTemplateGetter>();
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
                if (typestring == "Mutagen.Bethesda.Starfield.FullNameComponent")
                {
                    FullNameComponent fullName = (FullNameComponent)component;
                    //Console.WriteLine(fullName.Name);
                    fullName.Name = ShipName;
                }
            }



            targetMod.GenericBaseForms.Add(instance);
        }

    }
}
