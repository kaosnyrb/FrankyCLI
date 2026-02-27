# Space Cell (CELL — SpaceCell)

Space Cells are standalone CELL records used as asteroid-field encounter cells. The game selects one randomly from a LeveledSpaceCell (LVSC) when it needs to spawn an orbit encounter.

## Record structure

| Part | Content |
|------|---------|
| `Cell.InteriorData.InteriorType` | `InteriorType.SpaceCell` — must be set |
| `Cell.Persistent` | Origin Point (ship spawn) + persistent patrol/combat markers |
| `Cell.Temporary` | Asteroids (MoveableStatic) + patrol markers (Static) + triggers |

## Key FormIDs

| Record | FormID | EditorID | Notes |
|--------|--------|----------|-------|
| Vanilla rocky source cell | `CELL:00138C3E` (Starfield.esm) | `scGenRocky04` | Clone base for rocky asteroid cells |
| Vanilla LVSC | `LVSC:001D8553` (Starfield.esm) | `LVLSC_Rocky` | Reference pool (we create standalone new ones) |
| SE_AreaTrigger | `ACTI:001DEE0C` (Starfield.esm) | `SE_AreaTrigger` | Present in Temporary — **exclude from palette and from cloned cell** |

## Content type rules (confirmed from scGenRocky04)

- **Asteroids** → base record is `MoveableStatic` — check with `starfieldMod.MoveableStatics.ContainsKey(formKey)`
- **Patrol/combat/ship markers** → base record is `Static` — everything else in Temporary that isn't excluded
- `SE_AreaTrigger` is an `Activator` in Temporary — skip it entirely; it manages encounter triggers and should not be cloned

## LeveledSpaceCell (LVSC)

```csharp
var lvsc = new LeveledSpaceCell(targetMod) { EditorID = "rg_lvsc_" + id };
lvsc.Entries = new ExtendedList<LeveledNpcEntry>
{
    new LeveledNpcEntry { Level = 1, Count = 1 }
};
lvsc.Entries[0].Reference.SetTo(cell.FormKey); // INpcSpawnGetter constraint — use FormKey overload
targetMod.LeveledSpaceCells.Add(lvsc);
```

`LeveledNpcEntry.Reference` is typed `IFormLink<INpcSpawnGetter>`. Cell does not implement that interface directly, but `SetTo(FormKey)` bypasses the generic type constraint at the binary level and works correctly in-game.

## Building a new Space Cell

1. Find source in `starfieldMod.Cells` by iterating `CellBlock → CellSubBlock → Cells` and checking `c.FormKey.ID == 0x00138C3E`
2. Call `sourceGetter.DeepCopy()` to get a fully mutable `Cell` — do not copy fields directly from the getter (CS0266)
3. Split `srcCell.Temporary`:
   - `AreaTrigger` (FormID `0x001DEE0C`) → skip
   - `MoveableStatic` base → add `FormKey` to asteroid palette
   - All other bases → deep-copy `PlacedObject` into `markerTemplates` list
4. Clone `Persistent` items with fresh FormKeys (deep-copy pattern — see `SpaceCellNoun.cs`)
5. Create new `Cell(targetMod)`, copy metadata fields, set `InteriorData.InteriorType = SpaceCell`
6. Run passes: `SpaceMarkersPass` (clone markers) → `AsteroidChainPass` (place asteroids)
7. Register with `AddCellToMod` (CellBlock/CellSubBlock logic — see `StationNoun.cs:256-291`)

## Vanilla radius

Measure at extraction time — max `Length(pos)` across all Temporary items:

```csharp
float vanillaRadius = 0f;
foreach (var po in srcCell.Temporary)
    if (po.Position != null)
        vanillaRadius = MathF.Max(vanillaRadius, po.Position.Value.Length());
```

## Generator pass architecture

```
SpaceCellGenerator
  ├── SpaceMarkersPass      — clones marker templates with fresh FormKeys
  ├── AsteroidChainPass     — asteroids along a random chain direction
  ├── CometTailPass         — parabolic tail + dense coma (45% chance)
  ├── ShipWreckPass         — derelict ship hulk + debris field
  ├── LargeAsteroidRingPass — oversized hero asteroid + orbiting ring (40% chance)
  └── CrescentBeltPass      — 150° arc belt, cosine-tapered scale (always fires — chance > 1.0)
```

State object: `SpaceCellState` — holds `Cell`, `Location`, `AsteroidPalette` (FormKey list), `MarkerTemplates` (PlacedObject list), `VanillaRadius`, `Scale` (default `sqrt(2)` = 2× area).

## Physics on large asteroids

Set `placed.XALG = 8uL` on any MoveableStatic that should be fixed in position (ship parts, hero asteroids). Vanilla floating asteroids have `XALG = null` (physics enabled). See `placed_object.md` Gotchas for the `DontHavokSettle` vs XALG distinction.
