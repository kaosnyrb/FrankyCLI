using Retrograde;
using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public class NarrativePrompts
    {
        // ------------------------------
        // First Person Account
        // ------------------------------
        public static string GetFirstPersonAccount(List<string> Addons)
        {
            string speaker = SeedManager.SpeakerTypes[RandomProvider.Random.Next(SeedManager.SpeakerTypes.Count)];

            var logprompt =
                "Write a short personal dataslate entry — a first-person account from " + speaker + ".\r\n" +
                "The entry relates to the events described in the LoreContext established earlier in this conversation. The speaker is not the outlaw; they know only their piece of the story.\r\n\r\n" +

                "Rules:\r\n" +
                "- Under 80 words. Every sentence must add new information; cut anything that restates or pads.\r\n" +
                "- Write one specific moment or discovery the speaker witnessed or experienced themselves.\r\n" +
                "- Use concrete details from the LoreContext — a name, a place, an action. Do not invent names.\r\n" +
                "- Plain, personal speech. This person is writing for themselves, not performing.\r\n" +
                "- The speaker does not know the full story — they know their part of it.\r\n" +
                "- Do NOT include a date, timestamp, or header of any kind.\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                logprompt += item;

            var results = AITools.RunPrompt(logprompt);

            for (int i = 0; i < 10 && results.Length < 100; i++)
            {
                results = AITools.RunPrompt(logprompt);
            }
            return results;
        }

        // ------------------------------
        // Derelict Ship Transmission
        // ------------------------------
        public static string GetTransmission(List<string> Addons)
        {
            string transmissionType = SeedManager.TransmissionTypes[RandomProvider.Random.Next(SeedManager.TransmissionTypes.Count)];

            var prompt =
                "Write " + transmissionType + " that the player finds aboard a derelict ship in deep space.\r\n" +
                "This is a short audio recording played back through a data-slate — write it for voice performance, not for reading.\r\n\r\n" +

                "Rules:\r\n" +
                "- Under 100 words. Every word will be spoken aloud — make each one count.\r\n" +
                "- Pure spoken audio — no headers, bullet points, or labels.\r\n" +
                "- Do NOT open with a recording preamble — no 'Recording...', no date stamp, no name announcement. Go straight into the content.\r\n" +
                "- The recording must reference or hint at the next destination — where the trail leads from here.\r\n" +
                "- Use the LoreContext for concrete names, faction, and location. Do not invent new names.\r\n" +
                "- Match the tone to the type of transmission: urgency for a distress signal, cold precision for a coded dead drop, fear for a last warning.\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                prompt += item;

            var result = AITools.RunPrompt(prompt);
            for (int i = 0; i < 10 && result.Length < 50; i++)
                result = AITools.RunPrompt(prompt);
            return result;
        }

        // ------------------------------
        // Mission Briefing Dataslate
        // ------------------------------
        public static string GetMissionBriefingDataslate(List<string> Addons)
        {
            var logprompt =
                "Write a mission briefing dataslate for a bounty hunter. Length: 120-150 words.\r\n" +
                "Style: field intel note — plain declarative sentences, no atmosphere, no tone-setting prose, no metaphor.\r\n" +
                "Use the LoreContext established earlier in this conversation for concrete facts only: target name, occupation, crime, motive. Do not invent names or factions.\r\n\r\n" +

                "Cover these four things in order, with no headers or labels:\r\n" +
                "- Name the target exactly as established in the LoreContext. State what they did and what they are wanted for.\r\n" +
                "- One sentence on who they are — former occupation, what pushed them to crime.\r\n" +
                "- Identify the first location from the provided context. State plainly why the target is likely there.\r\n" +
                "- Tell the hunter exactly what to do at that location.\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                logprompt += item;

            var results = AITools.RunPrompt(logprompt);

            for (int i = 0; i < 5 && results.Length < 500; i++)
            {
                results = AITools.RunPrompt(logprompt);
            }
            return results;
        }
    }
}
