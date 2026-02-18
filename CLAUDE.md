# FrankyCLI

Starfield procedural dungeon generation tool using Mutagen.Bethesda.

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

- **Cells**: 128x128 vertices each, 1 cell = 4096 world units, 32 units per vertex
- **Tiles**: 8x8 groups of cells, the unit of decompression/caching
- **LOD levels**: LOD0 = 128x128, LOD1 = 64x64, LOD2 = 32x32, LOD3 = 16x16
- **Height encoding**: uint16 mapped linearly to `[WorldHeightMin, WorldHeightMax]`
- **Starfield detection**: all 4 cell boundary fields in header are zero; applies 8x height scale to min/max
- **Starfield cell bounds**: derived from resolution fields: `CellMinX = -(ResX >> 8)`, `CellCountX = ResX >> 7`
- **Cell min/max metadata uses UNSCALED heights** — when writing, divide by 8 for Starfield files
- **Compression**: must use `ZLibStream` (not `DeflateStream`) — requires proper zlib header + Adler32 checksum
- **Typical terrain heights**: the test BTD (oebb008world) has terrain at ~15–84 world units in a -4000 to 8000 range. `HeightToRaw(0)` = 21845

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

Template mods (`IStarfieldModGetter`) return getter interfaces (`IPlacedObjectGetter`, `ICellGetter`). When cloning from getters into a mutable `PlacedObject`:

- **Simple value types** (P3Float, float?, int?, bool?, FormKey) assign directly
- **FormLinks**: use `source.Base.FormKey.ToNullableLink<IPlaceableObjectGetter>()` to convert getter link to setter link
- **Complex sub-objects** (Primitive, Lighting, Ownership, EnableParent, VolumeData, MapMarker): use `.DeepCopy()` on the getter
- **Collection properties** (Components, LinkedReferences, etc.): also need `.DeepCopy()` or manual conversion — these can't be assigned directly from getter to setter
- **Skip properties** that aren't needed rather than fighting type conversions — worldspace tiles typically only need Base, Position, Rotation, Scale, Primitive, VolumeData, Lighting

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

### Key Files

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

```csharp
var newBlock = new SurfaceBlock(targetMod)
{
    ANAM = "Data\\Terrain\\" + editorId + ".btd",
    EditorID = "OverlayBlock" + editorId,
    NAM1 = "OverlayBlock",
    NAM5 = new FormKey(starfieldEsm, 0x002C17D4).ToNullableLink<ISurfaceBlockGetter>(),
    DNAM = new SurfaceBlockIntItem() { First = 4, Second = 4 },
    WHGT = float.MinValue,
};
// Link to worldspace
((WorldSpaceOverlayComponent)worldspace.Components[0]).SurfaceBlock =
    newBlock.ToNullableLink<ISurfaceBlockGetter>();
```

### Key Files

- `WorldspaceNoun.cs` — creates new SurfaceBlocks linked to worldspaces
- `IWorldspaceDesign.cs` — defines `TemplateSurfaceBlockEditorId` property
- `FortDesign.cs` — uses `stbblock001` template worldspace, `OverlayBlockstbblock001` surface block

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
```
