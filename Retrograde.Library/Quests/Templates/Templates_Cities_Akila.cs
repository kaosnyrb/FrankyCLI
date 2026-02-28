using Retrograde.Utils;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Quests
{
    public class Templates_Cities_Akila : TemplateLib
    {
        public Templates_Cities_Akila()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------
            
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Akila City Farms",
                Description = "Find info about the target in Akila City Farms",
                Location = "Akila City Farms",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                parameterformid = 0x00010DFB,
                needSpacesuit = false,
                parameter1 = "akilafarms",
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
                Name = "City Activator - Akila City Walls",
                Description = "Find info about the target in Akila City Wall",
                Location = "Akila City Walls",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                parameterformid = 0x00010DFB,
                needSpacesuit = false,
                parameter1 = "akilawalls",
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
                Name = "City Activator - Akila City Coe Plaza",
                Description = "Find info about the target in Akila City, Coe Plaza",
                Location = "Akila City Coe Plaza",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                parameterformid = 0x00010DFB,
                needSpacesuit = false,
                parameter1 = "akilacoeplaza",
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
                Name = "City Activator - Akila City The Stretch",
                Description = "Find info about the target in Akila City, The Stretch",
                Location = "Akila City The Stretch",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                parameterformid = 0x00010DFB,
                needSpacesuit = false,
                parameter1 = "akilathestretch",
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
                Name = "City Activator - Akila City The Core",
                Description = "Find info about the target in Akila City, The Core",
                Location = "Akila City The Core",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                parameterformid = 0x00010DFB,
                needSpacesuit = false,
                parameter1 = "akilathecore",
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
                Name = "City Activator - Akila City Midtown",
                Description = "Find info about the target in Akila City, Midtown",
                Location = "Akila City Midtown",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                parameterformid = 0x00010DFB,
                needSpacesuit = false,
                parameter1 = "akilamidtown",
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
                Name = "City Activator - Akila City Spaceport",
                Description = "Find info about the target in Akila City, Spaceport",
                Location = "Akila City Spaceport",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                parameterformid = 0x00010DFB,
                needSpacesuit = false,
                parameter1 = "akilaspaceport",
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
                Name = "City Bounty - Akila City Farms",
                Description = "Kill the target at Akila City Farms",
                Location = "Akila City Farms",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                needSpacesuit = false,
                parameter1 = "akilafarms",
                parameterformid = 0x00010DFB,
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

