using Retrograde.Passes;
using System;
using System.Collections.Generic;

namespace Retrograde.StationDesigns;

/// <summary>
/// Habitation station design with lower enemy density.
/// Uses living quarters, dormitories and residential themes.
/// </summary>
public class HabStation : IStationDesign
{
    private List<IGenPass> mainRoomPasses;
    private List<OptionalPass> optionalRoomPasses;
    private List<IGenPass> connectorSealingPasses;
    private List<IGenPass> contentPasses;
    private ScoringSystem scoringSystem;
    private string dungname;

    private float areaPerEnemy = 840f;

    List<IGenPass> IStationDesign.MainRoomPasses { get => mainRoomPasses; set => mainRoomPasses = value; }
    List<OptionalPass> IStationDesign.OptionalRoomPasses { get => optionalRoomPasses; set => optionalRoomPasses = value; }
    List<IGenPass> IStationDesign.ConnectorSealingPasses { get => connectorSealingPasses; set => connectorSealingPasses = value; }
    List<IGenPass> IStationDesign.ContentPasses { get => contentPasses; set => contentPasses = value; }
    ScoringSystem IStationDesign.scoringSystem { get => scoringSystem; set => scoringSystem = value; }
    public string dungeonName { get => dungname; set => dungname = value; }
    public float AreaPerEnemy { get => areaPerEnemy; set => areaPerEnemy = value; }

    public HabStation()
    {
        // Main room topology passes
        mainRoomPasses = new List<IGenPass>()
        {
            new StationSetupPass(),
            new TrunkTopologyPass(4),
            new BossTopologyPass("boss"),
            new DistrictTopologyPass("rg_hablist", 4, "hab", new List<string>(){}),
            new BridgingTopologyPass(),
        };

        // Optional room passes (with chance to run)
        optionalRoomPasses = new List<OptionalPass>()
        {
            // Add optional passes here, e.g.:
            new OptionalPass(new NPCKeyLootRoomPass("rg_lootroom"), 1),
            //new OptionalPass(new BountyTargetEventPass(), 1),
            //new OptionalPass(new InfectionEventPass(), 1)
        };

        // Connector sealing passes
        connectorSealingPasses = new List<IGenPass>()
        {
            new UtilTopologyPass("rg_utillist", 0.8f),
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
            BridgingWeight = 9.5,
            BridgingOverlapWeight = -1.19,
            NorthBiasWeight = 0.95,
            NewConnectorsWeight = 0.89,
            PlacementWeight = 77.16,
            AreaWeight = -0.15,
            ClusteringWeight = 2.37,
            SizeDiversityWeight = 5.94,
            RoomReuseWeight = -1.19,
            ConnectorViabilityWeight = 0.67,
            Effort = 100
        };
    }

    /// <summary>
    /// Generate a station name like "Station Bf-394" using themed call-sign fragments.
    /// </summary>
    public string GenerateStationName(string Faction)
    {
        var random = RandomProvider.Random;

        // Faction-themed transponder call signs.
        List<string> callLetters;
        switch (Faction)
        {
            case "Crimsonfleet":
                // Pirate hull tags and stolen registry marks.
                callLetters = new List<string>
                {
                    "RD","BK","SK","GR","CR","DK","HK","VX","KL","DR",
                    "BG","MK","JK","NX","ZR","RX","SV","TS","VR","BN"
                };
                break;
            case "Ecliptic":
                // Clean military NATO-style designators.
                callLetters = new List<string>
                {
                    "AL","BR","CH","DL","EC","FX","GL","HQ","KP","LT",
                    "MR","NV","PL","QR","RS","ST","TV","UX","WC","YR"
                };
                break;
            case "Varuun":
                // Archaic mystical shorthand.
                callLetters = new List<string>
                {
                    "AZ","VN","SN","KH","TH","IX","NU","OM","PH","RA",
                    "SE","UR","VE","XA","YN","ZE","AN","EL","OX","AT"
                };
                break;
            case "Spacer":
                // Rough scrawled junk codes.
                callLetters = new List<string>
                {
                    "XZ","QK","JB","WR","FT","ZB","KV","GX","DQ","HJ",
                    "MZ","PT","RW","SX","TK","VB","WQ","YX","NJ","BX"
                };
                break;
            default:
                // Standard registry codes.
                callLetters = new List<string>
                {
                    "BF","XR","NS","OD","VA","HG","ZE","PH","IR","KQ",
                    "LM","TR","UV","QA","CY","RN","SD","WG","TX","JY"
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
                    "Freehold","Haven","Shanty","Lair","Den",
                    "Hideout","Berth","Nest","Warren","Roost",
                    "Dive","Hovel","Foxhole","Bunkroom","Rathole",
                    "Hideaway","Safehouse","Crib","Squat","Dugout",
                    "Dogtown","Rattown","Scraptown","Drifttown",
                    "Brig","Stash","Flop","Galley","Bilge",
                    "Gutter","Holdfast","Stockade","Cathouse","Grotto"
                };
                break;
            case "Ecliptic":
                stationtypes = new List<string>
                {
                    "Barracks","Quarters","Billet","Garrison","Compound",
                    "Bunker","Wardroom","Dormitory","Annex","Block",
                    "Sector","Module","Berth","Bay","Bunkhouse",
                    "Lodging","Bivouac","Outpost","Depot","Facility",
                    "Pillbox","Redoubt","Bastion","Citadel","Stockade",
                    "Encampment","Precinct","Bulkhead","Foxhole","Turret"
                };
                break;
            case "Varuun":
                stationtypes = new List<string>
                {
                    "Sanctuary","Cloister","Conclave","Enclave","Refuge",
                    "Sanctum","Priory","Vestry","Abbey","Hermitage",
                    "Chapel","Haven","Hospice","Commune","Narthex",
                    "Rectory","Monastery","Tabernacle",
                    "Scriptorium","Oratory","Reliquary","Sacristy","Chancel",
                    "Chantry","Apse","Basilica","Cenacle","Refectory"
                };
                break;
            case "Spacer":
                stationtypes = new List<string>
                {
                    "Shack","Hole","Nest","Warren","Squat",
                    "Hovel","Dump","Pen","Flats","Stack",
                    "Hutch","Burrow","Bolthole","Rathole","Cubby",
                    "Coop","Dugout","Crawlspace","Hellhole","Doss",
                    "Sty","Kennel","Cage","Crate","Nook",
                    "Alcove","Closet","Cellar","Loft","Garret"
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
