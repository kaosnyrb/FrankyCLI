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
                formid = FormKeyLookup.GetFormKey("duout_info_planet_activator_small"),
                outlawQuest = new Investigation_ActivatorPlanet(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "planetside",
                },

                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Activator - Captive",
                Description = "Find info about the target on a nearby planet at a small facility",
                Location = "A remote location",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_activator_captive"),
                outlawQuest = new Investigation_ActivatorPlanet(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "planetside",
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Activator -  Large Marker",
                Description = "Find info about the target on a nearby planet at a small facility",
                Location = "A remote location",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_activator_large"),
                outlawQuest = new Investigation_ActivatorPlanet(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "planetside",
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", true} }
            });
            InvestigationTemplates.Add(new MissionTemplate()
            {
                Name = "Planet side Activator - Important Marker Breathable",
                Description = "Find info about the target on a nearby planet at a small facility with a breathable atmosphere",
                Location = "A remote location",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_activator_important"),
                outlawQuest = new Investigation_ActivatorPlanet(),
                MissionTags = new List<string>()
                {
                    "follow_clue",
                    "planetside",
                },
                parameters = new Dictionary<string, object>() { {"NeedSpacesuit", false} }
            });
            //-------------------------------  SHOWDOWN ------------------------------------------            

        }

    }
}

