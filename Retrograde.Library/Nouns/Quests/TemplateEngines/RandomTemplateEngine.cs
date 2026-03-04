using Retrograde.Chains;
using Retrograde.Chains.Interfaces;

namespace Retrograde.Quests.TemplateEngines
{
    public class RandomTemplateEngine : ITemplateEngine
    {
        public TemplateLib AvailableTemplateLib { get; set; } = new TemplateLib();

        public MissionTemplate GetShowdownMissionTemplate(string mission, List<string> addons = null)
        {
            if (AvailableTemplateLib.ShowdownTemplates.Count == 0) return null;
            Random random = RandomProvider.Random;

            MissionTemplate template;
            if (mission != "")
            {
                template = AvailableTemplateLib.ShowdownTemplates.FirstOrDefault(x => x.Name == mission);
                if (template == null)
                {
                    Console.WriteLine($"RandomTemplateEngine: No showdown template named '{mission}' found, falling back to random.");
                    template = AvailableTemplateLib.ShowdownTemplates[random.Next(AvailableTemplateLib.ShowdownTemplates.Count)];
                }
            }
            else
            {
                template = AvailableTemplateLib.ShowdownTemplates[random.Next(AvailableTemplateLib.ShowdownTemplates.Count)];
            }
            template.Addons = addons;
            return template;
        }

        public MissionTemplate GetInvestigationMissionTemplate(string mission, List<string> addons = null)
        {
            if (AvailableTemplateLib.InvestigationTemplates.Count == 0) return null;
            Random random = RandomProvider.Random;

            if (mission != "")
            {
                var named = AvailableTemplateLib.InvestigationTemplates.FirstOrDefault(x => x.Name == mission);
                if (named == null)
                    Console.WriteLine($"RandomTemplateEngine: No investigation template named '{mission}' found, falling back to random.");
                else
                    return named;
            }

            int selected = random.Next(AvailableTemplateLib.InvestigationTemplates.Count);
            var template = AvailableTemplateLib.InvestigationTemplates[selected];
            AvailableTemplateLib.InvestigationTemplates.RemoveAt(selected);
            template.Addons = addons;
            return template;
        }

        public MissionTemplate GetDiscoveryMissionTemplate(string mission, List<string> addons = null)
        {
            if (AvailableTemplateLib.DiscoveryTemplates.Count == 0) return null;
            Random random = RandomProvider.Random;

            int selected = random.Next(AvailableTemplateLib.DiscoveryTemplates.Count);
            var template = AvailableTemplateLib.DiscoveryTemplates[selected];
            AvailableTemplateLib.DiscoveryTemplates.RemoveAt(selected);
            template.Addons = addons;
            return template;
        }
    }
}
