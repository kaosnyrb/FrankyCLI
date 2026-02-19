using Retrograde.Utils;
using System;
using System.Collections.Generic;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Restores original terrain heights in the parts of the flattened area that the
/// fort layout didn't actually use, leaving a one-tile border around occupied tiles.
/// A hermite blend over BlendVerts vertices softens the flat→restored transition.
///
/// Must run after FortLayoutPass (map is populated) and after TerrainFlattenPass
/// (state.FlatAreaBtdData is set).  Must run before Save.
/// </summary>
public class TerrainRestorePass : IWorldspacePass
{
    private const int BlendVerts = 32; // ~25 overlay units of transition

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

        // Precomputed offsets — converts a global vertex index to its tile-grid position:
        //   relX = gx * overlayVertSpacing + xOffset   → tile tx = floor(relX / TileWorldSize)
        //   relY = -gy * overlayVertSpacing + yOffset  → tile ty = floor(relY / TileWorldSize)
        float xOffset = -(flatInfo.BestX0 + flatInfo.AreaVerts * 0.5f) * overlayVertSpacing
                        + state.TileWorldSize * map.xsize * 0.5f;
        float yOffset = (flatInfo.BestY0 + flatInfo.AreaVerts * 0.5f) * overlayVertSpacing
                        + state.TileWorldSize * map.ysize * 0.5f;

        // --- Build per-vertex keepFlat mask over the flat area ---
        int av = flatInfo.AreaVerts;
        bool[,] keepFlatVerts = new bool[av, av];
        for (int localGy = 0; localGy < av; localGy++)
        {
            int ggy = flatInfo.BestY0 + localGy;
            for (int localGx = 0; localGx < av; localGx++)
            {
                int ggx = flatInfo.BestX0 + localGx;
                int tx = (int)Math.Floor((ggx * overlayVertSpacing + xOffset) / state.TileWorldSize);
                int ty = (int)Math.Floor((-ggy * overlayVertSpacing + yOffset) / state.TileWorldSize);
                keepFlatVerts[localGy, localGx] = tx >= 0 && tx < map.xsize
                                                  && ty >= 0 && ty < map.ysize
                                                  && keepFlat[tx, ty];
            }
        }

        // --- Multi-source BFS Chebyshev distance map ---
        // distMap[ly, lx] = minimum Chebyshev distance from vertex (lx, ly) to the nearest
        // keepFlat vertex.  0 = keepFlat, ≥ BlendVerts = fully restored.
        // BFS with 8-connectivity gives exact Chebyshev distances in O(av²).
        var distMap = new int[av, av];
        var queue = new Queue<(int x, int y)>(av * av);
        for (int ly = 0; ly < av; ly++)
            for (int lx = 0; lx < av; lx++)
            {
                if (keepFlatVerts[ly, lx]) { distMap[ly, lx] = 0; queue.Enqueue((lx, ly)); }
                else distMap[ly, lx] = int.MaxValue;
            }

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            int next = distMap[y, x] + 1;
            if (next >= BlendVerts) continue; // no need to propagate further
            for (int dy = -1; dy <= 1; dy++)
            {
                int ny = y + dy;
                if (ny < 0 || ny >= av) continue;
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx;
                    if (nx < 0 || nx >= av) continue;
                    if (next < distMap[ny, nx]) { distMap[ny, nx] = next; queue.Enqueue((nx, ny)); }
                }
            }
        }

        // --- Edge-gap guard bounds ---
        int editMaxX = btd.CellMaxX - 1;
        int editMaxY = btd.CellMaxY - 1;
        int totalW = (editMaxX - flatInfo.EditMinX + 1) * BtdFile.CellResolution;
        int totalH = (editMaxY - flatInfo.EditMinY + 1) * BtdFile.CellResolution;
        int gap = flatInfo.EdgeGapVerts;

        // --- Apply restore / blend ---
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

                    // Hard guard: never touch the edge-gap zone.
                    if (gx < gap || gy < gap || gx >= totalW - gap || gy >= totalH - gap)
                        continue;

                    int idx = vy * BtdFile.CellResolution + vx;
                    int localGx = gx - flatInfo.BestX0;
                    int localGy = gy - flatInfo.BestY0;

                    if (localGx < 0 || localGx >= av || localGy < 0 || localGy >= av)
                    {
                        // Outside the flat area — this cell was touched by TerrainFlattenPass's
                        // outward blend zone.  Restore to original to undo that.
                        buf[idx] = origBuf[idx];
                        modified = true;
                        continue;
                    }

                    int dist = distMap[localGy, localGx];
                    if (dist == 0)
                        continue; // keepFlat — leave as is

                    if (dist >= BlendVerts)
                    {
                        buf[idx] = origBuf[idx];
                    }
                    else
                    {
                        float t = (float)dist / BlendVerts;
                        float s = t * t * (3f - 2f * t); // hermite: 0=flat, 1=original
                        ushort flat = buf[idx];
                        ushort original = origBuf[idx];
                        buf[idx] = (ushort)Math.Clamp(
                            (int)Math.Round(flat + (original - flat) * s), 0, 65535);
                    }

                    modified = true;
                }
            }

            if (modified)
            {
                btd.SetCellHeightMap(buf, cx, cy);
                restoredCells++;
            }
        }

        // Smooth cell-boundary seams once, on the final terrain state.
        // Gap-zone vertices were never written (hard guard above), so the boundary toward
        // the uneditable edge cells is at original height and SmoothDirtyCellEdges won't
        // push a visible gradient into those cells.
        btd.SmoothDirtyCellEdges(16);

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[TerrainRestorePass] restored {restoredCells} cells outside fort footprint + 1-tile border (blend={BlendVerts}v)");
    }
}
