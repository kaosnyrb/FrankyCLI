using Retrograde.AI;
using Retrograde.AI.Utils;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using System.Text;

namespace Retrograde.Quests.TemplateEngines
{
    public class AI_TemplateEngine : ITemplateEngine
    {
        public TemplateLib AvailableTemplateLib { get; set; } = new();

        public List<string> UsedTags = new();
        public List<string> UsedMissions = new();

        private MissionTemplate ApplyAddons(MissionTemplate template, List<string> addons)
        {
            if (template == null) return null;
            if (addons != null)
                template.Addons = new List<string>(addons); // clone to avoid shared references
            return template;
        }

        // Remove a random entry from the pool and return it
        private static MissionTemplate RemovePick(List<MissionTemplate> pool)
        {
            int i = RandomProvider.Random.Next(pool.Count);
            var t = pool[i];
            pool.RemoveAt(i);
            return t;
        }

        // Append a numbered mission list to the prompt
        private static void AppendMissionList(StringBuilder sb, List<MissionTemplate> pool)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                var tags = pool[i].MissionTags?.Count > 0
                    ? string.Join(", ", pool[i].MissionTags)
                    : "none";
                sb.AppendLine($"{i} :");
                sb.AppendLine($"  Location: {pool[i].Location}");
                sb.AppendLine($"  Description: {pool[i].Description}");
                sb.AppendLine($"  Tags: {tags}");
                sb.AppendLine();
            }
        }

        // Append the quest state addons block
        private static void AppendAddonContext(StringBuilder sb, List<string> addons)
        {
            sb.AppendLine("Additional quest state information (may include <QuestStage>, <QuestProgress>, <QuestStageLocation>, etc.):");
            if (addons != null)
                foreach (var a in addons) sb.AppendLine(a);
            sb.AppendLine();
        }

        public MissionTemplate GetShowdownMissionTemplate(string mission, List<string> addons = null)
        {
            if (AvailableTemplateLib.ShowdownTemplates.Count == 0) return null;
            Random random = RandomProvider.Random;

            if (!string.IsNullOrEmpty(mission))
            {
                var named = AvailableTemplateLib.ShowdownTemplates.FirstOrDefault(x => x.Name == mission);
                if (named == null)
                    Console.WriteLine($"AI_TemplateEngine: No showdown template named ‘{mission}’ found, falling back to random.");
                else
                    return ApplyAddons(named, addons);
            }

            if (!AITools.AIMODE)
            {
                var t = AvailableTemplateLib.ShowdownTemplates[random.Next(AvailableTemplateLib.ShowdownTemplates.Count)];
                return ApplyAddons(t, addons);
            }

            Console.WriteLine("AI_TemplateEngine: Choosing Showdown Mission Template...");
            var sb = new StringBuilder();
            sb.AppendLine("You are a Starfield quest designer choosing the FINAL SHOWDOWN mission for an ongoing bounty hunt.");
            sb.AppendLine("Use the LoreContext, quest state, and mission options to decide which location makes the most dramatic and logical climax.");
            sb.AppendLine();
            sb.AppendLine("Guidelines:");
            sb.AppendLine("- Prefer missions that feel like a culmination of the chase: high stakes, isolation, or a strong factional or thematic payoff.");
            sb.AppendLine("- Use the LoreContext to align with the target’s background, factions, motives, and past actions.");
            sb.AppendLine("- Consider locations and tags that connect to earlier investigation themes (e.g. medical cover-ups, mining fronts, corporate shadows, Neon underbelly).");
            sb.AppendLine("- Favour missions that could plausibly host the final confrontation with the fugitive, not just another clue.");
            sb.AppendLine();
            sb.AppendLine("If a <QuestStage> tag is present:");
            sb.AppendLine("- Treat it as the current stage of the quest (e.g. Discovery, Investigation, DeepInvestigation, ForkInvestigation, FinalShowdown).");
            sb.AppendLine("- For FINAL SHOWDOWN selection, choose a mission that makes sense as the end of the arc, not a mid-quest step.");
            sb.AppendLine();
            sb.AppendLine("If a <QuestProgress> tag is present:");
            sb.AppendLine("- Low values (0–25): avoid picking a mission that feels too climactic.");
            sb.AppendLine("- Mid values (26–75): pick something that could be a serious escalation but still leave room for one more step.");
            sb.AppendLine("- High values (76–100): favour missions that clearly feel like the final confrontation or last major reveal.");
            sb.AppendLine();
            sb.AppendLine("If one or more <QuestStageLocation> tags are present:");
            sb.AppendLine("- Treat them as the investigative trail the player has followed so far.");
            sb.AppendLine("- Prefer missions that feel like a logical culmination of that path, rather than a disconnected detour.");
            sb.AppendLine();
            sb.AppendLine("Tag Reuse Rules:");
            sb.AppendLine("- Below is a list of tags that have already been used earlier in the quest chain.");
            sb.AppendLine("- AVOID choosing any mission whose tags significantly overlap with the previously used tags.");
            sb.AppendLine("- Prefer missions that introduce NEW themes, NEW tags, or NEW mission types.");
            sb.AppendLine("- Reuse of a tag should only occur if absolutely necessary.");
            sb.AppendLine("- If every option includes at least one reused tag, choose the mission with the FEWEST overlaps.");
            sb.AppendLine();
            sb.AppendLine("Previously used tags:");
            foreach (var ut in UsedTags) sb.AppendLine($"- {ut}");
            sb.AppendLine();
            sb.AppendLine("Mission Name Reuse Rules:");
            sb.AppendLine("- Below is a list of mission template names that have already been used earlier in the quest chain.");
            sb.AppendLine("- You MUST avoid selecting any mission whose name is identical to, or strongly resembles, any previously used mission name.");
            sb.AppendLine("- Treat missions as ‘similar’ if they share the same leading keywords, prefix, or pattern.");
            sb.AppendLine("  Examples: two missions starting with ‘Space Derelict -’, ‘City Activator -’, or ‘Branching Node -’.");
            sb.AppendLine("- Do NOT pick missions that appear to be variations of an already used mission family.");
            sb.AppendLine("- Prefer missions whose names introduce a NEW mission type, NEW prefix, or NEW scenario family.");
            sb.AppendLine("- If avoiding all similar names is impossible, prefer the mission with the least similarity to those already used.");
            sb.AppendLine();
            sb.AppendLine("Previously used mission names:");
            foreach (var um in UsedMissions) sb.AppendLine($"- {um}");
            sb.AppendLine();
            sb.AppendLine("The character’s LoreContext has been established earlier in this conversation — use it to guide your selection.");
            sb.AppendLine();
            AppendAddonContext(sb, addons);
            sb.AppendLine("Below is a numbered list of possible SHOWDOWN missions.");
            sb.AppendLine("For each entry, you are given: index, location, description, and mission tags.");
            sb.AppendLine("Return ONLY the index number of the best choice. Do not explain your reasoning.");
            sb.AppendLine();

            var selected = AvailableTemplateLib.ShowdownTemplates
                .OrderBy(_ => random.Next())
                .ToList();
            AppendMissionList(sb, selected);

            var result = AITools.RunPrompt(sb.ToString());
            if (int.TryParse(result, out int index) && index >= 0 && index < selected.Count)
            {
                var chosen = selected[index];
                Console.WriteLine("AI_TemplateEngine: Choose " + chosen.Name);
                UsedMissions.Add(chosen.Name);
                UsedTags.AddRange(chosen.MissionTags);
                return ApplyAddons(chosen, addons);
            }

            // Fallback: random showdown, remove to avoid reuse
            return ApplyAddons(RemovePick(AvailableTemplateLib.ShowdownTemplates), addons);
        }

        public MissionTemplate GetInvestigationMissionTemplate(string mission, List<string> addons = null)
        {
            if (AvailableTemplateLib.InvestigationTemplates.Count == 0) return null;
            Random random = RandomProvider.Random;

            if (!string.IsNullOrEmpty(mission))
            {
                var named = AvailableTemplateLib.InvestigationTemplates.FirstOrDefault(x => x.Name == mission);
                if (named == null)
                    Console.WriteLine($"AI_TemplateEngine: No investigation template named ‘{mission}’ found, falling back to random.");
                else
                    return ApplyAddons(named, addons);
            }

            if (!AITools.AIMODE)
                return ApplyAddons(RemovePick(AvailableTemplateLib.InvestigationTemplates), addons);

            Console.WriteLine("AI_TemplateEngine: Choosing Investigation Mission Template...");
            var sb = new StringBuilder();
            sb.AppendLine("You are a Starfield quest designer choosing the next INVESTIGATION mission in a bounty hunt.");
            sb.AppendLine("This mission should feel like a step in the pursuit: following leads, exposing patterns, or escalating tension.");
            sb.AppendLine();
            sb.AppendLine("Guidelines:");
            sb.AppendLine("- Use the LoreContext for factions, target profile, motives, rumors, and location themes.");
            sb.AppendLine("- Prefer missions whose location and description make sense as the NEXT investigative step, not a final confrontation.");
            sb.AppendLine("- Look for tags that match the emerging story: e.g. ‘follow_clue’, ‘space’, ‘city’, ‘mining’, ‘medical’, ‘corporate’, etc.");
            sb.AppendLine("- Favour variety of environment while still feeling connected to the existing lore themes.");
            sb.AppendLine("- Do NOT pick something that feels like an unrelated one-off if others fit better.");
            sb.AppendLine();
            sb.AppendLine("Mission Tags and Variety:");
            sb.AppendLine("- Each mission below has a set of mission tags (e.g. ‘space’, ‘mine’, ‘city’, ‘crimson_fleet’, ‘research’, ‘wilderness’).");
            sb.AppendLine("- Treat these tags as shorthand for environment, tone, faction involvement, and mission style.");
            sb.AppendLine("- Where possible, choose a mission whose tags provide a distinct flavour or escalation compared to earlier quest stages, rather than repeating exactly the same tag combination.");
            sb.AppendLine("- However, do not pick something random: the tags should still feel like a natural climax to the journey implied by the quest state and LoreContext.");
            sb.AppendLine();
            sb.AppendLine("If a <QuestStage> tag is present:");
            sb.AppendLine("- Treat it as the current stage of the quest (e.g. InitialInvestigation, ForkInvestigation, DeepInvestigation).");
            sb.AppendLine("- Choose a mission whose tone and stakes match that stage (early = tentative, mid = branching/conflicting, deep = converging patterns).");
            sb.AppendLine();
            sb.AppendLine("If a <QuestProgress> tag is present:");
            sb.AppendLine("- Low values (0–25): select simpler, lower-stakes missions that feel like early investigation steps.");
            sb.AppendLine("- Mid values (26–75): select missions that introduce stronger patterns, faction involvement, or risk.");
            sb.AppendLine("- High values (76–100): select missions that feel like late-stage investigation, closing in on the truth.");
            sb.AppendLine();
            sb.AppendLine("If one or more <QuestStageLocation> tags are present:");
            sb.AppendLine("- Treat them as the investigative trail the player has followed so far.");
            sb.AppendLine("- Prefer missions that build on or complicate that trail, rather than ignoring it.");
            sb.AppendLine();
            sb.AppendLine("The character’s LoreContext has been established earlier in this conversation — use it to guide your selection.");
            sb.AppendLine();
            AppendAddonContext(sb, addons);
            sb.AppendLine("Below is a numbered list of possible INVESTIGATION missions.");
            sb.AppendLine("For each entry, you are given: index, location, description, and mission tags.");
            sb.AppendLine("Return ONLY the index number of the best choice. Do not output anything else.");
            sb.AppendLine();

            var selected = AvailableTemplateLib.InvestigationTemplates
                .OrderBy(_ => random.Next())
                .Take(12)
                .ToList();
            AppendMissionList(sb, selected);

            var result = AITools.RunPrompt(sb.ToString());
            if (int.TryParse(result, out int index) && index >= 0 && index < selected.Count)
            {
                var chosen = selected[index];
                Console.WriteLine("AI_TemplateEngine: Choose " + chosen.Name);
                // Clear out same-prefix missions to avoid repetition
                string prefix = chosen.Name.Split("-")[0];
                AvailableTemplateLib.InvestigationTemplates.RemoveAll(t => t.Name.Contains(prefix));
                return ApplyAddons(chosen, addons);
            }

            // Fallback: random investigation, remove to avoid reuse
            return ApplyAddons(RemovePick(AvailableTemplateLib.InvestigationTemplates), addons);
        }

        public MissionTemplate GetDiscoveryMissionTemplate(string mission, List<string> addons = null)
        {
            if (AvailableTemplateLib.DiscoveryTemplates.Count == 0) return null;
            Random random = RandomProvider.Random;

            if (!AITools.AIMODE)
                return ApplyAddons(RemovePick(AvailableTemplateLib.DiscoveryTemplates), addons);

            var sb = new StringBuilder();
            sb.AppendLine("You are a Starfield quest designer choosing the opening DISCOVERY mission for a bounty-style quest chain.");
            sb.AppendLine("This mission should feel like the hook: first strange sign, first encounter, or first hint that something is wrong.");
            sb.AppendLine();
            sb.AppendLine("Guidelines:");
            sb.AppendLine("- Use the LoreContext for the target’s background, the factions involved, and the kind of trouble brewing in the Settled Systems.");
            sb.AppendLine("- Prefer missions whose description and location can plausibly serve as the first contact with the mystery (a clue, rumor, odd incident, or job that goes sideways).");
            sb.AppendLine("- Look for tags that make sense as an opener: exploration, first clue, small job, routine contract that reveals something unexpected.");
            sb.AppendLine("- Avoid missions that already feel like a final showdown or heavily escalated conflict.");
            sb.AppendLine();
            sb.AppendLine("You must NOT pick a mission with a name, description, or location identical to any earlier selected stage.");
            sb.AppendLine("If a mission repeats a previous location or template, treat it as INVALID and do not choose it.");
            sb.AppendLine();
            sb.AppendLine("If a <QuestStage> tag is present:");
            sb.AppendLine("- Treat it as the current stage of the quest (for Discovery, it will usually indicate an early or starting phase).");
            sb.AppendLine("- Select a mission that feels appropriate as a beginning or first disturbance rather than a mid- or late-game beat.");
            sb.AppendLine();
            sb.AppendLine("If a <QuestProgress> tag is present:");
            sb.AppendLine("- Low values (0–25): ideal for discovery – pick something small, curious, or slightly off.");
            sb.AppendLine("- Mid values (26–75): pick something that could still serve as an entry point but hints at wider stakes.");
            sb.AppendLine("- High values (76–100): avoid unless the narrative intentionally returns to a simple-looking job that hides something big.");
            sb.AppendLine();
            sb.AppendLine("If one or more <QuestStageLocation> tags are present:");
            sb.AppendLine("- Treat them as context for where the player has been, but this mission should still feel like an entry into the main thread of the bounty.");
            sb.AppendLine();
            sb.AppendLine("The character’s LoreContext has been established earlier in this conversation — use it to guide your selection.");
            sb.AppendLine();
            AppendAddonContext(sb, addons);
            sb.AppendLine("Below is a numbered list of possible DISCOVERY missions.");
            sb.AppendLine("For each entry, you are given: index, location, description, and mission tags.");
            sb.AppendLine("Return ONLY the index number of the best choice. Do not explain your choice.");
            sb.AppendLine();

            var selected = AvailableTemplateLib.DiscoveryTemplates
                .OrderBy(_ => random.Next())
                .Take(5 + random.Next(10))
                .ToList();
            AppendMissionList(sb, selected);

            var result = AITools.RunPrompt(sb.ToString());
            if (int.TryParse(result, out int index) && index >= 0 && index < selected.Count)
                return ApplyAddons(selected[index], addons);

            // Fallback: random discovery, remove to avoid reuse
            return ApplyAddons(RemovePick(AvailableTemplateLib.DiscoveryTemplates), addons);
        }
    }
}
