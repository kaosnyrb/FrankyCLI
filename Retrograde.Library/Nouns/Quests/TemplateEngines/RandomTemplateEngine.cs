using Retrograde.Chains;
using Retrograde.Chains.Interfaces;

namespace Retrograde.Quests.TemplateEngines
{
    public class RandomTemplateEngine : ITemplateEngine
    {
        public TemplateLib AvailableTemplateLib { get; set; } = new TemplateLib();

        public MissionTemplate GetShowdownMissionTemplate(string mission, List<string> addons = null)
        {
            if (!AvailableTemplateLib.HasTemplates(AvailableTemplateLib.ShowdownGroups)) return null;
            Random random = RandomProvider.Random;

            MissionTemplate template;
            if (mission != "")
            {
                template = AvailableTemplateLib.FindByName(AvailableTemplateLib.ShowdownGroups, mission);
                if (template == null)
                {
                    Console.WriteLine($"RandomTemplateEngine: No showdown template named '{mission}' found, falling back to random.");
                    template = AvailableTemplateLib.PickRandom(AvailableTemplateLib.ShowdownGroups, random);
                }
            }
            else
            {
                template = AvailableTemplateLib.PickRandom(AvailableTemplateLib.ShowdownGroups, random);
            }
            template.Addons = addons;
            return template;
        }

        public MissionTemplate GetInvestigationMissionTemplate(string mission, List<string> addons = null)
        {
            if (!AvailableTemplateLib.HasTemplates(AvailableTemplateLib.InvestigationGroups)) return null;
            Random random = RandomProvider.Random;

            if (mission != "")
            {
                var named = AvailableTemplateLib.FindByName(AvailableTemplateLib.InvestigationGroups, mission);
                if (named == null)
                    Console.WriteLine($"RandomTemplateEngine: No investigation template named '{mission}' found, falling back to random.");
                else
                    return named;
            }

            var template = AvailableTemplateLib.PickAndRemove(AvailableTemplateLib.InvestigationGroups, random);
            template.Addons = addons;
            return template;
        }

        public MissionTemplate GetDiscoveryMissionTemplate(string mission, List<string> addons = null)
        {
            if (!AvailableTemplateLib.HasTemplates(AvailableTemplateLib.DiscoveryGroups)) return null;
            Random random = RandomProvider.Random;

            var template = AvailableTemplateLib.PickAndRemove(AvailableTemplateLib.DiscoveryGroups, random);
            template.Addons = addons;
            return template;
        }
    }
}
