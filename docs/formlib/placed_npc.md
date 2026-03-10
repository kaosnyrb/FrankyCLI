# PlacedNpc (ACHR)

A `PlacedNpc` record places an NPC_ (character) into a cell. It is the NPC equivalent of `PlacedObject` — same cell-routing and Persistent/Temporary rules, different record type.

See `docs/formlib/placed_object.md` for field copying and transform rules that apply to both.

---

## Required fields

| Field | Type | Notes |
|---|---|---|
| `Position` | `P3Float` | World-space XYZ. See coordinate system in `docs/formlib/worldspace.md`. |
| `Rotation` | `P3Float` | Optional — omit for default (0,0,0). |
| `StarfieldMajorRecordFlags` | `StarfieldMajorRecord.StarfieldMajorRecordFlag` | Must include the `Persistent` bit for worldspace NPCs. See below. |
| `Base` | `IFormLinkNullable<INpcGetter>` | The NPC_ record this placement uses. Set **after** construction. |
| `PersistentLocation` | `IFormLinkNullable<ILocationGetter>` | The location that "owns" this NPC. Required for quest targeting and map markers. Set **after** construction. |
| `Location` | `IFormLinkNullable<ILocationGetter>` | Same value as `PersistentLocation` in standard placement. Set **after** construction. |

---

## Persistent flag

All worldspace NPCs must carry the Persistent flag, or they will not load correctly in-game:

```csharp
const StarfieldMajorRecord.StarfieldMajorRecordFlag PersistentFlag =
    (StarfieldMajorRecord.StarfieldMajorRecordFlag)PlacedObject.DefaultMajorFlag.Persistent;
```

---

## Construction pattern (worldspace)

```csharp
var npc = new PlacedNpc(targetMod)
{
    StarfieldMajorRecordFlags = PersistentFlag,
    Position = new P3Float(posX, posY, posZ),
};
// Set FormLink fields AFTER construction
npc.Base               = new FormKey(sfEsm, npcFormId).ToNullableLink<INpcGetter>();
npc.PersistentLocation = state.Location.FormKey.ToNullableLink<ILocationGetter>();
npc.Location           = state.Location.FormKey.ToNullableLink<ILocationGetter>();

// Route to the correct sub-cell
state.PlacementUtil.AddToTemporary(cell, npc);
```

---

## PlacementUtil routing

| Method | Cell target | Use for |
|---|---|---|
| `AddToTemporary(cell, npc)` | Sub-cell temporary group | Regular enemies and non-persistent NPCs |
| `NPCAddToTemporary(cell, npc)` | Sub-cell temporary group | Alias for the above, specific to PlacedNpc |
| `AddToPersistent(npc)` | TopCell persistent group | Boss NPCs — must survive across cell loads |
| `NPCAddToPersistent(cell, npc)` | Sub-cell persistent group (station) | Station boss NPCs — also sets `LevelModifier = Medium` |

**Boss NPCs go into Persistent** — regular enemies go into Temporary.

`NPCAddToPersistent` also sets `placedNpc.LevelModifier = Level.Medium` automatically.

---

## Cell routing for worldspace NPCs

Given a world-space XY position (overlay units):

```csharp
int cellX = (int)MathF.Floor(posX / 100f);
int cellY = (int)MathF.Floor(posY / 100f);
if (!state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var cell))
{
    Console.WriteLine($"WARNING: no sub-cell at ({cellX},{cellY}) — skipping NPC");
    continue;
}
```

---

## LvlHumanHostile NPC pool (Starfield.esm)

These are **NPC_ records** (not LeveledNpc containers). The "Lvl" in the EditorID means their stats scale with the player's level via Starfield's built-in NPC leveling — not via a LVLN record.

| FormID | EditorID | Role |
|---|---|---|
| `0x00375AA4` | `LvlHumanHostile_Assault` | Standard assault enemy |
| `0x00375AA6` | `LvlHumanHostile_Charger` | Rushing melee type |
| `0x00375AA5` | `LvlHumanHostile_Boss` | Boss variant — use with `LocationRefTypes` |
| `0x00375AB9` | `LvlHumanHostile_Heavy` | Heavy weapons type |
| `0x00375ABA` | `LvlHumanHostile_Recruit` | Weak/recruit tier |
| `0x00375ABB` | `LvlHumanHostile_Sniper` | Long-range type |
| `0x00375ABD` | `LvlHumanHostile_Support` | Support/medic type |

All are in `Starfield.esm`. Randomise from the non-boss pool for regular enemies; use the boss ID only for the boss placement.

---

## Boss placement — LocationRefType and MasterSpecialReferences

The boss NPC needs two extra steps so the game registers it as the dungeon boss:

### 1. Add `LocationRefTypes` to the PlacedNpc

```csharp
// LocDungeonBossLocRef [LCRT:00003956]
private static readonly uint BossLocRefFormId = 0x00003956;

boss.LocationRefTypes =
[
    new FormKey(starfieldEsm, BossLocRefFormId).ToLink<ILocationReferenceTypeGetter>(),
];
```

### 2. Register in `Location.MasterSpecialReferences`

```csharp
state.Location.MasterSpecialReferences ??= new ExtendedList<LocationCellStaticReference>();
state.Location.MasterSpecialReferences.Add(new LocationCellStaticReference
{
    LocationRefType = new FormKey(starfieldEsm, BossLocRefFormId).ToLink<ILocationReferenceTypeGetter>(),
    Marker          = boss.FormKey.ToLink<IPlacedGetter>(),
    Location        = bossCell.FormKey.ToLink<IComplexLocationGetter>(),
    Grid            = new P2Int16((short)cellX, (short)cellY),
});
```

If the boss's cell is not found in `CellLookup`, log a warning and skip the `MasterSpecialReferences` entry — the NPC still exists but won't be compass-targeted as a boss.

`state.BossPlacedNpc` stores the reference so downstream quest passes can point a `BountyTarget` alias at it.

---

## Known LocationReferenceType FormIDs (Starfield.esm)

| FormID | EditorID | Used for |
|---|---|---|
| `0x00003956` | `LocDungeonBossLocRef` | Marks boss — required for quest targeting |

---

## Terrain height sampling

For worldspace Z position, sample the BTD if available:

```csharp
float posZ = state.TerrainHeight;   // fallback flat value
if (state.BtdFile != null)
    posZ = state.BtdFile.SampleHeightAtWorld(posX * (4096f / 100f), posY * (4096f / 100f)) / 8f;
```

See `docs/formlib/worldspace.md` — `SampleHeightAtWorld` takes BTD-internal coords (multiply overlay X/Y by `4096/100`); divide result by 8 for overlay Z.

---

## Station NPC placement (interior cells)

Station passes use `NPCAddToTemporary(state.instance, placedNpc)` where `state.instance` is the interior cell (a single `Cell`, not a sub-cell lookup). No `PersistentLocation` / `Location` wiring is needed — interior cells inherit the location from the cell record itself.

```csharp
var placedNpc = new PlacedNpc(RetrogradeContext.Current.TargetMod)
{
    Rotation = markerRot,
    Position = markerPos,
};
placedNpc.Base = npcFormKey.ToNullableLink<INpcGetter>();
state.PlacementUtil.NPCAddToTemporary(state.instance, placedNpc);
```

---

## Gotchas

- **`Base`, `PersistentLocation`, `Location` must be set after construction** — all are FormLink structs.
- **Both `PersistentLocation` and `Location` must be set** for worldspace NPCs — omitting either breaks quest alias fill and map marker display.
- **Persistent flag is mandatory** for worldspace NPCs — without it the NPC won't appear.
- **`LvlHumanHostile_*` are NPC_ records, not LVLN records** — don't confuse them with leveled NPC container lists (LVLN). The player-level scaling is baked into the NPC template.
- **Boss must use `AddToPersistent`** not `AddToTemporary` — if it goes into Temporary it may be unloaded when the player is far away and the quest can't resolve its alias.
