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

## LGT_ lighting PackIns vs Static + Light

Starfield provides two approaches for placing lights in a PackIn cell. **Which to use depends on render context:**

### When placing in a room PackIn's cell directly (generators, rg_sts_trk_shl_ pattern)

Use **Static mesh + companion Light record** placed as separate Temporary objects in the cell:

```
LightUtility_A01On  (2ACD6C:Starfield.esm)  — wall-mounted utility light mesh (Static)
<Light record>                               — provides actual illumination
```

Both sit directly in the room PackIn's Temporary list → both render when the prefab is previewed or placed. This is what the example rooms (`rg_sts_trk_shl_001`) do.

**Do NOT use `LGT_*` PackIns here.** An `LGT_*` PackIn placed inside a room PackIn is a sub-PackIn. Its internal Light record is one extra nesting level deep and is **not rendered** when the outer room prefab is previewed.

### When placing directly in a worldspace cell (not inside another PackIn)

`LGT_*` PackIns work correctly — the bundled mesh + Light record render in full.

### LGT_ PackIn catalogue (for direct worldspace use)

**SciInt ceiling/wall light panels:**

| FormID | EditorID |
|--------|----------|
| `1A5FC0` | `LGT_SciIntAddOn_LightPanel_A01` |
| `1A5FBD` | `LGT_SciIntAddOn_LightPanel_A02` |
| `1A5FB9` | `LGT_SciIntAddOn_LightPanel_A03` |
| `1A5FB6` | `LGT_SciIntAddOn_LightPanel_B01` |
| `1A5FAD` | `LGT_SciIntAddOn_LightPanel_B02` |
| `1A5FB0` | `LGT_SciIntAddOn_LightPanel_C01` |
| `1A5FB3` | `LGT_SciIntAddOn_LightPanel_C02` |
| `1A5F9F` | `LGT_SciIntAddOn_LightPanel_D02` |

Internal cell of `LGT_SciIntAddOn_LightPanel_A01` (`1A6134`): contains `SciIntAddOn_LightPanel_A01_On` mesh (`2A40E2`) + one Light record (`1B29C5`).

**General utility wall lights:**

| FormID | EditorID |
|--------|----------|
| `1A6092` | `LGT_LightUtility_A02` |
| `1A6096` | `LGT_LightUtility_A03` |
| `1A6083` | `LGT_LightUtility_A06b` |
| `1A608F` | `LGT_LightUtility_A06On` |
| `1A6020` | `LGT_LightUtility_B01` |
| `1A6017` | `LGT_LightUtility_B02` |
| `1A600E` | `LGT_LightUtility_B03` |
| `1A6008` | `LGT_LightUtility_C01` |

## SciIntRmSm room tile kit

**IMPORTANT: These are Statics, not PackIns.** Unlike the HallSm kit (which nests PackIns inside PackIns), room-sized enclosures place Static meshes directly into the room PackIn's Temporary list. There is no recursive unpacking.

All Statics from `Architecture\ScienceKit\Interiors\RoomSmall\`:

### Perimeter wall Statics

| FormID | EditorID | Role |
|--------|----------|------|
| `024C99` | `SciIntRmSmMidFull01` | Full mid wall (dominant — used 9–21× per room) |
| `024CA3` | `SciIntRmSmWallMid01` | Wall mid v1 |
| `042C1F` | `SciIntRmSmWallMid02` | Wall mid v2 |
| `024CA7` | `SciIntRmSmWallMid_ScktA01` | Wall mid + socket A |
| `024CA9` | `SciIntRmSmWallMid_ScktB01` | Wall mid + socket B |
| `024CAA` | `SciIntRmSmWallMid_ScktC01` | Wall mid + socket C |
| `024CA6` | `SciIntRmSmWallTransLg_ScktA01` | Large transition + socket A |
| `024CA8` | `SciIntRmSmWallTransLg_ScktB01` | Large transition + socket B |
| `024C9A` | `SciIntRmSmWallCorIn01` | Inside corner |
| `024C9B` | `SciIntRmSmWallCorIn_ScktA_Dbl01` | Inside corner + double socket A |
| `024C9C` | `SciIntRmSmWallCorIn_ScktA_L01` | Inside corner + socket A (left) |
| `024C9D` | `SciIntRmSmWallCorIn_ScktA_R01` | Inside corner + socket A (right) |
| `024C9E` | `SciIntRmSmWallCorOut01` | Outside corner |

### Partition wall Statics (internal dividers)

All from `Architecture\ScienceKit\Interiors\PartitionSmall\`:

| FormID | EditorID | Role |
|--------|----------|------|
| `0563A5` | `SciIntParSmWallA_MidFull01` | Partition mid full |
| `0563A6` | `SciIntParSmWallA_MidMed01` | Partition mid medium |
| `0563A7` | `SciIntParSmWallA_MidSm01` | Partition mid small |
| `0563A3` | `SciIntParSmWallA_MidFull_ExSm01` | Partition mid full extra-small |
| `0563A4` | `SciIntParSmWallA_MidFull_Win01` | Partition mid full with window |
| `0563A2` | `SciIntParSmWallA_CorInSm_R01` | Partition inside corner small (R) |

Design patterns and room catalog: see `designlib/sci_room.md`.

---

## Generators

`Retrograde.Library/RoomPackinGeneration/SciHallwayGenerator.cs` — parametric straight-corridor generator using the SciIntHallSm PackIn kit.

```csharp
var gen = new SciHallwayGenerator(targetMod, sfModKey);
gen.Generate("my_hallway", flatTilesStart: 2, stairCount: 3, flatTilesEnd: 2);
// → PackIn + Cell written to targetMod
```

`Retrograde.Library/RoomPackinGeneration/SciRoomGenerator.cs` — parametric 20×20 room generator using the SciIntRmSm Static kit. Statics placed directly in cell (no sub-PackIn nesting).

```csharp
var room = new SciRoomGenerator(targetMod, sfModKey);
room.Generate("my_room", exitSouth: true, exitNorth: true, exitEast: false, exitWest: false);
// → PackIn + Cell written to targetMod
```

Run standalone: `dotnet run -- gen_roompackin`
