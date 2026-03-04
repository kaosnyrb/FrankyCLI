using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Retrograde.Utils;
using Retrograde.SpaceCellDesigns;
using System.Collections.Generic;

namespace Retrograde.Quests
{
    public class Templates_SpaceActivator : TemplateLib
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

        public Templates_SpaceActivator()
        {
            DiscoveryTemplates     = [];
            InvestigationTemplates = [];
            ShowdownTemplates      = [];

            // --- Unguarded ---
            AddVariants(
                baseName:     "Space Activator - unguarded",
                baseDesc:     "Find info about the target from a clue in orbit around a planet",
                questFactory: () => new Investigation_ActivatorSpace(),
                formidKey:    "duout_info_space_activator",
                tags:         ["follow_clue", "space"],
                makeParams:   () => new Dictionary<string, object> { { "NeedSpacesuit", true } });

            // --- Guarded: faction × ship class ---
            var guards = new (string FactionLabel, string FactionTag, string NameSuffix, string DescSize, Func<uint> GetShip)[]
            {
                ("Crimson Fleet", "crimson_fleet", "A Class", "Small", ShipTools.GetAClassShip),
                ("Crimson Fleet", "crimson_fleet", "B Class", "Large", ShipTools.GetBClassShip),
                ("Crimson Fleet", "crimson_fleet", "Cargo",   "Cargo", ShipTools.GetCargoShip),
                ("Spacer",        "spacer",        "Small",   "Small", ShipTools.GetAClassShip),
                ("Spacer",        "spacer",        "Large",   "Large", ShipTools.GetBClassShip),
                ("Spacer",        "spacer",        "Cargo",   "Cargo", ShipTools.GetCargoShip),
                ("Ecliptic",      "ecliptic",      "Small",   "Small", ShipTools.GetAClassShip),
                ("Ecliptic",      "ecliptic",      "Large",   "Large", ShipTools.GetBClassShip),
                ("Ecliptic",      "ecliptic",      "Cargo",   "Cargo", ShipTools.GetCargoShip),
            };

            foreach (var (factionLabel, factionTag, nameSuffix, descSize, getShip) in guards)
                AddVariants(
                    baseName:     $"Space Activator - Guarded by {factionLabel} {nameSuffix}",
                    baseDesc:     $"Find info about the target from a clue in orbit around a planet guarded by a {descSize} {factionLabel} ship",
                    questFactory: () => new Investigation_ActivatorSpace_Guard(),
                    formidKey:    "duout_info_space_activator_guarded",
                    tags:         ["follow_clue", "space", factionTag],
                    makeParams:   () => new Dictionary<string, object>
                    {
                        { "NeedSpacesuit", true },
                        { "Label",         factionLabel },
                        { "FormId",        getShip() }
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
