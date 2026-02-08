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
                formid = FormKeyLookup.GetFormKey("duout_info_planet_activator_small"),
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

