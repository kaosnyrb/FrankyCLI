using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Models;
using Retrograde.Utils;
using System;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Content pass that scatters rock biome markers across the terrain.
///
/// Stage 1 — Large rocks: scanned in 2×2 tile blocks (stride 2). Each valid block must
/// have all 4 tiles empty AND a 1-tile Chebyshev buffer free of any fort content.
/// Matching blocks receive 1–2 large rocks centred on the area; all 4 tiles are tagged
/// occupied so vegetation does not overlap.
///
/// Stage 2 — Small/medium rocks: per-tile scatter on remaining empty tiles, biased toward
/// sloped terrain. Rocks are pitched/rolled to follow the hillside.
///
/// Must run before <see cref="VegetationScatterPass"/>.
/// </summary>
public class RockScatterPass : IWorldspacePass
{
    // --- Large rock config ---
    /// <summary>Spawn chance per eligible 2×2 block (stride-2 scan).</summary>
    private const float LargeClusterChance   = 0.30f;
    private const int   LargeMinObjects      = 1;
    private const int   LargeMaxObjects      = 2;
    /// <summary>Max scatter offset (overlay units) from the 2×2 block centre per large rock.</summary>
    private const float LargeScatterRadius   = 1.0f;
    private const float LargeSinkMin         = 0.10f;
    private const float LargeSinkMax         = 0.60f;

    // --- Small/medium rock config ---
    /// <summary>Spawn chance per empty tile on flat terrain.</summary>
    private const float FlatClusterChance    = 0.15f;
    /// <summary>Spawn chance per empty tile on sloped terrain.</summary>
    private const float SlopeClusterChance   = 0.45f;
    /// <summary>
    /// Steepness threshold (overlay height units per overlay horizontal unit) above
    /// which a tile is treated as sloped. ~14° at value 0.25.
    /// </summary>
    private const float SlopeThreshold       = 0.25f;
    private const int   SmallMedMinObjects   = 1;
    private const int   SmallMedMaxObjects   = 3;
    private const float SmallMedScatterRadius = 1.5f;

    /// <summary>Maximum slope tilt applied to all rocks, in radians (~30°).</summary>
    private const float MaxTiltAngle = 0.5236f;

    // --- Form IDs (Starfield.esm) ---
    private static readonly uint SmallFormId  = 0x0023CF67; // BiomeMarker_PlanetRockSmall  [BMMO:0023CF67]
    private static readonly uint MediumFormId = 0x0023CF68; // BiomeMarker_PlanetRockMedium [BMMO:0023CF68]
    private static readonly uint LargeFormId  = 0x0023CF69; // BiomeMarker_PlanetRockLarge  [BMMO:0023CF69]

    // Small/medium weight thresholds (renormalised without Large): Small ~59%, Medium ~41%
    private const float SmallMedThreshold = 0.588f;

    private const float OverlayToBtd = 4096f / 100f;

    /// <summary>Prefab tag added to a tile's list to mark it as occupied by rocks.</summary>
    private const string RockTileTag = "rock";

    private readonly float _density;

    /// <param name="density">
    /// Multiplier applied to all spawn chances. 1.0 = default density.
    /// Values below 1 thin out rocks; values above 1 increase them (capped at 100% chance).
    /// </param>
    public RockScatterPass(float density = 1.0f)
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

        // Mirror TileInstantiationPass origin logic.
        float originX = state.FlatAreaWorldX.HasValue
            ? state.FlatAreaWorldX.Value - blocksize * (map.xsize / 2f)
            : -94f;
        float originY = state.FlatAreaWorldY.HasValue
            ? state.FlatAreaWorldY.Value + blocksize * (map.ysize / 2f)
            : 94f;

        // Extend the iteration bounds to cover every active cell in CellLookup.
        // Positions outside the tile map have no fort content — treated as empty.
        int gxMin = 0, gxMax = map.xsize - 1;
        int gyMin = 0, gyMax = map.ysize - 1;
        foreach (var pt in state.CellLookup.Keys)
        {
            int txMin = (int)Math.Floor(( pt.X      * 100f - originX) / blocksize);
            int txMax = (int)Math.Ceiling(((pt.X + 1) * 100f - originX) / blocksize) - 1;
            int tyMin = (int)Math.Floor((originY - (pt.Y + 1) * 100f) / blocksize);
            int tyMax = (int)Math.Ceiling((originY -  pt.Y      * 100f) / blocksize) - 1;
            if (txMin < gxMin) gxMin = txMin;
            if (txMax > gxMax) gxMax = txMax;
            if (tyMin < gyMin) gyMin = tyMin;
            if (tyMax > gyMax) gyMax = tyMax;
        }

        // Snapshot which tiles have fort content before this pass modifies the map.
        // Used to enforce the clearance buffer around large rocks.
        bool[,] hasFortContent = BuildFortMask(map);

        int totalPlaced = 0;

        // Stage 1: Large rocks — stride-2 scan of non-overlapping 2×2 blocks.
        for (int tx = gxMin; tx <= gxMax - 1; tx += 2)
        {
            for (int ty = gyMin; ty <= gyMax - 1; ty += 2)
            {
                if (!IsLargeRockAreaValid(map, hasFortContent, tx, ty)) continue;
                if (rand.NextDouble() > LargeClusterChance * _density) continue;

                // Tag the 4 tiles occupied (only within tile map bounds).
                for (int dx = 0; dx <= 1; dx++)
                    for (int dy = 0; dy <= 1; dy++)
                    {
                        int bx = tx + dx, by = ty + dy;
                        if (bx >= 0 && bx < map.xsize && by >= 0 && by < map.ysize)
                            map.tiles[bx][by].prefabs.Add(RockTileTag);
                    }

                // Centre of the 2×2 block in overlay world space.
                float centerX = originX + blocksize * tx + blocksize * 0.5f;
                float centerY = originY - blocksize * ty - blocksize * 0.5f;

                var slope = btd != null ? ComputeSlope(btd, centerX, centerY, blocksize * 2) : default;

                int cellX = (int)Math.Floor(centerX / 100f);
                int cellY = (int)Math.Floor(centerY / 100f);
                if (!state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var cell))
                    continue;

                float terrainZ = SampleTerrainZ(btd, state, centerX, centerY);

                int count = rand.Next(LargeMinObjects, LargeMaxObjects + 1);
                for (int i = 0; i < count; i++)
                {
                    float ox        = (float)(rand.NextDouble() * 2 - 1) * LargeScatterRadius;
                    float oy        = (float)(rand.NextDouble() * 2 - 1) * LargeScatterRadius;
                    float yaw       = (float)(rand.NextDouble() * MathF.PI * 2f);
                    float sinkDepth = LargeSinkMin + (float)rand.NextDouble() * (LargeSinkMax - LargeSinkMin);
                    float pitch     = Math.Clamp(-MathF.Atan(slope.GradX), -MaxTiltAngle, MaxTiltAngle);
                    float roll      = Math.Clamp( MathF.Atan(slope.GradY), -MaxTiltAngle, MaxTiltAngle);

                    var placed = new PlacedObject(targetMod)
                    {
                        Base     = new FormKey(sfEsm, LargeFormId).ToNullableLink<IPlaceableObjectGetter>(),
                        Position = new P3Float(centerX + ox, centerY + oy, terrainZ - sinkDepth),
                        Rotation = new P3Float(pitch, roll, yaw),
                    };

                    state.PlacementUtil.AddToTemporary(cell, placed);
                    totalPlaced++;
                }
            }
        }

        // Stage 2: Small/medium rocks — per-tile scatter on remaining empty tiles.
        for (int tx = gxMin; tx <= gxMax; tx++)
        {
            for (int ty = gyMin; ty <= gyMax; ty++)
            {
                bool inMap = tx >= 0 && tx < map.xsize && ty >= 0 && ty < map.ysize;
                if (inMap && map.tiles[tx][ty].prefabs.Count > 0) continue;
                if (HasAdjacentFortContent(hasFortContent, tx, ty, map.xsize, map.ysize)) continue;

                float worldX = originX + blocksize * tx;
                float worldY = originY - blocksize * ty;

                var slope = btd != null ? ComputeSlope(btd, worldX, worldY, blocksize) : default;

                bool  isSteep = slope.Steepness > SlopeThreshold;
                float chance  = (isSteep ? SlopeClusterChance : FlatClusterChance) * _density;
                if (rand.NextDouble() > chance) continue;

                if (inMap) map.tiles[tx][ty].prefabs.Add(RockTileTag);

                int cellX = (int)Math.Floor(worldX / 100f);
                int cellY = (int)Math.Floor(worldY / 100f);
                if (!state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var cell))
                    continue;

                float terrainZ = SampleTerrainZ(btd, state, worldX, worldY);

                int count = rand.Next(SmallMedMinObjects, SmallMedMaxObjects + 1);
                for (int i = 0; i < count; i++)
                {
                    float ox    = (float)(rand.NextDouble() * 2 - 1) * SmallMedScatterRadius;
                    float oy    = (float)(rand.NextDouble() * 2 - 1) * SmallMedScatterRadius;
                    float yaw   = (float)(rand.NextDouble() * MathF.PI * 2f);
                    float pitch = Math.Clamp(-MathF.Atan(slope.GradX), -MaxTiltAngle, MaxTiltAngle);
                    float roll  = Math.Clamp( MathF.Atan(slope.GradY), -MaxTiltAngle, MaxTiltAngle);

                    var placed = new PlacedObject(targetMod)
                    {
                        Base     = new FormKey(sfEsm, PickSmallOrMediumFormId(rand)).ToNullableLink<IPlaceableObjectGetter>(),
                        Position = new P3Float(worldX + ox, worldY + oy, terrainZ),
                        Rotation = new P3Float(pitch, roll, yaw),
                    };

                    state.PlacementUtil.AddToTemporary(cell, placed);
                    totalPlaced++;
                }
            }
        }

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[RockScatterPass] Placed {totalPlaced} rock markers on terrain tiles");
    }

    /// <summary>
    /// Returns true if any of the 8 Chebyshev neighbours of (tx, ty) had fort content
    /// at the start of this pass (uses the pre-rock snapshot).
    /// </summary>
    private static bool HasAdjacentFortContent(bool[,] hasFortContent, int tx, int ty, int xsize, int ysize)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = tx + dx, ny = ty + dy;
                if (nx < 0 || nx >= xsize || ny < 0 || ny >= ysize) continue;
                if (hasFortContent[nx, ny]) return true;
            }
        return false;
    }

    /// <summary>
    /// Snapshots which tiles have fort content at the start of this pass.
    /// This lets the large-rock clearance check distinguish fort tiles from
    /// tiles that this pass itself tagged with rocks.
    /// </summary>
    private static bool[,] BuildFortMask(GenerationMap map)
    {
        var mask = new bool[map.xsize, map.ysize];
        for (int tx = 0; tx < map.xsize; tx++)
            for (int ty = 0; ty < map.ysize; ty++)
                mask[tx, ty] = map.tiles[tx][ty].prefabs.Count > 0;
        return mask;
    }

    /// <summary>
    /// Returns true if the 2×2 block anchored at (tx, ty) is valid for a large rock:
    /// all 4 tiles are empty AND a 1-tile Chebyshev border around the block has no fort content.
    /// Tile indices outside the tile map are treated as empty (no fort content).
    /// </summary>
    private static bool IsLargeRockAreaValid(GenerationMap map, bool[,] hasFortContent, int tx, int ty)
    {
        // All 4 interior tiles must be empty (OOB counts as empty).
        for (int dx = 0; dx <= 1; dx++)
            for (int dy = 0; dy <= 1; dy++)
            {
                int bx = tx + dx, by = ty + dy;
                if (bx < 0 || bx >= map.xsize || by < 0 || by >= map.ysize) continue;
                if (map.tiles[bx][by].prefabs.Count > 0) return false;
            }

        // 1-tile Chebyshev border must have no fort content (OOB = no fort content).
        for (int bx = tx - 1; bx <= tx + 2; bx++)
        {
            for (int by = ty - 1; by <= ty + 2; by++)
            {
                if (bx >= tx && bx <= tx + 1 && by >= ty && by <= ty + 1) continue; // inner 2×2
                if (bx < 0 || bx >= map.xsize || by < 0 || by >= map.ysize)  continue; // OOB = safe
                if (hasFortContent[bx, by]) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Slope gradients computed from four tile corners via central differences.
    /// GradX/GradY are height change per overlay unit in the X/Y world directions.
    /// Steepness is the magnitude of the gradient vector.
    /// </summary>
    private readonly struct SlopeInfo
    {
        public readonly float GradX;
        public readonly float GradY;
        public readonly float Steepness;

        public SlopeInfo(float gradX, float gradY)
        {
            GradX     = gradX;
            GradY     = gradY;
            Steepness = MathF.Sqrt(gradX * gradX + gradY * gradY);
        }
    }

    private static SlopeInfo ComputeSlope(BtdFile btd, float worldX, float worldY, int sampleWidth)
    {
        float half = sampleWidth * 0.5f;
        float hNW = btd.SampleHeightAtWorld((worldX - half) * OverlayToBtd, (worldY + half) * OverlayToBtd) / 8f;
        float hNE = btd.SampleHeightAtWorld((worldX + half) * OverlayToBtd, (worldY + half) * OverlayToBtd) / 8f;
        float hSW = btd.SampleHeightAtWorld((worldX - half) * OverlayToBtd, (worldY - half) * OverlayToBtd) / 8f;
        float hSE = btd.SampleHeightAtWorld((worldX + half) * OverlayToBtd, (worldY - half) * OverlayToBtd) / 8f;

        float gradX = ((hNE + hSE) - (hNW + hSW)) / (2f * sampleWidth);
        float gradY = ((hNW + hNE) - (hSW + hSE)) / (2f * sampleWidth);

        return new SlopeInfo(gradX, gradY);
    }

    private static float SampleTerrainZ(BtdFile? btd, WorldspaceState state, float worldX, float worldY)
    {
        if (btd == null) return state.TerrainHeight;
        return btd.SampleHeightAtWorld(worldX * OverlayToBtd, worldY * OverlayToBtd) / 8f;
    }

    /// <summary>Picks Small (~59%) or Medium (~41%), renormalised from original 50/35 weights.</summary>
    private static uint PickSmallOrMediumFormId(Random rand)
    {
        return rand.NextDouble() < SmallMedThreshold ? SmallFormId : MediumFormId;
    }
}
