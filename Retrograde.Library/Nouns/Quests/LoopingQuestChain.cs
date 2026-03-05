using Mutagen.Bethesda.Starfield;
using Retrograde.AI;
using Retrograde.AI.Utils;
using Retrograde.Chains.Interfaces;
using Retrograde.Nouns;
using Retrograde.Quests;
using Retrograde.Quests.TemplateEngines;
using Retrograde.Utils;
using System;
using System.Collections.Generic;

namespace Retrograde.Chains
{
    public class LoopingLayoutQuestChain : IQuestchain
    {
        public StarfieldMod myMod;

        public LoopingLayoutQuestChain(StarfieldMod myModparam)
        {
            myMod = myModparam;
        }

        private void AddStageLocation(MissionTemplate template, string stage, string location)
        {
            if (template.Addons == null)
                template.Addons = new List<string>();

            template.Addons.Add(
                $"<QuestStageLocation stage=\"{stage}\">{location}</QuestStageLocation>"
            );
        }

        public bool GenerateQuest()
        {
            // Story Setup --------------------------------
            Random random = RandomProvider.Random;
            Console.WriteLine("LoopingLayoutQuestChain");

            List<ITemplateManager> templates = new List<ITemplateManager>()
            {
                new AllTemplateManager(new AI_TemplateEngine()),
                //new FrontierTemplateManager(new AI_TemplateEngine()),
                //new NoPOITemplateManager(new AI_TemplateEngine()),
                //new CombatTemplateManager(new AI_TemplateEngine()),
                new AllTemplateManager(new RandomTemplateEngine())
            };
            var templateManager = templates[random.Next(templates.Count)];

            Console.WriteLine(templateManager.GetType());

            //            var Lorefile = File.ReadAllText(".\\questgen_quests\\Lorefiles\\LostMarine.md");
            //var Lorefile = LorePrompts.LoadRandomLoreFile();
            // NPC Target (base setup) --------------------------------
            OutlawNpc outlawNpc = new OutlawNpc(myMod, true);

            Console.WriteLine("Generating Lore File...");
            var Lorefile = LorePrompts.GenerateLoreFile(outlawNpc.Goal, outlawNpc.Flaw, outlawNpc.Occupation, outlawNpc.Crime);

            // Build LoreContext from Lorefile and NPC
            Console.WriteLine("Building Lore Context...");

            LorePrompts.GenerateLoreContext(outlawNpc, Lorefile, templateManager.AvailableLib);


            // Template Choices --------------------------------

            // Showdown (final encounter, high completion)
            var showdownAddons = new List<string>()
            {
                "<QuestStage>Showdown</QuestStage>",
                "<QuestProgress>90%</QuestProgress>"
            };

            var ShowdownMissionTemplate = templateManager.GetShowdownMissionTemplate(LorePrompts.PlannedShowdown, showdownAddons);

            outlawNpc.spacesuit = ShowdownMissionTemplate.parameters.ContainsKey("NeedSpacesuit") && (bool)ShowdownMissionTemplate.parameters["NeedSpacesuit"];
            outlawNpc.GenerateNPC();

            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Outlaw Name: " + outlawNpc.name);

            // Decide how many investigation missions we want between discovery and showdown
            int count = 2 + random.Next(4); // 2–5 investigations

            // We will collect investigation templates first, then run them from the end backwards
            var investigationStages = new List<(string Stage, MissionTemplate Template)>();

            // Generate investigation templates (from near-showdown backwards)
            for (int i = 0; i < count; i++)
            {
                Console.WriteLine("---------------------------------------------------------------------------------");

                // Map position in chain to an investigation stage and quest completion
                // i = 0  -> closest to showdown (deep)
                // i > 0  -> progressively earlier (initial-style)
                string stageName = (i == 0) ? "DeepInvestigation" : "InitialInvestigation";

                double t = (count == 1) ? 0.0 : (double)i / (count - 1);
                int progressValue = 70 - (int)((70 - 20) * t); // roughly 70% down to ~20%
                if (progressValue < 10) progressValue = 10;

                var investigationAddons = new List<string>()
                {
                    $"<QuestStage>{stageName}</QuestStage>",
                    $"<QuestProgress>{progressValue}%</QuestProgress>"
                };

                // Base investigation template — use planned name only for the deepest stage (i==0)
                var template = templateManager.GetInvestigationMissionTemplate(
                    i == 0 ? LorePrompts.PlannedInvestigation : "",
                    investigationAddons);

                // Optional fork substitution (kept from original logic)
                // Don't run on the first and last.
                if (i != 0 && i != count - 1)
                {
                    // Run on odd indices; avoids fork at very start or multiple in a row
                    if (i % 2 != 0 && random.Next(100) > 0)
                    {
                        var forktemplates = new Templates_Fork();
                        var forkTemplate = forktemplates.InvestigationTemplates[random.Next(forktemplates.InvestigationTemplates.Count)];

                        forkTemplate.Addons = new List<string>()
                        {
                            "<QuestStage>Investigation</QuestStage>",
                            $"<QuestProgress>{progressValue}%</QuestProgress>"
                        };

                        template = forkTemplate;
                        stageName = "Investigation";
                    }
                }

                Console.WriteLine("Investigation Template: " + template.Name);
                investigationStages.Add((stageName, template));
            }

            // Finally build the discovery step (earliest, lowest completion)
            Console.WriteLine("---------------------------------------------------------------------------------");

            var discoveryAddons = new List<string>()
            {
                "<QuestStage>Discovery</QuestStage>",
                "<QuestProgress>0%</QuestProgress>"
            };


            var DiscoveryTemplateManager = new AllTemplateManager(new AI_TemplateEngine());
            //var DiscoveryMissionTemplate = templateManager.GetDiscoveryMissionTemplate("", discoveryAddons); // We currently don't handle the fact that not all template libs have discovery missions.
            var DiscoveryMissionTemplate = DiscoveryTemplateManager.GetDiscoveryMissionTemplate(LorePrompts.PlannedDiscovery, discoveryAddons);

            // Now we build a story-ordered list for stage location history:
            // Discovery -> earliest investigation -> ... -> closest investigation -> Showdown
            var storyStages = new List<(string Stage, MissionTemplate Template)>();

            storyStages.Add(("Discovery", DiscoveryMissionTemplate));

            // investigations were added from near-showdown backwards;
            // reverse to put earliest first in story order
            for (int i = investigationStages.Count - 1; i >= 0; i--)
            {
                storyStages.Add(investigationStages[i]);
            }

            storyStages.Add(("Showdown", ShowdownMissionTemplate));

            // Add QuestStageLocation history to every stage based on where the player has already been
            for (int i = 0; i < storyStages.Count; i++)
            {
                var current = storyStages[i];

                // All previous stages in story order are "history" for this stage
                for (int j = 0; j < i; j++)
                {
                    var previous = storyStages[j];
                    AddStageLocation(current.Template, previous.Stage, previous.Template.Location);
                }
            }

            // At this point:
            // - Showdown, all investigations, and Discovery have <QuestStage> / <QuestProgress> in Addons
            // - Each has <QuestStageLocation> history for earlier story steps

            // Now we actually generate the quests from the end backwards

            // Prime the AI with a summary of all stages in story order before generating
            Console.WriteLine("Feeding the stages into the AI...");
            var stageSummary = new System.Text.StringBuilder();
            stageSummary.AppendLine("The following is a summary of all missions in this quest chain, in story order from earliest to final encounter.");
            stageSummary.AppendLine("Study each stage's location and type carefully.");
            stageSummary.AppendLine("Use this overview to:");
            stageSummary.AppendLine("- Plan how information about the outlaw is revealed gradually across stages.");
            stageSummary.AppendLine("- Ensure each stage's narrative connects logically to the next.");
            stageSummary.AppendLine("- Avoid introducing contradictions between locations or plot points.");
            stageSummary.AppendLine();
            foreach (var (stageName, template) in storyStages)
            {
                stageSummary.AppendLine($"<{stageName} Summary> {template.Description} Location: {template.Location}");
            }
            stageSummary.AppendLine();
            stageSummary.AppendLine("Summary complete. The stages will be generated next, starting from the final encounter and working backwards.");
            stageSummary.AppendLine("Each stage should feel like a natural step in the player's journey toward that final confrontation.");
            stageSummary.AppendLine("Do not generate any content yet — this is context only.");
            AITools.RunPrompt(stageSummary.ToString());

            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Showdown: " + ShowdownMissionTemplate.Name);
            AITools.RunPrompt(
                "Briefly set the scene for the Showdown — the player's final confrontation with " + outlawNpc.name + ".\r\n" +
                "Use the location and mission type from the summary above. Ground it in the lore.\r\n" +
                "Write 2-3 sentences of plain prose only. No bullet points, no section headings, no alternate outcomes."
            );
            var showdownQuest = ShowdownMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ShowdownMissionTemplate, null);

            AITools.RunPrompt(
                "Important: from this point on the player does not know where the final showdown takes place. " +
                "Do not state the showdown location explicitly in any investigation or discovery mission. You may plant indirect clues that point toward it."
            );

            var lastOutlawQuest = ShowdownMissionTemplate.outlawQuest;

            // Generate investigation quests in the order they were originally chosen:
            // closest to showdown first, then progressively earlier
            for (int i = 0; i < investigationStages.Count; i++)
            {
                Console.WriteLine("---------------------------------------------------------------------------------");
                var (stageName, template) = investigationStages[i];

                Console.WriteLine(stageName + ": " + template.Name);
                if (i == 0)
                {
                    AITools.RunPrompt(
                        "Briefly set the scene for the Deep Investigation — the closest lead before the showdown.\r\n" +
                        "The player is closing in on " + outlawNpc.name + " but does not yet know the final location.\r\n" +
                        "Write 2-3 sentences of plain prose only. No bullet points, no section headings, no alternate outcomes."
                    );
                }
                else
                {
                    AITools.RunPrompt(
                        "Briefly set the scene for the " + stageName + " — a step in the trail, building toward the showdown.\r\n" +
                        "Write 2-3 sentences of plain prose only. No bullet points, no section headings, no alternate outcomes."
                    );
                }
                Quest formmission = template.outlawQuest.Setup(myMod, outlawNpc, template, lastOutlawQuest);
                lastOutlawQuest = template.outlawQuest;
            }

            // Finally build the discovery step, linked to the earliest investigation
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Discovery: " + DiscoveryMissionTemplate.Name);
            AITools.RunPrompt(
                "Briefly set the scene for the Discovery — the moment " + outlawNpc.name + "'s existence first surfaces.\r\n" +
                "Write 2-3 sentences of plain prose only. No bullet points, no section headings, no alternate outcomes."
            );

            var DiscoveryMission = DiscoveryMissionTemplate.outlawQuest.Setup(
                myMod,
                outlawNpc,
                DiscoveryMissionTemplate,
                lastOutlawQuest
            );

            //We have now generated all the stages. Do any final linking steps
            outlawNpc.GenerateLegendaryItem();
            Console.WriteLine("Generating Final Bounty Log...");
            outlawNpc.GenerateLog();
            SpeechTools.AddVoice(outlawNpc.Logfile.ID, outlawNpc.instance.FormKey.ID, outlawNpc.LogText, outlawNpc.VoiceEditorId, outlawNpc.ElevenLabsVoiceId);
            SpeechTools.ConvertAndDeploy();
            return true;
        }

    }
}
