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

        public static uint GetCargoShip()
        {
            Random random = new Random();
            List<uint> shiplist = new List<uint>()
            {
                0x0018D3E2, // EncShip_UCCitizen_A_Cargo_MULE01 [GBFM:0018D3E2]
                0x0018D3E4, // EncShip_UCCitizen_A_Cargo_MULE02 [GBFM:0018D3E4]
                0x0018D3E6, // EncShip_UCCitizen_A_Cargo_MULE03 [GBFM:0018D3E6]
                0x0002C8AA, // EncShip_UCCitizen_B_Cargo_CarryAll01 [GBFM:0002C8AA]
                0x0002CA6B, // EncShip_UCCitizen_B_Cargo_CarryAll02 [GBFM:0002CA6B]
                0x0002CAEC, // EncShip_UCCitizen_B_Cargo_CarryAll03 [GBFM:0002CAEC]
                0x0002CB1E, // EncShip_UCCitizen_B_Cargo_Pelican01 [GBFM:0002CB1E]
                0x0002CB21, // EncShip_UCCitizen_B_Cargo_Pelican02 [GBFM:0002CB21]
                0x0002CAFA, // EncShip_UCCitizen_B_Cargo_SpaceOx01 [GBFM:0002CAFA]
                0x0002CB15, // EncShip_UCCitizen_B_Cargo_SpaceOx02 [GBFM:0002CB15]
                0x0002CB1B, // EncShip_UCCitizen_B_Cargo_SpaceOx03 [GBFM:0002CB1B]
                0x00331AC7, // EncShip_TradeAuthority_A_Atlas01 [GBFM:00331AC7]
                0x00333D9A, // EncShip_TradeAuthority_A_Atlas02 [GBFM:00333D9A]
                0x00333D9C, // EncShip_TradeAuthority_A_Atlas03 [GBFM:00333D9C]
                0x00333D9E, // EncShip_TradeAuthority_A_Railstar01 [GBFM:00333D9E]
                0x00333E89, // EncShip_TradeAuthority_A_Railstar02 [GBFM:00333E89]
                0x000423B1, // EncShip_TradeAuthority_A_Railstar03 [GBFM:000423B1]
                0x00347145, // EncShip_TradeAuthority_B_WagonTrain01 [GBFM:00347145]
                0x0034B5C4, // EncShip_TradeAuthority_B_WagonTrain02 [GBFM:0034B5C4]
                0x0034B5C7, // EncShip_TradeAuthority_B_WagonTrain03 [GBFM:0034B5C7]
                0x0034B5CC, // EncShip_TradeAuthority_C_Highlander01 [GBFM:0034B5CC]
                0x0034B5F0, // EncShip_TradeAuthority_C_Highlander02 [GBFM:0034B5F0]
                0x0003CF96, // EncShip_TradeAuthority_C_Highlander03 [GBFM:0003CF96]
                0x00315877, // EncShip_StarParcel_A_Pikup01 [GBFM:00315877]
                0x0031587B, // EncShip_StarParcel_A_Pikup02 [GBFM:0031587B]
                0x0031587D, // EncShip_StarParcel_A_Pikup03 [GBFM:0031587D]
                0x0002C269, // EncShip_StarParcel_B_Spacetruk01 [GBFM:0002C269]
                0x0002C26B, // EncShip_StarParcel_B_Spacetruk02 [GBFM:0002C26B]
                0x0002C2C0, // EncShip_StarParcel_B_Spacetruk03 [GBFM:0002C2C0]
                0x0003CFAA, // EncShip_StarParcel_C_Kirov04 [GBFM:0003CFAA]
                0x0003CFA8, // EncShip_StarParcel_C_StarSemi01 [GBFM:0003CFA8]
            };

            return shiplist[random.Next(shiplist.Count)];
        }
    }
}
