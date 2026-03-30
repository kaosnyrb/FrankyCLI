using Retrograde.Utils;
using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    /// <summary>
    /// Local madlibs generator for in-game notification messages shown to the player.
    /// Replaces MessagePrompts AI calls so quest Setup() runs offline.
    /// </summary>
    public static class MessageMadlibs
    {
        private static readonly List<string> PickupTemplates = new List<string>
        {
            "{item} recovered. Lead points to {location}.",
            "{item} acquired. Proceed to {location}.",
            "Found: {item}. Next stop: {location}.",
            "{item} secured. Head to {location}.",
            "Clue found: {item}. Move to {location}.",
        };

        private static readonly List<string> DestroyTemplates = new List<string>
        {
            "{item} destroyed. Trail leads to {location}.",
            "{item} eliminated. New lead: {location}. Move out.",
            "{item} gone. Head to {location}.",
            "Target down: {item}. Intel recovered — proceed to {location}.",
            "{item} neutralised. Next objective: {location}.",
        };

        /// <summary>
        /// Short notification shown when the player picks up a clue item.
        /// Under 30 words, states what was found and where to go next.
        /// </summary>
        public static string GetPickupMessage(string itemName, string nextLocation)
        {
            var template = PickupTemplates[RandomProvider.Random.Next(PickupTemplates.Count)];
            return template
                .Replace("{item}", itemName)
                .Replace("{location}", string.IsNullOrWhiteSpace(nextLocation) ? "the next location" : nextLocation);
        }

        /// <summary>
        /// Short notification shown when the player destroys a contraband item.
        /// Under 40 words, states what was destroyed and where to go next.
        /// </summary>
        public static string GetDestroyMessage(string itemName, string nextLocation)
        {
            var template = DestroyTemplates[RandomProvider.Random.Next(DestroyTemplates.Count)];
            return template
                .Replace("{item}", itemName)
                .Replace("{location}", string.IsNullOrWhiteSpace(nextLocation) ? "the next location" : nextLocation);
        }
    }
}
