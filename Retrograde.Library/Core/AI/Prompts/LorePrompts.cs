using Retrograde;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Retrograde.AI.Utils
{
    public class LorePrompts
    {
        public static string LoreContext;

        public static string LoadRandomLoreFile()
        {
            string loreDir = @".\questgen_quests\Lorefiles";

            var files = Directory.GetFiles(loreDir, "*.md", SearchOption.TopDirectoryOnly);

            if (files.Length == 0)
            {
                throw new FileNotFoundException($"No .md files found in: {Path.GetFullPath(loreDir)}");
            }

            string randomFile = files[RandomProvider.Random.Next(files.Length)];

            return File.ReadAllText(randomFile);
        }

        public static string GenerateLoreFile()
        {
            var rng = RandomProvider.Random;
            string occupation  = SeedManager.Occupations[rng.Next(SeedManager.Occupations.Count)];
            string crime       = SeedManager.Crimes[rng.Next(SeedManager.Crimes.Count)];
            string motive      = SeedManager.Motives[rng.Next(SeedManager.Motives.Count)];
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
    }
}
