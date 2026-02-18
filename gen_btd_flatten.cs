using Retrograde.Utils;
using System;
using System.Linq;

namespace FrankyCLI
{
    public class gen_btd_flatten
    {
        public static int Generate(string[] args)
        {
            string btdPath = @"C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data\terrain\oebb008world.btd";

            if (args.Length >= 6 && !string.IsNullOrWhiteSpace(args[5]))
                btdPath = args[5];

            Console.WriteLine($"=== BTD Flatten Tool ===");
            Console.WriteLine($"File: {btdPath}");

            if (!System.IO.File.Exists(btdPath))
            {
                Console.WriteLine($"ERROR: BTD file not found at '{btdPath}'");
                return 1;
            }

            var reader = new BtdTerrainReader(btdPath);
            Console.WriteLine($"  Cells: {reader.CellCountX}x{reader.CellCountY} ({reader.CellMinX},{reader.CellMinY}) to ({reader.CellMaxX},{reader.CellMaxY})");
            Console.WriteLine($"  Height range: {reader.WorldHeightMin} to {reader.WorldHeightMax}");

            float centerCellX = (reader.CellMinX + reader.CellMaxX) / 2.0f;
            float centerCellY = (reader.CellMinY + reader.CellMaxY) / 2.0f;

            // Hill parameters — keep proportional to actual terrain heights (~15-84 world units)
            float hillPeakHeight = 30f; // world units tall at the peak
            float hillRadiusCells = 0.75f; // 3/4 of a cell radius
            Console.WriteLine($"  Hill: peak {hillPeakHeight} units, radius {hillRadiusCells:F1} cells, center ({centerCellX}, {centerCellY})");

            int cellsModified = 0;
            int vertsModified = 0;
            int texelsModified = 0;

            // World-space center of the map
            float worldCenterX = centerCellX * 4096f + 2048f;
            float worldCenterY = centerCellY * 4096f + 2048f;
            float hillRadiusWorld = hillRadiusCells * 4096f;

            // Figure out which cells the hill touches (skip edge cells)
            int safeMinX = reader.CellMinX + 1;
            int safeMaxX = reader.CellMaxX - 1;
            int safeMinY = reader.CellMinY + 1;
            int safeMaxY = reader.CellMaxY - 1;

            // Read center cell's texture data to analyze the encoding
            int centerCX = (int)centerCellX;
            int centerCY = (int)centerCellY;
            var sampleTex = new ushort[128 * 128];
            reader.GetCellTextureData(sampleTex, centerCX, centerCY);

            // Find the unique texture values and their frequencies
            var texFreq = new Dictionary<ushort, int>();
            foreach (ushort v in sampleTex)
            {
                texFreq.TryGetValue(v, out int cnt);
                texFreq[v] = cnt + 1;
            }
            var sorted = texFreq.OrderByDescending(kv => kv.Value).ToList();
            Console.WriteLine($"  Texture analysis (center cell): {sorted.Count} unique values");
            for (int i = 0; i < Math.Min(8, sorted.Count); i++)
                Console.WriteLine($"    value={sorted[i].Key} (0x{sorted[i].Key:X4}) count={sorted[i].Value}");

            // Pick a texture value for the hill:
            // Use the least common of the top 3 values — should be visually distinct
            ushort hillTexture;
            if (sorted.Count >= 3)
                hillTexture = sorted[2].Key; // 3rd most common
            else if (sorted.Count >= 2)
                hillTexture = sorted[1].Key;
            else
                hillTexture = sorted[0].Key;
            Console.WriteLine($"  Hill texture value: {hillTexture} (0x{hillTexture:X4})");

            for (int cy = safeMinY; cy <= safeMaxY; cy++)
            {
                for (int cx = safeMinX; cx <= safeMaxX; cx++)
                {
                    // Check if this cell could overlap the hill at all
                    float cellWorldMinX = cx * 4096f;
                    float cellWorldMaxX = cellWorldMinX + 127 * 32f;
                    float cellWorldMinY = cy * 4096f;
                    float cellWorldMaxY = cellWorldMinY + 127 * 32f;

                    // Closest point on cell AABB to hill center
                    float nearX = Math.Clamp(worldCenterX, cellWorldMinX, cellWorldMaxX);
                    float nearY = Math.Clamp(worldCenterY, cellWorldMinY, cellWorldMaxY);
                    float nearDist = MathF.Sqrt((nearX - worldCenterX) * (nearX - worldCenterX) + (nearY - worldCenterY) * (nearY - worldCenterY));
                    if (nearDist > hillRadiusWorld)
                        continue; // cell is entirely outside the hill

                    // Read existing height and texture data
                    var buf = new ushort[128 * 128];
                    reader.GetCellHeightMap(buf, cx, cy, 0);
                    var texBuf = new ushort[128 * 128];
                    reader.GetCellTextureData(texBuf, cx, cy);

                    bool modified = false;
                    for (int vy = 0; vy < 128; vy++)
                    {
                        float wy = cy * 4096f + vy * 32f;
                        for (int vx = 0; vx < 128; vx++)
                        {
                            float wx = cx * 4096f + vx * 32f;

                            float distX = wx - worldCenterX;
                            float distY = wy - worldCenterY;
                            float dist = MathF.Sqrt(distX * distX + distY * distY);

                            if (dist <= hillRadiusWorld)
                            {
                                // Smooth cosine hill added on top of existing terrain
                                float t = dist / hillRadiusWorld;
                                float hillHeight = hillPeakHeight * 0.5f * (1f + MathF.Cos(t * MathF.PI));
                                float existing = reader.RawToHeight(buf[vy * 128 + vx]);
                                buf[vy * 128 + vx] = reader.HeightToRaw(existing + hillHeight);

                                // Paint texture on the inner 70% of the hill (leave edge for blending)
                                if (t < 0.7f)
                                {
                                    texBuf[vy * 128 + vx] = hillTexture;
                                    texelsModified++;
                                }

                                modified = true;
                                vertsModified++;
                            }
                        }
                    }

                    if (modified)
                    {
                        reader.SetCellHeightMap(buf, cx, cy);
                        reader.SetCellTextureData(texBuf, cx, cy);
                        cellsModified++;
                    }
                }
            }

            Console.WriteLine($"  Modified {vertsModified} verts, {texelsModified} texels in {cellsModified} cells");

            Console.WriteLine($"  Smoothing cell edges...");
            reader.SmoothDirtyCellEdges(32);

            Console.WriteLine($"  Saving to {btdPath}...");

            reader.Save(btdPath, updateMinMax: false);

            Console.WriteLine("  Done.");
            return 0;
        }
    }
}
