using FrankyCLI.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Assets;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Noggog.StructuredStrings.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public class StationNoun
    {
        public GenericBaseForm instance;

        /// <summary>
        /// Generate a station name like "Station Bf-394" using themed call-sign fragments.
        /// </summary>
        /// <returns>Formatted station name string.</returns>
        public static string GenerateStationName(string Faction)
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
                        "Blackwake","Redwake","Ironwake","Deadwake","Voidwake","Starwake","Bloodwake","Gravewake",
                        "Crossbones","Blackmast","Redmast","Ironmast","BrokenMast","LastMast",
                        "Skullpoint","Skullreach","Skullspire","Skullhold","Skullhaven",
                        "Gallows","HighGallows","IronGallows","VoidGallows",
                        "Deadfall","Hardfall","Starfall","Redfall","Blackfall",
                        "Cutthroat","Throatcutter","Razorhold","Bladepoint","Knifedge",
                        "Blackharbor","Redharbor","Voidharbor","Graveharbor",
                        "Freeport","Bloodport","Shadowport","Ironport",
                        "Strongarm","Ironfist","Redfist","Hardknuckle",
                        "Holdfast","LastHold","IronHold","CrimsonHold",
                        "Blackbrand","Redbrand","Voidbrand","Starbrand",
                        "Graveyard","Bonefield","Wreckyard","DebrisField",
                        "NoQuarter","FinalClaim","LastClaim","DeadClaim"
                    };
                    break;
                case "Ecliptic":
                    stationtypes = new List<string>
                    {
                        "Aegis","Bulwark","Rampart","Citadel","Bastion","Redoubt","Stronghold","Keep",
                        "Sentinel","Overwatch","Vanguard","Palisade","Spearhead","Phalanx","Shieldwall",
                        "Arbiter","Executor","Enforcer","Warden","Custodian","Overseer","Prefect","Justicar",
                        "Directive","Mandate","Edict","Protocol","Accord","Covenant","Pact","Charter",
                        "Fulcrum","Keystone","Anchor","Axis","Nexus","Node","Hub","Core",
                        "Control","Command","Operations","Logistics","Deployment","ForwardBase","Staging","Relay",
                        "Redline","Hardpoint","Killbox","Firebreak","Deadzone","Strikepoint","Crossfire","Lockdown",
                        "Blacksite","Securehold","Containment","Detention","Quarantine","Vault","Arsenal","Armory"
                    };
                    break;
                case "Varuun":
                    stationtypes = new List<string>
                    {
                        "Serpentis","Coil","FirstCoil","FinalCoil","EndlessCoil","SacredCoil","BlackCoil",
                        "Sanctum","InnerSanctum","DeepSanctum","HiddenSanctum","Hallowed","Consecrated","Anointed","Profane",
                        "Vigil","EternalVigil","LastVigil","SilentVigil","Unblinking","Watchful","Oathbound","Vowkeeper",
                        "Oracle","HighOracle","Revelator","Seer","Vision","Prophecy","Scripture","Canticle",
                        "Shrine","Reliquary","Tabernacle","Catacomb","Crypt","Sepulcher","Reliquum","Chapel",
                        "Ascension","Transcendence","Apotheosis","Rapture","Communion","Absolution","Judgment","Exaltation",
                        "Obsidian","BlackSpire","VoidSpire","SerpentSpire","IronSpire","SanctifiedSpire",
                        "Covenant","Edict","Decree","Mandatum","Doctrine","Orthodoxy","Dogma","Creed"
                    };
                    break;
                case "Spacer":
                    stationtypes = new List<string>
                    {
                        "Scrapheap","Junkpile","Rustbucket","Driftjunk","Wreckstack","DebrisNest","Salvage","Breakyard",
                        "DeadHull","ColdHull","SplitHull","Patchwork","Boltogether","Rivet","LoosePlate","Shearline",
                        "Grind","Backbreak","HardVac","ColdVac","AirLeak","PressureDrop","HullBreach","RedLight",
                        "LastStop","NoReturn","DeadEnd","Nowhere","Fallback","HoldHere","Stallpoint","WaitOne",
                        "Burnout","ShortFuse","Overload","FailSafe","Tripwire","HardStop","Lockup","Crashpoint",
                        "Meatlocker","Bonepile","GraveLot","Killroom","Bleedbay","Deadspace","Darkhole","DeepVoid",
                        "Spine","Ribcage","Skullrack","Gutwork","IronMess","TinCan","Scrapbox","Rusthold"
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

            return $"{stationPart} {letterPart}-{numberPart}";
        }

        public StationNoun(string stationName, string faction, string size)
        {
            string StationID = Guid.NewGuid().ToString().Substring(0, 8);
            
            //Clone the Interior
            var intcell = RetrogradeUtils.CloneCellById("duout02stationtestintcell");
            intcell.EditorID = "Station_int_" + StationID;

            //Create and attack the location to the int cell so we can find it in quests
            var location = new Location(gen_quest_main.myMod)
            {
                EditorID = intcell.EditorID + "_loc",
                LocationCellMarkerReference = new ExtendedList<IFormLinkGetter<IPlacedGetter>>(),
                LocationCellUniqueReferences = new ExtendedList<LocationCellUniqueReference>(),
                LocationCellUniques = new ExtendedList<LocationCellUnique>(),
                LocationCellPersistentReferences = new ExtendedList<LocationReference>(),
                LocationCellStaticReferences = new ExtendedList<LocationCellStaticReference>()

            };
            gen_quest_main.myMod.Locations.Add(location);

            intcell.Location = location.ToNullableLink<ILocationGetter>();

            //Set the Interior door to be linked
            PlacedObject doorreference = null;            
            PlacedObject xmarker = null;

            //Find the markers
            foreach (var persistant in intcell.Persistent)
            {
                if (persistant.EditorID == "duoutstationtestdoor")
                {
                    doorreference = (PlacedObject)persistant;
                    //((PlacedObject)persistant).LinkedReferences[0].Reference =
                }
                if (persistant.EditorID == "intdoorxmarker")
                {
                    xmarker = (PlacedObject)persistant;
                }
            }
            doorreference.LinkedReferences[0].Reference = xmarker.ToLink<ILinkedReferenceGetter>();

            gen_quest_main.myMod.Cells[0].SubBlocks[0].Cells.Add(intcell);

            //Clone the Exterior
            var extcell = RetrogradeUtils.CloneCellById("duout02stationtestextcell");
            extcell.EditorID = "Station_ext_" + StationID;


            //Set the Door to be linked
            ((PlacedObject)extcell.Persistent[0]).LinkedReferences[0].Reference = doorreference.ToLink<ILinkedReferenceGetter>();

            gen_quest_main.myMod.Cells[0].SubBlocks[0].Cells.Add(extcell);


            //Clone the Base Form

            var ship = gen_quest_main.myMod.GenericBaseForms[FormKeyLookup.GetFormKey("duout02_stationtest")].DeepCopy();
            instance = new GenericBaseForm(gen_quest_main.myMod)
            {
                EditorID = "station_form_" + StationID,
                ObjectBounds = ship.ObjectBounds,
                Components = ship.Components,
                ObjectTemplates = ship.ObjectTemplates,
                Template = ship.Template,
                ObjectPlacementDefaults = ship.ObjectPlacementDefaults,                
                ODTY = ship.ODTY,
                STRVs = ship.STRVs,                
            };



            //Set the Exterior and Interior of the base form
            foreach (var component in instance.Components)
            {
                var typestring = component.GetType().ToString();
                if (typestring == "Mutagen.Bethesda.Starfield.FullNameComponent")
                {
                    FullNameComponent fullName = (FullNameComponent)component;
                    fullName.Name = stationName;
                }
                if (typestring == "Mutagen.Bethesda.Starfield.FormLinkDataComponent")
                {
                    FormLinkDataComponent formLinkDataComponent = (FormLinkDataComponent)component;
                    formLinkDataComponent.Links[0].LinkedForm = intcell.ToNullableLink<IStarfieldMajorRecordGetter>();
                    formLinkDataComponent.Links[1].LinkedForm = extcell.ToNullableLink<IStarfieldMajorRecordGetter>();
                }
            }
            gen_quest_main.myMod.GenericBaseForms.Add(instance);



            //Now generate the dungeon....

            StationDungeonGenerator dungeonGenerator = new StationDungeonGenerator();
            dungeonGenerator.GenerateDungeon(intcell, location, faction, size);
        }        
    }
}
