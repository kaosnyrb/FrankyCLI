# SurfaceBlock (SFBK) + BTD Terrain Files

SurfaceBlock records define the terrain for a worldspace. Each links to a `.btd` binary file containing the heightmap and texture data.

## SurfaceBlock record

### Key properties

| Property | Type | Purpose | Typical value |
|----------|------|---------|---------------|
| `ANAM` | `string` | Path to the BTD file | `Data\Terrain\stbblock001.btd` (always lowercase) |
| `DNAM` | `SurfaceBlockIntItem` | Cell grid size (First=cols, Second=rows) | `(4, 4)` |
| `ENAM` | `SurfaceBlockFloatItem` | Height range as raw uint32-encoded floats | min/max from BTD |
| `NAM1` | `string` | Family name | `"OverlayBlock"` |
| `NAM5` | `IFormLinkNullable<ISurfaceBlockGetter>` | Parent standalone block (null = standalone) | `2C17D4:Starfield.esm` |
| `FNAM` | `ReadOnlyMemorySlice<byte>?` | Required binary metadata — copy verbatim from template | from template |
| `WHGT` | `float` | Water height | `float.MinValue` (unset) |
| `GNAM`–`KNAM` | various | Flags/indices | `0` |

### Two categories

- **Standalone** (`NAM5 = Null`): Base terrain for a planet surface (e.g. `oebb008world`)
- **Overlay** (`NAM5 = parent link`): Used by dungeon POI worldspaces. `NAM1 = "OverlayBlock"`

### Creating a SurfaceBlock for a new overlay worldspace

All values are derived from the template worldspace. `WorldspaceNoun` does this automatically:

```csharp
// 1. Resolve template worldspace → its overlay component → its SurfaceBlock
var templateWorldspace = FindInTemplateMods(templateMods, m => m.Worldspaces, design.TemplateWorldspaceEditorId);
var overlayComp = templateWorldspace.Components.OfType<IWorldSpaceOverlayComponentGetter>().First();
var templateSurfaceBlock = FindInTemplateMods(templateMods, m => m.SurfaceBlocks, overlayComp.SurfaceBlock.FormKey);

// 2. Extract values from template
int cellGridSize   = (int)templateSurfaceBlock.DNAM.First;
string sourceBtdFile = Path.GetFileName(templateSurfaceBlock.ANAM).ToLowerInvariant();

// 3. Copy BTD file for the new worldspace, then read it
var btd = new BtdFile(newBtdPath);

// 4. Create new SurfaceBlock
var newBlock = new SurfaceBlock(targetMod)
{
    ANAM   = "Data\\Terrain\\" + editorId + ".btd",
    EditorID = "OverlayBlock" + editorId,
    NAM1   = "OverlayBlock",
    DNAM   = new SurfaceBlockIntItem() { First = (uint)cellGridSize, Second = (uint)cellGridSize },
    WHGT   = float.MinValue,
    FNAM   = templateSurfaceBlock.FNAM.Value.ToArray(),
    ENAM   = new SurfaceBlockFloatItem()
    {
        First  = BitConverter.SingleToUInt32Bits(btd.WorldHeightMin / 8f),
        Second = BitConverter.SingleToUInt32Bits(btd.WorldHeightMax / 8f),
    },
};
// FormLink set after construction:
newBlock.NAM5 = templateSurfaceBlock.NAM5.FormKey.ToNullableLink<ISurfaceBlockGetter>();
targetMod.SurfaceBlocks.Add(newBlock);
```

**Prerequisite**: BTD source files must be unpacked from `Starfield - Terrain*.ba2` into `Data\Terrain\`. `WorldspaceNoun` throws `FileNotFoundException` if they are missing.

### Key file

`WorldspaceNoun.cs` — resolves template, copies BTD, creates SurfaceBlock, samples terrain height.

---

## BTD terrain binary format

BTD files (`.btd`) store terrain heightmaps for Starfield and Fallout 76. Implemented in `Retrograde.Library/Utils/BtdFile.cs`.

### File structure (Starfield, version 5 or 6)

| Section | Size | Notes |
|---------|------|-------|
| Header | 40 bytes | Magic `BTDB`, version, HeightMin/Max (floats, unscaled), ResX/ResY, CellMin/MaxX/Y |
| LTEX form IDs | 4 + N×4 bytes | Land texture form IDs (6–7 in all Starfield files) |
| Cell height min/max map | 8 bytes/cell | float min, float max — **unscaled** (before 8× Starfield multiplier) |
| Land texture map | 32 bytes/cell | 4 quadrant palettes × 8 bytes; each byte = LTEX array index |
| LOD4 height map | 128 bytes/cell | 8×8 uint16 grid (every 16th vertex of 128×128) |
| LOD4 land textures | 128 bytes/cell | Affects rendering; zero when painting custom textures |
| Block tables | LOD3→2→1→0 | 8 bytes/entry: uint32 offset + uint32 compressed size |
| Compressed block data | variable | zlib-compressed (must use `ZLibStream`, NOT `DeflateStream`) |

Each LOD0 compressed block decompresses to 65 536 bytes:
- Bytes 0–32 767: height data (128×128 uint16)
- Bytes 32 768–65 535: texture data (128×128 uint16)

**Starfield vs FO76 detection**: Starfield files have all 4 cell boundary fields = 0 in header; applies 8× height scale. FO76 has ground-cover and vertex-color sections absent in Starfield.

### Coordinate systems — two separate scales

| | BTD internal | Overlay worldspace (PlacedObject X/Y) |
|---|---|---|
| Cell size | 4096 units | 100 units |
| Vertex spacing | 32 units | 100/128 ≈ 0.78125 units |
| Z (height) | 8×-scaled | divide by 8 |

```csharp
float overlayX = btdX * (100f / 4096f);
float placedZ  = btd.SampleHeightAtWorld(btdX, btdY) / 8f;
```

`SampleHeightAtWorld(worldX, worldY)` takes **BTD-internal** coords (4096-unit scale) and returns 8×-scaled Z. Always divide by 8 before using as PlacedObject Z.

Starfield BTDs are always centred at 0: `WorldCenterX = 0`, no offset correction needed.

### Height encoding

`uint16` mapped linearly to `[WorldHeightMin, WorldHeightMax]`:

```csharp
ushort raw   = reader.HeightToRaw(worldHeight);
float  world = reader.RawToHeight(raw);
```

### Land texture map (32 bytes/cell)

4 quadrant palettes (Q0=TL, Q1=TR, Q2=BL, Q3=BR), 8 bytes each. Each byte is an index into the global LTEX form ID array.

**Critical**: neighbouring cells' palettes influence rendering at borders. When painting custom textures, normalize **all** 4 quadrants to be identical on **all** cells (including edge cells):

```csharp
var palette = new byte[32];
reader.GetCellLandTexMap(palette, cx, cy);
for (int q = 1; q < 4; q++)
    Array.Copy(palette, 0, palette, q * 8, 8);
for (int cy = reader.CellMinY; cy <= reader.CellMaxY; cy++)
    for (int cx = reader.CellMinX; cx <= reader.CellMaxX; cx++)
        reader.SetCellLandTexMap(palette, cx, cy);
```

Default palette across nearly all Starfield BTDs: `[6,5,4,3,2,0,1,0]`.

### Per-vertex texture encoding (uint16) — partially understood

- `0x0000` = no texture / transparent
- The full uint16 interacts with the land texture map palette; the exact mapping is complex
- **Known working values** (tested against `oebb008world.btd`, palette `06 05 04 03 02 00 01 00`):

| Value | Appearance |
|-------|-----------|
| `0x0100` | Base sandy |
| `0x3000` | Slightly less sandy |
| `0x4000` | Clean bright sand (most common painting target) |
| `0x6000` | Clean orangy sand |
| `0x0800`, `0x1000`, `0x2000`, `0x7000` | All "patchy sandy" (same despite different top nibble) |
| `0x0E00` | Patchy sandy variant (most common in vanilla files) |

Values with the same low 12 bits but different top 4 bits produce **identical** textures. The top nibble is not a simple layer index.

### Edge cells are off-limits

The outermost ring connects the map to the rest of the world — never modify their height or per-vertex texture data. For an N×N grid, only cells `(CellMinX+1 .. CellMaxX-1, CellMinY+1 .. CellMaxY-1)` are safe. A 3×3 BTD has only 1 editable cell.

**Exception**: the 32-byte land texture map palette on edge cells **can and should** be normalized, because neighbour palettes affect border rendering.

### Complete texture painting workflow

```csharp
var reader = new BtdFile(path);

// 1. Normalize all cells' palettes (including edge cells)
var palette = new byte[32];
reader.GetCellLandTexMap(palette, centerCellX, centerCellY);
for (int q = 1; q < 4; q++) Array.Copy(palette, 0, palette, q * 8, 8);
for (int cy = reader.CellMinY; cy <= reader.CellMaxY; cy++)
    for (int cx = reader.CellMinX; cx <= reader.CellMaxX; cx++)
        reader.SetCellLandTexMap(palette, cx, cy);

// 2. Paint per-vertex texture (editable cells only)
var texBuf = new ushort[128 * 128];
Array.Fill(texBuf, (ushort)0x4000); // "clean bright sand"
reader.SetCellTextureData(texBuf, cellX, cellY);

// 3. Zero LOD4 land textures to avoid interference
reader.SetCellLod4LandTex(new byte[128], cellX, cellY);

// 4. Save
reader.Save(outputPath, updateMinMax: false); // skip metadata updates for additive edits
```

### Height editing and edge smoothing

```csharp
reader.SetCellHeightMap(heightBuf, cellX, cellY);
reader.SmoothDirtyCellEdges(32); // blend transition band at dirty/clean borders
reader.Save(outputPath);         // updateMinMax defaults to true (full metadata update)
```

`SmoothDirtyCellEdges(bandWidth)` lerps the dirty cell's edge toward the original and propagates the delta into neighbour cells, then marks them dirty.

### Full API

```csharp
var reader = new BtdFile(path);

// Heights
float h  = reader.GetHeight(cellX, cellY, vertX, vertY);
float h2 = reader.SampleHeightAtWorld(btdWorldX, btdWorldY);
ushort raw = reader.HeightToRaw(worldHeight);
float world = reader.RawToHeight(raw);
reader.SetCellHeightMap(heightBuf, cellX, cellY); // ushort[128*128]

// Land texture map (32 bytes/cell)
reader.GetCellLandTexMap(palette, cellX, cellY);
reader.SetCellLandTexMap(palette, cellX, cellY);

// Per-vertex texture (128×128 uint16)
reader.GetCellTextureData(texBuf, cellX, cellY);
reader.SetCellTextureData(texBuf, cellX, cellY);

// LOD4 textures (128 bytes/cell)
reader.GetCellLod4LandTex(lod4, cellX, cellY);
reader.SetCellLod4LandTex(lod4, cellX, cellY);

// Save
reader.Save(outputPath);                     // full — updates cell min/max and LOD4
reader.Save(outputPath, updateMinMax: false); // additive — skip metadata updates
```

### Key pitfalls

- **Use `ZLibStream` not `DeflateStream`** for compression — requires proper zlib header + Adler32 checksum
- **Cell min/max metadata must be unscaled** — divide RawToHeight values by 8 for Starfield
- **LOD4 height map must be updated** for dirty cells or CK may crash (handled by `updateMinMax: true`)
- **`MemoryStream.GetBuffer()` reallocation**: don't patch a GetBuffer() result then keep writing to the stream — use a separate byte[] for the prefix

### Test harnesses

| Command | Script | Purpose |
|---------|--------|---------|
| `gen_btd_test` | `test_btd.bat` | Automated reader/writer verification |
| `gen_btd_flatten` | `flatten_btd.bat` | Adds cosine hill to center cell for visual check |
| `gen_btd_info` | `info_btd.bat` / `info_btd_all.bat` | Dumps structure, stats, height distribution |

Test BTDs: `C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data\terrain\`

### Cross-file findings (5 Starfield BTDs validated)

- All share the same height range (−500 to 1000, span 1500) and LTEX form IDs
- Versions 5 and 6 both parse identically as Starfield
- `0x0E00` is the most common texture value; `0x4000`, `0x3000`, `0x6000`, `0x7000` also common
- Quadrant palette differences are normal in vanilla files — normalization is necessary when painting
- Cell grids range 3×3 to 5×5; all have 0 empty LOD0 blocks
