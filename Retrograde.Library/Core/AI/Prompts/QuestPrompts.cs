using Retrograde;
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
                "- Reflect one concrete element from the LoreContext (faction, place, action); avoid vague mood words.\r\n" +
                "- Do not invent new names or factions beyond the LoreContext.\r\n" +
                "- Style: plain, declarative; no metaphor, riddles, or mysterious phrasing.\r\n" +
                "- Flavor: prefer a strong action verb or specific noun from the LoreContext to add punch (e.g., \"Seize\", \"Amber Smelter\", \"Dock Raid\").\r\n" +
                "- If using an adjective, make it concrete (e.g., \"Rust\", \"Frozen\", \"Broken\") not abstract (no \"Eternal\", \"Mysterious\").\r\n\r\n" +

                "Use the LoreContext established earlier in this conversation for tone, theme and narrative flavor.\r\n" +
                "You may draw on any relevant parts (Summary, TargetProfile, Rumors, Leads, Locations, Motives, Threats, MysteryElements).\r\n" +
                "Do NOT quote the lore; derive meaning and style from it.\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                questnameprompt += item;

            var results = AITools.RunPrompt(questnameprompt);
            for (int i = 0; i < 10 && results.Length > 200; i++)
            {
                results = AITools.RunPrompt(questnameprompt);
            }
            return results;
        }

        public static string GetLogMessage(List<string> Addons)
        {
            var logprompt =
                "Write a 50-word log entry for a bounty hunter.\r\n" +
                "State clearly: what the objective is, where it must be done, and why (the concrete reason tied to the target or situation).\r\n" +
                "If a <StageBridge> is provided in the Additional Information, frame it as what the bounty hunter hopes to learn or find — not as a known fact. Weave it naturally into the body of the entry as an investigative angle (e.g. 'may reveal', 'could confirm', 'worth checking') — do not add it as a separate final sentence.\r\n" +
                "Name the bounty target exactly as established in the LoreContext.\r\n" +
                "Style: field intel note — plain declarative sentences, no metaphor, no ominous hints, no atmospheric writing.\r\n" +
                "Use the LoreContext established earlier in this conversation for concrete facts only: target name, faction, motive. Do not invent new names.\r\n" +
                "Location: use ONLY the location provided in Additional Information exactly as written. Do not add planet names, system names, or any location detail from the LoreContext.\r\n" +
                "Output only the log text. Do NOT prefix with \"Objective:\", \"Log:\", or any other label or header.\r\n\r\n" +

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
