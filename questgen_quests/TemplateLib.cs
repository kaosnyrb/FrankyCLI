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
            if (ShowdownTemplates.Count == 0) return null;
            if (mission != "")
            {
                return ShowdownTemplates.Where(x => x.Name == mission).Single();
            }
            else
            {
                Random random = new Random();
                return ShowdownTemplates[random.Next(ShowdownTemplates.Count)];
            }
        }

        public MissionTemplate GetInvestigationMissionTemplate(string mission)
        {
            if (InvestigationTemplates.Count == 0) return null;

            //AI Test
            string ItemPrompts = "The following list is the missions that can be choosen for the next step.";
            ItemPrompts += "Return just the number of the item that makes the most sense story wise.";

            for(int i = 0; i < InvestigationTemplates.Count; i++)
            {
//                ItemPrompts+= i + " : " + InvestigationTemplates
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
            int selected = random.Next(DiscoveryTemplates.Count);
            var template = DiscoveryTemplates[selected];
            DiscoveryTemplates.RemoveAt(selected);
            return template;
        }
    }
}
