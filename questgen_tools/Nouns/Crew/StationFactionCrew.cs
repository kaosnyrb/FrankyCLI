using FrankyCLI.questgen_tools.Interfaces;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System.Data;

namespace FrankyCLI.questgen_tools
{
    public class StationFactionCrew : ICrew
    {
        public static string GetCrewJobRole(string Faction)
        {
            Random r = RandomUtils.random;

            List<string> roles = new List<string>();
            switch (Faction)
            {
                case "Crimsonfleet":
                    roles = new List<string>
                    {
                        "Grinder","IronGrinder","VoidGrinder","BloodGrinder","DeepGrinder",
                        "Crush","Stonecrush","Hullcrush","Redcrush","Hardcrush",
                        "Smelter","BlackSmelter","VoidSmelter","ChainSmelter",
                        "Foundry","IronFoundry","RedFoundry","GraveFoundry",
                        "Refinery","BlackRefinery","VoidRefinery","BloodRefinery",
                        "Breaker","Hullbreaker","Rockbreaker","Chainbreaker","Voidbreaker",
                        "Oreworks","Ironworks","Voidworks","Bloodworks",
                        "Forge","BlackForge","VoidForge","ChainForge","GraveForge",
                        "SalvageWorks","ScrapWorks","BreakWorks","CutWorks",
                        "Fuelworks","BlackFuel","VoidFuel","RedFuel",
                        "Kiln","BlastKiln","VoidKiln","IronKiln",
                        "Press","Hammer","Anvil","Die"
                    };

                    break;
                case "Ecliptic":
                    roles = new List<string>
                    {
                        "Arsenal","PrimaryArsenal","ForwardArsenal","OrbitalArsenal",
                        "Foundry","AdvancedFoundry","WeaponsFoundry","AlloyFoundry",
                        "Manufacture","ManufacturingNode","ProductionNode","AssemblyNode",
                        "Assembly","FinalAssembly","ModularAssembly","WeaponsAssembly",
                        "Refinery","FuelRefinery","IsotopeRefinery","MaterialRefinery",
                        "Processing","OreProcessing","MaterialProcessing","SalvageProcessing",
                        "Logistics","LogisticsHub","SupplyHub","DistributionHub",
                        "Depot","MunitionsDepot","SupplyDepot","OrbitalDepot",
                        "Stockpile","StrategicStockpile","ReserveStockpile",
                        "Works","HeavyWorks","OrdnanceWorks","DefenseWorks",
                        "Fabrication","FabricationBay","FabricationSector","FabricationRing",
                        "Forge","PrecisionForge","MilitaryForge",
                        "Plant","IndustrialPlant","WeaponsPlant","FuelPlant",
                        "Terminal","IndustrialTerminal","CargoTerminal"
                    };

                    break;
                case "Varuun":
                    roles = new List<string>
                    {
                        "Anvil","SacredAnvil","BlackAnvil","ConsecratedAnvil",
                        "Foundry","SanctifiedFoundry","VoidFoundry","SerpentFoundry",
                        "Forge","RitualForge","IronForge","ObsidianForge",
                        "Refinery","ConsecratedRefinery","VoidRefinery","BloodRefinery",
                        "Works","SacredWorks","HiddenWorks","SilentWorks",
                        "Processing","SanctifiedProcessing","Purification","Refinement",
                        "Crucible","BlackCrucible","VoidCrucible","SerpentCrucible",
                        "Kiln","RiteKiln","AshKiln","ObsidianKiln",
                        "Extraction","SacredExtraction","DeepExtraction","HiddenExtraction",
                        "Smelter","BlackSmelter","VoidSmelter","RitualSmelter",
                        "Laborium","SilentLaborium","ConsecratedLaborium",
                        "Vaultworks","ReliquaryWorks","DoctrineWorks",
                        "Plant","SanctifiedPlant","IndustrialPlant"
                    };
                    break;
                case "Spacer":
                    roles = new List<string>
                    {
                        "Scrapworks","Rustworks","Breakworks","Cutworks",
                        "Grinder","ScrapGrinder","BoneGrinder","HullGrinder",
                        "Crusher","RockCrusher","PlateCrusher","ChainCrusher",
                        "Smelter","JurySmelter","BlackSmelter","RustSmelter",
                        "Refinery","BadRefinery","PatchRefinery","LeakRefinery",
                        "Breaker","HullBreaker","ScrapBreaker","SpineBreaker",
                        "Strip","StripMine","VoidStrip","HardStrip",
                        "Kiln","CrackKiln","SmokeKiln","HotKiln",
                        "Forge","HackForge","WeldForge","PatchForge",
                        "Salvage","HotSalvage","DirtySalvage","QuickSalvage",
                        "Yard","ScrapYard","BreakYard","DeadYard",
                        "Press","BentPress","WarpPress","CrushPress",
                        "Plant","BadPlant","HotPlant","FailPlant"
                    };

                    break;
                default:
                    roles = new List<string>
                    {
                        "Station","Outpost","Facility","Platform","Installation","Complex","Depot","Hub","Relay","Array",
                        "Terminal","Dock","Yard","Anchorage","Spindle","Spire","Module","Node","Enclave",
                        "Bastion","Citadel","Stronghold","Redoubt","Sanctum","Vault","Foundry","Forge","Works","Refinery",
                        "Exchange","Concourse","Crossing","Waypoint","Observatory","Surveyor","ListeningPost",
                        "Harbor","Drydock",
                        "Arcology","Habitat","Hab","Colony","Settlement","Commune","Barracks","Garrison","Command",
                        "Operations","Control","CommandPost","Headquarters","Center","Core","Nexus","Axis","Pylon","Anchor","Keystone"
                    };
                    break;
            }

            return roles[r.Next(roles.Count)];
        }

        public IFormLink<IStarfieldMajorRecordGetter> GetCrewFormList(string Faction,string ShipName)
        {
            var frmlst = new FormList(gen_quest_main.myMod)
            {
                EditorID = ShipName + "_crewlist",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };
            //Dead Named Crew
            var list = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>();
            Random random = RandomUtils.random;
            //Generate a new NPC
            string Crewname = Faction;

            int crewcount = 10;

            for (int i = 0; i < crewcount; i++)
            {
                bool isfemale = false;
                if (random.Next(100) > 50)
                {
                    isfemale = true;
                }
                var outfit = NPCTools.GetRandomFactionOutfit(Faction);

                var NPC = gen_quest_main.myMod.Npcs[new FormKey(gen_quest_main.myMod.ModKey, NPCTools.GetTemplateNPC(isfemale))].DeepCopy();
                Npc npc = NPCTools.CloneNPC(gen_quest_main.myMod, NPC);

                //Name
                //Console.WriteLine("Generating Crew Name...");
                string Gender = "Male";
                if (isfemale) Gender = "Female";
                npc.Name = Crewname + " " + GetCrewJobRole(Faction);
                npc.EditorID = "npc_" + (npc.Name.ToString().ToLower()).Replace(" ", "");
               
                Random wrand = RandomUtils.random;
                npc.Weight = new NpcWeight()
                {
                    Fat = (float)wrand.NextDouble(),
                    Muscular = (float)wrand.NextDouble(),
                    Thin = (float)wrand.NextDouble()
                };
                var lev = new PcLevelMult();
                lev.LevelMult = (float)random.NextDouble();
                npc.Level = lev;
                npc.SpaceOutfit = outfit;

                npc.EyeColor = NPCTools.GetEyeColour();
                npc.HairColor = NPCTools.GetHairColour();
                npc.SkinToneIndex = (byte)wrand.Next(8);
                npc.HeadParts.Add(NPCTools.GetHaircut(isfemale));

                npc.Items = new ExtendedList<ContainerEntry>
                {
                    new ContainerEntry() { Item = new ContainerItem() { Item = NPCTools.GetRandomGear(), Count = 1 } },
                };

                gen_quest_main.myMod.Npcs.Add(npc);
                //Add it to the list
                list.Add(npc);
                frmlst.Items.Add(npc);
            }

            gen_quest_main.myMod.FormLists.Add(frmlst);
            return gen_quest_main.myMod.FormLists[frmlst.FormKey].ToLink<IStarfieldMajorRecordGetter>();
        }
    }
}
