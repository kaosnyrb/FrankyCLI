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
    public class Templates_Cities_Cydonia : TemplateLib
    {
        public Templates_Cities_Cydonia()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();

            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Outskirts of Cydonia",
                Description = "Find info about the target at the Outskirts of Cydonia",
                Location = "Outskirts of Cydonia",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "cydoniaoutskirts"}, {"FormId", 0x00015FF7} },
                Addons = new List<string>(),

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Cydonia Central Hub",
                Description = "Find info about the target at the Cydonia Central Hub",
                Location = "Cydonia Central Hub",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "cydoniacentralhub"}, {"FormId", 0x00015FF7} },
                Addons = new List<string>(),

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Cydonia Residential Level",
                Description = "Find info about the target at the Cydonia Residential Level",
                Location = "Cydonia Residential Level",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "cydoniaresidential"}, {"FormId", 0x00015FF7} },
                Addons = new List<string>(),

            });

            //-------------------------------  SHOWDOWN ------------------------------------------            
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Outskirts of Cydonia",
                Description = "Kill the target at Outskirts of Cydonia",
                Location = "Outskirts of Cydonia",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_cydonia"),
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "kill_target"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true}, {"Label", "cydoniaoutskirts"}, {"FormId", 0x00015FF7} },
                Addons = new List<string>(),
            });

        }
    }
}

