# Spec: NPC Conversational Dialogue (Scene-Based)

> **Source of truth:** `atbb_mq01` "Avontech Blacksite" [QUST:0008F6] in
> `avontechblacksiteblueprints.esm`. All field values below are extracted from that quest.
> The previous spec (DialogBranch + Player-category topics) is **superseded and incorrect**.

---

## Overview

Each generated NPC gets a dedicated quest containing 1 Greeting scene and N Topic scenes (one per
exchange). All scenes share the same two conditions so the engine matches them to the NPC.

The player's Phase 0 line in each Topic scene is what the engine shows as the selectable option
in the conversation menu. Exchange[0] is the **Completion Topic** — when it ends, the conversation
closes. Exchanges[1..N-1] are **Regular Topics** — they loop back to show the menu again via
`TopLevelTopicsOnEnd`. No `PlayerDialogueSceneAction` / `DialogueList`. No DialogBranch.

```
Player activates NPC
  → Greeting Scene (flags=0x1834) plays once
      Phase 0: NPC speaks greeting line
  → Engine presents all TopLevel Topic scenes as a choice menu
      ├─ Completion Topic (flags=0x2810)  ← Exchange[0], ends conversation
      │    Phase 0: player asks → Phase 1: NPC answers → conversation ends
      ├─ Regular Topic[1] (flags=0x2814)  ← Exchange[1], loops
      │    Phase 0: player asks → Phase 1: NPC answers → menu reappears
      └─ Regular Topic[N-1] (flags=0x2814)
           Phase 0: player asks → Phase 1: NPC answers → menu reappears
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
  ├─ Greeting Scene  flags=0x1834  (plays once on NPC activation)
  │    Conditions: GetIsID(npc)==1, GetStage(quest)==0
  │    Actors: [ID=0 NPC] [ID=-2 Player]
  │    Phase[0] "Greeting"/298
  │    Action[1]: DialogueSceneAction  AliasID=0  Phase 0→0
  │      Topic → NPC greeting  (Speaker=npcFormKey, WEMFile=info.FormKey.ID)
  │
  ├─ Completion Topic  flags=0x2810  (Exchange[0] — conversation ends when done)
  │    Conditions: GetIsID(npc)==1, GetStage(quest)==0
  │    Actors: [ID=0 NPC] [ID=-2 Player]
  │    Phase[0] ""/350
  │    Action[3]: DialogueSceneAction  AliasID=-2  Phase 0→0   ← text shown as menu option
  │      Topic → player_0  (Speaker=null, WEMFile=0)
  │    Phase[1] ""/350
  │    Action[4]: DialogueSceneAction  AliasID=0   Phase 1→1
  │      Topic → npc_0  (Speaker=npcFormKey, WEMFile=info.FormKey.ID)
  │
  └─ Regular Topic[i]  flags=0x2814  (Exchange[1..N-1] — loops via TopLevelTopicsOnEnd)
       Same layout as Completion Topic but flags=0x2814 (adds 0x0004 TopLevelTopicsOnEnd)
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

### Scene — shared fields (all scenes)

| Field | Value |
|-------|-------|
| `VNAM` | `03 00 00 00 03 00 00 00 03 00 00 00 03 00 00 00 03 00 00 00` (20 bytes) |
| Actor ID=0 `BehaviorFlags` | `266` |
| Actor ID=0 `Flags` | `NoCommandState` |
| Actor ID=-2 `BehaviorFlags` | `266` |
| Actor ID=-2 `Flags` | `NoCommandState` |

**Scene Conditions (required — all scenes have exactly these two):**

| # | Function | Parameter | Operator | Value |
|---|----------|-----------|----------|-------|
| 0 | `GetIsID` | `npcFormKey` | `EqualTo` | `1` |
| 1 | `GetStage` | `quest` | `EqualTo` | `0` |

Omitting these triggers the CK warning "Current Greeting or Top Level scene has no conditions."

### Greeting Scene (flags=0x1834 — plays once on NPC activation)

| Field | Value |
|-------|-------|
| `Flags` raw | `0x00001834` |
| Phase[0] Name | `"Greeting"` |
| Phase[0] EditorWidth | `298` |

### DialogueSceneAction (in Greeting Scene, Action[1] — NPC line)

| Field | Value |
|-------|-------|
| `Index` | `1` |
| `AliasID` | `0` (NPC) |
| `StartPhase` | `0` |
| `EndPhase` | `0` |
| `Flags` | `0` |

### Completion Topic (Exchange[0], flags=0x2810 — ends conversation)

`0x2810` = Top Level visible + DisableDialogueCamera + Interruptable. No `0x0004` (TopLevelTopicsOnEnd).
Confirmed from `City_NewAtlantis_Z_PartingGift_TL_HaddieQuest [SCEN:000D53FB]`.

| Field | Value |
|-------|-------|
| `Flags` raw | `0x00002810` |
| Phase[0] Name | `""` |
| Phase[0] EditorWidth | `350` |
| Phase[1] Name | `""` |
| Phase[1] EditorWidth | `350` |

### Regular Topic (Exchange[1..N-1], flags=0x2814 — loops after reply)

`0x2814` = `0x2810` + `0x0004` (TopLevelTopicsOnEnd) — after Phase 1 ends, engine shows all TopLevel topic scenes again.

**Scene flag breakdown:**

| Bit | Value | Meaning |
|-----|-------|---------|
| `0x2000` | Top Level | Scene appears as a selectable option in the conversation menu |
| `0x0800` | DisableDialogueCamera | — |
| `0x0010` | Interruptable | — |
| `0x0004` | TopLevelTopicsOnEnd | Conversation menu reappears after the scene ends (regular only) |

| Field | Value |
|-------|-------|
| `Flags` raw | `0x00002814` |
| Phase[0] Name | `""` |
| Phase[0] EditorWidth | `350` |
| Phase[1] Name | `""` |
| Phase[1] EditorWidth | `350` |

### DialogueSceneAction per Topic Scene — Action[3] player line + Action[4] NPC line

Both action types are `DialogueSceneAction`. The player's Phase 0 text is shown as the selectable
option in the conversation menu.

| Field | Action[3] — player | Action[4] — NPC |
|-------|-------------------|-----------------|
| `Index` | `3` | `4` |
| `AliasID` | `-2` (Player) | `0` (NPC) |
| `StartPhase` | `0` | `1` |
| `EndPhase` | `0` | `1` |
| `Flags` | `0` | `0` |

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
| `WEMFile` | `info.FormKey.ID` ⚠ — see Open Questions | `0` (player is silent) |
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

    // ── NPC greeting topic (Greeting Scene Phase 0) ───────────────────────
    var greetTopic = BuildSceneTopic(targetMod, quest);
    var greetInfo  = BuildInfo(targetMod, script.NpcGreeting, npcFormKey);
    greetTopic.Responses.Add(greetInfo);
    greetTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
        { greetInfo.FormKey.ToLink<IDialogResponsesGetter>() };
    quest.DialogTopics.Add(greetTopic);
    SpeechTools.GenerateWavs(greetInfo.FormKey.ID, voiceTypeEditorId,
        targetMod.ModKey, script.NpcGreeting, elevenLabsVoiceId);

    // ── Greeting Scene (flags=0x1834) — plays once on NPC activation ──────
    var greetScene = new Scene(targetMod) { EditorID = "dlg_scene_" + suffix + "_greeting" };
    greetScene.Quest.SetTo(quest.FormKey);
    greetScene.Flags = (Scene.Flag)0x00001834;
    greetScene.VNAM  = new byte[] { 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0 };
    greetScene.Conditions.Add(BuildGetIsIDCondition(npcFormKey));
    greetScene.Conditions.Add(BuildGetStageCondition(quest, 0, CompareOperator.EqualTo));
    greetScene.Actors.Add(new SceneActor { ID = 0,                   BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
    greetScene.Actors.Add(new SceneActor { ID = unchecked((uint)-2), BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
    greetScene.Phases.Add(new ScenePhase { Name = "Greeting", EditorWidth = 298 });
    var greetAction = new DialogueSceneAction { Index = 1, AliasID = 0, StartPhase = 0, EndPhase = 0 };
    greetAction.Topic.SetTo(greetTopic.FormKey);
    greetScene.Actions = new ExtendedList<ASceneAction> { greetAction };
    quest.Scenes.Add(greetScene);

    // ── Topic Scenes — one per exchange ───────────────────────────────────
    // Exchange[0] = completion topic (flags=0x2810 — Top Level, ends conversation)
    // Exchange[1..N-1] = regular topics (flags=0x2814 — Top Level + TopLevelTopicsOnEnd, loops)
    //
    // Phase 0: player line (DialogueSceneAction AliasID=-2, Index=3) — shown as menu option
    // Phase 1: NPC reply  (DialogueSceneAction AliasID=0,  Index=4)
    for (int i = 0; i < script.Exchanges.Count; i++)
    {
        var ex = script.Exchanges[i];

        var playerTopic = BuildSceneTopic(targetMod, quest);
        var playerInfo  = BuildInfo(targetMod, ex.PlayerPrompt);
        playerTopic.Responses.Add(playerInfo);
        playerTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
            { playerInfo.FormKey.ToLink<IDialogResponsesGetter>() };
        quest.DialogTopics.Add(playerTopic);

        var npcTopic = BuildSceneTopic(targetMod, quest);
        var npcInfo  = BuildInfo(targetMod, ex.NpcReply, npcFormKey);
        npcTopic.Responses.Add(npcInfo);
        npcTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
            { npcInfo.FormKey.ToLink<IDialogResponsesGetter>() };
        quest.DialogTopics.Add(npcTopic);
        SpeechTools.GenerateWavs(npcInfo.FormKey.ID, voiceTypeEditorId,
            targetMod.ModKey, ex.NpcReply, elevenLabsVoiceId);

        uint topicFlags = i == 0 ? 0x00002810u : 0x00002814u;
        var topicScene = new Scene(targetMod) { EditorID = "dlg_scene_" + suffix + "_topic_" + i };
        topicScene.Quest.SetTo(quest.FormKey);
        topicScene.Flags = (Scene.Flag)topicFlags;
        topicScene.VNAM  = new byte[] { 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0 };
        topicScene.Conditions.Add(BuildGetIsIDCondition(npcFormKey));
        topicScene.Conditions.Add(BuildGetStageCondition(quest, 0, CompareOperator.EqualTo));
        topicScene.Actors.Add(new SceneActor { ID = 0,                   BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
        topicScene.Actors.Add(new SceneActor { ID = unchecked((uint)-2), BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
        topicScene.Phases.Add(new ScenePhase { Name = "", EditorWidth = 350 });
        topicScene.Phases.Add(new ScenePhase { Name = "", EditorWidth = 350 });
        var playerAction = new DialogueSceneAction { Index = 3, AliasID = -2, StartPhase = 0, EndPhase = 0 };
        playerAction.Topic.SetTo(playerTopic.FormKey);
        var npcAction = new DialogueSceneAction { Index = 4, AliasID = 0, StartPhase = 1, EndPhase = 1 };
        npcAction.Topic.SetTo(npcTopic.FormKey);
        topicScene.Actions = new ExtendedList<ASceneAction> { playerAction, npcAction };
        quest.Scenes.Add(topicScene);
    }

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
        // Player lines are silent — WEMFile=0. NPC lines use info.FormKey.ID.
        WEMFile      = speakerFormKey != default ? info.FormKey.ID : 0u,
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

2. ~~**Player voice**~~ — **Confirmed:** Starfield player lines are always silent. No WEM
   is generated or expected for `PlayerChoice` topics. `WEMFile = 0` for player INFOs.
