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
///   Completion Topic (Exchange[0], flags=0x2810 — Top Level visible, no TopLevelTopicsOnEnd):
///     Phase 0 "": Player line shown as menu option (DialogueSceneAction AliasID=-2)
///     Phase 1 "": NPC reply (DialogueSceneAction AliasID=0)
///     Ends the conversation — does NOT loop back to topic menu.
///
///   Regular Topic Scenes (Exchange[1..N-1], flags=0x2814 — Top Level visible + TopLevelTopicsOnEnd):
///     Phase 0 "": Player line shown as menu option (DialogueSceneAction AliasID=-2)
///     Phase 1 "": NPC reply (DialogueSceneAction AliasID=0)
///     Loops back to show topic menu after NPC replies.
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
            quest.Stages.Add(new QuestStage { Index = 100 });
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

        // ── Topic Scenes — one per exchange ───────────────────────────────────
        // Exchange[0] = completion topic (flags=0x2810 — Top Level visible, no TopLevelTopicsOnEnd)
        // Exchange[1..N-1] = regular topics (flags=0x2814 — Top Level visible + 0x0004 TopLevelTopicsOnEnd)
        //
        // Flag breakdown (confirmed from City_NewAtlantis_Z_PartingGift_TL_HaddieQuest SCEN:000D53FB):
        //   0x2000 = Top Level (scene appears as selectable menu option) — both types
        //   0x0800 = DisableDialogueCamera — both types
        //   0x0010 = Interruptable — both types
        //   0x0004 = TopLevelTopicsOnEnd (menu reappears after scene ends) — regular only
        //
        // Each scene:
        //   Phase 0: player line shown as menu option  (DialogueSceneAction AliasID=-2, Index=3)
        //   Phase 1: NPC reply                         (DialogueSceneAction AliasID=0,  Index=4)
        for (int i = 0; i < script.Exchanges.Count; i++)
        {
            var ex = script.Exchanges[i];

            var playerTopic = BuildSceneTopic(targetMod, quest);
            var playerInfo  = BuildInfo(targetMod, ex.PlayerPrompt);
            playerTopic.Responses.Add(playerInfo);
            playerTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                { playerInfo.FormKey.ToLink<IDialogResponsesGetter>() };
            quest.DialogTopics.Add(playerTopic);

            var npcTopic = BuildSceneTopic(targetMod, quest);
            var npcInfo  = BuildInfo(targetMod, ex.NpcReply, npcFormKey);
            if (i == 0 && completionStage > 0)
                npcInfo.SetParentQuestStage = new DialogSetParentQuestStage { OnEnd = (short)completionStage };
            npcTopic.Responses.Add(npcInfo);
            npcTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                { npcInfo.FormKey.ToLink<IDialogResponsesGetter>() };
            quest.DialogTopics.Add(npcTopic);
            SpeechTools.GenerateWavs(npcInfo.FormKey.ID, voiceTypeEditorId,
                targetMod.ModKey, ex.NpcReply, elevenLabsVoiceId);

            // Exchange[0] ends the conversation (no 0x0004 TopLevelTopicsOnEnd); the rest loop back
            uint topicFlags = i == 0 ? 0x00002810u : 0x00002814u;
            var topicScene = new Scene(targetMod) { EditorID = "dlg_scene_" + suffix + "_topic_" + i };
            topicScene.Quest.SetTo(quest.FormKey);
            topicScene.Flags = (Scene.Flag)topicFlags;
            topicScene.VNAM  = new byte[] { 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0 };
            topicScene.Conditions.Add(BuildGetIsIDCondition(npcFormKey));
            topicScene.Conditions.Add(BuildGetStageCondition(quest, 0, CompareOperator.EqualTo));
            topicScene.Actors.Add(new SceneActor { ID = npcAliasId,            BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
            topicScene.Actors.Add(new SceneActor { ID = unchecked((uint)-2), BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
            topicScene.Phases.Add(new ScenePhase { Name = "", EditorWidth = 350 });
            topicScene.Phases.Add(new ScenePhase { Name = "", EditorWidth = 350 });
            var playerAction = new DialogueSceneAction { Index = 3, AliasID = -2, StartPhase = 0, EndPhase = 0 };
            playerAction.Topic.SetTo(playerTopic.FormKey);
            var npcAction = new DialogueSceneAction { Index = 4, AliasID = (int)npcAliasId, StartPhase = 1, EndPhase = 1 };
            npcAction.Topic.SetTo(npcTopic.FormKey);
            topicScene.Actions = new ExtendedList<ASceneAction> { playerAction, npcAction };
            quest.Scenes.Add(topicScene);
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
