using FrankyCLI.questgen_tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
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
                    needSpacesuit = true,
                    outlawQuest = new Discovery_Dataslate(),
                    MissionTags = new List<string>()
                    {
                        "discovery",

                    }
                });
            /*
            DiscoveryTemplates.Add(
                new MissionTemplate()
                {
                    Name = "Wanted Poster Activator",
                    Description = "The player finds a wanted poster hanging in a bar.",
                    Location = "A bar",
                    formid = 0,
                    needSpacesuit = true,
                    outlawQuest = new Discovery_WantedPoster()
                });
            */
        }
    }
}

