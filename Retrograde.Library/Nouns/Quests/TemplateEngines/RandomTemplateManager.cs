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
    public class RandomTemplateManager : ITemplateManager
    {
        List<TemplateLib> TemplateLibs = new List<TemplateLib>();

        public TemplateLib MergedLib = new TemplateLib();

        public TemplateLib planetlib = new TemplateLib();
        public TemplateLib spacelib = new TemplateLib();
        public TemplateLib citieslib = new TemplateLib();

        public RandomTemplateManager()
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
            Random random = RandomProvider.Random;

            if (MergedLib.ShowdownTemplates.Count == 0) return null;

            MissionTemplate template;
            if (mission != "")
            {
                template = MergedLib.ShowdownTemplates.FirstOrDefault(x => x.Name == mission);
                if (template == null)
                {
                    Console.WriteLine($"RandomTemplateManager: No showdown template named '{mission}' found, falling back to random.");
                    template = MergedLib.ShowdownTemplates[random.Next(MergedLib.ShowdownTemplates.Count)];
                }
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
            Random random = RandomProvider.Random;
            MissionTemplate template = null;

            if (mission != "")
            {
                //Don't really care about deleting as this is for testing
                var named = MergedLib.InvestigationTemplates.FirstOrDefault(x => x.Name == mission);
                if (named == null)
                    Console.WriteLine($"RandomTemplateManager: No investigation template named '{mission}' found, falling back to random.");
                else
                    return named;
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
            Random random = RandomProvider.Random;

            int selected = random.Next(MergedLib.DiscoveryTemplates.Count);
            template = MergedLib.DiscoveryTemplates[selected];
            MergedLib.DiscoveryTemplates.RemoveAt(selected);
            template.Addons = addons;
            return template;

        }
    }
}
