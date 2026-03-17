using Retrograde;
using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public class QuestPrompts
    {
        public static string GetQuestName(List<string> Addons)
        {
            var loreBlock = string.IsNullOrWhiteSpace(LorePrompts.LoreContext)
                ? ""
                : "LoreContext:\r\n" + LorePrompts.LoreContext + "\r\n\r\n";

            var questnameprompt =
                loreBlock +
                "Generate a quest name grounded in the lore and themes provided.\r\n" +
                "Constraints:\r\n" +
                "- 2-4 clear words in everyday language.\r\n" +
                "- Only output the quest name (no punctuation or explanation).\r\n" +
                "- Reflect one concrete element from the LoreContext (faction, place, action); avoid vague mood words.\r\n" +
                "- Do not invent new names or factions beyond the LoreContext.\r\n" +
                "- Style: plain, declarative; no metaphor, riddles, or mysterious phrasing.\r\n" +
                "- Flavor: prefer a strong action verb or specific noun from the LoreContext to add punch (e.g., \"Seize\", \"Amber Smelter\", \"Dock Raid\").\r\n" +
                "- If using an adjective, make it concrete (e.g., \"Rust\", \"Frozen\", \"Broken\") not abstract (no \"Eternal\", \"Mysterious\").\r\n\r\n" +

                "Use the LoreContext above for tone, theme and narrative flavor.\r\n" +
                "Do NOT quote the lore; derive meaning and style from it.\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                questnameprompt += item;

            var results = AITools.RunStatelessPrompt(questnameprompt);
            for (int i = 0; i < 10 && results.Length > 200; i++)
            {
                results = AITools.RunStatelessPrompt(questnameprompt);
            }
            return results;
        }

        public static string GetLogMessage(List<string> Addons)
        {
            var logprompt =
                "Write a 50-word log entry for a bounty hunter.\r\n" +
                "State clearly: what the objective is, where it must be done, and why (the concrete reason tied to the target or situation).\r\n" +
                "Name the bounty target exactly as established in the LoreContext.\r\n" +
                "Style: field intel note — plain declarative sentences, no metaphor, no ominous hints, no atmospheric writing.\r\n" +
                "Use the LoreContext established earlier in this conversation for concrete facts only: target name, faction, motive. Do not invent new names.\r\n" +
                "Location: use ONLY the location provided in Additional Information exactly as written. Do not add planet names, system names, or any location detail from the LoreContext.\r\n" +
                "Output only the log text. Do NOT begin with any label, name prefix, or header — not \"Objective:\", \"Log:\", \"Target:\", \"Name:\", \"Subject:\", or any similar word followed by a colon. Start directly with the action or fact.\r\n" +
                "Stage-locked knowledge: The log must reflect ONLY what the player could know at the current quest stage. At QuestProgress 0–10%, use only the target's name, their basic crime, and the current objective location or contact. Do NOT reference faction investigations, security assessments, or any reason for the hunt beyond the surface crime. At QuestProgress 50–80%, you may include faction involvement. At QuestProgress 90%+, the full picture is available.\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                logprompt += item;

            var results = AITools.RunPrompt(logprompt);
            for (int i = 0; i < 10 && results.Length < 150; i++)
            {
                results = AITools.RunPrompt(logprompt);
            }
            return results;
        }
    }
}
