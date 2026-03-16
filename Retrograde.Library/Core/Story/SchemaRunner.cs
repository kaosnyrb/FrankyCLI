using Mutagen.Bethesda.Starfield;
using Retrograde.AI;
using Retrograde.AI.Utils;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Retrograde.Nouns;
using Retrograde.Quests.TemplateEngines;
using Retrograde.Utils;
using Retrograde.Writing;

namespace Retrograde.Story
{
    /// <summary>
    /// Data-driven quest chain orchestrator. Reads a StorySchema and produces
    /// the same output as LoopingLayoutQuestChain when given the bounty_hunt schema.
    ///
    /// Phase 1: delegates to existing LorePrompts, ITemplateManager, and IOutlawQuest
    /// implementations via the adapter pattern. The schema controls the structure;
    /// existing code handles the content.
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
            Console.WriteLine($"SchemaRunner [{Schema.DisplayName}]");
            var templateManager = new AllTemplateManager(new AI_TemplateEngine());

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
            // Update cast with finalized NPC name
            cast.GetRole("target").Name = outlawNpc.name;

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

                // Replicate the exact progress calculation from LoopingLayoutQuestChain
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

            // ── Generate quests (reverse narrative order) ───────────────────
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Showdown: " + ShowdownMissionTemplate.Name);
            var showdownBeatAdapter = WrapBeat(ShowdownMissionTemplate.outlawQuest);
            showdownBeatAdapter.Setup(myMod, cast, MakeBeatContext(ShowdownMissionTemplate, "Showdown", showdownBeat?.ProgressMin ?? 90), null);

            if (!string.IsNullOrEmpty(showdownBeatAdapter.LogMessage))
                AITools.InjectContextIntoHistory($"[Stage '{ShowdownMissionTemplate.Name}' log entry]: {showdownBeatAdapter.LogMessage}");

            AITools.InjectContextIntoHistory(
                "Important: from this point on the player does not know where the final showdown takes place. " +
                "Do not state the showdown location explicitly in any investigation or discovery mission. You may plant indirect clues that point toward it."
            );

            IStoryBeat lastBeat = showdownBeatAdapter;

            for (int i = 0; i < investigationStages.Count; i++)
            {
                Console.WriteLine("---------------------------------------------------------------------------------");
                var (stageName, template) = investigationStages[i];

                Console.WriteLine(stageName + ": " + template.Name);

                if (Schema.Arc.Bridges)
                {
                    if (i > 0)
                        GenerateStageBridge(template, investigationStages[i - 1].Template);
                    else
                        GenerateStageBridge(template, ShowdownMissionTemplate);
                }

                var beat = WrapBeat(template.outlawQuest);
                int progressHigh = investigationBeat?.ProgressMax ?? 70;
                int progressLow  = investigationBeat?.ProgressMin ?? 20;
                double t = (count == 1) ? 0.0 : (double)i / (count - 1);
                int progressValue = progressHigh - (int)((progressHigh - progressLow) * t);
                if (progressValue < 10) progressValue = 10;

                beat.Setup(myMod, cast, MakeBeatContext(template, stageName, progressValue), lastBeat);
                if (!string.IsNullOrEmpty(beat.LogMessage))
                    AITools.InjectContextIntoHistory($"[Stage '{template.Name}' log entry]: {beat.LogMessage}");
                lastBeat = beat;
            }

            // Discovery
            Console.WriteLine("---------------------------------------------------------------------------------");
            Console.WriteLine("Discovery: " + DiscoveryMissionTemplate.Name);
            if (Schema.Arc.Bridges && investigationStages.Count > 0)
                GenerateDiscoveryBridge(DiscoveryMissionTemplate, investigationStages[^1].Template);

            var discoveryBeatAdapter = WrapBeat(DiscoveryMissionTemplate.outlawQuest);
            discoveryBeatAdapter.Setup(myMod, cast, MakeBeatContext(DiscoveryMissionTemplate, "Discovery", discoveryBeat?.ProgressMin ?? 0), lastBeat);

            // ── Post-generation ─────────────────────────────────────────────
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
