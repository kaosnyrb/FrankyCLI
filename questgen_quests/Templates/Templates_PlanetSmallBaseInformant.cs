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
    public class Templates_PlanetSmallBaseInformant : TemplateLib
    {
        public Templates_PlanetSmallBaseInformant()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Smallbase Informant - Medium Marker",
                Description = "Collect a vital clue from an Informant",
                Location = "A remote location",
                formid = 0x0008C0,
                needSpacesuit = true,
                outlawQuest = new Investigation_Informant_Planet(),
                MissionTags = new List<string>()
                {
                    "find_clue",
                    "planetside",
                },
                parameters = new Dictionary<string, object>()
                {
                    {"ExtraLore","This Informant is a UC Ageis Agent who was tracking the target." },
                    {"IsTargetDead",true }
                }

            });
            //-------------------------------  SHOWDOWN ------------------------------------------            

        }

    }
}
