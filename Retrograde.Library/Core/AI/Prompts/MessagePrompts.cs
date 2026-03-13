using Retrograde;
using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public class MessagePrompts
    {
        // ------------------------------
        // Destroy Message
        // ------------------------------
        public static string GetDestroyMessage(List<string> Addons)
        {
            var pickuppromt =
                "Write a short in-game notification (under 40 words, one paragraph) for when the player destroys a piece of contraband.\r\n" +
                "State three things plainly: what was destroyed, what that destruction revealed, and where to go or what to do next.\r\n" +
                "Style: field intel note — direct, factual, no metaphor, no atmospheric writing, no mood adjectives.\r\n" +
                "Use the LoreContext established earlier in this conversation for concrete facts only: names, places, roles. Do not derive atmosphere or mystery from it.\r\n" +
                "Do not invent names or locations beyond those in the LoreContext and Additional Information.\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                pickuppromt += item;

            var results = AITools.RunStatelessPrompt(pickuppromt);
            for (int i = 0; i < 10 && results.Length < 100; i++)
            {
                results = AITools.RunStatelessPrompt(pickuppromt);
            }
            return results;
        }

        // ------------------------------
        // Pickup Message
        // ------------------------------
        public static string GetPickupMessage(List<string> Addons)
        {
            var pickuppromt =
                "Write a short in-game notification (under 30 words, one sentence or two short ones) for when the player picks up a clue.\r\n" +
                "State two things plainly: what was found, and how it points to the next step.\r\n" +
                "Style: field intel note — direct, factual, no metaphor, no atmospheric writing, no mood adjectives.\r\n" +
                "Use the LoreContext established earlier in this conversation for concrete facts only: names, places, roles. Do not derive atmosphere or mystery from it.\r\n" +
                "Do not invent names or locations beyond those in the LoreContext and Additional Information.\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                pickuppromt += item;

            var results = AITools.RunStatelessPrompt(pickuppromt);
            for (int i = 0; i < 10 && results.Length < 100; i++)
            {
                results = AITools.RunStatelessPrompt(pickuppromt);
            }
            return results;
        }
    }
}
