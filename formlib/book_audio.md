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
var topic = new DialogTopic(targetMod)
{
    EditorID = "speech_topic_" + suffix,
    Category = DialogTopic.CategoryEnum.Scene,
    Subtype  = DialogTopic.SubtypeEnum.CustomScene,  // NOT Custom — confirmed from KT quest
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
info.Responses.Add(new DialogResponse
{
    ResponseText = text,
    // WEMFile = 0 until Wwise media ID is known after audio conversion
});
topic.Responses.Add(info);

// RadioSceneAction — not a StarfieldMajorRecord, just new RadioSceneAction { ... }
var action = new RadioSceneAction
{
    Name = "AudioAction", AliasID = -4, Index = 0, StartPhase = 0, EndPhase = 0,
};
action.Topic.SetTo(topic.FormKey);

// Scene — inline in quest.Scenes
var scene = new Scene(targetMod)
{
    EditorID = "speech_scene_" + suffix,
    Flags = Scene.Flag.BeginOnQuestStart | Scene.Flag.StopOnQuestEnd,
};
scene.Quest.SetTo(quest.FormKey);
scene.Phases.Add(new ScenePhase { Name = "AudioPhase" });
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
| `ResponseText` | `ITranslatedString` | Subtitle shown during playback |
| `EmotionOut` | `float` | Facial animation intensity (0.0 = none; 7.4, 4.8 seen in KT) |
| `RVSH` | `ISoundReferenceGetter?` | Optional Wwise event GUID (start/stop); present on 6/149 KT lines |
| `TROTs` | list | Per-VoiceType emotion overrides (VoiceType + EmotionOut per entry) |

## DialogTopic.SubtypeEnum (relevant values)

`Custom` — player choice topics
`CustomScene` — **scene-driven audio** (use this for audio log DIALs)
`SharedInfo` — shared info group header

## Reference Records (Starfield.esm)

| EditorID | FormKey | Role |
|---|---|---|
| `BE_KT04_AudioLogSlate04A` | `00022B59:Starfield.esm` | Reference audio data-slate Book |
| `BE_KT04_AudioLog_04A` | `022B56:Starfield.esm` | Corresponding Scene |
| `AudioLogsQuest_KT` | `0251D2:Starfield.esm` | Quest hosting 149 Scenes + DialogTopics |
| `DataSlate01_FP` | `0C64CD:Starfield.esm` | Static mesh used as InventoryArt |

## Open Questions

- How does the game resolve `WEMFile` (Wwise media ID) to a `.wem` on disk — via SoundBank, or
  can a loose WEM be referenced if named by a specific convention? (not yet tested in-game)
- Single-phase/single-action scenes (as generated by SpeechTools) have not been tested in-game.
  The vanilla KT quest uses 7 phases / 7 actions (one per audio segment).
