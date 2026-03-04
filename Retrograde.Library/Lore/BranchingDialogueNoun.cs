using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Lore;
using Retrograde.Nouns;
using Retrograde.Utils;

namespace Retrograde.Lore;

/// <summary>
/// Walks a DialogueLore node graph and emits Mutagen quest + scenes.
///
/// Graph → scene mapping:
///   start npc_line (or chain)     → Greeting scene (0x1834), TopLevelTopicsOnEnd
///   player_choices node           → one topic scene per choice (0x2814 or 0x2810)
///   choice → npc_line chain       → phases 1..N of that choice's topic scene
///   requires_stage on choice      → extra GetStage >= N condition on scene
///   set_conversation_stage        → quest stage recorded (result-script wiring deferred to follow-up)
///
/// Stage numbering:
///   0              = initial greeting state
///   dialogue YAMLs use multiples of 5 for set_conversation_stage values
///   quest objective stages (100, 200, …) come from Papyrus — not assigned here
/// </summary>
public class BranchingDialogueNoun
{
    public Quest QuestRecord { get; }

    private readonly StarfieldMod        _mod;
    private readonly Quest               _quest;
    private readonly FormKey             _npcFormKey;
    private readonly string              _voiceTypeEditorId;
    private readonly string              _elevenLabsVoiceId;
    private readonly Dictionary<string, DialogueNode> _nodes;
    private readonly HashSet<string>     _visitedChoiceNodes = new();
    private int                          _sceneIndex;

    public BranchingDialogueNoun(
        FormKey       npcFormKey,
        string        voiceTypeEditorId,
        DialogueLore  dialogue,
        string        elevenLabsVoiceId = "")
    {
        _mod               = RetrogradeContext.Current.TargetMod;
        _npcFormKey        = npcFormKey;
        _voiceTypeEditorId = voiceTypeEditorId;
        _elevenLabsVoiceId = elevenLabsVoiceId;
        _nodes             = dialogue.Nodes;

        string suffix = "lore_" + dialogue.Id;

        // ── Quest ──────────────────────────────────────────────────────────────
        _quest = new Quest(_mod)
        {
            EditorID = "dlg_quest_" + suffix,
            Data = new QuestData
            {
                Flags = Quest.Flag.StartGameEnabled | Quest.Flag.StartsEnabled
                      | Quest.Flag.RunOnce | (Quest.Flag)0x10000,
                Type  = Quest.TypeEnum.None,
            },
        };
        _quest.Stages.Add(new QuestStage { Index = 0 });
        _quest.Stages.Add(new QuestStage { Index = 100 });

        // Collect all conversation stages referenced in the graph and add them
        foreach (var node in _nodes.Values.Where(n => n.SetConversationStage.HasValue))
            _quest.Stages.Add(new QuestStage { Index = (ushort)node.SetConversationStage!.Value });

        _quest.Aliases = new ExtendedList<AQuestAlias>();
        _mod.Quests.Add(_quest);

        // NPC alias
        var alias = new QuestReferenceAlias { ID = 0, Name = "NPC" };
        alias.UniqueActor.SetTo(npcFormKey);
        _quest.Aliases.Add(alias);

        // ── Walk the graph from the start node ────────────────────────────────
        var startNode = _nodes.GetValueOrDefault(dialogue.Start);
        if (startNode == null)
            throw new InvalidDataException($"BranchingDialogue '{dialogue.Id}': start node '{dialogue.Start}' not found.");

        string? firstChoicesId;
        if (startNode.Type == "npc_line")
        {
            // Collect the greeting chain and build greeting scene
            var (greetChain, nextId) = CollectNpcChain(dialogue.Start);
            int greetExitStage = ComputeExitStage(greetChain, 0);
            BuildGreetingScene(greetChain, suffix);
            firstChoicesId = nextId;
            ProcessChoicesNode(firstChoicesId, greetExitStage, suffix);
        }
        else if (startNode.Type == "player_choices")
        {
            // No greeting — go straight to choices (unusual but handled)
            ProcessChoicesNode(dialogue.Start, 0, suffix);
        }
        // "end" start node is a no-op

        QuestRecord = _quest;
    }

    // ── Greeting scene ─────────────────────────────────────────────────────────

    /// <summary>
    /// One-time greeting scene (flags=0x1834). Plays on first NPC activation.
    /// Multi-phase if the greeting chains through multiple npc_line nodes.
    /// </summary>
    private void BuildGreetingScene(List<(string id, DialogueNode node)> chain, string suffix)
    {
        var scene = new Scene(_mod) { EditorID = "dlg_scene_" + suffix + "_greeting" };
        scene.Quest.SetTo(_quest.FormKey);
        scene.Flags = (Scene.Flag)0x00001834;
        scene.VNAM  = new byte[] { 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0 };
        scene.Conditions.Add(DialogueHelpers.BuildGetIsIDCondition(_npcFormKey));
        scene.Conditions.Add(DialogueHelpers.BuildGetStageCondition(_quest, 0, CompareOperator.EqualTo));
        scene.Actors.Add(new SceneActor { ID = 0,                   BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
        scene.Actors.Add(new SceneActor { ID = unchecked((uint)-2), BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
        scene.Actions = new ExtendedList<ASceneAction>();

        for (int i = 0; i < chain.Count; i++)
        {
            var (_, npcNode) = chain[i];
            var texts = NodeTexts(npcNode);
            foreach (var text in texts)
            {
                var topic = DialogueHelpers.BuildSceneTopic(_mod, _quest);
                var info  = DialogueHelpers.BuildInfo(_mod, text, _npcFormKey);
                topic.Responses.Add(info);
                topic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                    { info.FormKey.ToLink<IDialogResponsesGetter>() };
                _quest.DialogTopics.Add(topic);
                SpeechTools.GenerateWavs(info.FormKey.ID, _voiceTypeEditorId,
                    _mod.ModKey, text, _elevenLabsVoiceId);

                int phaseIdx = scene.Phases.Count;
                scene.Phases.Add(new ScenePhase { Name = i == 0 && phaseIdx == 0 ? "Greeting" : "", EditorWidth = 298 });
                var action = new DialogueSceneAction { Index = 1, AliasID = 0, StartPhase = (uint)phaseIdx, EndPhase = (uint)phaseIdx };
                action.Topic.SetTo(topic.FormKey);
                scene.Actions.Add(action);
            }
        }

        _quest.Scenes.Add(scene);
    }

    // ── Choice nodes ───────────────────────────────────────────────────────────

    /// <summary>
    /// Recursively processes a player_choices node.
    /// Each choice becomes a topic scene (player prompt + NPC reply chain).
    /// </summary>
    private void ProcessChoicesNode(string? nodeId, int stageCondition, string suffix)
    {
        if (nodeId == null) return;
        if (!_nodes.TryGetValue(nodeId, out var node)) return;
        if (node.Type == "end") return;
        if (node.Type != "player_choices") return;
        if (!_visitedChoiceNodes.Add(nodeId)) return;  // already processed

        foreach (var choice in node.Choices)
        {
            if (string.IsNullOrEmpty(choice.Next)) continue;

            // Collect NPC reply chain after this choice
            var (npcChain, nextChoicesId) = CollectNpcChain(choice.Next);
            int exitStage = ComputeExitStage(npcChain, stageCondition);
            bool isEndScene = nextChoicesId == null
                || !_nodes.TryGetValue(nextChoicesId, out var nextNode)
                || nextNode.Type == "end";

            BuildChoiceTopicScene(choice, npcChain, stageCondition, isEndScene, suffix);

            // Recurse to the next choices node
            if (!isEndScene)
                ProcessChoicesNode(nextChoicesId, exitStage, suffix);
        }
    }

    /// <summary>
    /// Builds one topic scene for a player choice.
    ///   Phase 0:   player prompt            (AliasID=-2, Index=3)
    ///   Phase 1..N: NPC reply chain         (AliasID=0,  Index=4)
    /// </summary>
    private void BuildChoiceTopicScene(
        PlayerChoice choice,
        List<(string id, DialogueNode node)> npcChain,
        int stageCondition,
        bool isEndScene,
        string suffix)
    {
        uint sceneFlags = isEndScene ? 0x00002810u : 0x00002814u;
        int idx = _sceneIndex++;

        var scene = new Scene(_mod) { EditorID = "dlg_scene_" + suffix + "_choice_" + idx };
        scene.Quest.SetTo(_quest.FormKey);
        scene.Flags = (Scene.Flag)sceneFlags;
        scene.VNAM  = new byte[] { 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0 };
        scene.Conditions.Add(DialogueHelpers.BuildGetIsIDCondition(_npcFormKey));
        scene.Conditions.Add(DialogueHelpers.BuildGetStageCondition(_quest, stageCondition, CompareOperator.EqualTo));
        if (choice.RequiresStage.HasValue)
            scene.Conditions.Add(DialogueHelpers.BuildGetStageCondition(_quest, choice.RequiresStage.Value, CompareOperator.GreaterThanOrEqualTo));
        scene.Actors.Add(new SceneActor { ID = 0,                   BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
        scene.Actors.Add(new SceneActor { ID = unchecked((uint)-2), BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
        scene.Actions = new ExtendedList<ASceneAction>();

        // Phase 0 — player choice text
        var playerTopic = DialogueHelpers.BuildSceneTopic(_mod, _quest);
        var playerInfo  = DialogueHelpers.BuildInfo(_mod, choice.Text);
        playerTopic.Responses.Add(playerInfo);
        playerTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
            { playerInfo.FormKey.ToLink<IDialogResponsesGetter>() };
        _quest.DialogTopics.Add(playerTopic);

        scene.Phases.Add(new ScenePhase { Name = "", EditorWidth = 350 });
        var playerAction = new DialogueSceneAction { Index = 3, AliasID = -2, StartPhase = 0, EndPhase = 0 };
        playerAction.Topic.SetTo(playerTopic.FormKey);
        scene.Actions.Add(playerAction);

        // Phases 1..N — NPC reply chain
        int phaseNum = 1;
        foreach (var (_, npcNode) in npcChain)
        {
            foreach (var text in NodeTexts(npcNode))
            {
                var npcTopic = DialogueHelpers.BuildSceneTopic(_mod, _quest);
                var npcInfo  = DialogueHelpers.BuildInfo(_mod, text, _npcFormKey);
                npcTopic.Responses.Add(npcInfo);
                npcTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                    { npcInfo.FormKey.ToLink<IDialogResponsesGetter>() };
                _quest.DialogTopics.Add(npcTopic);
                SpeechTools.GenerateWavs(npcInfo.FormKey.ID, _voiceTypeEditorId,
                    _mod.ModKey, text, _elevenLabsVoiceId);

                scene.Phases.Add(new ScenePhase { Name = "", EditorWidth = 350 });
                var npcAction = new DialogueSceneAction { Index = 4, AliasID = 0, StartPhase = (uint)phaseNum, EndPhase = (uint)phaseNum };
                npcAction.Topic.SetTo(npcTopic.FormKey);
                scene.Actions.Add(npcAction);
                phaseNum++;
            }
        }

        _quest.Scenes.Add(scene);
    }

    // ── Graph walk helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Follows npc_line nodes from startId until a non-npc_line node is reached.
    /// Returns the chain and the id of the terminating node (player_choices / end / null).
    /// </summary>
    private (List<(string id, DialogueNode node)> chain, string? nextId) CollectNpcChain(string startId)
    {
        var chain = new List<(string, DialogueNode)>();
        string? current = startId;

        while (current != null && _nodes.TryGetValue(current, out var node) && node.Type == "npc_line")
        {
            chain.Add((current, node));
            current = string.IsNullOrEmpty(node.Next) ? null : node.Next;
        }

        return (chain, current);
    }

    /// <summary>
    /// Returns the effective stage after a chain plays — last set_conversation_stage in the chain, or entryStage.
    /// </summary>
    private static int ComputeExitStage(List<(string id, DialogueNode node)> chain, int entryStage)
    {
        for (int i = chain.Count - 1; i >= 0; i--)
            if (chain[i].node.SetConversationStage.HasValue)
                return chain[i].node.SetConversationStage!.Value;
        return entryStage;
    }

    /// <summary>Returns the effective text list for a node (segments if present, else single text).</summary>
    private static List<string> NodeTexts(DialogueNode node)
    {
        if (node.Segments is { Count: > 0 })
            return node.Segments;
        if (!string.IsNullOrEmpty(node.Text))
            return new List<string> { node.Text };
        return new List<string>();
    }
}
