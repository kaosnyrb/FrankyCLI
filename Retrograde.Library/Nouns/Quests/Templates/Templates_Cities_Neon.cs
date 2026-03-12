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
    public class Templates_Cities_Neon : TemplateLib
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
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "neoncore"}, {"FormId", 0x00015FFE} },
                Addons = new List<string>(),
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Ryujin",
                Description = "Find info about the target in Neon Ryujin HQ",
                Location = "Neon Ryujin HQ",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "neonryujin"}, {"FormId", 0x00015FFE} },
                Addons = new List<string>(),
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Ebbside",
                Description = "Find info about the target in Neon Ebbside",
                Location = "Neon Ebbside",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "neonebbside"}, {"FormId", 0x00015FFE} },
                Addons = new List<string>(),
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Rooftops",
                Description = "Find info about the target in Neon Rooftops",
                Location = "Neon Rooftops",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "neonrooftops"}, {"FormId", 0x00015FFE} },
                Addons = new List<string>(),
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Underbelly",
                Description = "Find info about the target in Neon Underbelly",
                Location = "Neon Underbelly",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "neonunderbelly"}, {"FormId", 0x00015FFE} },
                Addons = new List<string>(),
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Starport",
                Description = "Find info about the target in Neon Starport",
                Location = "Neon Starport",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "neonstarport"}, {"FormId", 0x00015FFE} },
                Addons = new List<string>(),
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Activator - Neon Astrall Lounge",
                Description = "Find info about the target in Neon Astrall Lounge",
                Location = "Neon Astrall Lounge",
                formid = FormKeyLookup.GetFormKey("duout_info_city_activator_neon"),
                outlawQuest = new Investigation_ActivatorCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "follow_clue"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "neonastrallounge"}, {"FormId", 0x00015FFE} },
                Addons = new List<string>(),
            });
            //Conversations
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "City Conversation - Neon Starport Informant",
                Description = "Speak to a Mechanic at the Neon Starport about the target",
                Location = "Neon Starport",
                formid = FormKeyLookup.GetFormKey("duout_info_city_conversation_neon"),
                outlawQuest = new Investigation_ConversationCity(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "conversation",                    
                    "planetside",
                    "city",
                },

                parameters = new Dictionary<string, object>() { 
                    {"NeedSpacesuit", false},
                    {"Label", "neonstarport"}
                },
                Addons = new List<string>()
                {
                    "Target worked on a ship the target was on and overheard something key.",
                    "This character isn't really interested in the target and just wants the player to leave them alone",
                    
                },
                
            });

            //-------------------------------  SHOWDOWN ------------------------------------------            
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Neon Starport",
                Description = "Kill the target at Neon Starport",
                Location = "Neon Starport",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "kill_target"
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "neonstarport"}, {"FormId", 0x00015FFE} },
                                Addons = new List<string>(),

            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Neon Underbelly",
                Description = "Kill the target at Neon Underbelly",
                Location = "Neon Underbelly",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "kill_target"
                },

                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "neonunderbelly"}, {"FormId", 0x00015FFE} },
                                Addons = new List<string>(),

            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Neon Ebbside",
                Description = "Kill the target at Neon Ebbside",
                Location = "Neon Ebbside",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "kill_target"
                },

                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "neonebbside"}, {"FormId", 0x00015FFE} },
                                Addons = new List<string>(),

            });
            ShowdownTemplates.Add(new MissionTemplate()
            {
                Name = "City Bounty - Neon Rooftops",
                Description = "Kill the target at Neon Rooftops",
                Location = "Neon Rooftops",
                formid = FormKeyLookup.GetFormKey("duout_show_city_kill_neon"),
                outlawQuest = new Showdown_BountyCity(),
                MissionTags = new List<string>()
                {
                    "city",
                    "planetside",
                    "kill_target"
                },

                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false}, {"Label", "neonrooftops"}, {"FormId", 0x00015FFE} },
                                Addons = new List<string>(),

            });


        }
    }
}

