using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Retrograde.Utils;
using Retrograde.SpaceCellDesigns;
using System.Collections.Generic;

namespace Retrograde.Quests
{
    public class Templates_Derelicts : TemplateLib
    {
        // SpaceCell suffix appended to the ship location string
        static readonly (SpaceCellDesignType Design, string LocationSuffix, string NameSuffix)[] SpaceCellVariants =
        [
            (SpaceCellDesignType.Rocky,       "in a rocky asteroid field",     "Rocky Asteroids"),
            (SpaceCellDesignType.Icy,         "in a field of icy asteroids",   "Icy Asteroids"),
            (SpaceCellDesignType.IceShards,   "in a field of ice shards",      "Ice Shards"),
            (SpaceCellDesignType.IceCrystals, "in a field of Ice Crystals",    "IceCrystals"),
            (SpaceCellDesignType.RockShards,  "in a field of rock shards",     "Rock Shards"),
            (SpaceCellDesignType.Wisp,        "in a field of coralline wisps", "Wisps"),
        ];

        public Templates_Derelicts()
        {
            DiscoveryTemplates     = [];
            InvestigationTemplates = [];
            ShowdownTemplates      = [];

            // (nameSuffix, shipDesc, factionLabel, factionTag, getShip)
            var groups = new (string NameSuffix, string ShipDesc, string FactionLabel, string FactionTag, Func<uint> GetShip)[]
            {
                ("UC Navy Cargo",             "UC Navy Cargo Ship",                      "UC Navy",            "uc_navy",            ShipTools.GetCargoShip),
                ("UC Navy Ship A",            "UC Navy Light Scout Ship",                "UC Navy",            "uc_navy",            ShipTools.GetAClassShip),
                ("UC Navy Ship B",            "UC Navy Large Combat Ship",               "UC Navy",            "uc_navy",            ShipTools.GetBClassShip),
                ("UC Vanguard Ship A",        "UC Vanguard Light Scout Ship",            "UC Vanguard",        "uc_vanguard",        ShipTools.GetAClassShip),
                ("UC Vanguard Ship B",        "UC Vanguard Large Combat Ship",           "UC Vanguard",        "uc_vanguard",        ShipTools.GetBClassShip),
                ("UC SysDef Ship A",          "UC SysDef Light Tactical Ship",           "UC SysDef",          "uc_sysdef",          ShipTools.GetAClassShip),
                ("UC SysDef Ship B",          "UC SysDef Large Combat Ship",             "UC SysDef",          "uc_sysdef",          ShipTools.GetBClassShip),
                ("Freestar Security Cargo",   "Freestar Security Cargo Ship",            "Freestar Security",  "freestar_security",  ShipTools.GetCargoShip),
                ("Freestar Security Ship A",  "Freestar Security Light Patrol Ship",     "Freestar Security",  "freestar_security",  ShipTools.GetAClassShip),
                ("Freestar Security Ship B",  "Freestar Security Large Combat Ship",     "Freestar Security",  "freestar_security",  ShipTools.GetBClassShip),
                ("Trade Authority Cargo",     "Trade Authority Cargo Ship",              "Trade Authority",    "trade_authority",    ShipTools.GetCargoShip),
                ("Trade Authority Ship A",    "Trade Authority Fast Courier Ship",       "Trade Authority",    "trade_authority",    ShipTools.GetAClassShip),
                ("Trade Authority Ship B",    "Trade Authority Large Support Ship",      "Trade Authority",    "trade_authority",    ShipTools.GetBClassShip),
                ("Galbank Cargo",             "Galbank Vault Ship",                      "Galbank",            "galbank",            ShipTools.GetCargoShip),
                ("Galbank Ship A",            "Galbank Contract Transport Ship",         "Galbank",            "galbank",            ShipTools.GetAClassShip),
                ("Galbank Ship B",            "Galbank Large Investment Protection Ship","Galbank",            "galbank",            ShipTools.GetBClassShip),
                ("Trackers Alliance Ship A",  "Trackers Alliance Bounty Tracker Ship",   "Trackers Alliance",  "trackers_alliance",  ShipTools.GetAClassShip),
                ("Trackers Alliance Ship B",  "Trackers Alliance Bounty Hunter Ship",    "Trackers Alliance",  "trackers_alliance",  ShipTools.GetBClassShip),
            };

            foreach (var (nameSuffix, shipDesc, factionLabel, factionTag, getShip) in groups)
            {
                // Plain entry (no SpaceCell)
                InvestigationTemplates.Add(new MissionTemplate
                {
                    Name        = $"Space Derelict - {nameSuffix}",
                    Location    = $"A {shipDesc}",
                    Description = "A Derelict ship contains the clue",
                    formid      = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                    outlawQuest = new Investigation_Derelict_Space(),
                    MissionTags = ["space", "derelict_ship", "follow_clue", factionTag],
                    parameters  = new Dictionary<string, object> { { "NeedSpacesuit", true }, { "Label", factionLabel }, { "FormId", getShip() } },
                });

                // SpaceCell environment variants
                foreach (var (design, locationSuffix, scSuffix) in SpaceCellVariants)
                {
                    InvestigationTemplates.Add(new MissionTemplate
                    {
                        Name        = $"Space Derelict - {nameSuffix} {scSuffix}",
                        Location    = $"A {shipDesc} {locationSuffix}",
                        Description = "A Derelict ship contains the clue",
                        formid      = FormKeyLookup.GetFormKey("duout_info_space_derelict"),
                        outlawQuest = new Investigation_Derelict_Space(),
                        MissionTags = ["space", "derelict_ship", "follow_clue", factionTag],
                        Addons      = [],
                        parameters  = new Dictionary<string, object> { { "SpaceCell", design }, { "NeedSpacesuit", true }, { "Label", factionLabel }, { "FormId", getShip() } },
                    });
                }
            }
        }
    }
}
