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

        // Remove a random entry from the pool and return it
        private static MissionTemplate RemovePick(List<MissionTemplate> pool)
        {
            int i = RandomProvider.Random.Next(pool.Count);
            var t = pool[i];
            pool.RemoveAt(i);
            return t;
        }

        public MissionTemplate GetShowdownMissionTemplate(string mission, List<string> addons = null)
        {
            if (AvailableTemplateLib.ShowdownTemplates.Count == 0) return null;

            if (!string.IsNullOrEmpty(mission))
            {
                var named = AvailableTemplateLib.ShowdownTemplates.FirstOrDefault(x => x.Name == mission);
                if (named == null)
                    Console.WriteLine($"AI_TemplateEngine: No showdown template named '{mission}' found, falling back to random.");
                else
                    return ApplyAddons(named, addons);
            }

            return ApplyAddons(RemovePick(AvailableTemplateLib.ShowdownTemplates), addons);
        }

        public MissionTemplate GetInvestigationMissionTemplate(string mission, List<string> addons = null)
        {
            if (AvailableTemplateLib.InvestigationTemplates.Count == 0) return null;

            if (!string.IsNullOrEmpty(mission))
            {
                var named = AvailableTemplateLib.InvestigationTemplates.FirstOrDefault(x => x.Name == mission);
                if (named == null)
                    Console.WriteLine($"AI_TemplateEngine: No investigation template named '{mission}' found, falling back to random.");
                else
                    return ApplyAddons(named, addons);
            }

            var picked = RemovePick(AvailableTemplateLib.InvestigationTemplates);
            string prefix = picked.Name.Split("-")[0];
            AvailableTemplateLib.InvestigationTemplates.RemoveAll(t => t.Name.Contains(prefix));
            return ApplyAddons(picked, addons);
        }

        public MissionTemplate GetDiscoveryMissionTemplate(string mission, List<string> addons = null)
        {
            if (AvailableTemplateLib.DiscoveryTemplates.Count == 0) return null;

            if (!string.IsNullOrEmpty(mission))
            {
                var named = AvailableTemplateLib.DiscoveryTemplates.FirstOrDefault(x => x.Name == mission);
                if (named == null)
                    Console.WriteLine($"AI_TemplateEngine: No discovery template named '{mission}' found, falling back to random.");
                else
                    return ApplyAddons(named, addons);
            }

            return ApplyAddons(RemovePick(AvailableTemplateLib.DiscoveryTemplates), addons);
        }
    }
}
