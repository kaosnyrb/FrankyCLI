# Quest From Scratch — Design Spec

Creating a new Quest record (with VirtualMachineAdapter scripts and Aliases) entirely in C#,
without a pre-built CK template. Supplements the existing `QuestNoun` clone-based pattern.

---

## Problem

Every mission quest type requires a hand-built CK template (with scripts and aliases
pre-wired). Adding a new variant means CK work first. We want C# to be the single
source of truth.

---

## Architecture of a Quest Record

A Starfield quest has two parallel data structures that must stay in sync:

### 1. `Quest.Aliases` — gameplay alias definitions

Each alias is a polymorphic `AQuestAlias` subtype:

| Subtype | Purpose |
|---|---|
| `QuestReferenceAlias` | A tracked game object (actor, object, ship) |
| `QuestLocationAlias` | A tracked location |
| `QuestCollectionAlias` | A filtered collection of references |

Key fields shared by all alias types:
- `ID` — `int`, zero-indexed. **Must match the alias's position** in `VMA.Aliases`.
- `Name` — `string`, readable label (e.g., `"BountyTarget"`).

`QuestReferenceAlias` fill strategies (mutually exclusive — set only one):
- `ForcedReference` — `IFormLink<IPlacedGetter>`, pin to a specific placed ref
- `UniqueActor` — `IFormLink<INpcGetter>`, fill with the unique NPC of this base form
- `CreateReferenceToObject.Object` — `IFormLink<...>`, create an instance at runtime

`QuestLocationAlias` fill strategies:
- `SpecificLocation` — `IFormLinkNullable<ILocationGetter>` — pin to a known location
- `ALPS.PcmTypeKeyword` — `IFormLinkNullable<IKeywordGetter>` — PCM keyword filter

### 2. `Quest.VirtualMachineAdapter` (`QuestAdapter`) — Papyrus bindings

Key fields:

| Field | Type | Role |
|---|---|---|
| `Version` | `short` | Papyrus VM format version — **copy from vanilla** |
| `ObjectFormat` | `short` | Object link format — **copy from vanilla** |
| `ExtraBindDataVersion` | `short` | Fragment binding format — **copy from vanilla** |
| `Scripts` | `ExtendedList<ScriptEntry>` | Quest-level Papyrus scripts |
| `Script` | `ScriptEntry` | Auto-generated fragment script (see below) |
| `Fragments` | `ExtendedList<QuestScriptFragment>` | Stage/objective auto-fragments |
| `Aliases` | `ExtendedList<QuestFragmentAlias>` | VMA-side alias bindings (one per alias slot) |

#### `ScriptEntry`

One entry per attached Papyrus script:
- `Name` — `string`, e.g. `"duout_space_bounty_quest"`
- `Flags` — `ushort`, typically `0`
- `Properties` — `ExtendedList<ScriptProperty>` (see below)

#### Script property types

| C# type | `.psc` type | Constructor |
|---|---|---|
| `ScriptObjectProperty` | `Quest`, `Actor`, `FormList`, etc. | `new ScriptObjectProperty { Name="...", Flags=0, Object=link }` |
| `ScriptIntProperty` | `Int` | `new ScriptIntProperty { Name="...", Flags=0, Data=0 }` |
| `ScriptBoolProperty` | `Bool` | `new ScriptBoolProperty { Name="...", Flags=0, Data=false }` |
| `ScriptFloatProperty` | `Float` | `new ScriptFloatProperty { Name="...", Flags=0, Data=0f }` |
| `ScriptStringProperty` | `String` | `new ScriptStringProperty { Name="...", Flags=0, Data="" }` |

`Flags=0` = non-const, non-array (the vanilla `Auto Const Mandatory` properties end up `Flags=1` after compile — confirm by inspecting the template).

#### `QuestFragmentAlias` (VMA-side)

There is **one entry per alias slot** in `Quest.Aliases`. The index in `VMA.Aliases` must match `Quest.Aliases[i].ID`.

- `Version`, `ObjectFormat` — same values as the parent VMA
- `Property` — `ScriptObjectProperty` with `Object` set to **the quest's own FormLink** (self-reference, used by Papyrus to resolve the owning quest at runtime)
- `Scripts` — `ExtendedList<ScriptEntry>` — scripts attached to this specific alias (usually empty unless the alias has its own event handlers)

After construction, call `SetScriptAlias(aliasIndex, quest.ToLink<IStarfieldMajorRecordGetter>())` to fill `Property.Object` on all fragment aliases.

---

## Script Inventory

All scripts live in `Starfield/Data/scripts/Source/`. They are pre-compiled; we just reference them by name.

### `duout_space_bounty_quest` — used by: `duout_info_space_informant`
Kill a bounty NPC in space.
```
Properties (Const Mandatory):
  GangMembers        : FormList
  MinGangMembers     : Int
  MaxGangMembers     : Int
  DeathItems         : FormList
  BountyTarget       : ReferenceAlias    ← alias Name="PrimaryRef" (ID=14 in space quest family)
```
Completion: `BountyTarget.OnDeath` → `SetStage(100)` → `CompleteQuest()`

### `duout_space_activator_quest` — used by: `duout_info_space_destroy`
Destroy/activate an object or ship in space.
```
Properties (Const Mandatory):
  GangMembers        : FormList
  MinGangMembers     : Int
  MaxGangMembers     : Int
  BountyTarget       : ReferenceAlias    ← alias Name="PrimaryRef" (ID=14 in space quest family)
```
Completion: manual (caller sets stage via activator script)

### `duout_ground_bounty_quest` — used by: `duout_info_planet_*`, `duout_info_dungeon_*`, `duout_info_city_*`
Kill an NPC or activate an object on a planet/dungeon/city.
```
Properties (Const Mandatory):
  GangMembers        : FormList
  MinGangMembers     : Int
  MaxGangMembers     : Int
  DeathItems         : FormList          ← omitted for activator variants (optional)
  BountyTarget       : ReferenceAlias    ← alias Name="BountyTarget" (ID=2 in planet quest family)
```
Completion: `BountyTarget.OnDeath` → `SetStage(100)` → `CompleteQuest()`

### `duout_space_station_quest` — used by: `duout_info_space_station`
Board a station, retrieve item.
```
Properties:
  GangMembers, MinGangMembers, MaxGangMembers (Const Mandatory)
  DeathItems         : FormList
  Corpses            : FormList
  SmallMarker        : Form              ← ShipMarker_SmallItem static FormKey
  NPCMarker          : Form              ← ShipMarker_CombatTargetChainMarker static FormKey
  EnemyShipInteriorLocation : LocationAlias  ← alias ID=7
  BountyTarget       : ReferenceAlias    ← alias ID=14 (PrimaryRef)
```

### `duout_space_derelict_quest` — used by: `duout_info_space_derelict`
Explore a derelict ship, retrieve item.
```
Properties:
  GangMembers, MinGangMembers, MaxGangMembers (Const Mandatory)
  DeathItems, Corpses : FormList
  EnemyShipInteriorLocation : LocationAlias  ← alias ID=7
  CrewSpawnMarkers   : RefCollectionAlias    ← alias ID=20
  ItemSpawnMarkers   : RefCollectionAlias    ← alias ID=8
  BountyTarget       : ReferenceAlias        ← alias ID=14 (PrimaryRef)
```

### `duout_branching_quest` (choice fork)
```
Properties:
  currentquest       : Quest
  nextquest_1        : Quest
  nextquest_2        : Quest
  messagetext        : Message
```

---

## VMA Version Values — CONFIRMED

Verified via `gen_inspect quest_vmad` against all template quests
(`duout_info_space_destroy`, `duout_info_space_informant`, guarded variants).
All consistent:

| Field | Value |
|---|---|
| `VMA.Version` | **6** |
| `VMA.ObjectFormat` | **2** |
| `VMA.ExtraBindDataVersion` | **3** |
| `QuestFragmentAlias.Version` | **6** (same as VMA) |
| `QuestFragmentAlias.ObjectFormat` | **2** (same as VMA) |

## Script Property Flags — CONFIRMED

| Context | Value |
|---|---|
| `ScriptEntry.Flags` (the script itself) | `0x0000` |
| `ScriptObjectProperty.Flags` (Const Mandatory) | `0x0001` |
| `ScriptIntProperty.Flags` (Const Mandatory) | `0x0001` |
| `QuestFragmentAlias.Property.Flags` | `0x0001` |

## VMA.Aliases — KEY INSIGHT

`VMA.Aliases` does **NOT** have one entry per `Quest.Alias`. It only contains entries for
aliases that have **their own scripts attached** (e.g. `DefaultAliasMapMarkerScript`).

All template quests have exactly **1** VMA alias entry — the `SpaceMapMarker` alias with
`DefaultAliasMapMarkerScript`:

```
VMA.Aliases[0]:
  Version=6, ObjectFormat=2
  Property.Name=""  Property.Flags=0x0001  Property.Object=<questFormKey>
  Scripts[0]: Name=DefaultAliasMapMarkerScript  Flags=0x0000
    Property: Name=MapMarkerCategory  Flags=0x0001  Value=0 (Int)
```

`Property.Object` = the **quest's own FormKey** (self-reference, always). After `DeepCopy()`,
this still points to the template's FormKey — the existing `SetScriptAlias(0, questSelfLink)`
call in the Investigation classes corrects it.

## Alias Property Object — KEY INSIGHT

For script properties referencing `ReferenceAlias` / `LocationAlias` in Papyrus,
**`ScriptObjectProperty.Object` = the quest's own FormKey**, not the alias itself.
Papyrus resolves the alias by matching the property name at runtime.

```
Scripts[0].Properties[0]:
  Name=BountyTarget  Flags=0x0001  Object=<questFormKey>   ← NOT the alias FormKey
```

## Quest.Alias ID Pattern — CONFIRMED (two families)

There are **two distinct alias ID families** based on quest type. IDs are not zero-indexed; they were assigned by CK at template creation time and must be preserved when cloning.

### Space quest family (duout_info_space_*)

| ID | Name | Type | Flags | Notes |
|---|---|---|---|---|
| 10 | TargetPlanetLocation | LocAlias | `0x00000109` | All space quests |
| 11 | SpaceCellRefs | ColAlias (inner ID=11) | — | LeveledSpaceCell collection |
| 12 | SpawnMarker01 | RefAlias | `0x00000000` | All space quests |
| 13 | PatrolMarker01 | RefAlias | `0x00000000` | All space quests |
| 14 | PrimaryRef | RefAlias | `0x00080100` | CreateRefTemp flag |
| 16 | SpaceMapMarker | RefAlias | `0x00004080` | Has `DefaultAliasMapMarkerScript` in VMA |
| 18 | GuardShip | RefAlias | `0x00080100` | Guarded variants only |
| 7 | EnemyShipInteriorLocation | LocAlias | `0x00010001` | Station/derelict only |
| 8 | ItemSpawnMarkers | ColAlias (inner ID=8) | — | Derelict only |
| 20 | CrewSpawnMarkers | ColAlias (inner ID=20) | — | Derelict only |

`VMA.Aliases[0]` = `SpaceMapMarker` (ID=16) with `DefaultAliasMapMarkerScript`, `MapMarkerCategory=0 (Int)`.

### Planet/Dungeon/City quest family (duout_info_planet_*, duout_info_dungeon_*, duout_info_city_*)

| ID | Name | Type | Flags | Notes |
|---|---|---|---|---|
| 0 | DungeonLocation | LocAlias | `0x00000109` (planet) / `0x00000108` (city) | PCM keyword filter on ALPS |
| 1 | BountyTargetMarker | RefAlias | `0x00000000` | |
| 2 | BountyTarget | RefAlias | `0x00080104` | CreateRefTemp + extra flag |
| 3 | targetPlanet | LocAlias | `0x40010100` | |
| 5 | dungeonMapMarker | RefAlias | `0x00000000` | Has `DefaultAliasMapMarkerScript` in VMA |

Note: ID=4 is absent — this gap is from the original CK template.

`VMA.Aliases[0]` = `dungeonMapMarker` (ID=5) with `DefaultAliasMapMarkerScript`, `MapMarkerCategory=0 (Int)`.

Script used: `duout_ground_bounty_quest` for all planet/dungeon/city types.
`DeathItems` property is omitted for activator variants (not set on quest, Papyrus treats as None).

### Branch quest (duout_info_branch)

No `Quest.Aliases` (0 entries). Script: `duout_branching_quest`. Properties: `currentquest`, `nextquest_1`, `nextquest_2`, `messagetext`.

### For from-scratch quests

Use these exact IDs so that downstream code using `SetQuestReferenceAlias("PrimaryRef", ...)` etc. continues to work unchanged.

## Fragment Script — CONFIRMED (vestigial)

All template quests share the same fragment script inherited from vanilla:
- `vma.Script.Name` = `"Fragments:Quests:QF_SQ_TreasureMap_Surface_Lo_00045F48"`
- `Fragments[0]`: Stage=100, ScriptName=same, FragmentName=`Fragment_Stage_0100_Item_00`

This is vestigial — carried from the original CK copy-source. The actual quest completion
is handled by the attached Papyrus script, not by the fragment. **Quests work correctly
in-game despite this mismatch.** For from-scratch quests, copy the same fragment reference
(by cloning from the template's VMA) to avoid introducing a new unknown.

---

## Proposed Implementation

### New class: `QuestFromScratch`

Location: `Retrograde.Library/Nouns/QuestFromScratch.cs`

```csharp
public class QuestFromScratch
{
    private readonly Quest _quest;
    private readonly StarfieldMod _targetMod;
    private readonly List<AQuestAlias> _aliases = new();
    private readonly List<ScriptEntry> _scripts = new();

    public QuestFromScratch(string editorId, Quest.Flag flags = Quest.Flag.StartGameEnabled)
    {
        _targetMod = RetrogradeContext.Current.TargetMod;
        _quest = new Quest(_targetMod)
        {
            EditorID = editorId,
            Data = new QuestData { Flags = flags, Type = Quest.TypeEnum.None },
        };
        _quest.Aliases = new ExtendedList<AQuestAlias>();
        _targetMod.Quests.Add(_quest);
    }

    /// Add a reference alias. Returns the alias ID (zero-indexed).
    public int AddReferenceAlias(string name, QuestReferenceAliasFlag flags = 0)

    /// Add a location alias. Returns the alias ID.
    public int AddLocationAlias(string name)

    /// Add a Papyrus script to the quest VMA.
    public void AddScript(string scriptName, IEnumerable<ScriptProperty> properties)

    /// Seal: build QuestAdapter and wire all QuestFragmentAlias.Property.Object = quest self-link.
    public QuestNoun Build()
}
```

### Integration

`QuestFromScratch.Build()` returns a `QuestNoun` (wrapping `_quest`) so all existing
`SetScriptProperty`, `SetQuestReferenceAlias`, etc. methods continue to work unchanged.

### Factory helpers

Add factory methods per quest type:

```csharp
// Retrograde.Library/Nouns/QuestFactory.cs
public static QuestNoun CreateSpaceBountyQuest(string editorId,
    IFormLink<IFormListGetter> gangMembers,
    int minGang, int maxGang,
    IFormLink<IFormListGetter> deathItems);

public static QuestNoun CreateGroundBountyQuest(string editorId, ...);

public static QuestNoun CreateSpaceActivatorQuest(string editorId, ...);
```

Each factory:
1. Creates `QuestFromScratch`
2. Adds the correct aliases
3. Adds the script with all property values pre-filled
4. Calls `Build()`
5. Adds standard objective (index 10, text passed in)
6. Returns the `QuestNoun`

---

## Outstanding Unknowns

All major unknowns resolved by `gen_inspect quest_vmad`. Remaining open question:

1. **Fragment Script for from-scratch quests**: The safest approach is to copy the VMA
   (including its fragment script) from the template by calling `template.VirtualMachineAdapter.DeepCopy()`,
   then replace only `Scripts` and `Aliases` entries. This avoids introducing new unknown state.
   Alternatively, test with `vma.Script = null` and empty `Fragments` to see if the game
   crashes at stage 100. The vestigial vanilla script reference is likely a no-op in practice.

---

## Migration Path

Existing template-based quests (`duout_info_space_destroy` etc.) continue unchanged.
New from-scratch quests are a parallel path — no rework of existing code needed.

Priority order for implementation:
1. `SpaceActivator` (simplest — fewest properties, no death items)
2. `SpaceBounty`
3. `GroundBounty`
4. `SpaceStation` / `SpaceDerelict` (complex — location alias + ref collection aliases)
