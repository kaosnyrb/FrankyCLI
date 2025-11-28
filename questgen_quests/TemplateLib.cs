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


            if (AITools.AIMODE)
            {
                //AI Test
                string ItemPrompts = "The following list is the missions that can be choosen for the next step.";
                ItemPrompts += "Return just the number of the item that makes the most sense story wise." + "\r\n";

                for (int i = 0; i < ShowdownTemplates.Count; i++)
                {
                    ItemPrompts += i + " : " + ShowdownTemplates[i].Location + " " + ShowdownTemplates[i].Description + "\r\n";
                }
                var result = AITools.RunPrompt(ItemPrompts);
                int index = 0;
                try
                {
                    int.TryParse(result, out index);
                    return ShowdownTemplates[index];
                }
                catch
                {
                    int selected = random.Next(ShowdownTemplates.Count);
                    var template = ShowdownTemplates[selected];
                    ShowdownTemplates.RemoveAt(selected);
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

            if(AITools.AIMODE)
            {
                //AI Test
                string ItemPrompts = "The following list is the missions that can be choosen for the next step.";
                ItemPrompts += "Return just the number of the item that makes the most sense story wise." + "\r\n";

                for (int i = 0; i < InvestigationTemplates.Count; i++)
                {
                    ItemPrompts += i + " : " + InvestigationTemplates[i].Location + " " + InvestigationTemplates[i].Description + "\r\n";
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
                    Random random = new Random();
                    int selected = random.Next(InvestigationTemplates.Count);
                    var template = InvestigationTemplates[selected];
                    InvestigationTemplates.RemoveAt(selected);
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
                Random random = new Random();
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

                for (int i = 0; i < DiscoveryTemplates.Count; i++)
                {
                    ItemPrompts += i + " : " + DiscoveryTemplates[i].Location + " " + DiscoveryTemplates[i].Description + "\r\n";
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
                    int selected = random.Next(DiscoveryTemplates.Count);
                    var template = DiscoveryTemplates[selected];
                    DiscoveryTemplates.RemoveAt(selected);
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
