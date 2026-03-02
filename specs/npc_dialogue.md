# Spec: NPC Conversational Dialogue

## Overview

A generic system for generating voiced NPC dialogue — the player activates an NPC and gets a
dialogue menu with voiced responses. Reusable across any generated content.

**Per-NPC quests** — each generated NPC gets its own dedicated dialogue quest.

Distinct from the **audio data-slate** system (`SpeechTools.AddVoice`) which plays a one-sided
radio log with no player interaction.

---

## Architecture

Player choices come from a **DialogBranch** menu (player picks PROGRESS / EXPLORE / GOODBYE).
NPC voiced responses are delivered via **`ResponseText` directly on the INFO record** — the same
pattern used by `UC02_PerrysMeatsSupportQuest` (2B180A:Starfield.esm), a vanilla story quest
with a DialogBranch. No separate Scene records are needed for voice delivery in interactive
dialogue menus.

`StartScene` is used in Starfield for cutscene/animation sequences, not for voiced NPC responses
in player-menu dialogue. Audio slates (`SpeechTools.AddVoice`) use Scenes because the Book
record triggers playback through `book.Scene`; that mechanism does not apply here.

```
Player activates NPC
    → Greeting INFO fires (stage-gated condition, ResponseText = NPC's intro line)
    → Player sees dialogue menu:
        [PROGRESS] pick to advance stage  (no NPC voice, stage advance only)
        [EXPLORE]  pick to hear a reply   (ResponseText = NPC's reply)
        [GOODBYE]  farewell               (ResponseText = farewell line)
```

---

## Vanilla Reference

| Quest | Key facts |
|-------|-----------|
| `UC02_PerrysMeatsSupportQuest` (2B180A) | Flags `0x00010111`, DialogBranch (Player/TopLevel), Player-category topics with `ResponseText` directly on INFOs, `StartScene: null` throughout |
| `CREW_EliteCrew_OtherPlayer` (187431) | `GetStageDoneConditionData`, `SetParentQuestStage: OnBegin=-1, OnEnd=N`, Greeting INFOs with stage conditions |
| `UC02` (2B1808) | `StartScene` + `ResponseText` + `SetParentQuestStage` can all coexist on one INFO; double-condition pattern `GetStageDone(A)==1 AND GetStageDone(B)==0` confirmed |
| `FFNeonZ11` (2E8298) | Scene-only quest (no DialogBranch) — **not** the pattern for interactive dialogue menus |

---

## Dialogue Pattern — Staged Conversation

```
Stage 0 (intro)
  NPC: "You shouldn't be here. Move along."
  ├─ [PROGRESS]  "What's going on at this facility?"   → advances to Stage 100 (no NPC voice)
  ├─ [EXPLORE]   "Who are you?"                        → NPC: "Name's Rook. Guard duty."
  └─ [EXPLORE]   "Is it safe here?"                    → NPC: "Safe enough. Move along."

Stage 100 (topic: reactor incident)
  NPC: "Reactor went offline three days ago. Running on backup power."
  ├─ [PROGRESS]  "What happened to the workers?"       → advances to Stage 200 (no NPC voice)
  ├─ [EXPLORE]   "How bad is the damage?"              → NPC: "Bad. Very bad."
  └─ [EXPLORE]   "Did you report this?"                → NPC: "To who? Everyone's gone."

Stage 200 (last — no progress choice)
  NPC: "The workers didn't leave. They were taken. All twelve of them."
  ├─ [EXPLORE]   "Who took them?"                      → NPC: "I don't know. I saw lights."
  └─ [EXPLORE]   "Did anyone escape?"                  → NPC: "One. She's hiding."

[Goodbye]  "Watch yourself out there."  (always available, no stage condition)
```

`StageCount` = `topics.Count + 1`. Stage values: 0, 100, 200, … (100-increment).

---

## Data Model — `DialogueScript`

```csharp
// Retrograde.Library/Models/DialogueScript.cs
namespace Retrograde.Models;

public class DialogueScript
{
    public int StageCount => Stages.Count;
    public List<DialogueStage> Stages { get; set; } = new();
    public string Goodbye { get; set; } = "";
}

public class DialogueStage
{
    /// <summary>NPC's spoken greeting at this stage (ResponseText on the Greeting INFO).</summary>
    public string NpcLine { get; set; } = "";

    /// <summary>Player menu text for the advance choice. Null on the last stage.</summary>
    public string? ProgressPrompt { get; set; }

    public List<DialogueExchange> Explores { get; set; } = new();
}

public class DialogueExchange
{
    /// <summary>Player's menu text (≤60 chars).</summary>
    public string PlayerPrompt { get; set; } = "";

    /// <summary>NPC's voiced reply (≤200 chars) in ResponseText.</summary>
    public string NpcReply { get; set; } = "";
}
```

---

## AI Generation — `PromptManager.GetDialogueScript`

Single LLM call. Draws on the running LoreContext (same session as quest/NPC generation).

```csharp
public static DialogueScript GetDialogueScript(
    string       npcDescription,
    List<string> topics,
    List<string> addons)
{
    var sb = new StringBuilder();
    sb.AppendLine("Generate a staged NPC conversation for a Starfield-style bounty hunting game.");
    sb.AppendLine();
    sb.AppendLine("NPC: " + npcDescription);
    sb.AppendLine("Conversation topics (in order): " + string.Join(", ", topics));
    sb.AppendLine();
    sb.AppendLine("Use the LoreContext established earlier in this conversation for tone, setting, and names.");
    sb.AppendLine("Do NOT invent new names or factions beyond the LoreContext.");
    sb.AppendLine();
    sb.AppendLine("Rules:");
    sb.AppendLine("- One intro stage, then one stage per topic listed.");
    sb.AppendLine("- Each stage: NpcLine (under 200 chars), ProgressPrompt (under 60 chars, omit on last stage),");
    sb.AppendLine("  exactly 2 Explores (PlayerPrompt under 60 chars, NpcReply under 200 chars).");
    sb.AppendLine("- Goodbye: 1 sentence, under 100 chars. Tone: guarded and grounded.");
    sb.AppendLine("- No stage directions or quotes.");
    sb.AppendLine();
    sb.AppendLine("Additional Information:");
    foreach (var a in addons) sb.AppendLine(a);
    sb.AppendLine();
    int stageCount = topics.Count + 1;
    sb.AppendLine("< Dialogue >");
    for (int i = 0; i < stageCount; i++)
    {
        sb.AppendLine("    < Stage >");
        sb.AppendLine("        < NpcLine >TEXT</ NpcLine >");
        if (i < stageCount - 1) sb.AppendLine("        < ProgressPrompt >TEXT</ ProgressPrompt >");
        sb.AppendLine("        < Explore >< PlayerPrompt >TEXT</ PlayerPrompt >< NpcReply >TEXT</ NpcReply ></ Explore >");
        sb.AppendLine("        < Explore >< PlayerPrompt >TEXT</ PlayerPrompt >< NpcReply >TEXT</ NpcReply ></ Explore >");
        sb.AppendLine("    </ Stage >");
    }
    sb.AppendLine("    < Goodbye >TEXT</ Goodbye >");
    sb.AppendLine("</ Dialogue >");

    string raw = AITools.RunPrompt(sb.ToString());
    for (int i = 0; i < 5 && !raw.Contains("<NpcLine>", StringComparison.OrdinalIgnoreCase); i++)
        raw = AITools.RunPrompt(sb.ToString());

    return ParseDialogueScript(raw);
}
```

---

## Record Chain

```
Quest  (per-NPC, StartGameEnabled | StartsEnabled | RunOnce | 0x10000, Type=None)
  │
  ├─ QuestStages:  [0, 100, …, StageCount*100]   ← includes a terminal stage beyond the last dialogue stage
  │
  ├─ Alias[0]:  QuestReferenceAlias (ID=0, Flags=AllowDisabled, UniqueActor → NPC base form)
  │
  ├─ DialogBranch  (inline, Category=Player, Flags=TopLevel)
  │    └─ StartingTopic → Greeting topic
  │
  ├─ DialogTopic "Greeting"  (Category=Player, Subtype=Greeting)
  │    INFOs ordered latest-stage first (engine picks first whose conditions pass):
  │    ├─ INFO[last]  cond: GetStageDone(quest,200)==1   ResponseText=stages[2].NpcLine
  │    ├─ INFO[mid]   cond: GetStageDone(quest,100)==1   ResponseText=stages[1].NpcLine
  │    └─ INFO[0]     (no condition)                     ResponseText=stages[0].NpcLine
  │         each INFO: Speaker=npcFormKey, SubtitlePriority=Low, WEMFile=info.FormKey.ID
  │
  ├─ DialogTopic "Progress_N"  (Category=Player, Subtype=Custom, Name=progressPrompt)
  │    └─ INFO  cond: GetStageDone(quest,N+1)==0
  │         SetParentQuestStage: OnBegin=-1, OnEnd=N+1
  │         ResponseText="" / no WEM  (stage advance only, no NPC voice)
  │
  ├─ DialogTopic "Explore_N_J"  (Category=Player, Subtype=Custom, Name=playerPrompt)
  │    └─ INFO  cond: GetStageDone(quest,N+1)==0
  │         Speaker=npcFormKey, SubtitlePriority=Low
  │         Prompt=playerPrompt, ResponseText=NpcReply, WEMFile=info.FormKey.ID
  │
  └─ DialogTopic "Goodbye"  (Category=Player, Subtype=Goodbye)
       └─ INFO  (no condition)
            Speaker=npcFormKey, SubtitlePriority=Low
            ResponseText=script.Goodbye, WEMFile=info.FormKey.ID
```

**Stage gating** — progress and explore INFOs:
```
GetStageDoneConditionData(quest, nextStage) == 0   [hide once stage has advanced past N]
```

Greeting INFOs ordered **latest-stage first**; intro INFO has no condition (fires as fallback).

**Stage advance** — Progress INFOs only (no NPC voice):
```
SetParentQuestStage.OnBegin = -1
SetParentQuestStage.OnEnd   = nextStage
```

**WEMFile** — `info.FormKey.ID` (unique per INFO). WAV staged at
`{plugin}/{voiceType}/{infoId:X8}.wav`, matching how `GenerateWavs` resolves files.
⚠ WEMFile resolution for Player-category INFOs is unverified in-game — vanilla uses
Wwise-assigned IDs with no FormKey correlation. This convention needs testing.

---

## Quest Flags

Confirmed in `UC02_PerrysMeatsSupportQuest` (2B180A) — a story sub-quest with DialogBranch:

| Flag | Value | Notes |
|------|-------|-------|
| `StartGameEnabled` | `0x0001` | Quest active at game start |
| `StartsEnabled` | `0x0010` | Fires immediately |
| `RunOnce` | `0x0100` | Stage progression permanent |
| *(unknown)* | `0x10000` | On every vanilla quest with aliases |
| ~~`HasDialogueData`~~ | ~~`0x8000`~~ | **Not used** — absent from all vanilla dialogue quests |

Raw: `0x00010111`.

---

## Mutagen Construction — `NPCDialogueNoun`

> **Verified by `gen_dlgtest` + xEdit** — record chain is clean. Gotchas below are confirmed
> runtime fixes required beyond what the vanilla reference suggested.

### Construction gotchas

| Gotcha | Correct pattern |
|--------|----------------|
| `Quest.Aliases` is **null** on a fresh record | `quest.Aliases = new ExtendedList<AQuestAlias>()` before `.Add()` |
| `condData.FirstParameter.SetTo(...)` fails type inference (CS0411) | `condData.FirstParameter = new FormLinkOrIndex<IQuestGetter>(condData, quest.FormKey)` |
| `DialogResponse.Emotion` must be explicit null | `response.Emotion.SetTo(FormKey.Null)` — omitting it causes xEdit TRDA warning |
| Last-stage explore conditions reference a non-existent stage | Add terminal stage at `StageCount * 100` so `GetStageDone(quest, terminal) == 0` is valid |

```csharp
// Retrograde.Library/Nouns/NPCDialogueNoun.cs

public class NPCDialogueNoun
{
    public Quest QuestRecord { get; }

    public NPCDialogueNoun(
        FormKey        npcFormKey,
        string         voiceTypeEditorId,
        DialogueScript script,
        string         suffix,
        string         elevenLabsVoiceId = "")
    {
        var targetMod = RetrogradeContext.Current.TargetMod;

        // ── Quest ──────────────────────────────────────────────────────────────
        var data = new QuestData
        {
            Flags = Quest.Flag.StartGameEnabled | Quest.Flag.StartsEnabled
                  | Quest.Flag.RunOnce | (Quest.Flag)0x10000,
            Type  = Quest.TypeEnum.None,
        };
        var quest = new Quest(targetMod) { EditorID = "dlg_quest_" + suffix, Data = data };

        // i <= StageCount: includes a terminal stage at StageCount*100 so last-stage
        // explore conditions (GetStageDone == 0) reference a valid stage index.
        for (int i = 0; i <= script.StageCount; i++)
            quest.Stages.Add(new QuestStage { Index = (ushort)(i * 100) });

        // Quest.Aliases is null on fresh construction — must initialize before .Add().
        quest.Aliases = new ExtendedList<AQuestAlias>();
        targetMod.Quests.Add(quest);

        // ── Alias ──────────────────────────────────────────────────────────────
        var alias = new QuestReferenceAlias
            { ID = 0, Name = "DialogNPC", Flags = AQuestAlias.Flag.AllowDisabled };
        alias.UniqueActor.SetTo(npcFormKey);
        quest.Aliases.Add(alias);

        // ── DialogBranch ───────────────────────────────────────────────────────
        var branch = new DialogBranch(targetMod)
        {
            EditorID = "dlg_branch_" + suffix,
            Category = DialogBranch.CategoryType.Player,
            Flags    = DialogBranch.Flag.TopLevel,
        };
        branch.Quest.SetTo(quest.FormKey);
        quest.DialogBranches.Add(branch);

        // ── Greeting topic (INFOs ordered latest-stage first) ──────────────────
        var greetTopic = new DialogTopic(targetMod)
        {
            EditorID    = "dlg_greeting_" + suffix,
            Category    = DialogTopic.CategoryEnum.Player,
            Subtype     = DialogTopic.SubtypeEnum.Greeting,
            SubtypeName = DialogTopic.SubtypeNameEnum.Greeting,
        };
        greetTopic.Quest.SetTo(quest.FormKey);
        greetTopic.Branch.SetTo(branch.FormKey);
        quest.DialogTopics.Add(greetTopic);
        branch.StartingTopic.SetTo(greetTopic.FormKey);

        var greetInfoLinks = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>();

        for (int i = script.StageCount - 1; i >= 0; i--)
        {
            int stageValue = i * 100;

            var greetInfo = new DialogResponses(targetMod)
            {
                EditorID         = $"dlgi_greet_{suffix}_{i}",
                SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low,
            };
            greetInfo.Speaker.SetTo(npcFormKey);

            // Stage 0: no condition — fires as fallback on first visit
            if (i > 0)
                greetInfo.Conditions.Add(BuildStageDoneCondition(quest, stageValue, equalTo: 1));

            var textHash = SHA256.HashData(Encoding.UTF8.GetBytes(script.Stages[i].NpcLine))[..4];
            var greetResponse = new DialogResponse
            {
                ResponseText = script.Stages[i].NpcLine,
                WEMFile      = greetInfo.FormKey.ID,   // ⚠ convention unverified in-game
                TextHash     = textHash,
                EmotionOut   = 7.466667f,
            };
            greetResponse.Emotion.SetTo(FormKey.Null); // required — omitting causes xEdit TRDA warning
            greetInfo.Responses.Add(greetResponse);

            greetTopic.Responses.Add(greetInfo);
            greetInfoLinks.Add(greetInfo.FormKey.ToLink<IDialogResponsesGetter>());

            SpeechTools.GenerateWavs(greetInfo.FormKey.ID, voiceTypeEditorId,
                targetMod.ModKey, script.Stages[i].NpcLine, elevenLabsVoiceId);
        }
        greetTopic.TopicInfoList = greetInfoLinks;

        // ── Per-stage progress + explore topics ────────────────────────────────
        for (int i = 0; i < script.StageCount; i++)
        {
            var stage      = script.Stages[i];
            int nextStage  = (i + 1) * 100;
            var hideAfterAdvance = BuildStageDoneCondition(quest, nextStage, equalTo: 0);

            // Progress topic — stage advance only, no NPC voice
            if (stage.ProgressPrompt != null)
            {
                var progTopic = BuildMenuTopic(targetMod, quest,
                    $"dlg_progress_{suffix}_{i}", stage.ProgressPrompt);

                var progInfo = new DialogResponses(targetMod)
                {
                    EditorID            = $"dlgi_progress_{suffix}_{i}",
                    SubtitlePriority    = DialogResponses.SubtitlePriorityLevel.Low,
                    Prompt              = stage.ProgressPrompt,
                    SetParentQuestStage = new DialogSetParentQuestStage
                        { OnBegin = -1, OnEnd = (short)nextStage },
                };
                progInfo.Speaker.SetTo(npcFormKey);
                progInfo.Conditions.Add(hideAfterAdvance);
                progTopic.Responses.Add(progInfo);
                progTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                    { progInfo.FormKey.ToLink<IDialogResponsesGetter>() };
                quest.DialogTopics.Add(progTopic);
            }

            // Explore topics — NPC reply in ResponseText
            for (int j = 0; j < stage.Explores.Count; j++)
            {
                var ex      = stage.Explores[j];
                var exTopic = BuildMenuTopic(targetMod, quest,
                    $"dlg_explore_{suffix}_{i}_{j}", ex.PlayerPrompt);

                var exInfo = new DialogResponses(targetMod)
                {
                    EditorID         = $"dlgi_explore_{suffix}_{i}_{j}",
                    SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low,
                    Prompt           = ex.PlayerPrompt,
                };
                exInfo.Speaker.SetTo(npcFormKey);
                exInfo.Conditions.Add(hideAfterAdvance);

                var exHash = SHA256.HashData(Encoding.UTF8.GetBytes(ex.NpcReply))[..4];
                var exResponse = new DialogResponse
                {
                    ResponseText = ex.NpcReply,
                    WEMFile      = exInfo.FormKey.ID,
                    TextHash     = exHash,
                    EmotionOut   = 7.466667f,
                };
                exResponse.Emotion.SetTo(FormKey.Null);
                exInfo.Responses.Add(exResponse);
                exTopic.Responses.Add(exInfo);
                exTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
                    { exInfo.FormKey.ToLink<IDialogResponsesGetter>() };
                quest.DialogTopics.Add(exTopic);

                SpeechTools.GenerateWavs(exInfo.FormKey.ID, voiceTypeEditorId,
                    targetMod.ModKey, ex.NpcReply, elevenLabsVoiceId);
            }
        }

        // ── Goodbye ────────────────────────────────────────────────────────────
        var goodbyeTopic = new DialogTopic(targetMod)
        {
            EditorID    = "dlg_goodbye_" + suffix,
            Category    = DialogTopic.CategoryEnum.Player,
            Subtype     = DialogTopic.SubtypeEnum.Goodbye,
            SubtypeName = DialogTopic.SubtypeNameEnum.Goodbye,
        };
        goodbyeTopic.Quest.SetTo(quest.FormKey);
        quest.DialogTopics.Add(goodbyeTopic);

        var goodbyeInfo = new DialogResponses(targetMod)
        {
            EditorID         = "dlgi_goodbye_" + suffix,
            SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low,
        };
        goodbyeInfo.Speaker.SetTo(npcFormKey);
        var byeHash = SHA256.HashData(Encoding.UTF8.GetBytes(script.Goodbye))[..4];
        var byeResponse = new DialogResponse
        {
            ResponseText = script.Goodbye,
            WEMFile      = goodbyeInfo.FormKey.ID,
            TextHash     = byeHash,
            EmotionOut   = 7.466667f,
        };
        byeResponse.Emotion.SetTo(FormKey.Null);
        goodbyeInfo.Responses.Add(byeResponse);
        goodbyeTopic.Responses.Add(goodbyeInfo);
        goodbyeTopic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
            { goodbyeInfo.FormKey.ToLink<IDialogResponsesGetter>() };
        SpeechTools.GenerateWavs(goodbyeInfo.FormKey.ID, voiceTypeEditorId,
            targetMod.ModKey, script.Goodbye, elevenLabsVoiceId);

        QuestRecord = quest;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static DialogTopic BuildMenuTopic(
        StarfieldMod targetMod, Quest quest, string editorId, string playerText)
    {
        var topic = new DialogTopic(targetMod)
        {
            EditorID    = editorId,
            Name        = playerText,
            Category    = DialogTopic.CategoryEnum.Player,
            Subtype     = DialogTopic.SubtypeEnum.Custom,
            SubtypeName = DialogTopic.SubtypeNameEnum.Custom,
        };
        topic.Quest.SetTo(quest.FormKey);
        return topic;
    }

    /// <summary>
    /// GetStageDoneConditionData(quest, stageValue) EqualTo [0 or 1].
    /// equalTo=1 → "stage N has been reached" (used on Greeting INFOs to select the right stage).
    /// equalTo=0 → "stage N has NOT been reached" (used on Progress+Explore to hide after advance).
    /// Verified in CREW_EliteCrew_OtherPlayer and UC02.
    ///
    /// NOTE: FirstParameter uses direct assignment with FormLinkOrIndex — .SetTo() fails CS0411.
    /// </summary>
    private static ConditionFloat BuildStageDoneCondition(Quest quest, int stageValue, int equalTo)
    {
        var condData = new GetStageDoneConditionData { SecondParameter = stageValue };
        condData.FirstParameter = new FormLinkOrIndex<IQuestGetter>(condData, quest.FormKey);

        return new ConditionFloat
        {
            ComparisonValue = equalTo,
            CompareOperator = CompareOperator.EqualTo,
            Data            = condData,
        };
    }
}
```

---

## Call Site Example

```csharp
var script = PromptManager.GetDialogueScript(
    npcDescription: $"{npc.Name}, facility manager, frightened and guarded",
    topics: new List<string> { "the reactor failure", "the missing workers" },
    addons: new List<string> { "Location: " + missionTemplate.Location + "\n" });

var dialogue = new NPCDialogueNoun(
    npc.FormKey, npcVoiceEditorId, script,
    suffix: npc.EditorID ?? questID,
    elevenLabsVoiceId: txVoice.Id);

// SpeechTools.ConvertAndDeploy() at end of run
```

---

## Comparison: AudioLog vs Dialogue Quest

| | AudioLog Quest | Dialogue Quest |
|---|---|---|
| Flags | `StartGameEnabled \| StartsEnabled \| RunOnce \| 0x100000` | `StartGameEnabled \| StartsEnabled \| RunOnce \| 0x10000` |
| `DialogBranch` | none | required |
| Player topics | none | Greeting / Custom / Goodbye |
| NPC voice delivery | RadioSceneAction → CustomScene topic | **`ResponseText` directly on INFO** |
| `INFO.StartScene` | null | null (no scene for voice) |
| `INFO.WEMFile` | `topic.FormKey.ID` | `info.FormKey.ID` ⚠ unverified |
| Stage conditions | none | `GetStageDoneConditionData` |
| `SetParentQuestStage` | none | `OnBegin=-1, OnEnd=N` on Progress INFOs |
| Scene records in quest | yes (audio playback) | none needed |

---

## Phase 2 — Confirmed by `gen_dlgtest` + xEdit

xEdit loaded `outlaws02.esm` clean after the construction fixes. Record chain verified:

| Check | Result |
|-------|--------|
| Quest flags `0x00010111` | ✅ |
| Alias `UniqueActor` → NPC_ base form | ✅ (use cloned NPC from target mod, not raw Starfield.esm ID) |
| DialogBranch `Category=Player`, `Flags=TopLevel` | ✅ |
| `branch.StartingTopic` → Greeting topic | ✅ |
| `topic.Branch` property exists on DialogTopic | ✅ |
| `quest.DialogBranches` property exists on Quest | ✅ |
| Greeting INFOs ordered latest-stage first | ✅ |
| Stage 0 INFO has no condition (fallback) | ✅ |
| Progress `SetParentQuestStage.OnEnd=100, OnBegin=-1` | ✅ |
| Explore `ResponseText` + `WEMFile=info.FormKey.ID` | ✅ |
| `Emotion.SetTo(FormKey.Null)` silences TRDA warning | ✅ |
| Terminal stage eliminates "stage not found" warning | ✅ |

---

## Open Questions — Require In-Game Testing

1. **WEMFile for Player-category INFOs** — `info.FormKey.ID` is our convention. Vanilla uses
   Wwise-assigned IDs with no FormKey correlation. Whether Starfield resolves
   `{infoId:X8}.wem` from the voice directory when playing an INFO's ResponseText needs
   in-game verification. If not, the audio falls back to silent subtitles only.

2. **Greeting re-activation** — After stage advances (player picks PROGRESS), the next
   Greeting INFO fires on the next NPC activation. Whether the engine automatically re-greets
   on the same activation (without the player needing to walk away and back) needs testing.

3. **Custom topic visibility** — Topics with `GetStageDone(quest,N)==0` appear in the menu.
   Whether the engine correctly shows/hides Custom topics based solely on their INFO conditions
   needs verification. May also require the topic to have a `Branch` link (currently omitted
   on Custom/Goodbye topics, matching vanilla UC02_PerrysMeatsSupportQuest where some topics
   have `Branch: 26FFAB` and others don't).
