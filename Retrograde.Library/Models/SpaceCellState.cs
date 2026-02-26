using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System.Collections.Generic;

/// <summary>
/// Shared mutable state passed between space cell generation passes.
/// </summary>
public class SpaceCellState
{
    /// <summary>The space cell being populated.</summary>
    public Cell Cell;

    /// <summary>Location record attached to the cell.</summary>
    public Location Location;

    /// <summary>
    /// FormKeys of asteroid Static base records extracted from the vanilla source cell.
    /// Passes draw from this palette when placing asteroids.
    /// </summary>
    public List<FormKey> AsteroidPalette;

    /// <summary>
    /// Maximum distance (in game units) from origin to the furthest asteroid in the
    /// vanilla source cell. Used as a reference radius for procedural scaling.
    /// </summary>
    public float VanillaRadius;

    /// <summary>
    /// Linear scale multiplier applied to placement distances.
    /// Default sqrt(2) ≈ 1.414 gives approximately twice the vanilla cell area.
    /// </summary>
    public float Scale;
}
