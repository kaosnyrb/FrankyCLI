using Retrograde.Passes.Worldspace;
using Retrograde.Utils;
using Retrograde.WorldspaceDesigns;
using Mutagen.Bethesda.Starfield;
using System;
using System.Diagnostics;

namespace Retrograde.Generator;

public class WorldspaceDungeonGenerator
{
    private readonly IWorldspaceDesign _design;

    public WorldspaceDungeonGenerator(IWorldspaceDesign design)
    {
        _design = design;
    }

    public WorldspaceState Generate(Worldspace worldspace, Location location, int seed,
        float terrainHeight = 0, BtdFile? btd = null, string? btdPath = null)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Derive map size from the editable BTD cell count when available so the
        // tile grid exactly covers the usable worldspace area regardless of BTD grid size.
        // editableCells = (CellMax - CellMin - 1) strips one edge cell on each side.
        // For a 4×4 BTD (editableCells=2): 2 × 100 / 4 = 50  (same as the old hardcoded default).
        // For a 5×5 BTD (editableCells=3): 3 × 100 / 4 = 75.
        int mapSize = btd != null
            ? (int)Math.Round((btd.CellMaxX - btd.CellMinX - 1) * 100f / _design.TileWorldSize)
            : _design.MapSize;

        var state = new WorldspaceState(worldspace, location, mapSize)
        {
            Seed = seed,
            Rng = new Random(seed),
            TileWorldSize = _design.TileWorldSize,
            DesignName = _design.DesignName,
            TerrainHeight = terrainHeight,
            BtdFile = btd,
            BtdPath = btdPath,
        };

        // Phase 1: Map passes (build the 2D tile layout)
        foreach (var pass in _design.MapPasses)
        {
            pass.RunPass(state);
        }

        // Build cell lookup for cross-boundary placement
        foreach (var sbc in worldspace.SubCells)
        {
            var cell = sbc.Items[0].Items[0];
            state.CellLookup[cell.Grid.Point] = cell;
        }

        // Phase 2: Cell build passes (per quadrant)
        foreach (var sbc in worldspace.SubCells)
        {
            var point = sbc.Items[0].Items[0].Grid.Point;
            state.CurrentCellPos = point;
            state.CurrentCell = sbc.Items[0].Items[0];

            foreach (var pass in _design.CellBuildPasses)
            {
                pass.RunPass(state);
            }
        }

        // Phase 3: Content passes (global)
        foreach (var pass in _design.ContentPasses)
        {
            pass.RunPass(state);
        }

        state.PlacementUtil.Finalise();

        stopwatch.Stop();
        if (!RetrogradeContext.Quiet)
        {
            Console.WriteLine("Worldspace Generation Time: " + stopwatch.Elapsed);
        }

        return state;
    }
}
