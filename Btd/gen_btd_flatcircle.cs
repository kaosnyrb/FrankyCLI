using Retrograde.Utils;
using System;

namespace FrankyCLI
{
    public class gen_btd_flatcircle
    {
        public static int Generate(string[] args)
        {
            string btdPath = @"C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data\terrain\oebb008world.btd";

            if (args.Length >= 6 && !string.IsNullOrWhiteSpace(args[5]))
                btdPath = args[5];

            Console.WriteLine($"=== BTD FlattenCircle Test ===");
            Console.WriteLine($"File: {btdPath}");

            if (!System.IO.File.Exists(btdPath))
            {
                Console.WriteLine($"ERROR: BTD file not found at '{btdPath}'");
                return 1;
            }

            var btd = new BtdFile(btdPath);
            Console.WriteLine($"  Cells: {btd.CellCountX}x{btd.CellCountY} ({btd.CellMinX},{btd.CellMinY}) to ({btd.CellMaxX},{btd.CellMaxY})");
            Console.WriteLine($"  Height range: {btd.WorldHeightMin} to {btd.WorldHeightMax}");

            // Center of the map in world space
            float centerCellX = (btd.CellMinX + btd.CellMaxX) / 2.0f;
            float centerCellY = (btd.CellMinY + btd.CellMaxY) / 2.0f;
            float worldCenterX = centerCellX * 4096f + 2048f;
            float worldCenterY = centerCellY * 4096f + 2048f;

            float blendWidth = 256f;

            // --- FlattenCircle test ---
            float circleRadius = 512f;
            Console.WriteLine($"\n  --- FlattenCircle ---");
            Console.WriteLine($"  Center: ({worldCenterX:F0}, {worldCenterY:F0})");
            Console.WriteLine($"  Radius: {circleRadius} units ({circleRadius / 32f:F0} verts), blend: {blendWidth} units ({blendWidth / 32f:F0} verts)");

            float heightBefore = btd.SampleHeightAtWorld(worldCenterX, worldCenterY);
            Console.WriteLine($"  Height at center before: {heightBefore:F2}");

            float flatHeight = btd.FlattenCircle(worldCenterX, worldCenterY, circleRadius, blendWidth);
            Console.WriteLine($"  Flattened to: {flatHeight:F2}");

            Console.WriteLine($"  Circle samples (X axis):");
            float[] offsets = { 0, 64, 128, 256, 384, 512, 600, 768 };
            foreach (float off in offsets)
            {
                float h = btd.SampleHeightAtWorld(worldCenterX + off, worldCenterY);
                string zone = off <= circleRadius ? "FLAT" : off <= circleRadius + blendWidth ? "BLEND" : "ORIGINAL";
                Console.WriteLine($"    +{off,4:F0} units: {h,8:F2}  ({zone})");
            }

            // --- FlattenSquare test (offset so it doesn't overlap the circle) ---
            float sqCenterX = worldCenterX - 2048f;
            float sqCenterY = worldCenterY;
            float halfW = 384f;
            float halfH = 256f;
            Console.WriteLine($"\n  --- FlattenSquare ---");
            Console.WriteLine($"  Center: ({sqCenterX:F0}, {sqCenterY:F0})");
            Console.WriteLine($"  Half-size: {halfW}x{halfH} units, blend: {blendWidth} units");

            float sqHeightBefore = btd.SampleHeightAtWorld(sqCenterX, sqCenterY);
            Console.WriteLine($"  Height at center before: {sqHeightBefore:F2}");

            float sqFlatHeight = btd.FlattenSquare(sqCenterX, sqCenterY, halfW, halfH, blendWidth);
            Console.WriteLine($"  Flattened to: {sqFlatHeight:F2}");

            Console.WriteLine($"  Square samples (X axis):");
            float[] sqOffsets = { 0, 128, 256, 384, 480, 640 };
            foreach (float off in sqOffsets)
            {
                float h = btd.SampleHeightAtWorld(sqCenterX + off, sqCenterY);
                string zone = off <= halfW ? "FLAT" : off <= halfW + blendWidth ? "BLEND" : "ORIGINAL";
                Console.WriteLine($"    +{off,4:F0} units: {h,8:F2}  ({zone})");
            }
            Console.WriteLine($"  Square samples (Y axis):");
            foreach (float off in new float[] { 0, 64, 128, 256, 350, 512 })
            {
                float h = btd.SampleHeightAtWorld(sqCenterX, sqCenterY + off);
                string zone = off <= halfH ? "FLAT" : off <= halfH + blendWidth ? "BLEND" : "ORIGINAL";
                Console.WriteLine($"    +{off,4:F0} units: {h,8:F2}  ({zone})");
            }

            Console.WriteLine($"  Smoothing cell edges...");
            btd.SmoothDirtyCellEdges(32);

            Console.WriteLine($"  Saving to {btdPath}...");
            btd.Save(btdPath, updateMinMax: false);

            Console.WriteLine("  Done.");
            return 0;
        }
    }
}
