# Spec: Epic Data-Driven Lore System

> **Branch:** `EpicDataDriven`
> **Goal:** Decouple story content from Mutagen generation. All quest names, objectives,
> log entries, dialogue, and NPC definitions are authored in YAML files. Generation builds
> the ESM directly from a mission YAML with zero AI calls required.

---

## Directory Structure

Each mission is a self-contained subfolder. All paths inside `mission.yaml` are relative
to that folder. A `shared/` folder holds NPCs and dialogue that appear across multiple missions.

```
lore/
  cydonia_hunt/                   # one folder per mission
    mission.yaml                  # entry point — chains quests + declares NPCs
    npcs/
      vex_carrow.yaml
      ranger_scout_dela.yaml
    quests/
      discovery_wanted_poster.yaml
      investigation_ice_crystals.yaml
      deep_investigation_ranger_scout.yaml
      showdown_cydonia_outskirts.yaml
    dialogue/
      dela_informant_scene.yaml

  blacksite_op/                   # second mission — fully independent
    mission.yaml
    npcs/
    quests/
    dialogue/

  shared/                         # recurring characters, faction-wide dialogue
    npcs/
      rook.yaml                   # appears in multiple missions
    dialogue/
      rook_ambient.yaml
```

**Path resolution rules:**
- Paths in `mission.yaml` are relative to the mission folder (`npcs/vex_carrow.yaml` → `lore/cydonia_hunt/npcs/vex_carrow.yaml`)
- Paths prefixed `shared/` resolve from `lore/shared/` (`shared/npcs/rook.yaml`)
- Dialogue `id` references within a quest YAML are resolved by searching the mission's `dialogue/` folder first, then `shared/dialogue/`

CLI entry point takes the mission folder path:

```bash
dotnet run -- gen_lore lore/cydonia_hunt [modname]
```

---

## 1. NPC File (`lore/[mission]/npcs/rook.yaml`)

Defines a character. One file per named NPC.

```yaml
id: rook
name: Rook
faction: outlaws
gender: male
voice_type: MaleOutlaw
role: guard                     # archetype used by quest templates (guard / informant / boss)
notes: >
  Paranoid and mercenary. Loyal to whoever pays.
  Scar across left eye. Terse, clipped speech.

# Dialogue shown whenever the player activates this NPC (stage-independent).
# Omit if the NPC only speaks in quest-gated scenes.
ambient_dialogue: rook_ambient
```

**Fields:**

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | string | yes | Unique within a mission |
| `name` | string | yes | Display name |
| `faction` | string | yes | Used for template lookups |
| `gender` | `male` \| `female` | yes | |
| `voice_type` | string | yes | Starfield EditorID of the VoiceType |
| `role` | string | yes | `guard`, `informant`, `boss`, `neutral` — maps to quest template NPC slots |
| `notes` | string | no | Writing reference only, never emitted to game |
| `ambient_dialogue` | string | no | ID of a dialogue YAML |

---

## 2. Dialogue File (`lore/[mission]/dialogue/rook_ambient.yaml`)

A **node graph** of NPC and player lines. Nodes connect by ID. The engine walks the graph
when the player activates the NPC.

```yaml
id: rook_ambient
npc: rook
start: greeting          # entry point node

nodes:
  greeting:
    type: npc_line
    text: "You shouldn't be here. This area is restricted."
    next: initial_choices

  initial_choices:
    type: player_choices
    choices:
      - text: "Who are you?"
        next: who_reply
      - text: "What happened here?"
        next: what_reply
        requires_stage: 10        # only shown after quest stage 10
      - text: "I'm leaving."
        next: farewell

  who_reply:
    type: npc_line
    text: "Name's Rook. Facility security. And you are?"
    next: initial_choices         # loops back to menu

  what_reply:
    type: npc_line
    text: "Research team went dark three days ago. No distress call. Nothing."
    set_conversation_stage: 20    # advances this NPC's dialogue quest stage when this line plays
    next: what_followup

  what_followup:
    type: player_choices
    choices:
      - text: "Any survivors?"
        next: survivors_reply
      - text: "Sounds dangerous."
        next: dangerous_reply

  survivors_reply:
    type: npc_line
    text: "Maybe. But if they are, they're not talking."
    next: end_conversation

  dangerous_reply:
    type: npc_line
    text: "More than you know. Now move along."
    next: end_conversation

  farewell:
    type: npc_line
    text: "Smart choice."
    next: end_conversation

  end_conversation:
    type: end
```

### Node Types

#### `npc_line`
| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `text` | string | yes* | Spoken line. Hard limit 250 chars. *Use `segments` for multi-line. |
| `segments` | list | yes* | Alternative to `text` — each entry is one phase in the same scene. |
| `next` | string | yes | ID of next node. Chaining `npc_line → npc_line` is valid: both become phases in one multi-phase scene. |
| `set_conversation_stage` | int | no | Advances **this NPC's dialogue quest** to this stage when the line plays. Scoped to the NPC's ambient dialogue quest — does **not** affect the parent investigation/showdown quest. |
| `voice_note` | string | no | Delivery direction for voice generation (e.g. `"quiet, guilty"`). Never emitted to game. |

#### `player_choices`
| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `choices` | list | yes | 1–N choices (engine shows all visible ones as a menu) |
| `choices[].text` | string | yes | Player prompt shown in the menu. Hard limit 60 chars. |
| `choices[].next` | string | yes | Node ID to jump to when this choice is picked |
| `choices[].requires_stage` | int | no | Choice is hidden until quest stage ≥ this value |

#### `end`
No fields. Closes the conversation.

### Character Limits (Starfield constraints)
| Text type | Max |
|-----------|-----|
| NPC voiced line | 250 chars (split into segments if longer) |
| Player choice prompt | 60 chars |
| Quest log entry | 500 chars recommended |
| Quest name | 80 chars |

---

## 3. Quest File (`lore/[mission]/quests/investigation_blacksite_city.yaml`)

Defines one quest in the chain. `template` references an existing `MissionTemplate` by its
`Name` string. The loader clones that object; the YAML then overrides story fields on top.
This preserves mechanical config (dynamic FormIds, SpaceCell, StationSize, etc.) without the
author having to know or repeat it.

Quest stage progression is **entirely controlled by Papyrus scripts** baked into the template
ESM records. The generation code only writes into fixed text slots that the template exposes.

### Standard quest (investigations + showdowns)

```yaml
id: investigation_blacksite_city
template: "City Activator - New Atlantis"   # exact Name string from a Templates_* class

name: "Dark Signal"
log_entry: >
  A distress beacon has gone silent in the New Atlantis research quarter.
  Security is on edge. Whatever silenced that team, it didn't leave witnesses.

# objective: only supported by Investigation_Derelict_Space and Investigation_Informant_Planet
objective: "Find the source of the signal in New Atlantis"

npcs:
  informant: rook
  boss: null            # null = template generates one procedurally

# Only needed to override template defaults — most quests need nothing here
parameters:
  Label: "blacksite_city"
  FormId: 0x002CC1EF

addons:
  - "Outlaws faction cover-up"
  - "Corporate research gone wrong"
```

### Discovery_Dataslate — no quest record, creates a book item

```yaml
id: discovery_blacksite
template: "Dataslate in levelled item"

book_title: "Incident Log — New Atlantis Research Quarter"
book_contents: >
  [ENCRYPTED — UC RESEARCH DIVISION]
  The research team stopped responding on Day 14.
  Last telemetry: sublevel 3. Cause unknown. Contract issued for independent investigation.
  Authorised contact: see attached lead. Do not approach through official channels.

npcs: {}
addons:
  - "Corporate cover-up, restricted access, sublevel 3"
```

### Discovery_WantedPoster — no quest record, creates a poster activator

```yaml
id: discovery_wanted
template: "Wanted Poster Activator"

# Poster name is auto-generated as "Wanted: {outlawNpc.name}" — not writable
pickup_message: >
  The poster is old but the contract is still live. Whoever posted this wants them found —
  and the coordinates scrawled on the back point somewhere off the main lanes.

npcs: {}
addons:
  - "Old contract, off the books, frontier coordinates"
```

**Fields by template type:**

| Field | Standard quests | Dataslate | WantedPoster |
|-------|----------------|-----------|--------------|
| `name` | ✅ quest name | — | — |
| `log_entry` | ✅ journal text | — | — |
| `objective` | ✅ Derelict + Informant only | — | — |
| `book_title` | — | ✅ | — |
| `book_contents` | — | ✅ body text | — |
| `pickup_message` | — | — | ✅ popup text |
| `npcs` | optional | — | — |
| `parameters` | override only | — | — |
| `addons` | context notes (see below) | context notes | context notes |

> **`addons` in data-driven mode**: In AI-driven generation, `addons` feed the PromptManager as narrative context. In data-driven mode (YAML as sole source of truth) the PromptManager is bypassed, so `addons` are not consumed during generation. They serve as **author notes** — human-readable reminders of what the scene should convey, useful for future AI voice direction or when reviewing YAML in isolation. They are copied verbatim to the `MissionTemplate.Addons` list but have no mechanical effect on ESM output.

---

## 4. Mission File (`lore/[mission]/mission.yaml`)

The entry point for generation. Declares all NPCs and chains the quests in order.

```yaml
id: blacksite_mission
name: "The Dark Signal"
faction: outlaws

# Mission-level string constants. Use {{KEY}} in any quest or dialogue YAML in this mission.
# Resolved by LoreLoader before the node graph is walked.
constants:
  target_name: "Dr. Mira Chen"
  location_name: "New Atlantis Research Quarter"
  ship_name: "Serpent's Coil"

# All named NPCs used anywhere in this mission.
npcs:
  - npcs/rook.yaml
  - npcs/dr_chen.yaml

# Quest chain executed in order. Each quest hands off to the next.
chain:
  - quests/discovery_poster_blacksite.yaml
  - quests/investigation_blacksite_city.yaml
  - quests/showdown_blacksite_final.yaml
```

**Fields:**

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `id` | string | yes | |
| `name` | string | yes | Human-readable, not emitted to game |
| `faction` | string | yes | Passed to chain builder |
| `constants` | dict | no | String substitutions applied across all child files (`{{KEY}}` syntax) |
| `npcs` | list | yes | Paths relative to the mission folder; prefix `shared/` for shared NPCs |
| `chain` | list | yes | Paths relative to the mission folder, in execution order |

---

## 5. Multi-Segment NPC Lines

If an NPC line exceeds 250 characters, it must be split into segments. In the dialogue YAML,
use a `segments` list instead of `text`:

```yaml
long_exposition:
  type: npc_line
  segments:
    - "They built this place off the books. No permits, no oversight."
    - "When the board found out, they didn't shut it down. They funded it."
    - "Whatever they were building in there — they wanted it finished."
  next: player_choices_after_reveal
```

Each segment becomes a separate DialogTopic + DialogResponses + Scene phase, exactly as the
multi-segment audio pattern already implemented in `formlib/book_audio.md`.

---

## 6. C# Model Layer

New namespace: `Retrograde.Lore`

```
Retrograde.Library/
  Lore/
    NpcLore.cs           # maps to NPC YAML
    DialogueLore.cs      # maps to dialogue YAML — node graph
    QuestLore.cs         # maps to quest YAML
    MissionLore.cs       # maps to mission YAML
    LoreLoader.cs        # YAML → model deserialization, cross-file reference resolution
```

### Key types

```csharp
// NpcLore.cs
public class NpcLore
{
    public string Id             { get; set; }
    public string Name           { get; set; }
    public string Faction        { get; set; }
    public string Gender         { get; set; }
    public string VoiceType      { get; set; }
    public string Role           { get; set; }
    public string Notes          { get; set; }
    public string AmbientDialogue { get; set; }  // dialogue id
}

// DialogueLore.cs
public class DialogueLore
{
    public string Id  { get; set; }
    public string Npc { get; set; }
    public string Start { get; set; }
    public Dictionary<string, DialogueNode> Nodes { get; set; }
}

public abstract class DialogueNode
{
    public string Type { get; set; }  // "npc_line" | "player_choices" | "end"
}

public class NpcLineNode : DialogueNode
{
    public string       Text     { get; set; }
    public List<string> Segments { get; set; }  // alternative to Text for long lines
    public string       Next     { get; set; }
    public int?         SetStage { get; set; }
}

public class PlayerChoicesNode : DialogueNode
{
    public List<PlayerChoice> Choices { get; set; }
}

public class PlayerChoice
{
    public string Text          { get; set; }
    public string Next          { get; set; }
    public int?   RequiresStage { get; set; }
}

public class EndNode : DialogueNode { }

// QuestLore.cs
// Text fields map directly to the fixed slots each template exposes.
// Stage progression is Papyrus-driven; QuestLore has no stage list.
public class QuestLore
{
    public string Id          { get; set; }
    public string Template    { get; set; }   // MissionTemplate.Name string
    public string Name        { get; set; }   // → SetLogMessage quest name / QuestNoun ctor
    public string LogEntry    { get; set; }   // → SetLogMessage(0, 0, ...)
    public string Objective   { get; set; }   // → SetObjective(0, ...) — Derelict + Informant only
    // Discovery_Dataslate only:
    public string BookTitle    { get; set; }
    public string BookContents { get; set; }
    // Discovery_WantedPoster only:
    public string PickupMessage { get; set; }
    public Dictionary<string, string>  Npcs       { get; set; }
    public Dictionary<string, object>  Parameters { get; set; }
    public List<string>                Addons     { get; set; }
}

// MissionLore.cs
public class MissionLore
{
    public string       Id      { get; set; }
    public string       Name    { get; set; }
    public string       Faction { get; set; }
    public List<string> Npcs   { get; set; }   // file paths
    public List<string> Chain  { get; set; }   // file paths
}
```

### LoreLoader

```csharp
// Resolves a mission file + all referenced NPCs, quests, and dialogue files.
// Returns a fully linked MissionLore ready for generation.
public static class LoreLoader
{
    public static LoadedMission Load(string missionYamlPath);
}

public class LoadedMission
{
    public MissionLore                      Mission   { get; set; }
    public List<NpcLore>                    Npcs      { get; set; }
    public List<QuestLore>                  Quests    { get; set; }
    public Dictionary<string, DialogueLore> Dialogues { get; set; }  // id → tree
}
```

YAML deserialization via **YamlDotNet** (already used or easily added as a NuGet package).

---

## 7. Generation Pipeline

### CLI entry point

```bash
dotnet run -- gen_lore lore/cydonia_hunt [modname]
```

### Flow

```
LoreLoader.Load(missionFolder)
  └─ Deserialize MissionLore from mission.yaml
  └─ Load each NPC file → NpcLore
  └─ Load each Quest file → QuestLore
  └─ Load all referenced Dialogue files → DialogueLore

For each NPC with ambient_dialogue:
  └─ BranchingDialogueNoun(npcLore, dialogueLore, targetMod)
       → builds branching quest+scene structure (see §8)

Build TemplateIndex: instantiate all Templates_* classes, index MissionTemplate objects by Name

For each Quest in chain:
  └─ Clone MissionTemplate by QuestLore.Template name from TemplateIndex
       (dynamic parameters like ShipTools.GetAClassShip() are already resolved in the clone)
  └─ Apply QuestLore overrides onto clone:
       - Name ← QuestLore.Name
       - Location ← QuestLore.Location (if provided)
       - Addons ← template.Addons + QuestLore.Addons
       - parameters ← merge(template.parameters, QuestLore.Parameters)  ← YAML wins on conflict
       - outlawNpc ← from Npcs["boss"] if present
  └─ IOutlawQuest.Setup(...) — existing method signature unchanged
  └─ Apply stage objectives from QuestLore.Stages
  └─ For each stage with dialogue:
       BranchingDialogueNoun(npc, dialogueLore, targetMod, parentQuest, triggerStage)
```

The key design principle: **existing `IOutlawQuest` implementations keep working unchanged**.
The lore system builds the `MissionTemplate` that gets passed into them. `MissionTemplate`
gains a nullable `QuestLore Lore` property; quest templates check it and skip `PromptManager`
calls for any field the lore already provides. The bypass lives entirely inside
`MissionTemplate` — no changes to individual quest template classes.

---

## 8. Branching Dialogue → Mutagen

`BranchingDialogueNoun` replaces `NPCDialogueNoun` for node-graph dialogue. It walks
the `DialogueLore` graph and emits one quest per NPC with scenes conditioned on stages.

**Graph → scene mapping rules:**

A chain of `npc_line → npc_line → … → player_choices` nodes maps to a **single multi-phase
scene** — one phase per NPC line, playing automatically in sequence. This is valid in
Starfield; multiple phases within one scene execute without player input between them.
`segments` on a single node is equivalent and preferred when the lines form one unbroken
thought; chained nodes are preferred when each line is a distinct beat that may later need
a branch inserted between them.

| YAML construct | Mutagen output |
|---------------|----------------|
| Single `npc_line` node | One-phase NPC topic scene |
| Chain of `npc_line → npc_line → … → player_choices` | Single multi-phase scene; one phase + DialogueSceneAction per line |
| `npc_line` with `segments` list | Same as chained — single scene, one phase per segment |
| `player_choices` node | One topic scene per choice (all conditioned on current conversation stage) |
| `set_conversation_stage` on npc_line | `SetParentQuestStage` on the DialogResponses for that line — scoped to the NPC's dialogue quest |
| `requires_stage` on choice | Additional `GetStage >= N` condition on that topic scene |
| `next: <player_choices>` after final npc_line in chain | Sets `TopLevelTopicsOnEnd` on the scene (flags 0x2814) |
| `next: end_conversation` | Completion topic (flags=0x2810, conversation ends) |

**Stage numbering convention:**

| Range | Purpose |
|-------|---------|
| 0, 100, 200, … (increments of 100) | Quest progression stages |
| 5, 10, 15, … (increments of 5) | Dialogue conversation state |

Dialogue stages are assigned sequentially from 5 as the graph is walked. Each distinct
`player_choices` node gets its own stage so the engine shows the right set of choices
after each NPC line. Quest authors set `requires_stage` and `set_stage` using dialogue-range
values (multiples of 5). Quest objective stages use multiples of 100 and never overlap.

---

## 9. What Changes vs. What Stays the Same

| Component | Change |
|-----------|--------|
| `IOutlawQuest` implementations | No change — still accept `MissionTemplate` |
| `MissionTemplate` | No change — populated by `LoreLoader` instead of hardcoded C# |
| `MissionTemplate` | Add nullable `QuestLore Lore` property; helpers like `GetName()` return lore value or call `PromptManager` |
| `PromptManager` calls inside quest templates | No change — templates call helpers on `MissionTemplate`; bypass is invisible to them |
| `NPCDialogueNoun` | Kept for backward compat; `BranchingDialogueNoun` is the new path |
| `DialogueScript` | Kept; flat dialogue still works via existing noun |
| ElevenLabs / SpeechTools | Unchanged — still called for any NPC line, voiceId from `NpcLore.VoiceType` |
| Gen harness / `gen_quest_main` | New `gen_lore` entry point added alongside existing commands |

---

## 10. Decisions Made

| Decision | Resolution |
|----------|-----------|
| PromptManager bypass | Wrap at `MissionTemplate` level — add `QuestLore Lore` property; helpers return lore text or fall back to AI |
| Stage numbering | Quest stages in increments of 100; dialogue conversation stages in increments of 5 |
| NPC FormKey resolution | `LoreRegistry` — a runtime lookup populated as NPCs and records are created during generation; lore IDs map to FormKeys as they are assigned |

### LoreRegistry pattern

```csharp
// Populated incrementally during generation — lore id → FormKey
public static class LoreRegistry
{
    private static readonly Dictionary<string, FormKey> _npcs = new();

    public static void RegisterNpc(string loreId, FormKey formKey) =>
        _npcs[loreId] = formKey;

    public static FormKey ResolveNpc(string loreId) =>
        _npcs.TryGetValue(loreId, out var fk)
            ? fk
            : throw new InvalidOperationException($"NPC '{loreId}' not yet registered. Generate NPC records before quests.");
}
```

Generation order must be: **NPCs first → ambient dialogue → quests**. Quest templates
call `LoreRegistry.ResolveNpc(npcLore.Id)` when they need a FormKey.

---

## 11. Open Questions

1. **YamlDotNet polymorphism** — `DialogueNode` is abstract with subclasses. YamlDotNet needs
   a custom type discriminator on the `type:` field. Verify this pattern compiles and
   deserializes correctly before committing to the class hierarchy.
