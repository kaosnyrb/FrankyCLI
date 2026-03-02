# Spec: NPC Conversational Dialogue

## Overview

A generic system for generating voiced NPC dialogue — the player activates an NPC and gets a
dialogue menu with voiced responses. Reusable across any generated content (dungeons, worldspaces,
city encounters).

**Per-NPC quests** — each generated NPC gets its own dedicated dialogue quest. Clean isolation;
no shared-quest alias management complexity.

Distinct from the **audio data-slate** system (`SpeechTools.AddVoice`) which plays a one-sided
radio log with no player interaction. This system uses `DialogBranch` records (DLBR), which are
absent from audio-log quests.

---

## Dialogue Structure

One level of branching: greeting → player choices (each with one NPC reply) → goodbye.
No nested sub-trees. This covers the vast majority of generated NPC conversations and
maps directly to a single `DialogBranch`.

```
Greeting             — NPC's first words when player activates them
  ├─ Choice 1        — player menu item → NPC reply
  ├─ Choice 2        — player menu item → NPC reply
  └─ Choice N        — player menu item → NPC reply
Goodbye              — NPC farewell shown at bottom of menu / when player exits
```

---

## Data Model — `DialogueScript`

The dialogue is generated and stored as a plain C# data object before any Mutagen records
are touched. This separates AI generation from record construction.

```csharp
// Retrograde.Library/Models/DialogueScript.cs

namespace Retrograde.Models;

/// <summary>
/// Flat, one-level dialogue structure for a single NPC.
/// Produced by PromptManager.GetDialogueScript(); consumed by NPCDialogueNoun.
/// </summary>
public class DialogueScript
{
    /// <summary>NPC's greeting line (spoken on player activation, no player prompt).</summary>
    public string Greeting { get; set; } = "";

    /// <summary>Player choice → NPC reply pairs, shown as menu items after the greeting.</summary>
    public List<DialogueExchange> Choices { get; set; } = new();

    /// <summary>NPC farewell shown at the bottom of the dialogue menu.</summary>
    public string Goodbye { get; set; } = "";
}

/// <summary>One player choice entry and the NPC's response to it.</summary>
public class DialogueExchange
{
    /// <summary>Text shown in the player's dialogue menu (≤80 chars recommended).</summary>
    public string PlayerPrompt { get; set; } = "";

    /// <summary>NPC's spoken reply to this choice (≤200 chars; AI prompt enforces this).</summary>
    public string NpcReply { get; set; } = "";
}
```

---

## AI Generation — `PromptManager.GetDialogueScript`

Single LLM call returning an XML-tagged block (same convention as `GenerateLoreFile`).
Parsed with simple string search — no XML library needed.

### Prompt Design

```
Generate a short voiced conversation for an NPC in a Starfield-style bounty hunting game.

NPC: {npcDescription}
Context: {missionContext}

Use the LoreContext established earlier in this conversation for tone, setting, and names.
Do NOT invent new names or factions beyond the LoreContext.

Rules:
- Greeting: 1-2 sentences. NPC's first words on player activation. Wary or guarded.
  Under 200 characters.
- Choices: exactly 3 player questions and NPC replies.
  PlayerPrompt: what the player says. Plain and direct. Under 60 characters.
  NpcReply: NPC's response. In-character, grounded. Under 200 characters.
- Goodbye: 1 sentence. NPC ends the conversation. Under 100 characters.
- No stage directions, emotion labels, or quotes around the dialogue.
- Output in this exact format:

< Dialogue >
    < Greeting >TEXT</ Greeting >
    < Choice >
        < PlayerPrompt >TEXT</ PlayerPrompt >
        < NpcReply >TEXT</ NpcReply >
    </ Choice >
    < Choice >
        < PlayerPrompt >TEXT</ PlayerPrompt >
        < NpcReply >TEXT</ NpcReply >
    </ Choice >
    < Choice >
        < PlayerPrompt >TEXT</ PlayerPrompt >
        < NpcReply >TEXT</ NpcReply >
    </ Choice >
    < Goodbye >TEXT</ Goodbye >
</ Dialogue >
```

### C# Implementation

```csharp
// In PromptManager:

public static DialogueScript GetDialogueScript(string npcDescription, List<string> addons)
{
    var sb = new StringBuilder();
    sb.AppendLine("Generate a short voiced conversation for an NPC in a Starfield-style bounty hunting game.");
    sb.AppendLine();
    sb.AppendLine("NPC: " + npcDescription);
    sb.AppendLine();
    sb.AppendLine("Use the LoreContext established earlier in this conversation for tone, setting, and names.");
    sb.AppendLine("Do NOT invent new names or factions beyond the LoreContext.");
    sb.AppendLine();
    sb.AppendLine("Rules:");
    sb.AppendLine("- Greeting: 1-2 sentences. Wary or guarded tone. Under 200 characters.");
    sb.AppendLine("- Choices: exactly 3 player questions and NPC replies.");
    sb.AppendLine("  PlayerPrompt: plain and direct. Under 60 characters.");
    sb.AppendLine("  NpcReply: in-character, grounded. Under 200 characters.");
    sb.AppendLine("- Goodbye: 1 sentence. Under 100 characters.");
    sb.AppendLine("- No stage directions, emotion labels, or quotes.");
    sb.AppendLine("- Output in the exact XML format shown.");
    sb.AppendLine();
    sb.AppendLine("Additional Information:");
    foreach (var a in addons) sb.AppendLine(a);
    sb.AppendLine();
    sb.AppendLine("< Dialogue >");
    sb.AppendLine("    < Greeting >TEXT</ Greeting >");
    sb.AppendLine("    < Choice >< PlayerPrompt >TEXT</ PlayerPrompt >< NpcReply >TEXT</ NpcReply ></ Choice >");
    sb.AppendLine("    < Choice >< PlayerPrompt >TEXT</ PlayerPrompt >< NpcReply >TEXT</ NpcReply ></ Choice >");
    sb.AppendLine("    < Choice >< PlayerPrompt >TEXT</ PlayerPrompt >< NpcReply >TEXT</ NpcReply ></ Choice >");
    sb.AppendLine("    < Goodbye >TEXT</ Goodbye >");
    sb.AppendLine("</ Dialogue >");

    string raw = AITools.RunPrompt(sb.ToString());
    for (int i = 0; i < 5 && !raw.Contains("<Greeting>", StringComparison.OrdinalIgnoreCase); i++)
        raw = AITools.RunPrompt(sb.ToString());

    return ParseDialogueScript(raw);
}

private static DialogueScript ParseDialogueScript(string raw)
{
    var script = new DialogueScript
    {
        Greeting = ExtractDialogTag(raw, "Greeting"),
        Goodbye  = ExtractDialogTag(raw, "Goodbye"),
    };

    foreach (var block in ExtractAllDialogTags(raw, "Choice"))
    {
        var prompt = ExtractDialogTag(block, "PlayerPrompt");
        var reply  = ExtractDialogTag(block, "NpcReply");
        if (!string.IsNullOrWhiteSpace(prompt) && !string.IsNullOrWhiteSpace(reply))
            script.Choices.Add(new DialogueExchange { PlayerPrompt = prompt, NpcReply = reply });
    }

    return script;
}

// Shared helpers (can live alongside the LoreFile parsers):

private static string ExtractDialogTag(string text, string tag)
{
    var open  = $"<{tag}>";
    var close = $"</{tag}>";
    int start = text.IndexOf(open,  StringComparison.OrdinalIgnoreCase);
    int end   = text.IndexOf(close, StringComparison.OrdinalIgnoreCase);
    if (start < 0 || end < 0) return "";
    return text.Substring(start + open.Length, end - start - open.Length).Trim();
}

private static List<string> ExtractAllDialogTags(string text, string tag)
{
    var results = new List<string>();
    var open  = $"<{tag}>";
    var close = $"</{tag}>";
    int pos = 0;
    while (true)
    {
        int start = text.IndexOf(open,  pos, StringComparison.OrdinalIgnoreCase);
        if (start < 0) break;
        int end = text.IndexOf(close, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0) break;
        results.Add(text.Substring(start + open.Length, end - start - open.Length));
        pos = end + close.Length;
    }
    return results;
}
```

---

## Record Chain

```
Quest  (per-NPC, StartGameEnabled | StartsEnabled | HasDialogueData, Type=None)
  ├─ Alias[0]: QuestReferenceAlias  (ID=0, UniqueActor → NPC base form)
  │
  ├─ DialogBranch  (inline in Quest.DialogBranches)
  │    ├─ Quest → this quest
  │    ├─ Category = Player
  │    ├─ Flags = TopLevel
  │    └─ StartingTopic → DialogTopic "Greeting"   (set after topic created)
  │
  ├─ DialogTopic "Greeting"  (inline in Quest.DialogTopics)
  │    ├─ Branch → DialogBranch
  │    ├─ Category = Player,  Subtype = Greeting,  SubtypeName = Greeting
  │    └─ Responses → DialogResponses (INFO)
  │         ├─ Speaker → NPC,  SubtitlePriority = Low
  │         ├─ TopicInfoList cross-reference
  │         └─ DialogResponse { ResponseText=script.Greeting, WEMFile, TextHash }
  │
  ├─ DialogTopic "Choice_0..N"  (one per DialogueExchange, inline)
  │    ├─ Name = exchange.PlayerPrompt     ← text shown in player's menu
  │    ├─ Category = Player,  Subtype = Custom,  SubtypeName = Custom
  │    └─ Responses → DialogResponses (INFO)
  │         ├─ Speaker → NPC,  SubtitlePriority = Low
  │         ├─ Prompt = exchange.PlayerPrompt    ← mirrors Name on the INFO
  │         ├─ TopicInfoList cross-reference
  │         └─ DialogResponse { ResponseText=exchange.NpcReply, WEMFile, TextHash }
  │
  └─ DialogTopic "Goodbye"  (inline)
       ├─ Category = Player,  Subtype = Goodbye,  SubtypeName = Goodbye
       └─ Responses → DialogResponses (INFO)
            ├─ Speaker → NPC
            └─ DialogResponse { ResponseText=script.Goodbye, WEMFile, TextHash }
```

---

## Quest Flags for Dialogue Quests

| Flag | Value | Notes |
|------|-------|-------|
| `StartGameEnabled` | `0x0001` | Quest active at game start |
| `StartsEnabled` | `0x0010` | Dialogue fires immediately |
| **`HasDialogueData`** | **`0x8000`** | **Required for DialogBranch quests** |
| `RunOnce` | — | **Do NOT set** — dialogue must be repeatable |
| `AddIdleTopicToHello` | `0x0004` | Optional — NPC says greeting unprompted as player walks past |

```csharp
var data = new QuestData
{
    Flags = Quest.Flag.StartGameEnabled | Quest.Flag.StartsEnabled | Quest.Flag.HasDialogueData,
    Type  = Quest.TypeEnum.None,
};
```

No `0x100000` undocumented flag (confirmed on AudioLog quests only).

---

## Mutagen Construction — `NPCDialogueNoun`

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
            Flags = Quest.Flag.StartGameEnabled | Quest.Flag.StartsEnabled | Quest.Flag.HasDialogueData,
            Type  = Quest.TypeEnum.None,
        };
        var quest = new Quest(targetMod) { EditorID = "dlg_quest_" + suffix, Data = data };
        targetMod.Quests.Add(quest);

        // ── Alias ──────────────────────────────────────────────────────────────
        var alias = new QuestReferenceAlias { ID = 0, Name = "DialogNPC" };
        alias.UniqueActor.SetTo(npcFormKey);     // after construction — nullable FormLink rule
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

        // ── Greeting ───────────────────────────────────────────────────────────
        var greetTopic = BuildTopic(targetMod, quest, branch,
            "dlg_greeting_" + suffix,
            DialogTopic.SubtypeEnum.Greeting, DialogTopic.SubtypeNameEnum.Greeting);
        branch.StartingTopic.SetTo(greetTopic.FormKey);    // wire after topic exists

        AddInfo(targetMod, greetTopic, npcFormKey, voiceTypeEditorId,
            npcText: script.Greeting, playerPrompt: null, elevenLabsVoiceId);

        // ── Player choices ─────────────────────────────────────────────────────
        for (int i = 0; i < script.Choices.Count; i++)
        {
            var ex    = script.Choices[i];
            var topic = BuildTopic(targetMod, quest, branch: null,
                $"dlg_choice_{suffix}_{i}",
                DialogTopic.SubtypeEnum.Custom, DialogTopic.SubtypeNameEnum.Custom,
                playerText: ex.PlayerPrompt);

            AddInfo(targetMod, topic, npcFormKey, voiceTypeEditorId,
                npcText: ex.NpcReply, playerPrompt: ex.PlayerPrompt, elevenLabsVoiceId);
        }

        // ── Goodbye ────────────────────────────────────────────────────────────
        var goodbyeTopic = BuildTopic(targetMod, quest, branch: null,
            "dlg_goodbye_" + suffix,
            DialogTopic.SubtypeEnum.Goodbye, DialogTopic.SubtypeNameEnum.Goodbye);
        AddInfo(targetMod, goodbyeTopic, npcFormKey, voiceTypeEditorId,
            npcText: script.Goodbye, playerPrompt: null, elevenLabsVoiceId);

        QuestRecord = quest;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static DialogTopic BuildTopic(
        StarfieldMod targetMod, Quest quest, DialogBranch? branch,
        string editorId,
        DialogTopic.SubtypeEnum subtype, DialogTopic.SubtypeNameEnum subtypeName,
        string? playerText = null)
    {
        var topic = new DialogTopic(targetMod)
        {
            EditorID    = editorId,
            Category    = DialogTopic.CategoryEnum.Player,
            Subtype     = subtype,
            SubtypeName = subtypeName,
        };
        if (playerText != null) topic.Name = playerText;
        topic.Quest.SetTo(quest.FormKey);
        if (branch != null) topic.Branch.SetTo(branch.FormKey);    // Greeting only
        quest.DialogTopics.Add(topic);
        return topic;
    }

    private static void AddInfo(
        StarfieldMod targetMod, DialogTopic topic,
        FormKey npcFormKey, string voiceTypeEditorId,
        string npcText, string? playerPrompt, string elevenLabsVoiceId)
    {
        var info = new DialogResponses(targetMod)
        {
            EditorID         = topic.EditorID!.Replace("dlg_", "dlgi_"),
            SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low,
        };
        if (playerPrompt != null) info.Prompt = playerPrompt;
        info.Speaker.SetTo(npcFormKey);

        var textHash = SHA256.HashData(Encoding.UTF8.GetBytes(npcText))[..4];
        info.Responses.Add(new DialogResponse
        {
            ResponseText = npcText,
            WEMFile      = topic.FormKey.ID,     // Starfield resolves {topicId:X8}.wem ⚠ unverified
            TextHash     = textHash,
            EmotionOut   = 7.466667f,
        });
        topic.Responses.Add(info);
        topic.TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>
            { info.FormKey.ToLink<IDialogResponsesGetter>() };

        SpeechTools.GenerateWavs(topic.FormKey.ID, voiceTypeEditorId,
            targetMod.ModKey, npcText, elevenLabsVoiceId);
    }
}
```

---

## Call Site Example

```csharp
// In Investigation_Informant_Planet.cs (or any generation pass):

// 1. Generate dialogue content via AI
var script = PromptManager.GetDialogueScript(
    npcDescription: $"{npc.Name}, {Gender} criminal, wary and hostile",
    addons: new List<string>
    {
        "Location: " + missionTemplate.Location + "\n",
        "The NPC knows where the target is hiding but will not say directly.\n",
    });

// 2. Build all Mutagen records
var dialogue = new NPCDialogueNoun(
    npc.FormKey,
    npcVoiceEditorId,
    script,
    suffix: npc.EditorID ?? questID,
    elevenLabsVoiceId: txVoice.Id);

// QuestRecord is available if needed: dialogue.QuestRecord
// Audio conversion at end of run: SpeechTools.ConvertAndDeploy()
```

---

## Dialogue Quest vs AudioLog Quest — Key Differences

| | AudioLog Quest | Dialogue Quest |
|---|---|---|
| `Type` | `None` | `None` |
| Flags | `StartGameEnabled \| StartsEnabled \| RunOnce \| 0x100000` | `StartGameEnabled \| StartsEnabled \| HasDialogueData` |
| `DialogBranch` | **none** | **required, inline in quest** |
| `DialogTopic.Category` | `Scene` | `Player` |
| `DialogTopic.Subtype` | `CustomScene` | `Greeting` / `Custom` / `Goodbye` |
| `DialogTopic.Branch` | always null | set on Greeting only; null on Choice/Goodbye |
| Scene / RadioSceneAction | required | **not used** |
| `DialogResponses.Prompt` | absent | set on player-choice INFOs, mirrors `DialogTopic.Name` |
| Quest per NPC? | no — one shared quest | **yes — one per NPC** |

---

## Open Questions

1. **WEMFile convention for live NPC dialogue** — `{topicId:X8}.wem` is confirmed for audio
   data-slates (RadioSceneAction). Whether the same convention works for conversational
   dialogue (player-activated NPC) needs an in-game test before shipping `NPCDialogueNoun`.

2. **Player-choice topic linking** — `DialogBranch.StartingTopic` links to the Greeting, but
   how the engine discovers the `Custom` choice topics is unverified. Current assumption: all
   `Custom` topics on the same Quest appear as choices automatically. May need
   `topic.Branch.SetTo(branch.FormKey)` on choice topics too — test in CK first.

3. **`HasDialogueData` flag** — present in the `Quest.Flag` enum and logically required, but
   not yet verified against a live Starfield dialogue quest dump.

4. **NPC text length** — `AddInfo` assumes NPC reply ≤ 250 chars. The AI prompt constrains
   replies to ≤ 200 chars, so this almost always holds. If a reply exceeds 250 chars, port
   `SpeechTools.SplitText` (one INFO per chunk, multi-phase). Defer until it actually breaks.
