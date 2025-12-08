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
    public class AICardTemplateManager : ITemplateManager
    {
        List<TemplateLib> TemplateLibs = new List<TemplateLib>();

        public TemplateLib MergedLib = new TemplateLib();

        public TemplateLib planetlib = new TemplateLib();
        public TemplateLib spacelib = new TemplateLib();
        public TemplateLib citieslib = new TemplateLib();

        public AICardTemplateManager()
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

            YamlExporter.WriteObjToYaml("Missions.txt", MergedLib);
        }

        public MissionTemplate GetShowdownMissionTemplate(string mission)
        {
            Random random = RandomUtils.random;

            if (mission.Length > 0)
            {
                return MergedLib.ShowdownTemplates.Where(x => x.Name == mission).Single();
            }

            //Should showdowns use ai at all?
            if (AITools.AIMODE)
            {
                //AI Test
                string ItemPrompts = "The following list is the missions that can be choosen for the next step.";
                ItemPrompts += "Return just the number of the item that makes the most sense story wise." + "\r\n";

                int count = 5 + random.Next(10);
                List<MissionTemplate> selected = MergedLib.ShowdownTemplates
                    .OrderBy(x => random.Next())
                    .Take(count).ToList();
                for (int i = 0; i < selected.Count; i++)
                {
                    ItemPrompts += i + " : " + selected[i].Location + " " + selected[i].Description + "\r\n";
                }
                var result = AITools.RunPrompt(ItemPrompts);
                int index = 0;
                try
                {
                    int.TryParse(result, out index);
                    return selected[index];
                }
                catch
                {
                    int randselected = random.Next(MergedLib.ShowdownTemplates.Count);
                    var template = MergedLib.ShowdownTemplates[randselected];
                    MergedLib.ShowdownTemplates.RemoveAt(randselected);
                    return template;
                }
            }
            else
            {
                if (MergedLib.ShowdownTemplates.Count == 0) return null;
                if (mission != "")
                {
                    return MergedLib.ShowdownTemplates.Where(x => x.Name == mission).Single();
                }
                else
                {
                    return MergedLib.ShowdownTemplates[random.Next(MergedLib.ShowdownTemplates.Count)];
                }
            }

        }

        public MissionTemplate GetInvestigationMissionTemplate(string mission)
        {
            if (MergedLib.InvestigationTemplates.Count == 0) return null;
            Random random = RandomUtils.random;

            if (mission.Length > 0)
            {
                return MergedLib.InvestigationTemplates.Where(x => x.Name == mission).Single();
            }

            if (AITools.AIMODE)
            {
                //AI Test
                string ItemPrompts = "The following list is the missions that can be choosen for the next step.";
                ItemPrompts += "Return just the number of the item that makes the most sense story wise." + "\r\n";

                int count = 5 + random.Next(10);
                List<MissionTemplate> selected = MergedLib.InvestigationTemplates
                    .OrderBy(x => random.Next())
                    .Take(count).ToList();
                for (int i = 0; i < selected.Count; i++)
                {
                    ItemPrompts += i + " : " + selected[i].Location + " " + selected[i].Description + "\r\n";
                }


                var result = AITools.RunPrompt(ItemPrompts);
                int index = 0;
                try
                {
                    int.TryParse(result, out index);
                    return MergedLib.InvestigationTemplates[index];
                }
                catch
                {
                    int randselected = random.Next(MergedLib.InvestigationTemplates.Count);
                    var template = MergedLib.InvestigationTemplates[randselected];
                    MergedLib.InvestigationTemplates.RemoveAt(randselected);
                    return template;
                }
            }

            if (mission != "")
            {
                //Don't really care about deleting as this is for testing
                return MergedLib.InvestigationTemplates.Where(x => x.Name == mission).Single();
            }
            else
            {
                int selected = random.Next(MergedLib.InvestigationTemplates.Count);
                var template = MergedLib.InvestigationTemplates[selected];
                MergedLib.InvestigationTemplates.RemoveAt(selected);
                return template;
            }
        }

        public MissionTemplate GetDiscoveryMissionTemplate()
        {
            if (MergedLib.DiscoveryTemplates.Count == 0) return null;
            Random random = RandomUtils.random;

            if (AITools.AIMODE)
            {
                //AI Test
                string ItemPrompts = "The following list is the missions that can be choosen for the next step.";
                ItemPrompts += "Return just the number of the item that makes the most sense story wise." + "\r\n";

                int count = 5 + random.Next(10);
                List<MissionTemplate> selected = MergedLib.DiscoveryTemplates
                    .OrderBy(x => random.Next())
                    .Take(count).ToList();
                for (int i = 0; i < selected.Count; i++)
                {
                    ItemPrompts += i + " : " + selected[i].Location + " " + selected[i].Description + "\r\n";
                }

                var result = AITools.RunPrompt(ItemPrompts);
                int index = 0;
                try
                {
                    int.TryParse(result, out index);
                    return MergedLib.DiscoveryTemplates[index];
                }
                catch
                {
                    int randselected = random.Next(MergedLib.DiscoveryTemplates.Count);
                    var template = MergedLib.DiscoveryTemplates[randselected];
                    MergedLib.DiscoveryTemplates.RemoveAt(randselected);
                    return template;
                }
            }
            else
            {
                int selected = random.Next(MergedLib.DiscoveryTemplates.Count);
                var template = MergedLib.DiscoveryTemplates[selected];
                MergedLib.DiscoveryTemplates.RemoveAt(selected);
                return template;

            }
        }
    }
}
