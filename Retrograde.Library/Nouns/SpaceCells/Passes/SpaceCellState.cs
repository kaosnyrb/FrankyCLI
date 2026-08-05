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
    /// Non-Static PlacedObjects from the vanilla source cell's Temporary list
    /// (markers, triggers, etc.). SpaceMarkersPass clones these into the new cell.
    /// </summary>
    public List<PlacedObject> MarkerTemplates;

    /// <summary>
    /// Maximum distance (in game units) from origin to the furthest asteroid in the
    /// vanilla source cell. Used as a reference radius for procedural scaling.
    /// </summary>
    public float VanillaRadius;

    /// <summary>
    /// Linear scale multiplier applied to placement distances, as a fraction of VanillaRadius.
    ///
    /// The only writer (SpaceCellGenerator) sets sqrt(2) * 0.5 = <b>0.707</b>, so a generated
    /// cell is currently about 70% of the radius of the vanilla cell it was cloned from —
    /// i.e. TIGHTER than vanilla, not wider.
    ///
    /// This doc previously read "Default sqrt(2) ~= 1.414 gives approximately twice the vanilla
    /// cell area", which described a value nothing sets. Corrected rather than deleted because
    /// the number is the spread dial: raising it above 1.0 is what widens a cell, and 1.414 is
    /// presumably where it started before being halved.
    /// </summary>
    public float Scale;

    /// <summary>
    /// Base scale multiplier for all asteroid mesh placements, sourced from the palette.
    /// Applied on top of per-asteroid ±15% SizeNoise.
    /// </summary>
    public float AsteroidScale;
}
