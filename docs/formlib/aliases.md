# Quest Aliases

A quest's alias system has **two parallel structures** that must stay in sync:

- `Quest.Aliases` — the gameplay-side alias definitions (`AQuestAlias` list)
- `Quest.VirtualMachineAdapter.Aliases` — the Papyrus-side script bindings (`QuestFragmentAlias` list)

These are **not the same list** and do not have the same count. See the VMA section below.

---

## Alias types — `AQuestAlias` subtypes

All alias types share two fields:

| Field | Type | Notes |
|---|---|---|
| `ID` | `int` | Unique identifier within the quest. Assigned by CK. **Not necessarily zero-indexed.** |
| `Name` | `string` | Readable label used to find the alias by name in script properties. |

### `QuestReferenceAlias` — a tracked game object

Tracks an actor, placed object, or ship reference. Fill strategies are mutually exclusive — set exactly one:

| Fill strategy | Property | Notes |
|---|---|---|
| Pin to specific placed ref | `ForcedReference` — `IFormLink<IPlacedGetter>` | Static assignment. Object must already exist in the world. |
| Fill with unique NPC | `UniqueActor` — `IFormLink<INpcGetter>` | Finds the unique NPC with this base form. Use `SetTo(npcFormKey)`. |
| Create at runtime | `CreateReferenceToObject.Object` — `IFormLink<...>` | Instantiates a new ref when the quest starts. Requires `CreateRefTemp` flag. |

Other key fields:

| Field | Type | Notes |
|---|---|---|
| `Flags` | `QuestReferenceAlias.Flag` | See flag table below. **Clone from template unless building from scratch.** |
| `Conditions` | `ExtendedList<Condition>` | When to fill / validate this alias. Used for space location matching. |

#### `QuestReferenceAlias.Flag` — known values from template quests

| Hex value | Used by | Meaning |
|---|---|---|
| `0x00000000` | SpawnMarker, BountyTargetMarker | Default — no special behaviour |
| `0x00080100` | PrimaryRef (ID=14), GuardShip (ID=18) | `CreateRefTemp` — alias creates object at runtime |
| `0x00080104` | BountyTarget (ID=2, planet family) | `CreateRefTemp` + extra flag |
| `0x00004080` | SpaceMapMarker (ID=16) | Map marker alias |

**When cloning a template quest, these flags are preserved automatically.** For from-scratch construction, use `0x00000000` for ForcedReference/UniqueActor aliases and `0x00080100` for CreateReferenceToObject aliases.

---

### `QuestLocationAlias` — a tracked location

Tracks a location record. Fill strategies:

| Fill strategy | Property | Notes |
|---|---|---|
| Pin to specific location | `SpecificLocation` — `IFormLinkNullable<ILocationGetter>` | Set via `CastedAlias.SpecificLocation = value.ToNullableLink<ILocationGetter>()` |
| PCM keyword filter | `ALPS.PcmTypeKeyword` — `IFormLinkNullable<IKeywordGetter>` | Picks a location by PCM content type at runtime |

#### Known `QuestLocationAlias.Flags` values from template quests

| Hex value | Used by |
|---|---|
| `0x00000109` | TargetPlanetLocation (space), DungeonLocation (planet) |
| `0x40010100` | targetPlanet (planet family, ID=3) |
| `0x00010001` | EnemyShipInteriorLocation (station/derelict) |

---

### `QuestCollectionAlias` — a filtered collection of refs

Used for things like spawn marker pools and crew lists. Contains a `Collection` of inner reference aliases (`QuestCollectionAliasEntry`), each of which can have its own `CreateReferenceToObject` and `MaxInitialFillCount`.

Not typically constructed from scratch — always cloned from a template.

---

## The two alias structures — critical distinction

### `Quest.Aliases` (gameplay side)

The `AQuestAlias` list. One entry per alias in any order. IDs do **not** need to be sequential — they are whatever the CK assigned at template creation time.

```csharp
// Iterating aliases — always type-check
foreach (var alias in quest.Aliases)
{
    if (alias is QuestReferenceAlias refAlias) { ... }
    else if (alias is QuestLocationAlias locAlias) { ... }
    else if (alias is QuestCollectionAlias colAlias) { ... }
}
```

### `Quest.VirtualMachineAdapter.Aliases` (VMA side)

The `QuestFragmentAlias` list. **This is NOT one entry per `Quest.Aliases` entry.** It contains only aliases that have **their own Papyrus scripts attached** (e.g. `DefaultAliasMapMarkerScript`).

In practice, all mission template quests have exactly **1** VMA alias — the map marker alias with `DefaultAliasMapMarkerScript`.

```
VMA.Aliases[0]:
  Version=6, ObjectFormat=2
  Property.Name=""  Property.Flags=0x0001  Property.Object=<questFormKey>   ← self-reference
  Scripts[0]: Name=DefaultAliasMapMarkerScript  Flags=0x0000
    Property: Name=MapMarkerCategory  Flags=0x0001  Value=0 (Int)
```

The `Property.Object` field always points to the **quest's own FormKey** — this is a self-reference used by Papyrus to locate the owning quest. After `DeepCopy()`, this still points to the template's FormKey. The existing `SetScriptAlias(0, questSelfLink)` call in Investigation and Bounty quest classes corrects it.

---

## Script property Object — critical gotcha

When a Papyrus script has a property of type `ReferenceAlias` or `LocationAlias`, the `ScriptObjectProperty.Object` is **the quest's own FormKey**, not the alias:

```
Scripts[0].Properties[0]:
  Name=BountyTarget  Flags=0x0001  Object=<questFormKey>   ← NOT the alias FormKey
```

Papyrus resolves the alias at runtime by matching the property name against `Quest.Aliases[*].Name`. This is why the property name and the alias name must match exactly.

---

## Alias ID assignment

### Cloned quests (standard pattern)

IDs are preserved from the template. Never reassign `ID` on cloned aliases. Use `SetQuestReferenceAlias(name, ...)` to find and update an alias by name.

### Known template alias families

**Space quest family** (`duout_info_space_*`):

| ID | Name | Type |
|---|---|---|
| 7 | EnemyShipInteriorLocation | LocAlias |
| 8 | ItemSpawnMarkers | ColAlias |
| 10 | TargetPlanetLocation | LocAlias |
| 11 | SpaceCellRefs | ColAlias |
| 12 | SpawnMarker01 | RefAlias |
| 13 | PatrolMarker01 | RefAlias |
| 14 | PrimaryRef | RefAlias (CreateRefTemp) |
| 16 | SpaceMapMarker | RefAlias (has DefaultAliasMapMarkerScript) |
| 18 | GuardShip | RefAlias (guarded variants only) |
| 20 | CrewSpawnMarkers | ColAlias |

**Planet/Dungeon/City quest family** (`duout_info_planet_*`, `duout_info_dungeon_*`, `duout_info_city_*`):

| ID | Name | Type |
|---|---|---|
| 0 | DungeonLocation | LocAlias |
| 1 | BountyTargetMarker | RefAlias |
| 2 | BountyTarget | RefAlias (CreateRefTemp) |
| 3 | targetPlanet | LocAlias |
| 4 | *(absent — CK gap)* | — |
| 5 | dungeonMapMarker | RefAlias (has DefaultAliasMapMarkerScript) |

### From-scratch quests

Assign IDs sequentially starting from 0. Use the same IDs as the relevant template family if downstream code expects specific alias IDs (e.g. `SetScriptAlias(0, ...)`).

```csharp
var alias = new QuestReferenceAlias { ID = 0, Name = "NPC" };
alias.UniqueActor.SetTo(npcFormKey);
quest.Aliases ??= new ExtendedList<AQuestAlias>();
quest.Aliases.Add(alias);
```

---

## C# construction patterns

### Clone + patch (standard)

```csharp
var newQuest = new QuestNoun(templateFormId, questName);

// Patch by alias name
newQuest.SetQuestReferenceAlias("BountyTargetMarker", markerFormKey);
newQuest.SetQuestLocationAlias("DungeonLocation", location.ToNullableLink<ILocationGetter>());
newQuest.SetQuestReferenceCreateAlias("BountyTarget", activator.ToLink<...>());

// Correct the VMA self-reference after DeepCopy
newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
```

### `QuestNoun` alias methods

| Method | Operates on | Notes |
|---|---|---|
| `SetQuestReferenceAlias(name, formKey)` | `ForcedReference.FormKey` | Pin to existing placed ref |
| `SetQuestReferenceCreateAlias(name, link)` | `CreateReferenceToObject.Object` | Wire runtime-create alias |
| `SetQuestReferenceSpaceLocationAlias(name, condition)` | `Conditions[0]` | Space location condition |
| `SetQuestLocationAlias(name, link)` | `SpecificLocation` | Pin to known location |
| `SetQuestPCMTypeKeyword(name, keyword)` | `ALPS.PcmTypeKeyword` | PCM keyword fill |
| `SetQuestLevelledSpaceCellAlias(id, link)` | `QuestCollectionAlias.Collection[0]...Object` | Wire space cell |
| `SetScriptAlias(index, link)` | `VMA.Aliases[index].Property.Object` | Correct self-reference after clone |

### From scratch (reference alias with UniqueActor)

```csharp
var alias = new QuestReferenceAlias { ID = npcAliasId, Name = aliasName ?? "NPC" };
alias.UniqueActor.SetTo(npcFormKey);
quest.Aliases ??= new ExtendedList<AQuestAlias>();
quest.Aliases.Add(alias);
```

### Reading alias by name

```csharp
var refAlias = quest.Aliases
    ?.OfType<QuestReferenceAlias>()
    .FirstOrDefault(a => a.Name == "BountyTarget");
if (refAlias != null)
    refAlias.CreateReferenceToObject.Object = myLink;
```

---

## Conditions on aliases

Some reference aliases use `Conditions` to control where they fill — e.g. a space marker alias uses `GetIsInCurrentLocWithRef` (or a clone of a vanilla condition) to pin the spawn to the right space cell. Since `IsInLocation` condition types are missing from Mutagen, always clone from a vanilla quest:

```csharp
// From SpaceCellTools — clone-and-patch pattern
var sourceAlias = vanillaQuest.Aliases[Chosen.AliasID];
var condition = ((IQuestReferenceAliasGetter)sourceAlias).Conditions[0].DeepCopy();
newQuest.SetQuestReferenceSpaceLocationAlias("SpawnMarker01", condition);
```

See `docs/formlib/conditions.md` for the full clone-and-patch pattern and the list of condition types missing from Mutagen.

---

## Gotchas

- **VMA.Aliases count ≠ Quest.Aliases count** — VMA only contains aliases with scripts. Do not assume they are the same list.
- **IDs are not sequential** — template IDs were assigned by the CK and may have gaps (e.g. ID 4 is absent in the planet family). Never renumber them.
- **Property.Object = quest self-link** — Papyrus alias properties point to the quest, not the alias. After `DeepCopy()` always call `SetScriptAlias(0, quest.ToLink<...>())` to fix the stale template FormKey.
- **UniqueActor fill only works for unique NPCs** — if your NPC is not flagged Unique in the CK, `UniqueActor` will fail to fill at runtime. Use `ForcedReference` or `CreateReferenceToObject` instead.
- **`CreateReferenceToObject` needs the CreateRefTemp flag** — without the `0x00080100` flag on the alias, the runtime creation silently fails.
