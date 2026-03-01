# Book / Audio Data-Slate (BOOK)

Covers the `BOOK` record type, with emphasis on audio data-slates (Starfield's in-world voice logs).

## Key Properties

| Property | Mutagen type | Notes |
|---|---|---|
| `DataSlateType` | `Book.DataSlateTypeEnum` | `Audio` for voice logs, default is text |
| `Scene` | `IFormLinkNullable<ISceneGetter>` | Links to the audio scene (see below) |
| `Description` | `ITranslatedStringGetter?` | Transcript text shown in the book UI |
| `DataSlateHeaderLeft` | `ITranslatedStringGetter?` | E.g. "Flight Audio Transcription" |
| `InventoryArt` | `IFormLinkNullable<IStaticGetter>` | `DataSlate01_FP [STAT:0C64CD]` for the slate mesh |

## Audio Data-Slate Record Chain

Confirmed from `BE_KT04_AudioLogSlate04A [BOOK:00022B59]` / `AudioLogsQuest_KT [QUST:0251D2]`:

```
BOOK  (DataSlateType=Audio)
  └─ Scene → SCEN  (inline in Quest.Scenes)
       └─ ScenePhase (one per audio segment)
       └─ RadioSceneAction
            ├─ AliasID = -4          ← Bethesda sentinel: no actor / radio playback
            ├─ StartPhase / EndPhase = 0
            └─ Topic → DIAL  (inline in Quest.DialogTopics, Branch=null)
                 └─ DialogResponses  (inline in DialogTopic.Responses)
                      ├─ Speaker → NPC FormKey  ← required
                      ├─ SubtitlePriority = Low  ← required
                      └─ DialogResponse
                           ├─ ResponseText  ← subtitle transcript
                           └─ WEMFile (uint)  ← Wwise media ID (see below)
```

**DialogBranch:** NOT needed for audio data-slates. `AudioLogsQuest_KT` has zero DialogBranches.
All 149 DialogTopics have `Branch = null`. DialogBranches are only for interactive player-choice dialogue.

All SCEN and DIAL records are **inline sub-records of the parent Quest** — stored in
`Quest.Scenes` and `Quest.DialogTopics` respectively, not as separate top-level groups.
DialogResponses (INFO) are likewise inline in `DialogTopic.Responses`.

## Required DialogResponses Fields (confirmed from KT quest)

| Field | Value | Notes |
|---|---|---|
| `Speaker` | NPC FormKey | Set on every INFO in KT quest; `info.Speaker.SetTo(npcFormKey)` after construction |
| `SubtitlePriority` | `Low` | `DialogResponses.SubtitlePriorityLevel.Low` on all records with lines |
| `Flags` | `0` (typical) | `8192 = AudioOutputOverride` (rare, 3/149 in KT); `256 = PlayerAddress` (InfoGroup header only) |

## WEMFile — Wwise Media ID

`DialogResponse.WEMFile` is a **Wwise internal media ID** (uint32), NOT a filename hash.

- Every INFO with actual audio has a non-zero WEMFile (e.g. `9114284`, `10077997`, `804153`)
- FNV-1a, FNV-1, djb2, CRC32 were tried against the known KT values with many filename formats — none matched
- The ID is assigned by Wwise Authoring Tool when audio is imported into the project
- For custom mods: WEMFile must be set to the ID produced by the WAV→WEM conversion tool
- The game presumably resolves WEMFile to a `.wem` file via the Wwise SoundBank system

## Mutagen Construction Pattern (current / correct)

```csharp
// DialogTopic — inline in quest.DialogTopics; Branch=null (no DialogBranch needed)
// SubtypeName (SNAM) is a SEPARATE field from Subtype (ENAM) — must set both
var topic = new DialogTopic(targetMod)
{
    EditorID    = "speech_topic_" + suffix,
    Category    = DialogTopic.CategoryEnum.Scene,
    Subtype     = DialogTopic.SubtypeEnum.CustomScene,
    SubtypeName = DialogTopic.SubtypeNameEnum.CustomScene,  // SNAM — independent of Subtype
};
topic.Quest.SetTo(quest.FormKey);
quest.DialogTopics.Add(topic);

// DialogResponses — inline in topic.Responses
var info = new DialogResponses(targetMod)
{
    EditorID = "speech_info_" + suffix,
    SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low,
};
info.Speaker.SetTo(npcFormKey);  // required — set after construction
var textHash = System.Security.Cryptography.SHA256.HashData(
    System.Text.Encoding.UTF8.GetBytes(text))[..4];  // NAM9, 4 bytes
info.Responses.Add(new DialogResponse
{
    ResponseText = text,
    WEMFile  = placeholderWemId,   // random uint until Wwise conversion; see WAV path section
    TextHash = textHash,
});
topic.Responses.Add(info);
// TPIC cross-reference — missing causes CK crash on click
topic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
    { info.FormKey.ToLink<IDialogResponsesGetter>() };

// RadioSceneAction
var action = new RadioSceneAction
{
    Name = "AudioAction", AliasID = -4, Index = 0, StartPhase = 0, EndPhase = 0,
};
action.Topic.SetTo(topic.FormKey);

// Scene — inline in quest.Scenes
// Flags=0x80: undocumented bit on ALL vanilla AudioLog scenes (not BeginOnQuestStart/StopOnQuestEnd)
// VNAM: 5×uint32(3) — present verbatim on all vanilla AudioLog scenes
// Actors: one entry with ID=0xFFFFFFFC (-4) and NoCommandState — confirmed from KT and AN quests
var scene = new Scene(targetMod)
{
    EditorID = "speech_scene_" + suffix,
    Flags    = (Scene.Flag)0x80,
    VNAM     = new byte[] { 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0 },
};
scene.Quest.SetTo(quest.FormKey);
scene.Actors.Add(new SceneActor
{
    ID            = unchecked((uint)-4),  // 0xFFFFFFFC — "no actor" sentinel
    Flags         = SceneActor.Flag.NoCommandState,
    BehaviorFlags = 0,
});
scene.Phases.Add(new ScenePhase { Name = "AudioPhase", EditorWidth = 500 });
scene.Actions = new ExtendedList<ASceneAction> { action };  // nullable — must initialise
quest.Scenes.Add(scene);

// Wire book
book.DataSlateType = Book.DataSlateTypeEnum.Audio;
book.Scene.SetTo(scene.FormKey);
```

## ASceneAction Base Fields

| Field | Type | Notes |
|---|---|---|
| `Name` | `string` | Editor label |
| `AliasID` | `int?` | `-4` = no actor (radio/ambient); non-negative = quest alias index |
| `Index` | `uint?` | Action index within the scene |
| `StartPhase` | `uint` | First phase this action applies to |
| `EndPhase` | `uint` | Last phase this action applies to |
| `Flags` | `ASceneAction.Flag?` | Optional behaviour flags |

## Notable DialogResponse Fields (from KT quest)

| Field | Type | Notes |
|---|---|---|
| `WEMFile` | `uint` | Wwise media ID — non-zero when audio exists |
| `TextHash` | `byte[]?` | NAM9 subrecord, 4 bytes — SHA256[..4] of ResponseText UTF-8 bytes is a reasonable placeholder |
| `ResponseText` | `ITranslatedString` | Subtitle shown during playback — **250-character hard limit** |
| `EmotionOut` | `float` | Facial animation intensity (0.0 = none; 7.4, 4.8 seen in KT) |
| `RVSH` | `ISoundReferenceGetter?` | Optional Wwise event GUID (start/stop); present on 6/149 KT lines |
| `TROTs` | list | Per-VoiceType emotion overrides (VoiceType + EmotionOut per entry) |

## DialogTopic.SubtypeEnum (relevant values)

`Custom` — player choice topics
`CustomScene` — **scene-driven audio** (use this for audio log DIALs)
`SharedInfo` — shared info group header

**SubtypeName (SNAM) is a separate field.** Setting `Subtype = CustomScene` does NOT auto-populate `SubtypeName`. Always set both:
```csharp
Subtype     = DialogTopic.SubtypeEnum.CustomScene,
SubtypeName = DialogTopic.SubtypeNameEnum.CustomScene,
```

## Audio Quest Flags (confirmed from all 12 vanilla AudioLog quests)

```
Flags = StartGameEnabled | StartsEnabled | RunOnce | 0x100000 (undocumented)
Type  = None   ← NOT Misc (Misc causes a player-visible journal entry)
Name  = absent ← no FULL subrecord (a Name also causes a journal entry)
```

In Mutagen:
```csharp
var data = new QuestData
{
    Flags = Quest.Flag.StartGameEnabled | Quest.Flag.StartsEnabled | Quest.Flag.RunOnce,
    Type  = Quest.TypeEnum.None,
};
data.Flags = (Quest.Flag)((uint)data.Flags | 0x100000u);
```

`StartsEnabled` is **critical** — without it the quest is dormant and scenes never fire.

## NPC VoiceType

Set via `npc.Voice.SetTo(formKey)` after construction (property name is `Voice`, not `VoiceType`).

## WAV File Path Convention

Starfield looks for voice WAVs (pre-conversion) and WEMs under both `.esp` and `.esm` plugin name variants:

```
Data\Sound\Voice\{plugin.esp}\{VoiceTypeEditorId}\{WEMFile:X8}.wav
Data\Sound\Voice\{plugin.esm}\{VoiceTypeEditorId}\{WEMFile:X8}.wav
```

- Filename is the **WEMFile uint formatted as 8 uppercase hex digits** (e.g. `00B0DB68.wav`)
- Plugin stem is the mod filename without extension; output both `.esp` and `.esm` variants
- `SpeechTools.GenerateWavs(wemFile, voiceTypeEditorId, modKey)` prints these paths

## Reference Records (Starfield.esm)

| EditorID | FormKey | Role |
|---|---|---|
| `BE_KT04_AudioLogSlate04A` | `00022B59:Starfield.esm` | Reference audio data-slate Book |
| `BE_KT04_AudioLog_04A` | `022B56:Starfield.esm` | Corresponding Scene |
| `AudioLogsQuest_KT` | `0251D2:Starfield.esm` | Quest hosting 149 Scenes + DialogTopics |
| `DataSlate01_FP` | `0C64CD:Starfield.esm` | Static mesh used as InventoryArt |

## Multi-segment audio (text > 250 chars)

Split text across multiple segments; each segment gets its own Phase + RadioSceneAction + DialogTopic + DialogResponses.
`WEMFile = topic.FormKey.ID` still holds — each segment's topic has a unique FormKey.
EditorID convention: `speech_topic_{suffix}_{i}`, `speech_info_{suffix}_{i}`, etc.
This matches the vanilla KT pattern (7 phases / 7 actions, one per audio segment).

`SpeechTools.SplitText()` handles splitting: sentence boundaries first, then clause/word fallback.

## Wwise WAV → WEM conversion

WAVs are staged to `C:\StarfieldAudio\PC\{plugin.esp|esm}\{voiceType}\{wemId:X8}.wav`.
`SpeechTools.ConvertAndDeploy()` (call once at end of run) batches all pending WAVs through WwiseConsole.

`--source-by-platform` requires a **WSOURCES XML file** — NOT a plain text list:
```xml
<?xml version="1.0" encoding="utf-8"?>
<ExternalSourcesList SchemaVersion="1" Root="C:\StarfieldAudio\PC">
  <Source Path="outlaws02.esp\GenericMale01\00000AD5.wav" Conversion="Voice Conversion" />
</ExternalSourcesList>
```
- `Root` = `AudioStagingDir`; all `Path` values are relative to it
- `Conversion` = ShareSet name in the Wwise project — Starfield project uses **"Voice Conversion"** ✓ confirmed
- Wwise replicates the relative directory structure in the output: WEM lands at same path as WAV with `.wem` extension ✓ confirmed

After conversion, WEMs are copied from `StarfieldAudio\PC\` to `Starfield\Data\Sound\Voice\` by `ConvertAndDeploy()`.

## Open Questions

- How does the game resolve `WEMFile` (Wwise media ID) to a `.wem` on disk — via SoundBank, or
  can a loose WEM be referenced if named by the `{WEMFile:X8}.wem` convention? (not yet tested in-game)
- `TextHash` exact algorithm unconfirmed — SHA256[..4] is a placeholder. Real value may be CRC32 or similar.
