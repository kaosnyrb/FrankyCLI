using DynamicData;
using FrankyCLI.questgen_quests;
using FrankyCLI.questgen_tools.Interfaces;
using FrankyCLI.questgen_tools.Utils;
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
    public class RandomTemplateManager : ITemplateManager
    {
        List<TemplateLib> TemplateLibs = new List<TemplateLib>();

        public TemplateLib MergedLib = new TemplateLib();

        public static TemplateLib planetlib = new TemplateLib();
        public static TemplateLib spacelib = new TemplateLib();
        public static TemplateLib citieslib = new TemplateLib();

        public RandomTemplateManager()
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

        public MissionTemplate GetShowdownMissionTemplate(string mission, List<string> addons = null)
        {
            Random random = RandomUtils.random;

            MissionTemplate template = null;
            if (mission.Length > 0)
            {
                template = MergedLib.ShowdownTemplates.Where(x => x.Name == mission).Single();
            }

            if (MergedLib.ShowdownTemplates.Count == 0) return null;

            if (mission != "")
            {
                template = MergedLib.ShowdownTemplates.Where(x => x.Name == mission).Single();
            }
            else
            {
                template = MergedLib.ShowdownTemplates[random.Next(MergedLib.ShowdownTemplates.Count)];
            }
            template.Addons = addons;
            return template;
        }

        public MissionTemplate GetInvestigationMissionTemplate(string mission, List<string> addons = null)
        {
            if (MergedLib.InvestigationTemplates.Count == 0) return null;
            Random random = RandomUtils.random;
            MissionTemplate template = null;

            if (mission != "")
            {
                //Don't really care about deleting as this is for testing
                return MergedLib.InvestigationTemplates.Where(x => x.Name == mission).Single();
            }
            else
            {
                int selected = random.Next(MergedLib.InvestigationTemplates.Count);
                template = MergedLib.InvestigationTemplates[selected];
                MergedLib.InvestigationTemplates.RemoveAt(selected);
            }
            template.Addons = addons;
            return template;
        }

        public MissionTemplate GetDiscoveryMissionTemplate(string mission, List<string> addons = null)
        {
            MissionTemplate template = null;
            if (MergedLib.DiscoveryTemplates.Count == 0) return null;
            Random random = RandomUtils.random;

            int selected = random.Next(MergedLib.DiscoveryTemplates.Count);
            template = MergedLib.DiscoveryTemplates[selected];
            MergedLib.DiscoveryTemplates.RemoveAt(selected);
            template.Addons = addons;
            return template;

        }
    }
}
