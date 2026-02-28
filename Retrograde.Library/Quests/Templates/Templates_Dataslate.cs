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
    public class Templates_Dataslate : TemplateLib
    {
        public Templates_Dataslate()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();

            
            DiscoveryTemplates.Add(
                new MissionTemplate()
                {
                    Name = "Dataslate in levelled item",
                    Description = "The player finds a dataslate containing a lead to the target.",
                    Location = "A remote location",
                    formid = new Mutagen.Bethesda.Plugins.FormKey(),
                    outlawQuest = new Discovery_Dataslate(),
                    MissionTags = new List<string>()
                    {
                        "discovery",

                    },
                    parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true} }
                });
            /*
            DiscoveryTemplates.Add(
                new MissionTemplate()
                {
                    Name = "Wanted Poster Activator",
                    Description = "The player finds a wanted poster hanging in a bar.",
                    Location = "A bar",
                    formid = 0,
                    outlawQuest = new Discovery_WantedPoster(),
                    parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true} }
                });
            */
        }
    }
}

