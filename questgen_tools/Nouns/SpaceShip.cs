using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Noggog.StructuredStrings.CSharp;
using OpenAI.Chat;
using OpenAI;
using System.Security.Policy;
using FrankyCLI.questgen_tools;
using static Mutagen.Bethesda.FormKeys.Starfield.Starfield;


namespace FrankyCLI.questgen_tools
{
    public class SpaceShip
    {
        public string Name { get; set; }
        public uint FactionID { get; set; }
        public uint ShipID { get; set; }

        public GenericBaseForm Instance { get; set; }

        public SpaceShip(string ShipName, uint ShipFormID, uint ShipFaction)
        {
            Name=ShipName;
            FactionID = ShipFaction;
            ShipID = ShipFormID;

            var ship = gen_quest_main._StarfieldMod.GenericBaseForms[new FormKey(gen_quest_main.StarfieldModKey, ShipFormID)].DeepCopy();
            Instance = new GenericBaseForm(gen_quest_main.myMod)
            {
                EditorID = "encship_" + Guid.NewGuid().ToString().Substring(0, 8),
                ObjectBounds = ship.ObjectBounds,
                Components = ship.Components,
                ObjectTemplates = ship.ObjectTemplates,
                Template = ship.Template,
                ObjectPlacementDefaults = ship.ObjectPlacementDefaults,
            };

            bool setFaction = false;
            foreach (var component in Instance.Components)
            {
                var typestring = component.GetType().ToString();
                //Console.WriteLine(component.GetType().ToString());
                if (typestring == "Mutagen.Bethesda.Starfield.ExternalDataSourceComponent")
                {
                    var formkey = new FormKey(gen_quest_main.StarfieldModKey, ShipFaction);
                    var ShipTemplate = gen_quest_main._StarfieldMod.LeveledBaseForms[formkey];

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



            gen_quest_main.myMod.GenericBaseForms.Add(Instance);
        }

    }
}
