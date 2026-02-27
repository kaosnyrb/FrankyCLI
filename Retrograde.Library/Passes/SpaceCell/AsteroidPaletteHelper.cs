using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;

namespace Retrograde.Passes.SpaceCell;

/// <summary>
/// Size classes matching the size token embedded in vanilla asteroid EditorIDs.
/// MS_RockAsteroid[Rough|Ice]&lt;Size&gt;&lt;Number&gt;_NoLoot
/// Ordered smallest → largest so numeric comparison works for ShouldFixPhysics.
/// </summary>
public enum AsteroidSize { Small, Medium, Large, XLarge, XXLarge }

/// <summary>
/// Helpers for selecting palette asteroids by the size class in their EditorID,
/// and for computing scale noise around the natural mesh size.
/// </summary>
public static class AsteroidPaletteHelper
{
    /// <summary>
    /// Parses the size class from a MoveableStatic EditorID.
    /// Returns null if no recognised size token is found.
    /// "Boulder" is treated as Medium (it sits between Small and Medium visually).
    /// Longest tokens are checked first to avoid XLarge matching inside XXLarge.
    /// </summary>
    public static AsteroidSize? ParseSize(string? editorId)
    {
        if (editorId == null) return null;
        if (editorId.Contains("XXLarge", StringComparison.OrdinalIgnoreCase)) return AsteroidSize.XXLarge;
        if (editorId.Contains("XLarge",  StringComparison.OrdinalIgnoreCase)) return AsteroidSize.XLarge;
        if (editorId.Contains("Large",   StringComparison.OrdinalIgnoreCase)) return AsteroidSize.Large;
        if (editorId.Contains("Boulder", StringComparison.OrdinalIgnoreCase)) return AsteroidSize.Medium;
        if (editorId.Contains("Medium",  StringComparison.OrdinalIgnoreCase)) return AsteroidSize.Medium;
        if (editorId.Contains("Small",   StringComparison.OrdinalIgnoreCase)) return AsteroidSize.Small;
        return null;
    }

    /// <summary>
    /// Groups palette FormKeys by the size class in their EditorID.
    /// FormKeys whose EditorID has no recognised size token default to Medium.
    /// </summary>
    public static Dictionary<AsteroidSize, List<FormKey>> GroupBySize(
        List<FormKey> palette, IStarfieldModGetter starfieldMod)
    {
        var groups = new Dictionary<AsteroidSize, List<FormKey>>();
        foreach (AsteroidSize s in Enum.GetValues<AsteroidSize>())
            groups[s] = new List<FormKey>();

        foreach (var fk in palette)
        {
            IMoveableStaticGetter? rec = starfieldMod.MoveableStatics.ContainsKey(fk)
                ? starfieldMod.MoveableStatics[fk]
                : null;
            var size = ParseSize(rec?.EditorID) ?? AsteroidSize.Medium;
            groups[size].Add(fk);
        }
        return groups;
    }

    /// <summary>
    /// Picks a random FormKey from the closest available size bucket, falling back to
    /// adjacent sizes if the requested bucket is empty.
    /// Returns FormKey.Null only if every bucket is empty.
    /// </summary>
    public static FormKey Pick(
        Dictionary<AsteroidSize, List<FormKey>> groups,
        AsteroidSize desired,
        Random rng)
    {
        int centre = (int)desired;
        int total  = Enum.GetValues<AsteroidSize>().Length;
        for (int radius = 0; radius < total; radius++)
        {
            // radius 0 → try exact; radius 1 → try ±1; etc.
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int idx = centre + sign * radius;
                if (idx < 0 || idx >= total) continue;
                var bucket = groups[(AsteroidSize)idx];
                if (bucket.Count > 0)
                    return bucket[rng.Next(bucket.Count)];
                if (radius == 0) break; // only one try at radius 0
            }
        }
        return FormKey.Null;
    }

    /// <summary>
    /// ±15% scale noise around 1.0.
    /// Use this instead of large programmatic scale — the mesh already encodes visual size
    /// via its size class. Small variation keeps the field from looking uniform.
    /// </summary>
    public static float SizeNoise(Random rng)
        => 0.85f + (float)rng.NextDouble() * 0.30f;

    /// <summary>
    /// True for size classes large enough to warrant physics suppression (XALG = 8uL).
    /// Large, XLarge, XXLarge asteroids are fixed in place; smaller ones float freely.
    /// </summary>
    public static bool ShouldFixPhysics(AsteroidSize size)
        => size >= AsteroidSize.Large;

    /// <summary>
    /// Maps a normalised position t (0 = head/centre, 1 = tip/edge) to a size class.
    /// Drives the size taper in CometTailPass and CrescentBeltPass:
    ///   0.0 – 0.30  →  Large
    ///   0.30 – 0.65 →  Medium
    ///   0.65 – 1.0  →  Small
    /// </summary>
    public static AsteroidSize SizeFromT(float t)
    {
        if (t < 0.30f) return AsteroidSize.Large;
        if (t < 0.65f) return AsteroidSize.Medium;
        return AsteroidSize.Small;
    }
}
