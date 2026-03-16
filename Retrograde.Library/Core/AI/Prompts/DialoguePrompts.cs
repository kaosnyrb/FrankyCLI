using Retrograde;
using Retrograde.Models;
using Retrograde.Story;
using System.Collections.Generic;
using System.Text;

namespace Retrograde.AI.Utils
{
    public class DialoguePrompts
    {
        /// <summary>
        /// Generates a DialogueScript for an NPC who knows the target's location.
        /// exchangeCount controls how many player/NPC exchange pairs are generated (2–4, default 3).
        /// </summary>
        public static DialogueScript GetDialogueScript(List<string> addons, int exchangeCount = 3, string npcBackground = "")
        {
            exchangeCount = Math.Clamp(exchangeCount, 2, 4);

            var sb = new StringBuilder();
            sb.AppendLine("Write a short Starfield NPC conversation. The NPC is a friendly contact who knows where the bounty target is.");
            sb.AppendLine("Use the context provided for names, factions, and locations.");
            sb.AppendLine("Do NOT invent new names or places beyond the context and Additional Information.");
            if (!string.IsNullOrWhiteSpace(npcBackground))
            {
                sb.AppendLine();
                sb.AppendLine("NPC BACKGROUND (use this to shape their voice, vocabulary, and what they choose not to say):");
                sb.AppendLine(npcBackground);
            }
            sb.AppendLine();
            sb.AppendLine("GREETING constraint: Do NOT open with the NPC stating they know why the player is here, predicting what they will ask, or naming the bounty target first. The greeting must be pure speech — something the NPC says out loud that roots them in their world. Do NOT describe what they are doing, their physical state, or their surroundings. Foreknowledge should emerge from what they reveal, not be announced upfront.");
            sb.AppendLine();
            sb.AppendLine($"PLAYER voice: The player is a bounty hunter on a paying contract. Questions are operational, not conversational — specific and closed. Good examples: \"When did she leave?\" / \"What name was on the manifest?\" / \"Who processed her entry?\" Bad examples: \"What can you tell me?\" / \"How did you know that?\" PLAYER{exchangeCount} must be the most direct, closed question in the exchange.");
            sb.AppendLine();
            sb.AppendLine("Information beat rules — each exchange reveals ONE specific thing and nothing else:");
            sb.AppendLine("- Beat 1 (NPC1): What the NPC personally observed about the target — behavior or appearance only. Do NOT reveal destination or ship details here.");
            if (exchangeCount >= 3)
                sb.AppendLine("- Beat 2 (NPC2): The specific logistics detail — which freighter, what route, what alias they used. Do NOT repeat behavioral observations from Beat 1.");
            if (exchangeCount >= 4)
                sb.AppendLine("- Beat 3 (NPC3): How they disappeared from this location — exit method and timeline only. Do NOT reveal current destination yet.");
            sb.AppendLine($"- Beat {exchangeCount} (NPC{exchangeCount}): Where they are now and one actionable next step. Nothing else.");
            sb.AppendLine();
            sb.AppendLine("Output EXACTLY this format, one line per label, no extra text before or after:");
            sb.AppendLine("GREETING: <NPC opening line, max 100 chars>");
            for (int b = 1; b <= exchangeCount; b++)
            {
                bool isLast = b == exchangeCount;
                sb.AppendLine($"PLAYER{b}: {(isLast ? "<direct operational closing question about location, max 45 chars>" : "<operational bounty hunter question, max 45 chars>")}");
                sb.AppendLine($"NPC{b}a: {(isLast ? "<NPC first line — location reveal, max 150 chars>" : $"<NPC first line for question {b}, max 150 chars>")}");
                sb.AppendLine($"NPC{b}b: {(isLast ? "<NPC second line — wrap-up, max 150 chars — omit if one line suffices>" : $"<NPC second line for question {b}, max 150 chars — omit if one line suffices>")}");
            }
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- All lines must fit within their character limits.");
            sb.AppendLine("- No quotation marks around the text.");
            sb.AppendLine("- No stage directions, no asterisks, no line breaks within a line.");
            sb.AppendLine("- Tone: grounded, Starfield-style — terse, believable, not dramatic.");
            sb.AppendLine("- NPC knowledge constraint: This NPC knows only what someone in their job and location would personally witness or overhear. They do NOT have access to Vanguard investigation reports, security assessments, or classified faction files. If they mention the Vanguard, it must be from something they personally saw or heard — not a summary of why the Vanguard is interested.");
            sb.AppendLine($"- Beat {exchangeCount} NPC{exchangeCount}b scope: if used, name only a direction, location, or person to approach next. Do NOT explain why it matters or reveal what the player will discover there.");
            sb.AppendLine();
            sb.AppendLine("Additional Information:");
            foreach (var item in addons)
                sb.AppendLine(item);

            string raw = "";
            var envelope = PromptContext.CurrentEnvelope;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (envelope != null)
                    raw = AITools.RunIsolatedPrompt(envelope.BuildSystemPrompt(), sb.ToString());
                else
                    raw = AITools.RunPromptHighQuality(sb.ToString());
                if (raw.Contains("GREETING:") && raw.Contains($"PLAYER{exchangeCount}:") && raw.Contains($"NPC{exchangeCount}a:"))
                    break;
            }

            var script = ParseDialogueScript(raw, exchangeCount);
            script.NpcBackground = npcBackground;

            int sideExchangeIndex = exchangeCount - 2;
            script.Exchanges[sideExchangeIndex].SideOptions = GetSideOptions(script, sideExchangeIndex, addons);

            if (AITools.AIMODE)
                script.CompletionDismissal = GetCompletionDismissal(script, addons);

            return script;
        }

        private static DialogueSideOptions GetSideOptions(DialogueScript baseScript, int beatIndex, List<string> addons)
        {
            var beatLines = baseScript.Exchanges[beatIndex].NpcReply;
            var beatText  = string.Join(" / ", beatLines.Where(l => !string.IsNullOrWhiteSpace(l)));

            var sb = new StringBuilder();
            sb.AppendLine("You are writing two optional side dialogue choices for a Starfield NPC conversation.");
            sb.AppendLine("The player is a bounty hunter. They are mid-conversation with an NPC contact who has just told them something.");
            sb.AppendLine("Use the context provided. Do NOT invent new names or places.");
            sb.AppendLine();
            sb.AppendLine($"NPC BEAT {beatIndex + 1} REPLY (what was just said):");
            sb.AppendLine(beatText);
            sb.AppendLine();
            sb.AppendLine("These two side choices appear alongside the main follow-up question. Selecting one does NOT advance the conversation. The NPC answers and the player can still ask the main question afterward.");
            sb.AppendLine();
            sb.AppendLine("PLAYER voice: Operational, direct, brief. Bounty hunter on a paying contract.");
            sb.AppendLine();
            sb.AppendLine("Choice 1 — EXTRA INFO: Player wants one more concrete detail from what the NPC just said. A short, specific follow-up that probes exactly one thing from the reply above. Do NOT repeat what the NPC already said — push past it.");
            sb.AppendLine("Choice 2 — DETAIL: Player asks about a specific fact from the LoreContext — a faction, a location, or a recent event relevant to the hunt. The NPC gives a short, factual answer based on what they'd personally know from their job and location. No atmosphere, no jokes — just a concrete detail that reinforces the world.");
            sb.AppendLine();
            sb.AppendLine("Output EXACTLY this format, one line per label, no extra text before or after:");
            sb.AppendLine("EXTRA_PLAYER: <player's extra-info question, max 45 chars>");
            sb.AppendLine("EXTRA_NPC1a: <NPC first line, max 150 chars>");
            sb.AppendLine("EXTRA_NPC1b: <NPC second line, max 150 chars — omit if one line suffices>");
            sb.AppendLine("DETAIL_PLAYER: <player's factual question about the situation, max 45 chars>");
            sb.AppendLine("DETAIL_NPC1a: <NPC first line with a concrete fact, max 150 chars>");
            sb.AppendLine("DETAIL_NPC1b: <NPC second line, max 150 chars — omit if one line suffices>");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- All lines must fit within their character limits.");
            sb.AppendLine("- No quotation marks around the text.");
            sb.AppendLine("- No stage directions, no asterisks, no line breaks within a line.");
            sb.AppendLine("- Tone: grounded, Starfield-style — terse, believable, not dramatic.");
            sb.AppendLine("- EXTRA_NPC must follow directly from the beat 2 content — one specific, unembellished detail.");
            sb.AppendLine("- DETAIL_NPC must reference a concrete fact from the LoreContext — a name, location, faction, or event. No atmosphere or filler.");
            sb.AppendLine("- NPC knowledge constraint: same as main conversation — only what this person would personally witness or overhear.");
            sb.AppendLine();
            sb.AppendLine("Additional Information:");
            foreach (var item in addons)
                sb.AppendLine(item);

            string raw = "";
            var sideEnvelope = PromptContext.CurrentEnvelope;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (sideEnvelope != null)
                    raw = AITools.RunIsolatedPrompt(sideEnvelope.BuildSystemPrompt(), sb.ToString());
                else
                    raw = AITools.RunPrompt(sb.ToString());
                if (raw.Contains("EXTRA_PLAYER:") && raw.Contains("DETAIL_PLAYER:"))
                    break;
            }

            return ParseSideOptions(raw);
        }

        /// <summary>
        /// Generates a single NPC line for when the player re-activates the NPC after the dialogue
        /// quest completes. Uses RunStatelessPrompt so the full conversation context is available
        /// (lore, NPC background, what was said) without polluting the history.
        /// </summary>
        private static string GetCompletionDismissal(DialogueScript script, List<string> addons)
        {
            var sb = new StringBuilder();
            sb.AppendLine("The player has finished speaking to this NPC and got what they needed. Now they've come back.");
            sb.AppendLine("Write a single short NPC line for this re-approach — the NPC has nothing more to give.");
            if (!string.IsNullOrWhiteSpace(script.NpcBackground))
                sb.AppendLine($"NPC BACKGROUND: {script.NpcBackground}");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- Maximum 100 characters.");
            sb.AppendLine("- Do NOT name the bounty target.");
            sb.AppendLine("- Do NOT use generic phrases like 'I've told you everything I know'.");
            sb.AppendLine("- The line must feel specific to this character's job and personality.");
            sb.AppendLine("- Tone: grounded, Starfield-style — terse, believable.");
            sb.AppendLine("- Output ONLY the line text. No labels, no quotation marks, no explanation.");
            sb.AppendLine();
            sb.AppendLine("Additional Information:");
            foreach (var item in addons)
                sb.AppendLine(item);

            var dismissalEnvelope = PromptContext.CurrentEnvelope;
            if (dismissalEnvelope != null)
                return TruncateAtSentence(AITools.RunIsolatedPrompt(dismissalEnvelope.BuildSystemPrompt(), sb.ToString()), 100);
            return TruncateAtSentence(AITools.RunStatelessPrompt(sb.ToString()), 100);
        }

        private static DialogueSideOptions ParseSideOptions(string raw)
        {
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

            return new DialogueSideOptions
            {
                ExtraInfo = new SideOption
                {
                    PlayerPrompt = TruncateAtSentence(Extract("EXTRA_PLAYER"), 45),
                    NpcReply     = ExtractNpcLines("EXTRA_NPC1"),
                },
                Joke = new SideOption
                {
                    PlayerPrompt = TruncateAtSentence(Extract("DETAIL_PLAYER"), 45),
                    NpcReply     = ExtractNpcLines("DETAIL_NPC1"),
                },
            };
        }

        private static DialogueScript ParseDialogueScript(string raw, int exchangeCount)
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
            for (int b = 1; b <= exchangeCount; b++)
                script.Exchanges.Add(new DialogueExchange
                {
                    PlayerPrompt = TruncateAtSentence(Extract($"PLAYER{b}"), 45),
                    NpcReply     = ExtractNpcLines($"NPC{b}"),
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
