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
    /// cell area", which described a value nothing sets.
    ///
    /// THE DIAL HAS A HARD CEILING AND IT IS NOT AESTHETIC (owner, 2026-08-05): the engine has a
    /// target-distance limit around 15,000 units, past which enemy ships cannot be seen. Vanilla
    /// space cells already run out to roughly 11-12k, so 1.414 would have placed content near
    /// 16k -- outside the range at which a hostile is visible. The halving to 0.707 therefore
    /// reads as a deliberate correction for that limit rather than a leftover, and any future
    /// widening is bounded by the same wall: keep the outermost placements inside ~15k.
    ///
    /// Consequence for design: a cell gets more interesting by COMPOSITION (separated sites with
    /// negative space between) rather than by radius, because radius is nearly spent.
    /// </summary>
    public float Scale;

    /// <summary>
    /// Base scale multiplier for all asteroid mesh placements, sourced from the palette.
    /// Applied on top of per-asteroid ±15% SizeNoise.
    /// </summary>
    public float AsteroidScale;
}
