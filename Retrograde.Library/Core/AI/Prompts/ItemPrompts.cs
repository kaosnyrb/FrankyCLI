using Retrograde;
using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public class ItemPrompts
    {
        // ------------------------------
        // Clue Object Name
        // ------------------------------
        public static string GetActivatorName(List<string> Addons)
        {
            var datasourceprompt =
                "Generate a three-word-or-less object name that contains a clue to a character's location, intentions, or next move.\r\n" +
                "Use tone, themes, symbols, and motifs from the LoreContext established earlier in this conversation.\r\n" +
                "Do NOT quote lore; infer from it.\r\n" +
                "Only output the object name.\r\n" +
                "- Use literal descriptors pulled from the LoreContext (e.g., 'Dockmaster Ledger', 'Sealed Cargo Case').\r\n" +
                "- No cryptic phrases or invented names beyond the LoreContext.\r\n" +
                "- Style: plain and concrete.\r\n\r\n" +

                "You may use any relevant elements in the LoreContext (Summary, TargetProfile, Rumors, Leads, Locations, Motives, Threats, MysteryElements).\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                datasourceprompt += item;

            var results = AITools.RunPrompt(datasourceprompt);
            for (int i = 0; i < 10 && results.Length > 200; i++)
            {
                results = AITools.RunPrompt(datasourceprompt);
            }
            return results;
        }

        // ------------------------------
        // Contraband to Destroy
        // ------------------------------
        public static string GetDestroyActivatorName(List<string> Addons)
        {
            var datasourceprompt =
                "Generate a three-word-or-less contraband item name.\r\n" +
                "It should thematically match the LoreContext established earlier in this conversation and feel appropriate for the conflict, intrigue, and stakes described there.\r\n" +
                "Think in terms of illicit items, forbidden data, compromised artifacts, or black-market goods that could drive the story forward.\r\n" +
                "Only output the contraband name.\r\n" +
                "- Use literal descriptors tied to the LoreContext; avoid cryptic wording.\r\n" +
                "- No invented proper nouns beyond the LoreContext.\r\n" +
                "- Style: plain and direct.\r\n\r\n" +

                "Use any relevant parts of the LoreContext (Summary, TargetProfile, Rumors, Leads, Locations, Motives, Threats, MysteryElements) for tone and flavor.\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                datasourceprompt += item;

            var results = AITools.RunPrompt(datasourceprompt);
            for (int i = 0; i < 10 && results.Length > 200; i++)
            {
                results = AITools.RunPrompt(datasourceprompt);
            }
            return results;
        }
    }
}
