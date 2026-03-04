using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System.Security.Cryptography;
using System.Text;

namespace Retrograde.Nouns;

/// <summary>
/// Shared static helpers for building Starfield dialogue records.
/// Extracted from NPCDialogueNoun so BranchingDialogueNoun can reuse them.
/// </summary>
public static class DialogueHelpers
{
    public static DialogTopic BuildSceneTopic(StarfieldMod targetMod, Quest quest)
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

    /// <param name="speakerFormKey">Pass NPC FormKey for NPC lines; omit for silent player lines.</param>
    public static DialogResponses BuildInfo(StarfieldMod targetMod, string text, FormKey speakerFormKey = default)
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
            WEMFile      = speakerFormKey != default ? info.FormKey.ID : 0u,
            TextHash     = textHash,
            EmotionOut   = 7.466667f,
        };
        response.Emotion.SetTo(FormKey.None);
        info.Responses.Add(response);
        return info;
    }

    /// <summary>GetIsID(npcFormKey) EqualTo 1 — identifies the NPC that activates this scene.</summary>
    public static ConditionFloat BuildGetIsIDCondition(FormKey npcFormKey)
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

    /// <summary>GetStage(quest) [op] comparisonValue — gates a scene to a quest stage.</summary>
    public static ConditionFloat BuildGetStageCondition(Quest quest, int comparisonValue, CompareOperator op)
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
