using Mutagen.Bethesda.Starfield;
using Retrograde.AI;
using Retrograde.AI.Utils;
using Retrograde.Chains.Interfaces;
using Retrograde.Nouns;
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

        // Generates 1-2 sentences of prose describing what the player finds at fromTemplate
        // that sends them toward toTemplate. Adds to history AND to fromTemplate.Addons.
        private void GenerateStageBridge(MissionTemplate fromTemplate, MissionTemplate toTemplate)
        {
            var bridge = AITools.RunPrompt(
                $"In 1-2 sentences, describe the specific clue or contact the player uncovers at " +
                $"{fromTemplate.Location} (the \"{fromTemplate.Name}\" stage) that points them toward " +
                $"the \"{toTemplate.Name}\" stage at {toTemplate.Location}.\n" +
                "Be concrete — name a data file, an informant, a physical trail, or an overheard conversation. " +
                "Ground it in the established lore. Output only the 1-2 sentences, no headers or labels."
            );

            if (fromTemplate.Addons == null)
                fromTemplate.Addons = new List<string>();
            fromTemplate.Addons.Add($"<StageBridge>{bridge}</StageBridge>");
        }

        public bool GenerateQuest()
        {
            // Story Setup --------------------------------
            Console.WriteLine("LoopingLayoutQuestChain");
            var templateManager = new AllTemplateManager(new AI_TemplateEngine());
            // NPC Target (base setup) --------------------------------
            OutlawNpc outlawNpc = new OutlawNpc(myMod, true);

            Console.WriteLine("Generating Lore File...");
            var Lorefile = LorePrompts.GenerateLoreFile(outlawNpc.Traits);

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

            // PlannedInvestigations is in story order (earliest first, closest to showdown last).
            // We build investigationStages in reverse (closest to showdown at index 0) to match
            // the existing generation order (showdown → deepest → ... → earliest → discovery).
            var plannedList = LorePrompts.PlannedInvestigations;
            int count = plannedList.Count;

            var investigationStages = new List<(string Stage, MissionTemplate Template)>();

            for (int i = 0; i < count; i++)
            {
                Console.WriteLine("---------------------------------------------------------------------------------");

                // i=0 → closest to showdown (last in story order), i=count-1 → earliest
                string stageName = (i == 0) ? "DeepInvestigation" : "InitialInvestigation";

                double t = (count == 1) ? 0.0 : (double)i / (count - 1);
                int progressValue = 70 - (int)((70 - 20) * t); // roughly 70% down to ~20%
                if (progressValue < 10) progressValue = 10;

                var investigationAddons = new List<string>()
                {
                    $"<QuestStage>{stageName}</QuestStage>",
                    $"<QuestProgress>{progressValue}%</QuestProgress>"
                };

                // plannedList is story order → reverse index to map to generation order
                string plannedName = plannedList[count - 1 - i];

                var template = templateManager.GetInvestigationMissionTemplate(plannedName, investigationAddons);

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


            var DiscoveryMissionTemplate = templateManager.GetDiscoveryMissionTemplate(LorePrompts.PlannedDiscovery, discoveryAddons);

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

            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Showdown: " + ShowdownMissionTemplate.Name);
            AITools.RunPrompt(
                "Briefly set the scene for the Showdown — the player's final confrontation with " + outlawNpc.name + ".\r\n" +
                "Location: " + ShowdownMissionTemplate.Location + ". Mission type: " + ShowdownMissionTemplate.Name + ".\r\n" +
                "Ground it in the lore. Write 2-3 sentences of plain prose only. No bullet points, no section headings, no alternate outcomes."
            );
            var showdownQuest = ShowdownMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ShowdownMissionTemplate, null);

            AITools.InjectContextIntoHistory(
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
                    // Bridge: this stage leads the player to the previous (closer-to-showdown) stage
                    GenerateStageBridge(template, investigationStages[i - 1].Template);
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
            // Bridge: Discovery leads the player into the earliest investigation stage
            if (investigationStages.Count > 0)
                GenerateStageBridge(DiscoveryMissionTemplate, investigationStages[^1].Template);
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
