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
    public class Templates_Spacestation : TemplateLib
    {
        public Templates_Spacestation()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
            //-------------------------------  INVESTIGATION ------------------------------------------
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Station Activator - spacer small light guard",
                Description = "Find info about the target from a clue in a space station",
                Location = "A clue hidden on a lightly defended small space station taken over by a Spacer Gang",
                formid = FormKeyLookup.GetFormKey("duout_info_space_station"),
                outlawQuest = new Investigation_ActivatorSpacestation(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "Small station",
                    "Light or no spaceship defense"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>
                {
                    {"Faction","Spacer" },
                    {"StationSize","Small" },
                    {"DefendingShipCountMin", 0 },
                    {"DefendingShipCountMax", 2 },
                    {"NeedSpacesuit", true}
                }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Space Station Activator - spacer Medium light guard",
                Description = "Find info about the target from a clue in a space station",
                Location = "A clue hidden on a lightly defended medium sized space station taken over by a Spacer Gang",
                formid = FormKeyLookup.GetFormKey("duout_info_space_station"),
                outlawQuest = new Investigation_ActivatorSpacestation(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "space",
                    "Medium station",
                    "Light or no spaceship defense"
                },
                Addons = new List<string>(),
                parameters = new Dictionary<string, object>
                {
                    {"Faction","Spacer" },
                    {"StationSize","Medium" },
                    {"DefendingShipCountMin", 0 },
                    {"DefendingShipCountMax", 2 },
                    {"NeedSpacesuit", true}
                }
            });

            //-------------------------------  SHOWDOWN ------------------------------------------

        }
    }
}


