# Locations (LCTN)

A `Location` record represents a named place in the game world — a dungeon, city district, planet POI, space cell, or ship interior. Quests reference locations through their alias system (see `docs/formlib/aliases.md`). The map, encounter zones, and PCM tree all depend on correct location records.

---

## Key fields

| Field | Type | Notes |
|---|---|---|
| `EditorID` | `string` | Required. Naming convention: `prefix_shortname_loc` |
| `Name` | `ITranslatedString` | Displayed in journal and map. Shown as the quest objective location. |
| `ParentLocation` | `IFormLinkNullable<ILocationGetter>` | Links this location into the hierarchy. Set **after** construction (FormLink struct rule). |
| `Keywords` | `ExtendedList<IFormLinkNullable<IKeywordGetter>>` | Controls encounter filters, PCM matching, type flags. Always initialize to `[]` in constructor, then `.Add()` after. |
| `WorldLocationRadius` | `uint` | Radius used to auto-detect when the player has "arrived" at the location. Set `0` for procedural overlay worldspaces. |
| `ActorFadeMult` | `float` | Typically `1`. |
| `LocationCellUniqueReferences` | `ExtendedList<LocationCellUniqueReference>` | Almost always constructed as an empty list. Required to be non-null on some record types. |

---

## Location hierarchy

Locations form a parent→child tree via `ParentLocation`. The CK / PCM system traverses up the tree to find a matching location for quests and content nodes.

```
StarSystem_Location
  └── Planet_Location
        └── Dungeon_Location (or City_Location, POI_Location …)
              └── Interior_Location (optional sub-level)
```

For procedurally generated content the relevant level is usually the **dungeon/POI** location — the one passed to `SetQuestLocationAlias`.

Station / ship hierarchy has three levels:
```
ShipExterior_loc
  └── ShipInterior_loc
        └── InteriorCell_loc
```

---

## Creating a location from scratch

```csharp
// 1. Construct — initialize Keywords in-block (safe because it's a list, not a FormLink)
var location = new Location(targetMod)
{
    EditorID          = prefix + "loc_" + shortName,
    Name              = displayName,
    Keywords          = [],
    WorldLocationRadius = 0,
    ActorFadeMult     = 1,
    LocationCellUniqueReferences = new ExtendedList<LocationCellUniqueReference>(),
};
targetMod.Locations.Add(location);

// 2. Set FormLink properties AFTER construction
location.ParentLocation = parentLocation.ToNullableLink();
```

Never set `ParentLocation` inside the constructor initializer block — `IFormLinkNullable<T>` is a struct and will crash inside Mutagen's `SetTo` if assigned there.

---

## Linking a cell to its location

```csharp
// Cell.Location is IFormLinkNullable — set post-construction
cell.Location = location.ToNullableLink<ILocationGetter>();
```

---

## Location keywords (Starfield.esm)

These FormIDs are confirmed from `WorldspaceNoun.cs`:

| EditorID | FormID (Starfield.esm) | Purpose |
|---|---|---|
| `LocTypeDungeon` | `0x000254BC` | Marks as a dungeon — required for most POI missions |
| `LocTypeClearable` | `0x00064EDE` | Marks location as clearable (shows cleared marker on map) |
| `LocTypeOE_Keyword` | `0x001A5468` | Generic overlay encounter keyword |
| `LocTypeOverlay` | `0x002CA99D` | Marks as an overlay worldspace location |
| `LocEncSpacers_Exclusive` | `0x00283585` | Spacer-exclusive encounter zone |
| `LocEncCrimsonFleet_Exclusive` | `0x00023305` | Crimson Fleet–exclusive |
| `LocEncEcliptic_Exclusive` | `0x00283581` | Ecliptic-exclusive |
| `LocEncHouseVaruun_Exclusive` | `0x00283580` | House Va'ruun–exclusive |

Custom per-dungeon keywords follow the pattern `LocTypeOE_<shortname>` and are created on demand if they don't already exist in the template or target mod.

---

## Looking up a vanilla location

When a mission template specifies a known city or POI location by FormID:

```csharp
// parameters["FormId"] is a hex literal stored as int — use Convert.ToUInt32
var locaform = RetrogradeContext.Current.StarfieldMod.Locations[
    new FormKey(RetrogradeContext.Current.StarfieldModKey,
    Convert.ToUInt32(missionTemplate.parameters["FormId"]))
];
newQuest.SetQuestLocationAlias("DungeonLocation", locaform.ToNullableLink<ILocationGetter>());
```

Use `gi.sh` to find a location's FormID:

```bash
bash c:/Git/FrankyCLI/scripts/gi.sh Location NeonCity
```

---

## SpecificLocation vs PCM keyword fill

These are the two fill strategies on a `QuestLocationAlias` (from `aliases.md`):

| Strategy | When to use | Call |
|---|---|---|
| `SpecificLocation` | Known at generation time (city quest, fixed POI) | `SetQuestLocationAlias("DungeonLocation", location.ToNullableLink<ILocationGetter>())` |
| `ALPS.PcmTypeKeyword` | Runtime PCM pick (dungeon of any matching type) | `SetQuestPCMTypeKeyword("DungeonLocation", keyword.ToNullableLink<IKeywordGetter>())` |

For `SpecificLocation`: pass a `Location` record from Starfield.esm or from the target mod.

For PCM keyword: pass a `Keyword` record — usually a `LocType*` keyword. The PCM system will pick a qualifying location at quest start time.

---

## Gotchas

- **`ParentLocation` must be set after construction** — it's a FormLink struct. Setting it inside `new Location(...) { ParentLocation = ... }` crashes.
- **`Keywords` is safe to initialize in-block** (`Keywords = []`) because it's an `ExtendedList` reference, not a FormLink. Add keywords via `.Add()` after construction.
- **`LocationCellUniqueReferences` must be non-null** if you're adding cell refs later — always initialize to `new ExtendedList<LocationCellUniqueReference>()`.
- **Cell.Location must be set after the cell is constructed** — same FormLink struct rule.
- **`Convert.ToUInt32()`** is required when reading FormIDs from `missionTemplate.parameters` — hex literals stored as `int` cannot be cast directly to `uint` after unboxing.
