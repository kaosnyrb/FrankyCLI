using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.AI;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Retrograde.Utils;

public static class SpeechTools
{
    /// <summary>
    /// EditorID of the shared audio-log quest created once per mod run.
    /// Mirrors Bethesda's AudioLogsQuest_KT pattern: one dedicated quest hosts
    /// all Scenes and DialogTopics so they are never mixed into gameplay quests.
    /// </summary>
    private const string AudioLogQuestEditorId = "rg_audiolog_quest";
    private const int MaxResponseLength = 250;

    public static bool generateWavs = true;
    /// <summary>
    /// Creates the record skeleton for an audio data-slate and wires it to an existing Book.
    ///
    /// Long text is split on sentence boundaries into ≤250-character chunks.
    /// Each chunk produces its own ScenePhase, RadioSceneAction, DialogTopic, and DialogResponses
    /// — matching the vanilla KT quest pattern of one phase/action per audio segment.
    ///
    /// WEMFile is set to each DialogTopic's FormKey ID — Starfield uses {topicId:X8}.wem as the filename.
    /// </summary>
    /// <param name="logfileId">Raw FormKey ID of the Book (logfile) to attach voice to.</param>
    /// <param name="speakerId">Raw FormKey ID of the NPC speaking the log (sets INFO.Speaker).</param>
    /// <param name="text">Transcript text placed in the DialogResponse(s).</param>
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
        var chunks = SplitText(text);

        // 3. Create Scene skeleton
        //    Flags=0x80: undocumented flag present on all vanilla AudioLog scenes.
        //    VNAM: 5×uint32(3) — present on all vanilla AudioLog scenes verbatim.
        //    Actors: one entry with ID=-4 (no-actor sentinel), NoCommandState — required.
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
        scene.Actions = new ExtendedList<ASceneAction>();

        // 4. One phase + topic + info + action per chunk (vanilla KT pattern: 7 phases / 7 actions)
        for (int i = 0; i < chunks.Count; i++)
        {
            string chunk = chunks[i];
            string chunkSuffix = $"{suffix}_{i}";

            // Phase
            scene.Phases.Add(new ScenePhase { Name = $"AudioPhase{i}", EditorWidth = 500 });

            // DialogTopic (inline sub-record of audioQuest.DialogTopics)
            //   Category=Scene, Subtype=CustomScene confirmed from AudioLogsQuest_KT in Starfield.esm.
            var topic = new DialogTopic(targetMod)
            {
                EditorID     = "speech_topic_" + chunkSuffix,
                Category     = DialogTopic.CategoryEnum.Scene,
                Subtype      = DialogTopic.SubtypeEnum.CustomScene,
                SubtypeName  = DialogTopic.SubtypeNameEnum.CustomScene,
            };
            topic.Quest.SetTo(audioQuest.FormKey);
            audioQuest.DialogTopics.Add(topic);

            // DialogResponses (inline sub-record of DialogTopic.Responses)
            //   Speaker and SubtitlePriority=Low confirmed from AudioLogsQuest_KT in Starfield.esm.
            //   WEMFile = topic.FormKey.ID: Starfield resolves {topicId:X8}.wem at runtime.
            var info = new DialogResponses(targetMod)
            {
                EditorID = "speech_info_" + chunkSuffix,
                SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low,
            };
            info.Speaker.SetTo(new FormKey(targetMod.ModKey, speakerId));
            var textHash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(chunk))[..4];
            var response = new DialogResponse
            {
                ResponseText = chunk,
                WEMFile      = topic.FormKey.ID,
                TextHash     = textHash,
                EmotionOut   = 7.466667f,
            };
            response.Emotion.SetTo(FormKey.Null);  // None [FFFFFFFF]
            info.Responses.Add(response);
            topic.Responses.Add(info);
            // TPIC cross-reference — missing causes CK crash on click
            topic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                { info.FormKey.ToLink<IDialogResponsesGetter>() };

            // RadioSceneAction — AliasID=-4 is the Bethesda sentinel for "no actor alias" (radio/ambient).
            //   StartPhase/EndPhase = i so this action fires in phase i only.
            var action = new RadioSceneAction
            {
                Name       = "AudioAction",
                AliasID    = -4,
                Index      = (uint)i,
                StartPhase = (uint)i,
                EndPhase   = (uint)i,
            };
            action.Topic.SetTo(topic.FormKey);
            scene.Actions.Add(action);

            GenerateWavs(topic.FormKey.ID, voiceTypeEditorId, targetMod.ModKey, chunk, elevenLabsVoiceId);
        }

        audioQuest.Scenes.Add(scene);

        // 5. Wire the Book: mark as audio data-slate and point its Scene link at the new scene.
        book.DataSlateType = Book.DataSlateTypeEnum.Audio;
        book.Scene.SetTo(scene.FormKey);

        Console.WriteLine($"[SpeechTools] AddVoice: {scene.EditorID} → {audioQuest.EditorID} / {book.EditorID} ({chunks.Count} segment(s))");
    }

    /// <summary>
    /// Splits text into chunks of at most <paramref name="maxLen"/> characters,
    /// breaking on sentence endings (.!?) first to minimise the number of chunks,
    /// then on clause boundaries (,;) or word boundaries for any sentence that is
    /// itself too long.
    /// </summary>
    private static List<string> SplitText(string text, int maxLen = MaxResponseLength)
    {
        if (text.Length <= maxLen)
            return new List<string> { text };

        // Tokenise into sentences at sentence-ending punctuation followed by whitespace
        var sentences = Regex.Split(text.Trim(), @"(?<=[.!?])\s+");
        var chunks = new List<string>();
        var buf = new StringBuilder();

        foreach (var sentence in sentences)
        {
            if (sentence.Length == 0) continue;

            int needed = buf.Length == 0 ? sentence.Length : buf.Length + 1 + sentence.Length;
            if (needed <= maxLen)
            {
                if (buf.Length > 0) buf.Append(' ');
                buf.Append(sentence);
            }
            else
            {
                if (buf.Length > 0) { chunks.Add(buf.ToString()); buf.Clear(); }

                if (sentence.Length <= maxLen)
                {
                    buf.Append(sentence);
                }
                else
                {
                    // Single sentence exceeds maxLen — split on clauses then words
                    foreach (var part in SplitLong(sentence, maxLen))
                    {
                        int pNeeded = buf.Length == 0 ? part.Length : buf.Length + 1 + part.Length;
                        if (pNeeded <= maxLen)
                        {
                            if (buf.Length > 0) buf.Append(' ');
                            buf.Append(part);
                        }
                        else
                        {
                            if (buf.Length > 0) { chunks.Add(buf.ToString()); buf.Clear(); }
                            buf.Append(part);
                        }
                    }
                }
            }
        }

        if (buf.Length > 0) chunks.Add(buf.ToString());
        return chunks;
    }

    /// <summary>
    /// Splits a single oversized string: first tries comma/semicolon clause boundaries,
    /// then falls back to word-wrapping at <paramref name="maxLen"/>.
    /// </summary>
    private static IEnumerable<string> SplitLong(string text, int maxLen)
    {
        // Try clause split — if all parts fit, use them
        var clauses = Regex.Split(text, @"(?<=[,;])\s+");
        if (clauses.All(c => c.Length <= maxLen))
            return clauses;

        // Word-wrap fallback
        var result = new List<string>();
        var buf = new StringBuilder();
        foreach (var word in text.Split(' '))
        {
            if (word.Length == 0) continue;
            int needed = buf.Length == 0 ? word.Length : buf.Length + 1 + word.Length;
            if (needed <= maxLen)
            {
                if (buf.Length > 0) buf.Append(' ');
                buf.Append(word);
            }
            else
            {
                if (buf.Length > 0) { result.Add(buf.ToString()); buf.Clear(); }
                // Single word longer than maxLen — hard truncate as last resort
                buf.Append(word.Length <= maxLen ? word : word[..maxLen]);
            }
        }
        if (buf.Length > 0) result.Add(buf.ToString());
        return result;
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
