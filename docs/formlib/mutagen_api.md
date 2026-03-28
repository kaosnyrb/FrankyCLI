# Mutagen API Patterns

Cross-cutting patterns for reading and writing Starfield records with Mutagen.Bethesda.

## Required usings

```csharp
using Mutagen.Bethesda;                  // ToLink<T>(), ToNullableLink<T>() extension methods
using Mutagen.Bethesda.Plugins;          // FormKey, ModKey
using Mutagen.Bethesda.Plugins.Records;  // IFormLinkContainerGetter (for EnumerateFormLinks)
using Mutagen.Bethesda.Starfield;        // All Starfield record types
using Noggog;                            // ExtendedList<T>, P2Int, P3Float, etc.
```

`using Mutagen.Bethesda;` is the most commonly forgotten. Without it, `ToLink<T>()` and `ToNullableLink<T>()` are not found — even if `Mutagen.Bethesda.Plugins` is present.

## FormLink patterns

### Creating a FormKey

```csharp
var formKey = new FormKey(ModKey.FromFileName("Starfield.esm"), 0x00000043);
// or from a known mod key:
var formKey = new FormKey(sfModKey, 0x02447F);
```

### ToLink vs ToNullableLink

```csharp
// Non-nullable (required reference)
IFormLink<IStaticGetter> link = formKey.ToLink<IStaticGetter>();
// or from a record:
IFormLink<IPlaceableObjectGetter> link = record.ToLink<IPlaceableObjectGetter>();

// Nullable (optional reference)
IFormLinkNullable<ICellGetter> link = cell.ToNullableLink<ICellGetter>();
```

### IFormLinkNullable is a struct — never null

`IFormLinkNullableGetter<T>` is a **value type**. When the link is unset, `.IsNull = true` and `.FormKey = FormKey.Null`. Comparing to C# `null` always returns false; assigning C# `null` crashes inside Mutagen's `SetTo`.

```csharp
// Correct check:
if (!source.Emittance.IsNull)
    placed.Emittance = source.Emittance.FormKey.ToNullableLink<IEmittanceGetter>();

// Wrong — do not do this:
placed.Emittance = source.Emittance?.FormKey.ToNullableLink<IEmittanceGetter>(); // BAD
placed.Emittance = source.Emittance.IsNull ? null : ...;                         // BAD
```

### Set FormLinks after construction, not in initializer

```csharp
// Correct:
var packin = new PackIn(targetMod) { EditorID = "..." };
packin.Cell = cell.ToNullableLink<ICellGetter>(); // after construction

// Wrong:
var packin = new PackIn(targetMod)
{
    Cell = cell.ToNullableLink<ICellGetter>(), // BAD — crashes at runtime
};
```

## Cloning an entire record from starfieldMod (IStarfieldModGetter)

`starfieldMod` (the literal `Starfield.esm` reference) is an `IStarfieldModGetter`. Every property it exposes returns a getter interface (e.g. `ICellGetter`), not a mutable type. Attempting to copy fields directly causes **CS0266** (cannot convert `IFooGetter` to `Foo`).

**Fix: call `.DeepCopy()` on the entire record first**, then copy fields from the fully mutable result:

```csharp
// Find the source getter
ICellGetter? sourceGetter = null;
foreach (var block in starfieldMod.Cells)
    foreach (var sub in block.SubBlocks)
        foreach (var c in sub.Cells)
            if (c.FormKey.ID == 0x00138C3E) { sourceGetter = c; break; }

// DeepCopy() → fully mutable Cell; safe to read all fields
Cell srcCell = sourceGetter.DeepCopy();

// Now copy into new record (struct copies are safe in initializer)
var newCell = new Cell(targetMod)
{
    Music     = srcCell.Music,      // direct struct copy — ok
    Lighting  = srcCell.Lighting,   // complex sub-record already mutable via DeepCopy
};
```

Same pattern for `PlacedObject` entries inside a cell's Persistent/Temporary lists:

```csharp
foreach (var poRef in srcCell.Persistent)
{
    var src = poRef.DeepCopy(); // mutable PlacedObject
    var po  = new PlacedObject(targetMod) { Position = src.Position, ... };
}
```

## Cloning from getter types (template mods)

Template mods are `IStarfieldModGetter` and return getter interfaces. Conversion table when copying into a mutable record:

| Source type | Assignment pattern |
|---|---|
| `int`, `float`, `bool`, `enum`, `P3Float` | Assign directly |
| `IFormLinkNullableGetter<T>` | `if (!src.Foo.IsNull) dest.Foo = src.Foo.FormKey.ToNullableLink<T>();` |
| `IFormLinkGetter<T>` | `dest.Foo = src.Foo.FormKey.ToLink<T>();` |
| Complex sub-record (`IFooGetter`) | `dest.Foo = src.Foo?.DeepCopy();` |
| `IReadOnlyList<IFooGetter>` | `dest.Foos = src.Foos?.Select(x => x.DeepCopy()).ToExtendedList();` |
| `IReadOnlyList<IFormLinkGetter<T>>` | `dest.Foos = src.Foos?.ToExtendedList();` (compatible types) |
| `ReadOnlyMemorySlice<byte>?` | `dest.Foo = src.Foo?.ToArray();` |

## Creating new records

`new T(targetMod)` allocates a **fresh FormKey** but does **not** add the record to the mod. You must call `targetMod.RecordGroup.Add(record)` separately.

```csharp
var light = new Light(targetMod) { EditorID = "...", Radius = 300 };
targetMod.Lights.Add(light); // REQUIRED
```

## Filtering records by mod source

```csharp
if (source.Base.FormKey.ModKey.Name != "Starfield") return null; // skip non-Starfield bases
```

## Importing foreign base records

When a placed object's `Base` points to a template mod record (not Starfield.esm), clone it into the target mod before placing:

```csharp
private static FormKey EnsureLightImported(ILightGetter source, StarfieldMod targetMod)
{
    var existing = targetMod.Lights.FirstOrDefault(l => l.EditorID == source.EditorID);
    if (existing != null) return existing.FormKey;

    var copy = new Light(targetMod) { EditorID = source.EditorID, Radius = source.Radius };
    targetMod.Lights.Add(copy);
    return copy.FormKey;
}
```

Same pattern for `Statics`, `Activators`, `ImageSpaces`, etc. See `PlacementUtil.cs` (`EnsureBaseImported`, `EnsureLightImported`) and `CellTools.cs` (`EnsureImageSpaceImported`).

## Inspecting unknown Mutagen types with ilspycmd

When you don't know what properties a Mutagen type has, decompile the DLL directly. This is much faster than searching NuGet docs.

### Find the installed DLL

```bash
ls "C:/Users/kaosn/.nuget/packages/mutagen.bethesda.starfield/"
# → 0.53.1  (or whatever version is installed)
```

Path: `C:/Users/kaosn/.nuget/packages/mutagen.bethesda.starfield/<version>/lib/net8.0/Mutagen.Bethesda.Starfield.dll`

### Decompile a specific type

```bash
ilspycmd "C:/Users/kaosn/.nuget/packages/mutagen.bethesda.starfield/0.53.1/lib/net8.0/Mutagen.Bethesda.Starfield.dll" \
  -t "Mutagen.Bethesda.Starfield.SpaceshipAIActorComponent"
```

### Find the Mutagen class name from a CK name

CK record types follow the pattern `BGSFoo_Component` → Mutagen class `FooComponent` (strip `BGS` prefix and `_` separator).

```bash
ilspycmd "...Mutagen.Bethesda.Starfield.dll" -l type 2>&1 | grep -i "spaceshipai"
```

## Quest VirtualMachineAdapter (QuestAdapter) — confirmed values

Verified across all `duout_info_*` template quests via `gen_inspect quest_vmad`:

| Field | Value |
|---|---|
| `VMA.Version` | `6` |
| `VMA.ObjectFormat` | `2` |
| `VMA.ExtraBindDataVersion` | `3` |
| `QuestFragmentAlias.Version` | `6` |
| `QuestFragmentAlias.ObjectFormat` | `2` |
| Script entry `Flags` | `0x0000` |
| Property `Flags` (Auto Const Mandatory) | `0x0001` |

**VMA.Aliases** = only aliases with their OWN scripts. Template quests have 1 entry
(`SpaceMapMarker` → `DefaultAliasMapMarkerScript`). Most alias slots have no VMA entry.

**ScriptObjectProperty for alias properties**: `Object` = **quest's own FormKey** (not the alias).
Papyrus resolves alias by name at runtime.

**Quest.Alias ID scheme** — two families, both non-zero-indexed:
- *Space quests*: `ID=10` TargetPlanetLocation, `ID=11` SpaceCellRefs(Col), `ID=12` SpawnMarker01, `ID=13` PatrolMarker01, `ID=14` PrimaryRef, `ID=16` SpaceMapMarker(VMA), `ID=18` GuardShip; station/derelict add `ID=7` EnemyShipInteriorLocation, `ID=8/20` collection aliases.
- *Planet/dungeon/city quests*: `ID=0` DungeonLocation, `ID=1` BountyTargetMarker, `ID=2` BountyTarget, `ID=3` targetPlanet, `ID=5` dungeonMapMarker(VMA). ID=4 is absent (CK gap).
See `formlib/quest_from_scratch.md` for full tables.

**Fragment Script**: vestigial vanilla reference (`QF_SQ_TreasureMap_Surface_Lo_00045F48`).
Safe to copy as-is from template VMA. Actual quest logic is in the attached Papyrus script,
not in stage fragments. See `formlib/quest_from_scratch.md` for full spec.

## Scanning all FormLinks in a record (dependency detection)

`IFormLinkContainerGetter` + `EnumerateFormLinks()` — Mutagen 0.53.1 API. Requires `using Mutagen.Bethesda.Plugins.Records;`.

```csharp
foreach (var rec in mod.EnumerateMajorRecords())
{
    if (rec is not IFormLinkContainerGetter container) continue;
    foreach (var link in container.EnumerateFormLinks())
    {
        if (link.FormKey.IsNull) continue;
        if (templateModKeys.Contains(link.FormKey.ModKey))
            // found a template dependency
    }
}
```

**Old name `ContainedFormLinks` no longer exists in 0.53.1** — use `EnumerateFormLinks()`.

## Safe enumeration — skipping broken records

`EnumerateMajorRecords()` throws mid-iteration on some Starfield.esm Keywords (`BGSAdaptiveTriggerData_Component`). Noggog's `.Catch<T>()` extension does **not** resolve on Mutagen's return type (type mismatch at compile time). Use a manual iterator instead:

```csharp
// Add: using Mutagen.Bethesda.Plugins.Records;

static IEnumerable<IMajorRecordGetter> EnumerateSafe(
    IEnumerable<IMajorRecordGetter> source, string label)
{
    using var en = source.GetEnumerator();
    while (true)
    {
        bool moved;
        try { moved = en.MoveNext(); }
        catch (Exception ex)
        {
            Console.WriteLine($"[Skipped broken record in {label}]: {ex.Message}");
            continue; // stream has advanced past the bad record — try next
        }
        if (!moved) yield break;
        yield return en.Current;
    }
}
```

After catching, `continue` works because Mutagen's binary overlay enumerator advances its stream position before throwing, so the next `MoveNext()` moves to the following record.

## Quest Conditions — ConditionGlobal vs ConditionFloat

When a quest condition compares against a global (e.g. `GetDistanceGalacticParsec < distanceGlobal`), the condition type is **`ConditionGlobal`**, not `ConditionFloat`. The global FormKey sits on **`ComparisonValue`**, not inside `Data`:

```csharp
// ConditionGlobal — global is the RHS comparison value
if (cond is ConditionGlobal cg)
{
    var fk = cg.ComparisonValue.FormKey;   // property: ComparisonValue
    cg.ComparisonValue.SetTo(localFormKey); // SetTo() mutates in-place ✓
}

// ConditionFloat — global is a function argument (e.g. GetGlobalValue)
if (cond is ConditionFloat cf && cf.Data is GetGlobalValueConditionData gvData)
{
    var fk = gvData.FirstParameter.Link.FormKey;
    gvData.FirstParameter = new FormLinkOrIndex<IGlobalGetter>(gvData, localFormKey);
}
```

Both appear in `QuestLocationAlias.Conditions` as well as `IQuestReferenceAlias.Conditions` — check both when deep-copying quests from template mods.

## Scene.Index and Scene.SCPI — top-level topic ordering

For interactive dialogue scenes (flag `0x2000` Top Level), Starfield orders menu options by `Scene.Index` and `Scene.SCPI`. Both must be set.

| Field | Mutagen type | Notes |
|---|---|---|
| `Scene.Index` | `uint?` | Menu sort order — lower = higher in menu |
| `Scene.SCPI` | `MemorySlice<byte>?` | **2 bytes (ushort) only** — runtime error if 4 bytes written |

**Priority convention (confirmed in NPCDialogueNoun):**
- Mainline progression scenes: `SCPI = BitConverter.GetBytes((ushort)100)` — shown first
- Color/side scenes: `SCPI = BitConverter.GetBytes((ushort)0)` — shown after

**Index convention:** assign in increments of 10 starting at 0 across all topic scenes in the quest:
```csharp
topicScene.Index = (uint)(i * 10);
topicScene.SCPI  = BitConverter.GetBytes((ushort)100);
```

Scenes without `Scene.Index` set may sort unpredictably.

## Namespace / folder naming hazard

Avoid naming namespaces or folders the same as Mutagen Starfield record types (e.g. `Worldspace`, `Cell`, `Location`). This causes `CS0118: 'X' is a namespace but is used like a type`.

If a clash is unavoidable, add a using alias:

```csharp
using SfWorldspace = Mutagen.Bethesda.Starfield.Worldspace;
```
