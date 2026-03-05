using Retrograde;
using Retrograde.Chains;
using Retrograde.Nouns;
using Retrograde.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Retrograde.AI.Utils
{
    public class LorePrompts
    {
        public static string LoreContext;

        // Template names extracted from <PlannedArc> — passed directly to AI_TemplateEngine
        public static string PlannedDiscovery = "";
        public static string PlannedInvestigation = "";
        public static string PlannedShowdown = "";

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

        public static void GenerateLoreContext(OutlawNpc outlawNpc, string lorefile, TemplateLib templateLib)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are completing a partially written Lore Context File for a Starfield-style outlaw.");
            sb.AppendLine("The Lore Context File is the primary source of truth and MUST be treated as canonical.");
            sb.AppendLine("You will use the outlaw NPC's background ONLY to adapt and enrich this existing lore, not replace it.");
            sb.AppendLine();
            sb.AppendLine("Here is the Lore Context File you MUST build from and respect:");
            sb.AppendLine();
            sb.Append(lorefile);
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("Here is the outlaw NPC this Lore must be aligned with:");
            sb.AppendLine($"- Name: {outlawNpc.name}");
            sb.AppendLine($"- Gender: {outlawNpc.gender}");
            sb.AppendLine($"- Background: {outlawNpc.Upbringing}");
            sb.AppendLine($"- Core fear: {outlawNpc.Fear}");
            sb.AppendLine($"- Behavioural quirk: {outlawNpc.Quirk}");
            sb.AppendLine($"- Currently preoccupied with: {outlawNpc.CurrentPreoccupation}");
            sb.AppendLine($"- Being hunted by: {outlawNpc.HuntingFaction}");
            sb.AppendLine($"- Quest theme: {FlavourSeedData.GetQuestTheme()}");
            sb.AppendLine();
            sb.AppendLine("Your task: generate a full lore instance by completing every section that contains instructions.");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- Treat the existing Lore Context File as canon. Do NOT contradict it.");
            sb.AppendLine("- Reuse and expand on existing names, locations, factions, motifs, and events already in the Lore Context File whenever possible.");
            sb.AppendLine("- Only introduce new factions, locations, or concepts when a section explicitly calls for it or when absolutely necessary.");
            sb.AppendLine("- Follow the structure and tags exactly as provided.");
            sb.AppendLine("- For each section that contains instructions (such as <Faction>, <TreasureLegend>, <HistoricalContext>, etc.), replace the instructional text with a fully written lore entry.");
            sb.AppendLine("- Do NOT generate separate entries for each faction; produce a single consolidated lore section per tag, even if multiple factions are mentioned or implied.");
            sb.AppendLine("- Do NOT add new tags or remove existing ones.");
            sb.AppendLine("- Each generated lore section must be no more than 3\u20136 sentences.");
            sb.AppendLine("- Preserve the order and hierarchy of the Lore Context File.");
            sb.AppendLine($"- The Lore Context File is based on the outlaw NPC we just generated: {outlawNpc.name}.");
            sb.AppendLine("- Use the character's background to interpret and color the existing Lore Context, but do NOT discard or ignore the original lore.");
            sb.AppendLine("- Update the <Summary> and <StorySummary> to fit the outlaw we've generated, by merging the existing lore with the character background.");
            sb.AppendLine("- When updating <Summary> and <StorySummary>, preserve core themes, key events, and factions from the original Lore Context File.");
            sb.AppendLine("- Expand only sections that contain generation instructions.");
            sb.AppendLine("- Do NOT output explanations. Output ONLY the completed lore instance followed by the <PlannedArc> section described below.");
            sb.AppendLine();
            sb.AppendLine("After completing the lore instance, append a <PlannedArc> section that plans the full quest arc.");
            sb.AppendLine("Choose one template from each stage below. Copy the Name exactly as written — no paraphrasing.");
            sb.AppendLine();
            AppendTemplateMenu(sb, "DISCOVERY", templateLib.DiscoveryTemplates);
            AppendTemplateMenu(sb, "INVESTIGATION", templateLib.InvestigationTemplates);
            AppendTemplateMenu(sb, "SHOWDOWN", templateLib.ShowdownTemplates);
            sb.AppendLine("Append this section after the completed lore instance:");
            sb.AppendLine();
            sb.AppendLine("<PlannedArc>");
            sb.AppendLine("    <Discovery>");
            sb.AppendLine("        <Theme>2\u20133 sentences: what kind of opening hook fits this outlaw's story.</Theme>");
            sb.AppendLine("        <Template>exact Name from the DISCOVERY list above</Template>");
            sb.AppendLine("    </Discovery>");
            sb.AppendLine("    <Investigation>");
            sb.AppendLine("        <Theme>2\u20133 sentences: what the investigation should uncover and how it escalates.</Theme>");
            sb.AppendLine("        <Template>exact Name from the INVESTIGATION list above</Template>");
            sb.AppendLine("    </Investigation>");
            sb.AppendLine("    <Showdown>");
            sb.AppendLine("        <Theme>2\u20133 sentences: what makes this climax feel like the logical end of this specific story.</Theme>");
            sb.AppendLine("        <Template>exact Name from the SHOWDOWN list above</Template>");
            sb.AppendLine("    </Showdown>");
            sb.AppendLine("</PlannedArc>");
            sb.AppendLine();
            sb.AppendLine("Rules for <PlannedArc>:");
            sb.AppendLine("- Each template Name MUST be copied exactly from the lists above.");
            sb.AppendLine("- Pick templates whose location, description, and tags match the outlaw's background, factions, and crimes.");
            sb.AppendLine("- The three templates must form a coherent escalating arc.");
            sb.AppendLine("- Prefer variety of environment across the three stages (e.g. avoid three space missions in a row).");
            sb.AppendLine("- The <PlannedArc> is appended AFTER all other sections — do not merge it into the lore.");
            sb.AppendLine();
            sb.AppendLine("Generate the completed lore instance now, then append the <PlannedArc>.");

            LoreContext = AITools.RunPrompt(sb.ToString());

            PlannedDiscovery    = ExtractPlannedTemplate(LoreContext, "Discovery");
            PlannedInvestigation = ExtractPlannedTemplate(LoreContext, "Investigation");
            PlannedShowdown     = ExtractPlannedTemplate(LoreContext, "Showdown");

            Console.WriteLine($"PlannedArc — Discovery: {PlannedDiscovery}");
            Console.WriteLine($"PlannedArc — Investigation: {PlannedInvestigation}");
            Console.WriteLine($"PlannedArc — Showdown: {PlannedShowdown}");
        }

        private static string ExtractPlannedTemplate(string context, string stage)
        {
            var stageMatch = Regex.Match(context, $@"<{stage}>([\s\S]*?)</{stage}>", RegexOptions.IgnoreCase);
            if (!stageMatch.Success) return "";

            var templateMatch = Regex.Match(stageMatch.Groups[1].Value, @"<Template>([\s\S]*?)</Template>", RegexOptions.IgnoreCase);
            if (!templateMatch.Success) return "";

            // AI sometimes copies the full "Name | Location | Tags" line — strip everything from '|' onward
            var raw = templateMatch.Groups[1].Value.Trim();
            var pipeIdx = raw.IndexOf('|');
            return pipeIdx >= 0 ? raw.Substring(0, pipeIdx).Trim() : raw;
        }

        private static void AppendTemplateMenu(StringBuilder sb, string label, List<MissionTemplate> templates)
        {
            sb.AppendLine($"{label} templates:");
            foreach (var t in templates)
            {
                // Skip SpaceCell environment variants — the plain entry represents the same concept
                if (t.parameters != null && t.parameters.ContainsKey("SpaceCell")) continue;

                var tags = t.MissionTags?.Count > 0 ? string.Join(", ", t.MissionTags) : "none";
                sb.AppendLine($"  {t.Name} | {t.Location} | Tags: {tags}");
            }
            sb.AppendLine();
        }

        public static string GenerateLoreFile(string goal, string flaw, string occupation, string crime)
        {
            string seed = $"The outlaw was a {occupation} who {crime}, driven by {goal}. Personality: {flaw}.";

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
