using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public static class GangSeedData
    {
        // Neon / Ebbside cyber-noir style (StreetGang)
        public static readonly List<string> StreetGangPrefixes = new List<string>
        {
            "Neon", "Chrome", "Glow", "Pulse", "Glass", "Flux", "Neonwave", "Slip",
            "Shimmer", "Ghost", "Wire", "Ion", "Synth", "Drift", "Blueglass", "Ebb",
            "Shard", "Spire", "Circuit", "Pulse", "Silent", "Neonblack", "Heat",
            "Voltage", "Ether", "Silk", "Static", "Phase", "Neonline", "Deep",
            "Slick", "Glowdust", "Coldlight", "Redline", "Spark", "Vapor",
            "Grime", "Backline", "Razor", "Grid", "Shadow", "Chromatic",
        };

        public static readonly List<string> StreetGangSuffixes = new List<string>
        {
            "Runners", "Crew", "Fangs", "Slicks", "Sisters", "Boys", "Girls", "Collective",
            "Knives", "Serpents", "Cutters", "Rats", "Jackals", "Drifters", "Breakers",
            "Skulls", "Specters", "Wolves", "Pack", "Slicers", "Phantoms", "Synths",
            "Gunners", "Dealers", "Hackers", "Signals", "Ghosts", "Wreckers", "Lot",
            "Sparks", "Rogues", "Runners", "Crew", "Kings", "Line", "Circuit",
            "Vipers", "Strays", "Lowborn", "Ridge", "Slickline", "Loopers",
        };

        public static readonly List<string> StreetGangRoles = new List<string>
        {
            "Lookout", "Runner", "Enforcer", "Breaker", "Trigger", "Scout",
            "Slicer", "Hacker", "Skimmer", "Ghost", "Fixer", "Broker",
            "Cook", "Mule", "Mixer", "Keeper", "Cracker", "Shiv",
            "Handler", "Watcher", "Ripper", "Gunner", "Pusher",
            "Smuggler", "Reaper", "Dealer", "Sentry", "Scout",
            "Breaker", "Patcher", "Drifter", "Sniper",
        };

        // Gritty street + paramilitary style (NamedStreetGang)
        public static readonly List<string> NamedGangPrefixes = new List<string>
        {
            "Red", "Black", "Iron", "Steel", "Rust", "Grim", "Dead", "Broken", "Shadow",
            "Blood", "Night", "Grave", "Ash", "Gutter", "Backstreet", "Lowtown", "Hollow",
            "Cross", "Eastside", "Westside", "Southend", "Northblock", "Dust", "Mud",
            "Scrap", "Brick", "Stone", "Razor", "Chain", "Wire", "Block", "Wasteland",
            "Chrome", "Slag", "Smoke", "Sewer", "Under", "Bleak", "Ridge", "Ironbound",
            "Drift", "Lockjaw", "Blacktop", "Cracked", "Scar", "Vandal", "Pitch",
            "Copper", "Tin", "Lead", "Rot", "Slick", "Grime", "Blight", "Rivet",
            "Forge", "Rusted", "Cold", "Frost", "Burnt", "Charred", "Smolder",
            "Ember", "Thunder", "Storm", "Wild", "Feral", "Nomad", "Stray", "Lone",
            "Bone", "Skull", "Hate", "Vice", "Sorrow", "Dread", "Void", "Wraith",
            "Rumble", "Ruckus", "Scrapper", "Chainlink", "Barbed", "Hellbound", "Crimson",
            "Pale", "Ivory", "Coal", "Shiv", "Needle", "Soot", "Gloom", "Tangle",
            "Vermin", "Slickline", "Ironcore", "Blackwire", "Gravel", "Murk", "Roughcut",
            // Military phonetic alphabet & tactical designators
            "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Gamma", "Hotel",
            "Kilo", "Lima", "Omega", "Sierra", "Tango", "Uniform", "Victor", "Zulu",
            "Squad", "Unit", "Division", "Sector", "Zone", "Company", "Battalion",
            "Tier-One", "Strike", "Recon", "Forward", "Tactical", "Rapid", "Command",
            "Vector", "Grid", "Perimeter", "Outpost", "Protocol", "Cipher", "Directive",
            // Paramilitary / PMC-flavored
            "Blacksite", "Shadowcell", "Darkwatch", "Ironfront", "Redline", "Nightwatch",
            "Warpath", "Overwatch", "Sentinel", "Bulwark", "Vanguard", "Helix", "Crucible",
            "Legion", "Taskforce", "Cerberus", "Executioner", "Skirmish", "Breach",
        };

        public static readonly List<string> NamedGangSuffixes = new List<string>
        {
            "Reapers", "Rats", "Jackals", "Vipers", "Saints", "Devils", "Serpents", "Breakers", "Wolves",
            "Talons", "Mongrels", "Phantoms", "Specters", "Ghouls", "Grinders", "Cutthroats", "Rogues",
            "Drifters", "Raiders", "Bruisers", "Stalkers", "Outcasts", "Ironclaws", "Deadlights", "Bonecrushers",
            "Shadows", "Hollows", "Nightfolk", "Eclipsers", "Backlot Boys", "Dustwalkers", "Ridge Runners",
            "Streetburners", "Ashborn", "Pack", "Crimson Lot", "Black Fangs", "Gravepack", "Scrapwolves",
            "Rubble Rats", "Steel Vipers", "Gutter Kings", "Wastelanders", "Faultliners", "Lowborn",
            "Blackjacks", "Chain Runners", "Shivmasters", "Lockjaw Crew", "Thunder Dogs", "Pit Wolves",
            "Slick Syndicate", "Rust Syndicate", "Dripline Crew", "Backbreaker Union", "Molten Skulls",
            "Needle Boys", "Ironbloods", "Edgewalkers", "Night Wreckers", "Frosthands", "Grime Pact",
            "Razorbacks", "Slagborn", "Grim Company", "Gravel Kings", "Mirefolk", "Dust Devils", "Hellpack",
            "Red Lanterns", "Chrome Fangs", "Wreckrats", "Basement Lords", "Block Runners", "Gutterline",
            "Ash Syndicate", "Crackstone Crew", "Chainlink Mob", "Rustmarks", "Night Chain", "Hollow Sons",
            "Deadwater Crew", "Blight Riders", "Iron Syndicate", "Ravagers", "Hellchain", "Spinebreakers",
            "Rotfangs", "Blackwater Pact", "Rage Unit", "Broken Crown", "Silk Knives", "Rubble Born",
            "Wire Rats", "Torchline", "Gutter Serpents", "Blood Signal", "Shadow Union", "Vermin Pact",
        };
    }
}
