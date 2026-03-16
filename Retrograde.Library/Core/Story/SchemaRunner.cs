using Mutagen.Bethesda.Starfield;
using Retrograde.AI;
using Retrograde.AI.Utils;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Retrograde.Nouns;
using Retrograde.Quests.TemplateEngines;
using Retrograde.Utils;
using Retrograde.Writing;
using System.Text;

namespace Retrograde.Story
{
    /// <summary>
    /// Data-driven quest chain orchestrator. Reads a StorySchema and produces
    /// the same output as LoopingLayoutQuestChain when given the bounty_hunt schema.
    ///
    /// Phase 1.5: uses air-gapped contexts for all beat content generation.
    /// The shared conversation (lore, arc selection, fact planning) runs first,
    /// then each beat generates content via RunIsolatedPrompt with a sealed
    /// ContextEnvelope. No beat can see another beat's generated content.
    /// </summary>
    public class SchemaRunner : IQuestchain
    {
        public StarfieldMod myMod;
        public StorySchema Schema;

        // Optional pinned template overrides (same as LoopingLayoutQuestChain)
        public string ShowdownTemplate = "";
        public string InvestigationTemplate = "";
        public string DiscoveryTemplate = "";

        public SchemaRunner(StarfieldMod myModparam, StorySchema schema)
        {
            myMod = myModparam;
            Schema = schema;
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
            Console.WriteLine($"SchemaRunner [{Schema.DisplayName}]");
            var templateManager = new AllTemplateManager(new AI_TemplateEngine());

            // ══════════════════════════════════════════════════════════════════
            //  SHARED PHASE — one AI conversation, outputs frozen, then discarded
            // ══════════════════════════════════════════════════════════════════

            // ── Cast setup ──────────────────────────────────────────────────
            OutlawNpc outlawNpc = new OutlawNpc(myMod, true);

            Console.WriteLine("Generating Lore File...");
            var Lorefile = LorePrompts.GenerateLoreFile(outlawNpc.Traits);

            Console.WriteLine("Building Lore Context...");
            var pinnedInvestigations = InvestigationTemplate != ""
                ? new List<string>() { InvestigationTemplate }.Where(s => s != "").ToList()
                : null;

            LorePrompts.GenerateLoreContext(outlawNpc, Lorefile, templateManager.AvailableLib,
                pinnedDiscovery:      DiscoveryTemplate     != "" ? DiscoveryTemplate     : null,
                pinnedShowdown:       ShowdownTemplate      != "" ? ShowdownTemplate      : null,
                pinnedInvestigations: pinnedInvestigations);

            var cast = StoryCast.FromOutlawNpc(outlawNpc);

            // ── Template selection (driven by schema beat definitions) ───────
            var showdownBeat = Schema.GetBeat("showdown");
            var showdownAddons = new List<string>()
            {
                "<QuestStage>Showdown</QuestStage>",
                $"<QuestProgress>{showdownBeat?.ProgressMin ?? 90}%</QuestProgress>"
            };

            var ShowdownMissionTemplate = templateManager.GetShowdownMissionTemplate(LorePrompts.PlannedShowdown, showdownAddons);

            outlawNpc.spacesuit = ShowdownMissionTemplate.parameters.ContainsKey("NeedSpacesuit") && (bool)ShowdownMissionTemplate.parameters["NeedSpacesuit"];
            outlawNpc.GenerateNPC();
            cast.GetRole("target").Name = outlawNpc.name;

            // Still inject into shared history for the fact planning pass
            AITools.InjectContextIntoHistory(
                $"The outlaw target's name is '{outlawNpc.name}'. " +
                "Use this name in log entries, clues, and any in-world references to the target."
            );

            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Outlaw Name: " + outlawNpc.name);

            // ── Investigation stages ────────────────────────────────────────
            var investigationBeat = Schema.GetBeat("investigation");
            var plannedList = LorePrompts.PlannedInvestigations;
            int count = plannedList.Count;

            var investigationStages = new List<(string Stage, MissionTemplate Template)>();

            for (int i = 0; i < count; i++)
            {
                Console.WriteLine("---------------------------------------------------------------------------------");

                string stageName = (i == 0) ? "DeepInvestigation" : "InitialInvestigation";

                int progressHigh = investigationBeat?.ProgressMax ?? 70;
                int progressLow  = investigationBeat?.ProgressMin ?? 20;
                double t = (count == 1) ? 0.0 : (double)i / (count - 1);
                int progressValue = progressHigh - (int)((progressHigh - progressLow) * t);
                if (progressValue < 10) progressValue = 10;

                var investigationAddons = new List<string>()
                {
                    $"<QuestStage>{stageName}</QuestStage>",
                    $"<QuestProgress>{progressValue}%</QuestProgress>"
                };

                string plannedName = plannedList[count - 1 - i];
                var template = templateManager.GetInvestigationMissionTemplate(plannedName, investigationAddons);

                Console.WriteLine("Investigation Template: " + template.Name);
                investigationStages.Add((stageName, template));
            }

            // ── Discovery ───────────────────────────────────────────────────
            Console.WriteLine("---------------------------------------------------------------------------------");

            var discoveryBeat = Schema.GetBeat("discovery");
            var discoveryAddons = new List<string>()
            {
                "<QuestStage>Discovery</QuestStage>",
                $"<QuestProgress>{discoveryBeat?.ProgressMin ?? 0}%</QuestProgress>"
            };

            var DiscoveryMissionTemplate = templateManager.GetDiscoveryMissionTemplate(LorePrompts.PlannedDiscovery, discoveryAddons);

            // ── Stage location history ──────────────────────────────────────
            var storyStages = new List<(string Stage, MissionTemplate Template)>();

            storyStages.Add(("Discovery", DiscoveryMissionTemplate));

            for (int i = investigationStages.Count - 1; i >= 0; i--)
                storyStages.Add(investigationStages[i]);

            storyStages.Add(("Showdown", ShowdownMissionTemplate));

            for (int i = 0; i < storyStages.Count; i++)
            {
                var current = storyStages[i];
                for (int j = 0; j < i; j++)
                {
                    var previous = storyStages[j];
                    AddStageLocation(current.Template, previous.Stage, previous.Template.Location);
                }
            }

            // ── Fact Planning Pass ──────────────────────────────────────────
            // Build the beat list in generation order (showdown first, discovery last)
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Running Fact Planning Pass...");

            var beatList = new List<(string BeatId, string TemplateName, string Location, string StageName)>();
            beatList.Add(("showdown", ShowdownMissionTemplate.Name, ShowdownMissionTemplate.Location, "Showdown"));
            for (int i = 0; i < investigationStages.Count; i++)
            {
                var (stage, tmpl) = investigationStages[i];
                beatList.Add(($"investigation_{i}", tmpl.Name, tmpl.Location, stage));
            }
            beatList.Add(("discovery", DiscoveryMissionTemplate.Name, DiscoveryMissionTemplate.Location, "Discovery"));

            var (allFacts, playerKnowledgeTimeline) = FactPlanningPass.Plan(
                LorePrompts.LoreContext, outlawNpc.name, beatList);

            // Build frozen context for envelopes
            string loreSummary = FactPlanningPass.BuildLoreSummary(LorePrompts.LoreContext, outlawNpc.name, cast);
            var castSheetSb = new StringBuilder();
            cast.AppendToPrompt(castSheetSb);
            string castSheet = castSheetSb.ToString();
            string contentRules = $"You are writing for a {Schema.Stakes.PlayerRole}. " +
                "Style: terse field notes. Every sentence states a concrete fact. " +
                "No metaphors, no atmospheric filler.";

            // ══════════════════════════════════════════════════════════════════
            //  BEAT PHASE — each beat in isolation, back-to-front
            // ══════════════════════════════════════════════════════════════════

            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Showdown: " + ShowdownMissionTemplate.Name);

            // Showdown — no bridge (final beat)
            SetEnvelopeForBeat("showdown", allFacts, loreSummary, castSheet, contentRules,
                outlawNpc.name, playerKnowledgeTimeline, storyStages.Count - 1, storyStages.Count);

            var showdownBeatAdapter = WrapBeat(ShowdownMissionTemplate.outlawQuest);
            showdownBeatAdapter.Setup(myMod, cast, MakeBeatContext(ShowdownMissionTemplate, "Showdown", showdownBeat?.ProgressMin ?? 90), null);
            ClearEnvelope();

            IStoryBeat lastBeat = showdownBeatAdapter;

            for (int i = 0; i < investigationStages.Count; i++)
            {
                Console.WriteLine("---------------------------------------------------------------------------------");
                var (stageName, template) = investigationStages[i];
                Console.WriteLine(stageName + ": " + template.Name);

                string beatId = $"investigation_{i}";

                // Generate bridge to successor (isolated, location-pair only)
                if (Schema.Arc.Bridges)
                {
                    var successorTemplate = (i == 0) ? ShowdownMissionTemplate : investigationStages[i - 1].Template;
                    string bridgeText = ValidatedPrompt.RunBridge(template.Location, successorTemplate.Location);

                    Console.WriteLine($"[Bridge] {template.Location} → {successorTemplate.Location}");
                    if (allFacts.TryGetValue(beatId, out var facts))
                        facts.BridgeText = bridgeText;

                    if (template.Addons == null)
                        template.Addons = new List<string>();
                    template.Addons.Add($"<StageBridge>{bridgeText}</StageBridge>");
                }

                // Determine story-order index for PlayerKnowledge slicing
                // storyStages is [Discovery, ...investigations(reversed), Showdown]
                // This investigation's position in storyStages:
                int storyIndex = storyStages.FindIndex(s => s.Template == template);

                SetEnvelopeForBeat(beatId, allFacts, loreSummary, castSheet, contentRules,
                    outlawNpc.name, playerKnowledgeTimeline, storyIndex, storyStages.Count);

                var beat = WrapBeat(template.outlawQuest);
                int progressHigh = investigationBeat?.ProgressMax ?? 70;
                int progressLow  = investigationBeat?.ProgressMin ?? 20;
                double t2 = (count == 1) ? 0.0 : (double)i / (count - 1);
                int progressValue = progressHigh - (int)((progressHigh - progressLow) * t2);
                if (progressValue < 10) progressValue = 10;

                beat.Setup(myMod, cast, MakeBeatContext(template, stageName, progressValue), lastBeat);
                ClearEnvelope();
                lastBeat = beat;
            }

            // Discovery
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Discovery: " + DiscoveryMissionTemplate.Name);

            if (Schema.Arc.Bridges && investigationStages.Count > 0)
            {
                var firstInvTemplate = investigationStages[^1].Template;
                string bridgeText = ValidatedPrompt.RunBridge(DiscoveryMissionTemplate.Location, firstInvTemplate.Location);

                Console.WriteLine($"[Bridge] {DiscoveryMissionTemplate.Location} → {firstInvTemplate.Location}");
                if (allFacts.TryGetValue("discovery", out var discFacts))
                    discFacts.BridgeText = bridgeText;

                if (DiscoveryMissionTemplate.Addons == null)
                    DiscoveryMissionTemplate.Addons = new List<string>();
                DiscoveryMissionTemplate.Addons.Add($"<StageBridge>{bridgeText}</StageBridge>");
            }

            // Discovery is first in story order → PlayerKnowledge is EMPTY
            SetEnvelopeForBeat("discovery", allFacts, loreSummary, castSheet, contentRules,
                outlawNpc.name, playerKnowledgeTimeline, 0, storyStages.Count);

            var discoveryBeatAdapter = WrapBeat(DiscoveryMissionTemplate.outlawQuest);
            discoveryBeatAdapter.Setup(myMod, cast, MakeBeatContext(DiscoveryMissionTemplate, "Discovery", discoveryBeat?.ProgressMin ?? 0), lastBeat);
            ClearEnvelope();

            // ══════════════════════════════════════════════════════════════════
            //  POST-GENERATION
            // ══════════════════════════════════════════════════════════════════

            outlawNpc.GenerateLegendaryItem();
            Console.WriteLine("Generating Final Bounty Log...");
            outlawNpc.GenerateLog();

            // ── Writing polish pass ─────────────────────────────────────────
            var polishables = new List<IPolishable>();
            int invCount = storyStages.Count - 2;
            int invIndex = 0;
            foreach (var (stageName, tmpl) in storyStages)
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

            // ── Stage audio ─────────────────────────────────────────────────
            ShowdownMissionTemplate.outlawQuest.StageAudio();
            foreach (var (_, tmpl) in investigationStages)
                tmpl.outlawQuest.StageAudio();
            DiscoveryMissionTemplate.outlawQuest.StageAudio();

            SpeechTools.AddVoice(outlawNpc.Logfile.ID, outlawNpc.instance.FormKey.ID, outlawNpc.LogText, outlawNpc.VoiceEditorId, outlawNpc.ElevenLabsVoiceId);
            SpeechTools.GenerateAllWavs();
            SpeechTools.ConvertAndDeploy();
            return true;
        }

        /// <summary>
        /// Set the ambient PromptContext for a beat's Setup() call.
        /// storyIndex is this beat's position in story order (0 = discovery, last = showdown).
        /// PlayerKnowledge is sliced to include only entries UP TO (not including) this beat.
        /// </summary>
        private void SetEnvelopeForBeat(
            string beatId,
            Dictionary<string, BeatFacts> allFacts,
            string loreSummary,
            string castSheet,
            string contentRules,
            string targetName,
            List<string> playerKnowledgeTimeline,
            int storyIndex,
            int totalBeats)
        {
            var facts = allFacts.TryGetValue(beatId, out var f) ? f : new BeatFacts { BeatId = beatId };

            // PlayerKnowledge: story-order entries up to (not including) this beat
            var knowledge = new List<string>();
            for (int i = 0; i < Math.Min(storyIndex, playerKnowledgeTimeline.Count); i++)
                knowledge.Add(playerKnowledgeTimeline[i]);

            var envelope = new ContextEnvelope
            {
                LoreSummary = loreSummary,
                CastSheet = castSheet,
                Facts = facts,
                PlayerKnowledge = knowledge,
                ContentRules = contentRules,
                TargetName = targetName,
            };

            PromptContext.CurrentEnvelope = envelope;
            PromptContext.CurrentFacts = facts;
            PromptContext.TargetName = targetName;

            Console.WriteLine($"[Envelope] Beat '{beatId}': {facts.Location}, {knowledge.Count} knowledge entries, {facts.AllowedNpcRoles.Count} NPCs, {facts.AllowedObjects.Count} objects");
        }

        private void ClearEnvelope()
        {
            PromptContext.CurrentEnvelope = null;
            PromptContext.CurrentFacts = null;
            PromptContext.TargetName = "";
        }

        private OutlawQuestAdapter WrapBeat(IOutlawQuest quest) => new OutlawQuestAdapter(quest);

        private BeatContext MakeBeatContext(MissionTemplate template, string stageName, int progress)
        {
            return new BeatContext
            {
                Template = template,
                SchemaId = Schema.Id,
                NarrativeFunction = stageName,
                ProgressPercent = progress,
                StageName = stageName,
                PlayerRole = Schema.Stakes.PlayerRole,
                Cast = null!,  // cast is passed via Setup() directly
            };
        }
    }
}
