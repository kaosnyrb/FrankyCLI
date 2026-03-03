using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Models;
using Retrograde.Utils;
using System.Security.Cryptography;
using System.Text;

namespace Retrograde.Nouns;

/// <summary>
/// Builds a per-NPC dialogue quest using the scene-based pattern from atbb_mq01.
///
/// Architecture:
///   Quest → one Scene (greeting + PlayerDialogue choice menu)
///   All topics: Category=Scene, Subtype=CustomScene
///   Player choices and NPC replies live in PlayerDialogueSceneAction.DialogueList items.
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
        string         elevenLabsVoiceId = "")
    {
        var targetMod = RetrogradeContext.Current.TargetMod;

        // ── Quest ──────────────────────────────────────────────────────────────
        var quest = new Quest(targetMod)
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

        // ── Alias ──────────────────────────────────────────────────────────────
        var alias = new QuestReferenceAlias { ID = 0, Name = "NPC" };
        alias.UniqueActor.SetTo(npcFormKey);
        quest.Aliases.Add(alias);

        // ── NPC greeting topic (Scene Phase 0) ────────────────────────────────
        var greetTopic = BuildSceneTopic(targetMod, quest);
        var greetInfo  = BuildInfo(targetMod, script.NpcGreeting);
        greetTopic.Responses.Add(greetInfo);
        greetTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
            { greetInfo.FormKey.ToLink<IDialogResponsesGetter>() };
        quest.DialogTopics.Add(greetTopic);
        SpeechTools.GenerateWavs(greetInfo.FormKey.ID, voiceTypeEditorId,
            targetMod.ModKey, script.NpcGreeting, elevenLabsVoiceId);

        // ── PlayerChoice + NpcResponse topic pairs (Scene Phase 1) ────────────
        var dialogueItems = new ExtendedList<PlayerDialogueSceneActionItem>();
        foreach (var ex in script.Exchanges)
        {
            // Player question topic (voiced by player — no SpeechTools call)
            var playerTopic = BuildSceneTopic(targetMod, quest);
            var playerInfo  = BuildInfo(targetMod, ex.PlayerPrompt);
            playerTopic.Responses.Add(playerInfo);
            playerTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                { playerInfo.FormKey.ToLink<IDialogResponsesGetter>() };
            quest.DialogTopics.Add(playerTopic);

            // NPC reply topic
            var npcTopic = BuildSceneTopic(targetMod, quest);
            var npcInfo  = BuildInfo(targetMod, ex.NpcReply);
            npcTopic.Responses.Add(npcInfo);
            npcTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                { npcInfo.FormKey.ToLink<IDialogResponsesGetter>() };
            quest.DialogTopics.Add(npcTopic);
            SpeechTools.GenerateWavs(npcInfo.FormKey.ID, voiceTypeEditorId,
                targetMod.ModKey, ex.NpcReply, elevenLabsVoiceId);

            var item = new PlayerDialogueSceneActionItem();
            item.PlayerChoice.SetTo(playerTopic.FormKey);
            item.NpcResponse.SetTo(npcTopic.FormKey);
            dialogueItems.Add(item);
        }

        // ── Scene ──────────────────────────────────────────────────────────────
        var scene = new Scene(targetMod) { EditorID = "dlg_scene_" + suffix };
        scene.Quest.SetTo(quest.FormKey);
        scene.Flags = (Scene.Flag)0x00001834;
        scene.VNAM  = new byte[] { 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0 };

        scene.Actors.Add(new SceneActor
        {
            ID            = 0,
            BehaviorFlags = (SceneActor.BehaviorFlag)266,
            Flags         = SceneActor.Flag.NoCommandState,
        });
        scene.Actors.Add(new SceneActor
        {
            ID            = unchecked((uint)-2),  // player
            BehaviorFlags = (SceneActor.BehaviorFlag)266,
            Flags         = SceneActor.Flag.NoCommandState,
        });

        scene.Phases.Add(new ScenePhase { Name = "Greeting", EditorWidth = 298 });
        scene.Phases.Add(new ScenePhase { Name = "",          EditorWidth = 350 });

        // Action[1]: NPC greeting in Phase 0
        var greetAction = new DialogueSceneAction { Index = 1, AliasID = 0, StartPhase = 0, EndPhase = 0 };
        greetAction.Topic.SetTo(greetTopic.FormKey);

        // Action[3]: PlayerDialogue choice menu in Phase 1
        var playerAction = new PlayerDialogueSceneAction { Index = 3, AliasID = 0, StartPhase = 1, EndPhase = 1 };
        foreach (var item in dialogueItems)
            playerAction.DialogueList.Add(item);

        scene.Actions = new ExtendedList<ASceneAction> { greetAction, playerAction };
        quest.Scenes.Add(scene);

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

    private static DialogResponses BuildInfo(StarfieldMod targetMod, string text)
    {
        var info = new DialogResponses(targetMod)
        {
            SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low,
        };
        var textHash = SHA256.HashData(Encoding.UTF8.GetBytes(text))[..4];
        var response = new DialogResponse
        {
            ResponseText = text,
            WEMFile      = info.FormKey.ID,  // ⚠ convention — see spec open questions
            TextHash     = textHash,
            EmotionOut   = 7.466667f,
        };
        response.Emotion.SetTo(FormKey.Null);
        info.Responses.Add(response);
        return info;
    }
}
