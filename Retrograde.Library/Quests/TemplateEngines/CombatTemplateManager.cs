using Retrograde.Chains.Interfaces;
using Retrograde.Chains;

namespace Retrograde.Quests.TemplateEngines
{
    //This is the libary
    public class CombatTemplateManager : ITemplateManager
    {
        List<TemplateLib> TemplateLibs = new List<TemplateLib>();

        public TemplateLib CompleteLib = new TemplateLib();

        public TemplateLib planetlib = new TemplateLib();
        public TemplateLib spacelib = new TemplateLib();
        public TemplateLib citieslib = new TemplateLib();

        ITemplateEngine templateEngine;

        public CombatTemplateManager(ITemplateEngine templateEngine)
        {
            planetlib.ImportTemplates(new Templates_PlanetCombat());
            planetlib.ImportTemplates(new Templates_SpecificDungeons());
            planetlib.ImportTemplates(new Templates_PlanetSmallBaseDestroy());
            planetlib.ImportTemplates(new Templates_PlanetSmallBaseInformant());

            TemplateLibs.Add(planetlib);

            spacelib.ImportTemplates(new Templates_SpaceInformant());
            spacelib.ImportTemplates(new Templates_Spacestation());
            spacelib.ImportTemplates(new Templates_SpaceDestroy());

            TemplateLibs.Add(spacelib);

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
                foreach(var dis in templateLib.ShowdownTemplates)
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
            return templateEngine.GetShowdownMissionTemplate(missionName,addons);
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
