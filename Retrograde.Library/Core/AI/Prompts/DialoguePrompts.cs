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
        /// exchangeCount controls how many player/NPC exchange pairs are generated (2–4, default 3).
        /// After generation a single refinement pass improves writing quality without changing facts.
        /// </summary>
        public static DialogueScript GetDialogueScript(List<string> addons, int exchangeCount = 3, string npcBackground = "")
        {
            exchangeCount = Math.Clamp(exchangeCount, 2, 4);

            var sb = new StringBuilder();
            sb.AppendLine("Write a short Starfield NPC conversation. The NPC is a friendly contact who knows where the bounty target is.");
            sb.AppendLine("Use the LoreContext established earlier in this conversation for names, factions, and locations.");
            sb.AppendLine("Do NOT invent new names or places beyond the LoreContext and Additional Information.");
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
            sb.AppendLine($"- Intrigue constraint: implement the Intrigue detail from Additional Information as follows — in exactly ONE NPC line (not GREETING, not NPC{exchangeCount}a) let a concrete unasked-for detail land without comment. Do NOT have the NPC announce or reference their foreknowledge.");
            sb.AppendLine("- NPC knowledge constraint: This NPC knows only what someone in their job and location would personally witness or overhear. They do NOT have access to Vanguard investigation reports, security assessments, or classified faction files. If they mention the Vanguard, it must be from something they personally saw or heard — not a summary of why the Vanguard is interested.");
            sb.AppendLine($"- Beat {exchangeCount} NPC{exchangeCount}b scope: if used, name only a direction, location, or person to approach next. Do NOT explain why it matters or reveal what the player will discover there.");
            sb.AppendLine();
            sb.AppendLine("Additional Information:");
            foreach (var item in addons)
                sb.AppendLine(item);

            string raw = "";
            for (int attempt = 0; attempt < 5; attempt++)
            {
                raw = AITools.RunPromptHighQuality(sb.ToString());
                if (raw.Contains("GREETING:") && raw.Contains($"PLAYER{exchangeCount}:") && raw.Contains($"NPC{exchangeCount}a:"))
                    break;
            }

            var script = ParseDialogueScript(raw, exchangeCount);
            script.NpcBackground = npcBackground;

            if (AITools.AIMODE)
            {
                string refinedRaw = RunRefinementPass(script, exchangeCount);
                if (refinedRaw.Contains("GREETING:") && refinedRaw.Contains($"PLAYER{exchangeCount}:"))
                    script = ParseDialogueScript(refinedRaw, exchangeCount);
                script.NpcBackground = npcBackground;
            }

            int sideExchangeIndex = exchangeCount - 2;
            script.Exchanges[sideExchangeIndex].SideOptions = GetSideOptions(script, sideExchangeIndex, addons);

            if (AITools.AIMODE)
                script.CompletionDismissal = GetCompletionDismissal(script, addons);

            return script;
        }

        /// <summary>
        /// Critique-first refinement: a single high-quality call that identifies the weakest line,
        /// then rewrites the full draft with that critique as the primary focus.
        /// Logs the critique to console and returns only the improved draft text.
        /// </summary>
        private static string RunRefinementPass(DialogueScript draft, int exchangeCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Below is a draft Starfield NPC conversation.");
            if (!string.IsNullOrWhiteSpace(draft.NpcBackground))
            {
                sb.AppendLine($"NPC BACKGROUND: {draft.NpcBackground}");
                sb.AppendLine("When critiquing and rewriting, judge the NPC's voice against this background.");
            }
            sb.AppendLine();
            sb.AppendLine("Step 1 — identify the single weakest line in the entire draft. Output exactly:");
            sb.AppendLine("CRITIQUE: <one sentence naming the label and why that line fails>");
            sb.AppendLine();
            sb.AppendLine("Step 2 — rewrite the full draft. Your primary focus is fixing the line you critiqued.");
            sb.AppendLine("Also improve along these axes where possible:");
            sb.AppendLine("- Writing sharpness: cut filler words; concrete nouns over abstract ones.");
            sb.AppendLine("- NPC voice distinctiveness: specific person with a specific job, not a generic informant.");
            sb.AppendLine("- Beat specificity: each NPC line contains ONE concrete detail, not a vague summary.");
            sb.AppendLine("- Player question quality: operational pressure only; closed questions.");
            sb.AppendLine();
            sb.AppendLine("HARD CONSTRAINTS:");
            sb.AppendLine("- Information content must be identical. Do not add, remove, or swap facts.");
            sb.AppendLine("- Character limits: GREETING ≤100, PLAYER ≤45, NPC lines ≤150.");
            sb.AppendLine("- Output the improved draft using the EXACT same label format as below. One label per line. No extra text.");
            sb.AppendLine("- No quotation marks, no stage directions, no asterisks.");
            sb.AppendLine();
            sb.AppendLine("DRAFT:");
            sb.AppendLine($"GREETING: {draft.NpcGreeting}");
            for (int b = 0; b < exchangeCount; b++)
            {
                int beatNum = b + 1;
                var ex = draft.Exchanges[b];
                sb.AppendLine($"PLAYER{beatNum}: {ex.PlayerPrompt}");
                if (ex.NpcReply.Count > 0) sb.AppendLine($"NPC{beatNum}a: {ex.NpcReply[0]}");
                if (ex.NpcReply.Count > 1) sb.AppendLine($"NPC{beatNum}b: {ex.NpcReply[1]}");
            }

            string response = AITools.RunPromptHighQuality(sb.ToString());

            // Log the critique for developer visibility
            foreach (var line in response.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("CRITIQUE:", System.StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[Dialogue] {trimmed}");
                    break;
                }
            }

            return response;
        }

        private static DialogueSideOptions GetSideOptions(DialogueScript baseScript, int beatIndex, List<string> addons)
        {
            var beatLines = baseScript.Exchanges[beatIndex].NpcReply;
            var beatText  = string.Join(" / ", beatLines.Where(l => !string.IsNullOrWhiteSpace(l)));

            var sb = new StringBuilder();
            sb.AppendLine("You are writing two optional side dialogue choices for a Starfield NPC conversation.");
            sb.AppendLine("The player is a bounty hunter. They are mid-conversation with an NPC contact who has just told them something.");
            sb.AppendLine("Use the LoreContext established earlier in this conversation. Do NOT invent new names or places.");
            sb.AppendLine();
            sb.AppendLine($"NPC BEAT {beatIndex + 1} REPLY (what was just said):");
            sb.AppendLine(beatText);
            sb.AppendLine();
            sb.AppendLine("These two side choices appear alongside the main follow-up question. Selecting one does NOT advance the conversation — it is pure color. The NPC answers and the player can still ask the main question afterward.");
            sb.AppendLine();
            sb.AppendLine("PLAYER voice: Operational, direct, brief. Bounty hunter on a paying contract.");
            sb.AppendLine();
            sb.AppendLine("Choice 1 — EXTRA INFO: Player wants one more concrete detail from what the NPC just said. A short, specific follow-up that probes exactly one thing from the reply above. Do NOT repeat what the NPC already said — push past it.");
            sb.AppendLine("Choice 2 — JOKE: Player makes a wry, dry-humored aside about the situation or the NPC's answer. Starfield tone — understated, not quippy. The NPC reacts briefly: mild amusement, mild irritation, or a dry brush-off. Keep it grounded.");
            sb.AppendLine();
            sb.AppendLine("Output EXACTLY this format, one line per label, no extra text before or after:");
            sb.AppendLine("EXTRA_PLAYER: <player's extra-info question, max 45 chars>");
            sb.AppendLine("EXTRA_NPC1a: <NPC first line, max 150 chars>");
            sb.AppendLine("EXTRA_NPC1b: <NPC second line, max 150 chars — omit if one line suffices>");
            sb.AppendLine("JOKE_PLAYER: <player's wry aside or comment, max 45 chars>");
            sb.AppendLine("JOKE_NPC1a: <NPC first line reacting to the joke, max 150 chars>");
            sb.AppendLine("JOKE_NPC1b: <NPC second line, max 150 chars — omit if one line suffices>");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- All lines must fit within their character limits.");
            sb.AppendLine("- No quotation marks around the text.");
            sb.AppendLine("- No stage directions, no asterisks, no line breaks within a line.");
            sb.AppendLine("- Tone: grounded, Starfield-style — terse, believable, not dramatic.");
            sb.AppendLine("- EXTRA_NPC must follow directly from the beat 2 content — one specific, unembellished detail.");
            sb.AppendLine("- JOKE_NPC must feel natural from someone used to dealing with bounty hunters — not shocked, not gushing.");
            sb.AppendLine("- NPC knowledge constraint: same as main conversation — only what this person would personally witness or overhear.");
            sb.AppendLine();
            sb.AppendLine("Additional Information:");
            foreach (var item in addons)
                sb.AppendLine(item);

            string raw = "";
            for (int attempt = 0; attempt < 5; attempt++)
            {
                raw = AITools.RunPrompt(sb.ToString());
                if (raw.Contains("EXTRA_PLAYER:") && raw.Contains("JOKE_PLAYER:"))
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
                    PlayerPrompt = TruncateAtSentence(Extract("JOKE_PLAYER"), 45),
                    NpcReply     = ExtractNpcLines("JOKE_NPC1"),
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
