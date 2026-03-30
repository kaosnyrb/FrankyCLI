using Retrograde.Utils;

namespace Retrograde.AI.Utils
{
    /// <summary>
    /// Local madlibs generator for in-world item names.
    /// Replaces ItemPrompts AI calls so quest Setup() runs offline.
    /// </summary>
    public static class ItemMadlibs
    {
        /// <summary>
        /// Returns a 2-3 word name for a clue object the player picks up
        /// (data tablets, manifests, logs, etc.).
        /// </summary>
        public static string GetActivatorName()
        {
            return ItemSeedData.ClueItemNames[RandomProvider.Random.Next(ItemSeedData.ClueItemNames.Count)];
        }

        /// <summary>
        /// Returns a 2-3 word name for a contraband item the player must destroy.
        /// </summary>
        public static string GetDestroyActivatorName()
        {
            return ItemSeedData.ContrabandItemNames[RandomProvider.Random.Next(ItemSeedData.ContrabandItemNames.Count)];
        }
    }
}
