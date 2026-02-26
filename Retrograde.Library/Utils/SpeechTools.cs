using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Linq;

namespace Retrograde.Utils;

public static class SpeechTools
{
    /// <summary>
    /// Creates the record skeleton for an audio data-slate and wires it to an existing Book.
    ///
    /// Record chain: BOOK.Scene → SCEN → RadioSceneAction → DIAL → INFO (with ResponseText).
    /// Both Scene and DialogTopic are added as inline sub-records of the supplied Quest.
    /// No WEM audio file is generated; the chain is a skeleton ready for future voice synthesis.
    /// </summary>
    /// <param name="logfileId">Raw FormKey ID of the Book (logfile) to attach voice to.</param>
    /// <param name="questId">Raw FormKey ID of the Quest that will own the Scene and DialogTopic.</param>
    /// <param name="text">Transcript text placed in the DialogResponse.</param>
    public static void AddVoice(uint logfileId, uint questId, string text)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;

        // 1. Locate Book
        var bookKey = new FormKey(targetMod.ModKey, logfileId);
        var book = targetMod.Books.FirstOrDefault(b => b.FormKey == bookKey)
            ?? throw new KeyNotFoundException($"SpeechTools.AddVoice: Book 0x{logfileId:X6} not found in target mod.");

        // 2. Locate Quest
        var questKey = new FormKey(targetMod.ModKey, questId);
        var quest = targetMod.Quests.FirstOrDefault(q => q.FormKey == questKey)
            ?? throw new KeyNotFoundException($"SpeechTools.AddVoice: Quest 0x{questId:X6} not found in target mod.");

        string suffix = book.EditorID ?? logfileId.ToString("X6");

        // 3. Create DialogTopic (inline sub-record of Quest.DialogTopics)
        //    Category=Scene, Subtype=Custom matches Bethesda's radio-audio-log pattern.
        var topic = new DialogTopic(targetMod)
        {
            EditorID = "speech_topic_" + suffix,
            Category = DialogTopic.CategoryEnum.Scene,
            Subtype = DialogTopic.SubtypeEnum.Custom,
        };
        topic.Quest.SetTo(quest.FormKey);
        quest.DialogTopics.Add(topic);

        // 4. Create DialogResponses (inline sub-record of DialogTopic.Responses)
        var info = new DialogResponses(targetMod)
        {
            EditorID = "speech_info_" + suffix,
        };
        info.Responses.Add(new DialogResponse
        {
            ResponseText = text,
        });
        topic.Responses.Add(info);

        // 5. Create one ScenePhase and a RadioSceneAction pointing at the topic.
        //    AliasID = -4 is the Bethesda sentinel for "no actor alias" (radio/ambient playback).
        var phase = new ScenePhase { Name = "AudioPhase" };
        var action = new RadioSceneAction
        {
            Name = "AudioAction",
            AliasID = -4,
            Index = 0,
            StartPhase = 0,
            EndPhase = 0,
        };
        action.Topic.SetTo(topic.FormKey);

        // 6. Create Scene (inline sub-record of Quest.Scenes)
        var scene = new Scene(targetMod)
        {
            EditorID = "speech_scene_" + suffix,
            Flags = Scene.Flag.BeginOnQuestStart | Scene.Flag.StopOnQuestEnd,
        };
        scene.Quest.SetTo(quest.FormKey);
        scene.Phases.Add(phase);
        scene.Actions = new ExtendedList<ASceneAction> { action };
        quest.Scenes.Add(scene);

        // 7. Wire the Book: mark as audio data-slate and point its Scene link at the new scene.
        book.DataSlateType = Book.DataSlateTypeEnum.Audio;
        book.Scene.SetTo(scene.FormKey);

        Console.WriteLine($"[SpeechTools] AddVoice: {scene.EditorID} → {quest.EditorID} / {book.EditorID}");
    }
}
