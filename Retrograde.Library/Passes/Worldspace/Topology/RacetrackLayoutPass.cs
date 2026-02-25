using System;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Map layout pass for racetrack POIs.
///
/// Generates a closed oval loop on the GenerationMap:
///  - Tile positions in the annular region between the outer and inner ellipses
///    receive track surface tiles ("rt_surface").
///  - A pit-lane strip ("rt_box") is placed on the southern straight just inside
///    the outer ellipse boundary, centred on the map.
///  - Barrier addon tiles ("rt_barrier") are added along the inner track edge,
///    where surface tiles approach the central island.
///
/// The central island (inside the inner ellipse) is deliberately left empty so
/// that rock and vegetation scatter passes can populate it with terrain details.
///
/// Tile keys are resolved against the PackIn library by RacetrackDesign.RacetrackPackInIds().
/// The same tile key can map to zero variants when the matching PackIn EditorIDs are absent
/// from the template mods — in that case TileInstantiationPass silently skips those tiles.
/// </summary>
public class RacetrackLayoutPass(float trackScale = 1.0f) : IWorldspacePass
{
    private readonly float _scale = Math.Clamp(trackScale, 0.4f, 1.0f);

    // All values are in tile-grid index units (not world units).
    // The map is 50×50 tiles; we centre the oval on the nearest 3-step-aligned index.
    private const int CenterX = 24;
    private const int CenterY = 24;

    // Un-scaled outer ellipse semi-axes (track outer boundary).
    private const float BaseOuterA = 18f;
    private const float BaseOuterB = 14f;

    // Un-scaled inner ellipse semi-axes (central island boundary).
    private const float BaseInnerA = 10f;
    private const float BaseInnerB =  7f;

    // How close (in normalised inner-ellipse units) a surface tile has to be
    // to the inner island edge before a barrier addon is added.
    private const float BarrierEdgeThreshold = 1.55f;

    public void RunPass(WorldspaceState state)
    {
        var map = state.Map;

        float outerA = BaseOuterA * _scale;
        float outerB = BaseOuterB * _scale;
        float innerA = BaseInnerA * _scale;
        float innerB = BaseInnerB * _scale;

        // ------------------------------------------------------------------ //
        // Pass 1: track surface — annular region between outer and inner ellipses
        // ------------------------------------------------------------------ //
        for (int x = 3; x < map.xsize - 3; x += 3)
        {
            for (int y = 3; y < map.ysize - 3; y += 3)
            {
                float dx = x - CenterX;
                float dy = y - CenterY;

                float outerDist = (dx * dx) / (outerA * outerA) + (dy * dy) / (outerB * outerB);
                float innerDist = (dx * dx) / (innerA * innerA) + (dy * dy) / (innerB * innerB);

                // Must be inside outer ellipse and outside inner island
                if (outerDist > 1.0f || innerDist < 1.0f) continue;

                int rotation = DetermineTrackRotation(dx, dy, outerA, outerB);
                map.placesmalltile(x, y, "rt_surface", rotation, "rt_fill");
            }
        }

        // ------------------------------------------------------------------ //
        // Pass 2: pit-lane strip on southern straight
        //   Row at approximately CenterY + outerB - 3 (just inside outer edge).
        //   Centred on CenterX, width ±6 tile-grid units.
        // ------------------------------------------------------------------ //
        int pitY = CenterY + (int)Math.Round(outerB) - 3;
        if (pitY >= 3 && pitY < map.ysize - 3)
        {
            for (int x = CenterX - 6; x <= CenterX + 6; x += 3)
            {
                if (x >= 3 && x < map.xsize - 3 && map.tiles[x][pitY].prefabs.Contains("rt_surface"))
                    map.placesmalltile(x, pitY, "rt_box", 0, "rt_fill");
            }
        }

        // ------------------------------------------------------------------ //
        // Pass 3: inner-edge barrier addons
        //   Added as overlays on surface tiles that are close to the island boundary.
        // ------------------------------------------------------------------ //
        for (int x = 3; x < map.xsize - 3; x += 3)
        {
            for (int y = 3; y < map.ysize - 3; y += 3)
            {
                if (!map.tiles[x][y].prefabs.Contains("rt_surface")) continue;

                float dx = x - CenterX;
                float dy = y - CenterY;
                float innerDist = (dx * dx) / (innerA * innerA) + (dy * dy) / (innerB * innerB);

                if (innerDist < BarrierEdgeThreshold)
                {
                    int rotation = DetermineTrackRotation(dx, dy, outerA, outerB);
                    map.placesmalladdontile(x, y, "rt_barrier", rotation, "ignore");
                }
            }
        }

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[RacetrackLayoutPass] Oval outerA={outerA:F1} outerB={outerB:F1} innerA={innerA:F1} innerB={innerB:F1}");
    }

    /// <summary>
    /// Returns a 0° or 90° rotation for a track tile at the given offset from centre.
    ///
    /// Tiles on the top/bottom straights (position near the ellipse's minor-axis tips)
    /// are oriented at 0°. Tiles on the left/right curves (near the major-axis ends) are
    /// oriented at 90°. The boundary is determined by comparing the ellipse tangent direction
    /// at the tile position.
    /// </summary>
    private static int DetermineTrackRotation(float dx, float dy, float a, float b)
    {
        // Normalised angle on the ellipse: atan2(dy/b, dx/a) gives the eccentric anomaly.
        float eccAngle = MathF.Atan2(dy / b, dx / a);
        float absAngle = MathF.Abs(eccAngle);

        // Top/bottom straights: eccentric angle within 40° of ±π/2.
        const float Arc = MathF.PI * 0.22f; // ~40°
        if (absAngle > MathF.PI / 2f - Arc && absAngle < MathF.PI / 2f + Arc)
            return 0;   // horizontal orientation (top/bottom)

        return 90;      // vertical orientation (left/right curves)
    }
}
