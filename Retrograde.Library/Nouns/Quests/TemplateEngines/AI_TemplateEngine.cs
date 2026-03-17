using Retrograde.Chains;
using Retrograde.Chains.Interfaces;

namespace Retrograde.Quests.TemplateEngines
{
    public class AI_TemplateEngine : ITemplateEngine
    {
        public TemplateLib AvailableTemplateLib { get; set; } = new();

        private MissionTemplate ApplyAddons(MissionTemplate template, List<string> addons)
        {
            if (template == null) return null;
            if (addons != null)
                template.Addons = new List<string>(addons);
            return template;
        }

        public MissionTemplate GetShowdownMissionTemplate(string mission, List<string> addons = null)
        {
            if (!AvailableTemplateLib.HasTemplates(AvailableTemplateLib.ShowdownGroups)) return null;

            if (!string.IsNullOrEmpty(mission))
            {
                var named = AvailableTemplateLib.FindByName(AvailableTemplateLib.ShowdownGroups, mission);
                if (named == null)
                    Console.WriteLine($"AI_TemplateEngine: No showdown template named '{mission}' found, falling back to random.");
                else
                    return ApplyAddons(named, addons);
            }

            return ApplyAddons(AvailableTemplateLib.PickAndRemove(AvailableTemplateLib.ShowdownGroups, RandomProvider.Random), addons);
        }

        public MissionTemplate GetInvestigationMissionTemplate(string mission, List<string> addons = null)
        {
            if (!AvailableTemplateLib.HasTemplates(AvailableTemplateLib.InvestigationGroups)) return null;

            if (!string.IsNullOrEmpty(mission))
            {
                var named = AvailableTemplateLib.FindByName(AvailableTemplateLib.InvestigationGroups, mission);
                if (named == null)
                    Console.WriteLine($"AI_TemplateEngine: No investigation template named '{mission}' found, falling back to random.");
                else
                    return ApplyAddons(named, addons);
            }

            var picked = AvailableTemplateLib.PickAndRemove(AvailableTemplateLib.InvestigationGroups, RandomProvider.Random);
            string prefix = picked.Name.Split("-")[0];
            AvailableTemplateLib.RemoveAll(AvailableTemplateLib.InvestigationGroups, t => t.Name.Contains(prefix));
            return ApplyAddons(picked, addons);
        }

        public MissionTemplate GetDiscoveryMissionTemplate(string mission, List<string> addons = null)
        {
            if (!AvailableTemplateLib.HasTemplates(AvailableTemplateLib.DiscoveryGroups)) return null;

            if (!string.IsNullOrEmpty(mission))
            {
                var named = AvailableTemplateLib.FindByName(AvailableTemplateLib.DiscoveryGroups, mission);
                if (named == null)
                    Console.WriteLine($"AI_TemplateEngine: No discovery template named '{mission}' found, falling back to random.");
                else
                    return ApplyAddons(named, addons);
            }

            return ApplyAddons(AvailableTemplateLib.PickAndRemove(AvailableTemplateLib.DiscoveryGroups, RandomProvider.Random), addons);
        }
    }
}
