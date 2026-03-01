using Mutagen.Bethesda.Archives;
using Mutagen.Bethesda.Starfield;
using Retrograde;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Joins;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Collections.Specialized.BitVector32;

namespace Retrograde.AI.Utils
{
    public class PromptManager
    {
        public static string LoreContext;

        public static string LoadRandomLoreFile()
        {
            string loreDir = @".\questgen_quests\Lorefiles";

            // Get all .md files
            var files = Directory.GetFiles(loreDir, "*.md", SearchOption.TopDirectoryOnly);

            if (files.Length == 0)
            {
                throw new FileNotFoundException($"No .md files found in: {Path.GetFullPath(loreDir)}");
            }

            // Pick random file
            string randomFile = files[RandomProvider.Random.Next(files.Length)];

            // Return contents
            return File.ReadAllText(randomFile);
        }

        public static string GenerateLoreFile()
        {
            var rng = RandomProvider.Random;
            string occupation = SeedManager.Occupations[rng.Next(SeedManager.Occupations.Count)];
            string crime      = SeedManager.Crimes[rng.Next(SeedManager.Crimes.Count)];
            string motive     = SeedManager.Motives[rng.Next(SeedManager.Motives.Count)];
            string personality = SeedManager.PersonalityTraits[rng.Next(SeedManager.PersonalityTraits.Count)];

            string seed = $"The outlaw was a {occupation} who {crime}, driven by {motive}. Personality: {personality}.";

            var sb = new StringBuilder();

            sb.AppendLine("Generate a LoreFile for a Starfield-style bounty hunting mission system.");
            sb.AppendLine();
            sb.AppendLine("Use this story seed exactly — do not replace or reinterpret it:");
            sb.AppendLine(seed);
            sb.AppendLine();
            sb.AppendLine("SCALE: This is a small, personal crime story. Local stakes only.");
            sb.AppendLine("The outlaw is not a mastermind or a warlord. No cults, no conspiracies, no galaxy-threatening plots.");
            sb.AppendLine("Think: a desperate person who made a bad choice and is now running from the consequences.");
            sb.AppendLine();
            sb.AppendLine("OUTPUT (use this format exactly):");
            sb.AppendLine();
            sb.AppendLine("< LoreFile >");
            sb.AppendLine();
            sb.AppendLine("    < Summary >");
            sb.AppendLine("        2-3 sentences. Who the outlaw is, what they did, and what kind of trouble they are in now.");
            sb.AppendLine("    </ Summary >");
            sb.AppendLine();
            sb.AppendLine("    < TargetProfile >");
            sb.AppendLine("        - Former occupation and any skills or connections it gave them");
            sb.AppendLine("        - Psychological traits and how they behave under pressure");
            sb.AppendLine("        - Habits, tells, or patterns a hunter could exploit");
            sb.AppendLine("        - What tipped them over into crime");
            sb.AppendLine("    </ TargetProfile >");
            sb.AppendLine();
            sb.AppendLine("    < Motives >");
            sb.AppendLine("        2-3 key drivers: what they want now, what they fear, what they refuse to give up.");
            sb.AppendLine("    </ Motives >");
            sb.AppendLine();
            sb.AppendLine("</ LoreFile >");
            sb.AppendLine();
            sb.AppendLine("Output the LoreFile only. Be concise.");

            var prompt = sb.ToString();

            var results = AITools.RunPrompt(prompt);

            for (int i = 0; i < 10 && results.Length < 200; i++)
            {
                results = AITools.RunPrompt(prompt);
            }
            return results;
        }

        // ------------------------------
        // Quest Name
        // ------------------------------
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


            var results = AITools.RunPrompt(pickuppromt);
            for (int i = 0; i < 10 && results.Length < 100; i++)
            {
                results = AITools.RunPrompt(pickuppromt);
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

            var results = AITools.RunPrompt(pickuppromt);
            for (int i = 0; i < 10 && results.Length < 100; i++)
            {
                results = AITools.RunPrompt(pickuppromt);
            }

            return results;
        }

        // ------------------------------
        // Objective Log Text
        // ------------------------------
        public static string GetLogMessage(List<string> Addons)
        {

            var logprompt =
                "Write a 40-word objective log entry for a bounty hunter.\r\n" +
                "State clearly: what the objective is, where it must be done, and why (the concrete reason tied to the target or situation).\r\n" +
                "Name the bounty target exactly as established in the LoreContext.\r\n" +
                "Style: field intel note — plain declarative sentences, no metaphor, no ominous hints, no atmospheric writing.\r\n" +
                "Use the LoreContext established earlier in this conversation for concrete facts only: target name, faction, motive, location. Do not invent new names.\r\n\r\n" +

                "Additional Information:\r\n";



            foreach (var item in Addons)
                logprompt += item;

            var results = AITools.RunPrompt(logprompt);
            for(int i = 0; i < 10 && results.Length < 100; i++)
            {
                results = AITools.RunPrompt(logprompt);
            }
            return results;
        }

        // ------------------------------
        // First Person Account
        // ------------------------------
        public static string GetFirstPersonAccount(List<string> Addons)
        {
            DateTime dateTime = new DateTime(2330, 5, 6);
            string speaker = SeedManager.SpeakerTypes[RandomProvider.Random.Next(SeedManager.SpeakerTypes.Count)];

            var logprompt =
                "Write a short personal dataslate entry — a first-person account from " + speaker + ".\r\n" +
                "The entry relates to the events described in the LoreContext established earlier in this conversation. The speaker is not the outlaw; they know only their piece of the story.\r\n\r\n" +

                "Rules:\r\n" +
                "- Under 80 words. Every sentence must add new information; cut anything that restates or pads.\r\n" +
                "- Write one specific moment or discovery the speaker witnessed or experienced themselves.\r\n" +
                "- Use concrete details from the LoreContext — a name, a place, an action. Do not invent names.\r\n" +
                "- Plain, personal speech. This person is writing for themselves, not performing.\r\n" +
                "- The speaker does not know the full story — they know their part of it.\r\n\r\n" +

                "Date: optional. If used, it must fall between " +
                    dateTime.AddYears(-3).ToString("yyyy-MM-dd") + " and " + dateTime.ToString("yyyy-MM-dd") +
                    " and feel like a natural part of the entry, not a header.\r\n\r\n" +

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
                "Write a mission briefing dataslate for a bounty hunter. Length: 180-220 words.\r\n" +
                "Style: field intel note — plain declarative sentences, no atmosphere, no tone-setting prose, no metaphor.\r\n" +
                "Use the LoreContext established earlier in this conversation for concrete facts only: target name, occupation, crime, motive. Do not invent names or factions.\r\n\r\n" +

                "Cover these four things in order:\r\n" +
                "1. TARGET: Name the target exactly as established in the LoreContext. State what they did and what they are wanted for.\r\n" +
                "2. BACKGROUND: One or two sentences on who they are — former occupation, what pushed them to crime — so the hunter understands who they're dealing with.\r\n" +
                "3. LEAD: Identify the first location from the provided context. State plainly why the target is likely there.\r\n" +
                "4. ACTION: Tell the hunter exactly what to do at that location.\r\n\r\n" +

                "Additional Information:\r\n";

            foreach (var item in Addons)
                logprompt += item;

            var results = AITools.RunPrompt(logprompt);

            // retry if the model undershoots the length significantly
            for (int i = 0; i < 5 && results.Length < 800; i++)
            {
                results = AITools.RunPrompt(logprompt);
            }
            return results;
        }

    }

}
