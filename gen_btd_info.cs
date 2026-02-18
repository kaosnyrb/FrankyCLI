using Retrograde.Utils;
using System;
using System.Linq;

namespace FrankyCLI
{
    public class gen_btd_info
    {
        public static int Generate(string[] args)
        {
            string btdPath = @"C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data\terrain\oebb008world.btd";

            if (args.Length >= 6 && !string.IsNullOrWhiteSpace(args[5]))
                btdPath = args[5];

            Console.WriteLine($"=== BTD Info Dump ===");
            Console.WriteLine($"File: {btdPath}");
            Console.WriteLine();

            if (!System.IO.File.Exists(btdPath))
            {
                Console.WriteLine($"ERROR: BTD file not found at '{btdPath}'");
                return 1;
            }

            var fileBytes = System.IO.File.ReadAllBytes(btdPath);
            Console.WriteLine($"File size: {fileBytes.Length:N0} bytes ({fileBytes.Length / 1024.0 / 1024.0:F2} MB)");
            Console.WriteLine();

            // --- Raw header bytes ---
            Console.WriteLine("--- Raw Header (0x00-0x27) ---");
            Console.WriteLine($"  [0x00] Magic:        {(char)fileBytes[0]}{(char)fileBytes[1]}{(char)fileBytes[2]}{(char)fileBytes[3]}");
            Console.WriteLine($"  [0x04] Version:      {ReadUInt32(fileBytes, 0x04)}");
            Console.WriteLine($"  [0x08] HeightMin:    {ReadFloat(fileBytes, 0x08)}");
            Console.WriteLine($"  [0x0C] HeightMax:    {ReadFloat(fileBytes, 0x0C)}");
            Console.WriteLine($"  [0x10] ResX:         {ReadUInt32(fileBytes, 0x10)} (0x{ReadUInt32(fileBytes, 0x10):X8})");
            Console.WriteLine($"  [0x14] ResY:         {ReadUInt32(fileBytes, 0x14)} (0x{ReadUInt32(fileBytes, 0x14):X8})");
            Console.WriteLine($"  [0x18] CellMinX:     {ReadInt32(fileBytes, 0x18)}");
            Console.WriteLine($"  [0x1C] CellMinY:     {ReadInt32(fileBytes, 0x1C)}");
            Console.WriteLine($"  [0x20] CellMaxX:     {ReadInt32(fileBytes, 0x20)}");
            Console.WriteLine($"  [0x24] CellMaxY:     {ReadInt32(fileBytes, 0x24)}");
            Console.WriteLine();

            // --- Parsed header via reader ---
            var reader = new BtdTerrainReader(btdPath);
            Console.WriteLine("--- Parsed Header ---");
            Console.WriteLine($"  IsStarfield:     {reader.IsStarfield}");
            Console.WriteLine($"  CellMin:         ({reader.CellMinX}, {reader.CellMinY})");
            Console.WriteLine($"  CellMax:         ({reader.CellMaxX}, {reader.CellMaxY})");
            Console.WriteLine($"  CellCount:       {reader.CellCountX} x {reader.CellCountY} = {reader.CellCountX * reader.CellCountY} cells");
            Console.WriteLine($"  WorldHeightMin:  {reader.WorldHeightMin:F4}");
            Console.WriteLine($"  WorldHeightMax:  {reader.WorldHeightMax:F4}");
            Console.WriteLine($"  HeightRange:     {reader.WorldHeightMax - reader.WorldHeightMin:F4}");
            Console.WriteLine($"  RawToHeight(0):      {reader.RawToHeight(0):F4}");
            Console.WriteLine($"  RawToHeight(32768):  {reader.RawToHeight(32768):F4}");
            Console.WriteLine($"  RawToHeight(65535):  {reader.RawToHeight(65535):F4}");
            Console.WriteLine($"  HeightToRaw(0):      {reader.HeightToRaw(0f)}");
            Console.WriteLine();

            // --- Section layout ---
            // Walk the file the same way CalculateSectionOffsets does, but print everything
            Console.WriteLine("--- Section Layout ---");
            long pos = 0x28;
            uint ltexCnt = ReadUInt32(fileBytes, pos);
            Console.WriteLine($"  [0x{pos:X8}] LTEX form ID count: {ltexCnt}");
            pos += 4;

            // Print a few LTEX form IDs
            int ltexShow = (int)Math.Min(ltexCnt, 10);
            for (int i = 0; i < ltexShow; i++)
            {
                uint fid = ReadUInt32(fileBytes, pos + i * 4);
                Console.WriteLine($"    LTEX[{i}]: 0x{fid:X8}");
            }
            if (ltexCnt > 10) Console.WriteLine($"    ... ({ltexCnt - 10} more)");
            pos += ltexCnt * 4;

            long cellHeightMinMaxOffs = pos;
            long cellHeightMinMaxSize = (long)reader.CellCountY * reader.CellCountX * 8;
            Console.WriteLine($"  [0x{pos:X8}] Cell height min/max map: {cellHeightMinMaxSize:N0} bytes ({reader.CellCountX}x{reader.CellCountY} cells, 8 bytes each)");
            pos += cellHeightMinMaxSize;

            long ltexMapSize = (long)reader.CellCountY * reader.CellCountX * 32;
            Console.WriteLine($"  [0x{pos:X8}] Land texture map: {ltexMapSize:N0} bytes (32 bytes/cell)");
            pos += ltexMapSize;

            if (!reader.IsStarfield)
            {
                uint gcvrCnt = ReadUInt32(fileBytes, pos);
                Console.WriteLine($"  [0x{pos:X8}] GCVR form ID count: {gcvrCnt}");
                pos += 4;
                pos += gcvrCnt * 4;
                long gcvrMapSize = (long)reader.CellCountY * reader.CellCountX * 32;
                Console.WriteLine($"  [0x{pos:X8}] Ground cover map: {gcvrMapSize:N0} bytes");
                pos += gcvrMapSize;
            }

            long lod4HeightOffs = pos;
            long lod4HeightSize = (long)reader.CellCountY * reader.CellCountX * 128;
            Console.WriteLine($"  [0x{pos:X8}] LOD4 height map: {lod4HeightSize:N0} bytes (128 bytes/cell, 8x8 grid of uint16)");
            pos += lod4HeightSize;

            long lod4LtexSize = (long)reader.CellCountY * reader.CellCountX * 128;
            Console.WriteLine($"  [0x{pos:X8}] LOD4 land textures: {lod4LtexSize:N0} bytes (128 bytes/cell)");
            pos += lod4LtexSize;

            if (!reader.IsStarfield)
            {
                long vclrSize = (long)reader.CellCountY * reader.CellCountX * 128;
                Console.WriteLine($"  [0x{pos:X8}] Vertex color LOD4: {vclrSize:N0} bytes");
                pos += vclrSize;
            }

            // Block tables
            int countX = reader.CellCountX;
            int countY = reader.CellCountY;
            int entriesPerBlock = reader.IsStarfield ? 1 : 2;

            int gridLOD3 = CeilDiv(countY, 8) * CeilDiv(countX, 8);
            long tableLOD3Size = (long)gridLOD3 * (reader.IsStarfield ? 8 : 16);
            Console.WriteLine($"  [0x{pos:X8}] Block table LOD3: {gridLOD3} blocks, {tableLOD3Size:N0} bytes");
            long tableLOD3Offs = pos;
            pos += tableLOD3Size;

            int gridLOD2 = CeilDiv(countY, 4) * CeilDiv(countX, 4);
            long tableLOD2Size = (long)gridLOD2 * (reader.IsStarfield ? 8 : 16);
            Console.WriteLine($"  [0x{pos:X8}] Block table LOD2: {gridLOD2} blocks, {tableLOD2Size:N0} bytes");
            long tableLOD2Offs = pos;
            pos += tableLOD2Size;

            int gridLOD1 = CeilDiv(countY, 2) * CeilDiv(countX, 2);
            long tableLOD1Size = (long)gridLOD1 * 8;
            Console.WriteLine($"  [0x{pos:X8}] Block table LOD1: {gridLOD1} blocks, {tableLOD1Size:N0} bytes");
            long tableLOD1Offs = pos;
            pos += tableLOD1Size;

            int gridLOD0 = countY * countX;
            long tableLOD0Size = (long)gridLOD0 * (reader.IsStarfield ? 8 : 16);
            Console.WriteLine($"  [0x{pos:X8}] Block table LOD0: {gridLOD0} blocks, {tableLOD0Size:N0} bytes");
            long tableLOD0Offs = pos;
            pos += tableLOD0Size;

            Console.WriteLine($"  [0x{pos:X8}] Compressed block data starts");
            long blockDataOffs = pos;
            Console.WriteLine($"  Prefix size: {blockDataOffs:N0} bytes ({blockDataOffs / 1024.0:F1} KB)");
            Console.WriteLine($"  Block data size: {fileBytes.Length - blockDataOffs:N0} bytes ({(fileBytes.Length - blockDataOffs) / 1024.0 / 1024.0:F2} MB)");
            Console.WriteLine();

            // --- Cell height min/max sample ---
            Console.WriteLine("--- Cell Height Min/Max (sample) ---");
            float globalMin = float.MaxValue;
            float globalMax = float.MinValue;
            int uniformCells = 0;
            for (int dy = 0; dy < reader.CellCountY; dy++)
            {
                for (int dx = 0; dx < reader.CellCountX; dx++)
                {
                    long off = cellHeightMinMaxOffs + ((long)dy * reader.CellCountX + dx) * 8;
                    float cMin = ReadFloat(fileBytes, off);
                    float cMax = ReadFloat(fileBytes, off + 4);
                    if (cMin < globalMin) globalMin = cMin;
                    if (cMax > globalMax) globalMax = cMax;
                    if (Math.Abs(cMax - cMin) < 0.01f) uniformCells++;
                }
            }
            Console.WriteLine($"  Global cell min: {globalMin:F4}");
            Console.WriteLine($"  Global cell max: {globalMax:F4}");
            Console.WriteLine($"  Uniform cells (min==max): {uniformCells} / {reader.CellCountX * reader.CellCountY}");

            // Print corners and center
            PrintCellMinMax(fileBytes, cellHeightMinMaxOffs, reader, 0, 0, "Top-left");
            PrintCellMinMax(fileBytes, cellHeightMinMaxOffs, reader, reader.CellCountX - 1, 0, "Top-right");
            PrintCellMinMax(fileBytes, cellHeightMinMaxOffs, reader, 0, reader.CellCountY - 1, "Bottom-left");
            PrintCellMinMax(fileBytes, cellHeightMinMaxOffs, reader, reader.CellCountX - 1, reader.CellCountY - 1, "Bottom-right");
            PrintCellMinMax(fileBytes, cellHeightMinMaxOffs, reader, reader.CellCountX / 2, reader.CellCountY / 2, "Center");
            Console.WriteLine();

            // --- Block table stats per LOD ---
            Console.WriteLine("--- Compressed Block Stats ---");
            PrintBlockStats(fileBytes, "LOD3", tableLOD3Offs, gridLOD3, reader.IsStarfield ? 1 : 2);
            PrintBlockStats(fileBytes, "LOD2", tableLOD2Offs, gridLOD2, reader.IsStarfield ? 1 : 2);
            PrintBlockStats(fileBytes, "LOD1", tableLOD1Offs, gridLOD1, 1);
            PrintBlockStats(fileBytes, "LOD0", tableLOD0Offs, gridLOD0, reader.IsStarfield ? 1 : 2);
            Console.WriteLine();

            // --- LOD0 decompressed block analysis ---
            Console.WriteLine("--- LOD0 Block Content Analysis (first non-empty block) ---");
            for (int i = 0; i < gridLOD0; i++)
            {
                long off = tableLOD0Offs + (long)i * 8;
                uint dataOff = ReadUInt32(fileBytes, off);
                uint compSize = ReadUInt32(fileBytes, off + 4);
                if (compSize == 0) continue;

                long absOff = blockDataOffs + dataOff;
                byte[] decompressed = DecompressZlib(fileBytes, absOff, compSize, 65536);
                if (decompressed == null) continue;

                int cellX = i % reader.CellCountX + reader.CellMinX;
                int cellY = i / reader.CellCountX + reader.CellMinY;
                Console.WriteLine($"  Block {i} = cell ({cellX},{cellY}), compressed {compSize:N0} -> {decompressed.Length:N0} bytes");
                Console.WriteLine($"  Compression ratio: {(float)compSize / decompressed.Length:P1}");

                // Height portion: first 128*128*2 = 32768 bytes
                AnalyzeUint16Region(decompressed, 0, 128 * 128, "Height (0..32767)", reader);

                // Texture portion: next 128*128*2 = 32768 bytes
                AnalyzeUint16Region(decompressed, 32768, 128 * 128, "Texture (32768..65535)", null);

                break; // just the first one
            }
            Console.WriteLine();

            // --- LOD0 center block analysis ---
            Console.WriteLine("--- LOD0 Center Block Analysis ---");
            {
                int cx = reader.CellCountX / 2;
                int cy = reader.CellCountY / 2;
                int idx = cy * reader.CellCountX + cx;
                long off = tableLOD0Offs + (long)idx * 8;
                uint dataOff = ReadUInt32(fileBytes, off);
                uint compSize = ReadUInt32(fileBytes, off + 4);
                if (compSize > 0)
                {
                    long absOff = blockDataOffs + dataOff;
                    byte[] decompressed = DecompressZlib(fileBytes, absOff, compSize, 65536);
                    if (decompressed != null)
                    {
                        int cellX = cx + reader.CellMinX;
                        int cellY = cy + reader.CellMinY;
                        Console.WriteLine($"  Center cell ({cellX},{cellY}), compressed {compSize:N0} -> {decompressed.Length:N0} bytes");
                        AnalyzeUint16Region(decompressed, 0, 128 * 128, "Height", reader);
                        AnalyzeUint16Region(decompressed, 32768, 128 * 128, "Texture", null);
                    }
                }
            }
            Console.WriteLine();

            // --- LOD4 height map sample ---
            Console.WriteLine("--- LOD4 Height Map (center cell) ---");
            {
                int cx = reader.CellCountX / 2;
                int cy = reader.CellCountY / 2;
                long off = lod4HeightOffs + ((long)cy * reader.CellCountX + cx) * 128;
                Console.WriteLine($"  Cell ({cx + reader.CellMinX},{cy + reader.CellMinY}), 8x8 grid:");
                for (int ly = 0; ly < 8; ly++)
                {
                    Console.Write("    ");
                    for (int lx = 0; lx < 8; lx++)
                    {
                        ushort v = (ushort)(fileBytes[off] | (fileBytes[off + 1] << 8));
                        off += 2;
                        Console.Write($"{reader.RawToHeight(v),9:F1} ");
                    }
                    Console.WriteLine();
                }
            }
            Console.WriteLine();

            // --- Land texture map sample ---
            Console.WriteLine("--- Land Texture Map (center cell, 32 bytes) ---");
            {
                int cx = reader.CellCountX / 2;
                int cy = reader.CellCountY / 2;
                long off = cellHeightMinMaxOffs + (long)reader.CellCountY * reader.CellCountX * 8
                         + ((long)cy * reader.CellCountX + cx) * 32;
                Console.Write("  Hex:");
                for (int i = 0; i < 32; i++)
                    Console.Write($" {fileBytes[off + i]:X2}");
                Console.WriteLine();
                // Interpret as 8 uint32 values
                Console.Write("  As uint32:");
                for (int i = 0; i < 8; i++)
                    Console.Write($" {ReadUInt32(fileBytes, off + i * 4)}");
                Console.WriteLine();
                // Interpret as 16 uint16 values
                Console.Write("  As uint16:");
                for (int i = 0; i < 16; i++)
                {
                    ushort v = (ushort)(fileBytes[off + i * 2] | (fileBytes[off + i * 2 + 1] << 8));
                    Console.Write($" {v}");
                }
                Console.WriteLine();
            }
            Console.WriteLine();

            // --- Height distribution across all cells ---
            Console.WriteLine("--- Height Distribution (via cell min/max metadata) ---");
            int[] buckets = new int[10];
            float range = reader.WorldHeightMax - reader.WorldHeightMin;
            for (int dy = 0; dy < reader.CellCountY; dy++)
            {
                for (int dx = 0; dx < reader.CellCountX; dx++)
                {
                    long off = cellHeightMinMaxOffs + ((long)dy * reader.CellCountX + dx) * 8;
                    float cMin = ReadFloat(fileBytes, off);
                    float cMax = ReadFloat(fileBytes, off + 4);
                    float mid = (cMin + cMax) / 2f;
                    int bucket = (int)((mid - reader.WorldHeightMin) / range * (buckets.Length - 1));
                    bucket = Math.Clamp(bucket, 0, buckets.Length - 1);
                    buckets[bucket]++;
                }
            }
            int maxBucket = buckets.Max();
            for (int i = 0; i < buckets.Length; i++)
            {
                float lo = reader.WorldHeightMin + range * i / buckets.Length;
                float hi = reader.WorldHeightMin + range * (i + 1) / buckets.Length;
                int barLen = maxBucket > 0 ? buckets[i] * 40 / maxBucket : 0;
                Console.WriteLine($"  [{lo,10:F1} .. {hi,10:F1}] {new string('#', barLen)} ({buckets[i]})");
            }
            Console.WriteLine();

            Console.WriteLine("=== Done ===");
            return 0;
        }

        private static void PrintCellMinMax(byte[] data, long baseOffs, BtdTerrainReader reader, int dx, int dy, string label)
        {
            long off = baseOffs + ((long)dy * reader.CellCountX + dx) * 8;
            float cMin = ReadFloat(data, off);
            float cMax = ReadFloat(data, off + 4);
            Console.WriteLine($"  {label} ({dx + reader.CellMinX},{dy + reader.CellMinY}): min={cMin:F2}, max={cMax:F2}");
        }

        private static void PrintBlockStats(byte[] data, string label, long tableOffs, int blockCount, int entriesPerBlock)
        {
            int empty = 0;
            uint totalCompressed = 0;
            uint minSize = uint.MaxValue;
            uint maxSize = 0;
            for (int i = 0; i < blockCount; i++)
            {
                long off = tableOffs + (long)i * entriesPerBlock * 8;
                uint compSize = ReadUInt32(data, off + 4);
                if (compSize == 0) { empty++; continue; }
                totalCompressed += compSize;
                if (compSize < minSize) minSize = compSize;
                if (compSize > maxSize) maxSize = compSize;
            }
            int nonEmpty = blockCount - empty;
            Console.WriteLine($"  {label}: {blockCount} blocks, {nonEmpty} non-empty, {empty} empty");
            if (nonEmpty > 0)
            {
                Console.WriteLine($"    Total compressed: {totalCompressed:N0} bytes ({totalCompressed / 1024.0 / 1024.0:F2} MB)");
                Console.WriteLine($"    Size range: {minSize:N0} .. {maxSize:N0} bytes, avg {totalCompressed / nonEmpty:N0}");
            }
        }

        private static void AnalyzeUint16Region(byte[] data, int byteOffset, int count, string label, BtdTerrainReader reader)
        {
            ushort min = ushort.MaxValue, max = ushort.MinValue;
            long sum = 0;
            int zeroCount = 0;
            int maxValCount = 0;
            ushort[] histogram = new ushort[256]; // coarse histogram

            for (int i = 0; i < count; i++)
            {
                int off = byteOffset + i * 2;
                ushort v = (ushort)(data[off] | (data[off + 1] << 8));
                if (v < min) min = v;
                if (v > max) max = v;
                sum += v;
                if (v == 0) zeroCount++;
                if (v == 65535) maxValCount++;
                histogram[v >> 8]++;
            }

            Console.WriteLine($"  {label}: raw [{min}..{max}], avg {sum / (double)count:F1}, zeros={zeroCount}, maxvals={maxValCount}");
            if (reader != null)
                Console.WriteLine($"    World height: [{reader.RawToHeight(min):F2}..{reader.RawToHeight(max):F2}]");

            // Show coarse histogram (top 5 buckets)
            var top5 = Enumerable.Range(0, 256)
                .OrderByDescending(i => histogram[i])
                .Take(5)
                .Where(i => histogram[i] > 0);
            Console.Write("    Top raw ranges:");
            foreach (int b in top5)
                Console.Write($" [{b * 256}..{b * 256 + 255}]={histogram[b]}");
            Console.WriteLine();
        }

        private static byte[] DecompressZlib(byte[] data, long offset, uint compressedSize, int expectedSize)
        {
            try
            {
                using var ms = new System.IO.MemoryStream(data, (int)offset, (int)compressedSize);
                using var zlib = new System.IO.Compression.ZLibStream(ms, System.IO.Compression.CompressionMode.Decompress);
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
            catch { return null; }
        }

        private static uint ReadUInt32(byte[] data, long offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        private static int ReadInt32(byte[] data, long offset) => (int)ReadUInt32(data, offset);

        private static float ReadFloat(byte[] data, long offset) => BitConverter.ToSingle(data, (int)offset);

        private static int CeilDiv(int a, int b) => (a + b - 1) / b;
    }
}
