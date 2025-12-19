using FrankyCLI.questgen_quests;
using FrankyCLI.questgen_tools.Interfaces;

namespace FrankyCLI.questgen_tools
{
    
    public class NoPOITemplateManager : ITemplateManager
    {
        List<TemplateLib> TemplateLibs = new List<TemplateLib>();

        public TemplateLib CompleteLib = new TemplateLib();

        public TemplateLib planetlib = new TemplateLib();
        public TemplateLib spacelib = new TemplateLib();
        public TemplateLib citieslib = new TemplateLib();

        ITemplateEngine templateEngine;

        public NoPOITemplateManager(ITemplateEngine templateEngine)
        {
            
            //TemplateLibs.Add(new Templates_Fork());

            TemplateLibs.Add(new Templates_Dataslate());

            citieslib.ImportTemplates(new Templates_Cities());
            citieslib.ImportTemplates(new Templates_Cities_Neon());
            citieslib.ImportTemplates(new Templates_Cities_Cydonia());
            citieslib.ImportTemplates(new Templates_Cities_Akila());
            TemplateLibs.Add(citieslib);

            planetlib.ImportTemplates(new Templates_PlanetInvestigate());

            //planetlib.ImportTemplates(new Templates_SpecificDungeons());           
            TemplateLibs.Add(planetlib);

            spacelib.ImportTemplates(new Templates_SpaceActivator());
            spacelib.ImportTemplates(new Templates_SpaceInformant());
            spacelib.ImportTemplates(new Templates_Derelicts());
            spacelib.ImportTemplates(new Templates_SpaceDestroy());
            spacelib.ImportTemplates(new Templates_Spacestation());

            TemplateLibs.Add(spacelib);

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
