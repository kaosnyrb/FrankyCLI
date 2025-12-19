using FrankyCLI.questgen_quests;
using FrankyCLI.questgen_tools.Interfaces;

namespace FrankyCLI.questgen_tools
{
    //This is the libary
    public class AllTemplateManager : ITemplateManager
    {
        List<TemplateLib> TemplateLibs = new List<TemplateLib>();

        public TemplateLib CompleteLib = new TemplateLib();

        public TemplateLib planetlib = new TemplateLib();
        public TemplateLib spacelib = new TemplateLib();
        public TemplateLib citieslib = new TemplateLib();

        ITemplateEngine templateEngine;

        public AllTemplateManager(ITemplateEngine templateEngine)
        {
            
            //TemplateLibs.Add(new Templates_Fork());

            TemplateLibs.Add(new Templates_Dataslate());

            planetlib.ImportTemplates(new Templates_PlanetInvestigate());
            planetlib.ImportTemplates(new Templates_PlanetCombat());
            planetlib.ImportTemplates(new Templates_PlanetSmallBaseDestroy());
            planetlib.ImportTemplates(new Templates_PlanetSmallBaseInformant());
            

            planetlib.ImportTemplates(new Templates_SpecificDungeons());           
            TemplateLibs.Add(planetlib);

            spacelib.ImportTemplates(new Templates_SpaceActivator());
            spacelib.ImportTemplates(new Templates_SpaceInformant());
            spacelib.ImportTemplates(new Templates_SpaceDestroy());
            spacelib.ImportTemplates(new Templates_Spacestation());          
            spacelib.ImportTemplates(new Templates_Derelicts());
            TemplateLibs.Add(spacelib);


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
