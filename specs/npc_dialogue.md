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
  ├─ QuestStages:  [0]   (single startup stage only)
  │
  ├─ Alias[0]:  QuestReferenceAlias (ID=0, Name=NPC, ForcedReference → placed NPC ref)
  │
  └─ Scene "greeting"  (per-NPC)
       │  Flags=0x00001834, VNAM=standard-20-bytes
       │  Actors: [ID=0 NPC] [ID=-2 Player]
       │  Phases: [0 "Greeting"/298] [1 ""/350]
       │
       ├─ Action[0]: DialogueSceneAction  AliasID=0  Phase 0→0
       │    Topic → "npc_greeting" DialogTopic
       │      INFO: ResponseText=NpcGreeting, WEMFile=info.FormKey.ID
       │
       └─ Action[1]: PlayerDialogueSceneAction  AliasID=0  Phase 1→1
            DialogueList[N]:
              Item[i]:
                PlayerChoice → "player_i" DialogTopic
                  INFO: ResponseText=exchange.PlayerPrompt, WEMFile=info.FormKey.ID
                NpcResponse  → "npc_i" DialogTopic
                  INFO: ResponseText=exchange.NpcReply, WEMFile=info.FormKey.ID
                StartScene=null
```

---

## Field Values — confirmed from atbb_mq01

### Quest

| Field | Value |
|-------|-------|
| `Flags` raw | `0x00010111` |
| `Type` | `None` |
| Stage 0 `Flags` | `64` (0x40, StartUpStage) |
| Stage 100 `Flags` | `0` (completion stage — keeps quest running) |
| Alias `Flags` | `0` |
| Alias `UniqueActor` | null — use **ForcedReference** to the placed NPC ref instead |

> **Important:** atbb uses `ForcedReference` (alias ID=0 `AvontechSci` points to a
> specific placed ref, not a base-form). For procedurally placed NPCs, use
> `ForcedReference` to the placed REFR in the cell.

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

```csharp
scene.Conditions.Add(new ConditionFloat
{
    ComparisonValue = 1,
    CompareOperator = CompareOperator.EqualTo,
    Data = new GetIsIDConditionData
    {
        FirstParameter = new FormLinkOrIndex<IPlaceableObjectGetter>(condData, npcFormKey)
    }
});
scene.Conditions.Add(new ConditionFloat
{
    ComparisonValue = 0,
    CompareOperator = CompareOperator.EqualTo,
    Data = new GetStageConditionData
    {
        FirstParameter = new FormLinkOrIndex<IQuestGetter>(condData, quest.FormKey)
    }
});
```

### DialogueSceneAction (NPC greeting, Action[0])

| Field | Value |
|-------|-------|
| `AliasID` | `0` |
| `StartPhase` | `0` |
| `EndPhase` | `0` |
| `Flags` | `0` |
| `DialogueSubtype` | null |

### PlayerDialogueSceneAction (choice menu, Action[1])

| Field | Value |
|-------|-------|
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
| `TPIC` | null |

### DialogResponses (INFO)

| Field | Value |
|-------|-------|
| `Speaker` | null (actor inferred from scene AliasID at runtime) |
| `SubtitlePriority` | `Low` |
| `Prompt` | null |
| `StartScene` | null |
| `SetParentQuestStage` | null |
| Conditions | none |
| `WEMFile` | `info.FormKey.ID` ⚠ — see Open Questions |
| `EmotionOut` | `7.466667` |
| `Emotion` | `SetTo(FormKey.Null)` |
| `TextHash` | SHA256[..4] of ResponseText UTF-8 bytes |

---

## Mutagen Construction — `NPCDialogueNoun`

```csharp
public class NPCDialogueNoun
{
    public Quest QuestRecord { get; }

    public NPCDialogueNoun(
        FormKey        npcRefFormKey,      // placed REFR in cell (ForcedReference)
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
        quest.Stages.Add(new QuestStage { Index = 0, Flags = QuestStage.Flag.StartUpStage });
        quest.Aliases = new ExtendedList<AQuestAlias>();
        targetMod.Quests.Add(quest);

        // ── Alias (ForcedReference → placed NPC REFR) ─────────────────────────
        var alias = new QuestReferenceAlias { ID = 0, Name = "NPC", Flags = 0 };
        alias.ForcedReference.SetTo(npcRefFormKey);
        quest.Aliases.Add(alias);

        // ── Greeting topic (NPC's opening line) ───────────────────────────────
        var greetTopic = BuildSceneTopic(targetMod, quest);
        var greetInfo  = BuildInfo(targetMod, script.NpcGreeting);
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
            var playerTopic = BuildSceneTopic(targetMod, quest);
            var playerInfo  = BuildInfo(targetMod, ex.PlayerPrompt);
            playerTopic.Responses.Add(playerInfo);
            playerTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                { playerInfo.FormKey.ToLink<IDialogResponsesGetter>() };
            quest.DialogTopics.Add(playerTopic);
            // player lines: no SpeechTools (player voice not generated)

            var npcTopic = BuildSceneTopic(targetMod, quest);
            var npcInfo  = BuildInfo(targetMod, ex.NpcReply);
            npcTopic.Responses.Add(npcInfo);
            npcTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                { npcInfo.FormKey.ToLink<IDialogResponsesGetter>() };
            quest.DialogTopics.Add(npcTopic);
            SpeechTools.GenerateWavs(npcInfo.FormKey.ID, voiceTypeEditorId,
                targetMod.ModKey, ex.NpcReply, elevenLabsVoiceId);

            var item = new PlayerDialogueSceneActionItem();
            item.PlayerChoice.SetTo(playerTopic.FormKey);
            item.NpcResponse.SetTo(npcTopic.FormKey);
            // StartScene, PPST, PNST remain null/default
            dialogueItems.Add(item);
        }

        // ── Scene ──────────────────────────────────────────────────────────────
        var scene = new Scene(targetMod) { EditorID = "dlg_scene_" + suffix };
        scene.Quest.SetTo(quest.FormKey);
        scene.Flags = (Scene.Flag)0x00001834;
        scene.VNAM  = new byte[] { 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0, 3,0,0,0 };

        scene.Actors = new ExtendedList<SceneActor>
        {
            new SceneActor { ID = 0,              BehaviorFlags = 266, Flags = SceneActor.Flag.NoCommandState },
            new SceneActor { ID = unchecked((int)-2), BehaviorFlags = 266, Flags = SceneActor.Flag.NoCommandState },
        };

        scene.Phases = new ExtendedList<ScenePhase>
        {
            new ScenePhase { Name = "Greeting", EditorWidth = 298 },
            new ScenePhase { Name = "",          EditorWidth = 350 },
        };

        var greetAction = new DialogueSceneAction
        {
            Index = 1, AliasID = 0, StartPhase = 0, EndPhase = 0, Flags = 0,
        };
        greetAction.Topic.SetTo(greetTopic.FormKey);

        var playerAction = new PlayerDialogueSceneAction
        {
            Index = 3, AliasID = 0, StartPhase = 1, EndPhase = 1, Flags = 0,
            DialogueList = dialogueItems,
        };

        scene.Actions = new ExtendedList<ASceneAction> { greetAction, playerAction };
        quest.Scenes  = new ExtendedList<Scene> { scene };

        QuestRecord = quest;
    }

    private static DialogTopic BuildSceneTopic(StarfieldMod targetMod, Quest quest)
    {
        var topic = new DialogTopic(targetMod)
        {
            EditorID    = "",
            Category    = DialogTopic.CategoryEnum.Scene,
            Subtype     = DialogTopic.SubtypeEnum.CustomScene,
            SubtypeName = DialogTopic.SubtypeNameEnum.CustomScene,
        };
        topic.Quest.SetTo(quest.FormKey);
        return topic;
    }

    private static DialogResponses BuildInfo(StarfieldMod targetMod, string text)
    {
        var info = new DialogResponses(targetMod)
        {
            EditorID         = "",
            SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low,
        };
        var textHash = SHA256.HashData(Encoding.UTF8.GetBytes(text))[..4];
        var response = new DialogResponse
        {
            ResponseText = text,
            WEMFile      = info.FormKey.ID,
            TextHash     = textHash,
            EmotionOut   = 7.466667f,
        };
        response.Emotion.SetTo(FormKey.Null);
        info.Responses.Add(response);
        return info;
    }
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

2. **`Quest.Scenes` vs inline in Quest** — Mutagen may represent scenes as a top-level
   list or as inline sub-records. Verify `quest.Scenes` is the correct property before
   building.

3. **`PlayerDialogueSceneActionItem` construction** — `item.PlayerChoice` and
   `item.NpcResponse` are `IFormLinkNullable<IDialogTopicGetter>`. Confirm they are
   set via `.SetTo()` after construction (not in initializer) per the nullable FormLink rule.

4. **ForcedReference vs UniqueActor** — atbb uses `ForcedReference` (placed REFR).
   If the dialogue quest is created before the NPC is placed, use `UniqueActor` pointing
   to the NPC base form, then verify at runtime. Needs testing.

5. **Player voice** — Player-side topics (`PlayerChoice`) in atbb have WEMFiles set,
   implying player lines are voiced. Whether Starfield expects a WEM for the player side
   or silently ignores it for a silent player character needs verification.
