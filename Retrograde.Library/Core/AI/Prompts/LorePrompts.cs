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
        public static List<string> PlannedInvestigations = new();
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

        public static void GenerateLoreContext(OutlawNpc outlawNpc, string lorefile, TemplateLib templateLib,
            string? pinnedDiscovery = null, string? pinnedShowdown = null, List<string>? pinnedInvestigations = null,
            bool selectArc = true)
        {
            // ── Phase 1: Generate lore only (added to history) ──────────────────────────
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
            outlawNpc.Traits.AppendToPrompt(sb);
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
            sb.AppendLine("- Each generated lore section must be no more than 2\u20133 sentences.");
            sb.AppendLine("- Preserve the order and hierarchy of the Lore Context File.");
            sb.AppendLine($"- The Lore Context File is based on the outlaw NPC we just generated: {outlawNpc.name}.");
            sb.AppendLine("- Use the character's background to interpret and color the existing Lore Context, but do NOT discard or ignore the original lore.");
            sb.AppendLine("- Update the <Summary> and <StorySummary> to fit the outlaw we've generated, by merging the existing lore with the character background.");
            sb.AppendLine("- When updating <Summary> and <StorySummary>, preserve core themes, key events, and factions from the original Lore Context File.");
            sb.AppendLine("- Expand only sections that contain generation instructions.");
            sb.AppendLine("- Do NOT output explanations. Output ONLY the completed lore instance.");

            LoreContext = AITools.RunPrompt(sb.ToString());

            // ── Phase 2: Select arc templates (stateless — does NOT enter history) ──────
            if (!selectArc) return;

            var sb2 = new StringBuilder();

            sb2.AppendLine($"The lore context for {outlawNpc.name} has just been generated. Using that context, design a full quest arc.");
            sb2.AppendLine("Some slots are already decided (marked PINNED). For pinned slots, copy the template name exactly and write only the Theme.");
            sb2.AppendLine("For free slots, choose from the lists below — copy each Name exactly as written, no paraphrasing.");
            sb2.AppendLine();

            bool freeDiscovery     = string.IsNullOrEmpty(pinnedDiscovery);
            bool freeShowdown      = string.IsNullOrEmpty(pinnedShowdown);
            bool freeInvestigation = pinnedInvestigations == null || pinnedInvestigations.Count == 0;

            if (freeDiscovery)     AppendTemplateMenu(sb2, "DISCOVERY", templateLib.DiscoveryTemplates);
            if (freeInvestigation) AppendTemplateMenu(sb2, "INVESTIGATION", templateLib.InvestigationTemplates);
            if (freeShowdown)      AppendTemplateMenu(sb2, "SHOWDOWN", templateLib.ShowdownTemplates);

            sb2.AppendLine("Output only the following XML block:");
            sb2.AppendLine();
            sb2.AppendLine("<PlannedArc>");

            // Discovery
            sb2.AppendLine("    <Discovery>");
            sb2.AppendLine("        <Theme>1 sentence: what kind of opening hook fits this outlaw's story.</Theme>");
            sb2.AppendLine(freeDiscovery
                ? "        <Template>exact Name from the DISCOVERY list above</Template>"
                : $"        <Template>{pinnedDiscovery}</Template>");
            sb2.AppendLine("    </Discovery>");

            // Investigation
            sb2.AppendLine("    <Investigation>");
            if (freeInvestigation)
            {
                sb2.AppendLine("        <!-- Choose between 2 and 5 Stage entries in story order (earliest lead first, closest to showdown last) -->");
                sb2.AppendLine("        <Stage>");
                sb2.AppendLine("            <Theme>1 sentence: what this step uncovers and how it escalates toward the next.</Theme>");
                sb2.AppendLine("            <Template>exact Name from the INVESTIGATION list above</Template>");
                sb2.AppendLine("        </Stage>");
                sb2.AppendLine("        <!-- repeat <Stage> for each additional investigation step -->");
            }
            else
            {
                foreach (var t in pinnedInvestigations!)
                {
                    sb2.AppendLine("        <Stage>");
                    sb2.AppendLine("            <Theme>1 sentence: what this step uncovers and how it escalates toward the next.</Theme>");
                    sb2.AppendLine($"            <Template>{t}</Template>");
                    sb2.AppendLine("        </Stage>");
                }
            }
            sb2.AppendLine("    </Investigation>");

            // Showdown
            sb2.AppendLine("    <Showdown>");
            sb2.AppendLine("        <Theme>1 sentence: what makes this climax feel like the logical end of this specific story.</Theme>");
            sb2.AppendLine(freeShowdown
                ? "        <Template>exact Name from the SHOWDOWN list above</Template>"
                : $"        <Template>{pinnedShowdown}</Template>");
            sb2.AppendLine("    </Showdown>");

            sb2.AppendLine("</PlannedArc>");
            sb2.AppendLine();
            sb2.AppendLine("Rules:");
            if (freeDiscovery || freeInvestigation || freeShowdown)
                sb2.AppendLine("- For free slots, each template Name MUST be copied exactly from the lists above.");
            sb2.AppendLine("- Pinned slots are already decided — copy the template name as-is and only write the Theme.");
            sb2.AppendLine("- The arc must feel like a coherent escalation from first lead to final confrontation.");
            if (freeInvestigation)
            {
                sb2.AppendLine("- Use between 2 and 5 investigation stages — choose however many best serve this specific story.");
                sb2.AppendLine("- Prefer variety of environment across stages (e.g. avoid all-space or all-planet runs).");
                sb2.AppendLine("- Each investigation stage must use a different mission TYPE (the part before the dash). For example, you may NOT pick 'Planetside Smallbase Informant - Trade Authority Broker' AND 'Planetside Smallbase Informant - Crimson Fleet Smuggling Liaison' because both are the same type 'Planetside Smallbase Informant'. Pick at most ONE variant of each type.");
            }

            string arcResponse = AITools.RunPrompt(sb2.ToString());

            PlannedDiscovery      = ExtractPlannedTemplate(arcResponse, "Discovery");
            PlannedInvestigations = ExtractPlannedInvestigations(arcResponse);
            PlannedShowdown       = ExtractPlannedTemplate(arcResponse, "Showdown");

            // Inject the full arc (including Theme rationale) as silent context so all
            // subsequent stage prompts know why each template was chosen.
            AITools.InjectContextIntoHistory(
                "The following planned arc has been selected for this quest. " +
                "The <Theme> elements describe the intended narrative role of each stage — " +
                "use them to guide tone and content when generating each stage:\r\n\r\n" +
                arcResponse);

            Console.WriteLine($"PlannedArc — Discovery: {PlannedDiscovery}");
            for (int i = 0; i < PlannedInvestigations.Count; i++)
                Console.WriteLine($"PlannedArc — Investigation[{i}]: {PlannedInvestigations[i]}");
            Console.WriteLine($"PlannedArc — Showdown: {PlannedShowdown}");
        }

        private static List<string> ExtractPlannedInvestigations(string context)
        {
            var results = new List<string>();

            var investigationMatch = Regex.Match(context, @"<Investigation>([\s\S]*?)</Investigation>", RegexOptions.IgnoreCase);
            if (!investigationMatch.Success) return results;

            var stagesBlock = investigationMatch.Groups[1].Value;
            var stageMatches = Regex.Matches(stagesBlock, @"<Stage>([\s\S]*?)</Stage>", RegexOptions.IgnoreCase);

            foreach (Match stageMatch in stageMatches)
            {
                var templateMatch = Regex.Match(stageMatch.Groups[1].Value, @"<Template>([\s\S]*?)</Template>", RegexOptions.IgnoreCase);
                if (!templateMatch.Success) continue;

                var raw = templateMatch.Groups[1].Value.Trim();
                var pipeIdx = raw.IndexOf('|');
                results.Add(pipeIdx >= 0 ? raw.Substring(0, pipeIdx).Trim() : raw);
            }

            // Deduplicate by mission type (prefix before first " - ")
            var seenTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deduped = new List<string>();
            foreach (var name in results)
            {
                var dashIdx = name.IndexOf(" - ");
                var missionType = dashIdx >= 0 ? name.Substring(0, dashIdx).Trim() : name.Trim();
                if (seenTypes.Add(missionType))
                    deduped.Add(name);
                else
                    Console.WriteLine($"PlannedArc: Dropped duplicate mission type '{missionType}' (template: {name})");
            }
            results = deduped;

            // Clamp to valid range
            if (results.Count < 2) Console.WriteLine("PlannedArc WARNING: fewer than 2 investigation stages returned; chain will be short.");
            if (results.Count > 5) results = results.GetRange(0, 5);

            return results;
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

        public static string GenerateLoreFile(OutlawTraits traits)
        {
            string seed = $"The outlaw was a {traits.Occupation} who {traits.Crime}, driven by {traits.Goal}. Personality: {traits.Flaw}.";

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
