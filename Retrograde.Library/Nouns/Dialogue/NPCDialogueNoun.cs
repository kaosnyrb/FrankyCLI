using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Models;
using Retrograde.Utils;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Retrograde.Nouns;

/// <summary>
/// Builds a per-NPC dialogue quest using the scene-based pattern from atbb_mq01.
///
/// Architecture:
///   Quest → Greeting Scene (0x1834) + N Topic Scenes (one per exchange)
///
///   Greeting Scene (plays once on NPC activation):
///     Phase 0 "Greeting": NPC speaks opening line (DialogueSceneAction AliasID=0)
///
///   Linear Topic Scenes (one per exchange, forced sequential):
///     Phase 0 "": Player line shown as menu option (DialogueSceneAction AliasID=-2)
///     Phase 1..N: NPC reply lines (DialogueSceneAction AliasID=npcAliasId)
///     Exchange[0] flags=0x2814 — visible at stage 0,   advances to 100, re-evaluates topics
///     Exchange[1] flags=0x2814 — visible at stage 100, advances to 200, re-evaluates topics
///     Exchange[2] flags=0x2810 — visible at stage 200, advances to completionStage, ends conversation
///     Only one option visible at a time; player cannot skip beats.
///
/// Source of truth: atbb_mq01 [QUST:0008F6] in avontechblacksiteblueprints.esm.
/// </summary>
public class NPCDialogueNoun
{
    public Quest QuestRecord { get; }

    /// <param name="npcFormKey">
    /// Base-form FormKey of the NPC (used for UniqueActor alias).
    /// When using a placed REFR, switch to ForcedReference after verifying in-game.
    /// </param>
    public NPCDialogueNoun(
        FormKey        npcFormKey,
        string         voiceTypeEditorId,
        DialogueScript script,
        string         suffix,
        string         elevenLabsVoiceId = "",
        Quest?         existingQuest = null,
        int            completionStage = 0,
        string?        aliasName = null)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;

        Quest quest;
        uint npcAliasId;

        if (existingQuest != null)
        {
            quest = existingQuest;

            // If a name was provided, reuse the existing alias rather than creating a new one.
            var existingAlias = aliasName != null
                ? quest.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.Name == aliasName)
                : null;

            if (existingAlias != null)
            {
                npcAliasId = existingAlias.ID;
                goto aliasReady;
            }

            npcAliasId = (uint)(quest.Aliases?.Count ?? 0);
        }
        else
        {
            // ── Quest ──────────────────────────────────────────────────────────────
            quest = new Quest(targetMod)
            {
                EditorID = "dlg_quest_" + suffix,
                Data = new QuestData
                {
                    Flags = Quest.Flag.StartGameEnabled | Quest.Flag.StartsEnabled
                          | Quest.Flag.RunOnce | (Quest.Flag)0x10000,
                    Type  = Quest.TypeEnum.None,
                },
            };
            quest.Stages.Add(new QuestStage { Index = 0 });
            quest.Aliases = new ExtendedList<AQuestAlias>();
            targetMod.Quests.Add(quest);
            npcAliasId = 0;
        }

        // ── Alias ──────────────────────────────────────────────────────────────
        {
            var alias = new QuestReferenceAlias { ID = npcAliasId, Name = aliasName ?? "NPC" };
            alias.UniqueActor.SetTo(npcFormKey);
            quest.Aliases ??= new ExtendedList<AQuestAlias>();
            quest.Aliases.Add(alias);
        }

        aliasReady:

        // ── NPC greeting topic (Greeting Scene Phase 0) ───────────────────────
        var greetTopic = BuildSceneTopic(targetMod, quest);
        var greetInfo  = BuildInfo(targetMod, script.NpcGreeting, npcFormKey);
        greetTopic.Responses.Add(greetInfo);
        greetTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
            { greetInfo.FormKey.ToLink<IDialogResponsesGetter>() };
        quest.DialogTopics.Add(greetTopic);
        SpeechTools.GenerateWavs(greetInfo.FormKey.ID, voiceTypeEditorId,
            targetMod.ModKey, script.NpcGreeting, elevenLabsVoiceId);

        // ── Greeting Scene (flags=0x1834) — plays once on NPC activation ──────
        var greetScene = new Scene(targetMod) { EditorID = "dlg_scene_" + suffix + "_greeting" };
        greetScene.Quest.SetTo(quest.FormKey);
        greetScene.Flags = (Scene.Flag)0x00001834;
        greetScene.VNAM  = new byte[] { 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0 };
        greetScene.Conditions.Add(BuildGetIsIDCondition(npcFormKey));
        greetScene.Conditions.Add(BuildGetStageCondition(quest, 0, CompareOperator.EqualTo));
        greetScene.Actors.Add(new SceneActor { ID = npcAliasId,            BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
        greetScene.Actors.Add(new SceneActor { ID = unchecked((uint)-2), BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
        greetScene.Phases.Add(new ScenePhase { Name = "Greeting", EditorWidth = 298 });
        var greetAction = new DialogueSceneAction { Index = 1, AliasID = (int)npcAliasId, StartPhase = 0, EndPhase = 0 };
        greetAction.Topic.SetTo(greetTopic.FormKey);
        greetScene.Actions = new ExtendedList<ASceneAction> { greetAction };
        quest.Scenes.Add(greetScene);

        // ── Topic Scenes — one per exchange, shown in sequence ────────────────
        // Exchanges[0..N-2] use flags=0x2814 (TopLevelTopicsOnEnd): conversation stays open after
        // the NPC replies and re-evaluates topics. Because stage has advanced, only the next beat's
        // scene passes its GetStage condition, so the player sees exactly one new option.
        // Exchange[N-1] uses flags=0x2810 (no TopLevelTopicsOnEnd): closes the conversation.
        //   Exchange[0] visible at stage 0   → SetParentQuestStage → 100
        //   Exchange[1] visible at stage 100 → SetParentQuestStage → 200
        //   Exchange[2] visible at stage 200 → SetParentQuestStage → completionStage
        //
        // Flag breakdown (confirmed from City_NewAtlantis_Z_PartingGift_TL_HaddieQuest SCEN:000D53FB):
        //   0x2000 = Top Level (scene appears as selectable menu option)
        //   0x0800 = DisableDialogueCamera
        //   0x0010 = Interruptable
        //
        // Each scene:
        //   Phase 0:   player line shown as menu option  (DialogueSceneAction AliasID=-2, Index=3)
        //   Phase 1..N: NPC reply lines                  (DialogueSceneAction AliasID=npcAliasId, Index=4+j)
        for (int i = 0; i < script.Exchanges.Count; i++)
        {
            var ex = script.Exchanges[i];

            var playerTopic = BuildSceneTopic(targetMod, quest);
            var playerInfo  = BuildInfo(targetMod, ex.PlayerPrompt);
            playerTopic.Responses.Add(playerInfo);
            playerTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                { playerInfo.FormKey.ToLink<IDialogResponsesGetter>() };
            quest.DialogTopics.Add(playerTopic);

            // One topic+info per NPC reply line — each gets its own FormKey for audio.
            var npcEntries = new List<(DialogTopic topic, DialogResponses info)>();
            foreach (var line in ex.NpcReply)
            {
                var npcTopic = BuildSceneTopic(targetMod, quest);
                var npcInfo  = BuildInfo(targetMod, line, npcFormKey);
                npcTopic.Responses.Add(npcInfo);
                npcTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                    { npcInfo.FormKey.ToLink<IDialogResponsesGetter>() };
                quest.DialogTopics.Add(npcTopic);
                SpeechTools.GenerateWavs(npcInfo.FormKey.ID, voiceTypeEditorId,
                    targetMod.ModKey, line, elevenLabsVoiceId);
                npcEntries.Add((npcTopic, npcInfo));
            }

            // Advance to next stage after the last NPC line.
            int currentStage = 100 * i;
            int nextStage    = (i == script.Exchanges.Count - 1) ? completionStage : 100 * (i + 1);
            if (nextStage > 0 && npcEntries.Count > 0)
                npcEntries[^1].info.SetParentQuestStage = new DialogSetParentQuestStage { OnEnd = (short)nextStage };
            EnsureStage(quest, currentStage);
            if (nextStage > 0) EnsureStage(quest, nextStage);

            // All but the last exchange use 0x2814 (TopLevelTopicsOnEnd) so the conversation stays
            // open and re-evaluates available options after the NPC replies. Because stage has
            // advanced by then, only the next beat's scene passes its GetStage condition.
            // The final exchange uses 0x2810 (no TopLevelTopicsOnEnd) to close the conversation.
            bool isLast = i == script.Exchanges.Count - 1;
            var topicScene = new Scene(targetMod) { EditorID = "dlg_scene_" + suffix + "_topic_" + i };
            topicScene.Quest.SetTo(quest.FormKey);
            topicScene.Flags = (Scene.Flag)(isLast ? 0x00002810u : 0x00002814u);
            topicScene.VNAM  = new byte[] { 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0 };
            topicScene.Conditions.Add(BuildGetIsIDCondition(npcFormKey));
            topicScene.Conditions.Add(BuildGetStageCondition(quest, currentStage, CompareOperator.EqualTo));
            topicScene.Actors.Add(new SceneActor { ID = npcAliasId,            BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
            topicScene.Actors.Add(new SceneActor { ID = unchecked((uint)-2), BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });

            // Phase 0 = player; one phase per NPC reply line.
            topicScene.Phases.Add(new ScenePhase { Name = "", EditorWidth = 350 });
            for (int j = 0; j < npcEntries.Count; j++)
                topicScene.Phases.Add(new ScenePhase { Name = "", EditorWidth = 350 });

            var actions = new ExtendedList<ASceneAction>();
            var playerAction = new DialogueSceneAction { Index = 3, AliasID = -2, StartPhase = 0, EndPhase = 0 };
            playerAction.Topic.SetTo(playerTopic.FormKey);
            actions.Add(playerAction);
            for (int j = 0; j < npcEntries.Count; j++)
            {
                var npcAction = new DialogueSceneAction { Index = (uint?)(4 + j), AliasID = (int)npcAliasId, StartPhase = (uint)(j + 1), EndPhase = (uint)(j + 1) };
                npcAction.Topic.SetTo(npcEntries[j].topic.FormKey);
                actions.Add(npcAction);
            }
            topicScene.Actions = actions;
            quest.Scenes.Add(topicScene);
        }

        // ── Side color topics (Exchange[1], stage 100) ───────────────────────────
        // Two optional scenes that appear alongside the main beat-2 topic. Neither advances
        // the stage — the conversation re-evaluates and all three options remain visible.
        var sides = script.Exchanges[1].SideOptions;
        if (sides != null)
        {
            foreach (var (tag, side) in new[] { ("extra", sides.ExtraInfo), ("joke", sides.Joke) })
            {
                var playerTopic = BuildSceneTopic(targetMod, quest);
                var playerInfo  = BuildInfo(targetMod, side.PlayerPrompt);
                playerTopic.Responses.Add(playerInfo);
                playerTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                    { playerInfo.FormKey.ToLink<IDialogResponsesGetter>() };
                quest.DialogTopics.Add(playerTopic);

                var npcEntries = new List<(DialogTopic topic, DialogResponses info)>();
                foreach (var line in side.NpcReply)
                {
                    var npcTopic = BuildSceneTopic(targetMod, quest);
                    var npcInfo  = BuildInfo(targetMod, line, npcFormKey);
                    npcTopic.Responses.Add(npcInfo);
                    npcTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                        { npcInfo.FormKey.ToLink<IDialogResponsesGetter>() };
                    quest.DialogTopics.Add(npcTopic);
                    SpeechTools.GenerateWavs(npcInfo.FormKey.ID, voiceTypeEditorId,
                        targetMod.ModKey, line, elevenLabsVoiceId);
                    npcEntries.Add((npcTopic, npcInfo));
                }

                // No SetParentQuestStage — stage stays at 100 so all options reappear.
                var sideScene = new Scene(targetMod) { EditorID = "dlg_scene_" + suffix + "_topic_1_" + tag };
                sideScene.Quest.SetTo(quest.FormKey);
                sideScene.Flags = (Scene.Flag)0x00002814u;
                sideScene.VNAM  = new byte[] { 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0 };
                sideScene.Conditions.Add(BuildGetIsIDCondition(npcFormKey));
                sideScene.Conditions.Add(BuildGetStageCondition(quest, 100, CompareOperator.EqualTo));
                sideScene.Actors.Add(new SceneActor { ID = npcAliasId,            BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
                sideScene.Actors.Add(new SceneActor { ID = unchecked((uint)-2), BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });

                sideScene.Phases.Add(new ScenePhase { Name = "", EditorWidth = 350 });
                for (int j = 0; j < npcEntries.Count; j++)
                    sideScene.Phases.Add(new ScenePhase { Name = "", EditorWidth = 350 });

                var actions = new ExtendedList<ASceneAction>();
                var playerAction = new DialogueSceneAction { Index = 3, AliasID = -2, StartPhase = 0, EndPhase = 0 };
                playerAction.Topic.SetTo(playerTopic.FormKey);
                actions.Add(playerAction);
                for (int j = 0; j < npcEntries.Count; j++)
                {
                    var npcAction = new DialogueSceneAction { Index = (uint?)(4 + j), AliasID = (int)npcAliasId, StartPhase = (uint)(j + 1), EndPhase = (uint)(j + 1) };
                    npcAction.Topic.SetTo(npcEntries[j].topic.FormKey);
                    actions.Add(npcAction);
                }
                sideScene.Actions = actions;
                quest.Scenes.Add(sideScene);
            }
        }

        QuestRecord = quest;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static DialogTopic BuildSceneTopic(StarfieldMod targetMod, Quest quest)
    {
        var topic = new DialogTopic(targetMod)
        {
            Category    = DialogTopic.CategoryEnum.Scene,
            Subtype     = DialogTopic.SubtypeEnum.CustomScene,
            SubtypeName = DialogTopic.SubtypeNameEnum.CustomScene,
        };
        topic.Quest.SetTo(quest.FormKey);
        return topic;
    }

    // speakerFormKey: pass npcFormKey for NPC lines, omit for player lines (silent).
    private static DialogResponses BuildInfo(StarfieldMod targetMod, string text, FormKey speakerFormKey = default)
    {
        var info = new DialogResponses(targetMod)
        {
            SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low,
        };
        if (speakerFormKey != default)
            info.Speaker.SetTo(speakerFormKey);
        var textHash = SHA256.HashData(Encoding.UTF8.GetBytes(text))[..4];
        var response = new DialogResponse
        {
            ResponseText = text,
            // Player lines are silent in Starfield — WEMFile=0. NPC lines use info.FormKey.ID.
            WEMFile      = speakerFormKey != default ? info.FormKey.ID : 0u,
            TextHash     = textHash,
            EmotionOut   = 7.466667f,
        };
        response.Emotion.SetTo(FormKey.None);  // FFFFFFFF — "None Reference"
        info.Responses.Add(response);
        return info;
    }

    /// <summary>
    /// GetIsID(npcFormKey) EqualTo 1 — identifies the NPC that should activate this scene.
    /// Confirmed present on every interactive atbb_mq01 scene.
    /// </summary>
    private static ConditionFloat BuildGetIsIDCondition(FormKey npcFormKey)
    {
        var condData = new GetIsIDConditionData();
        condData.FirstParameter = new FormLinkOrIndex<IPlaceableObjectGetter>(condData, npcFormKey);
        return new ConditionFloat
        {
            ComparisonValue = 1,
            CompareOperator = CompareOperator.EqualTo,
            Data            = condData,
        };
    }

    private static void EnsureStage(Quest quest, int index)
    {
        if (!quest.Stages.Any(s => s.Index == index))
            quest.Stages.Add(new QuestStage { Index = (ushort)index });
    }

    /// <summary>
    /// GetStage(quest) [op] comparisonValue — gates the scene to a specific quest stage.
    /// SecondParameter is always 0 (unused) in atbb_mq01.
    /// </summary>
    private static ConditionFloat BuildGetStageCondition(Quest quest, int comparisonValue, CompareOperator op)
    {
        var condData = new GetStageConditionData();
        condData.FirstParameter = new FormLinkOrIndex<IQuestGetter>(condData, quest.FormKey);
        return new ConditionFloat
        {
            ComparisonValue = comparisonValue,
            CompareOperator = op,
            Data            = condData,
        };
    }
}
