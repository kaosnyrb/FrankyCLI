using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.AI;
using System.IO;
using System.Linq;

namespace Retrograde.Utils;

public static class SpeechTools
{
    /// <summary>
    /// EditorID of the shared audio-log quest created once per mod run.
    /// Mirrors Bethesda's AudioLogsQuest_KT pattern: one dedicated quest hosts
    /// all Scenes and DialogTopics so they are never mixed into gameplay quests.
    /// </summary>
    private const string AudioLogQuestEditorId = "rg_audiolog_quest";

    public static bool generateWavs = false;
    /// <summary>
    /// Creates the record skeleton for an audio data-slate and wires it to an existing Book.
    ///
    /// Record chain: BOOK.Scene → SCEN → RadioSceneAction → DIAL → INFO (with ResponseText).
    /// The Scene and DialogTopic are added to a dedicated shared audio-log Quest
    /// (created once per mod run, EditorID = "rg_audiolog_quest"), NOT to the gameplay quest.
    ///
    /// WEMFile is left at 0 — it must be set after WAV→WEM conversion using the Wwise media ID
    /// produced by the authoring tool. The expected voice file path is logged to console.
    /// </summary>
    /// <param name="logfileId">Raw FormKey ID of the Book (logfile) to attach voice to.</param>
    /// <param name="speakerId">Raw FormKey ID of the NPC speaking the log (sets INFO.Speaker).</param>
    /// <param name="text">Transcript text placed in the DialogResponse.</param>
    /// <param name="voiceTypeEditorId">EditorID of the NPC's VoiceType — used only to log the expected WEM file path.</param>
    public static void AddVoice(uint logfileId, uint speakerId, string text, string voiceTypeEditorId = "", string elevenLabsVoiceId = "")
    {
        var targetMod = RetrogradeContext.Current.TargetMod;

        // 1. Locate Book
        var bookKey = new FormKey(targetMod.ModKey, logfileId);
        var book = targetMod.Books.FirstOrDefault(b => b.FormKey == bookKey)
            ?? throw new KeyNotFoundException($"SpeechTools.AddVoice: Book 0x{logfileId:X6} not found in target mod.");

        // 2. Find-or-create the shared audio log quest
        var audioQuest = GetOrCreateAudioLogQuest(targetMod);

        string suffix = book.EditorID ?? logfileId.ToString("X6");

        // 3. Create DialogTopic (inline sub-record of audioQuest.DialogTopics)
        //    Category=Scene, Subtype=CustomScene confirmed from AudioLogsQuest_KT in Starfield.esm.
        var topic = new DialogTopic(targetMod)
        {
            EditorID     = "speech_topic_" + suffix,
            Category     = DialogTopic.CategoryEnum.Scene,
            Subtype      = DialogTopic.SubtypeEnum.CustomScene,
            SubtypeName  = DialogTopic.SubtypeNameEnum.CustomScene,
        };
        topic.Quest.SetTo(audioQuest.FormKey);
        audioQuest.DialogTopics.Add(topic);

        // 4. Create DialogResponses (inline sub-record of DialogTopic.Responses)
        //    Speaker and SubtitlePriority=Low confirmed from AudioLogsQuest_KT in Starfield.esm.
        //    WEMFile: Wwise media ID — NOT a filename hash. Must be set after WAV→WEM conversion.
        var info = new DialogResponses(targetMod)
        {
            EditorID = "speech_info_" + suffix,
            SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low,
        };
        info.Speaker.SetTo(new FormKey(targetMod.ModKey, speakerId));
        var textHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))[..4];
        info.Responses.Add(new DialogResponse
        {
            ResponseText = text,
            WEMFile   = (uint)Random.Shared.Next(500),
            TextHash  = textHash,
        });
        topic.Responses.Add(info);
        // Populate the TPIC cross-reference list so the CK can locate the INFO without
        // traversing the full DIAL GRUP. Missing TPIC causes a CK crash on click.
        topic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>> { info.FormKey.ToLink<IDialogResponsesGetter>() };

        // 5. Create one ScenePhase and a RadioSceneAction pointing at the topic.
        //    AliasID = -4 is the Bethesda sentinel for "no actor alias" (radio/ambient playback).
        var phase = new ScenePhase { Name = "AudioPhase", EditorWidth = 500 };
        var action = new RadioSceneAction
        {
            Name = "AudioAction",
            AliasID = -4,
            Index = 0,
            StartPhase = 0,
            EndPhase = 0,
        };
        action.Topic.SetTo(topic.FormKey);

        // 6. Create Scene (inline sub-record of audioQuest.Scenes)
        //    Flags=0x80: undocumented flag present on all vanilla AudioLog scenes (not BeginOnQuestStart/StopOnQuestEnd).
        //    VNAM: 5×uint32(3) — present on all vanilla AudioLog scenes verbatim.
        //    Actors: one entry with ID=-4 (no-actor sentinel), NoCommandState — required by vanilla AudioLog scenes.
        var scene = new Scene(targetMod)
        {
            EditorID = "speech_scene_" + suffix,
            Flags = (Scene.Flag)0x80,
            VNAM = new byte[] { 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0 },
        };
        scene.Quest.SetTo(audioQuest.FormKey);
        scene.Actors.Add(new SceneActor
        {
            ID            = unchecked((uint)-4),   // 0xFFFFFFFC — Bethesda "no actor" sentinel
            Flags         = SceneActor.Flag.NoCommandState,
            BehaviorFlags = 0,
        });
        scene.Phases.Add(phase);
        scene.Actions = new ExtendedList<ASceneAction> { action };
        audioQuest.Scenes.Add(scene);

        // 7. Wire the Book: mark as audio data-slate and point its Scene link at the new scene.
        book.DataSlateType = Book.DataSlateTypeEnum.Audio;
        book.Scene.SetTo(scene.FormKey);

        Console.WriteLine($"[SpeechTools] AddVoice: {scene.EditorID} → {audioQuest.EditorID} / {book.EditorID}");
        GenerateWavs(info.Responses[0].WEMFile, voiceTypeEditorId, targetMod.ModKey, text, elevenLabsVoiceId);
    }

    /// <summary>
    /// Logs the expected WAV file paths and, when an ElevenLabs voice ID is supplied,
    /// calls the TTS API to generate the WAV file and writes it to both plugin variants.
    /// Starfield looks for voice files under both .esp and .esm plugin name variants.
    /// Path format: Data\Sound\Voice\{plugin}\{voiceType}\{wemFile:X8}.wav
    /// </summary>
    public static void GenerateWavs(uint wemFile, string voiceTypeEditorId, ModKey modKey,
        string text = "", string elevenLabsVoiceId = "")
    {
        const string base_path = @"C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data\Sound\Voice";
        string stem    = Path.GetFileNameWithoutExtension(modKey.FileName);
        string wavName = wemFile.ToString("X8");
        string vtDir   = string.IsNullOrEmpty(voiceTypeEditorId) ? "<npc_voicetype>" : voiceTypeEditorId;

        string espPath = $@"{base_path}\{stem}.esp\{vtDir}\{wavName}.wav";
        string esmPath = $@"{base_path}\{stem}.esm\{vtDir}\{wavName}.wav";

        Console.WriteLine($"[SpeechTools] WAV paths for WEM {wavName}:");
        Console.WriteLine($"  {espPath}");
        Console.WriteLine($"  {esmPath}");

        if (string.IsNullOrEmpty(elevenLabsVoiceId) || string.IsNullOrEmpty(text) || string.IsNullOrEmpty(voiceTypeEditorId))
            return;

        if (generateWavs)
        {
            try
            {
                ElevenLabsAPI.GenerateSpeech(text, elevenLabsVoiceId, espPath);
                File.Copy(espPath, esmPath, overwrite: true);
                Console.WriteLine($"[SpeechTools] WAV written: {wavName}.wav");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SpeechTools] WAV generation failed: {ex.Message}");
            }            
        }
    }

    /// <summary>
    /// Returns the shared audio-log quest, creating it if it doesn't exist yet in the target mod.
    /// </summary>
    private static Quest GetOrCreateAudioLogQuest(StarfieldMod targetMod)
    {
        var existing = targetMod.Quests.FirstOrDefault(q => q.EditorID == AudioLogQuestEditorId);
        if (existing != null)
            return existing;

        var questData = new QuestData
        {
            Flags    = Quest.Flag.StartGameEnabled | Quest.Flag.StartsEnabled | Quest.Flag.RunOnce,
            Priority = 0,
            Type     = Quest.TypeEnum.None,
        };
        // 0x100000 is an undocumented flag present on all 12 vanilla AudioLog quests in Starfield.esm
        questData.Flags = (Quest.Flag)((uint)questData.Flags | 0x100000u);

        var quest = new Quest(targetMod)
        {
            EditorID = AudioLogQuestEditorId,
            // No Name — prevents a player-visible journal entry
            Data = questData,
        };
        targetMod.Quests.Add(quest);
        Console.WriteLine($"[SpeechTools] Created audio log quest: {AudioLogQuestEditorId} ({quest.FormKey})");
        return quest;
    }
}
