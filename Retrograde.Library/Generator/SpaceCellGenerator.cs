using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde.Passes.SpaceCell;
using System;
using System.Collections.Generic;

namespace Retrograde.Generator;

/// <summary>
/// Orchestrates space cell generation passes.
/// Modelled on StationDungeonGenerator — builds a SpaceCellState and runs
/// each registered ISpaceCellPass against it in order.
/// </summary>
public class SpaceCellGenerator
{
    private readonly List<ISpaceCellPass> _passes;

    public SpaceCellGenerator()
    {
        _passes = new List<ISpaceCellPass>
        {
            new SpaceMarkersPass(),
            new AsteroidChainPass(),
            new CometTailPass(),
            new ShipWreckPass(),
            new LargeAsteroidRingPass(),
            new CrescentBeltPass(),
        };
    }

    public SpaceCellState Generate(Cell cell, Location location,
                                   List<FormKey> asteroidPalette,
                                   List<PlacedObject> markerTemplates,
                                   float vanillaRadius,
                                   float asteroidScale = 1.0f)
    {
        var state = new SpaceCellState
        {
            Cell            = cell,
            Location        = location,
            AsteroidPalette = asteroidPalette,
            MarkerTemplates = markerTemplates,
            VanillaRadius   = vanillaRadius,
            Scale           = MathF.Sqrt(2f) * 0.5f,  // ~0.7× vanilla radius
            AsteroidScale   = asteroidScale,
        };

        foreach (var pass in _passes)
        {
            Console.WriteLine($"[SpaceCellGenerator] Running {pass.GetType().Name}");
            pass.RunPass(state);
        }

        return state;
    }
}
