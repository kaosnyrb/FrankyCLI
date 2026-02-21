using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Models;
using Retrograde.Utils;
using System;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Content pass that scatters biome vegetation (trees, shrubs, harvestable flora)
/// over empty terrain tiles — tiles that have no prefab content from FortLayoutPass.
/// Plants are grouped into small clusters on reasonably flat tiles, with a random yaw.
///
/// Must run after TerrainRestorePass (original terrain heights restored) and
/// FortLayoutPass (map populated). Runs as a ContentPass so it has access to the
/// full CellLookup for cross-cell placement routing.
/// </summary>
public class VegetationScatterPass : IWorldspacePass
{
    // --- Cluster configuration ---
    /// <summary>Probability that any given empty tile spawns a cluster.</summary>
    private const float ClusterChance     = 0.35f;
    private const int   ClusterMinObjects = 2;
    private const int   ClusterMaxObjects = 5;
    /// <summary>Max allowed overlay-unit height delta across a tile's corners to qualify as flat.</summary>
    private const float MaxHeightVariation = 2f;
    /// <summary>Max scatter offset (overlay units) from the tile centre for each cluster object.</summary>
    private const float ScatterRadius      = 1.5f;
    /// <summary>Trees are sunk this many overlay units below terrain so their roots don't float.</summary>
    private const float TreeSinkDepth      = 0.5f;

    // --- Biome marker form IDs (Starfield.esm) ---
    private static readonly uint TreeFormId           = 0x0023CF6A; // BiomeMarker_PlanetTree          [BMMO:0023CF6A]
    private static readonly uint ShrubPrimaryFormId   = 0x0023CF65; // BiomeMarker_PlanetShrubPrimary  [BMMO:0023CF65]
    private static readonly uint ShrubSecondaryFormId = 0x001C090B; // BiomeMarker_PlanetShrubSecondary [BMMO:001C090B]
    private static readonly uint FloraFormId          = 0x0026BA65; // BiomeMarker_PlanetFlora          [BMMO:0026BA65]

    // Cumulative spawn probability thresholds (must sum to 1.0):
    //   Flora:         0–0.05  (sparse harvestables)
    //   Tree:          0.05–0.30
    //   ShrubPrimary:  0.30–0.70
    //   ShrubSecondary: 0.70–1.00
    private const float FloraThreshold        = 0.05f;
    private const float TreeThreshold         = 0.30f;
    private const float ShrubPrimaryThreshold = 0.70f;

    /// <summary>Conversion factor from overlay units to BTD-internal units (4096 BTD per 100 overlay).</summary>
    private const float OverlayToBtd = 4096f / 100f;

    private readonly float _density;

    /// <param name="density">
    /// Multiplier applied to the spawn chance. 1.0 = default density.
    /// Values below 1 thin out vegetation; values above 1 increase it (capped at 100% chance).
    /// </param>
    public VegetationScatterPass(float density = 1.0f)
    {
        _density = density;
    }

    public void RunPass(WorldspaceState state)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;
        var sfEsm     = RetrogradeContext.Current.StarfieldModKey;
        var btd       = state.BtdFile;
        var rand      = state.Rng;
        var map       = state.Map;
        int blocksize = (int)state.TileWorldSize;

        // Mirror TileInstantiationPass origin: centre the tile grid on the flat area.
        float originX = state.FlatAreaWorldX.HasValue
            ? state.FlatAreaWorldX.Value - blocksize * (map.xsize / 2f)
            : -94f;
        float originY = state.FlatAreaWorldY.HasValue
            ? state.FlatAreaWorldY.Value + blocksize * (map.ysize / 2f)
            : 94f;

        int totalPlaced = 0;

        // Stage 1: Walk every empty tile and optionally plant a vegetation cluster.
        for (int tx = 0; tx < map.xsize; tx++)
        {
            for (int ty = 0; ty < map.ysize; ty++)
            {
                // Only scatter on tiles with no existing content.
                if (map.tiles[tx][ty].prefabs.Count > 0) continue;
                if (HasAdjacentContent(map, tx, ty)) continue;

                float worldX = originX + blocksize * tx;
                float worldY = originY - blocksize * ty;

                // Require reasonably flat terrain (skip if no BTD is loaded).
                if (btd != null && !IsTileFlat(btd, worldX, worldY, blocksize))
                    continue;

                if (rand.NextDouble() > ClusterChance * _density) continue;

                // Route to the owning worldspace cell.
                int cellX = (int)Math.Floor(worldX / 100f);
                int cellY = (int)Math.Floor(worldY / 100f);
                if (!state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var cell))
                    continue;

                float terrainZ = SampleTerrainZ(btd, state, worldX, worldY);

                // Stage 2: Scatter individual cluster objects around the tile centre.
                int count = rand.Next(ClusterMinObjects, ClusterMaxObjects + 1);
                for (int i = 0; i < count; i++)
                {
                    float ox     = (float)(rand.NextDouble() * 2 - 1) * ScatterRadius;
                    float oy     = (float)(rand.NextDouble() * 2 - 1) * ScatterRadius;
                    float yaw    = (float)(rand.NextDouble() * MathF.PI * 2f);
                    uint  formId = PickFormId(rand);
                    float oz     = formId == TreeFormId ? -TreeSinkDepth : 0f;

                    var placed = new PlacedObject(targetMod)
                    {
                        Base     = new FormKey(sfEsm, formId).ToNullableLink<IPlaceableObjectGetter>(),
                        Position = new P3Float(worldX + ox, worldY + oy, terrainZ + oz),
                        Rotation = new P3Float(0f, 0f, yaw),
                    };

                    state.PlacementUtil.AddToTemporary(cell, placed);
                    totalPlaced++;
                }
            }
        }

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[VegetationScatterPass] Placed {totalPlaced} biome markers on empty terrain tiles");
    }

    /// <summary>
    /// Returns true if any of the 8 Chebyshev neighbours of (tx, ty) has any prefab content.
    /// </summary>
    private static bool HasAdjacentContent(GenerationMap map, int tx, int ty)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = tx + dx, ny = ty + dy;
                if (nx < 0 || nx >= map.xsize || ny < 0 || ny >= map.ysize) continue;
                if (map.tiles[nx][ny].prefabs.Count > 0) return true;
            }
        return false;
    }

    /// <summary>
    /// Converts overlay X/Y to BTD-internal coordinates, samples the terrain height,
    /// and divides by 8 to obtain the overlay-space Z for object placement.
    /// Falls back to <see cref="WorldspaceState.TerrainHeight"/> when no BTD is loaded.
    /// </summary>
    private static float SampleTerrainZ(BtdFile? btd, WorldspaceState state, float worldX, float worldY)
    {
        if (btd == null) return state.TerrainHeight;
        return btd.SampleHeightAtWorld(worldX * OverlayToBtd, worldY * OverlayToBtd) / 8f;
    }

    /// <summary>
    /// Samples terrain height at the four corners of a tile and returns true if the
    /// difference between min and max is within <see cref="MaxHeightVariation"/>.
    /// </summary>
    private static bool IsTileFlat(BtdFile btd, float worldX, float worldY, int blocksize)
    {
        float half = blocksize * 0.5f;
        float hNW = btd.SampleHeightAtWorld((worldX - half) * OverlayToBtd, (worldY + half) * OverlayToBtd) / 8f;
        float hNE = btd.SampleHeightAtWorld((worldX + half) * OverlayToBtd, (worldY + half) * OverlayToBtd) / 8f;
        float hSW = btd.SampleHeightAtWorld((worldX - half) * OverlayToBtd, (worldY - half) * OverlayToBtd) / 8f;
        float hSE = btd.SampleHeightAtWorld((worldX + half) * OverlayToBtd, (worldY - half) * OverlayToBtd) / 8f;

        float min = MathF.Min(MathF.Min(hNW, hNE), MathF.Min(hSW, hSE));
        float max = MathF.Max(MathF.Max(hNW, hNE), MathF.Max(hSW, hSE));
        return (max - min) <= MaxHeightVariation;
    }

    /// <summary>
    /// Weighted random selection of which biome marker type to place.
    /// Flora is chosen rarely (5 %), trees moderately (25 %), shrubs dominate (70 %).
    /// </summary>
    private static uint PickFormId(Random rand)
    {
        float roll = (float)rand.NextDouble();
        if (roll < FloraThreshold)        return FloraFormId;
        if (roll < TreeThreshold)         return TreeFormId;
        if (roll < ShrubPrimaryThreshold) return ShrubPrimaryFormId;
        return ShrubSecondaryFormId;
    }
}
