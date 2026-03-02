using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Models;
using Retrograde.Utils;
using System.Security.Cryptography;
using System.Text;

namespace Retrograde.Nouns;

public class NPCDialogueNoun
{
    public Quest QuestRecord { get; }

    public NPCDialogueNoun(
        FormKey        npcFormKey,
        string         voiceTypeEditorId,
        DialogueScript script,
        string         suffix,
        string         elevenLabsVoiceId = "")
    {
        var targetMod = RetrogradeContext.Current.TargetMod;

        // ── Quest ──────────────────────────────────────────────────────────────
        var data = new QuestData
        {
            Flags = Quest.Flag.StartGameEnabled | Quest.Flag.StartsEnabled
                  | Quest.Flag.RunOnce | (Quest.Flag)0x10000,
            Type  = Quest.TypeEnum.None,
        };
        var quest = new Quest(targetMod) { EditorID = "dlg_quest_" + suffix, Data = data };

        // Stage i*100 for each dialogue stage, plus a terminal stage at StageCount*100.
        // The terminal stage is referenced by last-stage explore conditions (GetStageDone==0)
        // so xEdit doesn't warn about a missing stage index.
        for (int i = 0; i <= script.StageCount; i++)
            quest.Stages.Add(new QuestStage { Index = (ushort)(i * 100) });

        quest.Aliases = new ExtendedList<AQuestAlias>();
        targetMod.Quests.Add(quest);

        // ── Alias ──────────────────────────────────────────────────────────────
        var alias = new QuestReferenceAlias
            { ID = 0, Name = "DialogNPC", Flags = AQuestAlias.Flag.AllowDisabled };
        alias.UniqueActor.SetTo(npcFormKey);
        quest.Aliases.Add(alias);

        // ── DialogBranch ───────────────────────────────────────────────────────
        var branch = new DialogBranch(targetMod)
        {
            EditorID = "dlg_branch_" + suffix,
            Category = DialogBranch.CategoryType.Player,
            Flags    = DialogBranch.Flag.TopLevel,
        };
        branch.Quest.SetTo(quest.FormKey);
        quest.DialogBranches.Add(branch);

        // ── Greeting topic (INFOs ordered latest-stage first) ──────────────────
        var greetTopic = new DialogTopic(targetMod)
        {
            EditorID    = "dlg_greeting_" + suffix,
            Category    = DialogTopic.CategoryEnum.Player,
            Subtype     = DialogTopic.SubtypeEnum.Greeting,
            SubtypeName = DialogTopic.SubtypeNameEnum.Greeting,
        };
        greetTopic.Quest.SetTo(quest.FormKey);
        greetTopic.Branch.SetTo(branch.FormKey);
        quest.DialogTopics.Add(greetTopic);
        branch.StartingTopic.SetTo(greetTopic.FormKey);

        var greetInfoLinks = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>();

        for (int i = script.StageCount - 1; i >= 0; i--)
        {
            int stageValue = i * 100;

            var greetInfo = new DialogResponses(targetMod)
            {
                EditorID         = $"dlgi_greet_{suffix}_{i}",
                SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low,
            };
            greetInfo.Speaker.SetTo(npcFormKey);

            // Stage 0: no condition — fires as fallback on first visit
            if (i > 0)
                greetInfo.Conditions.Add(BuildStageDoneCondition(quest, stageValue, equalTo: 1));

            var textHash = SHA256.HashData(Encoding.UTF8.GetBytes(script.Stages[i].NpcLine))[..4];
            var greetResponse = new DialogResponse
            {
                ResponseText = script.Stages[i].NpcLine,
                WEMFile      = greetInfo.FormKey.ID,
                TextHash     = textHash,
                EmotionOut   = 7.466667f,
            };
            greetResponse.Emotion.SetTo(FormKey.Null);
            greetInfo.Responses.Add(greetResponse);

            greetTopic.Responses.Add(greetInfo);
            greetInfoLinks.Add(greetInfo.FormKey.ToLink<IDialogResponsesGetter>());

            SpeechTools.GenerateWavs(greetInfo.FormKey.ID, voiceTypeEditorId,
                targetMod.ModKey, script.Stages[i].NpcLine, elevenLabsVoiceId);
        }
        greetTopic.TopicInfoList = greetInfoLinks;

        // ── Per-stage progress + explore topics ────────────────────────────────
        for (int i = 0; i < script.StageCount; i++)
        {
            var stage      = script.Stages[i];
            int nextStage  = (i + 1) * 100;
            var hideAfterAdvance = BuildStageDoneCondition(quest, nextStage, equalTo: 0);

            // Progress topic — stage advance only, no NPC voice
            if (stage.ProgressPrompt != null)
            {
                var progTopic = BuildMenuTopic(targetMod, quest,
                    $"dlg_progress_{suffix}_{i}", stage.ProgressPrompt);

                var progInfo = new DialogResponses(targetMod)
                {
                    EditorID            = $"dlgi_progress_{suffix}_{i}",
                    SubtitlePriority    = DialogResponses.SubtitlePriorityLevel.Low,
                    Prompt              = stage.ProgressPrompt,
                    SetParentQuestStage = new DialogSetParentQuestStage
                        { OnBegin = -1, OnEnd = (short)nextStage },
                };
                progInfo.Speaker.SetTo(npcFormKey);
                progInfo.Conditions.Add(hideAfterAdvance);
                progTopic.Responses.Add(progInfo);
                progTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                    { progInfo.FormKey.ToLink<IDialogResponsesGetter>() };
                quest.DialogTopics.Add(progTopic);
            }

            // Explore topics — NPC reply in ResponseText
            for (int j = 0; j < stage.Explores.Count; j++)
            {
                var ex      = stage.Explores[j];
                var exTopic = BuildMenuTopic(targetMod, quest,
                    $"dlg_explore_{suffix}_{i}_{j}", ex.PlayerPrompt);

                var exInfo = new DialogResponses(targetMod)
                {
                    EditorID         = $"dlgi_explore_{suffix}_{i}_{j}",
                    SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low,
                    Prompt           = ex.PlayerPrompt,
                };
                exInfo.Speaker.SetTo(npcFormKey);
                exInfo.Conditions.Add(hideAfterAdvance);

                var exHash = SHA256.HashData(Encoding.UTF8.GetBytes(ex.NpcReply))[..4];
                var exResponse = new DialogResponse
                {
                    ResponseText = ex.NpcReply,
                    WEMFile      = exInfo.FormKey.ID,
                    TextHash     = exHash,
                    EmotionOut   = 7.466667f,
                };
                exResponse.Emotion.SetTo(FormKey.Null);
                exInfo.Responses.Add(exResponse);
                exTopic.Responses.Add(exInfo);
                exTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                    { exInfo.FormKey.ToLink<IDialogResponsesGetter>() };
                quest.DialogTopics.Add(exTopic);

                SpeechTools.GenerateWavs(exInfo.FormKey.ID, voiceTypeEditorId,
                    targetMod.ModKey, ex.NpcReply, elevenLabsVoiceId);
            }
        }

        // ── Goodbye ────────────────────────────────────────────────────────────
        var goodbyeTopic = new DialogTopic(targetMod)
        {
            EditorID    = "dlg_goodbye_" + suffix,
            Category    = DialogTopic.CategoryEnum.Player,
            Subtype     = DialogTopic.SubtypeEnum.Goodbye,
            SubtypeName = DialogTopic.SubtypeNameEnum.Goodbye,
        };
        goodbyeTopic.Quest.SetTo(quest.FormKey);
        quest.DialogTopics.Add(goodbyeTopic);

        var goodbyeInfo = new DialogResponses(targetMod)
        {
            EditorID         = "dlgi_goodbye_" + suffix,
            SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low,
        };
        goodbyeInfo.Speaker.SetTo(npcFormKey);
        var byeHash = SHA256.HashData(Encoding.UTF8.GetBytes(script.Goodbye))[..4];
        var byeResponse = new DialogResponse
        {
            ResponseText = script.Goodbye,
            WEMFile      = goodbyeInfo.FormKey.ID,
            TextHash     = byeHash,
            EmotionOut   = 7.466667f,
        };
        byeResponse.Emotion.SetTo(FormKey.Null);
        goodbyeInfo.Responses.Add(byeResponse);
        goodbyeTopic.Responses.Add(goodbyeInfo);
        goodbyeTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
            { goodbyeInfo.FormKey.ToLink<IDialogResponsesGetter>() };
        SpeechTools.GenerateWavs(goodbyeInfo.FormKey.ID, voiceTypeEditorId,
            targetMod.ModKey, script.Goodbye, elevenLabsVoiceId);

        QuestRecord = quest;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static DialogTopic BuildMenuTopic(
        StarfieldMod targetMod, Quest quest, string editorId, string playerText)
    {
        var topic = new DialogTopic(targetMod)
        {
            EditorID    = editorId,
            Name        = playerText,
            Category    = DialogTopic.CategoryEnum.Player,
            Subtype     = DialogTopic.SubtypeEnum.Custom,
            SubtypeName = DialogTopic.SubtypeNameEnum.Custom,
        };
        topic.Quest.SetTo(quest.FormKey);
        return topic;
    }

    /// <summary>
    /// GetStageDoneConditionData(quest, stageValue) EqualTo [0 or 1].
    /// equalTo=1 → "stage N has been reached" (used on Greeting INFOs to select the right stage).
    /// equalTo=0 → "stage N has NOT been reached" (used on Progress+Explore to hide after advance).
    /// Verified in CREW_EliteCrew_OtherPlayer and UC02.
    /// </summary>
    private static ConditionFloat BuildStageDoneCondition(Quest quest, int stageValue, int equalTo)
    {
        var condData = new GetStageDoneConditionData { SecondParameter = stageValue };
        condData.FirstParameter = new FormLinkOrIndex<IQuestGetter>(condData, quest.FormKey);

        return new ConditionFloat
        {
            ComparisonValue = equalTo,
            CompareOperator = CompareOperator.EqualTo,
            Data            = condData,
        };
    }
}
