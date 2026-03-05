using Retrograde;
using Retrograde.Models;
using System.Collections.Generic;
using System.Text;

namespace Retrograde.AI.Utils
{
    public class DialoguePrompts
    {
        /// <summary>
        /// Generates a DialogueScript for an NPC who knows the target's location.
        /// Produces 1 greeting + 2 optional topic exchanges + 1 completion exchange (in that order).
        /// Completion exchange (index 0) ends the quest; optional exchanges (index 1-2) loop back.
        /// </summary>
        public static DialogueScript GetDialogueScript(List<string> addons)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Write a short Starfield NPC conversation. The NPC is a friendly contact who knows where the bounty target is.");
            sb.AppendLine("Use the LoreContext established earlier in this conversation for names, factions, and locations.");
            sb.AppendLine("Do NOT invent new names or places beyond the LoreContext and Additional Information.");
            sb.AppendLine();
            sb.AppendLine("Output EXACTLY this format, one line per label, no extra text before or after:");
            sb.AppendLine("GREETING: <NPC opening line, max 100 chars>");
            sb.AppendLine("PLAYER1: <optional player question about the situation, max 50 chars>");
            sb.AppendLine("NPC1a: <NPC first line for question 1, max 150 chars>");
            sb.AppendLine("NPC1b: <NPC second line for question 1, max 150 chars — omit if one line suffices>");
            sb.AppendLine("PLAYER2: <optional player question about the target, max 50 chars>");
            sb.AppendLine("NPC2a: <NPC first line for question 2, max 150 chars>");
            sb.AppendLine("NPC2b: <NPC second line for question 2, max 150 chars — omit if one line suffices>");
            sb.AppendLine("PLAYER3: <player line that asks where the target is — the closing question, max 50 chars>");
            sb.AppendLine("NPC3a: <NPC first line — location reveal, max 150 chars>");
            sb.AppendLine("NPC3b: <NPC second line — wrap-up, max 150 chars — omit if one line suffices>");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- All lines must fit within their character limits.");
            sb.AppendLine("- No quotation marks around the text.");
            sb.AppendLine("- No stage directions, no asterisks, no line breaks within a line.");
            sb.AppendLine("- Tone: grounded, Starfield-style — terse, believable, not dramatic.");
            sb.AppendLine();
            sb.AppendLine("Additional Information:");
            foreach (var item in addons)
                sb.AppendLine(item);

            string raw = "";
            for (int attempt = 0; attempt < 5; attempt++)
            {
                raw = AITools.RunPrompt(sb.ToString());
                if (raw.Contains("GREETING:") && raw.Contains("PLAYER3:") && raw.Contains("NPC3:"))
                    break;
            }

            return ParseDialogueScript(raw);
        }

        private static DialogueScript ParseDialogueScript(string raw)
        {
            var script = new DialogueScript();
            string Extract(string label)
            {
                var prefix = label + ":";
                foreach (var line in raw.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                        return trimmed.Substring(prefix.Length).Trim();
                }
                return "";
            }

            List<string> ExtractNpcLines(string prefix)
            {
                var lines = new List<string>();
                var a = TruncateAtSentence(Extract(prefix + "a"), 150);
                var b = TruncateAtSentence(Extract(prefix + "b"), 150);
                if (!string.IsNullOrWhiteSpace(a)) lines.Add(a);
                if (!string.IsNullOrWhiteSpace(b)) lines.Add(b);
                if (lines.Count == 0) lines.Add("");
                return lines;
            }

            script.NpcGreeting = TruncateAtSentence(Extract("GREETING"), 100);

            // Exchange[0] = completion (ends quest) — PLAYER3/NPC3a/NPC3b
            // Exchange[1] = optional topic 1       — PLAYER1/NPC1a/NPC1b
            // Exchange[2] = optional topic 2       — PLAYER2/NPC2a/NPC2b
            script.Exchanges.Add(new DialogueExchange
            {
                PlayerPrompt = TruncateAtSentence(Extract("PLAYER3"), 50),
                NpcReply     = ExtractNpcLines("NPC3"),
            });
            script.Exchanges.Add(new DialogueExchange
            {
                PlayerPrompt = TruncateAtSentence(Extract("PLAYER1"), 50),
                NpcReply     = ExtractNpcLines("NPC1"),
            });
            script.Exchanges.Add(new DialogueExchange
            {
                PlayerPrompt = TruncateAtSentence(Extract("PLAYER2"), 50),
                NpcReply     = ExtractNpcLines("NPC2"),
            });

            return script;
        }

        /// <summary>
        /// Cuts at the last sentence-ending punctuation (. ? !) at or before maxLen.
        /// Falls back to a hard word-boundary cut, then a hard char cut if no boundary is found.
        /// </summary>
        private static string TruncateAtSentence(string s, int maxLen)
        {
            if (s.Length <= maxLen) return s;

            for (int i = maxLen - 1; i >= 0; i--)
                if (s[i] == '.' || s[i] == '?' || s[i] == '!')
                    return s.Substring(0, i + 1).Trim();

            for (int i = maxLen - 1; i >= 0; i--)
                if (s[i] == ' ')
                    return s.Substring(0, i).Trim();

            return s.Substring(0, maxLen);
        }
    }
}
