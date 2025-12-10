using FrankyCLI.questgen_tools;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace FrankyCLI.questgen_quests
{
    public class Templates_PlanetInvestigate : TemplateLib
    {
        public Templates_PlanetInvestigate()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Activator - Small Marker",
                Description = "Find info about the target on a nearby planet at a small facility",
                Location = "A remote location",
                formid = 0x000835,
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorPlanet(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "planetside",
                }

            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Activator - Captive",
                Description = "Find info about the target on a nearby planet at a small facility",
                Location = "A remote location",
                formid = 0x000907,
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorPlanet(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "planetside",
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Activator -  Large Marker",
                Description = "Find info about the target on a nearby planet at a small facility",
                Location = "A remote location",
                formid = 0x000908,
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorPlanet(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "planetside",
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Activator - Important Marker Breathable",
                Description = "Find info about the target on a nearby planet at a small facility with a breathable atmosphere",
                Location = "A remote location",
                formid = 0x000909,
                needSpacesuit = false,
                outlawQuest = new Investigation_ActivatorPlanet(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "planetside",
                }
            });

            //-------------------------------  SHOWDOWN ------------------------------------------            

        }

    }
}
