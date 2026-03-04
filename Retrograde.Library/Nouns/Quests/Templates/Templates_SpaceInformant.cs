using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Retrograde.Utils;
using Retrograde.SpaceCellDesigns;
using System.Collections.Generic;

namespace Retrograde.Quests
{
    public class Templates_SpaceInformant : TemplateLib
    {
        static readonly (SpaceCellDesignType Design, string LocationSuffix, string NameSuffix)[] SpaceCellVariants =
        [
            (SpaceCellDesignType.Rocky,       "in a rocky asteroid field",     "Rocky Asteroids"),
            (SpaceCellDesignType.Icy,         "in a field of icy asteroids",   "Icy Asteroids"),
            (SpaceCellDesignType.IceShards,   "in a field of ice shards",      "Ice Shards"),
            (SpaceCellDesignType.IceCrystals, "in a field of Ice Crystals",    "IceCrystals"),
            (SpaceCellDesignType.RockShards,  "in a field of rock shards",     "Rock Shards"),
            (SpaceCellDesignType.Wisp,        "in a field of coralline wisps", "Wisps"),
        ];

        public Templates_SpaceInformant()
        {
            DiscoveryTemplates     = [];
            InvestigationTemplates = [];
            ShowdownTemplates      = [];

            // (factionLabel, factionTag, shipDesc, nameSuffix, getShip)
            var groups = new (string FactionLabel, string FactionTag, string ShipDesc, string NameSuffix, Func<uint> GetShip)[]
            {
                ("Crimson Fleet", "crimson_fleet", "A cargo ship",              "Crimson Fleet - cargo",   ShipTools.GetCargoShip),
                ("Crimson Fleet", "crimson_fleet", "A small ship",              "Crimson Fleet - Class A", ShipTools.GetAClassShip),
                ("Crimson Fleet", "crimson_fleet", "A strong medium sized ship","Crimson Fleet - Class B", ShipTools.GetBClassShip),
                ("Spacer",        "spacer",        "A cargo ship",              "Spacer  - cargo",         ShipTools.GetCargoShip),
                ("Spacer",        "spacer",        "A small ship",              "Spacer  - Class A",       ShipTools.GetAClassShip),
                ("Spacer",        "spacer",        "A strong medium sized ship","Spacer  - Class B",       ShipTools.GetBClassShip),
                ("Ecliptic",      "ecliptic",      "A cargo ship",              "Ecliptic - cargo",        ShipTools.GetCargoShip),
                ("Ecliptic",      "ecliptic",      "A small ship",              "Ecliptic - Class A",      ShipTools.GetAClassShip),
                ("Ecliptic",      "ecliptic",      "A strong medium sized ship","Ecliptic - Class B",      ShipTools.GetBClassShip),
            };

            foreach (var (factionLabel, factionTag, shipDesc, nameSuffix, getShip) in groups)
            {
                // Plain entry (no SpaceCell)
                InvestigationTemplates.Add(new MissionTemplate
                {
                    Name        = $"Space Informant - {nameSuffix}",
                    Location    = shipDesc,
                    Description = $"A {factionLabel} ship has the data in there hold",
                    formid      = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                    outlawQuest = new Investigation_Informant_Space(),
                    MissionTags = ["kill_target", "space", factionTag],
                    parameters  = new Dictionary<string, object> { { "NeedSpacesuit", true }, { "Label", factionLabel }, { "FormId", getShip() } },
                });

                // SpaceCell environment variants
                foreach (var (design, locationSuffix, scSuffix) in SpaceCellVariants)
                {
                    InvestigationTemplates.Add(new MissionTemplate
                    {
                        Name        = $"Space Informant - {nameSuffix} {scSuffix}",
                        Location    = $"{shipDesc} {locationSuffix}",
                        Description = $"A {factionLabel} ship has the data in there hold",
                        formid      = FormKeyLookup.GetFormKey("duout_info_space_informant"),
                        outlawQuest = new Investigation_Informant_Space(),
                        MissionTags = ["kill_target", "space", factionTag],
                        Addons      = [],
                        parameters  = new Dictionary<string, object> { { "SpaceCell", design }, { "NeedSpacesuit", true }, { "Label", factionLabel }, { "FormId", getShip() } },
                    });
                }
            }
        }
    }
}
