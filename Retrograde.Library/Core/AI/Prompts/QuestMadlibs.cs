using Retrograde.Nouns;
using Retrograde.Utils;
using Retrograde.Chains;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Retrograde.AI.Utils
{
    /// <summary>
    /// Local madlibs-style generator for quest names and log messages.
    /// Replaces per-quest AI calls so quest Setup() runs offline; the
    /// WritingPolishPass (if enabled) handles the single AI quality pass.
    /// </summary>
    public static class QuestMadlibs
    {
        /// <summary>
        /// Generates a 2-3 word quest name from seed pools.
        /// Picks a stage-appropriate verb and combines it with contextHint
        /// (the quest-specific clue object), falling back to the outlaw's
        /// last name or the first word of the location.
        /// </summary>
        public static string GetQuestName(OutlawNpc outlaw, MissionTemplate template, string contextHint = "")
        {
            var stage = ParseQuestStage(template.Addons);

            var verbs = stage switch
            {
                "Showdown"  => QuestSeedData.ShowdownVerbs,
                "Discovery" => QuestSeedData.DiscoveryVerbs,
                _           => QuestSeedData.InvestigationVerbs,
            };

            var verb = verbs[RandomProvider.Random.Next(verbs.Count)];

            string obj;
            if (!string.IsNullOrWhiteSpace(contextHint))
            {
                obj = contextHint.Trim();
            }
            else
            {
                // Fall back to outlaw last name, then first word of location
                var nameParts = outlaw.name?.Split(' ');
                if (nameParts != null && nameParts.Length > 1)
                    obj = nameParts[nameParts.Length - 1];
                else if (!string.IsNullOrWhiteSpace(template.Location))
                    obj = template.Location.Split(' ')[0];
                else
                    obj = outlaw.name ?? "Target";
            }

            return $"{verb} {obj}";
        }

        /// <summary>
        /// Generates a ~50-word log entry from the outlaw's traits and the
        /// mission template. contextExtras are short descriptors of the
        /// quest-specific object (e.g. datasource name, ship name) appended
        /// as a closing sentence.
        /// </summary>
        public static string GetLogMessage(OutlawNpc outlaw, MissionTemplate template, params string[] contextExtras)
        {
            var stage    = ParseQuestStage(template.Addons);
            var name     = outlaw.name ?? "Target";
            var crime    = string.IsNullOrWhiteSpace(outlaw.Traits.Crime) ? "criminal activity" : outlaw.Traits.Crime;
            var occ      = string.IsNullOrWhiteSpace(outlaw.Traits.Occupation) ? "unknown occupation" : outlaw.Traits.Occupation;
            var location = string.IsNullOrWhiteSpace(template.Location) ? "the target location" : template.Location;

            string log = stage switch
            {
                "Discovery" =>
                    $"Bounty posted: {name}, formerly a {occ}, wanted for {crime}. " +
                    $"Begin your investigation at {location}.",

                "DeepInvestigation" or "ForkInvestigation" =>
                    $"The trail leads deeper. Go to {location} — evidence there will expose " +
                    $"{name}'s connections and point to the final location.",

                "Showdown" =>
                    $"Target confirmed: {name} is at {location}. " +
                    $"Wanted for {crime}. Move in and finish the job.",

                _ => // InitialInvestigation and anything else
                    $"{name} is wanted for {crime}. " +
                    $"Head to {location} to pick up the trail.",
            };

            // Append the first extra as a closing action sentence if provided
            if (contextExtras.Length > 0 && !string.IsNullOrWhiteSpace(contextExtras[0]))
            {
                var extra = contextExtras[0].Trim().TrimEnd('.');
                log += $" Find the {extra}.";
            }

            return log;
        }

        // ─────────────────────────────────────────────────────────────────────

        private static string ParseQuestStage(List<string> addons)
        {
            if (addons == null) return "";
            foreach (var addon in addons)
            {
                var m = Regex.Match(addon, @"<QuestStage>(\w+)</QuestStage>");
                if (m.Success) return m.Groups[1].Value;
            }
            return "";
        }
    }
}
