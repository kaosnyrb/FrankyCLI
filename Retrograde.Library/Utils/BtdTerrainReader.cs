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
    public class BtdTerrainReader
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

        private byte[] _fileData;
        private long _zlibBlocksDataOffs;
        private long _zlibBlkTableOffsLOD0;
        private long _zlibBlkTableOffsLOD1;
        private long _zlibBlkTableOffsLOD2;
        private long _zlibBlkTableOffsLOD3;

        // Tile cache: key = (tileY << 16) | tileX
        private readonly Dictionary<uint, TileData> _tileCache = new Dictionary<uint, TileData>();
        private int _tileCacheMaxSize = 16;

        private class TileData
        {
            public int X0;
            public int Y0;
            public ushort[] HeightMap; // TileStride * TileStride
        }

        public BtdTerrainReader()
        {
        }

        public BtdTerrainReader(string path)
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
            pos += (long)CellCountY * CellCountX * 8;

            // Land texture map: 32 bytes per cell
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
            pos += (long)CellCountY * CellCountX * 128;

            // LOD4 land textures: 128 bytes per cell
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
                // Zlib format: 2-byte header then deflate data
                // Skip the 2-byte zlib header for DeflateStream
                if (compressedSize < 3) return null;

                using var ms = new MemoryStream(data, (int)offset + 2, (int)compressedSize - 2);
                using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
                var result = new byte[expectedSize];
                int totalRead = 0;
                while (totalRead < expectedSize)
                {
                    int bytesRead = deflate.Read(result, totalRead, expectedSize - totalRead);
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
