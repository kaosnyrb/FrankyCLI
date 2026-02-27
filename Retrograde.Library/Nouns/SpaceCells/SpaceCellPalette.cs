using Mutagen.Bethesda.Plugins;
using System.Collections.Generic;

namespace Retrograde.Nouns.SpaceCells;

/// <summary>
/// Visual theme for a generated space cell — determines which mesh set is used.
/// </summary>
public enum SpaceCellPalette
{
    /// <summary>Dark rocky asteroid field — MS_RockAsteroidRough* variants (_NoLoot).</summary>
    Rocky,

    /// <summary>Icy asteroid field — MS_IceAsteroid* variants.</summary>
    Icy,

    /// <summary>Glacial shard field — IceShardHuge* (XXLarge) and IceBerg* (XLarge) Statics.</summary>
    IceShards,
}

/// <summary>
/// Hardcoded FormID tables for each SpaceCellPalette.
/// Rocky and Icy records are MoveableStatics; IceShards records are Statics.
/// </summary>
public static class SpaceCellPaletteData
{
    // ── Rocky — MS_RockAsteroidRough*_NoLoot ─────────────────────────────────────

    private static readonly uint[] RockyIds =
    {
        // Small
        0x1DDC64u, // MS_RockAsteroidRoughSmall01_NoLoot
        0x1DDC65u, // MS_RockAsteroidRoughSmall02_NoLoot
        0x1DDC66u, // MS_RockAsteroidRoughSmall03_NoLoot
        0x1DDC67u, // MS_RockAsteroidRoughSmall04_NoLoot
        0x1DDC68u, // MS_RockAsteroidRoughSmall05_NoLoot
        // Medium
        0x055F5Eu, // MS_RockAsteroidRoughMedium01_NoLoot
        0x055F5Fu, // MS_RockAsteroidRoughMedium02_NoLoot
        0x055F60u, // MS_RockAsteroidRoughMedium03_NoLoot
        // Boulder (Medium-class)
        0x0271A3u, // MS_RockAsteroidRoughBoulder01_NoLoot
        // Large
        0x0043DBu, // MS_RockAsteroidRoughLarge01_NoLoot
        0x055F59u, // MS_RockAsteroidRoughLarge02_NoLoot
        0x055F5Au, // MS_RockAsteroidRoughLarge03_NoLoot
        0x055F5Bu, // MS_RockAsteroidRoughLarge04_NoLoot
        0x055F5Cu, // MS_RockAsteroidRoughLarge05_NoLoot
        0x055F5Du, // MS_RockAsteroidRoughLarge06_NoLoot
        // XLarge
        0x055F61u, // MS_RockAsteroidRoughXLarge01_NoLoot
        0x055F62u, // MS_RockAsteroidRoughXLarge02_NoLoot
        0x055F63u, // MS_RockAsteroidRoughXLarge03_NoLoot
        // XXLarge
        0x055F64u, // MS_RockAsteroidRoughXXLarge01_NoLoot
        0x055F65u, // MS_RockAsteroidRoughXXLarge02_NoLoot
        0x055F66u, // MS_RockAsteroidRoughXXLarge03_NoLoot
    };

    // ── Icy — MS_IceAsteroid* ─────────────────────────────────────────────────────

    private static readonly uint[] IcyIds =
    {
        // Small
        0x1E176Du, // MS_IceAsteroidSmall01
        0x1E176Cu, // MS_IceAsteroidSmall02
        0x1E176Bu, // MS_IceAsteroidSmall03
        0x1E176Au, // MS_IceAsteroidSmall04
        0x1E1769u, // MS_IceAsteroidSmall05
        0x1E1768u, // MS_IceAsteroidSmall06
        // Medium
        0x1E1772u, // MS_IceAsteroidMedium01
        0x1E1771u, // MS_IceAsteroidMedium02
        0x1E1770u, // MS_IceAsteroidMedium03
        0x1E176Fu, // MS_IceAsteroidMedium04
        0x1E176Eu, // MS_IceAsteroidMedium05
        // Boulder (Medium-class)
        0x1E177Eu, // MS_IceAsteroidRoughBoulder01
        0x1E177Du, // MS_IceAsteroidRoughBoulder02
        0x1E177Cu, // MS_IceAsteroidRoughBoulder03
        0x1E177Bu, // MS_IceAsteroidRoughBoulder04
        0x1E177Au, // MS_IceAsteroidRoughBoulder05
        0x1E1779u, // MS_IceAsteroidRoughBoulder06
        // Large
        0x1E1778u, // MS_IceAsteroidLarge01
        0x1E1777u, // MS_IceAsteroidLarge02
        0x1E1776u, // MS_IceAsteroidLarge03
        0x1E1775u, // MS_IceAsteroidLarge04
        0x1E1774u, // MS_IceAsteroidLarge05
        0x1E1773u, // MS_IceAsteroidLarge06
        // XLarge
        0x1E1767u, // MS_IceAsteroidXLarge01
        0x1E1766u, // MS_IceAsteroidXLarge02
        0x1E1765u, // MS_IceAsteroidXLarge03
        // XXLarge
        0x1E1764u, // MS_IceAsteroidXXLarge01
        0x1E1763u, // MS_IceAsteroidXXLarge02
        0x1E1762u, // MS_IceAsteroidXXLarge03
    };

    // ── IceShards — IceShardHuge* (XXLarge) + IceBerg* (XLarge) ─────────────────────

    private static readonly uint[] IceShardsIds =
    {
        // XXLarge — IceShardHuge*
        0x0304A5u, // IceShardHuge01
        0x0304A7u, // IceShardHuge02
        0x0304A9u, // IceShardHuge03
        // XLarge — IceBerg*
        0x03047Fu, // IceBerg01
        0x030481u, // IceBerg02
        0x030483u, // IceBerg03
    };

    // Per-palette base scale applied to all placements.
    // Adjust if a palette's meshes are systematically larger or smaller than expected.
    private static readonly float[] Scales = { 1.0f, 1.0f, 20.0f }; // Rocky, Icy, IceShards

    /// <summary>
    /// Returns the base scale multiplier for the given palette.
    /// All asteroid placement scales are multiplied by this before ±15% SizeNoise is applied.
    /// </summary>
    public static float GetScale(SpaceCellPalette palette)
        => Scales[(int)palette];

    /// <summary>
    /// Returns the asteroid FormKey list for the given palette theme.
    /// </summary>
    public static List<FormKey> GetFormKeys(SpaceCellPalette palette, ModKey sfKey)
    {
        uint[] ids = palette switch
        {
            SpaceCellPalette.Icy       => IcyIds,
            SpaceCellPalette.IceShards => IceShardsIds,
            _                          => RockyIds,
        };
        var result = new List<FormKey>(ids.Length);
        foreach (var id in ids)
            result.Add(new FormKey(sfKey, id));
        return result;
    }
}
