using Retrograde.Utils;
using Retrograde.AI.Utils;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Quests
{
    public class Templates_Cities_Conversation_Neon : TemplateLib
    {
        public Templates_Cities_Conversation_Neon()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();

            //-------------------------------  INVESTIGATION ------------------------------------------            
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
                    {"Label", "neonstarport"},
                    {"NpcNameHint", "appropriate for a mechanic working in a busy commercial starport"},
                },
                Addons = new List<string>()
                {
                    FlavourSeedData.GetNpcRelationshipToTarget(),
                    FlavourSeedData.GetNpcConversationTone(),
                    "The NPC works in the Neon Starport as a Mechanic."
                },
                
            });
        }
    }
}

