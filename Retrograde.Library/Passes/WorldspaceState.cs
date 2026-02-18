using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Models;
using System;
using System.Collections.Generic;

namespace Retrograde.Passes;

public class WorldspaceState
{
    public WorldspaceState(Worldspace worldspace, Location location, int mapSize)
    {
        Worldspace = worldspace;
        Location = location;
        Map = new GenerationMap(mapSize, mapSize);
        PackInLibrary = new Dictionary<string, List<FormKey>>();
        PlacementUtil = new WorldspacePlacementUtil();
        PlacementUtil.SetTopCell(worldspace.TopCell);
    }

    public Worldspace Worldspace;
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
}
