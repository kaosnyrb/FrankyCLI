using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using SfWorldspace = Mutagen.Bethesda.Starfield.Worldspace;
using Noggog;
using Retrograde.Models;
using Retrograde.Utils;
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

    /// <summary>
    /// Per-category XY radius in overlay units, derived from PackIn ObjectBounds.
    /// Populated by IndustryPackInLibraryPass. Used by IndustryLayoutPass to
    /// prevent physical overlap between buildings of different sizes.
    /// </summary>
    public Dictionary<string, float> PackInRadii = new();
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

    /// <summary>
    /// Overlay world-space centroid of the placed POI buildings (X, Y in overlay units).
    /// Set by the topology pass (e.g. IndustryLayoutPass) immediately after it writes
    /// buildings to the map. Downstream passes use this to centre scatter, terrain
    /// flattening, and markers on the actual base location.
    /// Null until the topology pass has run.
    /// </summary>
    public (float X, float Y)? PoiCenter;

    /// <summary>
    /// World-space centre of the flat base area chosen by TerrainFlattenPass.
    /// Null until TerrainFlattenPass has run. TileInstantiationPass uses this
    /// to centre the tile grid on the flat area instead of the worldspace origin.
    /// </summary>
    public float? FlatAreaWorldX;
    public float? FlatAreaWorldY;

    /// <summary>
    /// The BTD terrain file for this worldspace, available to terrain passes.
    /// Null if no data folder was provided.
    /// </summary>
    public BtdFile? BtdFile;

    /// <summary>
    /// Full path to the BTD file on disk. Used to save terrain edits after generation.
    /// </summary>
    public string? BtdPath;

    /// <summary>
    /// BTD-level details captured by TerrainFlattenPass before it writes anything.
    /// Used by TerrainRestorePass to put back original terrain outside the fort footprint.
    /// </summary>
    public FlatAreaBtdInfo? FlatAreaBtdData;

    /// <summary>
    /// World-space position for the map marker placed by <see cref="MapMarkerPass"/>.
    /// If null, the marker is placed at the worldspace origin (0, 0) at terrain height.
    /// </summary>
    public P3Float? MarkerPosition;

    /// <summary>
    /// The boss NPC placed in the persistent TopCell by <see cref="WorldspaceBossPass"/>.
    /// Null until that pass has run. Use FormKey to reference in quest/bounty passes.
    /// </summary>
    public PlacedNpc? BossPlacedNpc;

    /// <summary>
    /// FormKey of the LOD-aggregating Layer record created by <see cref="LodLayerPass"/>.
    /// Null until that pass has run.
    /// </summary>
    public FormKey? LodLayer;

    /// <summary>
    /// World positions of every occupied pod in the science building, populated by
    /// <see cref="ScienceBuildingPass"/>. The Z component is the base elevation of the
    /// pods (buildingZ after vertical offset). Null until that pass has run.
    /// </summary>
    public List<P3Float>? BuildingPodPositions;

    /// <summary>
    /// Returns the overlay-space (originX, originY) used by TileInstantiationPass,
    /// IndustryGroundFlattenPass, IndustryPropScatterPass, and RockScatterPass to
    /// map tile grid indices to world positions.
    /// </summary>
    public (float originX, float originY) GetTileOrigin(int blocksize) =>
    (
        FlatAreaWorldX.HasValue ? FlatAreaWorldX.Value - blocksize * (Map.xsize / 2f) : -94f,
        FlatAreaWorldY.HasValue ? FlatAreaWorldY.Value + blocksize * (Map.ysize / 2f) :  94f
    );

    public class FlatAreaBtdInfo
    {
        /// <summary>First editable cell in each axis (absolute BTD cell coordinates).</summary>
        public int EditMinX, EditMinY;
        /// <summary>Top-left corner of the flat area in global vertex space (relative to editMin * CellResolution).</summary>
        public int BestX0, BestY0;
        /// <summary>Flat area side length in vertices.</summary>
        public int AreaVerts;
        /// <summary>
        /// The edge-gap inset (in vertices) that TerrainFlattenPass kept clear around the
        /// boundary of the editable region.  TerrainRestorePass uses this as a hard guard.
        /// </summary>
        public int EdgeGapVerts;
        /// <summary>
        /// Original (pre-flatten) height buffers keyed by absolute (cellX, cellY).
        /// Only covers cells that were in the flatten zone.
        /// </summary>
        public Dictionary<(int cellX, int cellY), ushort[]> OriginalHeights = new();
    }
}
