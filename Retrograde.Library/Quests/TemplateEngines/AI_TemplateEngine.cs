using Retrograde.Chains.Interfaces;
using Retrograde.Chains;
using Retrograde.Nouns;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog.StructuredStrings.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Quests.TemplateEngines
{
    //This is the Choice Engine
    public class AI_TemplateEngine : ITemplateEngine
    {
        public TemplateLib AvailableTemplateLib = new TemplateLib();

        TemplateLib ITemplateEngine.AvailableTemplateLib { get => AvailableTemplateLib; set => AvailableTemplateLib = value; }

        public List<string> UsedTags = new List<string>();
        public List<string> UsedMissions = new List<string>();


        // Helper to apply addons to a selected template
        private MissionTemplate ApplyAddons(MissionTemplate template, List<string> addons)
        {
            if (template == null) return null;

            if (addons != null)
                template.Addons = new List<string>(addons); // clone to avoid shared references

            return template;
        }

        public MissionTemplate GetShowdownMissionTemplate(string mission, List<string> addons = null)
        {
            Random random = RandomProvider.Random;

            if (!string.IsNullOrEmpty(mission))
            {
                var template = AvailableTemplateLib.ShowdownTemplates.Single(x => x.Name == mission);
                return ApplyAddons(template, addons);
            }

            if (AIRunner.AIMode)
            {
                Console.WriteLine("AI_TemplateEngine: Choosing Showdown Mission Template...");
                var sb = new StringBuilder();
                sb.AppendLine("You are a Starfield quest designer choosing the FINAL SHOWDOWN mission for an ongoing bounty hunt.");
                sb.AppendLine("Use the LoreContext, quest state, and mission options to decide which location makes the most dramatic and logical climax.");
                sb.AppendLine();
                sb.AppendLine("Guidelines:");
                sb.AppendLine("- Prefer missions that feel like a culmination of the chase: high stakes, isolation, or a strong factional or thematic payoff.");
                sb.AppendLine("- Use the LoreContext to align with the target's background, factions, motives, and past actions.");
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
                foreach (var ut in UsedTags)
                    sb.AppendLine($"- {ut}");
                sb.AppendLine();

                sb.AppendLine("Mission Name Reuse Rules:");
                sb.AppendLine("- Below is a list of mission template names that have already been used earlier in the quest chain.");
                sb.AppendLine("- You MUST avoid selecting any mission whose name is identical to, or strongly resembles, any previously used mission name.");
                sb.AppendLine("- Treat missions as 'similar' if they share the same leading keywords, prefix, or pattern.");
                sb.AppendLine("  Examples: two missions starting with 'Space Derelict -', 'City Activator -', or 'Branching Node -'.");
                sb.AppendLine("- Do NOT pick missions that appear to be variations of an already used mission family.");
                sb.AppendLine("- Prefer missions whose names introduce a NEW mission type, NEW prefix, or NEW scenario family.");
                sb.AppendLine("- If avoiding all similar names is impossible, prefer the mission with the least similarity to those already used.");
                sb.AppendLine();
                sb.AppendLine("Previously used mission names:");
                foreach (var um in UsedMissions)
                    sb.AppendLine($"- {um}");
                sb.AppendLine();

                sb.AppendLine("LoreContext:");
                sb.AppendLine("<LoreContext>");
                sb.AppendLine(AIRunner.LoreContext ?? string.Empty);
                sb.AppendLine("</LoreContext>");
                sb.AppendLine();

                sb.AppendLine("Additional quest state information (may include <QuestStage>, <QuestProgress>, <QuestStageLocation>, etc.):");
                if (addons != null)
                {
                    foreach (var a in addons)
                        sb.AppendLine(a);
                }
                sb.AppendLine();

                sb.AppendLine("Below is a numbered list of possible SHOWDOWN missions.");
                sb.AppendLine("For each entry, you are given: index, location, description, and mission tags.");
                sb.AppendLine("Return ONLY the index number of the best choice. Do not explain your reasoning.");
                sb.AppendLine();

                //int count = 10 + random.Next(10);
                int count = AvailableTemplateLib.ShowdownTemplates.Count;
                List<MissionTemplate> selected = AvailableTemplateLib.ShowdownTemplates
                    .OrderBy(x => random.Next())
                    .Take(count)
                    .ToList();

                for (int i = 0; i < selected.Count; i++)
                {
                    var tags = selected[i].MissionTags != null && selected[i].MissionTags.Any()
                        ? string.Join(", ", selected[i].MissionTags)
                        : "none";

                    sb.AppendLine($"{i} :");
                    sb.AppendLine($"  Location: {selected[i].Location}");
                    sb.AppendLine($"  Description: {selected[i].Description}");
                    sb.AppendLine($"  Tags: {tags}");
                    sb.AppendLine();
                }

                var result = AIRunner.RunPrompt(sb.ToString());
                int index;
                if (int.TryParse(result, out index) && index >= 0 && index < selected.Count)
                {
                    var ChoosenTemplate = selected[index];
                    UsedMissions.Add(ChoosenTemplate.Name);
                    Console.WriteLine("AI_TemplateEngine: Choose " + ChoosenTemplate.Name);

                    foreach (var tag in ChoosenTemplate.MissionTags)
                    {
                        UsedTags.Add(tag);
                    }
                    return ApplyAddons(ChoosenTemplate, addons);
                }

                // Fallback: random showdown, remove to avoid reuse
                int randselected = random.Next(AvailableTemplateLib.ShowdownTemplates.Count);
                var fallback = AvailableTemplateLib.ShowdownTemplates[randselected];
                AvailableTemplateLib.ShowdownTemplates.RemoveAt(randselected);
                return ApplyAddons(fallback, addons);
            }
            else
            {
                if (AvailableTemplateLib.ShowdownTemplates.Count == 0) return null;

                if (!string.IsNullOrEmpty(mission))
                {
                    var template = AvailableTemplateLib.ShowdownTemplates.Single(x => x.Name == mission);
                    return ApplyAddons(template, addons);
                }

                var randomTemplate = AvailableTemplateLib.ShowdownTemplates[random.Next(AvailableTemplateLib.ShowdownTemplates.Count)];
                return ApplyAddons(randomTemplate, addons);
            }
        }

        public MissionTemplate GetInvestigationMissionTemplate(string mission, List<string> addons = null)
        {
            if (AvailableTemplateLib.InvestigationTemplates.Count == 0) return null;
            Random random = RandomProvider.Random;

            if (!string.IsNullOrEmpty(mission))
            {
                var template = AvailableTemplateLib.InvestigationTemplates.Single(x => x.Name == mission);
                return ApplyAddons(template, addons);
            }

            

            if (AIRunner.AIMode)
            {
                Console.WriteLine("AI_TemplateEngine: Choosing Investigation Mission Template...");
                var sb = new StringBuilder();
                sb.AppendLine("You are a Starfield quest designer choosing the next INVESTIGATION mission in a bounty hunt.");
                sb.AppendLine("This mission should feel like a step in the pursuit: following leads, exposing patterns, or escalating tension.");
                sb.AppendLine();
                sb.AppendLine("Guidelines:");
                sb.AppendLine("- Use the LoreContext for factions, target profile, motives, rumors, and location themes.");
                sb.AppendLine("- Prefer missions whose location and description make sense as the NEXT investigative step, not a final confrontation.");
                sb.AppendLine("- Look for tags that match the emerging story: e.g. 'follow_clue', 'space', 'city', 'mining', 'medical', 'corporate', etc.");
                sb.AppendLine("- Favour variety of environment while still feeling connected to the existing lore themes.");
                sb.AppendLine("- Do NOT pick something that feels like an unrelated one-off if others fit better.");
                sb.AppendLine();
                sb.AppendLine("Mission Tags and Variety:");
                sb.AppendLine("- Each mission below has a set of mission tags (e.g. 'space', 'mine', 'city', 'crimson_fleet', 'research', 'wilderness').");
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

                sb.AppendLine("LoreContext:");
                sb.AppendLine("<LoreContext>");
                sb.AppendLine(AIRunner.LoreContext ?? string.Empty);
                sb.AppendLine("</LoreContext>");
                sb.AppendLine();

                sb.AppendLine("Additional quest state information (may include <QuestStage>, <QuestProgress>, <QuestStageLocation>, etc.):");
                if (addons != null)
                {
                    foreach (var a in addons)
                        sb.AppendLine(a);
                }
                sb.AppendLine();

                sb.AppendLine("Below is a numbered list of possible INVESTIGATION missions.");
                sb.AppendLine("For each entry, you are given: index, location, description, and mission tags.");
                sb.AppendLine("Return ONLY the index number of the best choice. Do not output anything else.");
                sb.AppendLine();

                
                int count = 10 + random.Next(25);
                
                List<MissionTemplate> selected = AvailableTemplateLib.InvestigationTemplates
                    .OrderBy(x => random.Next())
                    .Take(count)
                    .ToList();

                for (int i = 0; i < selected.Count; i++)
                {
                    var tags = selected[i].MissionTags != null && selected[i].MissionTags.Any()
                        ? string.Join(", ", selected[i].MissionTags)
                        : "none";

                    sb.AppendLine($"{i} :");
                    sb.AppendLine($"  Location: {selected[i].Location}");
                    sb.AppendLine($"  Description: {selected[i].Description}");
                    sb.AppendLine($"  Tags: {tags}");
                    sb.AppendLine();
                }

                var result = AIRunner.RunPrompt(sb.ToString());
                int index;
                if (int.TryParse(result, out index) && index >= 0 && index < selected.Count)
                {
                    var ChoosenTemplate = selected[index];
                    Console.WriteLine("AI_TemplateEngine: Choose " + ChoosenTemplate.Name);
                    //Clear out the same tagged missions.
                    string type = ChoosenTemplate.Name.Split("-")[0];

                    for (int i = 0; i< AvailableTemplateLib.InvestigationTemplates.Count; i++)
                    {
                        if (AvailableTemplateLib.InvestigationTemplates[i].Name.Contains(type))
                        {
                            AvailableTemplateLib.InvestigationTemplates.RemoveAt(i);
                            i--;
                        }
                    }


                    return ApplyAddons(ChoosenTemplate, addons);
                }
               
                // Fallback: random investigation, remove to avoid reuse
                int randselected = random.Next(AvailableTemplateLib.InvestigationTemplates.Count);
                var fallback = AvailableTemplateLib.InvestigationTemplates[randselected];
                AvailableTemplateLib.InvestigationTemplates.RemoveAt(randselected);
                return ApplyAddons(fallback, addons);
            }

            // Non-AI mode
            if (!string.IsNullOrEmpty(mission))
            {
                var template = AvailableTemplateLib.InvestigationTemplates.Single(x => x.Name == mission);
                return ApplyAddons(template, addons);
            }
            else
            {
                int selectedIndex = random.Next(AvailableTemplateLib.InvestigationTemplates.Count);
                var template = AvailableTemplateLib.InvestigationTemplates[selectedIndex];
                AvailableTemplateLib.InvestigationTemplates.RemoveAt(selectedIndex);
                return ApplyAddons(template, addons);
            }
        }

        public MissionTemplate GetDiscoveryMissionTemplate(string missionname, List<string> addons = null)
        {
            if (AvailableTemplateLib.DiscoveryTemplates.Count == 0) return null;
            Random random = RandomProvider.Random;

            if (AIRunner.AIMode)
            {
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

                sb.AppendLine("LoreContext:");
                sb.AppendLine("<LoreContext>");
                sb.AppendLine(AIRunner.LoreContext ?? string.Empty);
                sb.AppendLine("</LoreContext>");
                sb.AppendLine();

                sb.AppendLine("Additional quest state information (may include <QuestStage>, <QuestProgress>, <QuestStageLocation>, etc.):");
                if (addons != null)
                {
                    foreach (var a in addons)
                        sb.AppendLine(a);
                }
                sb.AppendLine();

                sb.AppendLine("Below is a numbered list of possible DISCOVERY missions.");
                sb.AppendLine("For each entry, you are given: index, location, description, and mission tags.");
                sb.AppendLine("Return ONLY the index number of the best choice. Do not explain your choice.");
                sb.AppendLine();

                int count = 5 + random.Next(10);
                List<MissionTemplate> selected = AvailableTemplateLib.DiscoveryTemplates
                    .OrderBy(x => random.Next())
                    .Take(count)
                    .ToList();

                for (int i = 0; i < selected.Count; i++)
                {
                    var tags = selected[i].MissionTags != null && selected[i].MissionTags.Any()
                        ? string.Join(", ", selected[i].MissionTags)
                        : "none";

                    sb.AppendLine($"{i} :");
                    sb.AppendLine($"  Location: {selected[i].Location}");
                    sb.AppendLine($"  Description: {selected[i].Description}");
                    sb.AppendLine($"  Tags: {tags}");
                    sb.AppendLine();
                }

                var result = AIRunner.RunPrompt(sb.ToString());
                int index;
                if (int.TryParse(result, out index) && index >= 0 && index < selected.Count)
                {
                    return ApplyAddons(selected[index], addons);
                }

                // Fallback: random discovery, remove to avoid reuse
                int randselected = random.Next(AvailableTemplateLib.DiscoveryTemplates.Count);
                var fallback = AvailableTemplateLib.DiscoveryTemplates[randselected];
                AvailableTemplateLib.DiscoveryTemplates.RemoveAt(randselected);
                return ApplyAddons(fallback, addons);
            }
            else
            {
                int selectedIndex = random.Next(AvailableTemplateLib.DiscoveryTemplates.Count);
                var template = AvailableTemplateLib.DiscoveryTemplates[selectedIndex];
                AvailableTemplateLib.DiscoveryTemplates.RemoveAt(selectedIndex);
                return ApplyAddons(template, addons);
            }
        }

    }
}
