# Worldspace (WRLD) — Overlay Worldspaces

Dungeon POI worldspaces in Starfield are **overlay worldspaces**: they reference a parent planet surface worldspace and have their own small cell grid floating on top of terrain.

## Overlay coordinate system

Overlay worldspaces use **100 overlay units per cell** for PlacedObject X/Y positions. This is separate from the BTD-internal coordinate system.

| | BTD internal | Overlay worldspace (PlacedObject X/Y) |
|---|---|---|
| Cell size | 4096 units | **100 units** |
| Vertex spacing | 32 units | 100/128 ≈ 0.78125 units |
| Z (height) | 8×-scaled | divide by 8 |

```csharp
float overlayX = btdX * (100f / 4096f);
float placedZ  = btd.SampleHeightAtWorld(btdWorldX, btdWorldY) / 8f;
```

## Cell grid

Cell coordinates span from `-(gridSize/2)` to `(gridSize/2 − 1)`. A 4×4 BTD gives cells −2..1 on each axis, spanning [−200, +200] overlay units.

| Cell (0,0)    | spans X/Y [0, 100) |
|---|---|
| Cell (−1,−1)  | spans X/Y [−100, 0) |
| Cell assignment | `floor(worldPos / 100f)` |

**PlacedObject positions are absolute overlay coordinates**, not cell-relative. Cell assignment is purely for spatial streaming. Do NOT subtract the cell origin from X/Y.

The cell grid size is derived from the template SurfaceBlock's `DNAM.First` in `WorldspaceNoun` — it is **not** a property on `IWorldspaceDesign`.

## Tile-to-cell assignment

In per-cell passes, skip tiles that belong to a different cell:

```csharp
int tileCellX = (int)Math.Floor(worldX / 100f);
int tileCellY = (int)Math.Floor(worldY / 100f);
if (tileCellX != state.CurrentCellPos.X || tileCellY != state.CurrentCellPos.Y)
    continue;
```

With a 50×50 map and `TileWorldSize=4`, total extent is 200 overlay units → cells −1..0 around origin.

## Cross-cell object routing

When unpacking prefabs, individual objects may land outside the tile's cell. Use `CellLookup`:

```csharp
int cellX = (int)Math.Floor(worldPos.X / 100f);
int cellY = (int)Math.Floor(worldPos.Y / 100f);
if (state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var cell))
    return cell;
return state.CurrentCell; // fallback
```

`WorldspaceState.CellLookup` is built by `WorldspaceDungeonGenerator` from all SubCells before per-cell passes run.

## Mutagen cell hierarchy for exterior worldspace cells

Overlay worldspace cells are stored in a **WorldspaceBlock → WorldspaceSubBlock → Cell** hierarchy (distinct from interior cell blocks):

```csharp
// WorldspaceNoun creates one block/subblock per cell:
var wsBlock = new WorldspaceBlock
{
    BlockNumberX = cellX,
    BlockNumberY = cellY,
    GroupType    = GroupTypeEnum.ExteriorCellBlock,
    SubBlocks    = new ExtendedList<WorldspaceSubBlock>(),
};
var wsSubBlock = new WorldspaceSubBlock
{
    BlockNumberX = 0,
    BlockNumberY = 0,
    GroupType    = GroupTypeEnum.ExteriorCellSubBlock,
    Cells        = new ExtendedList<Cell>(),
};
wsSubBlock.Cells.Add(cell);
wsBlock.SubBlocks.Add(wsSubBlock);
worldspace.SubCells.Add(wsBlock);
```

## Sampling terrain height for object placement

```csharp
var btd = new BtdFile(btdPath);
float terrainHeight = btd.SampleHeightAtWorld(0, 0) / 8f;
// terrainHeight → PlacedObject Z base for all tile placements
```

`WorldspaceNoun` sets `WorldspaceState.TerrainHeight`; `TileInstantiationPass` uses it as the Z base.

## Worldspace Components

Overlay worldspaces have components including `WorldSpaceOverlayComponent` which links to the SurfaceBlock:

```csharp
var overlayComp = worldspace.Components
    .OfType<IWorldSpaceOverlayComponentGetter>().First();
var surfaceBlockFormKey = overlayComp.SurfaceBlock.FormKey;
```

## Key files

- `WorldspaceNoun.cs` — creates worldspace record, subcell grid (from template DNAM), SurfaceBlock, samples terrain height
- `IWorldspaceDesign.cs` — `TemplateWorldspaceEditorId` (single source of truth for terrain setup)
- `WorldspaceDungeonGenerator.cs` — builds `CellLookup`, iterates cells for per-cell passes
- `TileInstantiationPass.cs` — dynamic tile-to-cell routing, cross-cell `ResolveCell()`
- `WorldspacePlacementUtil.cs` — placement helpers with `PlacedObject` and `PlacedNpc` overloads

## Gotchas

- **Do not hardcode cell quadrant bounds** — derive them from the cell grid size at runtime
- **PlacedObject X/Y are absolute**, not relative to cell origin — `ResolveCell()` routes to the right cell for streaming but the coordinate is always world-absolute
- **BTD `SampleHeightAtWorld` takes BTD-internal coords** (4096-unit scale), not overlay coords — always divide result by 8 for PlacedObject Z
- **Starfield BTDs are always centred at 0** — `btd.WorldCenterX = 0`, no offset correction needed
