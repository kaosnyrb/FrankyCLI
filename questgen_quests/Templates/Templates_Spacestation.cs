using FrankyCLI.questgen_tools;
using FrankyCLI.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    internal class Templates_Spacestation : TemplateLib
    {
        public Templates_Spacestation()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Station Activator - unguarded",
                Description = "Find info about the target from a clue in a space station",
                Location = "A clue hidden on a space station",
                formid = FormKeyLookup.GetFormKey("duout_info_space_activator"),
                needSpacesuit = true,
                outlawQuest = new Investigation_ActivatorSpacestation(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                },
                Addons = new List<string>(),

            });
            
            //-------------------------------  SHOWDOWN ------------------------------------------

        }
    }
}


