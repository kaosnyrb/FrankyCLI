using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde.Passes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Retrograde.StationDesigns
{
    public class OreStation : IStationDesign
    {
        private List<IGenPass> mainRoomPasses;
        private List<OptionalPass> optionalRoomPasses;
        private List<IGenPass> connectorSealingPasses;
        private List<IGenPass> contentPasses;
        private ScoringSystem scoringSystem;
        private string dungname;

        private float areaPerEnemy = 384f;

        List<IGenPass> IStationDesign.MainRoomPasses { get => mainRoomPasses; set => mainRoomPasses = value; }
        List<OptionalPass> IStationDesign.OptionalRoomPasses { get => optionalRoomPasses; set => optionalRoomPasses = value; }
        List<IGenPass> IStationDesign.ConnectorSealingPasses { get => connectorSealingPasses; set => connectorSealingPasses = value; }
        List<IGenPass> IStationDesign.ContentPasses { get => contentPasses; set => contentPasses = value; }
        ScoringSystem IStationDesign.scoringSystem { get => scoringSystem; set => scoringSystem = value; }
        public string dungeonName { get => dungname; set => dungname = value; }
        public float AreaPerEnemy { get => areaPerEnemy; set => areaPerEnemy = value; }

        public OreStation()
        {
            // Main room topology passes
            mainRoomPasses = new List<IGenPass>()
            {
                new StationSetupPass(),
                new DistrictTopologyPass("rg_orelist", 4, "ore", new List<string>(){"rg_sts_ore_hallway_002"}),
                new BossTopologyPass("boss"),
                new BridgingTopologyPass(),
            };

            // Optional room passes (with chance to run)
            optionalRoomPasses = new List<OptionalPass>()
            {
                // Add optional passes here, e.g.:
                // new OptionalPass(new LockedLootRoomPass(), 0.5f),
            };

            // Connector sealing passes
            connectorSealingPasses = new List<IGenPass>()
            {
                new UtilTopologyPass("rg_utillist", 0.5f),
                new ConnectorSealingPass(),
            };

            // Content passes (doors, plugs, enemies, etc.)
            contentPasses = new List<IGenPass>()
            {
                new PlugPass(),
                new DoorPass(),
                new EnemyPass(),
                new ContentPass(),
                new ShipMarkerPass(),
                new EnemyAlertPrimitiveCoveragePass(),
            };

            scoringSystem = new ScoringSystem()
            {
                BridgingWeight = 23.12,
                BridgingOverlapWeight = -3.21,
                NorthBiasWeight = 2.3,
                NewConnectorsWeight = 26.61,
                PlacementWeight = 17.33,
                AreaWeight = 0,
                ClusteringWeight = 6.42,
                SizeDiversityWeight = 16.06,
                RoomReuseWeight = -3.21,
                ConnectorViabilityWeight = 1.73,
                Effort = 250
            };

        }
        /// <summary>
        /// Generate a station name like "Station Bf-394" using themed call-sign fragments.
        /// </summary>
        /// <returns>Formatted station name string.</returns>
        public string GenerateStationName(string Faction)
        {
            Random random = RandomUtils.random;

            // Call signs evoke registry codes and transponder shorthand.
            List<string> callLetters = new List<string>
            {
                "BF","XR","NS","OD","VA","HG","ZE","PH","IR","KQ",
                "LM","TR","UV","QA","CY","RN","SD","WG","TX","JY",
                "AL","CP","DK","EM","FS","GV","HT","JC","KN","LP",
                "MQ","NW","OY","PR","QS","RU","SV","TY","WX","ZA",
                "AB","CE","DM","EK","FL","GR","HX","IL","JP","KR",
                "MT","NZ","OP","RB","SC","UF","VQ","YL","ZX","TD"
            };

            string letterPart = callLetters[random.Next(callLetters.Count)];

            // Occasionally generate a fresh 3-digit run to reduce repetition.
            string numberPart = random.Next(10, 1000).ToString("D3");

            List<string> stationtypes = new List<string>();

            switch (Faction)
            {
                case "Crimsonfleet":
                    stationtypes = new List<string>
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
                    stationtypes = new List<string>
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
                    stationtypes = new List<string>
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
                    stationtypes = new List<string>
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
                    stationtypes = new List<string>
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

            string stationPart = stationtypes[random.Next(stationtypes.Count)];

            dungeonName = $"{stationPart} {letterPart}-{numberPart}";
            return dungeonName;
        }
    }
}
