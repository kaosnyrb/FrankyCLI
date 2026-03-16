using Retrograde.AI;
using Retrograde.Chains;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Retrograde.Story
{
    /// <summary>
    /// Runs after lore/arc selection but before any beat content is generated.
    /// Produces structured BeatFacts for every beat in a single AI call.
    /// These facts are the ONLY specific details beat content may reference.
    ///
    /// Also pre-computes the PlayerKnowledge timeline from the generated ClueLearned entries.
    /// </summary>
    public static class FactPlanningPass
    {
        /// <summary>
        /// Generate BeatFacts for all beats in a single prompt.
        /// Returns facts keyed by beat identifier (e.g., "showdown", "investigation_0", "discovery").
        /// Also returns the PlayerKnowledge timeline (story-order list of clues).
        /// </summary>
        public static (Dictionary<string, BeatFacts> Facts, List<string> PlayerKnowledgeTimeline) Plan(
            string loreContext,
            string targetName,
            List<(string BeatId, string TemplateName, string Location, string StageName)> beats)
        {
            var prompt = BuildPrompt(loreContext, targetName, beats);
            string response = AITools.RunPrompt(prompt);

            var facts = BeatFacts.ParseAll(response);

            // Fill in any missing beats with minimal defaults
            foreach (var beat in beats)
            {
                if (!facts.ContainsKey(beat.BeatId))
                {
                    Console.WriteLine($"[FactPlanning] WARNING: No facts generated for beat '{beat.BeatId}', using defaults.");
                    facts[beat.BeatId] = new BeatFacts
                    {
                        BeatId = beat.BeatId,
                        Location = beat.Location,
                        Tone = "direct, businesslike",
                    };
                }
                else
                {
                    // Ensure location matches the template's actual location
                    facts[beat.BeatId].Location = beat.Location;
                }
            }

            // Pre-compute PlayerKnowledge timeline (story order = reverse of beats list,
            // since beats are provided in generation order: showdown first, discovery last)
            var storyOrder = beats.AsEnumerable().Reverse().ToList();
            var timeline = new List<string>();
            foreach (var beat in storyOrder)
            {
                if (facts.TryGetValue(beat.BeatId, out var f) && !string.IsNullOrWhiteSpace(f.ClueLearned))
                    timeline.Add(f.ClueLearned);
            }

            Console.WriteLine($"[FactPlanning] Generated facts for {facts.Count} beat(s), {timeline.Count} knowledge entries.");
            return (facts, timeline);
        }

        /// <summary>
        /// Build a lore summary from the full LoreContext — a curated subset
        /// for use in ContextEnvelopes. Strips verbose sections and keeps
        /// only names, factions, crime, personality, motives.
        /// </summary>
        public static string BuildLoreSummary(string loreContext, string targetName, StoryCast cast)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Target: {targetName}");

            var targetRole = cast.GetRole("target");
            if (targetRole.Traits != null)
            {
                sb.AppendLine($"Crime: {targetRole.Traits.DefiningEvent}");
                sb.AppendLine($"Faction: {targetRole.Traits.AssociatedFaction}");
                sb.AppendLine($"Personality flaw: {targetRole.Traits.Flaw}");
                sb.AppendLine($"Goal: {targetRole.Traits.Goal}");
                sb.AppendLine($"Occupation: {targetRole.Traits.Occupation}");
            }

            // Include a trimmed version of the lore context (first ~600 chars)
            if (!string.IsNullOrWhiteSpace(loreContext))
            {
                var trimmed = loreContext.Length > 600 ? loreContext[..600] + "..." : loreContext;
                sb.AppendLine();
                sb.AppendLine("Lore excerpt:");
                sb.AppendLine(trimmed);
            }

            return sb.ToString();
        }

        private static string BuildPrompt(
            string loreContext,
            string targetName,
            List<(string BeatId, string TemplateName, string Location, string StageName)> beats)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are a quest fact planner. Given a lore context and a sequence of quest stages,");
            sb.AppendLine("generate specific, concrete facts for each stage.");
            sb.AppendLine();
            sb.AppendLine("## Lore Context");
            sb.AppendLine(loreContext);
            sb.AppendLine();
            sb.AppendLine($"## Target: {targetName}");
            sb.AppendLine();
            sb.AppendLine("## Quest Stages (in story order, earliest first)");

            // Present beats in story order (reverse of generation order)
            var storyOrder = beats.AsEnumerable().Reverse().ToList();
            for (int i = 0; i < storyOrder.Count; i++)
            {
                var beat = storyOrder[i];
                sb.AppendLine($"  {i + 1}. [{beat.BeatId}] \"{beat.TemplateName}\" at {beat.Location} ({beat.StageName})");
            }

            sb.AppendLine();
            sb.AppendLine("## Rules");
            sb.AppendLine("- NPCs are described by ROLE only (e.g., 'dock security guard'). Do NOT invent names.");
            sb.AppendLine("- Objects must be plausible for the location (e.g., 'cargo manifest' at a docking bay).");
            sb.AppendLine("- Each beat's ClueLearned must logically lead to the next beat's location.");
            sb.AppendLine("- No beat should reference details from a LATER beat.");
            sb.AppendLine("- Use concrete, specific details. No vague atmosphere.");
            sb.AppendLine($"- The target's name is {targetName}. This is the ONLY proper noun name allowed in output.");
            sb.AppendLine("- SettingDetails: 2-3 physical details about the location (e.g., 'crowded docking bay', 'dimly lit corridor').");
            sb.AppendLine("- Tone: 1-2 words describing emotional register (e.g., 'tense, guarded' or 'routine, businesslike').");
            sb.AppendLine();
            sb.AppendLine("## Output Format");
            sb.AppendLine("Output one <BeatFacts> block per stage, in story order:");
            sb.AppendLine();

            foreach (var beat in storyOrder)
            {
                sb.AppendLine($"<BeatFacts beat=\"{beat.BeatId}\">");
                sb.AppendLine($"  <Location>{beat.Location}</Location>");
                sb.AppendLine("  <NpcRoles>role1; role2</NpcRoles>");
                sb.AppendLine("  <Objects>object1; object2</Objects>");
                sb.AppendLine("  <Setting>detail1; detail2; detail3</Setting>");
                sb.AppendLine("  <ClueLearned>what the player learns here that points to the next stage</ClueLearned>");
                sb.AppendLine("  <Tone>emotional register</Tone>");
                sb.AppendLine("</BeatFacts>");
                sb.AppendLine();
            }

            sb.AppendLine("Output ONLY the <BeatFacts> blocks. No explanations.");

            return sb.ToString();
        }
    }
}
