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

BTD files (`.btd`) store terrain heightmaps for Starfield and Fallout 76. Reader/writer: `Retrograde.Library/Utils/BtdTerrainReader.cs`.

### File Format (Version 6, Starfield)

- **Header** (40 bytes): magic `BTDB`, version (6), HeightMin/Max (floats, unscaled), ResX/ResY, CellMin/MaxX/Y
- **LTEX form IDs**: uint32 count + array of land texture form IDs
- **Cell height min/max map**: 8 bytes/cell (float min, float max) — stored in **unscaled** coordinates (before 8x Starfield multiplier)
- **Land texture map**: 32 bytes/cell — 8 texture layer indices (bytes referencing LTEX array), repeated 4x (possibly per-quadrant)
- **Ground cover** (FO76 only, absent in Starfield): form IDs + 32 bytes/cell map
- **LOD4 height map**: 128 bytes/cell — 8x8 grid of uint16 (downsampled from 128x128, every 16th sample)
- **LOD4 land textures**: 128 bytes/cell
- **Vertex color LOD4** (FO76 only, absent in Starfield): 128 bytes/cell
- **Block tables**: LOD3→LOD2→LOD1→LOD0 order, each entry is 8 bytes (uint32 offset, uint32 compressed size). Starfield has 1 entry/block; FO76 has 2 entries/block for LOD3/2/0
- **Compressed block data**: zlib-compressed blocks, each LOD0 block = 65536 bytes:
  - Bytes 0–32767: height data (128x128 uint16)
  - Bytes 32768–65535: texture data (128x128 uint16)

### Texture Data

Each LOD0 block contains per-vertex texture data (128x128 uint16) in the second half of the decompressed block. The cell also has a **land texture map** (32 bytes in the metadata) that defines which 8 LTEX layers are available for that cell.

The per-vertex uint16 encoding is not yet fully understood. Observed properties:
- Values range widely (0–32000+), not simple layer indices
- Many unique values per cell (hundreds), suggesting blend/weight encoding
- Values cluster in specific ranges (e.g., 2048–4095 most common in test BTD)
- Setting all hill vertices to one of the existing values does produce a visible texture change

Reading/writing texture data:
```csharp
var texBuf = new ushort[128 * 128];
reader.GetCellTextureData(texBuf, cellX, cellY);
// Modify texBuf values...
reader.SetCellTextureData(texBuf, cellX, cellY);
// Or single vertex:
reader.SetTexture(cellX, cellY, vertX, vertY, rawValue);
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

The outermost ring of cells in a BTD connects the map to the rest of the world. **Never modify edge cells.** For an NxN cell grid, only cells `(CellMinX+1..CellMaxX-1, CellMinY+1..CellMaxY-1)` are safe to edit. A 3x3 BTD has only 1 editable cell in the center.

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
var reader = new BtdTerrainReader(path);
float h = reader.GetHeight(cellX, cellY, vertX, vertY);
float h2 = reader.SampleHeightAtWorld(worldX, worldY);

// Read texture
var texBuf = new ushort[128 * 128];
reader.GetCellTextureData(texBuf, cellX, cellY);

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

### Test Harnesses

- `gen_btd_test` — automated reader/writer verification (test_btd.bat)
- `gen_btd_flatten` — adds a cosine hill to the center cell for visual verification (flatten_btd.bat)
- `gen_btd_info` — dumps file structure, section layout, block stats, height distribution (info_btd.bat)
- Test BTD: `C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data\terrain\oebb008world.btd`

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
