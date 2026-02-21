using System;
using System.Collections.Generic;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Map layout pass for fort-type POIs.
/// Populates WorldspaceState.Map with tile placements:
/// central core, directional petals, gap filler, walls, corners,
/// gates, stairs, wall addons, large blocks, barracks/armory, small scatter.
/// Ported from StarTiller FortCellGen.BuildMap().
/// </summary>
/// <param name="scale">Controls the overall footprint of the fort (0.1 = tiny outpost, 1.0 = full fort).</param>
public class FortLayoutPass(float scale = 1.0f) : IWorldspacePass
{
    private readonly float _scale = Math.Clamp(scale, 0.1f, 1.0f);

    public void RunPass(WorldspaceState state)
    {
        var rand = state.Rng;
        var map = state.Map;
        int mapsize = map.xsize;
        int centerx = 24;
        int centery = 24;

        // Core cluster: half-extent in multiples of 3 (full = 6 → 5x5, half = 3 → 3x3)
        int coreHalf = (int)Math.Round(6.0 * _scale / 3.0) * 3;

        for (int x = centerx - coreHalf; x <= centerx + coreHalf; x += 3)
        {
            for (int y = centery - coreHalf; y <= centery + coreHalf; y += 3)
            {
                map.placesmalltileonempty(x, y, "to_pkn_base", 0, "floor");
            }
        }

        // 25% chance: Landing pad
        if (rand.Next(100) > 75)
        {
            map.placelandingpadtile(centerx, centery, "to_pkn_landing_", 0, "pad");
        }

        // Directional rectangle petals
        int numberofrects = Math.Max(1, (int)Math.Round((3 + rand.Next(4)) * _scale));
        for (int i = 0; i < numberofrects; i++)
        {
            int direction = rand.Next(4);
            int rectx = Math.Max(3, (int)Math.Round((4 + rand.Next(2)) * _scale) * 3);
            int recty = Math.Max(3, (int)Math.Round((4 + rand.Next(2)) * _scale) * 3);

            switch (direction)
            {
                case 0: // TopLeft
                    for (int x = centerx - rectx; x <= centerx; x += 3)
                        for (int y = centery - recty; y <= centery; y += 3)
                            map.placesmalltileonempty(x, y, "to_pkn_base", 0, "floor");
                    break;
                case 1: // TopRight
                    for (int x = centerx; x <= centerx + rectx; x += 3)
                        for (int y = centery - recty; y <= centery; y += 3)
                            map.placesmalltileonempty(x, y, "to_pkn_base", 0, "floor");
                    break;
                case 2: // BottomRight
                    for (int x = centerx; x <= centerx + rectx; x += 3)
                        for (int y = centery; y <= centery + recty; y += 3)
                            map.placesmalltileonempty(x, y, "to_pkn_base", 0, "floor");
                    break;
                case 3: // BottomLeft
                    for (int x = centerx - rectx; x <= centerx; x += 3)
                        for (int y = centery; y <= centery + recty; y += 3)
                            map.placesmalltileonempty(x, y, "to_pkn_base", 0, "floor");
                    break;
            }
        }

        // Fill single-block gaps (avoids odd single-tile corridors)
        for (int x = 1; x < mapsize - 1; x++)
        {
            for (int y = 1; y < mapsize - 1; y++)
            {
                if (map.tiles[x][y].prefabs.Count == 0)
                {
                    if (map.tiles[x - 1][y].prefabs.Contains("floor") && map.tiles[x + 1][y].prefabs.Contains("floor"))
                    {
                        map.placesingletile(x, y, "to_pkn_single", 0);
                    }
                    if (map.tiles[x][y - 1].prefabs.Contains("floor") && map.tiles[x][y + 1].prefabs.Contains("floor"))
                    {
                        map.placesingletile(x, y, "to_pkn_single", 0);
                    }
                }
            }
        }

        // Build walls with corner/edge detection
        int wallcount = 0;
        for (int x = 3; x < mapsize - 3; x++)
        {
            for (int y = 3; y < mapsize - 3; y++)
            {
                if (map.tiles[x][y].prefabs.Contains("to_pkn_base"))
                {
                    bool topclear = map.tiles[x][y - 3].prefabs.Count == 0;
                    bool botclear = map.tiles[x][y + 3].prefabs.Count == 0;
                    bool leftclear = map.tiles[x - 3][y].prefabs.Count == 0;
                    bool rightclear = map.tiles[x + 3][y].prefabs.Count == 0;
                    bool tlclear = map.tiles[x - 3][y - 3].prefabs.Count == 0;
                    bool trclear = map.tiles[x + 3][y - 3].prefabs.Count == 0;
                    bool blclear = map.tiles[x - 3][y + 3].prefabs.Count == 0;
                    bool brclear = map.tiles[x + 3][y + 3].prefabs.Count == 0;

                    bool placed = false;

                    // Outer corners
                    if (topclear && leftclear && !placed) { map.placesmalltile(x, y, "to_pkn_wallcorner_", 0, "ignore"); placed = true; }
                    if (topclear && rightclear && !placed) { map.placesmalltile(x, y, "to_pkn_wallcorner_", 90, "ignore"); placed = true; }
                    if (botclear && rightclear && !placed) { map.placesmalltile(x, y, "to_pkn_wallcorner_", 180, "ignore"); placed = true; }
                    if (botclear && leftclear && !placed) { map.placesmalltile(x, y, "to_pkn_wallcorner_", 270, "ignore"); placed = true; }

                    // Straight walls
                    if (topclear && !placed) { map.placesmalltile(x, y, "to_pkn_wall_", 0, "ignore"); placed = true; }
                    if (botclear && !placed) { map.placesmalltile(x, y, "to_pkn_wall_", 180, "ignore"); placed = true; }
                    if (leftclear && !placed) { map.placesmalltile(x, y, "to_pkn_wall_", 270, "ignore"); placed = true; }
                    if (rightclear && !placed) { map.placesmalltile(x, y, "to_pkn_wall_", 90, "ignore"); placed = true; }

                    // Inner corners
                    if (tlclear && !placed) { map.placesmalltile(x, y, "to_pkn_wallinside_", 0, "ignore"); placed = true; }
                    if (trclear && !placed) { map.placesmalltile(x, y, "to_pkn_wallinside_", 90, "ignore"); placed = true; }
                    if (brclear && !placed) { map.placesmalltile(x, y, "to_pkn_wallinside_", 180, "ignore"); placed = true; }
                    if (blclear && !placed) { map.placesmalltile(x, y, "to_pkn_wallinside_", 270, "ignore"); placed = true; }

                    if (placed) wallcount++;
                }
            }
        }

        // Place gates on walls (2-4, one per side max)
        int gatecount = Math.Max(1, (int)Math.Round((2 + rand.Next(2)) * _scale));
        List<int> usedRotations = new List<int>();
        for (int i = 0; i < 100 && gatecount > 0; i++)
        {
            for (int x = 3; x < mapsize - 3; x++)
            {
                for (int y = 3; y < mapsize - 3; y++)
                {
                    if (map.tiles[x][y].prefabs.Contains("to_pkn_wall_"))
                    {
                        if (rand.Next(100) < 10 && gatecount > 0 && !usedRotations.Contains(map.tiles[x][y].rotation))
                        {
                            usedRotations.Add(map.tiles[x][y].rotation);
                            map.placesmalltile(x, y, "to_pkn_gate_", map.tiles[x][y].rotation, "ignore");
                            gatecount--;
                        }
                    }
                }
            }
        }

        // Place stairs on walls (2-4, one per side max)
        int staircount = Math.Max(1, (int)Math.Round((2 + rand.Next(2)) * _scale));
        usedRotations = new List<int>();
        for (int i = 0; i < 100 && staircount > 0; i++)
        {
            for (int x = 3; x < mapsize - 3; x++)
            {
                for (int y = 3; y < mapsize - 3; y++)
                {
                    if (map.tiles[x][y].prefabs.Contains("to_pkn_wall_"))
                    {
                        if (rand.Next(100) < 10 && staircount > 0 && !usedRotations.Contains(map.tiles[x][y].rotation))
                        {
                            usedRotations.Add(map.tiles[x][y].rotation);
                            map.placesmalltile(x, y, "to_pkn_stairs_", map.tiles[x][y].rotation, "ignore");
                            staircount--;
                        }
                    }
                }
            }
        }

        // Place wall addons (decorations on ~75% of walls)
        int walladdoncount = wallcount - (wallcount / 4);
        for (int i = 0; i < 200 && walladdoncount > 0; i++)
        {
            for (int x = 3; x < mapsize - 3; x++)
            {
                for (int y = 3; y < mapsize - 3; y++)
                {
                    if (map.tiles[x][y].prefabs.Contains("to_pkn_wall_"))
                    {
                        if (rand.Next(100) < 10 && walladdoncount > 0)
                        {
                            map.placesmalladdontile(x, y, "to_pkn_addon_wall_", map.tiles[x][y].rotation, "ignore");
                            walladdoncount--;
                        }
                    }
                }
            }
        }

        // Place large blocks over groups of 9 base tiles
        int largeblockcount = Math.Max(0, (int)Math.Round((2 + rand.Next(5)) * _scale));
        for (int i = 0; i < largeblockcount; i++)
        {
            bool foundblock = false;
            for (int x = 3; x < mapsize - 3; x++)
            {
                for (int y = 3; y < mapsize - 3; y++)
                {
                    if (map.tiles[x][y].prefabs.Contains("to_pkn_base") && !foundblock)
                    {
                        if (map.tiles[x + 3][y].prefabs.Contains("to_pkn_base") &&
                            map.tiles[x + 3][y + 3].prefabs.Contains("to_pkn_base") &&
                            map.tiles[x + 3][y - 3].prefabs.Contains("to_pkn_base") &&
                            map.tiles[x - 3][y].prefabs.Contains("to_pkn_base") &&
                            map.tiles[x - 3][y + 3].prefabs.Contains("to_pkn_base") &&
                            map.tiles[x - 3][y - 3].prefabs.Contains("to_pkn_base") &&
                            map.tiles[x][y + 3].prefabs.Contains("to_pkn_base") &&
                            map.tiles[x][y - 3].prefabs.Contains("to_pkn_base"))
                        {
                            if (rand.Next(100) > 25)
                            {
                                map.placelargetile(x, y, "to_pkn_large", rand.Next(3) * 90, "floor");
                                foundblock = true;
                            }
                        }
                    }
                }
            }
        }

        // Convert one large block to barracks
        PlaceSpecialLargeBlock(map, rand, "to_pkn_large_barracks");

        // Convert one large block to armory
        PlaceSpecialLargeBlock(map, rand, "to_pkn_large_armory");

        // Scatter small detail blocks on base tiles
        int blockcount = Math.Max(50, (int)Math.Round((1000 + rand.Next(10)) * _scale));
        int attempts = 150000;
        for (int i = 0; i < blockcount; i++)
        {
            bool foundblock = false;
            int sx = rand.Next(mapsize);
            int sy = rand.Next(mapsize);
            while (!foundblock && attempts > 0)
            {
                attempts--;
                if (map.tiles[sx][sy].prefabs.Contains("to_pkn_base"))
                {
                    map.placesmalltile(sx, sy, "to_pkn_small_", rand.Next(3) * 90, "floor");
                    foundblock = true;
                }
                else
                {
                    sx = rand.Next(mapsize);
                    sy = rand.Next(mapsize);
                }
            }
        }
    }

    private static void PlaceSpecialLargeBlock(Models.GenerationMap map, Random rand, string type)
    {
        bool foundblock = false;
        for (int i = 0; i < 200 && !foundblock; i++)
        {
            for (int x = 3; x < map.xsize - 3; x++)
            {
                for (int y = 3; y < map.ysize - 3; y++)
                {
                    if (map.tiles[x][y].prefabs.Contains("to_pkn_large") && !foundblock)
                    {
                        if (rand.Next(100) > 25)
                        {
                            map.placelargetile(x, y, type, rand.Next(3) * 90, "floor");
                            foundblock = true;
                        }
                    }
                }
            }
        }
    }
}
