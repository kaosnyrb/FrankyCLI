using DynamicData;
using Retrograde.Chains.Interfaces;
using Retrograde.Chains;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Quests.TemplateEngines
{
    public class CityTemplateManager : ITemplateManager
    {
        List<TemplateLib> TemplateLibs = new List<TemplateLib>();

        public TemplateLib CompleteLib = new TemplateLib();

        public TemplateLib planetlib = new TemplateLib();
        public TemplateLib spacelib = new TemplateLib();
        public TemplateLib citieslib = new TemplateLib();

        ITemplateEngine templateEngine;

        public CityTemplateManager(ITemplateEngine templateEngine)
        {

            //TemplateLibs.Add(new Templates_Fork());

            TemplateLibs.Add(new Templates_Dataslate());

            citieslib.ImportTemplates(new Templates_Cities());
            citieslib.ImportTemplates(new Templates_Cities_Neon());
            citieslib.ImportTemplates(new Templates_Cities_Cydonia());
            citieslib.ImportTemplates(new Templates_Cities_Akila());
            TemplateLibs.Add(citieslib);

            CompleteLib.DiscoveryTemplates = new List<MissionTemplate>();
            CompleteLib.InvestigationTemplates = new List<MissionTemplate>();
            CompleteLib.ShowdownTemplates = new List<MissionTemplate>();

            foreach (TemplateLib templateLib in TemplateLibs)
            {
                foreach (var dis in templateLib.DiscoveryTemplates)
                {
                    CompleteLib.DiscoveryTemplates.Add(dis);
                }
                foreach (var dis in templateLib.InvestigationTemplates)
                {
                    CompleteLib.InvestigationTemplates.Add(dis);
                }
                foreach (var dis in templateLib.ShowdownTemplates)
                {
                    CompleteLib.ShowdownTemplates.Add(dis);
                }
            }
            templateEngine.AvailableTemplateLib = CompleteLib;
            this.templateEngine = templateEngine;

            //YamlExporter.WriteObjToYaml("Missions.txt", MergedLib);
        }

        public MissionTemplate GetShowdownMissionTemplate(string missionName, List<string> addons = null)
        {
            return templateEngine.GetShowdownMissionTemplate(missionName, addons);
        }

        public MissionTemplate GetInvestigationMissionTemplate(string missionName, List<string> addons = null)
        {
            return templateEngine.GetInvestigationMissionTemplate(missionName, addons);
        }

        public MissionTemplate GetDiscoveryMissionTemplate(string missionName, List<string> addons = null)
        {
            return templateEngine.GetDiscoveryMissionTemplate(missionName, addons);
        }
    }
}
