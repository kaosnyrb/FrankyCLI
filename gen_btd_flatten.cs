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

            var reader = new BtdFile(btdPath);
            Console.WriteLine($"  Cells: {reader.CellCountX}x{reader.CellCountY} ({reader.CellMinX},{reader.CellMinY}) to ({reader.CellMaxX},{reader.CellMaxY})");
            Console.WriteLine($"  Height range: {reader.WorldHeightMin} to {reader.WorldHeightMax}");

            float centerCellX = (reader.CellMinX + reader.CellMaxX) / 2.0f;
            float centerCellY = (reader.CellMinY + reader.CellMaxY) / 2.0f;

            // Hill parameters — 100% bigger radius (1.5 cells)
            float hillPeakHeight = 30f;
            float hillRadiusCells = 1.5f;
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

            // Texture values: rocky top, sandy base
            ushort texRocky = 0x0E00;  // "patchy sandy/rocky"
            ushort texSandy = 0x4000;  // "clean bright sand"
            Console.WriteLine($"  Textures: rocky=0x{texRocky:X4}, sandy=0x{texSandy:X4}");

            // Normalize ALL cells' land texture maps
            var canonicalPalette = new byte[32];
            reader.GetCellLandTexMap(canonicalPalette, (int)centerCellX, (int)centerCellY);
            for (int q = 1; q < 4; q++)
                Array.Copy(canonicalPalette, 0, canonicalPalette, q * 8, 8);
            for (int cy = reader.CellMinY; cy <= reader.CellMaxY; cy++)
                for (int cx = reader.CellMinX; cx <= reader.CellMaxX; cx++)
                    reader.SetCellLandTexMap(canonicalPalette, cx, cy);

            for (int cy = safeMinY; cy <= safeMaxY; cy++)
            {
                for (int cx = safeMinX; cx <= safeMaxX; cx++)
                {
                    // Check if this cell could overlap the hill at all
                    float cellWorldMinX = cx * 4096f;
                    float cellWorldMaxX = cellWorldMinX + 127 * 32f;
                    float cellWorldMinY = cy * 4096f;
                    float cellWorldMaxY = cellWorldMinY + 127 * 32f;
                    float nearX = Math.Clamp(worldCenterX, cellWorldMinX, cellWorldMaxX);
                    float nearY = Math.Clamp(worldCenterY, cellWorldMinY, cellWorldMaxY);
                    float nearDist = MathF.Sqrt((nearX - worldCenterX) * (nearX - worldCenterX) + (nearY - worldCenterY) * (nearY - worldCenterY));
                    if (nearDist > hillRadiusWorld)
                        continue;

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
                                float t = dist / hillRadiusWorld;
                                float hillHeight = hillPeakHeight * 0.5f * (1f + MathF.Cos(t * MathF.PI));
                                float existing = reader.RawToHeight(buf[vy * 128 + vx]);
                                buf[vy * 128 + vx] = reader.HeightToRaw(existing + hillHeight);

                                // Rocky on top (inner 40%), sandy at base (outer 60%), blend in between
                                if (t < 0.3f)
                                    texBuf[vy * 128 + vx] = texRocky;
                                else if (t > 0.6f)
                                    texBuf[vy * 128 + vx] = texSandy;
                                else
                                {
                                    // Blend zone: interpolate between rocky and sandy values
                                    float blend = (t - 0.3f) / 0.3f; // 0 at t=0.3, 1 at t=0.6
                                    texBuf[vy * 128 + vx] = blend < 0.5f ? texRocky : texSandy;
                                }
                                texelsModified++;

                                modified = true;
                                vertsModified++;
                            }
                        }
                    }

                    if (modified)
                    {
                        reader.SetCellHeightMap(buf, cx, cy);
                        reader.SetCellTextureData(texBuf, cx, cy);
                        reader.SetCellLod4LandTex(new byte[128], cx, cy);
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
