using System;
using System.Collections.Generic;
using System.Linq;
using Retrograde.Models;
using Retrograde.Utils;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Map layout pass for SmallIndustryBase POIs.
/// Places 3–6 GPPIPCMManMade_ buildings in a compact cluster selected by candidate sampling.
///
/// 300 random configurations are scored on four components:
///   Flatness   0.40 — prefer already-flat terrain (low BTD height variance under footprints)
///   Clustering 0.30 — prefer tight clusters (low mean pairwise anchor distance)
///   LOS        0.20 — prefer clear sightlines (low ridge penalty between building pairs)
///   Variation  0.10 — prefer distinct categories and diverse rotations
///
/// Dead zone: no anchor within 4 tiles of any map edge.
/// Spread:    each building placed 5–10 tiles from the anchor; per-building ObjectBounds
///            radius prevents physical overlap between all category combinations.
/// Fallback:  if fewer than 5 valid candidates are found, a compact centre grid is used.
/// </summary>
/// <param name="scale">Controls building count (0.1 = 3, 1.0 = 6).</param>
public class IndustryLayoutPass(float scale = 0.5f) : IWorldspacePass
{
    private readonly float _scale = Math.Clamp(scale, 0.1f, 1.0f);

    private static readonly string[] CategoryKeys =
    [
        "industry_abandoned",
        "industry_large",
        "industry_comms",
        "industry_mech_large",
        "industry_mech_medium",
        "industry_fluid_xl",
        "industry_fluid_large",
        "industry_fluid_medium",
        "industry_solar",
        "industry_reactor",
        "industry_storage",
        "industry_foundations",
        "industry_clutter",
    ];

    private const int CandidateCount    = 300;
    private const int PlacementTries    = 20;
    private const int   SpreadMin       = 5;    // map tiles — minimum distance from anchor to try
    private const int   SpreadMax       = 10;   // map tiles — maximum cluster radius from anchor
    private const float FallbackRadius  = 4f;   // overlay units — used when ObjectBounds data is absent
    private const int   DeadZone        = 4;
    private const int FallbackThreshold = 5;

    // ── entry point ──────────────────────────────────────────────────────────

    public void RunPass(WorldspaceState state)
    {
        var rand  = state.Rng;
        var map   = state.Map;
        int count = GetBuildingCount(_scale, map.xsize);

        var radii      = state.PackInRadii;
        var tws        = state.TileWorldSize;
        var candidates = new List<Candidate>(CandidateCount);
        for (int i = 0; i < CandidateCount; i++)
        {
            var c = TryGenerateCandidate(map, rand, count, radii, tws);
            if (c != null) candidates.Add(c);
        }

        if (candidates.Count < FallbackThreshold)
        {
            Console.Error.WriteLine(
                $"[IndustryLayoutPass] WARNING: only {candidates.Count} valid candidates " +
                $"(threshold {FallbackThreshold}). Using grid fallback.");
            PlaceFallbackGrid(map, rand, count);
            // Fallback centres on the flat area origin.
            state.PoiCenterX = state.FlatAreaWorldX ?? 0f;
            state.PoiCenterY = state.FlatAreaWorldY ?? 0f;
            return;
        }

        ScoreCandidates(candidates, state);
        var best = candidates.OrderByDescending(c => c.Score).First();

        foreach (var b in best.Buildings)
            map.placesmalltile(b.X, b.Y, b.Key, b.Rotation, "floor");

        // Publish the anchor building position in overlay world space so
        // downstream passes (scatter, map marker, boss, etc.) can orient
        // themselves around the actual base location.
        // Mirrors TileInstantiationPass: worldX = FlatAreaWorldX + bs*(x - xsize/2)
        //                                worldY = FlatAreaWorldY - bs*(y - ysize/2)
        // TerrainFlattenPass must run before this pass so FlatAreaWorldX/Y are set.
        float mapCentre = map.xsize / 2f;
        float tileSize  = state.TileWorldSize;
        var anchor = best.Buildings[0];
        state.PoiCenterX = (state.FlatAreaWorldX ?? 0f) + (anchor.X - mapCentre) * tileSize;
        state.PoiCenterY = (state.FlatAreaWorldY ?? 0f) - (anchor.Y - mapCentre) * tileSize;
    }

    // ── candidate generation ─────────────────────────────────────────────────

    private static Candidate? TryGenerateCandidate(
        GenerationMap map, Random rand, int count,
        Dictionary<string, float> radii, float tileWorldSize)
    {
        int validMin  = DeadZone;
        int validMaxX = map.xsize - DeadZone - 1;
        int validMaxY = map.ysize - DeadZone - 1;
        if (validMin >= validMaxX || validMin >= validMaxY) return null;

        int anchorX = rand.Next(validMin, validMaxX + 1);
        int anchorY = rand.Next(validMin, validMaxY + 1);

        var keys      = SelectKeys(rand, count);
        var buildings = new List<Building>(count)
        {
            new(anchorX, anchorY, keys[0], rand.Next(4) * 90),
        };

        for (int b = 1; b < count; b++)
        {
            bool placed = false;
            for (int t = 0; t < PlacementTries; t++)
            {
                double angle = rand.NextDouble() * Math.PI * 2.0;
                int    dist  = rand.Next(SpreadMin, SpreadMax + 1);
                int    x     = anchorX + (int)Math.Round(Math.Cos(angle) * dist);
                int    y     = anchorY + (int)Math.Round(Math.Sin(angle) * dist);

                if (x < validMin || x > validMaxX || y < validMin || y > validMaxY) continue;
                if (!CanPlaceOnBuildings(buildings, x, y, keys[b], radii, tileWorldSize)) continue;

                buildings.Add(new Building(x, y, keys[b], rand.Next(4) * 90));
                placed = true;
                break;
            }
            if (!placed) return null;
        }

        return new Candidate(buildings);
    }

    private static List<string> SelectKeys(Random rand, int count)
    {
        var shuffled = CategoryKeys.ToList();
        Shuffle(shuffled, rand);
        var keys = new List<string>(count);
        for (int i = 0; i < count; i++)
            keys.Add(shuffled[i % shuffled.Count]);
        return keys;
    }

    // Returns true if (x, y) with category newKey does not physically overlap any
    // already-placed building. Minimum separation = sum of the two buildings' overlay
    // radii (from ObjectBounds), converted to map tiles.
    private static bool CanPlaceOnBuildings(
        List<Building> buildings, int x, int y, string newKey,
        Dictionary<string, float> radii, float tileWorldSize)
    {
        float newR = GetMapRadius(newKey, radii, tileWorldSize);
        foreach (var b in buildings)
        {
            float minDist = newR + GetMapRadius(b.Key, radii, tileWorldSize);
            float dx = x - b.X;
            float dy = y - b.Y;
            if (dx * dx + dy * dy < minDist * minDist) return false;
        }
        return true;
    }

    private static float GetMapRadius(string key, Dictionary<string, float> radii, float tileWorldSize)
        => radii.TryGetValue(key, out float ov) ? ov / tileWorldSize : FallbackRadius;

    // ── scoring ──────────────────────────────────────────────────────────────

    private static void ScoreCandidates(List<Candidate> candidates, WorldspaceState state)
    {
        int n = candidates.Count;
        var flatness   = new float[n];
        var clustering = new float[n];
        var los        = new float[n];
        var variation  = new float[n];

        float mapCentre = state.Map.xsize / 2f;
        float tws       = state.TileWorldSize;
        var   btd       = state.BtdFile;

        for (int i = 0; i < n; i++)
        {
            var c = candidates[i];
            if (btd != null)
            {
                flatness[i] = ComputeFlatnessVariance(c.Buildings, mapCentre, tws, btd);
                los[i]      = ComputeLosPenalty(c.Buildings, mapCentre, tws, btd);
            }
            clustering[i] = ComputeMeanPairwiseDist(c.Buildings);
            variation[i]  = ComputeVariation(c.Buildings);
        }

        // Normalise each array to [0,1] across the candidate set.
        // When all values are equal (range ≈ 0), Normalise sets all to 1.0 — no penalty for a tie.
        Normalise(flatness);
        Normalise(clustering);
        Normalise(los);
        Normalise(variation);

        for (int i = 0; i < n; i++)
        {
            candidates[i].Score =
                  0.40f * (1f - flatness[i])    // lower variance = better
                + 0.30f * (1f - clustering[i])  // closer = better
                + 0.20f * (1f - los[i])         // fewer ridges = better
                + 0.10f * variation[i];          // more variety = better
        }
    }

    // Convert a map coordinate to BTD world space.
    // Map → overlay: (mapCoord - mapCentre) * TileWorldSize
    // Overlay → BTD world: overlayCoord * (4096f / 100f)
    private static float ToBtd(float mapCoord, float mapCentre, float tws)
        => (mapCoord - mapCentre) * tws * (4096f / 100f);

    private static float ComputeFlatnessVariance(
        List<Building> buildings, float mapCentre, float tws, BtdFile btd)
    {
        var heights = new List<float>(buildings.Count * 4);
        foreach (var b in buildings)
        {
            // Four corners of the 3×3 tile footprint
            foreach (int dx in new[] { -1, 1 })
            foreach (int dy in new[] { -1, 1 })
                heights.Add(btd.SampleHeightAtWorld(
                    ToBtd(b.X + dx, mapCentre, tws),
                    ToBtd(b.Y + dy, mapCentre, tws)));
        }
        return Variance(heights);
    }

    private static float ComputeMeanPairwiseDist(List<Building> buildings)
    {
        if (buildings.Count < 2) return 0f;
        float sum = 0f;
        int   cnt = 0;
        for (int i = 0; i < buildings.Count - 1; i++)
        for (int j = i + 1; j < buildings.Count; j++)
        {
            float dx = buildings[i].X - buildings[j].X;
            float dy = buildings[i].Y - buildings[j].Y;
            sum += MathF.Sqrt(dx * dx + dy * dy);
            cnt++;
        }
        return sum / cnt;
    }

    private static float ComputeLosPenalty(
        List<Building> buildings, float mapCentre, float tws, BtdFile btd)
    {
        const int Samples = 8;
        float total = 0f;
        for (int i = 0; i < buildings.Count - 1; i++)
        for (int j = i + 1; j < buildings.Count; j++)
        {
            float ax = ToBtd(buildings[i].X, mapCentre, tws);
            float ay = ToBtd(buildings[i].Y, mapCentre, tws);
            float bx = ToBtd(buildings[j].X, mapCentre, tws);
            float by = ToBtd(buildings[j].Y, mapCentre, tws);
            float ha = btd.SampleHeightAtWorld(ax, ay);
            float hb = btd.SampleHeightAtWorld(bx, by);

            for (int k = 1; k <= Samples; k++)
            {
                float t      = k / (float)(Samples + 1);
                float hp     = btd.SampleHeightAtWorld(ax + (bx - ax) * t, ay + (by - ay) * t);
                float excess = hp - (ha + (hb - ha) * t);
                if (excess > 0f) total += excess;
            }
        }
        return total;
    }

    private static float ComputeVariation(List<Building> buildings)
    {
        int n = buildings.Count;
        if (n == 0) return 0f;
        float catVariety = (float)buildings.Select(b => b.Key).Distinct().Count() / n;
        float rotVariety = (float)buildings.Select(b => b.Rotation).Distinct().Count()
                         / Math.Min(4, n);
        return (catVariety + rotVariety) / 2f;
    }

    private static void Normalise(float[] values)
    {
        float min   = values.Min();
        float max   = values.Max();
        float range = max - min;
        if (range < 1e-6f) { Array.Fill(values, 1f); return; }
        for (int i = 0; i < values.Length; i++)
            values[i] = (values[i] - min) / range;
    }

    private static float Variance(List<float> values)
    {
        if (values.Count == 0) return 0f;
        float mean = values.Average();
        return values.Average(v => (v - mean) * (v - mean));
    }

    // ── fallback ─────────────────────────────────────────────────────────────

    private static void PlaceFallbackGrid(GenerationMap map, Random rand, int count)
    {
        var keys = SelectKeys(rand, count);
        int cx = map.xsize / 2;
        int cy = map.ysize / 2;
        (int x, int y)[] slots =
        [
            (cx,     cy),
            (cx + 6, cy),
            (cx - 6, cy),
            (cx,     cy + 6),
            (cx + 6, cy + 6),
            (cx - 6, cy - 6),
        ];
        for (int i = 0; i < count && i < slots.Length; i++)
            map.placesmalltile(slots[i].x, slots[i].y, keys[i], rand.Next(4) * 90, "floor");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    // Scale building count linearly with map size, using mapSize=50 (4×4 BTD) as the baseline.
    // scale then acts as a fraction of that maximum, clamped to a minimum of 3.
    private static int GetBuildingCount(float scale, int mapSize)
    {
        int max = Math.Max(3, (int)Math.Round(6f * mapSize / 50f));
        return Math.Clamp((int)Math.Round(max * scale), 3, max);
    }

    private static void Shuffle<T>(List<T> list, Random rand)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ── data types ───────────────────────────────────────────────────────────

    private sealed class Building(int x, int y, string key, int rotation)
    {
        public readonly int    X        = x;
        public readonly int    Y        = y;
        public readonly string Key      = key;
        public readonly int    Rotation = rotation;
    }

    private sealed class Candidate(List<Building> buildings)
    {
        public readonly List<Building> Buildings = buildings;
        public float Score;
    }
}
