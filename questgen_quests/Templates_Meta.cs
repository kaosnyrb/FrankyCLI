using FrankyCLI.questgen_tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    internal class Templates_Meta : TemplateLib
    {
        public Templates_Meta() {

            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();

            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Branching Node",
                Location = "",
                Description = "",
                formid = 0x0008BC,
                needSpacesuit = true,
                outlawQuest = new Investigation_Branch()
            });
        }
    }
}
