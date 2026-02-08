using FrankyCLI.questgen_tools;
using FrankyCLI.Utils;
using Retrograde.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    internal class Templates_SpaceDestroy : TemplateLib
    {
        public Templates_SpaceDestroy()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - unguarded",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue.",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy"),
                needSpacesuit = true,
                outlawQuest = new Investigation_DestroySpace(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                },
                Addons = new List<string>(),

            });
            //Crimson ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet A Class",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet B Class",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Crimson Fleet Cargo",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "crimson_fleet"
                },
                Addons = new List<string>(),
            });
            //Spacer ------------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Small",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Spacer ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Large",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Spacer ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Spacer Cargo",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Spacer ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "spacer"
                },
                Addons = new List<string>(),
            });
            //Ecliptic
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Small",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Small Ecliptic ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Large",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Large Ecliptic ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Destroy - Guarded by Ecliptic Cargo",
                Description = "Destroy the target in orbit around a planet. It's being used by the target and will give you a clue. Guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_destroy_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_DestroySpace_Guard(),
                MissionTags = new List<string>()
                {
                    "destroy_clue",
                    "space",
                    "ecliptic"
                },
                Addons = new List<string>(),               
            });
            //-------------------------------  SHOWDOWN ------------------------------------------

        }
    }
}


