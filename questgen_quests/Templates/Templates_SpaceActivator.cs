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
    internal class Templates_SpaceActivator : TemplateLib
    {
        public Templates_SpaceActivator()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - unguarded",
                Description = "Find info about the target from a clue in orbit around a planet",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                }
            });
            //Crimson Fleet
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet A Class",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Crimson Fleet ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet B Class",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Crimson Fleet ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                }

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Crimson Fleet Cargo",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Crimson Fleet ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Crimson Fleet",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                }

            });
            //Spacer
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Small",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Spacer ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                }

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Large",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Spacer ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Spacer Cargo",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Spacer ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Spacer",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "spacer"
                }
            });

            //Ecliptic
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Small",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Small Ecliptic ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetAClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Large",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Large Ecliptic ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetBClassShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Guarded by Ecliptic Cargo",
                Description = "Find info about the target from a clue in orbit around a planet guarded by a Cargo Ecliptic ship",
                Location = "A clue hidden in orbit around a planet",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_guarded"),
                parameter1 = "Ecliptic",
                parameterformid = ShipTools.GetCargoShip(),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Guard(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                }
            });

            // Traps
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Crimson Fleet Trap",
                Description = "Find info about the target from a clue in orbit around a planet. Crimson Fleet attack when the player uses the beacon",
                Location = "An clue hidden in an asteroid field",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                parameter1 = "Crimson Fleet",
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "crimson_fleet"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Spacer Trap",
                Description = "Find info about the target from a clue in orbit around a planet. Spacer attack when the player uses the beacon",
                Location = "An clue hidden in an asteroid field",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                parameter1 = "Spacer",
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "space"
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Activator - Ecliptic Trap",
                Description = "Find info about the target from a clue in orbit around a planet. Ecliptic attack when the player uses the beacon",
                Location = "An clue hidden in an asteroid field",
                parameter1 = "Ecliptic",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator_trapped"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpace_Trapped(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "ecliptic"
                }
            });
            //-------------------------------  SHOWDOWN ------------------------------------------

        }
    }
}


