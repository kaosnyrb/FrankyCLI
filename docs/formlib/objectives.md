# Quest Objectives and Stages

Objectives and stages are two parallel tracking systems on the `Quest` record.

- **Stages** — internal progress markers. Scripts call `quest.SetStage(n)`. Only the
  latest log entry (based on stage order and log entry conditions) shows in the journal.
- **Objectives** — player-visible task display. Must be explicitly shown/completed by
  a script. Targets within an objective drive compass markers.

Both are in `Quest.Stages` and `Quest.Objectives` (`ExtendedList<>`).

---

## Stages — `QuestStage`

```
Quest.Stages  →  List<QuestStage>
  QuestStage
    Index       ushort           — the stage number (e.g. 0, 10, 100, 200)
    Flags?      QuestStage.Flag  — optional behaviour flags
    LogEntries  List<QuestLogEntry>
```

### `QuestStage.Flag`

| Flag | Value | Effect |
|---|---|---|
| `RunOnStart` | 2 | Execute stage logic when quest starts |
| `RunOnStop` | 4 | Execute stage logic when quest stops |
| `KeepInstanceDataFromHereOn` | 8 | Persist instance data beyond this point |

Most stages use no flags (`Flags = null`).

---

### Log entries — `QuestLogEntry`

Each stage can have multiple log entries. Starfield picks the **first** entry whose
conditions pass and displays it in the journal.

```
QuestLogEntry
  Flags?             QuestLogEntry.Flag  — CompleteQuest or FailQuest
  Conditions         List<Condition>     — when to show this entry
  Entry?             TranslatedString    — journal text displayed to player
  Note?              string              — internal CK notes
  ScriptFlagComment? string              — internal
```

### `QuestLogEntry.Flag`

| Flag | Value | Effect |
|---|---|---|
| `CompleteQuest` | 1 | Setting this stage marks the quest complete |
| `FailQuest` | 2 | Setting this stage marks the quest failed |

Leave `Flags = null` for ordinary progress stages.

### Minimal stage / log entry creation

```csharp
// Stage 0 — initial stage (no log entry shown yet)
quest.Stages.Add(new QuestStage
{
    Index = 0,
});

// Stage 100 — active (shows journal text)
var stage100 = new QuestStage { Index = 100 };
stage100.LogEntries.Add(new QuestLogEntry
{
    Entry = "Find the cache at the abandoned outpost."
});
quest.Stages.Add(stage100);

// Stage 200 — complete
var stage200 = new QuestStage { Index = 200 };
stage200.LogEntries.Add(new QuestLogEntry
{
    Flags = QuestLogEntry.Flag.CompleteQuest,
    Entry = "You recovered the cache."
});
quest.Stages.Add(stage200);
```

When cloning from a template (`QuestNoun`), stages are deep-copied from the source. Use
`SetLogMessage(stageIndex, logEntryIndex, text)` to replace the text at a known position.

---

## Objectives — `QuestObjective`

Objectives are the checkbox-style task items visible in the mission log. They are
**not** automatically shown — a script or stage must call
`quest.SetObjectiveDisplayed(index, true)` (Papyrus) or the game will never reveal them.

```
Quest.Objectives  →  List<QuestObjective>
  QuestObjective
    Index        ushort                  — objective number (arbitrary; shown in order)
    Flags?       QuestObjective.Flag     — optional
    DisplayText? TranslatedString        — text shown in the mission log
    Targets      List<QuestObjectiveTarget>
```

### `QuestObjective.Flag`

| Flag | Value | Effect |
|---|---|---|
| `OrWithPrevious` | 1 | This objective is logically OR'd with the previous one |
| `NoStatsTracking` | 2 | Don't count this towards quest stat tracking |

Leave `Flags = null` for standard objectives.

---

### Objective display text — `<Alias=Name>` tokens

Display text supports dynamic substitution tokens. Starfield replaces them at runtime
with the current value of the named alias.

| Token | Resolves to |
|---|---|
| `<Alias=BountyTarget>` | Name of whatever is in alias "BountyTarget" |
| `<Alias=DungeonLocation>` | Location name in alias "DungeonLocation" |
| `<Global=MyGlobal>` | Current value of GlobalVariable |

Examples from live quests:
```
"Destroy the <Alias=BountyTarget> At <Alias=DungeonLocation>"
"Locate the <Alias=BountyTarget> At Neon"
"Recover the data slate from the Ecliptic ship"
```

---

### Objective targets — `QuestObjectiveTarget`

Each target entry places a compass marker on an aliased reference when the objective
is active. An objective with no targets has no compass marker.

```
QuestObjectiveTarget
  AliasID    int                  — ID of the reference alias to mark
  Flags      Quest.TargetFlag     — compass marker behaviour
  Keyword    IFormLink<IKeyword>  — nav-mesh keyword hint (often null/empty)
  Conditions List<Condition>      — when this target is active
  QSTADataTypeState  QSTADataType — internal (leave default)
```

### `Quest.TargetFlag`

| Flag | Value | Effect |
|---|---|---|
| `CompassMarkerIgnoresLocks` | 1 | Compass marker shown even through locked doors |
| `Hostile` | 2 | Target is treated as hostile for marker display |
| `UseStraightLinePathing` | 4 | Draw a straight-line marker (not navmesh path) |

Most objectives use `Flags = 0` (default — standard compass marker through navmesh).

---

### Creating an objective from scratch

```csharp
// Objective 10 — with a compass marker pointing at alias 2 ("Target")
var obj = new QuestObjective
{
    Index = 10,
    DisplayText = "Locate the <Alias=Target>"
};
obj.Targets.Add(new QuestObjectiveTarget
{
    AliasID = 2,  // must match the alias's ID field
    Flags = 0     // no special flags
    // Keyword — leave default (null link)
});
quest.Objectives.Add(obj);

// Objective 20 — text only, no compass marker
quest.Objectives.Add(new QuestObjective
{
    Index = 20,
    DisplayText = "Return to the mission board."
});
```

---

### Modifying an objective from a template quest

`QuestNoun.SetObjective(index, text)` patches by list position (not objective Index):

```csharp
newQuest.SetObjective(0, "Find the " + itemName + " aboard the ship");
```

This replaces `Objectives[0].DisplayText` regardless of what the objective's `Index`
field contains. If your template only has one objective, `SetObjective(0, ...)` is
always the right call.

---

## Showing and completing objectives (Papyrus side)

Objectives are hidden by default. The Papyrus script driving the quest must call:

```papyrus
; Show objective when the quest reaches stage 100
quest.SetObjectiveDisplayed(10, true)  ; 10 = objective Index

; Complete objective when stage 200 is set
quest.SetObjectiveCompleted(10, true)
```

This is typically wired via stage result scripts in the CK, or via the fragment script
attached to the quest's VMA. For template-based quests the template's stage scripts
already handle this — you don't need to add it.

---

## Relationship between stages, objectives, and aliases

```
Stage 0  ──────────────────────────────  quest starts (no journal)
Stage 10  SetObjectiveDisplayed(10)  ──  objective 10 shown, compass → alias 2
Stage 100 SetObjectiveCompleted(10)  ──  objective 10 ticked off
          SetObjectiveDisplayed(20)  ──  objective 20 shown (return to board)
Stage 200 CompleteQuest flag         ──  quest done
```

The stage script logic lives in Papyrus fragments (on the quest's VMA) or in the
template quest's compiled scripts. When cloning, this logic is preserved.

---

## Full example — clone + patch

Most quest types in Retrograde clone an existing template and patch the one objective:

```csharp
var newQuest = new QuestNoun(missionTemplate.formid.ID, questname);
newQuest.SetLogMessage(0, 0, logmessage);          // journal text at stage 0 → 100
newQuest.SetObjective(0, "Destroy the <Alias=BountyTarget> At <Alias=DungeonLocation>");
```

`SetLogMessage(stageIndex, logEntryIndex, text)` and `SetObjective(listIndex, text)`
both work by position in the cloned lists — so ensure the template has the same
number of stages and objectives as you expect.
