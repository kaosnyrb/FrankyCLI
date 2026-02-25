using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Utils;
using System;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Terrain shaping pass for racetrack worldspaces.
///
/// Generates <see cref="CandidateCount"/> candidate track layouts as closed
/// Catmull-Rom spline circuits through <see cref="WaypointCount"/> sector-anchored
/// random waypoints, then scores each candidate on three criteria:
///
///   Terrain-following (60 % weight)
///     Low mean cross-slope under the track band.  Sampled perpendicular to the
///     spline tangent at 200 evenly-spaced points.  Prefers routes where the
///     terrain flows along the track rather than cutting across the grade.
///
///   Waypoint spread (40 % weight)
///     Minimum angular gap between the 10 waypoints around the editable-area
///     centre.  Rewards even distribution; penalises bunched waypoints.
///
///   Self-intersection multiplier
///     Penalises layouts where non-adjacent spline segments come within
///     1.5× the track half-width.  Applied as exp(-closePairs × 0.1) so a
///     clean layout scores 1.0 and a self-crossing layout approaches 0.
///
/// The winning layout is applied to the BTD:
///   - Box-blur (4 passes, radius 3) smooths the track band while the natural
///     elevation gradient is preserved outside it.
///   - All cells' 32-byte land-texture palettes are normalised to prevent
///     quadrant-split artefacts.
///   - The core of the track band (blend ≥ 0.15) is painted with TrackTexture.
/// </summary>
public class RacetrackTerrainPass(float trackScale = 1.0f) : IWorldspacePass
{
    private readonly float _scale = Math.Clamp(trackScale, 0.4f, 1.0f);

    private const int    WaypointCount  = 10;
    private const int    CandidateCount = 100;
    private const float  TrackHalfWidth = 16f;       // overlay units
    private const int    SplineSamples  = 600;       // resolution for terrain editing
    private const int    ScoreSamples   = 200;       // resolution for candidate scoring
    private const ushort TrackTexture   = 0x4000;

    /// <summary>Overlay units per BTD vertex (100 / 128 ≈ 0.78125).</summary>
    private const float OvVtx    = 100f / BtdFile.CellResolution;
    /// <summary>BTD-internal units per overlay unit (4096 / 100 = 40.96).</summary>
    private const float BtdScale = 4096f / 100f;

    // ------------------------------------------------------------------
    // RunPass
    // ------------------------------------------------------------------
    public void RunPass(WorldspaceState state)
    {
        var btd = state.BtdFile;
        if (btd == null) return;

        int editMinX = btd.CellMinX + 1;
        int editMinY = btd.CellMinY + 1;
        int editMaxX = btd.CellMaxX - 1;
        int editMaxY = btd.CellMaxY - 1;
        if (editMinX > editMaxX || editMinY > editMaxY) return;

        int totalW = (editMaxX - editMinX + 1) * BtdFile.CellResolution;
        int totalH = (editMaxY - editMinY + 1) * BtdFile.CellResolution;

        // Editable area in overlay units.
        float ovXMin = editMinX * 100f;
        float ovXMax = (editMaxX + 1) * 100f;
        float ovYMin = editMinY * 100f;
        float ovYMax = (editMaxY + 1) * 100f;

        // Centre of the editable area.  For Starfield 4×4 BTDs this is (0, 0);
        // for 5×5 BTDs it is (50, 50).
        float ovCX = (ovXMin + ovXMax) * 0.5f;
        float ovCY = (ovYMin + ovYMax) * 0.5f;

        // Half-extents: how far the editable area reaches in each axis from centre.
        // The editable region is always symmetric around its centre for Starfield BTDs.
        float halfExtX = (ovXMax - ovXMin) * 0.5f;
        float halfExtY = (ovYMax - ovYMin) * 0.5f;

        float effectiveHalfWidth = TrackHalfWidth * _scale;

        // WorldHeightMin/Max are already 8×-scaled (BtdFile applies the multiplier
        // during parse for Starfield files).
        float heightRange = btd.WorldHeightMax - btd.WorldHeightMin;

        // ------------------------------------------------------------------
        // 1.  Run CandidateCount scoring attempts and pick the best layout.
        // ------------------------------------------------------------------
        var rng = state.Rng;
        var wx  = new float[WaypointCount];
        var wy  = new float[WaypointCount];

        // Reusable buffers for ScoreCandidate — avoids 100× allocation in hot loop.
        var ssx = new float[ScoreSamples];
        var ssy = new float[ScoreSamples];

        float[] bestWx    = null;
        float[] bestWy    = null;
        float   bestScore = float.MinValue;
        int     bestAttempt = -1;

        for (int attempt = 0; attempt < CandidateCount; attempt++)
        {
            GenerateWaypoints(rng, WaypointCount, ovCX, ovCY,
                halfExtX, halfExtY, effectiveHalfWidth + 8f, wx, wy);

            float score = ScoreCandidate(btd, wx, wy, WaypointCount,
                ovXMin, ovXMax, ovYMin, ovYMax,
                ovCX, ovCY, effectiveHalfWidth, heightRange,
                ssx, ssy);

            if (score > bestScore)
            {
                bestScore   = score;
                bestAttempt = attempt;
                if (bestWx == null)
                {
                    bestWx = new float[WaypointCount];
                    bestWy = new float[WaypointCount];
                }
                Array.Copy(wx, bestWx, WaypointCount);
                Array.Copy(wy, bestWy, WaypointCount);
            }
        }

        if (!RetrogradeContext.Quiet)
            Console.WriteLine(
                $"[RacetrackTerrainPass] Best score={bestScore:F4} " +
                $"(attempt {bestAttempt + 1}/{CandidateCount})");

        // Fallback: if every candidate was rejected (out of bounds), use a plain
        // circle centred in the editable area.
        if (bestWx == null)
        {
            if (!RetrogradeContext.Quiet)
                Console.WriteLine("[RacetrackTerrainPass] WARNING: all candidates rejected — using fallback circle.");

            bestWx = new float[WaypointCount];
            bestWy = new float[WaypointCount];
            float r      = Math.Min(halfExtX, halfExtY) * 0.6f;
            float sector = MathF.PI * 2f / WaypointCount;
            for (int i = 0; i < WaypointCount; i++)
            {
                bestWx[i] = ovCX + r * MathF.Cos(i * sector);
                bestWy[i] = ovCY + r * MathF.Sin(i * sector);
            }
        }

        // ------------------------------------------------------------------
        // 2a. Place an XMarker at each waypoint and collect them in a FormList.
        // ------------------------------------------------------------------
        {
            var sfModKey   = RetrogradeContext.Current.StarfieldModKey;
            var targetMod  = RetrogradeContext.Current.TargetMod;
            var xMarkerKey = new FormKey(sfModKey, 0x3B); // XMarker

            var formList = new FormList(targetMod)
            {
                EditorID = (state.Worldspace.EditorID ?? "RG_Racetrack") + "_WaypointList",
                Items    = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };

            for (int i = 0; i < WaypointCount; i++)
            {
                float wZ = btd.SampleHeightAtWorld(bestWx[i] * BtdScale, bestWy[i] * BtdScale) / 8f;

                var marker = new PlacedObject(targetMod)
                {
                    Base     = xMarkerKey.ToLink<IPlaceableObjectGetter>(),
                    Position = new P3Float(bestWx[i], bestWy[i], wZ),
                    Rotation = new P3Float(0, 0, 0),
                };
                state.PlacementUtil.AddToPersistent(marker);
                formList.Items.Add(marker.ToLink<IStarfieldMajorRecordGetter>());
            }

            targetMod.FormLists.Add(formList);

            if (!RetrogradeContext.Quiet)
                Console.WriteLine(
                    $"[RacetrackTerrainPass] Placed {WaypointCount} XMarkers → FormList {formList.EditorID}");
        }

        // State used by other passes (boss, marker, tile grid origin).
        // Use (0, 0) — the worldspace origin — which is within the editable area
        // for all supported BTD sizes.
        state.FlatAreaWorldX = 0f;
        state.FlatAreaWorldY = 0f;
        state.TerrainHeight  = btd.SampleHeightAtWorld(0f, 0f) / 8f;

        // ------------------------------------------------------------------
        // 2.  Sample the winning spline at full editing resolution.
        // ------------------------------------------------------------------
        var spx = new float[SplineSamples];
        var spy = new float[SplineSamples];
        SampleSpline(bestWx, bestWy, WaypointCount, SplineSamples, spx, spy);

        // ------------------------------------------------------------------
        // 3.  Build a blend buffer: for each vertex, maximum linear blend
        //     weight from any nearby spline sample.
        //     Uses per-sample bounding boxes → O(SplineSamples × (2w/OvVtx)²)
        //     instead of O(totalVertices × SplineSamples).
        // ------------------------------------------------------------------
        var blend = new float[totalW * totalH]; // zero-initialised
        BuildBlendBuffer(spx, spy, SplineSamples,
            totalW, totalH, ovXMin, ovYMin, effectiveHalfWidth, blend);

        // ------------------------------------------------------------------
        // 4.  Read original heights, apply blur, write back within the band.
        // ------------------------------------------------------------------
        var origHeights = new ushort[totalW * totalH];
        ReadEditableHeights(btd, editMinX, editMinY, editMaxX, editMaxY, totalW, origHeights);

        ApplyTerrain(btd, origHeights, blend, totalW, totalH,
            editMinX, editMinY, editMaxX, editMaxY);

        btd.SmoothDirtyCellEdges(24);

        // ------------------------------------------------------------------
        // 5.  Normalise palettes; paint track texture.
        // ------------------------------------------------------------------
        ApplyTexture(btd, blend, totalW, totalH,
            editMinX, editMinY, editMaxX, editMaxY);
    }

    // ==================================================================
    // Waypoint generation — 10 sector-anchored random points
    // ==================================================================

    /// <summary>
    /// Places <paramref name="count"/> waypoints around (<paramref name="cx"/>,
    /// <paramref name="cy"/>), one per angular sector of 2π/count.  Within each
    /// sector the angle is chosen uniformly at random.  The radius is chosen
    /// uniformly in [rMin, rMax] where rMax is the furthest distance in that
    /// angular direction before reaching the inset boundary of the editable area.
    /// </summary>
    private static void GenerateWaypoints(
        Random rng, int count,
        float cx, float cy,
        float halfExtX, float halfExtY, float inset,
        float[] wx, float[] wy)
    {
        float sector = MathF.PI * 2f / count;
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(rng.NextDouble() * sector + i * sector);
            float cos   = MathF.Cos(angle);
            float sin   = MathF.Sin(angle);

            // Rectangle formula: furthest radius in direction (cos, sin) before
            // hitting the axis-aligned editable inset boundary.
            float rMaxX = MathF.Abs(cos) > 1e-4f
                ? (halfExtX - inset) / MathF.Abs(cos)
                : float.MaxValue;
            float rMaxY = MathF.Abs(sin) > 1e-4f
                ? (halfExtY - inset) / MathF.Abs(sin)
                : float.MaxValue;
            float rMax = Math.Min(rMaxX, rMaxY);

            // Minimum radius: 1.5× inset so the circuit doesn't degenerate.
            float rMin   = inset * 1.5f;
            float radius = rMin + (float)rng.NextDouble() * Math.Max(0f, rMax - rMin);

            wx[i] = cx + cos * radius;
            wy[i] = cy + sin * radius;
        }
    }

    // ==================================================================
    // Catmull-Rom closed spline
    // ==================================================================

    private static float CatmullRom(float p0, float p1, float p2, float p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2 * p1)
            + (-p0 + p2) * t
            + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2
            + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
    }

    /// <summary>
    /// Evaluates the closed Catmull-Rom spline through <paramref name="n"/>
    /// control points at <paramref name="samples"/> evenly spaced parameter values.
    /// Results are written into the caller-supplied arrays <paramref name="spx"/>
    /// and <paramref name="spy"/>.
    /// </summary>
    private static void SampleSpline(
        float[] wx, float[] wy, int n, int samples,
        float[] spx, float[] spy)
    {
        float step = (float)n / samples;
        for (int s = 0; s < samples; s++)
        {
            float gt  = s * step;
            int   seg = (int)gt % n;
            float t   = gt - (int)gt;

            int i0 = ((seg - 1) % n + n) % n;
            int i1 = seg;
            int i2 = (seg + 1) % n;
            int i3 = (seg + 2) % n;

            spx[s] = CatmullRom(wx[i0], wx[i1], wx[i2], wx[i3], t);
            spy[s] = CatmullRom(wy[i0], wy[i1], wy[i2], wy[i3], t);
        }
    }

    // ==================================================================
    // Candidate scoring
    // ==================================================================

    /// <summary>
    /// Returns a score in [0, 1] for the given waypoint set, or -1 if the
    /// spline exits the editable boundary.
    /// </summary>
    private static float ScoreCandidate(
        BtdFile btd,
        float[] wx, float[] wy, int n,
        float ovXMin, float ovXMax, float ovYMin, float ovYMax,
        float cx, float cy,
        float halfWidth, float heightRange,
        float[] spx, float[] spy)
    {
        SampleSpline(wx, wy, n, ScoreSamples, spx, spy);

        // Reject if any spline point is too close to or outside the editable boundary.
        float margin = halfWidth + 5f;
        for (int s = 0; s < ScoreSamples; s++)
        {
            if (spx[s] < ovXMin + margin || spx[s] > ovXMax - margin ||
                spy[s] < ovYMin + margin || spy[s] > ovYMax - margin)
                return -1f;
        }

        // -- Terrain cross-slope (60 % weight) -------------------------
        // For each spline sample, measure the height difference across the
        // track (perpendicular to the tangent) over a ±Delta overlay-unit span.
        // Low cross-slope → terrain flows along the track → high score.
        float crossSum = 0f;
        const float Delta = 2f; // overlay units along normal

        for (int s = 0; s < ScoreSamples; s++)
        {
            // Central-difference tangent from adjacent spline samples.
            int prev = (s - 1 + ScoreSamples) % ScoreSamples;
            int next = (s + 1) % ScoreSamples;
            float tx  = spx[next] - spx[prev];
            float ty  = spy[next] - spy[prev];
            float len = MathF.Sqrt(tx * tx + ty * ty);
            if (len < 1e-6f) continue;
            tx /= len;
            ty /= len;

            // Perpendicular: (-ty, tx).  Convert to BTD-internal displacement.
            float dBtdX = -ty * Delta * BtdScale;
            float dBtdY =  tx * Delta * BtdScale;
            float btdX  = spx[s] * BtdScale;
            float btdY  = spy[s] * BtdScale;

            float hL = btd.SampleHeightAtWorld(btdX + dBtdX, btdY + dBtdY);
            float hR = btd.SampleHeightAtWorld(btdX - dBtdX, btdY - dBtdY);
            crossSum += MathF.Abs(hR - hL);
        }

        float meanCross    = crossSum / ScoreSamples;
        // Normalise by 1 % of the BTD height range to get a dimensionless ratio.
        float normCross    = meanCross / (heightRange * 0.01f + 1f);
        float terrainScore = 1f / (1f + normCross);

        // -- Spread: minimum angular gap between waypoints (40 % weight) --
        // Compute the angle of each waypoint around the editable centre,
        // sort, find the tightest gap.  Ideal = 2π/n (perfectly even spacing).
        var angles = new float[n];
        for (int i = 0; i < n; i++)
        {
            angles[i] = MathF.Atan2(wy[i] - cy, wx[i] - cx);
            if (angles[i] < 0f) angles[i] += MathF.PI * 2f;
        }
        Array.Sort(angles);

        float idealGap = MathF.PI * 2f / n;
        float minGap   = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            float gap = (i < n - 1)
                ? angles[i + 1] - angles[i]
                : angles[0] + MathF.PI * 2f - angles[n - 1];
            if (gap < minGap) minGap = gap;
        }
        float spreadScore = Math.Clamp(minGap / idealGap, 0f, 1f);

        // -- Self-intersection penalty -----------------------------------
        // Count pairs of spline samples that are close enough to indicate an
        // overlap but are NOT adjacent on the closed circuit.
        int   minSep    = ScoreSamples / (n * 2);               // ~half-segment samples
        float tooClose2 = (halfWidth * 1.5f) * (halfWidth * 1.5f);
        int   closePairs = 0;
        for (int a = 0; a < ScoreSamples; a++)
        {
            for (int b = a + 1; b < ScoreSamples; b++)
            {
                // Skip pairs adjacent on the closed circuit (within minSep samples).
                int circDist = Math.Min(b - a, ScoreSamples - (b - a));
                if (circDist <= minSep) continue;

                float dx = spx[a] - spx[b];
                float dy = spy[a] - spy[b];
                if (dx * dx + dy * dy < tooClose2)
                    closePairs++;
            }
        }
        float intersectionScore = closePairs == 0
            ? 1f
            : MathF.Exp(-closePairs * 0.1f);

        return (terrainScore * 0.6f + spreadScore * 0.4f) * intersectionScore;
    }

    // ==================================================================
    // Blend buffer — per-sample bounding-box update
    // ==================================================================

    /// <summary>
    /// For each spline sample, restricts updates to the axis-aligned bounding box
    /// of vertices within <paramref name="halfWidth"/> overlay units.  Each vertex
    /// retains the maximum linear blend weight from any sample.
    ///
    /// Complexity: O(SplineSamples × (2·halfWidth/OvVtx)²) ≈ O(S·W²),
    /// which is ~20× faster than the naïve O(totalVertices × S) for
    /// the half-widths and vertex densities used here.
    /// </summary>
    private static void BuildBlendBuffer(
        float[] spx, float[] spy, int samples,
        int totalW, int totalH,
        float ovXMin, float ovYMin,
        float halfWidth, float[] blend)
    {
        for (int s = 0; s < samples; s++)
        {
            int gxMin = Math.Max(0,        (int)MathF.Floor((spx[s] - ovXMin - halfWidth) / OvVtx));
            int gxMax = Math.Min(totalW-1, (int)MathF.Ceiling((spx[s] - ovXMin + halfWidth) / OvVtx));
            int gyMin = Math.Max(0,        (int)MathF.Floor((spy[s] - ovYMin - halfWidth) / OvVtx));
            int gyMax = Math.Min(totalH-1, (int)MathF.Ceiling((spy[s] - ovYMin + halfWidth) / OvVtx));

            for (int gy = gyMin; gy <= gyMax; gy++)
            {
                float ovY = ovYMin + gy * OvVtx;
                float dy  = ovY - spy[s];

                for (int gx = gxMin; gx <= gxMax; gx++)
                {
                    float ovX = ovXMin + gx * OvVtx;
                    float dx  = ovX - spx[s];
                    float w   = 1f - MathF.Sqrt(dx * dx + dy * dy) / halfWidth;
                    if (w <= 0f) continue;

                    int idx = gy * totalW + gx;
                    if (w > blend[idx]) blend[idx] = w;
                }
            }
        }
    }

    // ==================================================================
    // Terrain application — blur and lerp within the band
    // ==================================================================
    private static void ApplyTerrain(
        BtdFile btd, ushort[] origHeights, float[] blend,
        int totalW, int totalH,
        int editMinX, int editMinY, int editMaxX, int editMaxY)
    {
        // Box-blur the full editable height buffer (4 passes, radius 3 vertices).
        // Blurring the entire buffer — not just the band — ensures the Lerp
        // transition at track edges uses genuine terrain neighbours, not zeros.
        var smoothed = (ushort[])origHeights.Clone();
        for (int p = 0; p < 4; p++)
            BoxBlurPass(smoothed, totalW, totalH, 3);

        var hBuf = new ushort[BtdFile.CellResolution * BtdFile.CellResolution];

        for (int cy = editMinY; cy <= editMaxY; cy++)
        {
            for (int cx = editMinX; cx <= editMaxX; cx++)
            {
                btd.GetCellHeightMap(hBuf, cx, cy);
                bool modified = false;

                int baseGx = (cx - editMinX) * BtdFile.CellResolution;
                int baseGy = (cy - editMinY) * BtdFile.CellResolution;

                for (int vy = 0; vy < BtdFile.CellResolution; vy++)
                {
                    int gy = baseGy + vy;
                    for (int vx = 0; vx < BtdFile.CellResolution; vx++)
                    {
                        int gx = baseGx + vx;
                        float b = blend[gy * totalW + gx];
                        if (b <= 0f) continue;

                        int idx  = vy * BtdFile.CellResolution + vx;
                        int gIdx = gy * totalW + gx;
                        hBuf[idx] = Lerp(origHeights[gIdx], smoothed[gIdx], b);
                        modified  = true;
                    }
                }

                if (modified)
                    btd.SetCellHeightMap(hBuf, cx, cy);
            }
        }
    }

    // ==================================================================
    // Texture application — normalise palettes then paint
    // ==================================================================
    private static void ApplyTexture(
        BtdFile btd, float[] blend,
        int totalW, int totalH,
        int editMinX, int editMinY, int editMaxX, int editMaxY)
    {
        // Normalise the 32-byte land-texture palette on every cell.
        // Quadrant splits occur when adjacent cells' palettes differ; copying
        // quadrant-0 across all four quadrants eliminates this.
        var palette = new byte[32];
        btd.GetCellLandTexMap(palette, editMinX, editMinY);
        for (int q = 1; q < 4; q++)
            Array.Copy(palette, 0, palette, q * 8, 8);

        for (int cy = btd.CellMinY; cy <= btd.CellMaxY; cy++)
            for (int cx = btd.CellMinX; cx <= btd.CellMaxX; cx++)
                btd.SetCellLandTexMap(palette, cx, cy);

        var tBuf     = new ushort[BtdFile.CellResolution * BtdFile.CellResolution];
        var lod4Zero = new byte[128];

        for (int cy = editMinY; cy <= editMaxY; cy++)
        {
            for (int cx = editMinX; cx <= editMaxX; cx++)
            {
                btd.GetCellTextureData(tBuf, cx, cy);
                bool modified = false;

                int baseGx = (cx - editMinX) * BtdFile.CellResolution;
                int baseGy = (cy - editMinY) * BtdFile.CellResolution;

                for (int vy = 0; vy < BtdFile.CellResolution; vy++)
                {
                    int gy = baseGy + vy;
                    for (int vx = 0; vx < BtdFile.CellResolution; vx++)
                    {
                        // blend ≥ 0.15 → dist ≤ 0.85 × halfWidth → paint the core band.
                        if (blend[(gy * totalW) + baseGx + vx] < 0.15f) continue;

                        tBuf[vy * BtdFile.CellResolution + vx] = TrackTexture;
                        modified = true;
                    }
                }

                if (modified)
                {
                    btd.SetCellTextureData(tBuf, cx, cy);
                    btd.SetCellLod4LandTex(lod4Zero, cx, cy);
                }
            }
        }
    }

    // ==================================================================
    // Shared helpers
    // ==================================================================

    private static void ReadEditableHeights(
        BtdFile btd,
        int editMinX, int editMinY, int editMaxX, int editMaxY,
        int totalW, ushort[] heights)
    {
        var buf = new ushort[BtdFile.CellResolution * BtdFile.CellResolution];
        for (int cy = editMinY; cy <= editMaxY; cy++)
        {
            for (int cx = editMinX; cx <= editMaxX; cx++)
            {
                btd.GetCellHeightMap(buf, cx, cy);
                int bx = (cx - editMinX) * BtdFile.CellResolution;
                int by = (cy - editMinY) * BtdFile.CellResolution;
                for (int vy = 0; vy < BtdFile.CellResolution; vy++)
                    for (int vx = 0; vx < BtdFile.CellResolution; vx++)
                        heights[(by + vy) * totalW + (bx + vx)]
                            = buf[vy * BtdFile.CellResolution + vx];
            }
        }
    }

    /// <summary>Separable box blur: one horizontal pass then one vertical pass.</summary>
    private static void BoxBlurPass(ushort[] buf, int w, int h, int radius)
    {
        var temp = new ushort[w * h];

        // Horizontal
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int sum = 0, cnt = 0;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int nx = x + dx;
                    // (uint) cast makes the bounds check branch-free.
                    if ((uint)nx < (uint)w) { sum += buf[y * w + nx]; cnt++; }
                }
                temp[y * w + x] = (ushort)(sum / cnt);
            }
        }

        // Vertical
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int sum = 0, cnt = 0;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int ny = y + dy;
                    if ((uint)ny < (uint)h) { sum += temp[ny * w + x]; cnt++; }
                }
                buf[y * w + x] = (ushort)(sum / cnt);
            }
        }
    }

    private static ushort Lerp(ushort a, ushort b, float t)
        => (ushort)Math.Clamp((int)Math.Round(a + (b - a) * t), 0, 65535);
}
