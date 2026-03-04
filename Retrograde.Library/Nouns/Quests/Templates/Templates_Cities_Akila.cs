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
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "akilafarms"}, {"FormId", 0x00010DFB} },
                                Addons = new List<string>(),

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Akila City Walls",
                Description = "Find info about the target in Akila City Wall",
                Location = "Akila City Walls",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "akilawalls"}, {"FormId", 0x00010DFB} },
                                Addons = new List<string>(),

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Akila City Coe Plaza",
                Description = "Find info about the target in Akila City, Coe Plaza",
                Location = "Akila City Coe Plaza",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "akilacoeplaza"}, {"FormId", 0x00010DFB} },
                                Addons = new List<string>(),

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Akila City The Stretch",
                Description = "Find info about the target in Akila City, The Stretch",
                Location = "Akila City The Stretch",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "akilathestretch"}, {"FormId", 0x00010DFB} },
                                Addons = new List<string>(),

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Akila City The Core",
                Description = "Find info about the target in Akila City, The Core",
                Location = "Akila City The Core",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "akilathecore"}, {"FormId", 0x00010DFB} },
                                Addons = new List<string>(),

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Akila City Midtown",
                Description = "Find info about the target in Akila City, Midtown",
                Location = "Akila City Midtown",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "akilamidtown"}, {"FormId", 0x00010DFB} },
                                Addons = new List<string>(),

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Akila City Spaceport",
                Description = "Find info about the target in Akila City, Spaceport",
                Location = "Akila City Spaceport",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "akilaspaceport"}, {"FormId", 0x00010DFB} },
                                Addons = new List<string>(),

            });
            //-------------------------------  SHOWDOWN ------------------------------------------

            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Akila City Farms",
                Description = "Kill the target at Akila City Farms",
                Location = "Akila City Farms",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "kill_target"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "akilafarms"}, {"FormId", 0x00010DFB} },
                                Addons = new List<string>(),

            });
        }
    }
}

