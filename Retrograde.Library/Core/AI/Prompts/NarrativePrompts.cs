using Retrograde.Nouns;
using System.Collections.Generic;
using System.Text;

namespace Retrograde.AI.Utils
{
    public class NarrativePrompts
    {
        // ------------------------------
        // First Person Account
        // ------------------------------
        public static string GetFirstPersonAccount(List<string> Addons)
        {
            string speaker = NarrativeSeedData.SpeakerTypes[RandomProvider.Random.Next(NarrativeSeedData.SpeakerTypes.Count)];

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
            string transmissionType = NarrativeSeedData.TransmissionTypes[RandomProvider.Random.Next(NarrativeSeedData.TransmissionTypes.Count)];

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
        // Outlaw Personal Log
        // ------------------------------
        public static string GetOutlawLogfile(string name, string gender, OutlawTraits traits)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Write the final personal audio log of " + name + ", a " + gender + " fugitive now dead — the player has just killed them and found this recording on their body.");
            sb.AppendLine("This is the epilogue of the hunt. It should give the player a moment of understanding: who this person really was, what they were carrying, and why it ended the way it did.");
            sb.AppendLine("This is a spoken monologue recorded alone into a data-slate — write it for voice performance, not for reading.");
            sb.AppendLine();

            sb.AppendLine("Character context — use the LoreContext established earlier in this conversation. It is the source of truth for who this person is and why they were running.");
            sb.AppendLine();
            string tone = NarrativeSeedData.LogTones[RandomProvider.Random.Next(NarrativeSeedData.LogTones.Count)];

            sb.AppendLine("Tone: " + tone + ". Underneath it, there should be a sense that they knew how this might end.");
            sb.AppendLine();
            sb.AppendLine("Voice delivery rules — follow these exactly:");
            sb.AppendLine("- You may use these audio tags sparingly, only where they add genuine stress or fear: [sighs], [whispers], [exhales sharply].");
            sb.AppendLine("- Do NOT use [laughs] or any tag suggesting levity or relief.");
            sb.AppendLine("- Use ellipses (...) for hesitation, a thought that collapses, or words they can't finish.");
            sb.AppendLine("- Use an em dash (—) for an abrupt self-correction or a thought cut short by nerves.");
            sb.AppendLine("- CAPITALIZE a single word only when fear or desperation forces it out louder than the rest.");
            sb.AppendLine("- Write as natural stressed speech: stumbles, fragments, and restarts are right for this character.");
            sb.AppendLine("- No headers, bullet points, or any formatting — this is pure spoken audio.");
            sb.AppendLine("- Do NOT open with any recording preamble — no 'Recording...', no stating their name, no date stamp. Go straight into the content.");
            sb.AppendLine();
            sb.AppendLine("Content:");
            sb.AppendLine("- The emotional core of this log is: " + traits.CurrentPreoccupation + ".");
            sb.AppendLine("- It should feel like a closing chapter — something resolved, accepted, or finally said out loud.");
            sb.AppendLine("- Total length: under 140 words. Every word will be read aloud, so make each one count.");

            Console.WriteLine("Generating Outlaw Log...");

            string basePrompt = sb.ToString();
            string prompt = FlavourSeedData.AddFlavourToTargetBook(basePrompt);
            string result = AITools.RunPrompt(prompt);

            // If the flavoured prompt triggered a refusal, retry with the base prompt only
            if (IsRefusal(result))
            {
                Console.WriteLine("Outlaw Log: flavour prompt refused — retrying without flavour.");
                result = AITools.RunPrompt(basePrompt);
            }

            return result;
        }

        private static bool IsRefusal(string response)
        {
            if (string.IsNullOrWhiteSpace(response)) return true;
            var head = response.TrimStart().ToLowerInvariant();
            return head.StartsWith("i don't think i can")
                || head.StartsWith("i can't write")
                || head.StartsWith("i cannot write")
                || head.StartsWith("i'm not able to write")
                || head.StartsWith("i won't write")
                || head.StartsWith("i'd rather not write");
        }

        // ------------------------------
        // Mission Briefing Dataslate
        // ------------------------------
        public static string GetMissionBriefingDataslate(List<string> Addons)
        {
            var logprompt =
                "Write a mission briefing dataslate for a bounty hunter. Length: 120-150 words.\r\n" +
                "Style: write as if a fixer dropped the hunter a terse field note mid-route — short bursts, functional shorthand, no fluff. Allow one hedged construction per piece (e.g. 'believed to be', 'last reported moving through', 'confirmed using aliases'). No atmosphere, no metaphor. No headers or labels of any kind.\r\n" +
                "Use the LoreContext established earlier in this conversation for concrete facts only: target name, occupation, crime, motive. Do not invent names or factions.\r\n\r\n" +

                "Cover these three things in order:\r\n" +
                "- Name the target exactly as established in the LoreContext. State what they did, what they are wanted for, and who they are — former occupation, what they did that crossed the line.\r\n" +
                "- Identify the first location from the provided context. State plainly why the target is likely there.\r\n" +
                "- Tell the hunter exactly what to do at that location. Close with a concrete urgency hook — a named rival also working the bounty, a contact who moves on quickly, or a window that closes soon.\r\n" +

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
