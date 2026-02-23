# PackIn (PKNI)

A PackIn is a **prefab assembly record** — it acts as a named container for a set of pre-placed objects stored in a dedicated interior Cell. When the game (or a generator) "unpacks" a PackIn, it instantiates all the objects from that cell into the target worldspace or dungeon cell, applying a world-space transform.

## Key fields

| Field | Type | Notes |
|-------|------|-------|
| `EditorID` | string | Naming convention in du_outlaws_template: `rg_sts_trk_shl_001` etc. |
| `Cell` | `IFormLinkNullable<ICellGetter>` | The storage cell containing all placed objects |
| `ObjectBounds` | `ObjectBounds` | Approximate AABB of the prefab (First/Second P3Float). Used by CK for display only. |
| `MajorRecordFlagsRaw` | uint | `512` = Prefab flag. Starfield.esm base tiles also set `2048` (2560 total) |
| `FNAM` | `ReadOnlyMemorySlice<byte>?` | Binary metadata. Template PackIns have this set. Copy verbatim from an existing PackIn if needed; can omit for generated PackIns. |
| `NativeTerminal` | `IFormLinkNullable<T>` | Usually Null. Leave unset. |

## Mutagen type

```csharp
using Mutagen.Bethesda;           // ToLink, ToNullableLink
using Mutagen.Bethesda.Plugins;   // FormKey, ModKey
using Mutagen.Bethesda.Starfield; // PackIn, ICellGetter, etc.

// Read (from template mod)
foreach (var packin in mod.PackIns)
    Console.WriteLine($"{packin.EditorID}  cell={packin.Cell.FormKey}");

// Write (new record)
var packin = new PackIn(targetMod)
{
    EditorID            = "rg_gen_...",
    MajorRecordFlagsRaw = 512,
    ObjectBounds        = new ObjectBounds
    {
        First  = new P3Float(xMin, yMin, zMin),
        Second = new P3Float(xMax, yMax, zMax),
    },
};
packin.Cell = cell.ToNullableLink<ICellGetter>(); // FormLink — set AFTER construction
targetMod.PackIns.Add(packin);
```

## Inspecting with gen_inspect

```bash
# By EditorID prefix
dotnet run -- gen_inspect PackIn rg_sts_trk_shl_

# By FormID (hex)
dotnet run -- gen_inspect PackIn 0x024466
```

## The interior storage Cell

Each PackIn has a dedicated interior Cell (`Flags = Cell.Flag.IsInteriorCell`) whose EditorID follows the pattern `PackIn<EditorIdCamelCase>StorageCell`.

The cell has two object lists:

| List | Purpose |
|------|---------|
| `Persistent` | Game-logic markers: connectors (`rg_conn_*`), enemy spawns (`rg_enemy_spawn_*`) |
| `Temporary` | Visual / structural objects: tile PackIns, props, lights |

### Creating the Cell

Interior cells in Mutagen require a **block/subblock hierarchy**. Block and subblock numbers are derived from the last two decimal digits of the Cell's FormKey ID:

```csharp
int blockNum    = (int)(cell.FormKey.ID % 10);
int subBlockNum = (int)((cell.FormKey.ID / 10) % 10);

CellBlock? cellBlock = null;
foreach (var b in targetMod.Cells)
    if (b.BlockNumber == blockNum) { cellBlock = b; break; }
if (cellBlock == null)
{
    cellBlock = new CellBlock
    {
        BlockNumber = blockNum,
        GroupType   = GroupTypeEnum.InteriorCellBlock,
        SubBlocks   = new ExtendedList<CellSubBlock>(),
    };
    targetMod.Cells.Add(cellBlock);
}

CellSubBlock? subBlock = null;
foreach (var sb in cellBlock.SubBlocks)
    if (sb.BlockNumber == subBlockNum) { subBlock = sb; break; }
if (subBlock == null)
{
    subBlock = new CellSubBlock
    {
        BlockNumber = subBlockNum,
        GroupType   = GroupTypeEnum.InteriorCellSubBlock,
        Cells       = new ExtendedList<Cell>(),
    };
    cellBlock.SubBlocks.Add(subBlock);
}

subBlock.Cells.Add(cell);
```

## PackIns are assembled from nested PackIns

**Critical discovery from rg_sts_trk_shl_ analysis (2025-02):**

The `PlacedObject.Base` inside a PackIn's storage cell can itself be another PackIn FormKey (from Starfield.esm). The game unpacks recursively. This is how the Science Interior hallway rooms work — every structural "tile" is a nested Starfield.esm PackIn placed as a `PlacedObject`.

The `PlacedObject.Base` field (`IFormLink<IPlaceableObjectGetter>`) accepts PackIns, Statics, Activators, and other placeable types.

## SciIntHallSm tile kit

Starfield.esm PackIns for assembling Science Interior Hallway Small rooms (`Architecture\ScienceKit\Interiors\HallSmall\`):

| FormID | EditorID | Role |
|--------|----------|------|
| `02447F` | `SciIntHallSm1Way01__SC` | Straight segment v1 (4 units long) |
| `02446E` | `SciIntHallSm1Way02__SC` | Straight segment v2 (4 units long) |
| `024466` | `SciIntHallSm1WayStairs01__SC` | Staircase — rises **2 units Z per tile** |
| `024441` | `SciIntHallSmCapScktA01__SC` | End cap with socket |
| `0185DE` | `SciIntHallSm3Way01__SC` | T-junction (3-way) |
| `012CE8` | `SciIntHallSm1WayScktA01__SC` | Straight with socket A |

Each tile is centred on its placement point and occupies ±2 units on each side (4×4 footprint).

## Special marker Statics (Starfield.esm)

| FormID | EditorID | Type | Usage in PackIn cells |
|--------|----------|------|----------------------|
| `000034` | `XMarkerHeading` | Static | Connector marker — `Base` for `rg_conn_*` objects. Rotation Z encodes facing direction |
| `00003B` | `XMarker` | Static | Enemy spawn marker — `Base` for `rg_enemy_spawn_*` objects |
| `03F808` | `PrefabPackinPivotDummy` | Static | Root pivot — always placed at (0,0,0) as the first Temporary object in every PackIn |

## Straight corridor tile grammar (N-S, Y axis)

Used by `rg_sts_trk_shl_*` rooms:

```
[S connector]   Y = -6,          Z = southZ    rotation=(0,0,π)
[S end cap]     Y = -4,          Z = southZ    SciIntHallSmCapScktA01__SC
[flat tiles]    Y = 0,4,…        Z = southZ    alternating Way1/Way2
[stair tiles]   Y = …            Z = southZ + i*2   SciIntHallSm1WayStairs01__SC
[flat tiles]    Y = …            Z = northZ    alternating Way2/Way1
[N end cap]     Y = nCapY,       Z = northZ    SciIntHallSmCapScktA01__SC
[N connector]   Y = nCapY + 2,   Z = northZ    rotation=(0,0,0)
```

Where:
- `northZ = stairCount * 2`
- `nCapY = (flatStart + stairCount + flatEnd) * 4`
- Connectors sit 2 units past the end cap edge (each cap occupies ±2 around its centre Y)

## Connector EditorID convention

```
rg_conn_{direction}_{doorSize}_{tileset}_{uniqueIndex}
         n|s|e|w    D1|D2      station   001…
```

Example: `rg_conn_n_D1_station_011`

Parsed by `RgConnectorParser.Parse()` in `DataModels.cs`. Direction and door size are encoded in the EditorID; the `PlacedObject.Rotation.Z` on the `XMarkerHeading` base additionally encodes facing.

## Gotchas

- **`PackIn.Cell` is `IFormLinkNullable`** — set it after construction, not in the initializer block
- **`using Mutagen.Bethesda;` required** — `ToLink<T>()` and `ToNullableLink<T>()` are extension methods in this namespace; `Mutagen.Bethesda.Plugins` alone is not enough
- **Cell must be added to `targetMod.Cells` via the block/subblock hierarchy** — direct `targetMod.Cells.Add(cell)` is not valid; the cell lives inside `CellBlock → CellSubBlock → Cell`
- **`cell.Temporary` and `cell.Persistent` need explicit init** — `new ExtendedList<IPlaced>()` in the Cell constructor for both lists
- **`MajorRecordFlagsRaw = 512`** is the Prefab flag. Template mod PackIns use this. Starfield.esm base PackIns use `2560` (adds bit `0x800`)
- **Template-mod-referenced objects in cells** (e.g. `Base=000E8F:du_outlaws_template.esm`) are filtered out by `TileInstantiationPass` when unpacking — generated PackIns should only reference Starfield.esm objects to avoid master dependencies
- **FNAM** can be omitted in generated PackIns (leave null) — it's CK filter/metadata

## Generator

`Retrograde.Library/RoomPackinGeneration/SciHallwayGenerator.cs` — parametric straight-corridor generator using the SciIntHallSm kit.

```csharp
var gen = new SciHallwayGenerator(targetMod, sfModKey);
gen.Generate("my_hallway", flatTilesStart: 2, stairCount: 3, flatTilesEnd: 2);
// → PackIn + Cell written to targetMod
```

Run standalone: `dotnet run -- gen_roompackin`
