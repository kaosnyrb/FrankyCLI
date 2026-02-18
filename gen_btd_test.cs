using Retrograde.Utils;
using System;
using System.Linq;

namespace FrankyCLI
{
    public class gen_btd_test
    {
        public static int Generate(string[] args)
        {
            string btdPath = @"C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data\terrain\oebb008world.btd";

            if (args.Length >= 6 && !string.IsNullOrWhiteSpace(args[5]))
                btdPath = args[5];

            Console.WriteLine($"=== BTD Terrain Reader Test ===");
            Console.WriteLine($"File: {btdPath}");
            Console.WriteLine();

            if (!System.IO.File.Exists(btdPath))
            {
                Console.WriteLine($"ERROR: BTD file not found at '{btdPath}'");
                return 1;
            }

            int passed = 0;
            int failed = 0;

            // --- Test 1: Load file ---
            BtdFile reader;
            try
            {
                reader = new BtdFile(btdPath);
                Pass(ref passed, "Load BTD file");
            }
            catch (Exception ex)
            {
                Fail(ref failed, "Load BTD file", ex.Message);
                return 1;
            }

            // --- Test 2: Header sanity checks ---
            Console.WriteLine("--- Header Values ---");
            Console.WriteLine($"  IsStarfield:    {reader.IsStarfield}");
            Console.WriteLine($"  CellMinX:       {reader.CellMinX}");
            Console.WriteLine($"  CellMinY:       {reader.CellMinY}");
            Console.WriteLine($"  CellMaxX:       {reader.CellMaxX}");
            Console.WriteLine($"  CellMaxY:       {reader.CellMaxY}");
            Console.WriteLine($"  CellCountX:     {reader.CellCountX}");
            Console.WriteLine($"  CellCountY:     {reader.CellCountY}");
            Console.WriteLine($"  WorldHeightMin: {reader.WorldHeightMin}");
            Console.WriteLine($"  WorldHeightMax: {reader.WorldHeightMax}");
            Console.WriteLine();

            Check(ref passed, ref failed, "IsStarfield is true",
                reader.IsStarfield, "Expected Starfield BTD");

            Check(ref passed, ref failed, "CellCountX > 0",
                reader.CellCountX > 0, $"Got {reader.CellCountX}");

            Check(ref passed, ref failed, "CellCountY > 0",
                reader.CellCountY > 0, $"Got {reader.CellCountY}");

            Check(ref passed, ref failed, "WorldHeightMax > WorldHeightMin",
                reader.WorldHeightMax > reader.WorldHeightMin,
                $"Min={reader.WorldHeightMin}, Max={reader.WorldHeightMax}");

            // --- Test 3: Read a cell height map at LOD0 ---
            int testCellX = reader.CellMinX;
            int testCellY = reader.CellMinY;
            Console.WriteLine($"--- Cell Height Map LOD0 at ({testCellX}, {testCellY}) ---");

            try
            {
                var buf = new ushort[128 * 128];
                reader.GetCellHeightMap(buf, testCellX, testCellY, 0);

                ushort min = buf.Min();
                ushort max = buf.Max();
                double avg = buf.Average(v => (double)v);
                bool hasNonZero = buf.Any(v => v != 0);

                Console.WriteLine($"  Raw min: {min}, max: {max}, avg: {avg:F1}");
                Console.WriteLine($"  Height min: {reader.RawToHeight(min):F2}, max: {reader.RawToHeight(max):F2}");

                Pass(ref passed, "GetCellHeightMap LOD0 succeeded");
                Check(ref passed, ref failed, "Height map has non-zero values",
                    hasNonZero, "All values were zero");
                Check(ref passed, ref failed, "Height map max > min",
                    max > min, $"min={min}, max={max}");
            }
            catch (Exception ex)
            {
                Fail(ref failed, "GetCellHeightMap LOD0", ex.Message);
            }

            // --- Test 4: Read center cell ---
            int centerX = (reader.CellMinX + reader.CellMaxX) / 2;
            int centerY = (reader.CellMinY + reader.CellMaxY) / 2;
            Console.WriteLine($"--- Center Cell ({centerX}, {centerY}) ---");

            try
            {
                var buf = new ushort[128 * 128];
                reader.GetCellHeightMap(buf, centerX, centerY, 0);

                ushort min = buf.Min();
                ushort max = buf.Max();
                Console.WriteLine($"  Raw min: {min}, max: {max}");
                Console.WriteLine($"  Height min: {reader.RawToHeight(min):F2}, max: {reader.RawToHeight(max):F2}");

                Pass(ref passed, "GetCellHeightMap center cell");
            }
            catch (Exception ex)
            {
                Fail(ref failed, "GetCellHeightMap center cell", ex.Message);
            }

            // --- Test 5: LOD levels ---
            for (int lod = 1; lod <= 3; lod++)
            {
                int res = 128 >> lod;
                try
                {
                    var buf = new ushort[res * res];
                    reader.GetCellHeightMap(buf, testCellX, testCellY, lod);

                    bool hasNonZero = buf.Any(v => v != 0);
                    Console.WriteLine($"  LOD{lod} ({res}x{res}): min={buf.Min()}, max={buf.Max()}, nonZero={hasNonZero}");

                    Pass(ref passed, $"GetCellHeightMap LOD{lod}");
                }
                catch (Exception ex)
                {
                    Fail(ref failed, $"GetCellHeightMap LOD{lod}", ex.Message);
                }
            }

            // --- Test 6: GetHeight single vertex ---
            Console.WriteLine($"--- GetHeight single vertex ---");
            try
            {
                float h00 = reader.GetHeight(testCellX, testCellY, 0, 0);
                float h64 = reader.GetHeight(testCellX, testCellY, 64, 64);
                float h127 = reader.GetHeight(testCellX, testCellY, 127, 127);

                Console.WriteLine($"  (0,0)={h00:F2}  (64,64)={h64:F2}  (127,127)={h127:F2}");

                Check(ref passed, ref failed, "GetHeight returns finite values",
                    float.IsFinite(h00) && float.IsFinite(h64) && float.IsFinite(h127),
                    "Non-finite height value");

                Check(ref passed, ref failed, "GetHeight within world bounds",
                    h00 >= reader.WorldHeightMin && h00 <= reader.WorldHeightMax,
                    $"Height {h00} outside [{reader.WorldHeightMin}, {reader.WorldHeightMax}]");

                Pass(ref passed, "GetHeight single vertex");
            }
            catch (Exception ex)
            {
                Fail(ref failed, "GetHeight single vertex", ex.Message);
            }

            // --- Test 7: SampleHeightAtWorld ---
            Console.WriteLine($"--- SampleHeightAtWorld ---");
            try
            {
                float worldX = centerX * 4096f + 2048f;
                float worldY = centerY * 4096f + 2048f;
                float h = reader.SampleHeightAtWorld(worldX, worldY);

                Console.WriteLine($"  World ({worldX:F0}, {worldY:F0}) -> height {h:F2}");

                Check(ref passed, ref failed, "SampleHeightAtWorld returns finite",
                    float.IsFinite(h), $"Got {h}");

                Check(ref passed, ref failed, "SampleHeightAtWorld within world bounds",
                    h >= reader.WorldHeightMin && h <= reader.WorldHeightMax,
                    $"Height {h} outside [{reader.WorldHeightMin}, {reader.WorldHeightMax}]");

                Pass(ref passed, "SampleHeightAtWorld");
            }
            catch (Exception ex)
            {
                Fail(ref failed, "SampleHeightAtWorld", ex.Message);
            }

            // --- Test 8: RawToHeight boundary values ---
            Console.WriteLine($"--- RawToHeight boundary values ---");
            {
                float hMin = reader.RawToHeight(0);
                float hMax = reader.RawToHeight(65535);
                float hMid = reader.RawToHeight(32768);

                Console.WriteLine($"  Raw 0     -> {hMin:F2} (expected ~{reader.WorldHeightMin:F2})");
                Console.WriteLine($"  Raw 65535 -> {hMax:F2} (expected ~{reader.WorldHeightMax:F2})");
                Console.WriteLine($"  Raw 32768 -> {hMid:F2}");

                Check(ref passed, ref failed, "RawToHeight(0) == WorldHeightMin",
                    Math.Abs(hMin - reader.WorldHeightMin) < 0.01f,
                    $"Got {hMin}, expected {reader.WorldHeightMin}");

                Check(ref passed, ref failed, "RawToHeight(65535) ~= WorldHeightMax",
                    Math.Abs(hMax - reader.WorldHeightMax) < 1.0f,
                    $"Got {hMax}, expected {reader.WorldHeightMax}");
            }

            // --- Test 9: Out-of-range cell throws ---
            Console.WriteLine($"--- Out-of-range checks ---");
            try
            {
                var buf = new ushort[128 * 128];
                reader.GetCellHeightMap(buf, reader.CellMaxX + 100, reader.CellMaxY + 100, 0);
                Fail(ref failed, "Out-of-range cell throws", "No exception was thrown");
            }
            catch (ArgumentOutOfRangeException)
            {
                Pass(ref passed, "Out-of-range cell throws ArgumentOutOfRangeException");
            }
            catch (Exception ex)
            {
                Fail(ref failed, "Out-of-range cell throws", $"Wrong exception type: {ex.GetType().Name}");
            }

            // --- Test 10: HeightToRaw round-trip ---
            Console.WriteLine($"--- HeightToRaw round-trip ---");
            {
                ushort raw1 = 0;
                ushort raw2 = 32768;
                ushort raw3 = 65535;
                Check(ref passed, ref failed, "HeightToRaw(RawToHeight(0)) == 0",
                    reader.HeightToRaw(reader.RawToHeight(raw1)) == raw1,
                    $"Got {reader.HeightToRaw(reader.RawToHeight(raw1))}");
                Check(ref passed, ref failed, "HeightToRaw(RawToHeight(32768)) == 32768",
                    reader.HeightToRaw(reader.RawToHeight(raw2)) == raw2,
                    $"Got {reader.HeightToRaw(reader.RawToHeight(raw2))}");
                Check(ref passed, ref failed, "HeightToRaw(RawToHeight(65535)) == 65535",
                    reader.HeightToRaw(reader.RawToHeight(raw3)) == raw3,
                    $"Got {reader.HeightToRaw(reader.RawToHeight(raw3))}");
            }

            // --- Test 11: SetHeight + read back ---
            Console.WriteLine($"--- SetHeight + read back ---");
            try
            {
                ushort origVal = (ushort)(reader.HeightToRaw(reader.GetHeight(testCellX, testCellY, 0, 0)));
                ushort newVal = (ushort)(origVal == 12345 ? 54321 : 12345);

                reader.SetHeight(testCellX, testCellY, 0, 0, newVal);
                float readBack = reader.GetHeight(testCellX, testCellY, 0, 0);
                ushort readBackRaw = reader.HeightToRaw(readBack);

                Check(ref passed, ref failed, "SetHeight value persists in memory",
                    readBackRaw == newVal,
                    $"Expected {newVal}, got {readBackRaw}");

                // Restore original
                reader.SetHeight(testCellX, testCellY, 0, 0, origVal);
                Pass(ref passed, "SetHeight + read back");
            }
            catch (Exception ex)
            {
                Fail(ref failed, "SetHeight + read back", ex.Message);
            }

            // --- Test 12: Save + reload round-trip ---
            Console.WriteLine($"--- Save + reload round-trip ---");
            try
            {
                string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "btd_test_output.btd");

                // Read original cell data
                var origBuf = new ushort[128 * 128];
                reader.GetCellHeightMap(origBuf, testCellX, testCellY, 0);
                ushort origCorner = origBuf[0];

                // Modify a single vertex
                ushort modifiedVal = (ushort)(origCorner == 11111 ? 22222 : 11111);
                reader.SetHeight(testCellX, testCellY, 0, 0, modifiedVal);

                // Also read an unmodified cell for comparison
                var unmodBuf = new ushort[128 * 128];
                reader.GetCellHeightMap(unmodBuf, centerX, centerY, 0);

                // Save
                reader.Save(tempPath);
                Pass(ref passed, "Save to temp file");

                // Reload
                var reader2 = new BtdFile(tempPath);
                Pass(ref passed, "Reload saved file");

                // Check header matches
                Check(ref passed, ref failed, "Saved header: CellCountX matches",
                    reader2.CellCountX == reader.CellCountX,
                    $"Expected {reader.CellCountX}, got {reader2.CellCountX}");
                Check(ref passed, ref failed, "Saved header: WorldHeightMin matches",
                    Math.Abs(reader2.WorldHeightMin - reader.WorldHeightMin) < 0.01f,
                    $"Expected {reader.WorldHeightMin}, got {reader2.WorldHeightMin}");

                // Check modified cell
                float reloadedHeight = reader2.GetHeight(testCellX, testCellY, 0, 0);
                ushort reloadedRaw = reader2.HeightToRaw(reloadedHeight);
                Check(ref passed, ref failed, "Modified vertex persists after save/reload",
                    reloadedRaw == modifiedVal,
                    $"Expected {modifiedVal}, got {reloadedRaw}");

                // Check unmodified cell is intact
                var reloadUnmod = new ushort[128 * 128];
                reader2.GetCellHeightMap(reloadUnmod, centerX, centerY, 0);
                bool unmodMatch = true;
                for (int i = 0; i < unmodBuf.Length; i++)
                {
                    if (unmodBuf[i] != reloadUnmod[i]) { unmodMatch = false; break; }
                }
                Check(ref passed, ref failed, "Unmodified cell unchanged after save/reload",
                    unmodMatch, "Cell data differs");

                // Restore original and clean up
                reader.SetHeight(testCellX, testCellY, 0, 0, origCorner);
                try { System.IO.File.Delete(tempPath); } catch { }

                Console.WriteLine($"  Temp file: {tempPath}");
            }
            catch (Exception ex)
            {
                Fail(ref failed, "Save + reload round-trip", ex.Message);
            }

            // --- Summary ---
            Console.WriteLine();
            Console.WriteLine($"=== Results: {passed} passed, {failed} failed ===");
            return failed > 0 ? 1 : 0;
        }

        private static void Pass(ref int passed, string name)
        {
            passed++;
            Console.WriteLine($"  PASS: {name}");
        }

        private static void Fail(ref int failed, string name, string reason)
        {
            failed++;
            Console.WriteLine($"  FAIL: {name} - {reason}");
        }

        private static void Check(ref int passed, ref int failed, string name, bool condition, string failReason)
        {
            if (condition)
                Pass(ref passed, name);
            else
                Fail(ref failed, name, failReason);
        }
    }
}
