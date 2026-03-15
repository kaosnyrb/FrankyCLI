using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde;
using Retrograde.AI;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

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

    // ── Configurable paths ────────────────────────────────────────────────────
    /// <summary>Staging directory for WAV files and Wwise WEM output.</summary>
    public static string AudioStagingDir    = @"C:\StarfieldAudio\PC";
    /// <summary>Full path to WwiseConsole.exe.</summary>
    public static string WwiseConsolePath   = @"C:\Audiokinetic\Wwise2021.1.14.8108\Authoring\x64\Release\bin\WwiseConsole.exe";
    /// <summary>Wwise Conversion Settings ShareSet name applied to all external sources.</summary>
    public static string WwiseConversionPreset = "Voice Conversion";
    /// <summary>Wwise project file (.wproj) used for conversion.</summary>
    public static string WwiseProjectPath   = @"C:\StarfieldAudio\wwise\Starfield\Starfield.wproj";
    /// <summary>Starfield game voice directory where WEMs are deployed.</summary>
    public static string GameVoiceDir       = @"C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data\Sound\Voice";

    public static bool generateWavs
    {
        get => RetrogradeContext.GenerateWavs;
        set => RetrogradeContext.GenerateWavs = value;
    }

    // Pending WAV generation requests — drained by GenerateAllWavs().
    private record PendingWavRequest(
        string EspWav, string EsmWav, string EspWem, string EsmWem,
        string Text, string ElevenLabsVoiceId, VoiceSettings? VoiceSettings, string WavName);
    private static readonly List<PendingWavRequest> _pendingWavRequests = new();

    // Pending Wwise conversions: (staged WAV path, game WEM destination path)
    private static readonly List<(string WavPath, string GameWemPath)> _pending = new();

    // Set to true after the first ElevenLabs failure — skips the API for all subsequent calls.
    private static bool _useSapiFallback = false;

    /// <summary>
    /// Overload that accepts a raw NPC ID from the target mod.
    /// Speaker FormKey is built from <c>targetMod.ModKey + speakerId</c>.
    /// </summary>
    public static void AddVoice(uint logfileId, uint speakerId, string text, string voiceTypeEditorId = "", string elevenLabsVoiceId = "")
        => AddVoice(logfileId, new FormKey(RetrogradeContext.Current.TargetMod.ModKey, speakerId), text, voiceTypeEditorId, elevenLabsVoiceId);

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
    /// <param name="speakerFormKey">Full FormKey of the NPC speaking the log (may be from any mod, including Starfield.esm).</param>
    /// <param name="text">Transcript text placed in the DialogResponse(s).</param>
    /// <param name="voiceTypeEditorId">EditorID of the NPC's VoiceType — used to build WAV file paths.</param>
    public static void AddVoice(uint logfileId, FormKey speakerFormKey, string text, string voiceTypeEditorId = "", string elevenLabsVoiceId = "")
    {
        var targetMod = RetrogradeContext.Current.TargetMod;

        // 1. Locate Book
        var bookKey = new FormKey(targetMod.ModKey, logfileId);
        var book = targetMod.Books.FirstOrDefault(b => b.FormKey == bookKey)
            ?? throw new KeyNotFoundException($"SpeechTools.AddVoice: Book 0x{logfileId:X6} not found in target mod.");

        // 2. Find-or-create the shared audio log quest
        var audioQuest = GetOrCreateAudioLogQuest(targetMod);

        string suffix = book.EditorID ?? logfileId.ToString("X6");
        // Collapse all newline variants and excess whitespace into single spaces so
        // DialogResponse records and the ElevenLabs API receive clean prose.
        text = Regex.Replace(text.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " "), @"\s{2,}", " ").Trim();
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
            info.Speaker.SetTo(speakerFormKey);
            var textHash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(chunk))[..4];
            var response = new DialogResponse
            {
                ResponseText = chunk,
                WEMFile      = topic.FormKey.ID,
                TextHash     = textHash,
                EmotionOut   = 7.466667f,
            };
            response.Emotion.SetTo(FormKey.None);  // FFFFFFFF — "None Reference"
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
    /// Updates the text for an already-queued WAV request identified by its WEM file ID.
    /// Call this after the polish pass updates a DialogResponses.ResponseText so that
    /// the WAV generated by GenerateAllWavs() matches the improved text.
    /// No-op if the WEM file ID is not found in the pending queue.
    /// </summary>
    public static void UpdateWavText(uint wemFile, string newText)
    {
        if (string.IsNullOrEmpty(newText))
            return;
        string wavName = wemFile.ToString("X8");
        int idx = _pendingWavRequests.FindIndex(r => r.WavName == wavName);
        if (idx >= 0)
            _pendingWavRequests[idx] = _pendingWavRequests[idx] with { Text = newText };
    }

    /// <summary>
    /// Queues one audio segment for WAV generation. Does NOT call ElevenLabs immediately.
    /// Call <see cref="GenerateAllWavs"/> to generate all queued WAVs, then
    /// <see cref="ConvertAndDeploy"/> to convert them to WEM and deploy to the game directory.
    /// </summary>
    public static void GenerateWavs(uint wemFile, string voiceTypeEditorId, ModKey modKey,
        string text = "", string elevenLabsVoiceId = "", VoiceSettings? voiceSettings = null)
    {
        if (string.IsNullOrEmpty(elevenLabsVoiceId) || string.IsNullOrEmpty(text) || string.IsNullOrEmpty(voiceTypeEditorId))
            return;

        if (!generateWavs)
            return;

        string stem    = Path.GetFileNameWithoutExtension(modKey.FileName);
        string wavName = wemFile.ToString("X8");
        string vtDir   = voiceTypeEditorId;

        string espWav = Path.Combine(AudioStagingDir, $"{stem}.esp", vtDir, $"{wavName}.wav");
        string esmWav = Path.Combine(AudioStagingDir, $"{stem}.esm", vtDir, $"{wavName}.wav");
        string espWem = Path.Combine(GameVoiceDir, $"{stem}.esp", vtDir, $"{wavName}.wem");
        string esmWem = Path.Combine(GameVoiceDir, $"{stem}.esm", vtDir, $"{wavName}.wem");

        _pendingWavRequests.Add(new PendingWavRequest(espWav, esmWav, espWem, esmWem, text, elevenLabsVoiceId, voiceSettings, wavName));
    }

    /// <summary>
    /// Generates all queued WAV files by calling ElevenLabs (or SAPI fallback).
    /// Call this once after mod records are built and reviewed, before <see cref="ConvertAndDeploy"/>.
    /// </summary>
    public static void GenerateAllWavs()
    {
        if (_pendingWavRequests.Count == 0)
        {
            Console.WriteLine("[SpeechTools] GenerateAllWavs: nothing queued.");
            return;
        }

        Console.WriteLine($"[SpeechTools] Generating {_pendingWavRequests.Count} WAV(s)...");
        foreach (var req in _pendingWavRequests)
        {
            Console.WriteLine($"[SpeechTools] Staging WAVs for WEM {req.WavName}:");
            Console.WriteLine($"  {req.EspWav}");
            Console.WriteLine($"  {req.EsmWav}");

            if (_useSapiFallback)
            {
                GenerateWavSapi(req.Text, req.EspWav, req.EsmWav, req.EspWem, req.EsmWem, req.WavName);
                continue;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(req.EspWav)!);
                ElevenLabsAPI.GenerateSpeech(req.Text, req.ElevenLabsVoiceId, req.EspWav, voiceSettings: req.VoiceSettings);
                Directory.CreateDirectory(Path.GetDirectoryName(req.EsmWav)!);
                File.Copy(req.EspWav, req.EsmWav, overwrite: true);
                Console.WriteLine($"[SpeechTools] WAV written: {req.WavName}.wav");
                _pending.Add((req.EspWav, req.EspWem));
                _pending.Add((req.EsmWav, req.EsmWem));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SpeechTools] ElevenLabs failed ({ex.Message}), switching to Windows SAPI for this and all remaining WAVs...");
                _useSapiFallback = true;
                GenerateWavSapi(req.Text, req.EspWav, req.EsmWav, req.EspWem, req.EsmWem, req.WavName);
            }
        }

        _pendingWavRequests.Clear();
        Console.WriteLine("[SpeechTools] GenerateAllWavs complete.");
    }

    private static void GenerateWavSapi(string text, string espWav, string esmWav, string espWem, string esmWem, string wavName)
    {
        try
        {
            WindowsSpeechFallback.GenerateWav(text, espWav);
            Directory.CreateDirectory(Path.GetDirectoryName(esmWav)!);
            File.Copy(espWav, esmWav, overwrite: true);
            Console.WriteLine($"[SpeechTools] WAV written (SAPI fallback): {wavName}.wav");
            _pending.Add((espWav, espWem));
            _pending.Add((esmWav, esmWem));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SpeechTools] SAPI fallback also failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts all staged WAV files to WEM using WwiseConsole, then deploys the WEMs
    /// into the Starfield game voice directory.
    ///
    /// Call this once at the end of the mod generation run, after all <c>AddVoice</c> calls.
    /// Does nothing if no WAVs were staged.
    /// </summary>
    public static void ConvertAndDeploy()
    {
        if (_pending.Count == 0)
        {
            Console.WriteLine("[SpeechTools] ConvertAndDeploy: nothing to convert.");
            return;
        }

        Console.WriteLine($"[SpeechTools] Converting {_pending.Count} WAV(s) via WwiseConsole...");

        // Write WSOURCES XML — Wwise requires an ExternalSourcesList XML, not a plain text list
        string tmpFile = Path.ChangeExtension(Path.GetTempFileName(), ".wsources");
        WriteWSourcesXml(tmpFile, _pending.Select(p => p.WavPath));
        Console.WriteLine($"[SpeechTools] Source list: {tmpFile}");

        try
        {
            string args = $"convert-external-source \"{WwiseProjectPath}\" " +
                          $"--platform \"Windows\" " +
                          $"--source-by-platform \"Windows\" \"{tmpFile}\" " +
                          $"--output \"Windows\" \"{AudioStagingDir}\" " +
                          $"--verbose";

            var psi = new ProcessStartInfo(WwiseConsolePath, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start WwiseConsole.exe");

            // Read stdout/stderr asynchronously to avoid deadlock if buffers fill
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            proc.WaitForExit();
            Console.Write(stdoutTask.Result);

            if (proc.ExitCode != 0)
            {
                Console.WriteLine($"[SpeechTools] WwiseConsole exited with code {proc.ExitCode}");
                Console.Error.Write(stderrTask.Result);
                return;
            }

            // Deploy WEMs to game directory
            int deployed = 0;
            foreach (var (wavPath, gameWemPath) in _pending)
            {
                string wemPath = Path.ChangeExtension(wavPath, ".wem");
                if (!File.Exists(wemPath))
                {
                    Console.WriteLine($"[SpeechTools] WEM not found after conversion: {wemPath}");
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(gameWemPath)!);
                File.Copy(wemPath, gameWemPath, overwrite: true);
                string gameWavPath = Path.ChangeExtension(gameWemPath, ".wav");
                File.Copy(wavPath, gameWavPath, overwrite: true);
                Console.WriteLine($"[SpeechTools] Deployed: {gameWemPath}");
                deployed++;
            }

            Console.WriteLine($"[SpeechTools] ConvertAndDeploy complete: {deployed}/{_pending.Count} WEM(s) deployed.");
        }
        finally
        {
            File.Delete(tmpFile);
            _pending.Clear();
        }
    }

    /// <summary>
    /// Writes a Wwise WSOURCES XML file listing WAV files for conversion.
    /// Paths are stored relative to <see cref="AudioStagingDir"/> (the Root attribute),
    /// so Wwise replicates the same directory structure in the output.
    /// </summary>
    private static void WriteWSourcesXml(string path, IEnumerable<string> wavPaths)
    {
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false) };
        using var writer = XmlWriter.Create(path, settings);
        writer.WriteStartDocument();
        writer.WriteStartElement("ExternalSourcesList");
        writer.WriteAttributeString("SchemaVersion", "1");
        writer.WriteAttributeString("Root", AudioStagingDir);
        foreach (var wavPath in wavPaths)
        {
            writer.WriteStartElement("Source");
            writer.WriteAttributeString("Path", Path.GetRelativePath(AudioStagingDir, wavPath));
            writer.WriteAttributeString("Conversion", WwiseConversionPreset);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndDocument();
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
