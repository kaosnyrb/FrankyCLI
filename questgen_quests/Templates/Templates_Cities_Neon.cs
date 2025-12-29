using FrankyCLI.questgen_tools;
using FrankyCLI.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    internal class Templates_Cities_Neon : TemplateLib
    {
        public Templates_Cities_Neon()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();

            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Core",
                Description = "Find info about the target in Neon Core District",
                Location = "Neon Core",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                parameterformid = 0x00015FFE,
                needSpacesuit = false,
                parameter1 = "neoncore",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Ryujin",
                Description = "Find info about the target in Neon Ryujin HQ",
                Location = "Neon Ryujin HQ",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                parameterformid = 0x00015FFE,
                needSpacesuit = false,
                parameter1 = "neonryujin",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Ebbside",
                Description = "Find info about the target in Neon Ebbside",
                Location = "Neon Ebbside",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                parameterformid = 0x00015FFE,
                needSpacesuit = false,
                parameter1 = "neonebbside",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Rooftops",
                Description = "Find info about the target in Neon Rooftops",
                Location = "Neon Rooftops",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                parameterformid = 0x00015FFE,
                needSpacesuit = false,
                parameter1 = "neonrooftops",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Underbelly",
                Description = "Find info about the target in Neon Underbelly",
                Location = "Neon Underbelly",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                parameterformid = 0x00015FFE,
                needSpacesuit = false,
                parameter1 = "neonunderbelly",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Starport",
                Description = "Find info about the target in Neon Starport",
                Location = "Neon Starport",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                parameterformid = 0x00015FFE,
                needSpacesuit = false,
                parameter1 = "neonstarport",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                }
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Astrall Lounge",
                Description = "Find info about the target in Neon Astrall Lounge",
                Location = "Neon Astrall Lounge",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                parameterformid = 0x00015FFE,
                needSpacesuit = false,
                parameter1 = "neonastrallounge",
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                }
            });
            //-------------------------------  SHOWDOWN ------------------------------------------            
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Neon Starport",
                Description = "Kill the target at Neon Starport",
                Location = "Neon Starport",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                needSpacesuit = false,
                parameter1 = "neonstarport",
                parameterformid = 0x00015FFE,
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "kill_target"
                }
            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Neon Underbelly",
                Description = "Kill the target at Neon Underbelly",
                Location = "Neon Underbelly",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                needSpacesuit = false,
                parameter1 = "neonunderbelly",
                parameterformid = 0x00015FFE,
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "kill_target"
                }

            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Neon Ebbside",
                Description = "Kill the target at Neon Ebbside",
                Location = "Neon Ebbside",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                needSpacesuit = false,
                parameter1 = "neonebbside",
                parameterformid = 0x00015FFE,
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "kill_target"
                }

            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Neon Rooftops",
                Description = "Kill the target at Neon Rooftops",
                Location = "Neon Rooftops",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                needSpacesuit = false,
                parameter1 = "neonrooftops",
                parameterformid = 0x00015FFE,
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "kill_target"
                }

            });
        }
    }
}

