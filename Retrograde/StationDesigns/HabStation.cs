using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde.Passes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Retrograde.StationDesigns
{
    public class HabStation : IStationDesign
    {
        private List<IGenPass> stationPasses;
        private ScoringSystem scoringSystem;
        private string dungname;

        List<IGenPass> IStationDesign.stationPasses { get => stationPasses; set => stationPasses = value;  }
        ScoringSystem IStationDesign.scoringSystem { get => scoringSystem; set => scoringSystem = value; }
        public string dungeonName { get => dungname; set => dungname =value; }

        public HabStation()
        {
            stationPasses = new List<IGenPass>()
            {
                //Place rooms
                new StationSetupPass(),
                new TrunkTopologyPass(4),
                    new BossTopologyPass("boss"),
                    new DistrictTopologyPass("rg_hablist", 4, "hab",  new List<string>(){}),
                    //new BridgeHelperPass(),
                    new BridgingTopologyPass(),
                    new UtilTopologyPass("rg_utillist", 0.8f),
                //Seal connectors
                    new ConnectorSealingPass(),
                //Doors and plugs
                    new PlugPass(),
                    new DoorPass(),
                //Fill content
                    new EnemyPass(),
                    new ContentPass(),
                    new ShipMarkerPass(),
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
                        "Freehold","RedFreehold","BlackFreehold","LastFreehold",
                        "Haven","RedHaven","BlackHaven","GraveHaven","FalseHaven",
                        "Shanty","ShantyRing","Shantyhold","ShantyRow",
                        "Driftown","Driftward","Driftstead","DriftHab",
                        "Crowspoint","Crowshold","Crowshaven",
                        "LowDeck","DeepDeck","GunDeck","HabDeck",
                        "Scumline","Rotline","Bloodline","Redline",
                        "Gutside","Hullside","Darkside","Voidside",
                        "The Warrens","IronWarrens","RedWarrens","SkullWarrens",
                        "Dogtown","Rattown","Scraptown","Voidtown",
                        "Holdrow","Bilgerow","Keelrow","Chainrow",
                        "LastBerth","DeadBerth","HardBerth","RedBerth",
                        "NooseNest","CrowNest","ViperNest","SkullNest"
                    };

                    break;
                case "Ecliptic":
                    stationtypes = new List<string>
                    {
                        "Barracks","HighBarracks","DeepBarracks","CentralBarracks",
                        "Quarters","SecureQuarters","OfficerQuarters","ContractorQuarters",
                        "Residency","ControlledResidency","AuthorizedResidency",
                        "HabRing","PrimaryHab","SecondaryHab","HabBlock","HabSector",
                        "Lodging","StandardLodging","ExtendedLodging","RotationalLodging",
                        "Compound","ResidentialCompound","SecureCompound","InnerCompound",
                        "Cantonment","GarrisonHab","DeploymentHab","StagingHab",
                        "Housing","PersonnelHousing","StaffHousing","CommandHousing",
                        "Block","ResidentialBlock","HabBlock-A","HabBlock-B",
                        "Zone","LivingZone","ResidentialZone","RestrictedZone",
                        "Annex","HabAnnex","ResidentialAnnex","PersonnelAnnex"
                    };

                    break;
                case "Varuun":
                    stationtypes = new List<string>
                    {
                        "Sanctuary","InnerSanctuary","DeepSanctuary","HiddenSanctuary",
                        "Cloister","InnerCloister","SealedCloister","SilentCloister",
                        "Conclave","LivingConclave","LowerConclave","VeiledConclave",
                        "Habitat","SacredHabitat","ConsecratedHab","InnerHab",
                        "Dormitory","VotiveDormitory","AsceticDormitory","CommonDormitory",
                        "Communion","LivingCommunion","SharedCommunion",
                        "Enclosure","InnerEnclosure","BlessedEnclosure","SealedEnclosure",
                        "Ward","DevoutWard","PilgrimWard","InitiateWard",
                        "Residence","ConsecratedResidence","FaithboundResidence",
                        "Refuge","SilentRefuge","SerpentRefuge","HiddenRefuge",
                        "Quarter","InnerQuarter","CovenantQuarter","AnointedQuarter",
                        "HabRing","SacredRing","InnerRing","PilgrimRing"
                    };

                    break;
                case "Spacer":
                    stationtypes = new List<string>
                    {
                        "Shack","HabShack","TinShack","VoidShack",
                        "LeanTo","HabLean","PatchLean","DriftLean",
                        "Hole","HabHole","DeepHole","BlackHole",
                        "Nest","ScrapNest","RatNest","VoidNest",
                        "Warren","RustWarren","IronWarren","DeepWarren",
                        "Row","HabRow","ScrapRow","BoltRow",
                        "Deck","LowDeck","DeadDeck","RedDeck",
                        "Block","HabBlock","ScrapBlock","CrowdBlock",
                        "Side","Hullside","Darkside","Outerside",
                        "Pen","HabPen","HoldingPen","PeoplePen",
                        "Barrio","VoidBarrio","ScrapBarrio",
                        "Camp","DriftCamp","ScrapCamp","HardCamp",
                        "Flats","ColdFlats","IronFlats","LowFlats",
                        "Stack","HabStack","ScrapStack","WreckStack",
                        "Lot","Gravelot","DeadLot","RustLot"
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
