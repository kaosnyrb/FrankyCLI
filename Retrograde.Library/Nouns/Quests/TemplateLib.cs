using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Chains
{
    public class TemplateGroup
    {
        public float Weight { get; set; }
        public List<MissionTemplate> Templates { get; set; }

        public TemplateGroup(float weight, List<MissionTemplate> templates)
        {
            Weight = weight;
            Templates = templates;
        }
    }

    public class TemplateLib
    {
        // Flat lists — used by individual Templates_* subclasses to populate their templates.
        public List<MissionTemplate> DiscoveryTemplates;
        public List<MissionTemplate> InvestigationTemplates;
        public List<MissionTemplate> ShowdownTemplates;

        // Grouped storage — populated by ImportTemplates for weighted selection.
        public List<TemplateGroup> DiscoveryGroups = new();
        public List<TemplateGroup> InvestigationGroups = new();
        public List<TemplateGroup> ShowdownGroups = new();

        public TemplateLib()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();
        }

        /// <summary>
        /// Import templates from a source TemplateLib as a weighted group.
        /// </summary>
        public void ImportTemplates(TemplateLib source, float weight = 1.0f)
        {
            if (source.DiscoveryTemplates.Count > 0)
                DiscoveryGroups.Add(new TemplateGroup(weight, new List<MissionTemplate>(source.DiscoveryTemplates)));
            if (source.InvestigationTemplates.Count > 0)
                InvestigationGroups.Add(new TemplateGroup(weight, new List<MissionTemplate>(source.InvestigationTemplates)));
            if (source.ShowdownTemplates.Count > 0)
                ShowdownGroups.Add(new TemplateGroup(weight, new List<MissionTemplate>(source.ShowdownTemplates)));

            // Also maintain flat lists for backward compatibility (LorePrompts menu, named lookups, etc.)
            DiscoveryTemplates.AddRange(source.DiscoveryTemplates);
            InvestigationTemplates.AddRange(source.InvestigationTemplates);
            ShowdownTemplates.AddRange(source.ShowdownTemplates);
        }

        /// <summary>
        /// Two-stage weighted pick: choose a group (weighted), then a random template within it.
        /// </summary>
        public MissionTemplate PickRandom(List<TemplateGroup> groups, Random random)
        {
            var available = groups.Where(g => g.Templates.Count > 0).ToList();
            if (available.Count == 0) return null;

            float totalWeight = available.Sum(g => g.Weight);
            float roll = (float)(random.NextDouble() * totalWeight);
            float cumulative = 0f;
            TemplateGroup chosen = available[^1];
            foreach (var g in available)
            {
                cumulative += g.Weight;
                if (roll < cumulative) { chosen = g; break; }
            }

            return chosen.Templates[random.Next(chosen.Templates.Count)];
        }

        /// <summary>
        /// Two-stage weighted pick that also removes the picked template from its group.
        /// </summary>
        public MissionTemplate PickAndRemove(List<TemplateGroup> groups, Random random)
        {
            var available = groups.Where(g => g.Templates.Count > 0).ToList();
            if (available.Count == 0) return null;

            float totalWeight = available.Sum(g => g.Weight);
            float roll = (float)(random.NextDouble() * totalWeight);
            float cumulative = 0f;
            TemplateGroup chosen = available[^1];
            foreach (var g in available)
            {
                cumulative += g.Weight;
                if (roll < cumulative) { chosen = g; break; }
            }

            int idx = random.Next(chosen.Templates.Count);
            var template = chosen.Templates[idx];
            chosen.Templates.RemoveAt(idx);
            return template;
        }

        /// <summary>
        /// Find a template by name across all groups.
        /// </summary>
        public MissionTemplate FindByName(List<TemplateGroup> groups, string name)
        {
            foreach (var g in groups)
            {
                var t = g.Templates.FirstOrDefault(x => x.Name == name);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>
        /// Remove all templates matching a predicate across all groups.
        /// </summary>
        public void RemoveAll(List<TemplateGroup> groups, Predicate<MissionTemplate> match)
        {
            foreach (var g in groups)
                g.Templates.RemoveAll(match);
        }

        /// <summary>
        /// Check if any group has templates.
        /// </summary>
        public bool HasTemplates(List<TemplateGroup> groups) => groups.Any(g => g.Templates.Count > 0);
    }
}
