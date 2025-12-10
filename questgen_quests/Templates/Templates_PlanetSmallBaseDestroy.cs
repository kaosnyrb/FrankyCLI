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
    public class Templates_PlanetSmallBaseDestroy : TemplateLib
    {
        public Templates_PlanetSmallBaseDestroy()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Smallbase Destroy - Small Marker",
                Description = "Destroy a dangerous object a nearby planet at a small facility",
                Location = "A remote location",
                formid = 0x000835,
                needSpacesuit = true,
                outlawQuest = new Investigation_DestroySmallBase(),
                MissionTags = new List<string>()
                {
                    "destroy",
                    "planetside",
                }

            });
            //-------------------------------  SHOWDOWN ------------------------------------------            

        }

    }
}
