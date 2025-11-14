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
    public class ShipTools
    {
        //EncShip_FreestarCitizen_A_Railstar01 [GBFM:00042383]
        //FACTIONS  LShip_FreestarCitizen_Template [LVLB:000CFABA] in External data sources

        /*
            Blueprint_Component
            BGSSpaceshipWeaponBindings_Component
            BGSKeywordForm_Component
            TESContainer_Component
            BGSAddToInventoryOnDestroy_Component
            FACTIONS
            0CFABA:Starfield.esm<IExternalBaseTemplateGetter>
            AIDATA
            BGSSpaceshipEquipment_Component
            BGSSpaceshipHullCode_Component
            TRAITS
            BGSPropertySheet_Component
        */
        //TESFullName_Component 
        public static GenericBaseForm GenShip(string ShipName, uint ShipFormID, uint ShipFaction)
        {
            // 
            //
            var ship = gen_quest._StarfieldMod.GenericBaseForms[new FormKey(gen_quest.StarfieldModKey, ShipFormID)].DeepCopy();

            var newship = new GenericBaseForm(gen_quest.myMod)
            {
                EditorID = "encship_" + Guid.NewGuid().ToString().Substring(0, 8),
                ObjectBounds = ship.ObjectBounds,
                Components = ship.Components,
                ObjectTemplates = ship.ObjectTemplates,
                Template = ship.Template,
                ObjectPlacementDefaults = ship.ObjectPlacementDefaults,
            };

            bool setFaction = false;
            foreach (var component in newship.Components)
            {
                var typestring = component.GetType().ToString();
                //Console.WriteLine(component.GetType().ToString());
                if(typestring == "Mutagen.Bethesda.Starfield.ExternalDataSourceComponent")
                {
                    var ShipTemplate = gen_quest._StarfieldMod.LeveledBaseForms[new FormKey(gen_quest.StarfieldModKey, ShipFaction)]; //LShip_Ecliptic_Template [LVLB:000AE4F3]

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



            gen_quest.myMod.GenericBaseForms.Add(newship);
            return newship;
        }
    }
}
