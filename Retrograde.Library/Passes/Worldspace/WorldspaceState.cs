using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using SfWorldspace = Mutagen.Bethesda.Starfield.Worldspace;
using Noggog;
using Retrograde.Models;
using System;
using System.Collections.Generic;

namespace Retrograde.Passes.Worldspace;

public class WorldspaceState
{
    public WorldspaceState(SfWorldspace worldspace, Location location, int mapSize)
    {
        Worldspace = worldspace;
        Location = location;
        Map = new GenerationMap(mapSize, mapSize);
        PackInLibrary = new Dictionary<string, List<FormKey>>();
        PlacementUtil = new WorldspacePlacementUtil();
        PlacementUtil.SetTopCell(worldspace.TopCell);
    }

    public SfWorldspace Worldspace;
    public Location Location;
    public GenerationMap Map;
    public Dictionary<string, List<FormKey>> PackInLibrary;
    public WorldspacePlacementUtil PlacementUtil;

    /// <summary>
    /// The current cell quadrant being built. Set by the orchestrator
    /// before running CellBuildPasses.
    /// </summary>
    public P2Int CurrentCellPos;
    public Cell CurrentCell;

    public int Seed;
    public Random Rng;
    public string DesignName;

    /// <summary>
    /// World units per tile (blocksize in StarTiller).
    /// </summary>
    public float TileWorldSize = 4f;

    public string Faction = "Spacer";
    public bool IsHarnessRun;

    /// <summary>
    /// Base terrain height sampled from the BTD file at worldspace center.
    /// Used by TileInstantiationPass to place tiles at the correct elevation.
    /// </summary>
    public float TerrainHeight;

    /// <summary>
    /// Lookup from cell grid point to Cell. Built by the generator so that
    /// passes can route objects to the correct cell based on world position.
    /// </summary>
    public Dictionary<P2Int, Cell> CellLookup = new();
}
