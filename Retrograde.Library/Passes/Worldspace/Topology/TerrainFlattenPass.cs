using Retrograde.Utils;
using System;
using System.Collections.Generic;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Scans the editable BTD cells to find the flattest rectangular area large enough
/// to fit the base footprint, then raises and flattens it to a height slightly above
/// the average surrounding terrain with a smooth hermite blend at the boundary.
///
/// Updates state.TerrainHeight to the resulting flat elevation (PlacedObject coordinate
/// space, i.e. already divided by the 8× Starfield scale factor).
///
/// Must be added to MapPasses so it runs before tile placement.
/// Requires state.BtdFile to be set (done automatically when a dataFolderPath is
/// provided to WorldspaceNoun).
/// </summary>
public class TerrainFlattenPass : IWorldspacePass
{
    private readonly float _coveragePercent;

    /// <param name="coveragePercent">
    /// Fraction of a cell edge (0–1) that the flat area should cover.
    /// E.g. 0.5 = 64×64 vertices = 2048×2048 world units per cell.
    /// Clamped to [0.05, 1.0].
    /// </param>
    public TerrainFlattenPass(float coveragePercent)
    {
        _coveragePercent = Math.Clamp(coveragePercent, 0.05f, 1.0f);
    }

    public void RunPass(WorldspaceState state)
    {
        var btd = state.BtdFile;
        if (btd == null) return;

        int areaVerts = Math.Max(4, (int)(BtdFile.CellResolution * _coveragePercent));

        int editMinX = btd.CellMinX + 1;
        int editMinY = btd.CellMinY + 1;
        int editMaxX = btd.CellMaxX - 1;
        int editMaxY = btd.CellMaxY - 1;

        if (editMinX > editMaxX || editMinY > editMaxY) return;

        int totalW = (editMaxX - editMinX + 1) * BtdFile.CellResolution;
        int totalH = (editMaxY - editMinY + 1) * BtdFile.CellResolution;

        areaVerts = Math.Min(areaVerts, Math.Min(totalW, totalH));

        // --- Find flattest placement ---
        // Scan candidate top-left corners with a coarse step; sample sparsely inside each.
        int scanStep = Math.Max(1, areaVerts / 8);
        int sampleStep = Math.Max(1, areaVerts / 16);

        // Bias: prefer the candidate closest to the editable-area centre on ties.
        // This ensures that for a flat template BTD (all heights equal) we pick
        // the centre rather than the first (corner) candidate.
        float candidateCenterX = (totalW - areaVerts) / 2f;
        float candidateCenterY = (totalH - areaVerts) / 2f;
        int bestX0 = (int)candidateCenterX, bestY0 = (int)candidateCenterY;
        float bestRange = float.MaxValue;
        float bestDistSq = float.MaxValue;

        for (int y0 = 0; y0 + areaVerts <= totalH; y0 += scanStep)
        {
            for (int x0 = 0; x0 + areaVerts <= totalW; x0 += scanStep)
            {
                float hMin = float.MaxValue, hMax = float.MinValue;

                for (int dy = 0; dy < areaVerts; dy += sampleStep)
                {
                    for (int dx = 0; dx < areaVerts; dx += sampleStep)
                    {
                        float h = GlobalHeight(btd, editMinX, editMinY, x0 + dx, y0 + dy);
                        if (h < hMin) hMin = h;
                        if (h > hMax) hMax = h;
                    }
                }

                float range = hMax - hMin;
                float ddx = x0 - candidateCenterX, ddy = y0 - candidateCenterY;
                float distSq = ddx * ddx + ddy * ddy;

                if (range < bestRange || (range == bestRange && distSq < bestDistSq))
                {
                    bestRange = range;
                    bestDistSq = distSq;
                    bestX0 = x0;
                    bestY0 = y0;
                }
            }
        }

        // --- Sample a ring around the chosen area for the surrounding average ---
        int ringStep = Math.Max(1, areaVerts / 16);
        int ringWidth = Math.Max(8, areaVerts / 8);
        float ringSum = 0f;
        int ringCount = 0;

        for (int dx = 0; dx < areaVerts; dx += ringStep)
        {
            for (int r = 1; r <= ringWidth; r += ringStep)
            {
                TrySampleRing(btd, editMinX, editMinY, totalW, totalH, bestX0 + dx, bestY0 - r, ref ringSum, ref ringCount);
                TrySampleRing(btd, editMinX, editMinY, totalW, totalH, bestX0 + dx, bestY0 + areaVerts + r - 1, ref ringSum, ref ringCount);
            }
        }

        for (int dy = 0; dy < areaVerts; dy += ringStep)
        {
            for (int r = 1; r <= ringWidth; r += ringStep)
            {
                TrySampleRing(btd, editMinX, editMinY, totalW, totalH, bestX0 - r, bestY0 + dy, ref ringSum, ref ringCount);
                TrySampleRing(btd, editMinX, editMinY, totalW, totalH, bestX0 + areaVerts + r - 1, bestY0 + dy, ref ringSum, ref ringCount);
            }
        }

        float surroundAvg = ringCount > 0 ? ringSum / ringCount : btd.SampleHeightAtWorld(0, 0);

        // Slightly above surrounding terrain (1% of total height range)
        float lift = (btd.WorldHeightMax - btd.WorldHeightMin) * 0.01f;
        float targetHeight = surroundAvg + lift;
        ushort targetRaw = btd.HeightToRaw(targetHeight);

        // --- Apply flat height with hermite blend to all touched cells ---
        int blendVerts = 16; // ~512 world units of transition

        int cellX0 = Math.Max(editMinX, editMinX + bestX0 / BtdFile.CellResolution - 1);
        int cellX1 = Math.Min(editMaxX, editMinX + (bestX0 + areaVerts - 1) / BtdFile.CellResolution + 1);
        int cellY0 = Math.Max(editMinY, editMinY + bestY0 / BtdFile.CellResolution - 1);
        int cellY1 = Math.Min(editMaxY, editMinY + (bestY0 + areaVerts - 1) / BtdFile.CellResolution + 1);

        var buf = new ushort[BtdFile.CellResolution * BtdFile.CellResolution];

        for (int cy = cellY0; cy <= cellY1; cy++)
        {
            for (int cx = cellX0; cx <= cellX1; cx++)
            {
                btd.GetCellHeightMap(buf, cx, cy);
                bool modified = false;

                for (int vy = 0; vy < BtdFile.CellResolution; vy++)
                {
                    int gy = (cy - editMinY) * BtdFile.CellResolution + vy;
                    for (int vx = 0; vx < BtdFile.CellResolution; vx++)
                    {
                        int gx = (cx - editMinX) * BtdFile.CellResolution + vx;

                        // Chebyshev distance from global vertex to nearest edge of flat area
                        int distX = Math.Max(0, Math.Max(bestX0 - gx, gx - (bestX0 + areaVerts - 1)));
                        int distY = Math.Max(0, Math.Max(bestY0 - gy, gy - (bestY0 + areaVerts - 1)));
                        int dist = Math.Max(distX, distY);

                        if (dist == 0)
                        {
                            buf[vy * BtdFile.CellResolution + vx] = targetRaw;
                            modified = true;
                        }
                        else if (dist < blendVerts)
                        {
                            float t = (float)dist / blendVerts;
                            float s = t * t * (3f - 2f * t); // hermite: 0 at dist=0, 1 at dist=blendVerts
                            ushort original = buf[vy * BtdFile.CellResolution + vx];
                            buf[vy * BtdFile.CellResolution + vx] = BlendRaw(targetRaw, original, s);
                            modified = true;
                        }
                    }
                }

                if (modified)
                    btd.SetCellHeightMap(buf, cx, cy);
            }
        }

        btd.SmoothDirtyCellEdges(16);

        // btd heights are 8×-scaled; PlacedObject coordinates need /8
        state.TerrainHeight = targetHeight / 8f;

        // Store flat area world-space centre so TileInstantiationPass can
        // centre the tile grid on it. vertSpacing = cellSize / CellResolution = 32.
        const float vertSpacing = 32f;
        const float cellSize = 4096f;
        state.FlatAreaWorldX = editMinX * cellSize + (bestX0 + areaVerts * 0.5f) * vertSpacing;
        state.FlatAreaWorldY = editMinY * cellSize + (bestY0 + areaVerts * 0.5f) * vertSpacing;
    }

    // Returns height (8×-scaled) at global vertex (gx, gy) within the editable region.
    private static float GlobalHeight(BtdFile btd, int editMinX, int editMinY, int gx, int gy)
        => btd.GetHeight(editMinX + gx / BtdFile.CellResolution, editMinY + gy / BtdFile.CellResolution,
                         gx % BtdFile.CellResolution, gy % BtdFile.CellResolution);

    private static void TrySampleRing(BtdFile btd, int editMinX, int editMinY,
        int totalW, int totalH, int gx, int gy, ref float sum, ref int count)
    {
        if (gx < 0 || gy < 0 || gx >= totalW || gy >= totalH) return;
        sum += GlobalHeight(btd, editMinX, editMinY, gx, gy);
        count++;
    }

    // Blend from target (s=0) toward original (s=1) with pre-computed hermite factor.
    private static ushort BlendRaw(ushort target, ushort original, float s)
        => (ushort)Math.Clamp((int)Math.Round(target + (original - target) * s), 0, 65535);
}
