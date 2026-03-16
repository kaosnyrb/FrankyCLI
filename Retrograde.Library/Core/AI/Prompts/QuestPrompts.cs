using Retrograde;
using Retrograde.Story;
using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public class QuestPrompts
    {
        public static string GetQuestName(List<string> Addons)
        {
            var questnameprompt =
                "Generate a quest name grounded in the lore and themes provided.\r\n" +
                "Constraints:\r\n" +
                "- 2-4 clear words in everyday language.\r\n" +
                "- Only output the quest name (no punctuation or explanation).\r\n" +
                "- Reflect one concrete element from the context (faction, place, action); avoid vague mood words.\r\n" +
                "- Do not invent new names or factions beyond the context.\r\n" +
                "- Style: plain, declarative; no metaphor, riddles, or mysterious phrasing.\r\n" +
                "- Flavor: prefer a strong action verb or specific noun to add punch (e.g., \"Seize\", \"Amber Smelter\", \"Dock Raid\").\r\n" +
                "- If using an adjective, make it concrete (e.g., \"Rust\", \"Frozen\", \"Broken\") not abstract (no \"Eternal\", \"Mysterious\").\r\n\r\n" +
                "Additional Information:\r\n";

            foreach (var item in Addons)
                questnameprompt += item;

            var envelope = PromptContext.CurrentEnvelope;
            if (envelope != null)
            {
                var validator = new CompositeValidator(new QuestNameValidator(), new StyleValidator());
                var results = ValidatedPrompt.Run(envelope, questnameprompt, validator,
                    PromptContext.CurrentFacts, PromptContext.TargetName);
                return results.Trim().Trim('"');
            }

            // Legacy path: shared conversation history
            var loreBlock = string.IsNullOrWhiteSpace(LorePrompts.LoreContext)
                ? ""
                : "LoreContext:\r\n" + LorePrompts.LoreContext + "\r\n\r\n";

            var fullPrompt = loreBlock + questnameprompt;
            var result = AITools.RunStatelessPrompt(fullPrompt);
            for (int i = 0; i < 10 && result.Length > 200; i++)
            {
                result = AITools.RunStatelessPrompt(fullPrompt);
            }
            return result;
        }

        public static string GetLogMessage(List<string> Addons, string playerRole = "bounty hunter")
        {
            var logprompt =
                $"Write a 50-word log entry for a {playerRole}.\r\n" +
                "State clearly: what the objective is, where it must be done, and why (the concrete reason tied to the target or situation).\r\n" +
                "If a <StageBridge> is provided in the Additional Information, frame it as what the bounty hunter hopes to learn or find — not as a known fact. Weave it naturally into the body of the entry as an investigative angle (e.g. 'may reveal', 'could confirm', 'worth checking') — do not add it as a separate final sentence.\r\n" +
                "Name the bounty target exactly as established.\r\n" +
                "Style: field intel note — plain declarative sentences, no metaphor, no ominous hints, no atmospheric writing.\r\n" +
                "Use the context for concrete facts only: target name, faction, motive. Do not invent new names.\r\n" +
                "Location: use ONLY the location provided in Additional Information exactly as written. Do not add planet names, system names, or any other location detail.\r\n" +
                "Output only the log text. Do NOT begin with any label, name prefix, or header — not \"Objective:\", \"Log:\", \"Target:\", \"Name:\", \"Subject:\", or any similar word followed by a colon. Start directly with the action or fact.\r\n\r\n" +
                "Additional Information:\r\n";

            foreach (var item in Addons)
                logprompt += item;

            var envelope = PromptContext.CurrentEnvelope;
            if (envelope != null)
            {
                var validator = new CompositeValidator(new LogMessageValidator(), new StyleValidator(), new ProperNounValidator());
                return ValidatedPrompt.Run(envelope, logprompt, validator,
                    PromptContext.CurrentFacts, PromptContext.TargetName);
            }

            // Legacy path: shared conversation history
            var results = AITools.RunPrompt(logprompt);
            for (int i = 0; i < 10 && results.Length < 150; i++)
            {
                results = AITools.RunPrompt(logprompt);
            }
            return results;
        }
    }
}
