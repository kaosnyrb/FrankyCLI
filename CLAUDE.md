# FrankyCLI

Starfield procedural dungeon generation tool using Mutagen.Bethesda.

## Form Library

`docs/formlib/` — detailed reference docs. Read the relevant file before working with an unfamiliar record type.

| File | Covers |
|------|--------|
| `docs/formlib/packin.md` | PackIn record, tile nesting, SciIntHallSm kit, SciIntRmSm kit, LGT_ lights, creating from scratch |
| `docs/formlib/placed_object.md` | PlacedObject fields, full copy field list, PlacedPrimitive, VolumeData, world transforms, cloning from getters |
| `docs/formlib/surface_block.md` | SurfaceBlock (SFBK) record + BTD binary format, terrain height, texture painting, coordinate systems |
| `docs/formlib/worldspace.md` | Overlay coordinate system, cell grid, tile-to-cell assignment, cross-cell routing |
| `docs/formlib/pcm.md` | Planet Content Manager tree — BranchNode, ContentNode, root hooks, creating entries |
| `docs/formlib/mutagen_api.md` | ToLink/ToNullableLink patterns, cloning from getters, ilspycmd, namespace hazards |
| `docs/formlib/book_audio.md` | Audio data-slate (BOOK) chain — Scene, DialogTopic, DialogResponses, WEM audio |
| `docs/formlib/space_cell.md` | Space Cell (CELL SpaceCell) + LeveledSpaceCell — structure, key FormIDs, content type rules, cloning pattern |
| `docs/formlib/ship.md` | GenericBaseForm (GBFM) encounter ship — safe clone fields, ObjectTemplateInstanceData gotcha, ExternalDataSource faction sources, LeveledBaseForm faction FormIDs |
| `docs/formlib/quest_from_scratch.md` | Quest built entirely in C# — VMA version values, alias ID families (space/planet), script inventory, fragment script, proposed QuestFromScratch class |

## Design Library

`docs/designlib/` — Bethesda design patterns reverse-engineered from vanilla content.

| File | Covers |
|------|--------|
| `docs/designlib/principles.md` | Philosophy: extract intent not measurements |
| `docs/designlib/sci_hallway.md` | SciIntHallSm corridor layout, lighting, decoration |
| `docs/designlib/sci_room.md` | SciIntRmSm room variants, connectors, archetypes |

## Critical Rules

### Mutagen nullable FormLink — set after construction, never in initializer

`IFormLinkNullable<T>` is a **struct** — never `null`. When unset, `.IsNull = true` and `.FormKey = FormKey.Null`. Assigning C# `null` crashes inside Mutagen `SetTo`.

```csharp
// Correct — set FormLink properties AFTER the constructor block:
var packin = new PackIn(targetMod) { EditorID = "..." };
packin.Cell = cell.ToNullableLink<ICellGetter>();

// Wrong — crashes at runtime:
var packin = new PackIn(targetMod) { Cell = cell.ToNullableLink<ICellGetter>() };
```

Guard nullable FormLink reads with `.IsNull`:

```csharp
if (!source.Emittance.IsNull)
    placed.Emittance = source.Emittance.FormKey.ToNullableLink<IEmittanceGetter>();
```

### Required usings for any file that places records

```csharp
using Mutagen.Bethesda;           // ToLink<T>(), ToNullableLink<T>() — most often forgotten
using Mutagen.Bethesda.Plugins;   // FormKey, ModKey
using Mutagen.Bethesda.Starfield; // All Starfield record types
using Noggog;                     // P3Float, ExtendedList
```

### new T(targetMod) does NOT add to the mod

`new T(targetMod)` allocates a fresh FormKey but does **not** register the record. Always call `targetMod.RecordGroup.Add(record)` separately.

### Namespace / folder naming hazard

Avoid naming namespaces or folders the same as Mutagen record types (`Worldspace`, `Cell`, `Location`, etc.) — causes `CS0118`. Add a using alias if unavoidable:

```csharp
using SfWorldspace = Mutagen.Bethesda.Starfield.Worldspace;
```
