using Mutagen.Bethesda.Starfield;
using Retrograde.AI;
using Retrograde.AI.Utils;
using Retrograde.Chains.Interfaces;
using Retrograde.Grammar;
using Retrograde.Nouns;
using Retrograde.Quests.TemplateEngines;
using Retrograde.Utils;
using Retrograde.Writing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Chains
{
    /// <summary>
    /// Grammar-driven quest chain orchestrator. Sits alongside LoopingLayoutQuestChain.
    /// Same quest types, same templates, same game output — but enriched with arc shaping,
    /// stage roles, character webs, and entity manifests via the Addons mechanism.
    /// </summary>
    public class LorewalkerQuestChain : IQuestchain
    {
        public StarfieldMod myMod;

        public LorewalkerQuestChain(StarfieldMod myModparam)
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
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("LorewalkerQuestChain");

            // ── Grammar: select strategy and framing ──────────────────────────
            var engine = new GrammarEngine(Motivation.Justice);
            var specs = engine.BuildStageSpecs();

            Console.WriteLine($"Strategy: {engine.Strategy.Name} ({engine.Strategy.Arc})");
            Console.WriteLine($"Framing:  {engine.Framing}");
            Console.WriteLine($"Stages:   {string.Join(" → ", specs.Select(s => s.Role))}");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");

            // ── NPC + Lore (same as LoopingLayoutQuestChain) ──────────────────
            var templateManager = new AllTemplateManager(new RandomTemplateEngine());
            OutlawNpc outlawNpc = new OutlawNpc(myMod, true);

            Console.WriteLine("Generating Lore File...");
            var lorefile = LorePrompts.GenerateLoreFile(outlawNpc.Traits);

            Console.WriteLine("Building Lore Context...");
            // selectArc: false — the grammar handles arc planning, not the AI
            LorePrompts.GenerateLoreContext(outlawNpc, lorefile, templateManager.AvailableLib,
                selectArc: false);

            // ── Grammar: generate character web ───────────────────────────────
            Console.WriteLine("Generating Character Web...");
            engine.Web = CharacterWeb.Generate(outlawNpc, engine.Framing);
            engine.AssignCharactersToStages();

            // ── Template selection ────────────────────────────────────────────
            // Stage pattern from grammar: Discovery, N investigations, Resolution
            // We select templates in the same order as LoopingLayoutQuestChain:
            // showdown first, then investigations (near-showdown to earliest), then discovery.

            int investigationCount = specs.Count(s =>
                s.Role != StageRole.Discovery && s.Role != StageRole.Resolution);

            // Showdown template
            var showdownSpec = specs.Last(s => s.Role == StageRole.Resolution);
            int showdownIndex = specs.IndexOf(showdownSpec);
            var showdownAddons = new List<string>
            {
                "<QuestStage>Showdown</QuestStage>",
                "<QuestProgress>90%</QuestProgress>"
            };
            var showdownTemplate = templateManager.GetShowdownMissionTemplate("", showdownAddons);

            outlawNpc.spacesuit = showdownTemplate.parameters.ContainsKey("NeedSpacesuit")
                && (bool)showdownTemplate.parameters["NeedSpacesuit"];
            outlawNpc.GenerateNPC();
            AITools.InjectContextIntoHistory(
                $"The outlaw target's name is '{outlawNpc.name}'. " +
                "Use this name in log entries, clues, and any in-world references to the target.");

            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Outlaw Name: " + outlawNpc.name);

            // Investigation templates (built in reverse story order, like LoopingLayoutQuestChain)
            var investigationSpecs = specs
                .Select((s, idx) => (Spec: s, Index: idx))
                .Where(x => x.Spec.Role != StageRole.Discovery && x.Spec.Role != StageRole.Resolution)
                .ToList();

            // investigationSpecs is in story order. Reverse for generation order.
            var investigationStages = new List<(StageSpec Spec, int SpecIndex, MissionTemplate Template)>();

            for (int i = investigationSpecs.Count - 1; i >= 0; i--)
            {
                var (spec, specIndex) = investigationSpecs[i];
                Console.WriteLine("---------------------------------------------------------------------------------");

                bool isDeep = (i == investigationSpecs.Count - 1); // closest to showdown
                string stageName = GrammarEngine.StageRoleToQuestStage(spec.Role, isDeep);

                // Progress based on arc tension rather than linear
                int progressValue = (int)(spec.Tension * 80);
                if (progressValue < 10) progressValue = 10;
                if (progressValue > 80) progressValue = 80;

                var baseAddons = new List<string>
                {
                    $"<QuestStage>{stageName}</QuestStage>",
                    $"<QuestProgress>{progressValue}%</QuestProgress>"
                };

                var template = templateManager.GetInvestigationMissionTemplate("", baseAddons);
                Console.WriteLine($"{spec.Role} ({stageName}): {template.Name}");
                investigationStages.Add((spec, specIndex, template));
            }

            // Discovery template
            var discoverySpec = specs.First(s => s.Role == StageRole.Discovery);
            int discoveryIndex = specs.IndexOf(discoverySpec);
            Console.WriteLine("---------------------------------------------------------------------------------");

            var discoveryAddons = new List<string>
            {
                "<QuestStage>Discovery</QuestStage>",
                "<QuestProgress>0%</QuestProgress>"
            };
            var discoveryTemplate = templateManager.GetDiscoveryMissionTemplate("", discoveryAddons);

            // ── Build story-ordered stage list for location history ────────────
            var storyStages = new List<(string Stage, MissionTemplate Template, int SpecIndex)>();
            storyStages.Add(("Discovery", discoveryTemplate, discoveryIndex));

            // investigations were added near-showdown-first; reverse for story order
            for (int i = investigationStages.Count - 1; i >= 0; i--)
            {
                var (spec, specIndex, tmpl) = investigationStages[i];
                storyStages.Add((spec.Role.ToString(), tmpl, specIndex));
            }
            storyStages.Add(("Showdown", showdownTemplate, showdownIndex));

            // Add QuestStageLocation history (same pattern as LoopingLayoutQuestChain)
            for (int i = 0; i < storyStages.Count; i++)
            {
                var current = storyStages[i];
                for (int j = 0; j < i; j++)
                {
                    var previous = storyStages[j];
                    AddStageLocation(current.Template, previous.Stage, previous.Template.Location);
                }
            }

            // ── Enrich Addons with grammar context ────────────────────────────
            // Apply enriched addons AFTER location history has been added
            // (AssembleEnrichedAddons preserves existing addons)
            showdownTemplate.Addons = engine.AssembleEnrichedAddons(showdownIndex, showdownTemplate);
            discoveryTemplate.Addons = engine.AssembleEnrichedAddons(discoveryIndex, discoveryTemplate);
            foreach (var (spec, specIndex, tmpl) in investigationStages)
            {
                tmpl.Addons = engine.AssembleEnrichedAddons(specIndex, tmpl);
            }

            // ── Backward generation (same as LoopingLayoutQuestChain) ─────────
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Showdown: " + showdownTemplate.Name);
            showdownTemplate.outlawQuest.Setup(myMod, outlawNpc, showdownTemplate, null);
            if (!string.IsNullOrEmpty(showdownTemplate.outlawQuest.LogMessage))
                AITools.InjectContextIntoHistory($"[Stage '{showdownTemplate.Name}' log entry]: {showdownTemplate.outlawQuest.LogMessage}");

            AITools.InjectContextIntoHistory(
                "Important: from this point on the player does not know where the final showdown takes place. " +
                "Do not state the showdown location explicitly in any investigation or discovery mission. You may plant indirect clues that point toward it.");

            var lastOutlawQuest = showdownTemplate.outlawQuest;

            // Generate investigations (closest to showdown first, same as LoopingLayoutQuestChain)
            for (int i = 0; i < investigationStages.Count; i++)
            {
                Console.WriteLine("---------------------------------------------------------------------------------");
                var (spec, specIndex, template) = investigationStages[i];

                Console.WriteLine($"{spec.Role}: {template.Name}");
                template.outlawQuest.Setup(myMod, outlawNpc, template, lastOutlawQuest);

                if (!string.IsNullOrEmpty(template.outlawQuest.LogMessage))
                    AITools.InjectContextIntoHistory($"[Stage '{template.Name}' log entry]: {template.outlawQuest.LogMessage}");

                // Entity validation (observational — logs warnings only)
                EntityValidator.Check(spec, template.outlawQuest.LogMessage ?? "");

                lastOutlawQuest = template.outlawQuest;
            }

            // Discovery
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Discovery: " + discoveryTemplate.Name);
            discoveryTemplate.outlawQuest.Setup(myMod, outlawNpc, discoveryTemplate, lastOutlawQuest);

            // ── Final linking (same as LoopingLayoutQuestChain) ───────────────
            outlawNpc.GenerateLegendaryItem();
            Console.WriteLine("Generating Final Bounty Log...");
            outlawNpc.GenerateLog();

            // ── Writing polish pass ───────────────────────────────────────────
            var polishables = new List<IPolishable>();
            int invCount = storyStages.Count - 2;
            int invIndex = 0;
            foreach (var (stageName, tmpl, _) in storyStages)
            {
                string stageLabel;
                if (stageName == "Discovery")
                    stageLabel = "Act 1: Discovery";
                else if (stageName == "Showdown")
                    stageLabel = "Act 3: Showdown (Climax)";
                else
                {
                    invIndex++;
                    stageLabel = $"Act 2: Investigation {invIndex} of {invCount}";
                }
                foreach (var p in tmpl.outlawQuest.GetPolishables())
                    polishables.Add(new StageAnnotatedPolishable(p, stageLabel));
            }
            foreach (var p in outlawNpc.GetPolishables())
                polishables.Add(new StageAnnotatedPolishable(p, "Found Document (Outlaw Log)"));
            WritingPolishPass.Run(polishables);

            // ── Stage audio ───────────────────────────────────────────────────
            showdownTemplate.outlawQuest.StageAudio();
            foreach (var (_, _, tmpl) in investigationStages)
                tmpl.outlawQuest.StageAudio();
            discoveryTemplate.outlawQuest.StageAudio();

            SpeechTools.AddVoice(outlawNpc.Logfile.ID, outlawNpc.instance.FormKey.ID, outlawNpc.LogText, outlawNpc.VoiceEditorId, outlawNpc.ElevenLabsVoiceId);
            SpeechTools.GenerateAllWavs();
            SpeechTools.ConvertAndDeploy();
            return true;
        }
    }
}
