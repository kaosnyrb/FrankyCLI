using Retrograde.Utils;
using System;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Restores original terrain heights in the parts of the flattened area that the
/// fort layout didn't actually use, leaving a one-tile border around occupied tiles.
///
/// Must run after FortLayoutPass (map is populated) and after TerrainFlattenPass
/// (state.FlatAreaBtdData is set).  Must run before Save.
/// </summary>
public class TerrainRestorePass : IWorldspacePass
{
    public void RunPass(WorldspaceState state)
    {
        var btd = state.BtdFile;
        var flatInfo = state.FlatAreaBtdData;
        if (btd == null || flatInfo == null) return;

        var map = state.Map;

        // Build keepFlat mask: every occupied tile plus a 1-tile Chebyshev border.
        bool[,] keepFlat = new bool[map.xsize, map.ysize];
        for (int tx = 0; tx < map.xsize; tx++)
            for (int ty = 0; ty < map.ysize; ty++)
                if (map.tiles[tx][ty].prefabs.Count > 0)
                    for (int bx = Math.Max(0, tx - 1); bx <= Math.Min(map.xsize - 1, tx + 1); bx++)
                        for (int by = Math.Max(0, ty - 1); by <= Math.Min(map.ysize - 1, ty + 1); by++)
                            keepFlat[bx, by] = true;

        const float overlayCellSize = 100f;
        float overlayVertSpacing = overlayCellSize / BtdFile.CellResolution;

        // Precomputed offset converts a global vertex index to its position within the
        // tile grid (in overlay units):
        //   relX = gx * overlayVertSpacing + xOffset   → tile tx = floor(relX / TileWorldSize)
        //   relY = -gy * overlayVertSpacing + yOffset  → tile ty = floor(relY / TileWorldSize)
        //
        // Derivation: the tile grid is centred on FlatAreaWorldX/Y, and the flat area is
        // centred on vertex (bestX0 + areaVerts/2, bestY0 + areaVerts/2).
        float xOffset = -(flatInfo.BestX0 + flatInfo.AreaVerts * 0.5f) * overlayVertSpacing
                        + state.TileWorldSize * map.xsize * 0.5f;
        float yOffset = (flatInfo.BestY0 + flatInfo.AreaVerts * 0.5f) * overlayVertSpacing
                        + state.TileWorldSize * map.ysize * 0.5f;

        var buf = new ushort[BtdFile.CellResolution * BtdFile.CellResolution];
        int restoredCells = 0;

        foreach (var ((cx, cy), origBuf) in flatInfo.OriginalHeights)
        {
            btd.GetCellHeightMap(buf, cx, cy);
            bool modified = false;

            for (int vy = 0; vy < BtdFile.CellResolution; vy++)
            {
                int gy = (cy - flatInfo.EditMinY) * BtdFile.CellResolution + vy;
                for (int vx = 0; vx < BtdFile.CellResolution; vx++)
                {
                    int gx = (cx - flatInfo.EditMinX) * BtdFile.CellResolution + vx;

                    // Only process vertices inside the flat area proper (not blend-zone cells).
                    if (gx < flatInfo.BestX0 || gx >= flatInfo.BestX0 + flatInfo.AreaVerts ||
                        gy < flatInfo.BestY0 || gy >= flatInfo.BestY0 + flatInfo.AreaVerts)
                        continue;

                    // Map vertex to tile grid position.
                    int tx = (int)Math.Floor((gx * overlayVertSpacing + xOffset) / state.TileWorldSize);
                    int ty = (int)Math.Floor((-gy * overlayVertSpacing + yOffset) / state.TileWorldSize);

                    bool inKeep = tx >= 0 && tx < map.xsize && ty >= 0 && ty < map.ysize
                                  && keepFlat[tx, ty];
                    if (!inKeep)
                    {
                        buf[vy * BtdFile.CellResolution + vx] = origBuf[vy * BtdFile.CellResolution + vx];
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                btd.SetCellHeightMap(buf, cx, cy);
                restoredCells++;
            }
        }

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[TerrainRestorePass] restored {restoredCells} cells outside fort footprint + 1-tile border");
    }
}
