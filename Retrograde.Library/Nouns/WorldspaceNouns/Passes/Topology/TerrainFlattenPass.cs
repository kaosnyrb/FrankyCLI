using Retrograde.Utils;
using System;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Randomly picks a flat area sized to match the fort footprint, then raises and
/// flattens it to a height slightly above the average surrounding terrain with a
/// smooth hermite blend at the boundary.
///
/// The flat area size is derived from state.Map.xsize * state.TileWorldSize (overlay
/// units) converted to BTD vertices.  A 10 overlay-unit inset is kept clear around
/// the edges of the editable region.
///
/// Updates state.TerrainHeight (PlacedObject coordinate space, already / 8) and
/// state.FlatAreaCenter to the centre of the chosen area.
///
/// Must run before tile placement. Requires state.BtdFile and state.Rng.
/// </summary>
public class TerrainFlattenPass : IWorldspacePass
{
    public void RunPass(WorldspaceState state)
    {
        var btd = state.BtdFile;
        if (btd == null) return;

        // Flat area sized to the fort footprint.
        // fort overlay size = map tiles * units-per-tile  (e.g. 50 * 4 = 200 overlay units)
        // overlay vertex spacing = 100 / CellResolution  ≈ 0.78125 units/vert
        const float overlayCellSize = 100f;
        float overlayVertSpacing = overlayCellSize / BtdFile.CellResolution;
        float fortOverlaySize = state.Map.xsize * state.TileWorldSize;
        int areaVerts = (int)Math.Ceiling(fortOverlaySize / overlayVertSpacing);

        int editMinX = btd.CellMinX + 1;
        int editMinY = btd.CellMinY + 1;
        int editMaxX = btd.CellMaxX - 1;
        int editMaxY = btd.CellMaxY - 1;

        if (editMinX > editMaxX || editMinY > editMaxY) return;

        int totalW = (editMaxX - editMinX + 1) * BtdFile.CellResolution;
        int totalH = (editMaxY - editMinY + 1) * BtdFile.CellResolution;

        // 10 overlay-unit inset from every edge of the editable region, in vertices.
        int edgeGapVerts = (int)Math.Ceiling(10f / overlayVertSpacing); // ≈ 13

        int scanMinX = edgeGapVerts;
        int scanMaxX = totalW - edgeGapVerts;
        int scanMinY = edgeGapVerts;
        int scanMaxY = totalH - edgeGapVerts;

        if (scanMinX >= scanMaxX || scanMinY >= scanMaxY) return;

        areaVerts = Math.Min(areaVerts, Math.Min(scanMaxX - scanMinX, scanMaxY - scanMinY));

        // Random placement: pick a top-left corner uniformly inside the constrained region.
        int rangeX = scanMaxX - scanMinX - areaVerts;
        int rangeY = scanMaxY - scanMinY - areaVerts;
        int bestX0 = scanMinX + (rangeX > 0 ? state.Rng.Next(rangeX + 1) : 0);
        int bestY0 = scanMinY + (rangeY > 0 ? state.Rng.Next(rangeY + 1) : 0);

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

        // Slightly below surrounding terrain (0.5% of total height range)
        float lift = (btd.WorldHeightMax - btd.WorldHeightMin) * 0.005f;
        float targetHeight = surroundAvg - lift;
        ushort targetRaw = btd.HeightToRaw(targetHeight);

        // --- Apply flat height with hermite blend to all touched cells ---
        int blendVerts = 16; // ~12.5 overlay units of transition

        int cellX0 = Math.Max(editMinX, editMinX + bestX0 / BtdFile.CellResolution - 1);
        int cellX1 = Math.Min(editMaxX, editMinX + (bestX0 + areaVerts - 1) / BtdFile.CellResolution + 1);
        int cellY0 = Math.Max(editMinY, editMinY + bestY0 / BtdFile.CellResolution - 1);
        int cellY1 = Math.Min(editMaxY, editMinY + (bestY0 + areaVerts - 1) / BtdFile.CellResolution + 1);

        // Capture originals before any modification so TerrainRestorePass can recover them.
        var btdInfo = new WorldspaceState.FlatAreaBtdInfo
        {
            EditMinX = editMinX, EditMinY = editMinY,
            BestX0 = bestX0, BestY0 = bestY0, AreaVerts = areaVerts,
            EdgeGapVerts = edgeGapVerts
        };
        for (int cy2 = cellY0; cy2 <= cellY1; cy2++)
            for (int cx2 = cellX0; cx2 <= cellX1; cx2++)
            {
                var snapshot = new ushort[BtdFile.CellResolution * BtdFile.CellResolution];
                btd.GetCellHeightMap(snapshot, cx2, cy2);
                btdInfo.OriginalHeights[(cx2, cy2)] = snapshot;
            }
        state.FlatAreaBtdData = btdInfo;

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

                        // Never write gap-zone vertices — keeps the cell boundary adjacent to
                        // the uneditable edge cells at original height so SmoothDirtyCellEdges
                        // (called in TerrainRestorePass) doesn't contaminate those edge cells.
                        if (gx < edgeGapVerts || gy < edgeGapVerts ||
                            gx >= totalW - edgeGapVerts || gy >= totalH - edgeGapVerts)
                            continue;

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

        // SmoothDirtyCellEdges is intentionally NOT called here.
        // TerrainRestorePass runs after FortLayoutPass and calls it once on the final
        // terrain state, so cell-boundary blending reflects the post-restoration heights.

        // btd heights are 8×-scaled; PlacedObject coordinates need /8
        state.TerrainHeight = targetHeight / 8f;

        // Convert flat area centre from BTD-internal vertex space to overlay worldspace coordinates.
        const float overlayVertSpacingConst = overlayCellSize / BtdFile.CellResolution; // ≈ 0.78125
        float flatX = editMinX * overlayCellSize + (bestX0 + areaVerts * 0.5f) * overlayVertSpacingConst
                      - btd.WorldCenterX * (overlayCellSize / 4096f);
        float flatY = editMinY * overlayCellSize + (bestY0 + areaVerts * 0.5f) * overlayVertSpacingConst
                      - btd.WorldCenterY * (overlayCellSize / 4096f);
        state.FlatAreaCenter = (flatX, flatY);

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[TerrainFlattenPass] origin=({bestX0},{bestY0}) areaVerts={areaVerts}  FlatArea=({flatX:F2},{flatY:F2})");
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
