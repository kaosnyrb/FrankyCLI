using DynamicData;
using FrankyCLI.questgen_quests;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public class TemplateManager
    {
        List<TemplateLib> TemplateLibs = new List<TemplateLib>();

        public TemplateLib MergedLib = new TemplateLib();

        public static TemplateLib planetlib = new TemplateLib();
        public static TemplateLib spacelib = new TemplateLib();
        public static TemplateLib citieslib = new TemplateLib();

        public TemplateManager()
        {

            //TemplateLibs.Add(new Templates_Fork());

            TemplateLibs.Add(new Templates_Dataslate());

            planetlib.ImportTemplates(new Templates_PlanetPCM());
            planetlib.ImportTemplates(new Templates_SpecificDungeons());           
            TemplateLibs.Add(planetlib);

            spacelib.ImportTemplates(new Templates_SpaceActivator());
            spacelib.ImportTemplates(new Templates_SpaceInformant());
            spacelib.ImportTemplates(new Templates_Derelicts());
            TemplateLibs.Add(spacelib);


            citieslib.ImportTemplates(new Templates_Cities());
            citieslib.ImportTemplates(new Templates_Cities_Neon());
            citieslib.ImportTemplates(new Templates_Cities_Cydonia());
            citieslib.ImportTemplates(new Templates_Cities_Akila());
            TemplateLibs.Add(citieslib);

            MergedLib.DiscoveryTemplates = new List<MissionTemplate>();
            MergedLib.InvestigationTemplates = new List<MissionTemplate>();
            MergedLib.ShowdownTemplates = new List<MissionTemplate>();

            foreach (TemplateLib templateLib in TemplateLibs)
            {
                foreach (var dis in templateLib.DiscoveryTemplates)
                {
                    MergedLib.DiscoveryTemplates.Add(dis);
                }
                foreach (var dis in templateLib.InvestigationTemplates)
                {
                    MergedLib.InvestigationTemplates.Add(dis);
                }
                foreach(var dis in templateLib.ShowdownTemplates)
                {
                    MergedLib.ShowdownTemplates.Add(dis);
                }
            }
        }

        public MissionTemplate GetShowdownMissionTemplate(string missionName)
        {
            return MergedLib.GetShowdownMissionTemplate(missionName);
        }

        public MissionTemplate GetInvestigationMissionTemplate(string missionName)
        {

            Random random = new Random();
            bool foundMission = false;

            if (missionName.Length > 0)
            {
                return MergedLib.GetInvestigationMissionTemplate(missionName);
            }

            while (!foundMission)
            {
                var chosenlib = TemplateLibs[random.Next(TemplateLibs.Count)];
                if (chosenlib.InvestigationTemplates.Count >0)
                {
                    var chosen = chosenlib.GetInvestigationMissionTemplate(missionName);
                    if (chosen != null)
                    {
                        return chosen;
                    }
                }

            }

            return MergedLib.GetInvestigationMissionTemplate(missionName);
        }

        public MissionTemplate GetDiscoveryMissionTemplate()
        {
            return MergedLib.GetDiscoveryMissionTemplate();
       }

    }


}
