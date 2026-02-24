# FrankyCLI

Starfield procedural dungeon generation tool using Mutagen.Bethesda.

## Form Library

`formlib/` — detailed reference docs for individual Starfield/Mutagen record types. Read the relevant file before working with an unfamiliar form type.

| File | Covers |
|------|--------|
| `formlib/packin.md` | PackIn structure, tile nesting, SciIntHallSm kit, creating from scratch, gotchas |
| `formlib/placed_object.md` | PlacedObject fields, copying, PlacedPrimitive, VolumeData, world transforms, cloning from getters |
| `formlib/surface_block.md` | SurfaceBlock (SFBK) record + BTD binary format, terrain height, texture painting, coordinate systems |
| `formlib/worldspace.md` | Overlay coordinate system, cell grid, tile-to-cell assignment, cross-cell routing |
| `formlib/pcm.md` | Planet Content Manager tree — BranchNode, ContentNode, root hooks, creating entries |
| `formlib/mutagen_api.md` | ToLink/ToNullableLink patterns, cloning from getters, ilspycmd, namespace hazards |

## PlacedPrimitives

PlacedPrimitives are invisible volume boxes/shapes used for trigger areas in Starfield. They're attached to `PlacedObject` records.

### Creating a PlacedPrimitive Box

```csharp
var placed = new PlacedObject(gen_quest_main.myMod)
{
    Count = 1,
    Position = new P3Float(x, y, z),
    Rotation = new P3Float(0, 0, 0),
    Base = activator.ToLink<IPlaceableObjectGetter>(),
    Primitive = new PlacedPrimitive()
    {
        Bounds = new P3Float(sizeX, sizeY, sizeZ),  // extents, not half-extents
        Color = Color.FromArgb(255, 100, 100),       // editor visualization color
        Type = PlacedPrimitive.TypeEnum.Box
    }
};

state.PlacementUtil.AddToTemporary(state.instance, placed);
```

### Key Points

- `Base` links to an `IActivatorGetter` that defines the trigger behavior (e.g., enemy alert types)
- `Bounds` is the full size of the box (not half-extents)
- `Type` can be `Box`, `Sphere`, etc.
- Use `PlacementUtil.AddToTemporary()` to add to the dungeon cell
- Activators are looked up from `gen_quest_main._StarfieldMod.Activators` by EditorID

### Enemy Alert Activators

Common alert activator EditorIDs:
- `DMP_Room_SandboxEngagedPreferredDefend` - Defend behavior (used for boss rooms)
- `DMP_Room_PreferredDefend`
- `DMP_Room_EngagedPreferred`
- `DMP_Room_Engaged`

## XVL2 Volume Data

PlacedObjects can have `VolumeData` for post-processing effects using `Xvl2VolumeData`.

### Post-Effect Volume Example

```csharp
var placed = new PlacedObject(gen_quest_main.myMod)
{
    Position = new P3Float(x, y, z),
    Base = postEffectStatic.ToLink<IPlaceableObjectGetter>(),
    Primitive = new PlacedPrimitive()
    {
        Bounds = new P3Float(sx, sy, sz),
        Color = Color.FromArgb(128, 200, 255),
        Type = PlacedPrimitive.TypeEnum.Box
    },
    VolumeData = new Xvl2VolumeData()
    {
        ImageSpace = imageSpaceFormKey.ToLink<IImageSpaceGetter>()
    }
};
```

### Key FormKeys

- `PostEffectVolume [STAT:00000043]` - Static used as base for post-effect volumes
- `LGT_LUT_SpaceStation_General_curve [IMGS:0015078D]` - Example ImageSpace for space station look

### Creating FormKeys for Starfield Records

```csharp
var formKey = new FormKey(ModKey.FromFileName("Starfield.esm"), 0x00000043);
```

## BTD Terrain Data

BTD files (`.btd`) store terrain heightmaps for Starfield and Fallout 76. Reader/writer: `Retrograde.Library/Utils/BtdFile.cs`.

### File Format (Version 5/6, Starfield)

- **Header** (40 bytes): magic `BTDB`, version (5 or 6), HeightMin/Max (floats, unscaled), ResX/ResY, CellMin/MaxX/Y
- **LTEX form IDs**: uint32 count + array of land texture form IDs (typically 6-7 shared across all Starfield BTDs)
- **Cell height min/max map**: 8 bytes/cell (float min, float max) — stored in **unscaled** coordinates (before 8x Starfield multiplier)
- **Land texture map**: 32 bytes/cell — 4 quadrant palettes of 8 bytes each (Q0=TL, Q1=TR, Q2=BL, Q3=BR; boundary at vertex 64). Each byte is an LTEX array index
- **Ground cover** (FO76 only, absent in Starfield): form IDs + 32 bytes/cell map
- **LOD4 height map**: 128 bytes/cell — 8x8 grid of uint16 (downsampled from 128x128, every 16th sample)
- **LOD4 land textures**: 128 bytes/cell
- **Vertex color LOD4** (FO76 only, absent in Starfield): 128 bytes/cell
- **Block tables**: LOD3→LOD2→LOD1→LOD0 order, each entry is 8 bytes (uint32 offset, uint32 compressed size). Starfield has 1 entry/block; FO76 has 2 entries/block for LOD3/2/0
- **Compressed block data**: zlib-compressed blocks, each LOD0 block = 65536 bytes:
  - Bytes 0–32767: height data (128x128 uint16)
  - Bytes 32768–65535: texture data (128x128 uint16)

### Texture Data

Each LOD0 block contains per-vertex texture data (128x128 uint16) in the second half of the decompressed block. The cell also has a **land texture map** (32 bytes in the metadata) and **LOD4 land textures** (128 bytes/cell) that influence rendering.

**Land texture map (32 bytes/cell):**
- 4 groups of 8 bytes — per-quadrant palettes mapping layer indices to LTEX form IDs
- Each byte is an index into the global LTEX form ID array
- Different groups can have different LTEX mappings, causing visible quadrant-based texture splits
- **Critical**: neighboring cells' palettes influence rendering at cell borders. To avoid quadrant splits, normalize all 4 groups to be identical via `SetCellLandTexMap` on **ALL cells** (including edge cells)
- Some entries may reference out-of-bounds LTEX indices (renders as default/wrong texture)

**Per-vertex uint16 encoding (partially understood):**
- The full uint16 value determines which texture is rendered — it is NOT a simple `layer << 12 | blend` encoding (that theory was disproven: different "layers" with the same sub-value produce identical textures)
- Value 0x0000 = no texture / transparent
- The value interacts with the land texture map palette, but the exact mapping is complex
- **Known working values** (tested on oebb008world.btd, palette `06 05 04 03 02 00 01 00`):
  - 0x0100 = "Base sandy" texture
  - 0x3000 = "slightly less sandy" texture
  - 0x4000 = "clean bright sand" texture
  - 0x6000 = "Clean orangy sand" texture
  - 0x0800, 0x1000, 0x2000, 0x7000 = all show "patchy sandy" (same texture despite spanning different `>> 12` ranges)
  - 0x0E00 = "patchy sandy" variant
  - 0x0FFF = "Clean orangy sand" (but had quadrant issues before full palette normalization)
  - Values with the same low 12 bits but different top 4 bits (e.g., all `N*4096+2048`) produce **identical** textures

**LOD4 land textures (128 bytes/cell):**
- Affects rendering; should be zeroed when painting custom textures to avoid interference
- Read/write via `GetCellLod4LandTex` / `SetCellLod4LandTex`

**Complete texture painting workflow:**
```csharp
var reader = new BtdFile(path);

// 1. Normalize ALL cells' land texture maps to avoid quadrant splits
var palette = new byte[32];
reader.GetCellLandTexMap(palette, centerCellX, centerCellY);
for (int q = 1; q < 4; q++)
    Array.Copy(palette, 0, palette, q * 8, 8);
for (int cy = reader.CellMinY; cy <= reader.CellMaxY; cy++)
    for (int cx = reader.CellMinX; cx <= reader.CellMaxX; cx++)
        reader.SetCellLandTexMap(palette, cx, cy);

// 2. Paint per-vertex texture data
var texBuf = new ushort[128 * 128];
for (int i = 0; i < texBuf.Length; i++)
    texBuf[i] = 0x4000; // "clean bright sand"
reader.SetCellTextureData(texBuf, cellX, cellY);

// 3. Zero LOD4 land textures for modified cells
reader.SetCellLod4LandTex(new byte[128], cellX, cellY);

// 4. Save
reader.Save(outputPath, updateMinMax: false);
```

Save automatically patches both height and texture data for dirty cells when `_dirtyTextures` has data.

### Key Concepts

- **Cells**: 128x128 vertices each
- **Tiles**: 8x8 groups of cells, the unit of decompression/caching
- **LOD levels**: LOD0 = 128x128, LOD1 = 64x64, LOD2 = 32x32, LOD3 = 16x16
- **Height encoding**: uint16 mapped linearly to `[WorldHeightMin, WorldHeightMax]`
- **Starfield detection**: all 4 cell boundary fields in header are zero; applies 8x height scale to min/max
- **Starfield cell bounds**: derived from resolution fields: `CellMinX = -(ResX >> 8)`, `CellCountX = ResX >> 7`; always centered at 0 so `WorldCenterX = 0`
- **Cell min/max metadata uses UNSCALED heights** — when writing, divide by 8 for Starfield files
- **Compression**: must use `ZLibStream` (not `DeflateStream`) — requires proper zlib header + Adler32 checksum
- **Typical terrain heights**: the test BTD (oebb008world) has terrain at ~15–84 world units in a -4000 to 8000 range. `HeightToRaw(0)` = 21845
- **BtdFile returns 8x-scaled heights** — `SampleHeightAtWorld()` and `RawToHeight()` return values in the Starfield 8x-scaled coordinate space (e.g. -101). `PlacedObject` positions use **unscaled** coordinates (e.g. -12.7). **Always divide BtdFile heights by 8** when using them for object placement: `btd.SampleHeightAtWorld(x, y) / 8f`

### BTD vs Overlay Worldspace Coordinate Systems

Two separate unit systems are in play — do not confuse them:

| | BTD internal | Overlay worldspace (PlacedObject X/Y) |
|---|---|---|
| Cell size | 4096 units | **100 units** |
| Vertex spacing | 32 units | 100/128 ≈ 0.78125 units |
| Z (height) | 8x-scaled | divide by 8 |

**Converting BTD position → overlay PlacedObject position:**
```csharp
float overlayX = btdX * (100f / 4096f);
// equivalently: overlayVertSpacing = 100f / BtdFile.CellResolution  (≈ 0.78125)
// overlayX = editMinX * 100f + globalVertexIndex * overlayVertSpacing
```

`BtdFile.SampleHeightAtWorld(worldX, worldY)` takes **BTD-internal coordinates** (4096-unit scale), not overlay coordinates. Use it only for Z sampling; convert the result to overlay Z by dividing by 8.

For overlay worldspaces, Starfield BTD cell bounds are always `CellMinX = -halfGrid .. CellMaxX = halfGrid-1`, centered at 0, so `btd.WorldCenterX = 0` and no centre-offset correction is needed.

### Edge Cells Are Off-Limits

The outermost ring of cells in a BTD connects the map to the rest of the world. **Never modify edge cells' height or per-vertex texture data.** For an NxN cell grid, only cells `(CellMinX+1..CellMaxX-1, CellMinY+1..CellMaxY-1)` are safe to edit. A 3x3 BTD has only 1 editable cell in the center.

**Exception**: the land texture map (32-byte palette) on edge cells CAN and SHOULD be normalized when painting textures, because neighboring cells' palettes influence rendering at cell borders.

### Writing BTD Files

The Save method copies the prefix (header + metadata + block tables) as a byte array, rebuilds compressed block data (dirty blocks are decompressed, patched, recompressed; clean blocks copied verbatim), patches block table entries with new offsets/sizes, then writes prefix + block data.

Key pitfalls discovered during development:
- **MemoryStream.GetBuffer() reallocation**: don't patch a buffer obtained from GetBuffer() then continue writing to the stream — use a separate byte[] for the prefix
- **Cell min/max must stay in unscaled coordinates** for Starfield (divide RawToHeight values by 8)
- **LOD4 height map must be updated** for dirty cells or CK may crash
- **`updateMinMax: false`** parameter on Save skips min/max and LOD4 metadata updates (useful when making small additive edits that stay within the original height range)

### Edge Smoothing

`SmoothDirtyCellEdges(bandWidth)` blends a transition band at borders between dirty and clean cells:
- Dirty cell side: lerps from original values at the edge toward modified values in the interior
- Neighbor cell side: propagates the height delta from the dirty edge, fading to zero
- Reads original data directly from file bytes (bypasses tile cache) to know the "before" state
- Automatically marks touched neighbor cells as dirty for Save

### Usage

```csharp
// Read height
var reader = new BtdFile(path);
float h = reader.GetHeight(cellX, cellY, vertX, vertY);
float h2 = reader.SampleHeightAtWorld(worldX, worldY);

// Read/write land texture map (32 bytes/cell, 4 quadrant palettes)
var palette = new byte[32];
reader.GetCellLandTexMap(palette, cellX, cellY);
reader.SetCellLandTexMap(palette, cellX, cellY);

// Read/write per-vertex texture (128x128 uint16)
var texBuf = new ushort[128 * 128];
reader.GetCellTextureData(texBuf, cellX, cellY);
reader.SetCellTextureData(texBuf, cellX, cellY);

// Read/write LOD4 land textures (128 bytes/cell)
var lod4Tex = new byte[128];
reader.GetCellLod4LandTex(lod4Tex, cellX, cellY);
reader.SetCellLod4LandTex(lod4Tex, cellX, cellY);

// Write height + texture (additive edit, skip metadata updates)
reader.SetCellHeightMap(heightBuf, cellX, cellY);
reader.SetCellTextureData(texBuf, cellX, cellY);
reader.SmoothDirtyCellEdges(32); // blend height seams before saving
reader.Save(outputPath, updateMinMax: false);

// Write (full edit, update all metadata)
reader.Save(outputPath); // updateMinMax defaults to true

// Conversion
ushort raw = reader.HeightToRaw(worldHeight);
float world = reader.RawToHeight(raw);
```

### Cross-File Findings (5 Starfield BTDs validated)

All BTDs in `Data\terrain\`: oedb508world, oejm008world, oejp008caveworld, oeob008world, oesd008world.

- **All Starfield**, same height range (-500 to 1000, span 1500), same LTEX form IDs (6-7 entries)
- **Versions**: 4 files v6, 1 file v5 (oejp008caveworld) — both parse identically as Starfield
- **Quadrant palette differences are normal** in vanilla files (e.g. oesd008world: only 3/16 cells uniform). Confirms palette normalization is necessary when painting
- **Default palette `[6,5,4,3,2,0,1,0]`** appears in nearly every file
- **0x0E00** is the most common texture value across 4/5 files
- **0x7000, 0x4000, 0x3000, 0x6000** all confirmed present across multiple files
- Cell grids range from 3x3 to 5x5; all have 0 empty LOD0 blocks

### Test Harnesses

- `gen_btd_test` — automated reader/writer verification (test_btd.bat)
- `gen_btd_flatten` — adds a cosine hill to the center cell for visual verification (flatten_btd.bat)
- `gen_btd_info` — dumps file structure, section layout, block stats, height distribution (info_btd.bat); pass a directory path to scan all BTD files (info_btd_all.bat)
- Test BTDs: `C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data\terrain\`

## Unpacking PackIn Prefab Contents

To eliminate dependencies on template library mods, PackIn prefabs can be unpacked — placing their individual Starfield.esm base forms directly instead of referencing the PackIn.

### Resolving a PackIn's Cell

PackIn records have a `Cell` property (FormLink). To get the cell contents from template mods:

```csharp
foreach (var mod in RetrogradeContext.Current.TemplateMods)
{
    var packin = mod.PackIns.FirstOrDefault(p => p.FormKey == packinFormKey);
    if (packin?.Cell.FormKey != null)
    {
        foreach (var block in mod.Cells)
            foreach (var subBlock in block.SubBlocks)
                foreach (var cell in subBlock.Cells)
                    if (cell.FormKey == packin.Cell.FormKey)
                        return cell; // ICellGetter
    }
}
```

### Cloning from Getter Types (Template Mods)

Template mods (`IStarfieldModGetter`) return getter interfaces (`IPlacedObjectGetter`, `ICellGetter`). When cloning from getters into a mutable record:

| Source type | Conversion pattern |
|---|---|
| Simple value (int, float, bool, enum, P3Float) | Assign directly |
| `IFormLinkNullableGetter<T>` | `source.Foo.FormKey.ToNullableLink<T>()` |
| `IFormLinkGetter<T>` | `source.Foo.FormKey.ToLink<T>()` |
| Complex sub-record (`IFooGetter`) | `source.Foo?.DeepCopy()` |
| `IReadOnlyList<IFooGetter>` | `source.Foos?.Select(x => x.DeepCopy()).ToExtendedList()` |
| `IReadOnlyList<IFormLinkGetter<T>>` | `source.Foos?.ToExtendedList()` (direct copy — types are compatible) |
| `ReadOnlyMemorySlice<byte>?` | `source.Foo?.ToArray()` |

### Eliminating Template Mod Dependencies for Base Records

When a `PlacedObject.Base` points to a non-Starfield record (e.g. a custom `Light` in a template mod), clone that base record into the target mod with a fresh FormKey before creating the placed object. See `EnsureBaseImported` / `EnsureLightImported` in `PlacementUtil.cs`.

Pattern for any new record type:

```csharp
// 1. Detect by checking the record group on each template mod
foreach (var tm in templateMods)
{
    if (tm.Lights.TryGetValue(baseFormKey, out var light))
        return EnsureLightImported(light, targetMod);
    // add more: tm.Statics, tm.Activators, etc.
}

// 2. Clone: check EditorID dedup, then new T(targetMod) + copy all properties
private static FormKey EnsureLightImported(ILightGetter source, StarfieldMod targetMod)
{
    var existing = targetMod.Lights.FirstOrDefault(l => l.EditorID == source.EditorID);
    if (existing != null) return existing.FormKey;

    var copy = new Light(targetMod) { EditorID = source.EditorID, Radius = source.Radius, /* ... */ };
    targetMod.Lights.Add(copy); // REQUIRED — new T(targetMod) only allocates a FormKey, does NOT add to the group
    Console.WriteLine($"[PlacementUtil] Imported Light {source.EditorID} → {copy.FormKey}");
    return copy.FormKey;
}
```

Same pattern applies to top-level records referenced from cells (e.g. `ImageSpace` in `CellTools.EnsureImageSpaceImported`).

### Filtering to Starfield.esm Only

```csharp
if (source.Base.FormKey.ModKey.Name != "Starfield") return null;
```

### World Transform for Unpacked Objects

Convert tile rotation (degrees) to yaw steps, then use `RgRotation`:

```csharp
int yawSteps = map.tiles[x][y].rotation / 90;
var rotatedLocal = RgRotation.RotateYaw90(source.Position, yawSteps);
var worldPos = tilePos + rotatedLocal;
var worldRot = source.Rotation + RgRotation.RotationToP3Float(yawSteps);
```

When unpacking from `PlacementUtil` (where the parent `PlacedObject` carries world rotation in radians):

```csharp
int yawSteps = (int)Math.Round(parent.Rotation.Z / (MathF.PI / 2f));
yawSteps = ((yawSteps % 4) + 4) % 4;
```

### Key Files

- `PlacementUtil.cs` — `EnsureBaseImported` / `EnsureLightImported` / `ClonePlacedObject` (full field copy from getter)
- `CellTools.cs` — `EnsureImageSpaceImported` (same pattern for top-level cell references)
- `TileInstantiationPass.cs` — worldspace prefab unpacking (iterates PackIn cell contents)
- `ShipMarkerPass.cs` — similar pattern for dungeon ship markers
- `ExitTopologyPass.cs` — similar pattern for dungeon exit prefabs
- `WorldspacePlacementUtil.cs` — has overloads for both `PlacedObject` and `PlacedNpc`

## SurfaceBlock Records (SFBK)

SurfaceBlock records define terrain data for worldspaces. Each links to a `.btd` file via the ANAM property.

### Key Properties

| Property | Purpose | Typical Value |
|----------|---------|---------------|
| `ANAM` | Path to BTD terrain file | `Data\TERRAIN\stbblock001.btd` |
| `DNAM` | Cell grid dimensions (First=cols, Second=rows) | `(4, 4)` |
| `ENAM` | Height range as raw floats (First=min, Second=max) | `(-500f, 1000f)` |
| `NAM1` | Family name | `"OverlayBlock"` |
| `NAM5` | Parent block link (Null = standalone) | `2C17D4:Starfield.esm` |
| `WHGT` | Water height | `float.MinValue` (unset) |
| `GNAM`–`KNAM` | Various flags/indices | `0` |
| `NAM2`–`NAM4` | Additional metadata | `0` / `(0,0)` |

### Two Categories

- **Standalone** (`NAM5 = Null`): Base terrain blocks for planet surfaces (e.g., `oebb008world`, `oesd008world`)
- **Overlay** (`NAM5 = parent link`): Used by POIs/worldspaces, reference a parent block. `NAM1 = "OverlayBlock"`

### Creating a SurfaceBlock for a New Worldspace

All SurfaceBlock properties are derived from the template worldspace (via `IWorldspaceDesign.TemplateWorldspaceEditorId`). `WorldspaceNoun` resolves the template worldspace → its `WorldSpaceOverlayComponent.SurfaceBlock` FormKey → the SurfaceBlock record, then reads:
- `ANAM` → source BTD filename to copy (always lowercase in Starfield)
- `FNAM` → required binary field copied verbatim
- `NAM5` → parent standalone SurfaceBlock link (reused unchanged)
- `DNAM.First` → cell grid size (replaces the old hardcoded `CellGridSize`)

BTD source files must be unpacked from `Starfield - Terrain*.ba2` into `Data\Terrain\` before running. `WorldspaceNoun` throws `FileNotFoundException` with an actionable message if they are missing.

```csharp
// WorldspaceNoun does this automatically — shown here for reference:
var templateWorldspace = FindInTemplateMods(templateMods, m => m.Worldspaces, design.TemplateWorldspaceEditorId);
var overlayComp = templateWorldspace.Components.OfType<IWorldSpaceOverlayComponentGetter>().First();
var templateSurfaceBlock = FindInTemplateMods(templateMods, m => m.SurfaceBlocks, overlayComp.SurfaceBlock.FormKey);

int cellGridSize = (int)templateSurfaceBlock.DNAM.First;  // e.g. 4 → 4x4 cells
string sourceBtdFile = Path.GetFileName(templateSurfaceBlock.ANAM).ToLowerInvariant();

var newBlock = new SurfaceBlock(targetMod)
{
    ANAM = "Data\\Terrain\\" + editorId + ".btd",
    EditorID = "OverlayBlock" + editorId,
    NAM1 = "OverlayBlock",
    NAM5 = templateSurfaceBlock.NAM5.FormKey.ToNullableLink<ISurfaceBlockGetter>(),
    DNAM = new SurfaceBlockIntItem() { First = (uint)cellGridSize, Second = (uint)cellGridSize },
    WHGT = float.MinValue,
    FNAM = templateSurfaceBlock.FNAM.Value.ToArray(),
    ENAM = new SurfaceBlockFloatItem()
    {
        First  = BitConverter.SingleToUInt32Bits(btd.WorldHeightMin / 8f),
        Second = BitConverter.SingleToUInt32Bits(btd.WorldHeightMax / 8f),
    },
};
```

### Sampling Terrain Height for Object Placement

When placing objects in a worldspace, sample the BTD terrain height and **divide by 8** (Starfield scale factor):

```csharp
var btd = new BtdFile(btdPath);
float terrainHeight = btd.SampleHeightAtWorld(0, 0) / 8f;
// terrainHeight is now in PlacedObject coordinate space
```

This is used by `WorldspaceNoun` to set `WorldspaceState.TerrainHeight`, which `TileInstantiationPass` uses as the Z base for all tile placements.

### Key Files

- `WorldspaceNoun.cs` — resolves template SurfaceBlock, copies BTD, creates new SurfaceBlock, samples terrain height
- `IWorldspaceDesign.cs` — defines `TemplateWorldspaceEditorId` (single source of truth for terrain setup)
- `FortDesign.cs` — accepts `templateWorldspaceEditorId` as constructor parameter (default `"DR001World"`)
- `TileInstantiationPass.cs` — places tiles using `state.TerrainHeight` for Z position

## Worldspace Cell Grid and Tile Mapping

Overlay worldspaces (dungeon POIs) use **100 overlay units per cell** for PlacedObject X/Y positions. A 4x4 BTD gives cells −2..1, spanning [−200, +200] overlay units. The cell grid size is derived automatically from the template SurfaceBlock's `DNAM.First` in `WorldspaceNoun` (no longer a property on `IWorldspaceDesign`). Cell coordinates range from `-(gridSize/2)` to `(gridSize/2 - 1)`.

### Tile-to-Cell Assignment

Tiles in the `GenerationMap` are placed at world positions centred on `FlatAreaWorldX/Y` (in overlay units). Cell index = `floor(worldPos / 100)`. With a 50×50 map and `TileWorldSize=4`, total extent is 200 overlay units, spanning cells −1..0 around the worldspace origin.

**Do not hardcode cell quadrant bounds.** Use dynamic cell lookup:

```csharp
// In per-cell passes, skip tiles that don't belong to the current cell
int tileCellX = (int)Math.Floor(worldX / 100f);
int tileCellY = (int)Math.Floor(worldY / 100f);
if (tileCellX != state.CurrentCellPos.X || tileCellY != state.CurrentCellPos.Y)
    continue;
```

### Placed Object Coordinate System

**PlacedObject positions are absolute overlay worldspace coordinates** (in 100-unit/cell overlay units), not cell-relative. Cell assignment (which SubCell record a placed object lives in) is for spatial streaming only. Do NOT subtract the cell origin from X/Y when storing positions.

- Cell (0,0) spans overlay X/Y [0, 100).
- Cell (−1,−1) spans overlay X/Y [−100, 0).
- `ResolveCell()` computes `floor(worldPos / 100)` to determine the right cell.

### Cross-Cell Object Routing

When unpacking prefabs, individual objects may land outside the tile's cell. `WorldspaceState.CellLookup` (built by the generator from all SubCells) maps `P2Int` grid points to `Cell` instances:

```csharp
int cellX = (int)Math.Floor(worldPos.X / 100f);
int cellY = (int)Math.Floor(worldPos.Y / 100f);
if (state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var cell))
    return cell;
return state.CurrentCell; // fallback
```

### Key Files

- `WorldspaceNoun.cs` — creates subcell grid from `cellGridSize` (derived from template DNAM), one WorldspaceBlock/SubBlock per cell
- `WorldspaceDungeonGenerator.cs` — builds `CellLookup`, iterates cells for per-cell passes
- `TileInstantiationPass.cs` — dynamic tile-to-cell check, cross-cell `ResolveCell()`

## Copying PlacedObjects

When duplicating a `PlacedObject` from a prefab cell into the world, do NOT use `DeepCopy()` — it preserves the original FormKey, causing ID collisions. Instead, create a `new PlacedObject(RetrogradeContext.Current.TargetMod)` (which assigns a fresh FormKey) and copy all properties manually. See `CellTools.CloneCellById` for the canonical pattern.

```csharp
var placed = new PlacedObject(RetrogradeContext.Current.TargetMod)
{
    Action = source.Action,
    AttachRef = source.AttachRef,
    Base = source.Base,
    BlueprintPartOrigin = source.BlueprintPartOrigin,
    BOLV = source.BOLV,
    Collision = source.Collision,
    Comments = source.Comments,
    Components = source.Components,
    ConstrainedDecal = source.ConstrainedDecal,
    Count = source.Count,
    CurrentZoneCell = source.CurrentZoneCell,
    DebugText = source.DebugText,
    EditorID = source.EditorID,
    Emittance = source.Emittance,
    EnableParent = source.EnableParent,
    EncounterZone = source.EncounterZone,
    ExternalEmittance = source.ExternalEmittance,
    FactionRank = source.FactionRank,
    GeometryDirtinessScale = source.GeometryDirtinessScale,
    GroupedPackIn = source.GroupedPackIn,
    HeadTrackingWeight = source.HeadTrackingWeight,
    HealthPercent = source.HealthPercent,
    IsActivationPoint = source.IsActivationPoint,
    IsIgnoredBySandbox = source.IsIgnoredBySandbox,
    IsLinkedRefTransient = source.IsLinkedRefTransient,
    Layer = source.Layer,
    LayeredMaterialSwaps = source.LayeredMaterialSwaps,
    LevelModifier = source.LevelModifier,
    LightArea = source.LightArea,
    LightBarndoorData = source.LightBarndoorData,
    LightColors = source.LightColors,
    LightFlicker = source.LightFlicker,
    LightGobo = source.LightGobo,
    Lighting = source.Lighting,
    LightLayerData = source.LightLayerData,
    LightRoundedness = source.LightRoundedness,
    LightStaticShadowMap = source.LightStaticShadowMap,
    LightVolumetricData = source.LightVolumetricData,
    LinkedReferences = source.LinkedReferences,
    LocationRefTypes = source.LocationRefTypes,
    Lock = source.Lock,
    MapMarker = source.MapMarker,
    NavigationDoorLink = source.NavigationDoorLink,
    NumTraversalFluffBytes = source.NumTraversalFluffBytes,
    OpenByDefault = source.OpenByDefault,
    Ownership = source.Ownership,
    Patrol = source.Patrol,
    PersistentLocation = source.PersistentLocation,
    PowerLinks = source.PowerLinks,
    Primitive = source.Primitive,
    ProjectedDecal = source.ProjectedDecal,
    ProjectedDecalReferences = source.ProjectedDecalReferences,
    Radius = source.Radius,
    RagdollBipedRotation = source.RagdollBipedRotation,
    Properties = source.Properties,
    RagdollData = source.RagdollData,
    ReferenceGroup = source.ReferenceGroup,
    StarfieldMajorRecordFlags = source.StarfieldMajorRecordFlags,
    Rotation = source.Rotation,
    Scale = source.Scale,
    ShipArrival = source.ShipArrival,
    SnapLinks = source.SnapLinks,
    SourcePackIn = source.SourcePackIn,
    TeleportDestination = source.TeleportDestination,
    TeleportName = source.TeleportName,
    Spline = source.Spline,
    TimeOfDay = source.TimeOfDay,
    Traversals = source.Traversals,
    VolumeData = source.VolumeData,
    VirtualMachineAdapter = source.VirtualMachineAdapter,
    XALG = source.XALG,
    XCZA = source.XCZA,
    XFLG = source.XFLG,
    XNSE = source.XNSE,
    XPCK = source.XPCK,
    Position = worldPos // override position/rotation as needed
};

## Inspecting Mutagen Types with ilspycmd

When you need to know the properties of an unknown Mutagen type (e.g. a component referenced by the Starfield CK name like `BGSSpaceshipAIActor_Component`), use `ilspycmd` to decompile the Mutagen DLL directly. This is **much faster** than searching NuGet XML docs or exploring the codebase.

### Find the Mutagen DLL

```
C:/Users/kaosn/.nuget/packages/mutagen.bethesda.starfield/<version>/lib/net8.0/Mutagen.Bethesda.Starfield.dll
```

Check the installed version with:
```bash
ls "C:/Users/kaosn/.nuget/packages/mutagen.bethesda.starfield/"
```

### Decompile a specific type

```bash
ilspycmd "C:/Users/kaosn/.nuget/packages/mutagen.bethesda.starfield/0.53.1/lib/net8.0/Mutagen.Bethesda.Starfield.dll" \
  -t "Mutagen.Bethesda.Starfield.SpaceshipAIActorComponent"
```

This shows the C# class definition including all properties, types, and interfaces — exactly what you need to write code against it.

### List all types matching a keyword

When you don't know the exact Mutagen class name for a CK record type:

```bash
ilspycmd "C:/Users/kaosn/.nuget/packages/mutagen.bethesda.starfield/0.53.1/lib/net8.0/Mutagen.Bethesda.Starfield.dll" \
  -l type 2>&1 | grep -i "spaceshipai"
```

This quickly maps CK names (e.g. `BGSSpaceshipAIActor_Component`) to Mutagen class names (e.g. `SpaceshipAIActorComponent`).

### Naming convention

CK record type `BGSFoo_Component` → Mutagen class `FooComponent`. Strip the `BGS` prefix and `_` separator.
```

## Planet Content Manager (PCM) Trees

The PCM system controls which worldspace POIs appear on planets and under what conditions. Three record types are involved, all in `targetMod.PlanetContentManagerBranchNodes` / `targetMod.PlanetContentManagerContentNodes`.

### Tree structure

```
PlanetContentManagerBranchNode (NodeType=BranchNode)   ← parented to a Starfield.esm root
  └─ PlanetContentManagerBranchNode (NodeType=ContentNode)  ← conditions live here; has BGSPlanetContentManagerContentProperties_Component
       └─ PlanetContentManagerContentNode                   ← Content = Worldspace FormKey
```

Parent–child wiring is **bidirectional**:
- Child sets `ParentNode` → parent's FormKey
- Parent adds child to its `Nodes: ExtendedList<IFormLinkGetter<IPlanetNodeGetter>>`

### Root hook nodes in Starfield.esm

| PCM category | Root EditorID | FormID |
|---|---|---|
| Block creation (spawned POIs) | `PCM_BlockCreation_PrimaryContent` | `00225373` |
| Planet scan (visible from space) | `PCM_ScanPlanet_General` | `0026F5DF` |
| Quest location requests | `PCM_LocationRequest_General` | `000F35E4` |

The top BranchNode's `ParentNode` must point to the appropriate root or the game ignores the entry.

### BGSPlanetContentManagerContentProperties_Component

Added to the ContentNode-type BranchNode. Values verified from `du_takeover_blockcontent`:

```csharp
contentBranch.Components.Add(new PlanetContentManagerContentPropertiesComponent
{
    ZNAM = 0, YNAM = 1, XNAM = 0, WNAM = 0, VNAM = 0, UNAM = 0,
    NAM1 = 0f,
    NAM3 = 0,
    NAM4 = new byte[] { 0x00, 0xFF, 0x00, 0x00 },
    NAM5 = 0, NAM6 = 0, NAM7 = 0, NAM8 = 0,
    NAM9 = 1,   // ← required; missing this causes the component to not register
});
```

`YNAM=1` and `NAM9=1` are the only non-zero values for a standard block-creation content node.

### Key type facts

- `Worldspace` implements `IPlanetContentTargetGetter` — use `.FormKey.ToNullableLink<IPlanetContentTargetGetter>()` for `ContentNode.Content`
- `PlanetContentManagerBranchNode.NodeTypeOption`: `BranchNode = 1`, `ContentNode = 2`
- `ContentNode.ParentNode` is typed `IFormLinkNullable<IPlanetContentManagerBranchNodeGetter>` (not the general `IPlanetParentNodeGetter`)
- BranchNode's `ParentNode` is `IFormLinkNullable<IPlanetParentNodeGetter>` — both BranchNode and Starfield root nodes implement this
- Set all FormLink properties **after** construction (Mutagen nullable FormLink rule)

### Find-or-create pattern

Both the BranchNode and ContentNode-type BranchNode are **shared** across worldspaces in the same mod run — search `targetMod.PlanetContentManagerBranchNodes` by EditorID before creating. Only the `PlanetContentManagerContentNode` (leaf) is always created fresh per worldspace.

### Pass files

- `PlanetContentManagerPass.cs` — block creation (spawned POIs), parent `00225373`
- `PlanetScanPass.cs` — planet scan (visible from orbit), parent `0026F5DF`
- `PlanetQuestPass.cs` — quest location requests, parent `000F35E4`

All three accept `(branchNodeEditorId, contentBranchEditorId, contentNodeEditorId)` constructor parameters.

## RoomPackinGeneration

`Retrograde.Library/RoomPackinGeneration/` — procedurally generates new PackIn records by assembling Starfield.esm tile-kit pieces. Entry point: `gen_roompackin`.

### What a PackIn cell actually contains

Rooms like `rg_sts_trk_shl_*` are **nested PackIn assemblies** — every structural piece in the storage cell is itself a `PlacedObject` whose `Base` points to another Starfield.esm PackIn, not a Static. The game recursively unpacks these at runtime.

### SciIntHallSm tile kit (`Architecture\ScienceKit\Interiors\HallSmall\`)

| FormID | EditorID | Role |
|--------|----------|------|
| `02447F` | `SciIntHallSm1Way01__SC` | Straight segment v1 |
| `02446E` | `SciIntHallSm1Way02__SC` | Straight segment v2 |
| `024466` | `SciIntHallSm1WayStairs01__SC` | Staircase — rises **2 units Z per tile** |
| `024441` | `SciIntHallSmCapScktA01__SC` | End cap with socket |
| `0185DE` | `SciIntHallSm3Way01__SC` | T-junction (3-way) |
| `012CE8` | `SciIntHallSm1WayScktA01__SC` | Straight with socket A |

Each tile is **4 units** along the corridor axis, symmetric about its placement point (bounds ±2).

### Special markers (Starfield.esm Statics)

| FormID | EditorID | Role |
|--------|----------|------|
| `000034` | `XMarkerHeading` | Connector marker — carries facing rotation |
| `00003B` | `XMarker` | Enemy spawn marker |
| `03F808` | `PrefabPackinPivotDummy` | Root pivot — always at (0,0,0) in every PackIn |

### Straight corridor tile grammar (N-S, Y axis)

```
[S connector Y=-6, Z=southZ]
[S end cap   Y=-4, Z=southZ]         ← SciIntHallSmCapScktA01__SC
[flat segs   Y=0,4,…  Z=southZ]      ← alternating Way1/Way2
[stair segs  Y=…  Z=southZ+i*2]      ← SciIntHallSm1WayStairs01__SC, each +2Z
[flat segs   Y=…  Z=northZ]          ← alternating Way2/Way1 (reversed)
[N end cap   Y=nCapY, Z=northZ]      ← SciIntHallSmCapScktA01__SC
[N connector Y=nCapY+2, Z=northZ]
```

- `northZ = stairCount * 2`
- `nCapY = (flatStart + stairCount + flatEnd) * 4`
- Connectors: 2 units past the end cap edge (cap occupies [capY−2, capY+2])
- S connector rotation `(0,0,π)`, N connector rotation `(0,0,0)`

### Creating a PackIn from scratch

```csharp
// 1. Create interior Cell with proper block/subblock routing
int blockNum    = (int)(cell.FormKey.ID % 10);
int subBlockNum = (int)((cell.FormKey.ID / 10) % 10);
// find-or-create CellBlock / CellSubBlock and add cell

// 2. Add structural tiles to cell.Temporary
cell.Temporary.Add(new PlacedObject(targetMod)
{
    Base     = new FormKey(sfModKey, 0x02447F).ToLink<IPlaceableObjectGetter>(),
    Position = new P3Float(0, y, z),
    Rotation = new P3Float(0, 0, 0),
});

// 3. Add connectors/spawns to cell.Persistent
cell.Persistent.Add(new PlacedObject(targetMod)
{
    EditorID = "rg_conn_n_D1_station_001",
    Base     = new FormKey(sfModKey, 0x000034).ToLink<IPlaceableObjectGetter>(),
    Position = new P3Float(0, nCapY + 2f, northZ),
    Rotation = new P3Float(0, 0, 0),
});

// 4. Create PackIn pointing at the cell (FormLink set after construction)
var packin = new PackIn(targetMod)
{
    EditorID          = "rg_gen_...",
    MajorRecordFlagsRaw = 512, // Prefab flag
    ObjectBounds      = new ObjectBounds { First = ..., Second = ... },
};
packin.Cell = cell.ToNullableLink<ICellGetter>(); // after construction
targetMod.PackIns.Add(packin);
```

**Required using:** `using Mutagen.Bethesda;` — provides `ToLink<T>()` and `ToNullableLink<T>()` extension methods.

### SciIntRmSm room tile kit

Room tiles (`rg_sts_trk_big_001`–`006`) use **Statics**, not nested PackIns. Key wall pieces: `SciIntRmSmMidFull01` (`024C99:Starfield.esm`, dominant), corner pieces (`024C9A`–`024C9E`), partition walls (`0563A5`–`0563A7`). Design rules: `designlib/sci_room.md`. Full FormID table: `formlib/packin.md`.

### Key files

- `Retrograde.Library/RoomPackinGeneration/SciHallwayGenerator.cs` — parametric hallway generator
- `Retrograde.Library/RoomPackinGeneration/SciRoomGenerator.cs` — parametric room generator (SciIntRmSm Statics)
- `gen_roompackin.cs` — standalone entry point, writes `generated_templates.esm`
- Script: `scripts/gen_roompackin.sh`
