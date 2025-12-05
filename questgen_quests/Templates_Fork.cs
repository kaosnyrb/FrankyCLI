using FrankyCLI.questgen_tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    internal class Templates_Fork : TemplateLib
    {
        public Templates_Fork() {

            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();

            //This just forces the forks to be different missions
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Branching Node - planet/space",
                Location = "",
                Description = "",
                formid = 0x0008BC,
                needSpacesuit = true,
                outlawQuest = new Investigation_Branch(),
                Lib1 = MissionLib.planetlib,
                Lib2 = MissionLib.spacelib
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Branching Node - city/space",
                Location = "",
                Description = "",
                formid = 0x0008BC,
                needSpacesuit = true,
                outlawQuest = new Investigation_Branch(),
                Lib1 = MissionLib.citieslib,
                Lib2 = MissionLib.spacelib
            });

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Branching Node - city/planet",
                Location = "",
                Description = "",
                formid = 0x0008BC,
                needSpacesuit = true,
                outlawQuest = new Investigation_Branch(),
                Lib1 = MissionLib.citieslib,
                Lib2 = MissionLib.planetlib
            });

        }
    }
}
