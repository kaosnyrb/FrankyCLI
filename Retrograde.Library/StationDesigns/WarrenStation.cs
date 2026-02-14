using Retrograde.Passes;
using System;
using System.Collections.Generic;

namespace Retrograde.StationDesigns;

/// <summary>
/// Compact warren-like station design with dense bridging.
/// Creates tight, maze-like layouts with lots of cross-connections
/// using the ore tileset with medium enemy density.
/// </summary>
public class WarrenStation : IStationDesign
{
    private List<IGenPass> mainRoomPasses;
    private List<OptionalPass> optionalRoomPasses;
    private List<IGenPass> connectorSealingPasses;
    private List<IGenPass> contentPasses;
    private ScoringSystem scoringSystem;
    private string dungname;

    private float areaPerEnemy = 500f;

    List<IGenPass> IStationDesign.MainRoomPasses { get => mainRoomPasses; set => mainRoomPasses = value; }
    List<OptionalPass> IStationDesign.OptionalRoomPasses { get => optionalRoomPasses; set => optionalRoomPasses = value; }
    List<IGenPass> IStationDesign.ConnectorSealingPasses { get => connectorSealingPasses; set => connectorSealingPasses = value; }
    List<IGenPass> IStationDesign.ContentPasses { get => contentPasses; set => contentPasses = value; }
    ScoringSystem IStationDesign.scoringSystem { get => scoringSystem; set => scoringSystem = value; }
    public string dungeonName { get => dungname; set => dungname = value; }
    public float AreaPerEnemy { get => areaPerEnemy; set => areaPerEnemy = value; }

    public WarrenStation()
    {
        // Main room topology passes — three district clusters with short trunk
        // spines between them, plus aggressive bridging for a dense warren layout.
        mainRoomPasses = new List<IGenPass>()
        {
            new StationSetupPass(),
            new DistrictTopologyPass("rg_orelist", 2, "ore", new List<string>(){}),
            new TrunkTopologyPass(2),
            new DistrictTopologyPass("rg_orelist", 2, "ore", new List<string>(){}),
            new TrunkTopologyPass(2),
            new DistrictTopologyPass("rg_orelist", 2, "ore", new List<string>(){}),
            new BossTopologyPass("boss"),
            new BridgingTopologyPass(),
        };

        // Optional room passes (with chance to run)
        optionalRoomPasses = new List<OptionalPass>()
        {
            new OptionalPass(new NPCKeyLootRoomPass("rg_lootroom"), 0.1f),
            new OptionalPass(new BountyTargetEventPass(), 0.1f),
            new OptionalPass(new InfectionEventPass(), 0.1f)
        };

        // Connector sealing passes — aggressive fill to pack the warren tight
        connectorSealingPasses = new List<IGenPass>()
        {
            new UtilTopologyPass("rg_utillist", 0.85f),
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
            BridgingWeight = 35.0,
            BridgingOverlapWeight = -1.0,
            NorthBiasWeight = 0.5,
            NewConnectorsWeight = 30.0,
            PlacementWeight = 15.0,
            AreaWeight = -1.0,
            ClusteringWeight = -5.0,
            SizeDiversityWeight = 10.0,
            RoomReuseWeight = -2.0,
            ConnectorViabilityWeight = 3.0,
            DuplicateRoomPenaltyWeight = -2.0,
            Effort = 300
        };
    }

    /// <summary>
    /// Generate a station name like "Labyrinth VK-394" using warren/maze-themed fragments.
    /// </summary>
    public string GenerateStationName(string Faction)
    {
        var random = RandomProvider.Random;

        // Faction-themed warren transponder codes.
        List<string> callLetters;
        switch (Faction)
        {
            case "Crimsonfleet":
                // Scratched tunnel marks and smuggler codes.
                callLetters = new List<string>
                {
                    "BW","DG","FK","GR","HX","JK","KR","LV","MN","NK",
                    "PR","RK","SK","TN","VK","WR","XK","ZR","BR","CR"
                };
                break;
            case "Ecliptic":
                // Tactical grid-sector designators.
                callLetters = new List<string>
                {
                    "GD","HX","KN","LR","MX","NV","PK","QR","RX","SN",
                    "TK","VR","WN","XD","YK","ZN","BK","CX","DR","FN"
                };
                break;
            case "Varuun":
                // Labyrinthine mystic shorthand.
                callLetters = new List<string>
                {
                    "AX","BN","CZ","DV","EK","FX","GN","HZ","JV","KX",
                    "LN","MZ","NV","PX","QN","RZ","SV","TX","VN","WZ"
                };
                break;
            case "Spacer":
                // Tunnel rat scrawl.
                callLetters = new List<string>
                {
                    "ZX","XZ","QZ","JZ","KZ","GZ","BZ","DZ","FZ","HZ",
                    "LZ","MZ","NZ","PZ","RZ","SZ","TZ","VZ","WZ","YZ"
                };
                break;
            default:
                // Standard warren registry codes.
                callLetters = new List<string>
                {
                    "WN","BW","TN","MZ","LB","CR","DG","HV","KN","NX",
                    "PL","RW","SN","VX","XN","ZW","FK","GR","JN","QW"
                };
                break;
        }

        string letterPart = callLetters[random.Next(callLetters.Count)];

        string numberPart = random.Next(10, 1000).ToString("D3");

        List<string> stationtypes = new List<string>();

        switch (Faction)
        {
            case "Crimsonfleet":
                stationtypes = new List<string>
                {
                    "Rathole","Warren","Burrow","Tunnels","Den",
                    "Maze","Nest","Crawl","Foxhole","Hideout",
                    "Labyrinth","Catacomb","Undercroft","Dugout","Pit",
                    "Sett","Lair","Bolt-Hole","Hutch","Run",
                    "Deadfall","Trench","Underpass","Passage","Gauntlet",
                    "Snare","Blindside","Ambush","Bottleneck","Narrows"
                };
                break;
            case "Ecliptic":
                stationtypes = new List<string>
                {
                    "Complex","Grid","Matrix","Nexus","Hub",
                    "Lattice","Network","Sector","Block","Cluster",
                    "Compound","Precinct","Array","Cell","Node",
                    "Junction","Interchange","Crossroads","Corridor","Passage",
                    "Conduit","Manifold","Framework","Bulkhead","Module",
                    "Partition","Quadrant","Labyrinth","Truss","Deck"
                };
                break;
            case "Varuun":
                stationtypes = new List<string>
                {
                    "Labyrinth","Catacomb","Ossuary","Undercroft","Crypt",
                    "Sanctum","Maze","Passage","Gallery","Vault",
                    "Grotto","Nave","Ambulatory","Cloister","Peristyle",
                    "Hypogeum","Cenote","Oracle","Threshold","Sepulcher",
                    "Reliquary","Vestibule","Antechamber","Chancel","Narthex",
                    "Scriptorium","Refectory","Oubliette","Sacristy","Transept"
                };
                break;
            case "Spacer":
                stationtypes = new List<string>
                {
                    "Rathole","Warren","Nest","Maze","Hole",
                    "Crawlspace","Tunnels","Burrow","Hutch","Dump",
                    "Sty","Den","Cage","Pen","Stack",
                    "Pit","Tangle","Knot","Snarl","Heap",
                    "Shambles","Wreck","Deadend","Trap","Snare",
                    "Gutter","Drain","Vent","Chute","Shaft"
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
