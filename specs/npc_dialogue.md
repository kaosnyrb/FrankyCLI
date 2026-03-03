# Spec: NPC Conversational Dialogue (Scene-Based)

> **Source of truth:** `atbb_mq01` "Avontech Blacksite" [QUST:0008F6] in
> `avontechblacksiteblueprints.esm`. All field values below are extracted from that quest.
> The previous spec (DialogBranch + Player-category topics) is **superseded and incorrect**.

---

## Overview

Each generated NPC gets a dedicated quest containing one interactive greeting scene. When the
player activates the NPC, the scene starts: the NPC speaks a greeting, then the player is
presented with a voiced choice menu. Each choice plays the player's question, then the NPC's
voiced response. No DialogBranch, no Player-category topics.

```
Player activates NPC
  → Greeting scene starts
      Phase 0: NPC speaks greeting line
      Phase 1: Player sees choice menu (N items)
                ├─ [Choice 0]  player asks question → NPC answers
                ├─ [Choice 1]  player asks question → NPC answers
                └─ [Choice N]  player asks question → NPC answers
```

---

## Data Model

```csharp
// Retrograde.Library/Models/DialogueScript.cs
public class DialogueScript
{
    public string NpcGreeting { get; set; } = "";
    public List<DialogueExchange> Exchanges { get; set; } = new();
}

public class DialogueExchange
{
    /// <summary>Player's voiced question (≤60 chars, shown in choice menu).</summary>
    public string PlayerPrompt { get; set; } = "";

    /// <summary>NPC's voiced reply (≤200 chars).</summary>
    public string NpcReply { get; set; } = "";
}
```

---

## Record Chain

```
Quest  (per-NPC, Flags=0x00010111, Type=None)
  │
  ├─ QuestStages:  [0, 100]
  │
  ├─ Alias[0]:  QuestReferenceAlias (ID=0, Name=NPC, UniqueActor → NPC base-form)
  │
  └─ Scene "greeting"  (per-NPC)
       │  Flags=0x00001834, VNAM=standard-20-bytes
       │  Conditions: GetIsID(npc)==1, GetStage(quest)==0
       │  Actors: [ID=0 NPC] [ID=-2 Player]
       │  Phases: [0 "Greeting"/298] [1 ""/350]
       │
       ├─ Action[1]: DialogueSceneAction  AliasID=0  Phase 0→0
       │    Topic → greeting DialogTopic
       │      INFO: Speaker=npcFormKey, ResponseText=NpcGreeting, WEMFile=info.FormKey.ID
       │
       └─ Action[3]: PlayerDialogueSceneAction  AliasID=0  Phase 1→1
            DialogueList[N]:
              Item[i]:
                PlayerChoice → player_i DialogTopic
                  INFO: Speaker=null, ResponseText=exchange.PlayerPrompt, WEMFile=info.FormKey.ID
                NpcResponse  → npc_i DialogTopic
                  INFO: Speaker=npcFormKey, ResponseText=exchange.NpcReply, WEMFile=info.FormKey.ID
                StartScene=null
```

---

## Field Values — confirmed from atbb_mq01 + in-game testing

### Quest

| Field | Value |
|-------|-------|
| `Flags` raw | `0x00010111` |
| `Type` | `None` |
| Stage 0 `Flags` | `0` |
| Stage 100 `Flags` | `0` (completion stage — keeps quest running) |
| Alias `Flags` | `0` |
| Alias `UniqueActor` | NPC base-form FormKey |

### Scene

| Field | Value |
|-------|-------|
| `Flags` raw | `0x00001834` |
| `VNAM` | `03 00 00 00 03 00 00 00 03 00 00 00 03 00 00 00 03 00 00 00` (20 bytes) |
| Actor ID=0 `BehaviorFlags` | `266` |
| Actor ID=0 `Flags` | `NoCommandState` |
| Actor ID=-2 `BehaviorFlags` | `266` |
| Actor ID=-2 `Flags` | `NoCommandState` |
| Phase[0] Name | `"Greeting"` |
| Phase[0] EditorWidth | `298` |
| Phase[1] Name | `""` |
| Phase[1] EditorWidth | `350` |

**Scene Conditions (required — every interactive atbb_mq01 scene has exactly these two):**

| # | Function | Parameter | Operator | Value |
|---|----------|-----------|----------|-------|
| 0 | `GetIsID` | `npcFormKey` | `EqualTo` | `1` |
| 1 | `GetStage` | `quest` | `EqualTo` | `0` |

Omitting these triggers the CK warning "Current Greeting or Top Level scene has no conditions."

### DialogueSceneAction (NPC greeting, Action[1])

| Field | Value |
|-------|-------|
| `Index` | `1` |
| `AliasID` | `0` |
| `StartPhase` | `0` |
| `EndPhase` | `0` |
| `Flags` | `0` |

### PlayerDialogueSceneAction (choice menu, Action[3])

| Field | Value |
|-------|-------|
| `Index` | `3` |
| `AliasID` | `0` |
| `StartPhase` | `1` |
| `EndPhase` | `1` |
| `Flags` | `0` |
| Item `StartScene` | null |
| Item `PhaseIndex` | null |
| Item `PAPN` | `""` |
| Item `PPST` / `PNST` | null |

### DialogTopic (all topics in this quest)

| Field | Value |
|-------|-------|
| `Category` | `Scene` |
| `Subtype` | `CustomScene` |
| `SubtypeName` | `CustomScene` |
| `EditorID` | `""` (blank) |
| `Name` | `""` (blank) |
| `Branch` | null |
| `TPIC` | populated (missing causes CK crash on click) |

### DialogResponses (INFO)

| Field | NPC lines | Player lines |
|-------|-----------|--------------|
| `Speaker` | `npcFormKey` | null (inferred at runtime) |
| `SubtitlePriority` | `Low` | `Low` |
| `Prompt` | null | null |
| `StartScene` | null | null |
| `SetParentQuestStage` | null | null |
| Conditions | none | none |
| `WEMFile` | `info.FormKey.ID` ⚠ — see Open Questions | `info.FormKey.ID` |
| `EmotionOut` | `7.466667` | `7.466667` |
| `Emotion` | `FormKey.None` → `FFFFFFFF` | `FormKey.None` → `FFFFFFFF` |
| `TextHash` | SHA256[..4] of ResponseText UTF-8 bytes | SHA256[..4] |

> **Mutagen gotcha — `FormKey.None` vs `FormKey.Null`:**
> `FormKey.Null` (ID=0) serializes as `0x00000000`.
> `FormKey.None` (ID=0xFFFFFF, ModKey.Null) serializes as `0xFFFFFFFF`.
> Bethesda's "None Reference" sentinel is `0xFFFFFFFF`. Always use `FormKey.None` for
> fields that should show "None Reference [FFFFFFFF]" in xEdit.

---

## Mutagen Construction — `NPCDialogueNoun`

```csharp
public NPCDialogueNoun(
    FormKey        npcFormKey,        // base-form FormKey of the NPC (UniqueActor alias)
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
    quest.Stages.Add(new QuestStage { Index = 100 });
    quest.Aliases = new ExtendedList<AQuestAlias>();
    targetMod.Quests.Add(quest);

    // ── Alias ──────────────────────────────────────────────────────────────
    var alias = new QuestReferenceAlias { ID = 0, Name = "NPC" };
    alias.UniqueActor.SetTo(npcFormKey);
    quest.Aliases.Add(alias);

    // ── Greeting topic (NPC's opening line) ───────────────────────────────
    var greetTopic = BuildSceneTopic(targetMod, quest);
    var greetInfo  = BuildInfo(targetMod, script.NpcGreeting, npcFormKey);
    greetTopic.Responses.Add(greetInfo);
    greetTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
        { greetInfo.FormKey.ToLink<IDialogResponsesGetter>() };
    quest.DialogTopics.Add(greetTopic);
    SpeechTools.GenerateWavs(greetInfo.FormKey.ID, voiceTypeEditorId,
        targetMod.ModKey, script.NpcGreeting, elevenLabsVoiceId);

    // ── PlayerChoice + NpcResponse topic pairs ────────────────────────────
    var dialogueItems = new ExtendedList<PlayerDialogueSceneActionItem>();
    foreach (var ex in script.Exchanges)
    {
        // Player prompts: no Speaker, no SpeechTools
        var playerTopic = BuildSceneTopic(targetMod, quest);
        var playerInfo  = BuildInfo(targetMod, ex.PlayerPrompt);
        playerTopic.Responses.Add(playerInfo);
        playerTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
            { playerInfo.FormKey.ToLink<IDialogResponsesGetter>() };
        quest.DialogTopics.Add(playerTopic);

        // NPC replies: Speaker=npcFormKey
        var npcTopic = BuildSceneTopic(targetMod, quest);
        var npcInfo  = BuildInfo(targetMod, ex.NpcReply, npcFormKey);
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

    scene.Conditions.Add(BuildGetIsIDCondition(npcFormKey));
    scene.Conditions.Add(BuildGetStageCondition(quest, 0, CompareOperator.EqualTo));

    scene.Actors.Add(new SceneActor { ID = 0, BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
    scene.Actors.Add(new SceneActor { ID = unchecked((uint)-2), BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });

    scene.Phases.Add(new ScenePhase { Name = "Greeting", EditorWidth = 298 });
    scene.Phases.Add(new ScenePhase { Name = "",          EditorWidth = 350 });

    var greetAction = new DialogueSceneAction { Index = 1, AliasID = 0, StartPhase = 0, EndPhase = 0 };
    greetAction.Topic.SetTo(greetTopic.FormKey);

    var playerAction = new PlayerDialogueSceneAction { Index = 3, AliasID = 0, StartPhase = 1, EndPhase = 1 };
    foreach (var item in dialogueItems)
        playerAction.DialogueList.Add(item);

    scene.Actions = new ExtendedList<ASceneAction> { greetAction, playerAction };
    quest.Scenes.Add(scene);

    QuestRecord = quest;
}

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

private static DialogResponses BuildInfo(StarfieldMod targetMod, string text, FormKey speakerFormKey = default)
{
    var info = new DialogResponses(targetMod) { SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low };
    if (speakerFormKey != default)
        info.Speaker.SetTo(speakerFormKey);
    var textHash = SHA256.HashData(Encoding.UTF8.GetBytes(text))[..4];
    var response = new DialogResponse
    {
        ResponseText = text,
        WEMFile      = info.FormKey.ID,
        TextHash     = textHash,
        EmotionOut   = 7.466667f,
    };
    response.Emotion.SetTo(FormKey.None);  // FFFFFFFF — "None Reference"
    info.Responses.Add(response);
    return info;
}
```

---

## AI Generation — `PromptManager.GetDialogueScript`

```csharp
public static DialogueScript GetDialogueScript(
    string       npcDescription,
    List<string> topics,
    List<string> addons)
{
    // Build prompt: N exchanges, one per topic.
    // Each exchange: PlayerPrompt (≤60 chars) + NpcReply (≤200 chars).
    // NpcGreeting: 1 sentence under 150 chars, guarded and contextual.
    // Output as XML with <Greeting>, <Exchange><PlayerPrompt><NpcReply> tags.
}
```

---

## Open Questions — Require In-Game Testing

1. **WEMFile for Scene-category INFOs** — Using `info.FormKey.ID` as the WEM identifier.
   In `atbb_mq01`, WEMFiles are large Wwise-assigned IDs with no FormKey correlation.
   Whether Starfield resolves `{infoId:X8}.wem` from the voice directory for Scene topics
   needs in-game verification.

2. **Player voice** — Player-side topics (`PlayerChoice`) in atbb have WEMFiles set,
   implying player lines are voiced. Whether Starfield expects a WEM for the player side
   or silently ignores it for a silent player character needs verification.
