using FrankyCLI.questgen_tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    public class TemplateLib
    {
        public List<MissionTemplate> DiscoveryTemplates;
        public List<MissionTemplate> InvestigationTemplates;
        public List<MissionTemplate> ShowdownTemplates;

        public MissionTemplate GetShowdownMissionTemplate(string mission)
        {
            Random random = new Random();

            if (mission.Length > 0) {
                return ShowdownTemplates.Where(x => x.Name == mission).Single();
            }

            //Should showdowns use ai at all?
            if (AITools.AIMODE && false)
            {
                //AI Test
                string ItemPrompts = "The following list is the missions that can be choosen for the next step.";
                ItemPrompts += "Return just the number of the item that makes the most sense story wise." + "\r\n";
                
                int count = 5 + random.Next(10);
                List<MissionTemplate> selected = ShowdownTemplates
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
                    int randselected = random.Next(ShowdownTemplates.Count);
                    var template = ShowdownTemplates[randselected];
                    ShowdownTemplates.RemoveAt(randselected);
                    return template;
                }
            }
            else
            {
                if (ShowdownTemplates.Count == 0) return null;
                if (mission != "")
                {
                    return ShowdownTemplates.Where(x => x.Name == mission).Single();
                }
                else
                {
                    return ShowdownTemplates[random.Next(ShowdownTemplates.Count)];
                }
            }

        }

        public MissionTemplate GetInvestigationMissionTemplate(string mission)
        {
            if (InvestigationTemplates.Count == 0) return null;
            Random random = new Random();

            if (mission.Length > 0)
            {
                return InvestigationTemplates.Where(x => x.Name == mission).Single();
            }

            if (AITools.AIMODE)
            {
                //AI Test
                string ItemPrompts = "The following list is the missions that can be choosen for the next step.";
                ItemPrompts += "Return just the number of the item that makes the most sense story wise." + "\r\n";
                ItemPrompts += "Do not choose a City stage if you already have." + "\r\n";

                int count = 5 + random.Next(10);
                List<MissionTemplate> selected = InvestigationTemplates
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
                    return InvestigationTemplates[index];
                }
                catch
                {
                    int randselected = random.Next(InvestigationTemplates.Count);
                    var template = InvestigationTemplates[randselected];
                    InvestigationTemplates.RemoveAt(randselected);
                    return template;
                }
            }

            if (mission != "")
            {
                //Don't really care about deleting as this is for testing
                return InvestigationTemplates.Where(x => x.Name == mission).Single();
            }
            else
            {
                int selected = random.Next(InvestigationTemplates.Count);
                var template = InvestigationTemplates[selected];
                InvestigationTemplates.RemoveAt(selected);
                return template;
            }
        }

        public MissionTemplate GetDiscoveryMissionTemplate()
        {
            if (DiscoveryTemplates.Count == 0) return null;
            Random random = new Random();

            if (AITools.AIMODE)
            {
                //AI Test
                string ItemPrompts = "The following list is the missions that can be choosen for the next step.";
                ItemPrompts += "Return just the number of the item that makes the most sense story wise." + "\r\n";

                int count = 5 + random.Next(10);
                List<MissionTemplate> selected = DiscoveryTemplates
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
                    return DiscoveryTemplates[index];
                }
                catch
                {
                    int randselected = random.Next(DiscoveryTemplates.Count);
                    var template = DiscoveryTemplates[randselected];
                    DiscoveryTemplates.RemoveAt(randselected);
                    return template;
                }
            }
            else
            {
                int selected = random.Next(DiscoveryTemplates.Count);
                var template = DiscoveryTemplates[selected];
                DiscoveryTemplates.RemoveAt(selected);
                return template;

            }
        }
    }
}
