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

        public string ShowdownTemplate = "";
        public string InvestigationTemplate = "";
        public string DiscoveryTemplate = "";

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

        // Generates 1-2 sentences of intel that points the player toward the first investigation.
        // No reference to how or where the player obtained it — the discovery mechanic is meta, not lore.
        private void GenerateDiscoveryBridge(MissionTemplate discoveryTemplate, MissionTemplate toTemplate)
        {
            var bridge = AITools.RunPrompt(
                $"In 1-2 sentences, describe a piece of intel that points toward " +
                $"the \"{toTemplate.Name}\" stage at {toTemplate.Location}.\n" +
                "Do not mention where or how this intel was obtained. " +
                "Be concrete — name a data file reference, a contact by role only, a location fragment, or a coded instruction. " +
                "Ground it in the established lore. Output only the 1-2 sentences, no headers or labels."
            );

            if (discoveryTemplate.Addons == null)
                discoveryTemplate.Addons = new List<string>();
            discoveryTemplate.Addons.Add($"<StageBridge>{bridge}</StageBridge>");
        }

        // Generates 1-2 sentences of prose describing what the player finds at fromTemplate
        // that sends them toward toTemplate. Adds to history AND to fromTemplate.Addons.
        private void GenerateStageBridge(MissionTemplate fromTemplate, MissionTemplate toTemplate)
        {
            var bridge = AITools.RunPrompt(
                $"In 1-2 sentences, describe the specific clue or contact the player uncovers at " +
                $"{fromTemplate.Location} (the \"{fromTemplate.Name}\" stage) that points them toward " +
                $"the \"{toTemplate.Name}\" stage at {toTemplate.Location}.\n" +
                $"The source contact or clue is located at {fromTemplate.Location} — use this exactly; do not substitute any other location from the lore context. " +
                "Be concrete — name a data file, a physical trail, or an overheard conversation. " +
                "If a contact provides the lead, describe them by role only (e.g. 'a dock worker', 'a UC officer') — do not invent a name for them. " +
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

            var pinnedInvestigations = InvestigationTemplate != ""
                ? new List<string>() { InvestigationTemplate }.Where(s => s != "").ToList()
                : null;

            LorePrompts.GenerateLoreContext(outlawNpc, Lorefile, templateManager.AvailableLib,
                pinnedDiscovery:      DiscoveryTemplate     != "" ? DiscoveryTemplate     : null,
                pinnedShowdown:       ShowdownTemplate      != "" ? ShowdownTemplate      : null,
                pinnedInvestigations: pinnedInvestigations);


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
            AITools.InjectContextIntoHistory(
                $"The outlaw target's name is '{outlawNpc.name}'. " +
                "Use this name in log entries, clues, and any in-world references to the target."
            );

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
            var showdownQuest = ShowdownMissionTemplate.outlawQuest.Setup(myMod, outlawNpc, ShowdownMissionTemplate, null);
            if (!string.IsNullOrEmpty(ShowdownMissionTemplate.outlawQuest.LogMessage))
                AITools.InjectContextIntoHistory($"[Stage '{ShowdownMissionTemplate.Name}' log entry]: {ShowdownMissionTemplate.outlawQuest.LogMessage}");

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
                if (i > 0)
                {
                    // Bridge: this stage leads the player to the previous (closer-to-showdown) stage
                    GenerateStageBridge(template, investigationStages[i - 1].Template);
                }
                else
                {
                    // Bridge: closest investigation leads the player toward the showdown
                    GenerateStageBridge(template, ShowdownMissionTemplate);
                }
                Quest formmission = template.outlawQuest.Setup(myMod, outlawNpc, template, lastOutlawQuest);
                if (!string.IsNullOrEmpty(template.outlawQuest.LogMessage))
                    AITools.InjectContextIntoHistory($"[Stage '{template.Name}' log entry]: {template.outlawQuest.LogMessage}");
                lastOutlawQuest = template.outlawQuest;
            }

            // Finally build the discovery step, linked to the earliest investigation
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Discovery: " + DiscoveryMissionTemplate.Name);
            // Bridge: Discovery leads the player into the earliest investigation stage
            if (investigationStages.Count > 0)
                GenerateDiscoveryBridge(DiscoveryMissionTemplate, investigationStages[^1].Template);

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
