using Retrograde.Utils;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Retrograde.Quests
{
    public class Templates_PlanetConversation : TemplateLib
    {
        public Templates_PlanetConversation()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Conversation - UC Vanguard",
                Description = "Speak to a UC Vanguard member about the target on a nearby planet at a small facility",
                Location = "A remote location",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Investigation_ConversationPlanet(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "conversation",                    
                    "planetside",
                },

                parameters = new Dictionary<string, object>() { 
                {
                    "NeedSpacesuit", true}                 
                },
                Addons = new List<string>()
                {
                    "Target was a friend of this character, they are shocked at what they've done.",
                    "This character talks about the good old days",
                    
                },
                
            });
            
            //-------------------------------  SHOWDOWN ------------------------------------------            

        }

    }
}

