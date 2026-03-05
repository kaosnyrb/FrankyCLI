using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Retrograde.Utils;
using Retrograde.SpaceCellDesigns;
using System.Collections.Generic;

namespace Retrograde.Quests
{
    public class Templates_SpaceDestroy : TemplateLib
    {
        static readonly (SpaceCellDesignType Design, string Location, string Suffix)[] SpaceCellVariants =
        [
            (SpaceCellDesignType.Rocky,       "a rocky asteroid field around a planet",     "Rocky Asteroids"),
            (SpaceCellDesignType.Icy,         "a field of icy asteroids around a planet",   "Icy Asteroids"),
            (SpaceCellDesignType.IceShards,   "a field of ice shards around a planet",      "Ice Shards"),
            (SpaceCellDesignType.IceCrystals, "a field of Ice Crystals around a planet",    "IceCrystals"),
            (SpaceCellDesignType.RockShards,  "a field of rock shards around a planet",     "Rock Shards"),
            (SpaceCellDesignType.Wisp,        "a field of coralline wisps around a planet", "Wisps"),
        ];

        public Templates_SpaceDestroy()
        {
            DiscoveryTemplates     = [];
            InvestigationTemplates = [];
            ShowdownTemplates      = [];

            // --- Unguarded ---
            AddVariants(
                baseName:     "Space Destroy - unguarded",
                baseDesc:     "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue.",
                questFactory: () => new Investigation_DestroySpace(),
                formidKey:    "duout_info_space_destroy",
                tags:         ["destroy_clue", "space"],
                makeParams:   () => []);

            // --- Guarded: faction × ship class ---
            var guards = new (string FactionLabel, string FactionTag, string NameSuffix, string DescSize, Func<uint> GetShip)[]
            {
                ("Crimson Fleet", "crimson_fleet", "A Class", "Small", ShipSeedData.GetAClassShip),
                ("Crimson Fleet", "crimson_fleet", "B Class", "Large", ShipSeedData.GetBClassShip),
                ("Crimson Fleet", "crimson_fleet", "Cargo",   "Cargo", ShipSeedData.GetCargoShip),
                ("Spacer",        "spacer",        "Small",   "Small", ShipSeedData.GetAClassShip),
                ("Spacer",        "spacer",        "Large",   "Large", ShipSeedData.GetBClassShip),
                ("Spacer",        "spacer",        "Cargo",   "Cargo", ShipSeedData.GetCargoShip),
                ("Ecliptic",      "ecliptic",      "Small",   "Small", ShipSeedData.GetAClassShip),
                ("Ecliptic",      "ecliptic",      "Large",   "Large", ShipSeedData.GetBClassShip),
                ("Ecliptic",      "ecliptic",      "Cargo",   "Cargo", ShipSeedData.GetCargoShip),
            };

            foreach (var (factionLabel, factionTag, nameSuffix, descSize, getShip) in guards)
                AddVariants(
                    baseName:     $"Space Destroy - Guarded by {factionLabel} {nameSuffix}",
                    baseDesc:     $"Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a {descSize} {factionLabel} ship",
                    questFactory: () => new Investigation_DestroySpace_Guard(),
                    formidKey:    "duout_info_space_destroy_guarded",
                    tags:         ["destroy_clue", "space", factionTag],
                    makeParams:   () => new Dictionary<string, object>
                    {
                        { "Label",  factionLabel },
                        { "FormId", getShip() }
                    });
        }

        void AddVariants(
            string baseName, string baseDesc,
            Func<IOutlawQuest> questFactory,
            string formidKey,
            List<string> tags,
            Func<Dictionary<string, object>> makeParams)
        {
            // Plain entry (no SpaceCell)
            InvestigationTemplates.Add(new MissionTemplate
            {
                Name        = baseName,
                Description = baseDesc,
                Location    = "A clue hidden in orbit around a planet",
                formid      = FormKeyLookup.GetFormKey(formidKey),
                outlawQuest = questFactory(),
                MissionTags = tags,
                Addons      = [],
                parameters  = makeParams(),
            });

            // SpaceCell environment variants
            foreach (var (design, location, suffix) in SpaceCellVariants)
            {
                var p = makeParams();
                p["SpaceCell"]     = design;
                p["NeedSpacesuit"] = true;
                InvestigationTemplates.Add(new MissionTemplate
                {
                    Name        = $"{baseName} {suffix}",
                    Description = baseDesc,
                    Location    = $"A clue hidden in {location}",
                    formid      = FormKeyLookup.GetFormKey(formidKey),
                    outlawQuest = questFactory(),
                    MissionTags = tags,
                    Addons      = [],
                    parameters  = p,
                });
            }
        }
    }
}
