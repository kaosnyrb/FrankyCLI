using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Models;
using Retrograde.Utils;
using System;
using System.Collections.Generic;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Content pass that carves a natural pond/lake into the terrain and fills it with water tiles.
///
/// Algorithm:
///   1. Finds a flat, unused area (3–7 tiles wide) with no adjacent fort content.
///   2. Carves a bowl-shaped depression into the BTD terrain (smooth bowl, configurable depth).
///   3. Covers the depression with a tight grid of Water16mBiomeWaterSmooth tiles placed 1 unit
///      below the original rim height, so tile edges are underground at the pond boundary.
///   4. Tags all pond tiles as occupied so <see cref="RockScatterPass"/> and
///      <see cref="VegetationScatterPass"/> leave a buffer ring around the water.
///   5. Places rock biome markers in/around the pond and sparse flora around the rim.
///
/// Must run before <see cref="RockScatterPass"/>. Requires a loaded BTD file.
/// </summary>
public class PondPass : IWorldspacePass
{
    // --- Pond geometry ---
    private const float DefaultDepth       = 5.0f;   // overlay units
    private const float WaterSurfaceOffset = -1.0f;  // below rim (overlay units)
    private const int   MinPondTiles       = 1;
    private const int   MaxPondTiles       = 7;
    /// <summary>Max allowed height variation (overlay units) across the candidate area.</summary>
    private const float FlatnessThreshold  = 4.0f;
    /// <summary>
    /// Minimum tile distance from the map edge for the pond's nearest border.
    /// TerrainRestorePass blends ~36 vertices (≈28 overlay units ≈ 7 tiles at size 4) outward
    /// from the fort boundary; 8 tiles keeps the entire pond outside that zone.
    /// </summary>
    private const int EdgeBuffer = 8;
    /// <summary>
    /// Minimum tile clearance between the pond's nearest border and any tile with fort content.
    /// Keeps the pond outside TerrainRestorePass's hermite-blend zone around the fort footprint.
    /// </summary>
    private const int FortBuffer = 7;

    // --- Water tile ---
    private const float WaterTileHalfSize  = 8.0f;   // tile half-extent in overlay units
    private const float WaterTileSize      = WaterTileHalfSize * 2f;
    /// <summary>Water16mBiomeWaterSmooth [ACTI:0005E73C]</summary>
    private const uint  WaterActivatorFormId = 0x0005E73C;

    // --- Decoration form IDs (Starfield.esm) ---
    private static readonly uint SmallRockFormId  = 0x0023CF67; // BiomeMarker_PlanetRockSmall
    private static readonly uint MediumRockFormId = 0x0023CF68; // BiomeMarker_PlanetRockMedium
    private static readonly uint ShrubFormId      = 0x0023CF65; // BiomeMarker_PlanetShrubPrimary
    private static readonly uint FloraFormId      = 0x0026BA65; // BiomeMarker_PlanetFlora

    private const string PondTileTag  = "pond";
    private const float  OverlayToBtd = 4096f / 100f;

    private readonly float _depth;

    /// <param name="depth">Pond depth in overlay units. Default 5.</param>
    public PondPass(float depth = DefaultDepth)
    {
        _depth = depth;
    }

    public void RunPass(WorldspaceState state)
    {
        var btd = state.BtdFile;
        if (btd == null) return;

        var rand      = state.Rng;
        var map       = state.Map;
        int blocksize = (int)state.TileWorldSize;

        var (originX, originY) = state.GetTileOrigin(blocksize);

        // Try largest pond sizes first; fall back to smaller if no valid location exists.
        for (int pondTiles = MaxPondTiles; pondTiles >= MinPondTiles; pondTiles--)
        {
            var candidates = FindCandidates(map, btd, originX, originY, blocksize, pondTiles);
            if (candidates.Count == 0) continue;

            var (tx, ty) = candidates[rand.Next(candidates.Count)];

            float halfSize    = pondTiles * blocksize * 0.5f;
            float pondCenterX = originX + blocksize * tx + halfSize;
            float pondCenterY = originY - blocksize * ty - halfSize;
            float pondRadius  = halfSize; // circular radius == square half-side

            float rimZ   = SampleRimHeight(btd, pondCenterX, pondCenterY, pondRadius);
            float waterZ = rimZ + WaterSurfaceOffset;

            DigPond(btd, pondCenterX, pondCenterY, pondRadius, _depth);
            // Raise any rim vertices that have dipped below the water surface, guaranteeing
            // a solid lip of terrain above waterZ just outside the pond boundary.
            EnsureRim(btd, pondCenterX, pondCenterY, pondRadius, waterZ);

            // Tag tiles so subsequent scatter passes respect the pond boundary.
            for (int dx = 0; dx < pondTiles; dx++)
                for (int dy = 0; dy < pondTiles; dy++)
                    map.tiles[tx + dx][ty + dy].prefabs.Add(PondTileTag);

            // Two complementary tile grids ensure full pond coverage for every valid radius:
            //   Grid A (0,0 offset)             – centre tile covers small ponds (R ≤ T/2)
            //   Grid B (T/2, T/2 = 8,8 offset)  – four quarter tiles at cx±8,cy±8 cover
            //                                      the full area for larger ponds (R > T/2)
            // Together they cover the complete pond circle for R in [1, MaxPondTiles×blocksize/2].
            // Retry with a progressively lower water level only if no tiles are placed at all
            // (e.g. the terrain inside the bowl was not dug deep enough).
            const float waterLowerStep = 0.5f;
            const int   maxWaterRetries = 5;
            int waterPlaced = 0;
            for (int attempt = 0; attempt <= maxWaterRetries && waterPlaced == 0; attempt++)
            {
                waterPlaced  = PlaceWaterSurface(state, pondCenterX, pondCenterY, pondRadius, waterZ, 0f, 0f);
                waterPlaced += PlaceWaterSurface(state, pondCenterX, pondCenterY, pondRadius, waterZ, WaterTileHalfSize, WaterTileHalfSize);

                if (waterPlaced == 0 && attempt < maxWaterRetries)
                    waterZ -= waterLowerStep;
            }

            PlaceDecorations(state, rand, pondCenterX, pondCenterY, pondRadius, rimZ);

            if (!RetrogradeContext.Quiet)
                Console.WriteLine($"[PondPass] Created {pondTiles}×{pondTiles}-tile pond at " +
                                  $"({pondCenterX:F1}, {pondCenterY:F1}), depth={_depth} overlay units, " +
                                  $"waterZ={waterZ:F2}, tiles={waterPlaced}");
            return;
        }

        if (!RetrogradeContext.Quiet)
            Console.WriteLine("[PondPass] No valid pond location found — skipping");
    }

    // ─── Candidate search ─────────────────────────────────────────────────────────

    private static List<(int tx, int ty)> FindCandidates(
        GenerationMap map, BtdFile btd,
        float originX, float originY, int blocksize, int pondTiles)
    {
        var candidates = new List<(int, int)>();

        // Keep EdgeBuffer tiles from each map edge so the pond stays outside the
        // TerrainRestorePass hermite-blend zone (~28 overlay units / 7 tiles).
        for (int tx = EdgeBuffer; tx <= map.xsize - pondTiles - EdgeBuffer; tx++)
        {
            for (int ty = EdgeBuffer; ty <= map.ysize - pondTiles - EdgeBuffer; ty++)
            {
                if (!BlockEmpty(map, tx, ty, pondTiles)) continue;
                if (!BorderEmpty(map, tx, ty, pondTiles)) continue;

                float cx = originX + blocksize * tx + pondTiles * blocksize * 0.5f;
                float cy = originY - blocksize * ty - pondTiles * blocksize * 0.5f;
                float r  = pondTiles * blocksize * 0.5f;
                if (!IsAreaFlat(btd, cx, cy, r)) continue;

                candidates.Add((tx, ty));
            }
        }

        return candidates;
    }

    private static bool BlockEmpty(GenerationMap map, int tx, int ty, int size)
    {
        for (int dx = 0; dx < size; dx++)
            for (int dy = 0; dy < size; dy++)
                if (map.tiles[tx + dx][ty + dy].prefabs.Count > 0) return false;
        return true;
    }

    private static bool BorderEmpty(GenerationMap map, int tx, int ty, int size)
    {
        for (int bx = tx - FortBuffer; bx < tx + size + FortBuffer; bx++)
        {
            for (int by = ty - FortBuffer; by < ty + size + FortBuffer; by++)
            {
                if (bx >= tx && bx < tx + size && by >= ty && by < ty + size) continue;
                if (bx < 0 || bx >= map.xsize || by < 0 || by >= map.ysize) continue;
                if (map.tiles[bx][by].prefabs.Count > 0) return false;
            }
        }
        return true;
    }

    private static bool IsAreaFlat(BtdFile btd, float cx, float cy, float radius)
    {
        float min = float.MaxValue, max = float.MinValue;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                float h = btd.SampleHeightAtWorld(
                    (cx + dx * radius * 0.7f) * OverlayToBtd,
                    (cy + dy * radius * 0.7f) * OverlayToBtd) / 8f;
                if (h < min) min = h;
                if (h > max) max = h;
            }
        }
        return (max - min) <= FlatnessThreshold;
    }

    // ─── Terrain depression ───────────────────────────────────────────────────────

    private static float SampleRimHeight(BtdFile btd, float cx, float cy, float radius)
    {
        float total = 0f;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * MathF.PI / 4f;
            total += btd.SampleHeightAtWorld(
                (cx + MathF.Cos(angle) * radius) * OverlayToBtd,
                (cy + MathF.Sin(angle) * radius) * OverlayToBtd) / 8f;
        }
        return total / 8f;
    }

    private static void DigPond(BtdFile btd, float cx, float cy, float radius, float depth)
    {
        int cellMinX = Math.Max(btd.CellMinX + 1, (int)Math.Floor((cx - radius - 2f) / 100f));
        int cellMaxX = Math.Min(btd.CellMaxX - 1, (int)Math.Floor((cx + radius + 2f) / 100f));
        int cellMinY = Math.Max(btd.CellMinY + 1, (int)Math.Floor((cy - radius - 2f) / 100f));
        int cellMaxY = Math.Min(btd.CellMaxY - 1, (int)Math.Floor((cy + radius + 2f) / 100f));

        const float vertSpacing = 100f / BtdFile.CellResolution;
        var buf = new ushort[BtdFile.CellResolution * BtdFile.CellResolution];

        for (int cellX = cellMinX; cellX <= cellMaxX; cellX++)
        {
            for (int cellY = cellMinY; cellY <= cellMaxY; cellY++)
            {
                btd.GetCellHeightMap(buf, cellX, cellY);
                bool modified = false;

                for (int vy = 0; vy < BtdFile.CellResolution; vy++)
                {
                    float vWorldY = cellY * 100f + vy * vertSpacing;
                    float dY      = vWorldY - cy;

                    for (int vx = 0; vx < BtdFile.CellResolution; vx++)
                    {
                        float vWorldX = cellX * 100f + vx * vertSpacing;
                        float dX      = vWorldX - cx;
                        float dist    = MathF.Sqrt(dX * dX + dY * dY) / radius;

                        if (dist >= 1f) continue;

                        // depth is in overlay units; heights are stored 8x-scaled.
                        float d = ComputeDepth(dist, depth) * 8f;
                        if (d <= 0f) continue;

                        int   idx = vy * BtdFile.CellResolution + vx;
                        float h   = btd.RawToHeight(buf[idx]);
                        buf[idx]  = btd.HeightToRaw(h - d);
                        modified  = true;
                    }
                }

                if (modified)
                    btd.SetCellHeightMap(buf, cellX, cellY);
            }
        }
    }

    /// <summary>
    /// Walks a ring of BTD vertices just outside the pond radius (1.0 – 1.3× radius) and
    /// raises any vertex whose terrain has dipped below <paramref name="waterZ"/> to sit
    /// just above it. This guarantees a solid lip of ground at the pond boundary so that
    /// water tiles placed at <paramref name="waterZ"/> are always underground at the rim,
    /// regardless of natural terrain variation.
    /// </summary>
    private static void EnsureRim(BtdFile btd, float cx, float cy, float radius, float waterZ)
    {
        const float rimBand      = 0.75f; // normalised ring width beyond radius
        const float rimClearance = 0.25f; // overlay units terrain must sit above waterZ

        float outerRadius = radius * (1f + rimBand);
        int cellMinX = Math.Max(btd.CellMinX + 1, (int)Math.Floor((cx - outerRadius - 2f) / 100f));
        int cellMaxX = Math.Min(btd.CellMaxX - 1, (int)Math.Floor((cx + outerRadius + 2f) / 100f));
        int cellMinY = Math.Max(btd.CellMinY + 1, (int)Math.Floor((cy - outerRadius - 2f) / 100f));
        int cellMaxY = Math.Min(btd.CellMaxY - 1, (int)Math.Floor((cy + outerRadius + 2f) / 100f));

        // Minimum acceptable 8x-scaled height: one rimClearance above the water surface.
        float minH8x = (waterZ + rimClearance) * 8f;

        const float vertSpacing = 100f / BtdFile.CellResolution;
        var buf = new ushort[BtdFile.CellResolution * BtdFile.CellResolution];

        for (int cellX = cellMinX; cellX <= cellMaxX; cellX++)
        {
            for (int cellY = cellMinY; cellY <= cellMaxY; cellY++)
            {
                btd.GetCellHeightMap(buf, cellX, cellY);
                bool modified = false;

                for (int vy = 0; vy < BtdFile.CellResolution; vy++)
                {
                    float vWorldY = cellY * 100f + vy * vertSpacing;
                    float dY      = vWorldY - cy;

                    for (int vx = 0; vx < BtdFile.CellResolution; vx++)
                    {
                        float vWorldX = cellX * 100f + vx * vertSpacing;
                        float dX      = vWorldX - cx;
                        float dist    = MathF.Sqrt(dX * dX + dY * dY) / radius;

                        if (dist < 1.0f || dist > 1.0f + rimBand) continue;

                        int   idx = vy * BtdFile.CellResolution + vx;
                        float h   = btd.RawToHeight(buf[idx]);
                        if (h < minH8x)
                        {
                            buf[idx]  = btd.HeightToRaw(minH8x);
                            modified  = true;
                        }
                    }
                }

                if (modified)
                    btd.SetCellHeightMap(buf, cellX, cellY);
            }
        }
    }

    /// <summary>
    /// Returns the depression depth (overlay units) at a normalised radial distance [0,1].
    /// Full depth at the centre, tapering to zero at the rim via a smoothstep curve applied
    /// to the outer 45 % of the radius.
    /// </summary>
    private static float ComputeDepth(float normDist, float maxDepth)
    {
        float t      = Math.Clamp((normDist - 0.55f) / 0.45f, 0f, 1f);
        float smooth = t * t * (3f - 2f * t);
        return maxDepth * (1f - smooth);
    }

    // ─── Water surface ────────────────────────────────────────────────────────────

    private static int PlaceWaterSurface(
        WorldspaceState state, float cx, float cy, float radius, float waterZ,
        float gridOffsetX = 0f, float gridOffsetY = 0f)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;
        var sfEsm     = RetrogradeContext.Current.StarfieldModKey;
        var btd       = state.BtdFile; // non-null: RunPass returns early if btd is null

        // Grid of 16m tiles: enough to cover the pond + a half-tile margin on each side.
        // gridOffset shifts the alignment within one tile period to search for valid placements.
        int   nTiles = (int)Math.Ceiling((radius * 2f + WaterTileSize) / WaterTileSize);
        float startX = cx - (nTiles - 1) * WaterTileSize * 0.5f + gridOffsetX;
        float startY = cy - (nTiles - 1) * WaterTileSize * 0.5f + gridOffsetY;
        int   placed = 0;

        for (int ix = 0; ix < nTiles; ix++)
        {
            for (int iy = 0; iy < nTiles; iy++)
            {
                float wx = startX + ix * WaterTileSize;
                float wy = startY + iy * WaterTileSize;

                // Place wherever the water surface is above the terrain at the tile centre.
                // The centre check alone is the correct inclusion test: inside the dug bowl
                // every vertex is below waterZ by design, so a corner check would incorrectly
                // reject all interior tiles.  EnsureRim guarantees the surrounding terrain
                // rises above waterZ, so tile edges at the rim boundary are safely hidden.
                float terrainZ = btd!.SampleHeightAtWorld(wx * OverlayToBtd, wy * OverlayToBtd) / 8f;
                if (waterZ <= terrainZ) continue;

                int cellX = (int)Math.Floor(wx / 100f);
                int cellY = (int)Math.Floor(wy / 100f);
                if (!state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var cell))
                    continue;

                var placedObj = new PlacedObject(targetMod)
                {
                    Base     = new FormKey(sfEsm, WaterActivatorFormId).ToNullableLink<IPlaceableObjectGetter>(),
                    Position = new P3Float(wx, wy, waterZ),
                    Rotation = new P3Float(0f, 0f, 0f),
                };
                state.PlacementUtil.AddToTemporary(cell, placedObj);
                placed++;
            }
        }

        return placed;
    }

    // ─── Decorations ──────────────────────────────────────────────────────────────

    private static void PlaceDecorations(
        WorldspaceState state, Random rand,
        float cx, float cy, float radius, float rimZ)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;
        var sfEsm     = RetrogradeContext.Current.StarfieldModKey;
        var btd       = state.BtdFile;

        // Rocks: scattered in and around the water (0.5 – 1.2× radius), some partially submerged.
        int rockCount = rand.Next(3, 7);
        for (int i = 0; i < rockCount; i++)
        {
            float angle = (float)(rand.NextDouble() * MathF.PI * 2f);
            float dist  = 0.5f + (float)rand.NextDouble() * 0.7f;
            float rx    = cx + MathF.Cos(angle) * radius * dist;
            float ry    = cy + MathF.Sin(angle) * radius * dist;
            float rz    = btd != null
                ? btd.SampleHeightAtWorld(rx * OverlayToBtd, ry * OverlayToBtd) / 8f
                : rimZ;

            int cellX = (int)Math.Floor(rx / 100f);
            int cellY = (int)Math.Floor(ry / 100f);
            if (!state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var cell)) continue;

            uint  formId = rand.NextDouble() < 0.6 ? SmallRockFormId : MediumRockFormId;
            float yaw    = (float)(rand.NextDouble() * MathF.PI * 2f);
            float sink   = 0.05f + (float)rand.NextDouble() * 0.20f;

            var placed = new PlacedObject(targetMod)
            {
                Base     = new FormKey(sfEsm, formId).ToNullableLink<IPlaceableObjectGetter>(),
                Position = new P3Float(rx, ry, rz - sink),
                Rotation = new P3Float(0f, 0f, yaw),
            };
            state.PlacementUtil.AddToTemporary(cell, placed);
        }

        // Plants: sparse ring just outside the pond rim (1.1 – 1.5× radius).
        int plantCount = rand.Next(4, 9);
        for (int i = 0; i < plantCount; i++)
        {
            float angle = (float)(rand.NextDouble() * MathF.PI * 2f);
            float dist  = 1.1f + (float)rand.NextDouble() * 0.4f;
            float px    = cx + MathF.Cos(angle) * radius * dist;
            float py    = cy + MathF.Sin(angle) * radius * dist;
            float pz    = btd != null
                ? btd.SampleHeightAtWorld(px * OverlayToBtd, py * OverlayToBtd) / 8f
                : rimZ;

            int cellX = (int)Math.Floor(px / 100f);
            int cellY = (int)Math.Floor(py / 100f);
            if (!state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var cell)) continue;

            uint  formId = rand.NextDouble() < 0.5 ? ShrubFormId : FloraFormId;
            float yaw    = (float)(rand.NextDouble() * MathF.PI * 2f);

            var placed = new PlacedObject(targetMod)
            {
                Base     = new FormKey(sfEsm, formId).ToNullableLink<IPlaceableObjectGetter>(),
                Position = new P3Float(px, py, pz),
                Rotation = new P3Float(0f, 0f, yaw),
            };
            state.PlacementUtil.AddToTemporary(cell, placed);
        }
    }
}
