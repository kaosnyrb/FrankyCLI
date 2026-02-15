using Retrograde.Passes;
using System;
using System.Collections.Generic;

namespace Retrograde.StationDesigns;

/// <summary>
/// Industrial ore processing station design with higher enemy density.
/// Uses the ore tileset with industrial and mining themes.
/// </summary>
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
            new DistrictTopologyPass("rg_orelist", 2, "ore", new List<string>(){}),
            new TrunkTopologyPass(2),
            new DistrictTopologyPass("rg_orelist", 2, "ore", new List<string>(){}),
            new BossTopologyPass("boss"),
            new BridgeHelperPass(),
            new BridgingTopologyPass(),
        };

        // Optional room passes (with chance to run)
        optionalRoomPasses = new List<OptionalPass>()
        {
            // Add optional passes here, e.g.:
            new OptionalPass(new NPCKeyLootRoomPass("rg_lootroom"), 0.1f),
            new OptionalPass(new BountyTargetEventPass(), 0.1f),
            new OptionalPass(new InfectionEventPass(), 0.1f)
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
            DuplicateRoomPenaltyWeight = -2.0,
            Effort = 250
        };
    }

    /// <summary>
    /// Generate a station name like "Station Bf-394" using themed call-sign fragments.
    /// </summary>
    public string GenerateStationName(string Faction)
    {
        var random = RandomProvider.Random;

        // Faction-themed industrial transponder codes.
        List<string> callLetters;
        switch (Faction)
        {
            case "Crimsonfleet":
                // Heavy industry pirate forge marks.
                callLetters = new List<string>
                {
                    "FG","HM","KR","SL","GD","VK","TX","BL","MK","NK",
                    "PR","WR","ZK","JR","XR","DR","BR","SK","CR","RN"
                };
                break;
            case "Ecliptic":
                // Military logistics and supply chain codes.
                callLetters = new List<string>
                {
                    "SC","PD","LG","DP","MN","SR","FN","HD","KT","OD",
                    "QL","RG","TN","WD","CL","EQ","GP","JL","NR","AD"
                };
                break;
            case "Varuun":
                // Alchemical element shorthand.
                callLetters = new List<string>
                {
                    "AQ","HG","AU","FE","CU","AG","SN","PB","ZN","CR",
                    "TI","NI","CO","PT","OS","IR","RU","MG","AL","BI"
                };
                break;
            case "Spacer":
                // Scratched-in junk identifiers.
                callLetters = new List<string>
                {
                    "XX","ZZ","QQ","BX","FK","GZ","HV","KX","LZ","MX",
                    "PX","RZ","SZ","TZ","VZ","WZ","XJ","YZ","JJ","NX"
                };
                break;
            default:
                // Standard industrial registry codes.
                callLetters = new List<string>
                {
                    "AL","CP","DK","EM","FS","GV","HT","JC","KN","LP",
                    "MQ","NW","OY","PR","QS","RU","SV","TY","WX","ZA"
                };
                break;
        }

        string letterPart = callLetters[random.Next(callLetters.Count)];

        // Occasionally generate a fresh 3-digit run to reduce repetition.
        string numberPart = random.Next(10, 1000).ToString("D3");

        List<string> stationtypes = new List<string>();

        switch (Faction)
        {
            case "Crimsonfleet":
                stationtypes = new List<string>
                {
                    "Grinder","Smelter","Foundry","Forge","Breaker",
                    "Kiln","Anvil","Press","Hammer","Furnace",
                    "Crucible","Ironworks","Sweatshop","Chopshop",
                    "Boneyard","Pit","Scrapheap","Junkworks",
                    "Hellforge","Die",
                    "Galleyworks","Bellows","Ladle","Slag",
                    "Cinder","Clinker","Maw","Gutworks","Trough","Cruciform"
                };
                break;
            case "Ecliptic":
                stationtypes = new List<string>
                {
                    "Arsenal","Foundry","Assembly","Refinery","Depot",
                    "Stockpile","Plant","Terminal","Forge","Works",
                    "Fabricator","Armory","Munitions","Factory",
                    "Smelter","Furnace","Dockyard","Metalworks",
                    "Millworks","Manufactory",
                    "Warehouse","Storehouse","Silo","Hangar","Drydock",
                    "Shipyard","Proving","Crucible","Lathe","Workshop"
                };
                break;
            case "Varuun":
                stationtypes = new List<string>
                {
                    "Crucible","Forge","Kiln","Furnace","Smelter",
                    "Reliquary","Laborium","Purgatory","Sanctum",
                    "Ossuary","Athanor","Alembic","Crematory",
                    "Anvil","Foundry","Refinery","Works","Altar",
                    "Censer","Thurible","Catacomb","Sepulcher","Vestry",
                    "Tabernacle","Sanctorum","Chalice","Pyre","Brazier"
                };
                break;
            case "Spacer":
                stationtypes = new List<string>
                {
                    "Scrapyard","Grinder","Crusher","Smelter","Breaker",
                    "Junkyard","Salvage","Pit","Chopshop","Dump",
                    "Heap","Yard","Scrapheap","Junkheap","Tinworks",
                    "Rustworks","Kiln","Press","Forge","Furnace",
                    "Hacksaw","Torchworks","Welders","Cutters","Shredder",
                    "Compactor","Slagheap","Burnhole","Ashpit","Trashworks"
                };
                break;
            default:
                stationtypes = new List<string>
                {
                    "Station","Outpost","Facility","Platform","Complex","Depot","Hub","Relay","Array",
                    "Terminal","Dock","Yard","Anchorage","Spindle","Spire","Module","Node","Enclave",
                    "Bastion","Citadel","Stronghold","Redoubt","Sanctum","Vault","Foundry","Forge","Works","Refinery",
                    "Exchange","Concourse","Crossing","Waypoint","Observatory","Surveyor",
                    "Harbor","Drydock",
                    "Arcology","Habitat","Colony","Settlement","Commune","Barracks","Garrison","Command",
                    "Operations","Control","CommandPost","Center","Core","Nexus","Axis","Pylon","Anchor","Keystone"
                };
                break;
        }

        string stationPart = stationtypes[random.Next(stationtypes.Count)];

        dungeonName = $"{stationPart} {letterPart}-{numberPart}";
        return dungeonName;
    }
}
