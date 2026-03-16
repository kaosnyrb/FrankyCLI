using Retrograde;
using Retrograde.Story;
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
            var prompt =
                "Generate a three-word-or-less object name that contains a clue to a character's location, intentions, or next move.\r\n" +
                "Only output the object name.\r\n" +
                "- Use literal descriptors from the context (e.g., 'Dockmaster Ledger', 'Sealed Cargo Case').\r\n" +
                "- No cryptic phrases or invented names beyond the context.\r\n" +
                "- Style: plain and concrete.\r\n\r\n" +
                "Additional Information:\r\n";

            foreach (var item in Addons)
                prompt += item;

            var envelope = PromptContext.CurrentEnvelope;
            if (envelope != null)
                return ValidatedPrompt.Run(envelope, prompt).Trim().Trim('"');

            var results = AITools.RunPrompt(prompt);
            for (int i = 0; i < 10 && results.Length > 200; i++)
            {
                results = AITools.RunPrompt(prompt);
            }
            return results;
        }

        // ------------------------------
        // Contraband to Destroy
        // ------------------------------
        public static string GetDestroyActivatorName(List<string> Addons)
        {
            var prompt =
                "Generate a three-word-or-less contraband item name.\r\n" +
                "It should thematically match the context — illicit items, forbidden data, or black-market goods.\r\n" +
                "Only output the contraband name.\r\n" +
                "- Use literal descriptors tied to the context; avoid cryptic wording.\r\n" +
                "- No invented proper nouns beyond the context.\r\n" +
                "- Style: plain and direct.\r\n\r\n" +
                "Additional Information:\r\n";

            foreach (var item in Addons)
                prompt += item;

            var envelope = PromptContext.CurrentEnvelope;
            if (envelope != null)
                return ValidatedPrompt.Run(envelope, prompt).Trim().Trim('"');

            var results = AITools.RunPrompt(prompt);
            for (int i = 0; i < 10 && results.Length > 200; i++)
            {
                results = AITools.RunPrompt(prompt);
            }
            return results;
        }
    }
}
