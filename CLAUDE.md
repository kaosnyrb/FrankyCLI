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
| `docs/formlib/scripts.md` | Vanilla Default* script catalogue — alias events, counter system, TopicInfo scripts, map markers, C# wiring pattern with ScriptProperty subtypes |
| `docs/formlib/conditions.md` | Condition record — ConditionFloat vs ConditionGlobal, RunOnType, FormLinkOrIndex assignment gotcha, confirmed types (GetStageDone, GetStage, GetIsID, HasKeyword, GetGlobalValue, GetInFaction), IsInLocation missing from Mutagen, clone-and-patch pattern |
| `docs/formlib/objectives.md` | Quest stages (QuestStage, QuestLogEntry, CompleteQuest flag) + objectives (QuestObjective, QuestObjectiveTarget, compass targets, Alias token syntax, Papyrus SetObjectiveDisplayed pattern) |
| `docs/formlib/messages.md` | Message (MESG) record — Description/Name fields, MessageBox flag, BNAM must clone, MenuButtons, MessageNoun pattern, two template FormIDs (0x000844 notification, 0x0008BA 2-button dialog) |
| `docs/formlib/aliases.md` | Quest aliases — QuestReferenceAlias (ForcedReference/UniqueActor/CreateReferenceToObject fill strategies, Flags), QuestLocationAlias (SpecificLocation/ALPS), QuestCollectionAlias, VMA.Aliases vs Quest.Aliases split, Property.Object self-reference gotcha, template alias ID families |
| `docs/formlib/locations.md` | Location (LCTN) record — key fields, parent→child hierarchy, creating from scratch, cell→location linkage, confirmed keyword FormIDs (LocTypeDungeon/Clearable/Overlay/LocEnc*), vanilla lookup by FormID, SpecificLocation vs PCM keyword fill |
| `docs/formlib/placed_npc.md` | PlacedNpc (ACHR) — required fields, Persistent flag, Base/PersistentLocation/Location post-construction pattern, PlacementUtil routing (Temporary/Persistent), LvlHumanHostile_ NPC pool FormIDs, boss wiring (LocDungeonBossLocRef + MasterSpecialReferences), terrain Z sampling |
| `docs/formlib/formlist.md` | FormList (FLST) — Items list type, construction pattern, crew/gang pool, waypoint marker collection, slot-to-content mapping, template copy pattern (no master dependency), lookup by EditorID/FormKey, XMarker FormID (0x3B), gang list EditorIDs |
| `docs/formlib/weapon_upgrade.md` | WeaponModification (OMOD) upgrade chain — OMOD property types (Int/Float/Enum/KeywordFloat/Include/Group), blueprint Book unlock, ConstructibleObject recipe gating, LeveledItem split distribution, modgroup loot injection, attach point mapping, level styles, data-driven YAML config |

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
