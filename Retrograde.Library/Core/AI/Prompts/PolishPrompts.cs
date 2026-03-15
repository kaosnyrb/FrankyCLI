using Retrograde.Writing;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Retrograde.AI.Utils
{
    public static class PolishPrompts
    {
        private const string ImprovedOpen  = "[IMPROVED:";
        private const string ImprovedClose = "[/IMPROVED]";

        /// <summary>
        /// Builds a stateless review prompt that presents all polishable text pieces
        /// and asks the AI to rewrite the weakest ones.
        /// Uses the full conversation history (lore context) without polluting it.
        /// </summary>
        public static string Build(IReadOnlyList<IPolishable> polishables, int passNumber)
        {
            int targetCount = Math.Max(2, polishables.Count / 3);

            // Collect story stages in presentation order, deduped
            var arcStages = new List<string>();
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in polishables)
            {
                if (!string.IsNullOrEmpty(p.StoryStage) && seen.Add(p.StoryStage))
                    arcStages.Add(p.StoryStage);
            }

            var sb = new StringBuilder();
            sb.AppendLine("You are reviewing ALL the writing generated for this Starfield quest chain.");
            sb.AppendLine("Use the LoreContext, NPC names, factions, and locations established earlier in this conversation.");
            sb.AppendLine();

            if (arcStages.Count > 0)
            {
                sb.AppendLine("Narrative arc (content is presented in this order below):");
                sb.AppendLine("  " + string.Join(" → ", arcStages));
                sb.AppendLine();
            }

            sb.AppendLine($"Writing quality pass {passNumber}. Identify the {targetCount} weakest piece(s) from the list below.");
            sb.AppendLine("Evaluate each piece for:");
            sb.AppendLine("  • Bethesda-style voice: grounded, terse, specific to the setting");
            sb.AppendLine("  • Concrete specificity: names, places, roles — not vague summaries");
            sb.AppendLine("  • Coherence: consistent with the full quest chain narrative");
            sb.AppendLine("  • Narrative flow: each piece earns its place in the arc — Discovery plants hooks,");
            sb.AppendLine("    Investigation stages build tension and advance the trail, Showdown pays it off.");
            sb.AppendLine("    Dialogue and logs should feel like they belong at their specific story moment.");
            sb.AppendLine("  • Absence of generic phrasing or AI-sounding filler");
            sb.AppendLine();
            sb.AppendLine("For each piece you rewrite:");
            sb.AppendLine("  • Log entries: preserve approximate length and all factual content");
            sb.AppendLine("  • Books/dataslates: preserve approximate word count and all factual content");
            sb.AppendLine("  • Dialogue: preserve the exact GREETING/PLAYER/NPC label format and character limits");
            sb.AppendLine("    (GREETING ≤100, PLAYER ≤45, NPC lines ≤150, DISMISSAL ≤100)");
            sb.AppendLine("    Include all labels that were in the original, even if unchanged.");
            sb.AppendLine();
            sb.AppendLine("Output ONLY the improved pieces. Skip pieces that are already strong.");
            sb.AppendLine("Use this exact format for each improved piece:");
            sb.AppendLine($"  {ImprovedOpen}<LABEL>]");
            sb.AppendLine("  <improved text>");
            sb.AppendLine($"  {ImprovedClose}");
            sb.AppendLine();
            sb.AppendLine("━━━ PIECES ━━━");

            foreach (var p in polishables)
            {
                sb.AppendLine();
                string header = $"[{p.Label} | type:{p.ContentType}";
                if (!string.IsNullOrEmpty(p.StoryStage))
                    header += $" | stage:{p.StoryStage}";
                if (p.MaxChars > 0)
                    header += $" | max:{p.MaxChars}chars";
                header += "]";
                sb.AppendLine(header);
                sb.AppendLine(p.GetText());
            }

            return sb.ToString();
        }

        /// <summary>
        /// Parses the AI response and returns a dictionary of Label → improved text.
        /// </summary>
        public static Dictionary<string, string> Parse(string response)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(response))
                return result;

            // Match [IMPROVED:<label>] ... [/IMPROVED] blocks
            var pattern = new Regex(
                @"\[IMPROVED:([^\]]+)\]\r?\n(.*?)\[/IMPROVED\]",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match m in pattern.Matches(response))
            {
                string label   = m.Groups[1].Value.Trim();
                string content = m.Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(content))
                    result[label] = content;
            }

            return result;
        }
    }
}
