using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Retrograde.Utils
{
    /// <summary>
    /// Reads Bethesda Terrain Data (.btd) files used by Starfield (and Fallout 76).
    /// Based on the fo76utils reverse engineering of the BTDB format.
    ///
    /// The BTD stores terrain height data organized into cells (128x128 vertices each)
    /// grouped into 8x8 cell tiles, with multiple LOD levels and zlib compression.
    ///
    /// Height values are stored as uint16 and mapped linearly to [worldHeightMin, worldHeightMax].
    /// Starfield BTDs are detected by all cell boundary fields being zero, and apply an 8x
    /// height scale factor.
    /// </summary>
    public class BtdFile
    {
        /// <summary>Vertices per cell edge at full (LOD0) resolution.</summary>
        public const int CellResolution = 128;

        /// <summary>Cells per tile edge. Tiles are the unit of cached decompression.</summary>
        private const int TileSize = 8;

        /// <summary>Tile data stride: TileSize * CellResolution = 1024.</summary>
        private const int TileStride = TileSize * CellResolution;

        public int CellMinX { get; private set; }
        public int CellMinY { get; private set; }
        public int CellMaxX { get; private set; }
        public int CellMaxY { get; private set; }
        public int CellCountX { get; private set; }
        public int CellCountY { get; private set; }
        public float WorldHeightMin { get; private set; }
        public float WorldHeightMax { get; private set; }
        public bool IsStarfield { get; private set; }

        /// <summary>
        /// World-space X position of the BTD grid centre (in BTD coordinates).
        /// When the BTD is used by an overlay worldspace, the engine maps
        /// BTD cell CellMinX → overlay cell −halfGrid, so the BTD centre maps
        /// to overlay local (0, 0). Subtract this from any BTD world coordinate
        /// to obtain the overlay-local position; add it to go the other way.
        /// </summary>
        public float WorldCenterX => ((float)(CellMinX + CellMaxX + 1) / 2f) * 4096f;
        public float WorldCenterY => ((float)(CellMinY + CellMaxY + 1) / 2f) * 4096f;

        private byte[] _fileData;
        private long _zlibBlocksDataOffs;
        private long _zlibBlkTableOffsLOD0;
        private long _zlibBlkTableOffsLOD1;
        private long _zlibBlkTableOffsLOD2;
        private long _zlibBlkTableOffsLOD3;

        // Metadata section offsets (needed for Save to update min/max and LOD4)
        private long _cellHeightMinMaxOffs;
        private long _landTexMapOffs;
        private long _lod4HeightMapOffs;
        private long _lod4LandTexOffs;

        // Tile cache: key = (tileY << 16) | tileX
        private readonly Dictionary<uint, TileData> _tileCache = new Dictionary<uint, TileData>();
        private int _tileCacheMaxSize = 16;

        // Tracks which cells have been modified (cellX - CellMinX, cellY - CellMinY)
        private readonly HashSet<(int, int)> _dirtyCells = new HashSet<(int, int)>();

        // Per-cell texture data cache (only for cells with texture edits)
        // Key: (dx, dy) relative to CellMin, Value: 128*128 uint16 texture data
        private readonly Dictionary<(int, int), ushort[]> _dirtyTextures = new Dictionary<(int, int), ushort[]>();

        private class TileData
        {
            public int X0;
            public int Y0;
            public ushort[] HeightMap; // TileStride * TileStride
        }

        public BtdFile()
        {
        }

        public BtdFile(string path)
        {
            Load(path);
        }

        public void Load(string path)
        {
            _fileData = File.ReadAllBytes(path);
            ParseHeader();
        }

        public void Load(byte[] data)
        {
            _fileData = data;
            ParseHeader();
        }

        private void ParseHeader()
        {
            if (_fileData.Length < 40)
                throw new InvalidDataException("File too small to be a BTD file");

            // Magic: "BTDB"
            if (_fileData[0] != (byte)'B' || _fileData[1] != (byte)'T' ||
                _fileData[2] != (byte)'D' || _fileData[3] != (byte)'B')
                throw new InvalidDataException("Not a BTD file (invalid magic)");

            uint version = ReadUInt32(4);
            if (version != 5 && version != 6)
                throw new InvalidDataException($"Unsupported BTD version: {version}");

            float heightMin = ReadFloat(0x08);
            float heightMax = ReadFloat(0x0C);
            uint resX = ReadUInt32(0x10);
            uint resY = ReadUInt32(0x14);
            int cellMinX = ReadInt32(0x18);
            int cellMinY = ReadInt32(0x1C);
            int cellMaxX = ReadInt32(0x20);
            int cellMaxY = ReadInt32(0x24);

            // Starfield BTDs have all cell boundaries zeroed
            IsStarfield = (cellMinX | cellMinY | cellMaxX | cellMaxY) == 0;

            if (IsStarfield)
            {
                heightMin *= 8.0f;
                heightMax *= 8.0f;
                cellMinX = -(int)(resX >> 8);
                cellMinY = -(int)(resY >> 8);
                cellMaxX = cellMinX + (int)(resX >> 7) - 1;
                cellMaxY = cellMinY + (int)(resY >> 7) - 1;
            }

            WorldHeightMin = heightMin;
            WorldHeightMax = heightMax;
            CellMinX = cellMinX;
            CellMinY = cellMinY;
            CellMaxX = cellMaxX;
            CellMaxY = cellMaxY;
            CellCountX = cellMaxX + 1 - cellMinX;
            CellCountY = cellMaxY + 1 - cellMinY;

            CalculateSectionOffsets();
        }

        private void CalculateSectionOffsets()
        {
            long pos = 0x28; // after 40-byte header

            // Land texture form IDs
            uint ltexCnt = ReadUInt32(pos);
            pos += 4;
            pos += ltexCnt * 4; // skip form ID array

            // Cell height min/max map: 8 bytes per cell
            _cellHeightMinMaxOffs = pos;
            pos += (long)CellCountY * CellCountX * 8;

            // Land texture map: 32 bytes per cell (4 quadrant palettes of 8 bytes each)
            _landTexMapOffs = pos;
            pos += (long)CellCountY * CellCountX * 32;

            // Ground cover (not present in Starfield)
            if (!IsStarfield)
            {
                uint gcvrCnt = ReadUInt32(pos);
                pos += 4;
                pos += gcvrCnt * 4; // skip form ID array
                pos += (long)CellCountY * CellCountX * 32; // ground cover map
            }

            // LOD4 height map: 128 bytes per cell
            _lod4HeightMapOffs = pos;
            pos += (long)CellCountY * CellCountX * 128;

            // LOD4 land textures: 128 bytes per cell
            _lod4LandTexOffs = pos;
            pos += (long)CellCountY * CellCountX * 128;

            // Vertex color LOD4 (not present in Starfield)
            if (!IsStarfield)
                pos += (long)CellCountY * CellCountX * 128;

            // Compressed block tables
            if (IsStarfield)
            {
                _zlibBlkTableOffsLOD3 = pos;
                pos += CeilDiv(CellCountY, 8) * CeilDiv(CellCountX, 8) * 8;

                _zlibBlkTableOffsLOD2 = pos;
                pos += CeilDiv(CellCountY, 4) * CeilDiv(CellCountX, 4) * 8;

                _zlibBlkTableOffsLOD1 = pos;
                pos += CeilDiv(CellCountY, 2) * CeilDiv(CellCountX, 2) * 8;

                _zlibBlkTableOffsLOD0 = pos;
                pos += (long)CellCountY * CellCountX * 8;
            }
            else
            {
                _zlibBlkTableOffsLOD3 = pos;
                pos += CeilDiv(CellCountY, 8) * CeilDiv(CellCountX, 8) * 2 * 8;

                _zlibBlkTableOffsLOD2 = pos;
                pos += CeilDiv(CellCountY, 4) * CeilDiv(CellCountX, 4) * 2 * 8;

                _zlibBlkTableOffsLOD1 = pos;
                pos += CeilDiv(CellCountY, 2) * CeilDiv(CellCountX, 2) * 8;

                _zlibBlkTableOffsLOD0 = pos;
                pos += (long)CellCountY * CellCountX * 2 * 8;
            }

            _zlibBlocksDataOffs = pos;
        }

        /// <summary>
        /// Gets the raw uint16 height map for a single cell at the specified LOD level.
        /// LOD 0 = full resolution (128x128), LOD 1 = 64x64, LOD 2 = 32x32, LOD 3 = 16x16.
        /// Buffer must be at least (128 >> lod)^2 elements.
        /// </summary>
        public void GetCellHeightMap(ushort[] buf, int cellX, int cellY, int lod = 0)
        {
            if (cellX < CellMinX || cellX > CellMaxX || cellY < CellMinY || cellY > CellMaxY)
                throw new ArgumentOutOfRangeException($"Cell ({cellX}, {cellY}) is out of range");

            lod = Math.Clamp(lod, 0, 3);
            var tile = LoadTile(cellX, cellY, lod);

            int localX = ((cellX - CellMinX) & (TileSize - 1)) * CellResolution;
            int localY = ((cellY - CellMinY) & (TileSize - 1)) * CellResolution;
            int n = CellResolution >> lod;
            int step = 1 << lod;

            for (int vy = 0; vy < n; vy++)
            {
                for (int vx = 0; vx < n; vx++)
                {
                    int srcIdx = (localY + vy * step) * TileStride + localX + vx * step;
                    buf[vy * n + vx] = tile.HeightMap[srcIdx];
                }
            }
        }

        /// <summary>
        /// Gets the height in world units for a single cell vertex.
        /// vertX/vertY are 0..127 within the cell at LOD 0.
        /// </summary>
        public float GetHeight(int cellX, int cellY, int vertX, int vertY)
        {
            var tile = LoadTile(cellX, cellY, 0);
            int localX = ((cellX - CellMinX) & (TileSize - 1)) * CellResolution;
            int localY = ((cellY - CellMinY) & (TileSize - 1)) * CellResolution;
            int idx = (localY + vertY) * TileStride + localX + vertX;
            return RawToHeight(tile.HeightMap[idx]);
        }

        /// <summary>
        /// Samples interpolated world-space height at an arbitrary world X/Y position.
        /// In Creation Engine, 1 cell = 4096 units, each cell has 128 vertices → 32 units per vertex.
        /// </summary>
        public float SampleHeightAtWorld(float worldX, float worldY)
        {
            const float cellSize = 4096f;

            float cellFX = worldX / cellSize;
            float cellFY = worldY / cellSize;

            int cellX = (int)Math.Floor(cellFX);
            int cellY = (int)Math.Floor(cellFY);

            // Local position within cell in vertex space (0..128)
            float localVX = (cellFX - cellX) * CellResolution;
            float localVY = (cellFY - cellY) * CellResolution;

            int vx0 = Math.Clamp((int)localVX, 0, CellResolution - 2);
            int vy0 = Math.Clamp((int)localVY, 0, CellResolution - 2);
            int vx1 = vx0 + 1;
            int vy1 = vy0 + 1;
            float tx = localVX - vx0;
            float ty = localVY - vy0;

            // Clamp cell to valid range
            int cx = Math.Clamp(cellX, CellMinX, CellMaxX);
            int cy = Math.Clamp(cellY, CellMinY, CellMaxY);

            var tile = LoadTile(cx, cy, 0);
            int baseX = ((cx - CellMinX) & (TileSize - 1)) * CellResolution;
            int baseY = ((cy - CellMinY) & (TileSize - 1)) * CellResolution;

            float h00 = RawToHeight(tile.HeightMap[(baseY + vy0) * TileStride + baseX + vx0]);
            float h10 = RawToHeight(tile.HeightMap[(baseY + vy0) * TileStride + baseX + vx1]);
            float h01 = RawToHeight(tile.HeightMap[(baseY + vy1) * TileStride + baseX + vx0]);
            float h11 = RawToHeight(tile.HeightMap[(baseY + vy1) * TileStride + baseX + vx1]);

            // Bilinear interpolation
            return (h00 * (1 - tx) + h10 * tx) * (1 - ty)
                 + (h01 * (1 - tx) + h11 * tx) * ty;
        }

        /// <summary>
        /// Converts a raw uint16 height value to world-space height.
        /// </summary>
        public float RawToHeight(ushort raw)
        {
            return WorldHeightMin + (raw / 65535.0f) * (WorldHeightMax - WorldHeightMin);
        }

        /// <summary>
        /// Converts a world-space height to a raw uint16 value. Inverse of RawToHeight.
        /// </summary>
        public ushort HeightToRaw(float worldHeight)
        {
            float t = (worldHeight - WorldHeightMin) / (WorldHeightMax - WorldHeightMin);
            return (ushort)Math.Clamp((int)(t * 65535.0f + 0.5f), 0, 65535);
        }

        /// <summary>
        /// Sets the raw uint16 height map for a single cell at LOD0.
        /// Buffer must be exactly 128x128 elements.
        /// </summary>
        public void SetCellHeightMap(ushort[] buf, int cellX, int cellY)
        {
            if (cellX < CellMinX || cellX > CellMaxX || cellY < CellMinY || cellY > CellMaxY)
                throw new ArgumentOutOfRangeException($"Cell ({cellX}, {cellY}) is out of range");

            var tile = LoadTile(cellX, cellY, 0);
            int localX = ((cellX - CellMinX) & (TileSize - 1)) * CellResolution;
            int localY = ((cellY - CellMinY) & (TileSize - 1)) * CellResolution;

            for (int vy = 0; vy < CellResolution; vy++)
            {
                int dstRow = (localY + vy) * TileStride + localX;
                for (int vx = 0; vx < CellResolution; vx++)
                    tile.HeightMap[dstRow + vx] = buf[vy * CellResolution + vx];
            }

            _dirtyCells.Add((cellX - CellMinX, cellY - CellMinY));
        }

        /// <summary>
        /// Gets the raw uint16 texture data for a single cell at LOD0 (from the compressed block).
        /// Buffer must be at least 128*128 elements.
        /// </summary>
        public void GetCellTextureData(ushort[] buf, int cellX, int cellY)
        {
            if (cellX < CellMinX || cellX > CellMaxX || cellY < CellMinY || cellY > CellMaxY)
                throw new ArgumentOutOfRangeException($"Cell ({cellX}, {cellY}) is out of range");

            var key = (cellX - CellMinX, cellY - CellMinY);
            if (_dirtyTextures.TryGetValue(key, out var cached))
            {
                Array.Copy(cached, buf, CellResolution * CellResolution);
                return;
            }

            // Read from original file
            var fileBuf = new ushort[CellResolution * CellResolution];
            GetCellTextureDataFromFile(fileBuf, cellX, cellY);
            Array.Copy(fileBuf, buf, fileBuf.Length);
        }

        /// <summary>
        /// Sets the raw uint16 texture data for a single cell at LOD0.
        /// Buffer must be exactly 128*128 elements.
        /// </summary>
        public void SetCellTextureData(ushort[] buf, int cellX, int cellY)
        {
            if (cellX < CellMinX || cellX > CellMaxX || cellY < CellMinY || cellY > CellMaxY)
                throw new ArgumentOutOfRangeException($"Cell ({cellX}, {cellY}) is out of range");

            var key = (cellX - CellMinX, cellY - CellMinY);
            var copy = new ushort[CellResolution * CellResolution];
            Array.Copy(buf, copy, copy.Length);
            _dirtyTextures[key] = copy;
            _dirtyCells.Add(key);
        }

        /// <summary>
        /// Sets a single vertex texture value as raw uint16 at LOD0.
        /// </summary>
        public void SetTexture(int cellX, int cellY, int vertX, int vertY, ushort raw)
        {
            if (cellX < CellMinX || cellX > CellMaxX || cellY < CellMinY || cellY > CellMaxY)
                throw new ArgumentOutOfRangeException($"Cell ({cellX}, {cellY}) is out of range");

            var key = (cellX - CellMinX, cellY - CellMinY);
            if (!_dirtyTextures.TryGetValue(key, out var tex))
            {
                tex = new ushort[CellResolution * CellResolution];
                GetCellTextureDataFromFile(tex, cellX, cellY);
                _dirtyTextures[key] = tex;
            }
            tex[vertY * CellResolution + vertX] = raw;
            _dirtyCells.Add(key);
        }

        /// <summary>
        /// Reads the 32-byte land texture map for a cell (4 quadrant palettes of 8 LTEX indices each).
        /// </summary>
        public void GetCellLandTexMap(byte[] buf32, int cellX, int cellY)
        {
            int dx = cellX - CellMinX;
            int dy = cellY - CellMinY;
            long off = _landTexMapOffs + ((long)dy * CellCountX + dx) * 32;
            for (int i = 0; i < 32; i++)
                buf32[i] = _fileData[off + i];
        }

        /// <summary>
        /// Writes the 32-byte land texture map for a cell directly into the file data buffer.
        /// Call before Save() to update quadrant palettes.
        /// </summary>
        public void SetCellLandTexMap(byte[] buf32, int cellX, int cellY)
        {
            int dx = cellX - CellMinX;
            int dy = cellY - CellMinY;
            long off = _landTexMapOffs + ((long)dy * CellCountX + dx) * 32;
            for (int i = 0; i < 32; i++)
                _fileData[off + i] = buf32[i];
        }

        /// <summary>
        /// Reads the 128-byte LOD4 land texture data for a cell.
        /// </summary>
        public void GetCellLod4LandTex(byte[] buf128, int cellX, int cellY)
        {
            int dx = cellX - CellMinX;
            int dy = cellY - CellMinY;
            long off = _lod4LandTexOffs + ((long)dy * CellCountX + dx) * 128;
            for (int i = 0; i < 128; i++)
                buf128[i] = _fileData[off + i];
        }

        /// <summary>
        /// Writes the 128-byte LOD4 land texture data for a cell.
        /// </summary>
        public void SetCellLod4LandTex(byte[] buf128, int cellX, int cellY)
        {
            int dx = cellX - CellMinX;
            int dy = cellY - CellMinY;
            long off = _lod4LandTexOffs + ((long)dy * CellCountX + dx) * 128;
            for (int i = 0; i < 128; i++)
                _fileData[off + i] = buf128[i];
        }

        private void GetCellTextureDataFromFile(ushort[] buf, int cellX, int cellY)
        {
            int dx = cellX - CellMinX;
            int dy = cellY - CellMinY;

            int gridW = CellCountX;
            int blockIndex = dy * gridW + dx;
            long tableOffs = _zlibBlkTableOffsLOD0 + blockIndex * 8;
            uint dataOffset = ReadUInt32(tableOffs);
            uint compressedSize = ReadUInt32(tableOffs + 4);
            if (compressedSize == 0) { Array.Clear(buf, 0, buf.Length); return; }

            long absOffset = _zlibBlocksDataOffs + dataOffset;
            byte[] decompressed = DecompressZlib(_fileData, absOffset, compressedSize, 65536);
            if (decompressed == null) { Array.Clear(buf, 0, buf.Length); return; }

            // Texture data starts at byte 32768 (after 128*128 uint16 height data)
            int texOffset = CellResolution * CellResolution * 2;
            for (int i = 0; i < CellResolution * CellResolution; i++)
            {
                int off = texOffset + i * 2;
                buf[i] = (ushort)(decompressed[off] | (decompressed[off + 1] << 8));
            }
        }

        /// <summary>
        /// Sets a single vertex height as raw uint16 at LOD0.
        /// vertX/vertY are 0..127 within the cell.
        /// </summary>
        public void SetHeight(int cellX, int cellY, int vertX, int vertY, ushort raw)
        {
            if (cellX < CellMinX || cellX > CellMaxX || cellY < CellMinY || cellY > CellMaxY)
                throw new ArgumentOutOfRangeException($"Cell ({cellX}, {cellY}) is out of range");

            var tile = LoadTile(cellX, cellY, 0);
            int localX = ((cellX - CellMinX) & (TileSize - 1)) * CellResolution;
            int localY = ((cellY - CellMinY) & (TileSize - 1)) * CellResolution;
            tile.HeightMap[(localY + vertY) * TileStride + localX + vertX] = raw;

            _dirtyCells.Add((cellX - CellMinX, cellY - CellMinY));
        }

        /// <summary>
        /// Flattens terrain in a circle around a world-space point. The target height is
        /// sampled from the terrain at the center. A smooth blend band around the outer
        /// edge transitions from flat back to the original terrain.
        /// Respects edge cell boundaries (won't modify outermost ring of cells).
        /// </summary>
        /// <param name="worldX">World-space X of circle center.</param>
        /// <param name="worldY">World-space Y of circle center.</param>
        /// <param name="radius">Radius of the flat area in world units.</param>
        /// <param name="blendWidth">Width of the smooth transition band in world units (default 256 = 8 vertices).</param>
        /// <returns>The world-space height the area was flattened to.</returns>
        public float FlattenCircle(float worldX, float worldY, float radius, float blendWidth = 256f)
        {
            const float cellSize = 4096f;
            const float vertSpacing = 32f; // cellSize / CellResolution

            float targetHeight = SampleHeightAtWorld(worldX, worldY);
            ushort targetRaw = HeightToRaw(targetHeight);
            float outerRadius = radius + blendWidth;

            // Safe cell range (skip edge cells)
            int safeMinX = CellMinX + 1;
            int safeMaxX = CellMaxX - 1;
            int safeMinY = CellMinY + 1;
            int safeMaxY = CellMaxY - 1;

            for (int cy = safeMinY; cy <= safeMaxY; cy++)
            {
                for (int cx = safeMinX; cx <= safeMaxX; cx++)
                {
                    // Quick AABB reject: does this cell overlap the outer circle?
                    float cellWorldMinX = cx * cellSize;
                    float cellWorldMaxX = cellWorldMinX + 127 * vertSpacing;
                    float cellWorldMinY = cy * cellSize;
                    float cellWorldMaxY = cellWorldMinY + 127 * vertSpacing;

                    float nearX = Math.Clamp(worldX, cellWorldMinX, cellWorldMaxX);
                    float nearY = Math.Clamp(worldY, cellWorldMinY, cellWorldMaxY);
                    float nearDist = MathF.Sqrt((nearX - worldX) * (nearX - worldX) + (nearY - worldY) * (nearY - worldY));
                    if (nearDist > outerRadius)
                        continue;

                    var buf = new ushort[CellResolution * CellResolution];
                    GetCellHeightMap(buf, cx, cy, 0);
                    bool modified = false;

                    for (int vy = 0; vy < CellResolution; vy++)
                    {
                        float wy = cy * cellSize + vy * vertSpacing;
                        for (int vx = 0; vx < CellResolution; vx++)
                        {
                            float wx = cx * cellSize + vx * vertSpacing;
                            float dx = wx - worldX;
                            float dy = wy - worldY;
                            float dist = MathF.Sqrt(dx * dx + dy * dy);

                            if (dist <= radius)
                            {
                                buf[vy * CellResolution + vx] = targetRaw;
                                modified = true;
                            }
                            else if (dist < outerRadius)
                            {
                                float t = (dist - radius) / blendWidth; // 0 at inner edge, 1 at outer edge
                                // Smooth hermite interpolation for a nice curve
                                float s = t * t * (3f - 2f * t);
                                ushort original = buf[vy * CellResolution + vx];
                                buf[vy * CellResolution + vx] = LerpRaw(targetRaw, original, s);
                                modified = true;
                            }
                        }
                    }

                    if (modified)
                        SetCellHeightMap(buf, cx, cy);
                }
            }

            return targetHeight;
        }

        /// <summary>
        /// Flattens terrain in an axis-aligned rectangle around a world-space point.
        /// The target height is sampled from the terrain at the center.
        /// A smooth blend band around the outer edge transitions from flat back to original terrain.
        /// Respects edge cell boundaries (won't modify outermost ring of cells).
        /// </summary>
        /// <param name="worldX">World-space X of rectangle center.</param>
        /// <param name="worldY">World-space Y of rectangle center.</param>
        /// <param name="halfWidth">Half-width of the flat area in world units (X axis).</param>
        /// <param name="halfHeight">Half-height of the flat area in world units (Y axis).</param>
        /// <param name="blendWidth">Width of the smooth transition band in world units (default 256 = 8 vertices).</param>
        /// <returns>The world-space height the area was flattened to.</returns>
        public float FlattenSquare(float worldX, float worldY, float halfWidth, float halfHeight, float blendWidth = 256f)
        {
            const float cellSize = 4096f;
            const float vertSpacing = 32f;

            float targetHeight = SampleHeightAtWorld(worldX, worldY);
            ushort targetRaw = HeightToRaw(targetHeight);

            float outerHalfW = halfWidth + blendWidth;
            float outerHalfH = halfHeight + blendWidth;

            int safeMinX = CellMinX + 1;
            int safeMaxX = CellMaxX - 1;
            int safeMinY = CellMinY + 1;
            int safeMaxY = CellMaxY - 1;

            for (int cy = safeMinY; cy <= safeMaxY; cy++)
            {
                for (int cx = safeMinX; cx <= safeMaxX; cx++)
                {
                    // Quick AABB reject
                    float cellWorldMinX = cx * cellSize;
                    float cellWorldMaxX = cellWorldMinX + 127 * vertSpacing;
                    float cellWorldMinY = cy * cellSize;
                    float cellWorldMaxY = cellWorldMinY + 127 * vertSpacing;

                    if (cellWorldMaxX < worldX - outerHalfW || cellWorldMinX > worldX + outerHalfW) continue;
                    if (cellWorldMaxY < worldY - outerHalfH || cellWorldMinY > worldY + outerHalfH) continue;

                    var buf = new ushort[CellResolution * CellResolution];
                    GetCellHeightMap(buf, cx, cy, 0);
                    bool modified = false;

                    for (int vy = 0; vy < CellResolution; vy++)
                    {
                        float wy = cy * cellSize + vy * vertSpacing;
                        for (int vx = 0; vx < CellResolution; vx++)
                        {
                            float wx = cx * cellSize + vx * vertSpacing;

                            // Signed distance to the rectangle edge (negative = inside, positive = outside)
                            float distX = Math.Max(0, Math.Abs(wx - worldX) - halfWidth);
                            float distY = Math.Max(0, Math.Abs(wy - worldY) - halfHeight);
                            float dist = MathF.Sqrt(distX * distX + distY * distY);

                            if (dist <= 0f)
                            {
                                buf[vy * CellResolution + vx] = targetRaw;
                                modified = true;
                            }
                            else if (dist < blendWidth)
                            {
                                float t = dist / blendWidth;
                                float s = t * t * (3f - 2f * t);
                                ushort original = buf[vy * CellResolution + vx];
                                buf[vy * CellResolution + vx] = LerpRaw(targetRaw, original, s);
                                modified = true;
                            }
                        }
                    }

                    if (modified)
                        SetCellHeightMap(buf, cx, cy);
                }
            }

            return targetHeight;
        }

        /// <summary>
        /// Smooths the edges of dirty cells where they border unmodified cells.
        /// Creates a linear blend over `bandWidth` vertices at each dirty cell edge
        /// that neighbors a clean cell, eliminating hard seams.
        /// Call this before Save().
        /// </summary>
        public void SmoothDirtyCellEdges(int bandWidth = 16)
        {
            // Snapshot the dirty set — we'll mark neighbor cells dirty as we blend into them
            var originalDirty = new HashSet<(int, int)>(_dirtyCells);

            foreach (var (dx, dy) in originalDirty)
            {
                int cellX = dx + CellMinX;
                int cellY = dy + CellMinY;

                // Check each of the 4 neighbors
                BlendEdge(cellX, cellY, dx, dy, -1, 0, bandWidth, originalDirty); // left
                BlendEdge(cellX, cellY, dx, dy, 1, 0, bandWidth, originalDirty);  // right
                BlendEdge(cellX, cellY, dx, dy, 0, -1, bandWidth, originalDirty); // bottom
                BlendEdge(cellX, cellY, dx, dy, 0, 1, bandWidth, originalDirty);  // top
            }
        }

        private void BlendEdge(int cellX, int cellY, int dx, int dy,
            int dirX, int dirY, int bandWidth, HashSet<(int, int)> originalDirty)
        {
            int nx = dx + dirX;
            int ny = dy + dirY;

            // Skip if neighbor is out of bounds or also dirty
            if (nx < 0 || nx >= CellCountX || ny < 0 || ny >= CellCountY) return;
            if (originalDirty.Contains((nx, ny))) return;

            int neighborCellX = nx + CellMinX;
            int neighborCellY = ny + CellMinY;

            // Load both tiles
            var dirtyTile = LoadTile(cellX, cellY, 0);
            int dirtyLocalX = ((cellX - CellMinX) & (TileSize - 1)) * CellResolution;
            int dirtyLocalY = ((cellY - CellMinY) & (TileSize - 1)) * CellResolution;

            var cleanTile = LoadTile(neighborCellX, neighborCellY, 0);
            int cleanLocalX = ((neighborCellX - CellMinX) & (TileSize - 1)) * CellResolution;
            int cleanLocalY = ((neighborCellY - CellMinY) & (TileSize - 1)) * CellResolution;

            // Read original (unmodified) neighbor edge values for reference
            var neighborBuf = new ushort[CellResolution * CellResolution];
            GetCellHeightMapFromFile(neighborBuf, neighborCellX, neighborCellY);

            // Also read original dirty cell values for the edge
            var dirtyOrigBuf = new ushort[CellResolution * CellResolution];
            GetCellHeightMapFromFile(dirtyOrigBuf, cellX, cellY);

            int bw = Math.Min(bandWidth, CellResolution / 2);

            if (dirX != 0) // horizontal edge (left/right)
            {
                // Blend inside the dirty cell near the edge, and inside the neighbor cell near the edge
                for (int vy = 0; vy < CellResolution; vy++)
                {
                    for (int i = 0; i < bw; i++)
                    {
                        float t = (i + 1) / (float)(bw + 1); // 0..1 from edge toward interior

                        // Dirty cell side: blend from edge (t=0 → use original) to interior (t=1 → use modified)
                        int dvx = dirX > 0 ? (CellResolution - 1 - i) : i;
                        int dirtyIdx = (dirtyLocalY + vy) * TileStride + dirtyLocalX + dvx;
                        ushort modifiedVal = dirtyTile.HeightMap[dirtyIdx];
                        ushort originalVal = dirtyOrigBuf[vy * CellResolution + dvx];
                        dirtyTile.HeightMap[dirtyIdx] = LerpRaw(originalVal, modifiedVal, t);

                        // Neighbor cell side: blend from interior (t=0 → keep original) to edge (t=1 → approach modified)
                        int nvx = dirX > 0 ? i : (CellResolution - 1 - i);
                        int cleanIdx = (cleanLocalY + vy) * TileStride + cleanLocalX + nvx;
                        ushort neighborVal = neighborBuf[vy * CellResolution + nvx];

                        // Get the modified value at the dirty cell's edge for the target
                        int edgeVx = dirX > 0 ? (CellResolution - 1) : 0;
                        ushort edgeModified = dirtyTile.HeightMap[(dirtyLocalY + vy) * TileStride + dirtyLocalX + edgeVx];
                        ushort edgeOriginal = dirtyOrigBuf[vy * CellResolution + edgeVx];
                        ushort delta = (ushort)Math.Abs(edgeModified - edgeOriginal);
                        float neighborT = 1f - t; // strongest near edge, fades toward interior
                        float blend = neighborT * (edgeModified >= edgeOriginal ? delta : -delta) / 65535f;
                        float nHeight = RawToHeight(neighborVal) + blend * (WorldHeightMax - WorldHeightMin);
                        cleanTile.HeightMap[cleanIdx] = HeightToRaw(nHeight);
                    }
                }
                _dirtyCells.Add((nx, ny));
            }
            else // vertical edge (top/bottom)
            {
                for (int vx = 0; vx < CellResolution; vx++)
                {
                    for (int i = 0; i < bw; i++)
                    {
                        float t = (i + 1) / (float)(bw + 1);

                        int dvy = dirY > 0 ? (CellResolution - 1 - i) : i;
                        int dirtyIdx = (dirtyLocalY + dvy) * TileStride + dirtyLocalX + vx;
                        ushort modifiedVal = dirtyTile.HeightMap[dirtyIdx];
                        ushort originalVal = dirtyOrigBuf[dvy * CellResolution + vx];
                        dirtyTile.HeightMap[dirtyIdx] = LerpRaw(originalVal, modifiedVal, t);

                        int nvy = dirY > 0 ? i : (CellResolution - 1 - i);
                        int cleanIdx = (cleanLocalY + nvy) * TileStride + cleanLocalX + vx;
                        ushort neighborVal = neighborBuf[nvy * CellResolution + vx];

                        int edgeVy = dirY > 0 ? (CellResolution - 1) : 0;
                        ushort edgeModified = dirtyTile.HeightMap[(dirtyLocalY + edgeVy) * TileStride + dirtyLocalX + vx];
                        ushort edgeOriginal = dirtyOrigBuf[edgeVy * CellResolution + vx];
                        ushort delta = (ushort)Math.Abs(edgeModified - edgeOriginal);
                        float neighborT = 1f - t;
                        float blend = neighborT * (edgeModified >= edgeOriginal ? delta : -delta) / 65535f;
                        float nHeight = RawToHeight(neighborVal) + blend * (WorldHeightMax - WorldHeightMin);
                        cleanTile.HeightMap[cleanIdx] = HeightToRaw(nHeight);
                    }
                }
                _dirtyCells.Add((nx, ny));
            }
        }

        private static ushort LerpRaw(ushort a, ushort b, float t)
        {
            return (ushort)Math.Clamp((int)(a + (b - a) * t + 0.5f), 0, 65535);
        }

        /// <summary>
        /// Reads a cell's height map directly from the original file data (ignoring tile cache modifications).
        /// </summary>
        private void GetCellHeightMapFromFile(ushort[] buf, int cellX, int cellY)
        {
            int dx = cellX - CellMinX;
            int dy = cellY - CellMinY;

            if (!IsStarfield)
            {
                // Fall back to reading from tile cache for non-Starfield
                GetCellHeightMap(buf, cellX, cellY, 0);
                return;
            }

            int gridW = CellCountX;
            int blockIndex = dy * gridW + dx;
            long tableOffs = _zlibBlkTableOffsLOD0 + blockIndex * 8;
            uint dataOffset = ReadUInt32(tableOffs);
            uint compressedSize = ReadUInt32(tableOffs + 4);
            if (compressedSize == 0) { Array.Clear(buf, 0, buf.Length); return; }

            long absOffset = _zlibBlocksDataOffs + dataOffset;
            byte[] decompressed = DecompressZlib(_fileData, absOffset, compressedSize, 65536);
            if (decompressed == null) { Array.Clear(buf, 0, buf.Length); return; }

            for (int i = 0; i < CellResolution * CellResolution; i++)
            {
                int off = i * 2;
                buf[i] = (ushort)(decompressed[off] | (decompressed[off + 1] << 8));
            }
        }

        /// <summary>
        /// Saves the BTD to a new file. Modified cells are recompressed; unmodified blocks
        /// are copied verbatim. Currently only supports Starfield format.
        /// </summary>
        public void Save(string outputPath, bool updateMinMax = true)
        {
            if (!IsStarfield)
                throw new NotSupportedException("Save is currently only supported for Starfield BTD files");

            // Copy the prefix (header + metadata + block tables) into a byte array we can patch
            var prefix = new byte[_zlibBlocksDataOffs];
            Array.Copy(_fileData, 0, prefix, 0, prefix.Length);

            // Update cell height min/max and LOD4 height map for dirty cells
            if (updateMinMax)
            {
                foreach (var (dx, dy) in _dirtyCells)
                {
                    int cellX = dx + CellMinX;
                    int cellY = dy + CellMinY;
                    var tile = LoadTile(cellX, cellY, 0);
                    int localX = ((cellX - CellMinX) & (TileSize - 1)) * CellResolution;
                    int localY = ((cellY - CellMinY) & (TileSize - 1)) * CellResolution;

                    // Scan for min/max raw height in this cell
                    ushort cellMin = ushort.MaxValue;
                    ushort cellMax = ushort.MinValue;
                    for (int vy = 0; vy < CellResolution; vy++)
                    {
                        int row = (localY + vy) * TileStride + localX;
                        for (int vx = 0; vx < CellResolution; vx++)
                        {
                            ushort val = tile.HeightMap[row + vx];
                            if (val < cellMin) cellMin = val;
                            if (val > cellMax) cellMax = val;
                        }
                    }

                    // Cell height min/max: stored as float pairs in UNSCALED coordinates
                    // (before Starfield 8x multiplier), row-major (y * countX + x)
                    long minMaxOffs = _cellHeightMinMaxOffs + ((long)dy * CellCountX + dx) * 8;
                    float fMin = RawToHeight(cellMin);
                    float fMax = RawToHeight(cellMax);
                    if (IsStarfield) { fMin /= 8f; fMax /= 8f; }
                    Array.Copy(BitConverter.GetBytes(fMin), 0, prefix, minMaxOffs, 4);
                    Array.Copy(BitConverter.GetBytes(fMax), 0, prefix, minMaxOffs + 4, 4);

                    // LOD4 height map: 128 bytes per cell = 8x8 grid of uint16 values (64 values = 128 bytes)
                    // Downsample from 128x128 to 8x8 by taking every 16th sample
                    long lod4Offs = _lod4HeightMapOffs + ((long)dy * CellCountX + dx) * 128;
                    for (int ly = 0; ly < 8; ly++)
                    {
                        int srcRow = (localY + ly * 16) * TileStride + localX;
                        for (int lx = 0; lx < 8; lx++)
                        {
                            ushort val = tile.HeightMap[srcRow + lx * 16];
                            prefix[lod4Offs] = (byte)(val & 0xFF);
                            prefix[lod4Offs + 1] = (byte)(val >> 8);
                            lod4Offs += 2;
                        }
                    }
                }
            }

            // Build compressed block data into a separate stream, collect table patches
            using var blockData = new MemoryStream();
            uint newDataOffset = 0;

            for (int lod = 3; lod >= 0; lod--)
            {
                int cellsPerBlock = 1 << lod;
                int gridW = CeilDiv(CellCountX, cellsPerBlock);
                int gridH = CeilDiv(CellCountY, cellsPerBlock);
                long tableBase = GetLodTableOffset(lod);

                for (int gy = 0; gy < gridH; gy++)
                {
                    for (int gx = 0; gx < gridW; gx++)
                    {
                        int blockIndex = gy * gridW + gx;
                        long tableOffs = tableBase + blockIndex * 8;

                        uint origDataOffset = ReadUInt32(tableOffs);
                        uint origCompressedSize = ReadUInt32(tableOffs + 4);

                        if (origCompressedSize == 0)
                        {
                            WriteUInt32InArray(prefix, tableOffs, 0);
                            WriteUInt32InArray(prefix, tableOffs + 4, 0);
                            continue;
                        }

                        // Check if any cell in this block is dirty (LOD0: 1 cell per block)
                        bool isDirty = false;
                        if (lod == 0)
                        {
                            isDirty = _dirtyCells.Contains((gx, gy));
                        }
                        else
                        {
                            for (int dy = 0; dy < cellsPerBlock && !isDirty; dy++)
                                for (int dx = 0; dx < cellsPerBlock && !isDirty; dx++)
                                    isDirty = _dirtyCells.Contains((gx * cellsPerBlock + dx, gy * cellsPerBlock + dy));
                        }

                        byte[] compressedBlock;
                        if (isDirty && lod == 0)
                        {
                            // Decompress original to get texture data, patch height data, recompress
                            long absOffset = _zlibBlocksDataOffs + origDataOffset;
                            byte[] decompressed = DecompressZlib(_fileData, absOffset, origCompressedSize, 65536);
                            if (decompressed == null)
                                decompressed = new byte[65536];

                            // Patch height data from tile cache (first 128*128 uint16)
                            int cellX = gx + CellMinX;
                            int cellY = gy + CellMinY;
                            var tile = LoadTile(cellX, cellY, 0);
                            int localX = ((cellX - CellMinX) & (TileSize - 1)) * CellResolution;
                            int localY = ((cellY - CellMinY) & (TileSize - 1)) * CellResolution;

                            int pos = 0;
                            for (int vy = 0; vy < CellResolution; vy++)
                            {
                                int srcRow = (localY + vy) * TileStride + localX;
                                for (int vx = 0; vx < CellResolution; vx++)
                                {
                                    ushort val = tile.HeightMap[srcRow + vx];
                                    decompressed[pos] = (byte)(val & 0xFF);
                                    decompressed[pos + 1] = (byte)(val >> 8);
                                    pos += 2;
                                }
                            }

                            // Patch texture data if modified (second 128*128 uint16)
                            if (_dirtyTextures.TryGetValue((gx, gy), out var texData))
                            {
                                int texPos = CellResolution * CellResolution * 2; // 32768
                                for (int i = 0; i < CellResolution * CellResolution; i++)
                                {
                                    decompressed[texPos] = (byte)(texData[i] & 0xFF);
                                    decompressed[texPos + 1] = (byte)(texData[i] >> 8);
                                    texPos += 2;
                                }
                            }

                            compressedBlock = CompressZlib(decompressed);
                        }
                        else
                        {
                            // Copy original compressed bytes verbatim
                            long absOffset = _zlibBlocksDataOffs + origDataOffset;
                            compressedBlock = new byte[origCompressedSize];
                            Array.Copy(_fileData, absOffset, compressedBlock, 0, origCompressedSize);
                        }

                        // Patch block table entry in prefix
                        WriteUInt32InArray(prefix, tableOffs, newDataOffset);
                        WriteUInt32InArray(prefix, tableOffs + 4, (uint)compressedBlock.Length);

                        // Append compressed block
                        blockData.Write(compressedBlock, 0, compressedBlock.Length);
                        newDataOffset += (uint)compressedBlock.Length;
                    }
                }
            }

            // Write prefix + block data
            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            fs.Write(prefix, 0, prefix.Length);
            blockData.WriteTo(fs);
        }

        private static byte[] CompressZlib(byte[] data)
        {
            using var ms = new MemoryStream();
            using (var zlib = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }

        private static void WriteUInt32InArray(byte[] buf, long offset, uint value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private TileData LoadTile(int cellX, int cellY, int lod)
        {
            int tileX = (cellX - CellMinX) & ~(TileSize - 1);
            int tileY = (cellY - CellMinY) & ~(TileSize - 1);
            uint cacheKey = (uint)(tileY << 16) | (uint)tileX;

            if (_tileCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var tile = new TileData
            {
                X0 = tileX,
                Y0 = tileY,
                HeightMap = new ushort[TileStride * TileStride]
            };

            if (IsStarfield)
                LoadBlocksStarfield(tile, tileX, tileY, lod);
            else
                LoadBlocksStandard(tile, tileX, tileY, lod);

            // Evict oldest entry if cache is full
            if (_tileCache.Count >= _tileCacheMaxSize)
                _tileCache.Clear();

            _tileCache[cacheKey] = tile;
            return tile;
        }

        private void LoadBlocksStarfield(TileData tile, int tileX, int tileY, int lod)
        {
            int cellsPerBlock = 1 << lod;
            int blocksPerTile = TileSize >> lod;

            for (int by = 0; by < blocksPerTile; by++)
            {
                int cellY = tileY + (by << lod);
                if (cellY >= CellCountY) break;

                for (int bx = 0; bx < blocksPerTile; bx++)
                {
                    int cellX = tileX + (bx << lod);
                    if (cellX >= CellCountX) break;

                    int gridW = CeilDiv(CellCountX, cellsPerBlock);
                    int blockIndex = (cellY >> lod) * gridW + (cellX >> lod);

                    long tableOffs = GetLodTableOffset(lod) + blockIndex * 8;
                    if (tableOffs + 8 > _fileData.Length) continue;

                    uint dataOffset = ReadUInt32(tableOffs);
                    uint compressedSize = ReadUInt32(tableOffs + 4);
                    if (compressedSize == 0) continue;

                    long absOffset = _zlibBlocksDataOffs + dataOffset;
                    if (absOffset + compressedSize > _fileData.Length) continue;

                    byte[] decompressed = DecompressZlib(_fileData, absOffset, compressedSize, 65536);
                    if (decompressed == null) continue;

                    // First 128*128 uint16 = height data, second 128*128 uint16 = texture data.
                    // Each decompressed block is 128x128 values written at stride (1 << lod)
                    // into the tile array, so blocks are spaced by 128 * step in tile coordinates.
                    int step = 1 << lod;
                    int destX = bx * CellResolution * step;
                    int destY = by * CellResolution * step;

                    int srcPos = 0;
                    for (int vy = 0; vy < CellResolution; vy++)
                    {
                        int dstRow = (destY + vy * step) * TileStride + destX;
                        for (int vx = 0; vx < CellResolution; vx++)
                        {
                            ushort val = (ushort)(decompressed[srcPos] | (decompressed[srcPos + 1] << 8));
                            srcPos += 2;
                            if (dstRow + vx * step < tile.HeightMap.Length)
                                tile.HeightMap[dstRow + vx * step] = val;
                        }
                    }
                }
            }
        }

        private void LoadBlocksStandard(TileData tile, int tileX, int tileY, int lod)
        {
            int cellsPerBlock = 1 << lod;
            int blocksPerTile = TileSize >> lod;

            for (int by = 0; by < blocksPerTile; by++)
            {
                int cellY = tileY + (by << lod);
                if (cellY >= CellCountY) break;

                for (int bx = 0; bx < blocksPerTile; bx++)
                {
                    int cellX = tileX + (bx << lod);
                    if (cellX >= CellCountX) break;

                    int gridW = CeilDiv(CellCountX, cellsPerBlock);
                    int blockIndex = (cellY >> lod) * gridW + (cellX >> lod);

                    // Standard format has 2 blocks per entry for LOD3, LOD2, LOD0
                    int blocksPerEntry = (lod == 1) ? 1 : 2;
                    long tableOffs = GetLodTableOffset(lod) + blockIndex * blocksPerEntry * 8;
                    if (tableOffs + 8 > _fileData.Length) continue;

                    uint dataOffset = ReadUInt32(tableOffs);
                    uint compressedSize = ReadUInt32(tableOffs + 4);
                    if (compressedSize == 0) continue;

                    long absOffset = _zlibBlocksDataOffs + dataOffset;
                    if (absOffset + compressedSize > _fileData.Length) continue;

                    // Standard blocks decompress to 49152 bytes (height + texture interleaved)
                    byte[] decompressed = DecompressZlib(_fileData, absOffset, compressedSize, 49152);
                    if (decompressed == null) continue;

                    int step = 1 << lod;
                    int destX = bx * CellResolution * step;
                    int destY = by * CellResolution * step;
                    int blockW = lod >= 2 ? 64 : CellResolution;
                    int blockH = lod >= 2 ? 64 : CellResolution;

                    int srcPos = 0;
                    for (int vy = 0; vy < blockH; vy++)
                    {
                        int dstRow = (destY + vy * step) * TileStride + destX;
                        for (int vx = 0; vx < blockW; vx++)
                        {
                            ushort val = (ushort)(decompressed[srcPos] | (decompressed[srcPos + 1] << 8));
                            srcPos += 2;
                            if (dstRow + vx * step < tile.HeightMap.Length)
                                tile.HeightMap[dstRow + vx * step] = val;
                        }
                    }
                }
            }
        }

        private long GetLodTableOffset(int lod)
        {
            return lod switch
            {
                0 => _zlibBlkTableOffsLOD0,
                1 => _zlibBlkTableOffsLOD1,
                2 => _zlibBlkTableOffsLOD2,
                3 => _zlibBlkTableOffsLOD3,
                _ => _zlibBlkTableOffsLOD0
            };
        }

        private static byte[] DecompressZlib(byte[] data, long offset, uint compressedSize, int expectedSize)
        {
            try
            {
                if (compressedSize < 3) return null;

                using var ms = new MemoryStream(data, (int)offset, (int)compressedSize);
                using var zlib = new ZLibStream(ms, CompressionMode.Decompress);
                var result = new byte[expectedSize];
                int totalRead = 0;
                while (totalRead < expectedSize)
                {
                    int bytesRead = zlib.Read(result, totalRead, expectedSize - totalRead);
                    if (bytesRead == 0) break;
                    totalRead += bytesRead;
                }
                return result;
            }
            catch
            {
                return null;
            }
        }

        private uint ReadUInt32(long offset)
        {
            return (uint)(_fileData[offset]
                | (_fileData[offset + 1] << 8)
                | (_fileData[offset + 2] << 16)
                | (_fileData[offset + 3] << 24));
        }

        private int ReadInt32(long offset)
        {
            return (int)ReadUInt32(offset);
        }

        private float ReadFloat(long offset)
        {
            return BitConverter.ToSingle(_fileData, (int)offset);
        }

        private static int CeilDiv(int a, int b)
        {
            return (a + b - 1) / b;
        }
    }
}
