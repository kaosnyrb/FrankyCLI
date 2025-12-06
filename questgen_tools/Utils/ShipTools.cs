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

        public static uint GetAClassShip()
        {
            //Note  the faction ones seem to work but the manufactor ones don't
            Random random = new Random();
            List<uint> shiplist = new List<uint>()
            {
                0x0031DF00, // EncShip_CrimsonFleet_A_Ghost01 [GBFM:0031DF00]
                0x0031DF60, // EncShip_CrimsonFleet_A_Haunt01 [GBFM:0031DF60]
                0x00322BE1, // EncShip_CrimsonFleet_A_Phantom01 [GBFM:00322BE1]
                0x003889AD, // EncShip_UCSecurity_A_Dagger01 [GBFM:003889AD]
                0x003889E7, // EncShip_UCSysDef_A_Gladius01 [GBFM:003889E7]
                0x00187C77, // EncShip_UCNavy_A_Longsword01 [GBFM:00187C77]
                0x00322BF7, // EncShip_Ecliptic_A_Falcata01 [GBFM:00322BF7]
                0x00322BF1, // EncShip_Ecliptic_A_Stiletto01 [GBFM:00322BF1]
                0x002E74A4, // EncShip_FreestarCitizen_A_Kfir01 [GBFM:002E74A4]
                0x002E749E, // EncShip_FreestarCitizen_A_Discovery [GBFM:002E749E]
                0x002E762D, // EncShip_FreestarCitizen_A_Mustang01 [GBFM:002E762D]
                0x0004237F, // EncShip_FreestarCitizen_A_Narcissus01 [GBFM:0004237F]
                0x0018D3D5, // EncShip_FreestarCitizen_A_Responder01 [GBFM:0018D3D5]
                0x000423AB, // EncShip_TheFirst_A_Thresher01 [GBFM:000423AB]
                0x000D0AA9, // EncShip_TheFirst_A_Wendigo01 [GBFM:000D0AA9]
                0x00042376, // EncShip_UCCitizen_A_Rambler01 [GBFM:00042376]
                0x0004238B, // EncShip_FreestarSecurity_A_Railstar01 [GBFM:0004238B]
                0x00322C13, // EncShip_Spacer_A_Raccoon01 [GBFM:00322C13]
                0x00322C15, // EncShip_Spacer_A_Raven01 [GBFM:00322C15]
                0x00322C17, // EncShip_Spacer_A_Scarab01 [GBFM:00322C17]
            };


            return shiplist[random.Next(shiplist.Count)];
        }

        public static uint GetBClassShip()
        {
            //Note  the faction ones seem to work but the manufactor ones don't
            Random random = new Random();
            List<uint> shiplist = new List<uint>()
            {
                0x00322BE9, // EncShip_CrimsonFleet_B_Banshee01 [GBFM:00322BE9]
                0x00322BEB, // EncShip_CrimsonFleet_B_Reaper01 [GBFM:00322BEB]
                0x00322BED, // EncShip_CrimsonFleet_B_Specter01 [GBFM:00322BED]
                0x00322BFF, // EncShip_Ecliptic_B_Cutlass01 [GBFM:00322BFF]
                0x00322C03, // EncShip_Ecliptic_B_Rapier01 [GBFM:00322C03]
                0x00322C07, // EncShip_Ecliptic_B_Scimitar01 [GBFM:00322C07]
                0x002E7530, // EncShip_FreestarCitizen_B_Dolphin01 [GBFM:002E7530]
                0x002E7611, // EncShip_FreestarCitizen_B_Ranger01 [GBFM:002E7611]
                0x0002C201, // EncShip_FreestarCitizen_B_Slipstream01 [GBFM:0002C201]
                0x0018D3DD, // EncShip_FreestarCitizen_B_Venture01 [GBFM:0018D3DD]
                0x00388977, // EncShip_FreestarSecurity_B_Falcon01 [GBFM:00388977]
                0x00388985, // EncShip_FreestarSecurity_B_Ranger01 [GBFM:00388985]
                0x0030ED45, // EncShip_Galbank_B_AsphaltCB01 [GBFM:0030ED45]
                0x00323454, // EncShip_HouseVaruun_B_Eulogy01 [GBFM:00323454]
                0x00322C19, // EncShip_Spacer_B_Coyote01 [GBFM:00322C19]
                0x00322C1B, // EncShip_Spacer_B_Jackal01 [GBFM:00322C1B]
                0x00322C1D, // EncShip_Spacer_B_Vulture01 [GBFM:00322C1D]
                0x0002C88F, // EncShip_TheFirst_B_Zumwalt01 [GBFM:0002C88F]
                0x0002C75D, // EncShip_TheFirst_B_Combat_Pterosaur01 [GBFM:0002C75D]
                0x00331347, // EncShip_TrackersAlliance_B_Naginata01 [GBFM:00331347]
                0x001E5E76, // EncShip_UCNavy_B_Bireme01 [GBFM:001E5E76]
                0x001E5E79, // EncShip_UCNavy_B_Celestial01 [GBFM:001E5E79]
                0x001E5E7B, // EncShip_UCNavy_B_Hoplite01 [GBFM:001E5E7B]
                0x001E5E7D, // EncShip_UCNavy_B_Nebula01 [GBFM:001E5E7D]
                0x001E5E7F, // EncShip_UCNavy_B_Phalanx01 [GBFM:001E5E7F]
            };
            return shiplist[random.Next(shiplist.Count)];
        }

        public static string GetFactionShipName(string id)
        {
            string shipname = ShipTools.GetShipName();

            switch (id)
            {
                case "UC Navy":
                    shipname = GetUCShipCode();
                    break;
                case "UC Vanguard":
                    shipname = GetUCShipCode();
                    break;
                case "UC SysDef":
                    shipname = GetUCShipCode();
                    break;
                case "Freestar Security":
                    shipname = GetFreestarCodeName();
                    break;
                case "Trackers Alliance":
                    shipname = GetFreestarCodeName();
                    break;
                case "Galbank":
                    shipname = GetGalBankCodeName();
                    break;
                case "Crimson Fleet":
                    shipname = GetCrimsonFleetCodeName();
                    break;
                case "Spacer":
                    shipname = GetSpacerCodeName();
                    break;
                case "Ecliptic":
                    shipname = GetMercenaryCodeName();
                    break;
                case "Trade Authority":
                    shipname = GetTradeAuthoritySailingCodeName();
                    break;

            }
            return shipname;
        }

        public static string GetTradeAuthoritySailingCodeName()
        {
            Random random = new Random();

            // Old-world merchant & sailing-inspired ship names (single word, classy)
            List<string> singleWordNames = new List<string>
            {
                "Windward", "Starborne", "Wayfarer", "Goldenwake", "Mariner",
                "Seafarer", "Voyager", "Windcrier", "Sunchaser", "Tidebreaker",
                "Silverwake", "Harbormark", "Tradewind", "Northstar", "Indenture",
                "Merchantine", "Seafoam", "Gildedwave", "Longshore", "Brinefall",
                "Tidewalker", "Starclipper", "Windcrest", "Oceancrest", "Brightwake",
                "Saltbrand", "Seamender", "Starwharf", "Galeheart", "Windcrest",
                "Deepwater", "Wavecrest", "Hullmark", "Widebeam", "Ironkeel",
                "Sovereign", "Goldenspar", "Starbrig", "Freewater", "Skyclipper"
            };

            // Trade Authority codes
            List<string> codePrefixes = new List<string>
            {
                "TA", // Trade Authority
                "TB", // Trade Bureau
                "TR", // Trade Registry
                "LT", // Logistics Transport
                "CT", // Cargo Transit
                "AX"  // Authority Exchange
            };

            int number = random.Next(10, 9999);

            return $"{singleWordNames[random.Next(singleWordNames.Count)]} {codePrefixes[random.Next(codePrefixes.Count)]}-{number}";
        }


        public static string GetMercenaryCodeName()
        {
            Random random = new Random();

            // Tactical, military-style code prefixes
            List<string> codePrefixes = new List<string>
            {
                "MX", // Mercenary X Division
                "VR", // Vanguard Recon
                "KT", // Kill Team
                "HX", // Helix Unit
                "OD", // Onyx Division
                "ST", // Strike Team
                "BL", // Blacklight
                "AR", // Aegis Regiment
                "GS", // Ghost Squad
                "RF"  // Red Force
            };

            // Professional, sleek, aggressive single-word names
            List<string> singleWordNames = new List<string>
            {
                "Blacktalon", "Ironstrike", "Ghostline", "Nightblade", "Steelshade",
                "Warhawk", "Shadowfall", "Hardpoint", "Crossfire", "Deadlock",
                "Viperline", "Stormpoint", "Grimshot", "Helixborn", "Redstrike",
                "Starblade", "Lanceredge", "Darkwolf", "Nullfire", "Specterborn",
                "Warshade", "Ghostforge", "Talonfire", "Blackstorm", "Razorwind",
                "Ironshard", "Strikefall", "Nightcore", "Steelbrand", "Shadowwing",
                "Bloodsteel", "Edgefire", "Razerclaw", "Crossline", "Stormlance",
                "Silentedge", "Frostblade", "Shockwave", "Voidbrand", "Gunspire"
            };

            int number = random.Next(10, 9999);

            return $"{codePrefixes[random.Next(codePrefixes.Count)]}-{number} {singleWordNames[random.Next(singleWordNames.Count)]}";
        }


        public static string GetShipName()
        {
            Random random = new Random();
            List<string> spaceshipPrefixes = new List<string>
            {
                "Star", "Nova", "Nebula", "Galaxy", "Cosmic", "Solar", "Lunar", "Quantum", "Stellar",
                "Astro", "Meteor", "Comet", "Ion", "Photon", "Plasma", "Warp", "Hyper", "Proto", "Omega",
                "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Sigma", "Lambda", "Zenith", "Apex",
                "Radiant", "Infinite", "Eternal", "Celestial", "Astral", "Void", "NovaCore", "DarkStar", "Bright",
                "Vortex", "Chrono", "Mythic", "Eon", "Holo", "Spectra", "Titan", "Orion", "Pulsar", "Aurora",
                "Fusion", "Nebulon", "Aether", "Prime", "Vector", "Vanguard", "Vesper", "Tempest", "Ignis",
                "Drift", "Pyre", "Crystal", "Echo", "Pulse", "Circuit", "Arc", "Cyber", "Nano", "Mega",
                "Ultra", "Micro", "Macro", "Astris", "Nimbus", "Thunder", "Storm", "Phoenix", "Dragon",
                "Seraph", "Vertex", "Singularity", "Halo", "Ionis", "Specter", "Shadow", "Frost", "Ember",
                "Radiance", "Spectrum", "Horizon", "Cosmos", "Starlance", "Astrion", "Solaris", "Equinox",
                "Monolith", "Zen", "Genesis", "Frontier", "Pioneer", "EclipseCore", "Velocity", "Nebulite"
            };
            List<string> spaceshipSuffixes = new List<string>
            {
                "Voyager", "Runner", "Spear", "Falcon", "Hawk", "Drifter", "Carrier", "Cruiser", "Ranger",
                "Explorer", "Warrior", "Sentinel", "Guardian", "Hunter", "Pioneer", "Prospector", "Trader",
                "Nomad", "Lancer", "Strider", "Wanderer", "Harbinger", "Ascendant", "Venture", "Breaker",
                "Wing", "Claw", "Ray", "Beacon", "Frontier", "Comet", "Trailblazer", "Traverse", "Seeker",
                "Shadow", "Command", "Legacy", "Titan", "Voyage", "Impact", "Conquest", "Discovery",
                "Odyssey", "Pilgrim", "Specter", "Echo", "Vanguard", "Pulse", "Chaser", "Dragon", "Phoenix",
                "Starship", "Cruise", "Engine", "Rider", "Vector", "Pathfinder", "Tempest", "Striker", "Stalker",
                "Wingblade", "Forge", "Harrier", "Reclaimer", "Interceptor", "Marauder", "Navigator",
                "Battleship", "Destroyer", "Frigate", "Skylance", "Storm", "Cycler", "Ascension", "Authority",
                "Circuit", "Frequency", "Equinox", "Nebulon", "Starlance", "Echohawk", "Skyrider", "Warpwing",
                "Revenant", "Eternity", "Obsidian", "Constellation", "Continuum", "Prospect", "Arc",
                "Dominion", "ReclaimerX", "Pathway", "Surge", "Vortex", "Helix", "Prime", "Horizon", "VectorX"
            };
            return spaceshipPrefixes[random.Next(spaceshipPrefixes.Count)] + " " + spaceshipSuffixes[random.Next(spaceshipSuffixes.Count)];
        }

        public static string GetSpacerCodeName()
        {
            Random random = new Random();

            // Scavenger / bandit-style prefixes
            List<string> codePrefixes = new List<string>
            {
                "SP", // Spacers
                "SB", // Scrap Bandits
                "JT", // Junk Trawlers
                "GX", // Grease X
                "RD", // Rust Devils
                "BR", // Bolt Runners
                "SL", // Scrap Lords
                "WK"  // Wreckers
            };

            // Single-word gritty, junker, scavenger ship names
            List<string> singleWordNames = new List<string>
            {
                "Scrapfang", "Rustburner", "Junkrider", "Boltshank", "Grimejaw",
                "Wreckborn", "Filthrunner", "Trashfire", "Scrapmaw", "Rusthound",
                "Grindcore", "Crackspine", "Bashstar", "Pitfall", "Irontrash",
                "Grungeblade", "Dustshank", "Junklash", "Metalgrit", "Shatterjaw",
                "Ragskull", "Sootclaw", "Gutterwind", "Wastemaul", "Rivetstrike",
                "Rustclaw", "Scraprider", "Dirtywing", "Gravelshot", "Ironjunk",
                "Mashrider", "Gunkfang", "Trashfang", "Ruststalker", "Grindfang",
                "Scrapviper", "Wretchborn", "Boltbreaker", "Dirtmaw", "Smogshard"
            };

            int number = random.Next(10, 9999);

            return $"{codePrefixes[random.Next(codePrefixes.Count)]}-{number} {singleWordNames[random.Next(singleWordNames.Count)]}";
        }

        public static string GetFreestarCodeName()
        {
            Random random = new Random();

            // Military-style prefixes for Freestar
            List<string> codePrefixes = new List<string>
            {
                "FS", "FR", "FC", "WC", "LF", "RG", "MD", "PR"
            };

            // Single-word, space-cowboy, frontier-style ship names
            List<string> singleWordNames = new List<string>
            {
                "Dustrunner", "Ironwind", "Sundrifter", "Lonerider", "Redhawk",
                "Coyoteheart", "Brimstone", "Mustang", "Trailborn", "Dustfall",
                "Wildshot", "Longhorn", "Starforge", "Rusthoof", "Badlander",
                "Highnoon", "Gritstone", "Sunchaser", "Buckshot", "Outrider",
                "Skybronco", "Dustforge", "Sunstalker", "Firetrail", "Sablestride",
                "Wildbrand", "Redsun", "Irontrail", "Loneforge", "Mesaheart",
                "Starstallion", "Ashrider", "Windbrand", "Prairiefire", "Dustlance",
                "Goldspur", "Starrodeo", "Thornsteel", "Dawnbreak", "Ironspur"
            };

            // Number: 10–9999
            int number = random.Next(10, 9999);

            return $"{codePrefixes[random.Next(codePrefixes.Count)]}-{number} {singleWordNames[random.Next(singleWordNames.Count)]}";
        }

        public static string GetUCShipCode()
        {
            Random random = new Random();

            // Military-style prefix letters
            List<string> shipPrefixes = new List<string>
            {
                "FT", "XR", "NV", "TS", "RG", "VT", "HM", "SK",
                "DF", "CW", "KN", "RS", "OM", "LX", "GX", "BL",
                "ZT", "PX", "FX", "CM", "AP", "GT", "HS", "JN",
                "QR", "ST", "VG", "WK", "YP", "ZC", "MK", "DH"
            };

            // Generate a 2–4 digit number
            int number = random.Next(10, 9999);

            return $"{shipPrefixes[random.Next(shipPrefixes.Count)]}-{number}";
        }

        public static string GetCrimsonFleetCodeName()
        {
            Random random = new Random();

            // Aggressive, outlaw-style prefixes suitable for Crimson Fleet
            List<string> codePrefixes = new List<string>
            {
                "CF",  // Crimson Fleet
                "CR",  // Crimson Raiders
                "RS",  // Red Sabers
                "BL",  // Bloodline
                "FM",  // Fleet Marauders
                "DS",  // Deadstar
                "VK",  // Void Killers
                "BR"   // Blood Reavers
            };

            // Single-word pirate names
            List<string> singleWordNames = new List<string>
            {
                "Bloodreaver", "Skullrender", "Voidfang", "Ironmaw", "Redhowl",
                "Starcutlass", "Darkstrike", "Bloodwake", "Nightraider", "Riftclaw",
                "Scarrunner", "Dreadskull", "Havocborn", "Hellwake", "Gunmaw",
                "Crimsonfang", "Ragewing", "Emberblade", "Killstar", "Voidbreaker",
                "Boneclaw", "Cutlassfire", "Maraudstar", "Darklance", "Stormreaver",
                "Riftblade", "Flameskull", "Viperwind", "Bloodshot", "Gravespine",
                "Deadfall", "Ravageborn", "Ironreaper", "Warhound", "Bloodtrail",
                "Shadowreaver", "Riftbane", "Starwrecker", "Ironskull"
            };

            int number = random.Next(10, 9999);

            return $"{codePrefixes[random.Next(codePrefixes.Count)]}-{number} {singleWordNames[random.Next(singleWordNames.Count)]}";
        }


        public static string GetGalBankCodeName()
        {
            Random random = new Random();

            // Corporate / banking military-style prefixes
            List<string> codePrefixes = new List<string>
            {
                "GB", "GC", "GF", "GL", "GA", "BX", "CB", "CR"  // GalBank, Credit Bureau, Credit Reserve, etc.
            };

            // Single-word corporate banking-style names
            List<string> singleWordNames = new List<string>
            {
                "Vaultguard", "Credshield", "Fundrunner", "Mintline", "Bondrider",
                "Securehaul", "Depositrail", "Ledgerstar", "Goldshift", "Coinlance",
                "Trustwave", "Cashflow", "Quantisec", "Finline", "Creditmark",
                "Bankrider", "Depositron", "Primesec", "Safewing", "Yieldcore",
                "Stockwind", "Vaultborne", "Fundshift", "Mintward", "Goldvector",
                "Assetguard", "Capitalwing", "Shareline", "Trustforge", "Bondshift",
                "Vaultline", "Profitstar", "Cashrunner", "Lockwind", "Currency",
                "Mintforge", "Dividend", "Creditforge", "Trustsphere", "Coinshift"
            };

            // Number 10–9999
            int number = random.Next(10, 9999);

            return $"{codePrefixes[random.Next(codePrefixes.Count)]}-{number} {singleWordNames[random.Next(singleWordNames.Count)]}";
        }



        public static uint GetFactionID(string faction)
        {
            switch (faction)
            {
                case "Crimson Fleet":
                    return 0x000B1375;
                case "Spacer":
                    return 0x000B13A8;
                case "Ecliptic":
                    return 0x000AE4F3;
                case "UC Navy":
                    return 0x000D320E;
                case "UC Vanguard":
                    return 0x000D1859;
                case "Freestar Security":
                    return 0x000CA78D;
                case "UC SysDef":
                    return 0x000DBC51;
                case "Trade Authority":
                    return 0x000AE4D0;
                case "Galbank":
                    return 0x0034BB12;
                case "Trackers Alliance":
                    return 0x000AE4D3;

            }
            return 0;
        }

        //LShip_UCNavy_Cargo [LVLB:000D320F]
        public static IFormLink<IStarfieldMajorRecordGetter> GetGangList(uint ShipFaction)
        {
            string ganglistEditorID = "";
            switch(ShipFaction)
            {
                case 0x000AE4F3: //LShip_Ecliptic_Template [LVLB:000AE4F3]
                    ganglistEditorID = "duout_GangMembersList_Space_Ecliptic";
                    break;
                case 0x000B1375: //LShip_CrimsonFleet_Template [LVLB:000B1375]
                    ganglistEditorID = "duout_GangMembersList_Space_Crimsonfleet";
                    break;
                case 0x000B13A8: //LShip_Spacer_Template [LVLB:000B13A8]
                    ganglistEditorID = "duout_GangMembersList_Space_Spacer";
                    break;
                default:
                    ganglistEditorID = "duout_GangMembersList_Space_Spacer";
                    break;
            }

            foreach (var record in gen_quest_main.myMod.EnumerateMajorRecords())
            {
                if (record.EditorID != null)
                {
                    if (record.EditorID.Contains(ganglistEditorID))
                    {
                        return record.ToLink<IStarfieldMajorRecordGetter>();
                    }
                }
            }
            return null;
        }
    }
}
