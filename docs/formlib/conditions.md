# Conditions — Reference for Procedural Quests

A `Condition` is a single boolean test. Records that hold conditions store them as
`IList<Condition>` — all conditions in the list must pass (AND logic by default).

Conditions appear on: quest aliases, story manager nodes, leveled list entries,
dialogue info, script properties, package data, and many more record types.

---

## Type hierarchy

```
Condition  (abstract)
  ├── ConditionFloat   — comparison value is a float literal
  └── ConditionGlobal  — comparison value is a GlobalVariable FormLink
```

The concrete type is chosen based on whether the comparison value is a hard-coded
number (`ConditionFloat`) or a variable (`ConditionGlobal`). For most quest logic,
`ConditionFloat` is what you want.

---

## `Condition` — base fields

| Field | Type | Notes |
|---|---|---|
| `CompareOperator` | `CompareOperator` | How to compare the function result against the comparison value |
| `Flags` | `Condition.Flag` | OR / SwapSubjectAndTarget — see below |
| `Data` | `ConditionData` | The specific condition function + its parameters |
| `Unknown1` | `MemorySlice<byte>` | Do not set; leave default |
| `Unknown2` | `ushort` | Do not set; leave default |

### `CompareOperator` enum

```csharp
CompareOperator.EqualTo
CompareOperator.NotEqualTo
CompareOperator.GreaterThan
CompareOperator.GreaterThanOrEqualTo
CompareOperator.LessThan
CompareOperator.LessThanOrEqualTo
```

### `Condition.Flag` enum

```csharp
Condition.Flag.OR                  // = 0x01 — this condition uses OR with the next instead of AND
Condition.Flag.SwapSubjectAndTarget // = 0x10 — swap subject/target roles
```

Leave `Flags = 0` (default) for standard AND conditions.

### `ConditionFloat` — additional field

| Field | Type | Notes |
|---|---|---|
| `ComparisonValue` | `float` | The literal value the function result is compared against |

Most conditions compare against `1f` (EqualTo → "function returned true") or `0f`
(EqualTo → "function returned false"), or a stage number for `GetStage`.

### `ConditionGlobal` — additional field

| Field | Type | Notes |
|---|---|---|
| `ComparisonValue` | `IFormLink<IGlobalGetter>` | Set via `.SetTo(formKey)` |

---

## `ConditionData` — base fields (on every condition type)

| Field | Type | Notes |
|---|---|---|
| `RunOnType` | `Condition.RunOnType` | Which actor/object the function evaluates against |
| `Reference` | form link | Only used when `RunOnType = Reference` or `QuestAlias` |
| `UseAliases` | bool | Required true when `RunOnType = QuestAlias` |
| `UsePackageData` | bool | Used with `RunOnType = PackageData` |
| `Unknown3` | — | Leave default |

### `Condition.RunOnType` enum

| Value | Int | Meaning |
|---|---|---|
| `Subject` | 0 | The actor/object the condition is attached to (default) |
| `Target` | 1 | The current combat/dialogue target |
| `Reference` | 2 | A specific placed reference (set `Reference` field) |
| `CombatTarget` | 3 | Current combat target |
| `LinkedReference` | 4 | The subject's linked reference |
| `QuestAlias` | 5 | An alias on the owning quest (set `Reference` to quest + alias ID) |
| `PackageData` | 6 | |
| `EventData` | 7 | |
| `CommandTarget` | 9 | |
| `EventCameraRef` | 10 | |
| `MyKiller` | 11 | |
| `PlayerShip` | 14 | The player's current ship |

For most quest conditions (is stage set, has keyword, etc.), leave `RunOnType = Subject`.

---

## `FormLinkOrIndex` — the parameter assignment type

Most condition data types store their parameters as `IFormLinkOrIndex<T>`, which can
hold either a direct FormKey link or an integer alias/parameter index.

**Critical gotcha:** always use direct assignment, never `.SetTo()`:

```csharp
// CORRECT — direct assignment to FirstParameter
condData.FirstParameter = new FormLinkOrIndex<IQuestGetter>(condData, quest.FormKey);

// WRONG — .SetTo() fails with CS0411 (type inference cannot resolve TMajorRhs)
condData.FirstParameter.SetTo<IQuestGetter>(quest);
```

The `FormLinkOrIndex<T>` constructor takes **(owning condData object, FormKey)**:
```csharp
new FormLinkOrIndex<TGetter>(condData, formKey)
```

---

## Confirmed condition types with C# patterns

### `GetStageDoneConditionData`
Returns 1 if a specific stage has been completed on a quest.

```csharp
var condData = new GetStageDoneConditionData();
condData.FirstParameter = new FormLinkOrIndex<IQuestGetter>(condData, quest.FormKey);
condData.SecondParameter = 100; // the stage number

var cond = new ConditionFloat
{
    CompareOperator = CompareOperator.EqualTo,
    ComparisonValue = 1f,
    Data = condData
};
```

`SecondParameter` type: `int` (the stage number).

---

### `GetStageConditionData`
Returns the current stage number of a quest. Useful for range checks (`>= 10`, `< 50`).

```csharp
var condData = new GetStageConditionData();
condData.FirstParameter = new FormLinkOrIndex<IQuestGetter>(condData, quest.FormKey);
// SecondParameter unused — leave default 0

var cond = new ConditionFloat
{
    CompareOperator = CompareOperator.EqualTo,
    ComparisonValue = 50f, // stage must be exactly 50
    Data = condData
};
```

---

### `GetQuestCompletedConditionData`
Returns 1 if the specified quest has been completed (all objectives done).

```csharp
var condData = new GetQuestCompletedConditionData();
condData.FirstParameter = new FormLinkOrIndex<IQuestGetter>(condData, quest.FormKey);

var cond = new ConditionFloat
{
    CompareOperator = CompareOperator.EqualTo,
    ComparisonValue = 1f,
    Data = condData
};
```

---

### `GetIsIDConditionData`
Returns 1 if the subject (actor/object) matches a specific base form. Used in scene
conditions to check which NPC is activating.

```csharp
var condData = new GetIsIDConditionData();
condData.FirstParameter = new FormLinkOrIndex<IPlaceableObjectGetter>(condData, npcFormKey);
// SecondParameter is int, leave default

var cond = new ConditionFloat
{
    CompareOperator = CompareOperator.EqualTo,
    ComparisonValue = 1f,
    Data = condData
};
```

---

### `HasKeywordConditionData`
Returns 1 if the subject has the specified keyword.

```csharp
var condData = new HasKeywordConditionData();
condData.FirstParameter = new FormLinkOrIndex<IKeywordGetter>(condData, keywordFormKey);

var cond = new ConditionFloat
{
    CompareOperator = CompareOperator.EqualTo,
    ComparisonValue = 1f,
    Data = condData
};
```

---

### `GetGlobalValueConditionData`
Returns the current value of a GlobalVariable. Used in conjunction with `ConditionFloat`
when comparing against a literal number, or `ConditionGlobal` to compare against
another global.

```csharp
// Compare GetGlobalValue(myGlobal) == 0 (i.e. global has not been set)
var condData = new GetGlobalValueConditionData();
condData.FirstParameter = new FormLinkOrIndex<IGlobalGetter>(condData, globalFormKey);

var cond = new ConditionFloat
{
    CompareOperator = CompareOperator.EqualTo,
    ComparisonValue = 0f,
    Data = condData
};
```

---

### `GetInFactionConditionData`
Returns 1 if the subject is a member of the specified faction.

```csharp
var condData = new GetInFactionConditionData();
condData.FirstParameter = new FormLinkOrIndex<IFactionGetter>(condData, factionFormKey);

var cond = new ConditionFloat
{
    CompareOperator = CompareOperator.EqualTo,
    ComparisonValue = 1f,
    Data = condData
};
```

---

## Condition types NOT in Mutagen

Several CK condition functions have no `ConditionData` class in Mutagen Starfield.
You cannot build them from scratch — use the **clone-and-patch** pattern instead.

| CK function name | Status |
|---|---|
| `IsInLocation` | ❌ not in Mutagen — clone from vanilla alias |
| `IsInLocationCurrent` | ❌ not in Mutagen |
| `IsInLocationFormList` | ❌ not in Mutagen |

For quest alias location filtering, prefer using `DefaultAliasOnLocationChange` with
`LocationsToCheckAgainst` instead of a condition.

---

## `ConditionGlobal` pattern

Use `ConditionGlobal` when the comparison value is a GlobalVariable (e.g. a distance
threshold that needs to change per-quest):

```csharp
var condData = new GetDistanceGalacticParsecConditionData();
// ... set condData parameters

var cond = new ConditionGlobal
{
    CompareOperator = CompareOperator.LessThan,
    Data = condData
};
cond.ComparisonValue.SetTo(distanceGlobal.FormKey); // note: SetTo IS valid on IFormLink, not on IFormLinkOrIndex
```

**Important:** `cond.ComparisonValue` is a full `IFormLink<IGlobalGetter>`, not a
`FormLinkOrIndex`, so `.SetTo()` works correctly here (unlike `FirstParameter`).

---

## Clone-and-patch pattern

When you need a complex condition that's hard to build from scratch (e.g. location
reference type conditions used for space marker placement), clone from a vanilla source
and patch the FormKey reference:

```csharp
// Get a vanilla alias whose conditions you want to reuse
var sourceAlias = (IQuestReferenceAliasGetter)vanillaQuest.Aliases[aliasIndex];
Condition cond = sourceAlias.Conditions[0].DeepCopy();

// Patch the quest FormKey inside the cloned condition
if (cond is ConditionFloat cf && cf.Data is GetStageDoneConditionData stageCond)
    stageCond.FirstParameter = new FormLinkOrIndex<IQuestGetter>(stageCond, myQuest.FormKey);
```

This is how `SpaceCellTools.GetSpaceMarkerCondition()` works — it clones a space-marker
placement condition from the vanilla Robot Survey quest and hands it back ready-to-use.

---

## Global fixup — template mod hazard

When deep-copying a quest that has conditions referencing globals from a template mod,
those FormKeys will still point to the template mod's globals. `QuestNoun` handles this
automatically via `EnsureAliasConditionGlobals()`, which detects:

- `ConditionFloat` with `GetGlobalValueConditionData` → patches `FirstParameter`
- `ConditionGlobal` → patches `ComparisonValue.SetTo()`

If you build your own quest cloning code, replicate this fixup or the global references
will be broken master dependencies.

---

## Where conditions live

| Record | Property path |
|---|---|
| `QuestReferenceAlias` | `.Conditions` |
| `QuestLocationAlias` | `.Conditions` |
| `StoryManagerQuestNode` | `.Conditions` |
| `LeveledItem` / `LeveledNpc` entry | `.Conditions` |
| `DialogInfo` (DialogResponses) | `.Conditions` |
| `Scene` | `.Conditions` |
| `PackIn` | (no conditions — handled by quest alias) |
