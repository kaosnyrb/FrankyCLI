using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Utils;
using System;
using System.Collections.Generic;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Defines a rectangular room in pod-grid coordinates.
/// Each unit is one "pod" = 6 overlay game units.
/// X increases east, Y increases north.
/// </summary>
public readonly record struct PodRoom(int X0, int Y0, int Width, int Height);

/// <summary>
/// Content pass that assembles a hydroponic science building from Starfield's modular
/// OutpostHydroponic statics (OphExtPodSm / OphIntPodSm).
///
/// The building is defined as a list of rectangular rooms in pod-grid units. The pass:
///   1. Builds a 2-D occupancy grid from all rooms.
///   2. Scans the BTD for the flattest available area matching the building footprint.
///   3. Flattens the BTD under the footprint with a hermite blend at the boundary.
///   4. Places Ext + Int pod cores, walls, and convex-corner pieces for every pod.
///
/// Wall type (A or B) is chosen randomly per face. The southern face of the entry
/// row uses OphExtPodSmWallBExArch01 / OphIntPodSmWallBExArch01 (arched entrance).
///
/// TODO: Add CorOut (concave corner) pieces at room junctions where two rooms
/// meet at an inside corner. Requires empirical placement-offset verification.
/// </summary>
public class ScienceBuildingPass : IWorldspacePass
{
    // ----- Pod grid constant -----
    private const float PodSize = 6f; // one pod = 6 overlay units

    // ----- Default science-building layout -----
    // Pod-grid visual (j=0 bottom/south, j increases north):
    //   j=3:  [M][M][M][M]
    //   j=2:  [M][M][M][M][E][E]
    //   j=1:  [M][M][M][M]
    //   j=0:     [V][V]
    // M = Main lab (room 0), E = East wing (room 1), V = Vestibule entrance (room 2)
    private static readonly IReadOnlyList<PodRoom> DefaultLayout = new PodRoom[]
    {
        new(0, 1, 4, 3), // Main lab
        new(4, 2, 2, 2), // East wing
        new(1, 0, 2, 1), // Entry vestibule (south)
    };

    // ----- FormIDs (Starfield.esm) -----
    // Exterior
    private const uint ExtMidId        = 0x1F632B; // OphExtPodSmMid01
    private const uint ExtWallAId      = 0x1F631C; // OphExtPodSmWallA01
    private const uint ExtWallAWinId   = 0x1F6362; // OphExtPodSmWallAWin01
    private const uint ExtWallBId      = 0x1F6398; // OphExtPodSmWallB01
    private const uint ExtWallBArchId  = 0x1F6392; // OphExtPodSmWallBExArch01
    private const uint ExtWallBLgId    = 0x1F6391; // OphExtPodSmWallBExLg01
    private const uint ExtCornerAId    = 0x1F638A; // OphExtPodSmWallACorIn01
    private const uint ExtCornerBId    = 0x1F631D; // OphExtPodSmWallBCorIn01
    // Interior
    private const uint IntMidId        = 0x1F6321; // OphIntPodSmMid01
    private const uint IntWallAId      = 0x1F6319; // OphIntPodSmWallA01
    private const uint IntWallAWinId   = 0x1F6364; // OphIntPodSmWallAWin01
    private const uint IntWallBId      = 0x1F6394; // OphIntPodSmWallB01
    private const uint IntWallBArchId  = 0x1F6350; // OphIntPodSmWallBExArch01
    private const uint IntWallBLgId    = 0x1F6352; // OphIntPodSmWallBExLg01
    private const uint IntCornerAId    = 0x1F6388; // OphIntPodSmWallACorIn01
    private const uint IntCornerBId    = 0x1F539C; // OphIntPodSmWallBCorIn01

    private const float OverlayToBtd = 4096f / 100f; // overlay units → BTD-internal units

    private readonly IReadOnlyList<PodRoom> _rooms;
    private readonly float _verticalOffset;

    public ScienceBuildingPass(IReadOnlyList<PodRoom>? rooms = null, float verticalOffset = 0.1f)
    {
        _rooms = rooms ?? DefaultLayout;
        _verticalOffset = verticalOffset;
    }

    public void RunPass(WorldspaceState state)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;
        var sfEsm     = RetrogradeContext.Current.StarfieldModKey;
        var btd       = state.BtdFile;
        var rand      = state.Rng;

        // ── Step 1: Build occupancy grid ──────────────────────────────────────────
        int gridMinX = int.MaxValue, gridMinY = int.MaxValue;
        int gridMaxX = int.MinValue, gridMaxY = int.MinValue;
        foreach (var r in _rooms)
        {
            gridMinX = Math.Min(gridMinX, r.X0);
            gridMinY = Math.Min(gridMinY, r.Y0);
            gridMaxX = Math.Max(gridMaxX, r.X0 + r.Width);
            gridMaxY = Math.Max(gridMaxY, r.Y0 + r.Height);
        }
        int gridW = gridMaxX - gridMinX;
        int gridH = gridMaxY - gridMinY;
        var occupied = new bool[gridW, gridH];

        // Southernmost occupied row (used to identify entrance face)
        int entranceMinJ = gridH; // normalised grid index
        foreach (var r in _rooms)
        {
            int jMin = r.Y0 - gridMinY;
            int jMax = r.Y0 - gridMinY + r.Height;
            for (int di = 0; di < r.Width; di++)
                for (int dj = 0; dj < r.Height; dj++)
                    occupied[r.X0 - gridMinX + di, r.Y0 - gridMinY + dj] = true;
            if (jMin < entranceMinJ) entranceMinJ = jMin;
        }

        // ── Step 2: Find flattest area ─────────────────────────────────────────────
        float footprintOverlay = Math.Max(gridW, gridH) * PodSize + 12f; // 2-pod padding
        float flatX, flatY, buildingZ;

        if (btd != null)
        {
            FindFlatArea(btd, footprintOverlay, out flatX, out flatY, out buildingZ);
        }
        else if (state.FlatAreaWorldX.HasValue)
        {
            flatX = state.FlatAreaWorldX.Value;
            flatY = state.FlatAreaWorldY!.Value;
            buildingZ = state.TerrainHeight;
        }
        else
        {
            flatX = 0f; flatY = 0f;
            buildingZ = state.TerrainHeight;
        }

        // ── Step 3: Flatten BTD under building footprint ───────────────────────────
        if (btd != null)
            FlattenBtdFootprint(btd, flatX, flatY, footprintOverlay, buildingZ, ref buildingZ);

        // Expose building location to subsequent passes (marker, boss, scatter).
        state.TerrainHeight   = buildingZ;
        state.FlatAreaWorldX  = flatX;
        state.FlatAreaWorldY  = flatY;
        state.MarkerPosition  = new P3Float(flatX, flatY, buildingZ + 1f);

        // Apply vertical offset so pieces sit cleanly above the flattened terrain.
        buildingZ += _verticalOffset;

        // Building origin in overlay worldspace: pod (0,0) of normalised grid
        float originX = flatX - (gridW * 0.5f) * PodSize;
        float originY = flatY - (gridH * 0.5f) * PodSize;

        float WorldX(int i) => originX + i * PodSize;
        float WorldY(int j) => originY + j * PodSize;

        // Pick one wall set for the whole building so pieces are visually consistent.
        bool useSetA = rand.NextDouble() < 0.5;

        bool IsOccupied(int i, int j) =>
            i >= 0 && j >= 0 && i < gridW && j < gridH && occupied[i, j];

        // ── Step 4: Place building statics ────────────────────────────────────────
        int totalPlaced = 0;

        for (int i = 0; i < gridW; i++)
        {
            for (int j = 0; j < gridH; j++)
            {
                if (!occupied[i, j]) continue;

                float wx = WorldX(i);
                float wy = WorldY(j);

                int cellX = (int)Math.Floor(wx / 100f);
                int cellY = (int)Math.Floor(wy / 100f);
                if (!state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var cell))
                    continue;

                // Pod core (Ext shell + Int interior)
                Place(targetMod, sfEsm, ExtMidId, wx, wy, buildingZ, 0f, cell, state);
                Place(targetMod, sfEsm, IntMidId, wx, wy, buildingZ, 0f, cell, state);
                totalPlaced += 2;

                // ── Walls on exposed faces ──
                // +Y (north): Z = 0
                if (!IsOccupied(i, j + 1))
                {
                    var (extW, intW) = PickWall(rand, false, useSetA);
                    Place(targetMod, sfEsm, extW, wx, wy, buildingZ, 0f, cell, state);
                    Place(targetMod, sfEsm, intW, wx, wy, buildingZ, 0f, cell, state);
                    totalPlaced += 2;
                }
                // -Y (south): Z = π
                if (!IsOccupied(i, j - 1))
                {
                    bool entrance = (j == entranceMinJ);
                    var (extW, intW) = PickWall(rand, entrance, useSetA);
                    Place(targetMod, sfEsm, extW, wx, wy, buildingZ, MathF.PI, cell, state);
                    Place(targetMod, sfEsm, intW, wx, wy, buildingZ, MathF.PI, cell, state);
                    totalPlaced += 2;
                }
                // +X (east): Z = +π/2
                if (!IsOccupied(i + 1, j))
                {
                    var (extW, intW) = PickWall(rand, false, useSetA);
                    Place(targetMod, sfEsm, extW, wx, wy, buildingZ, MathF.PI / 2f, cell, state);
                    Place(targetMod, sfEsm, intW, wx, wy, buildingZ, MathF.PI / 2f, cell, state);
                    totalPlaced += 2;
                }
                // -X (west): Z = -π/2
                if (!IsOccupied(i - 1, j))
                {
                    var (extW, intW) = PickWall(rand, false, useSetA);
                    Place(targetMod, sfEsm, extW, wx, wy, buildingZ, -MathF.PI / 2f, cell, state);
                    Place(targetMod, sfEsm, intW, wx, wy, buildingZ, -MathF.PI / 2f, cell, state);
                    totalPlaced += 2;
                }

                // ── Convex corners (CorIn): placed when both adjacent faces are exposed ──
                // +X,+Y corner: Z = -π/2
                if (!IsOccupied(i + 1, j) && !IsOccupied(i, j + 1))
                {
                    var (extC, intC) = PickCorner(useSetA);
                    Place(targetMod, sfEsm, extC, wx, wy, buildingZ, -MathF.PI / 2f, cell, state);
                    Place(targetMod, sfEsm, intC, wx, wy, buildingZ, -MathF.PI / 2f, cell, state);
                    totalPlaced += 2;
                }
                // -X,+Y corner: Z = π
                if (!IsOccupied(i - 1, j) && !IsOccupied(i, j + 1))
                {
                    var (extC, intC) = PickCorner(useSetA);
                    Place(targetMod, sfEsm, extC, wx, wy, buildingZ, MathF.PI, cell, state);
                    Place(targetMod, sfEsm, intC, wx, wy, buildingZ, MathF.PI, cell, state);
                    totalPlaced += 2;
                }
                // -X,-Y corner: Z = π/2
                if (!IsOccupied(i - 1, j) && !IsOccupied(i, j - 1))
                {
                    var (extC, intC) = PickCorner(useSetA);
                    Place(targetMod, sfEsm, extC, wx, wy, buildingZ, MathF.PI / 2f, cell, state);
                    Place(targetMod, sfEsm, intC, wx, wy, buildingZ, MathF.PI / 2f, cell, state);
                    totalPlaced += 2;
                }
                // +X,-Y corner: Z = 0
                if (!IsOccupied(i + 1, j) && !IsOccupied(i, j - 1))
                {
                    var (extC, intC) = PickCorner(useSetA);
                    Place(targetMod, sfEsm, extC, wx, wy, buildingZ, 0f, cell, state);
                    Place(targetMod, sfEsm, intC, wx, wy, buildingZ, 0f, cell, state);
                    totalPlaced += 2;
                }
            }
        }

        // ── Step 5: Tag map tiles under the building so scatter passes skip them ──
        // Scatter passes check prefabs.Count > 0 to avoid placing on occupied tiles.
        {
            var map     = state.Map;
            int bs      = (int)state.TileWorldSize; // tile size in overlay units (4)
            float toX   = flatX - bs * (map.xsize / 2f); // tile grid origin X (SW of tile 0,0)
            float toY   = flatY + bs * (map.ysize / 2f); // tile grid origin Y (NW of tile 0,0)

            // Building bounding box in overlay units; include wall depth (~6) + one-tile buffer.
            float margin = 6f + bs;
            float bMinX  = originX - margin;
            float bMaxX  = originX + gridW * PodSize + margin;
            float bMinY  = originY - margin;
            float bMaxY  = originY + gridH * PodSize + margin;

            for (int tx = 0; tx < map.xsize; tx++)
            {
                float tileX = toX + tx * bs;
                if (tileX + bs < bMinX || tileX > bMaxX) continue;
                for (int ty = 0; ty < map.ysize; ty++)
                {
                    float tileY = toY - ty * bs;
                    if (tileY < bMinY || tileY - bs > bMaxY) continue;
                    map.tiles[tx][ty].prefabs.Add("building");
                }
            }
        }

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[ScienceBuildingPass] Placed {totalPlaced} statics at centre " +
                              $"({flatX:F1},{flatY:F1}) Z={buildingZ:F2}  grid={gridW}×{gridH} pods");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────

    private static void Place(
        StarfieldMod targetMod, ModKey sfEsm,
        uint formId, float wx, float wy, float wz, float yawZ,
        Cell cell, WorldspaceState state)
    {
        var placed = new PlacedObject(targetMod)
        {
            Base     = new FormKey(sfEsm, formId).ToNullableLink<IPlaceableObjectGetter>(),
            Position = new P3Float(wx, wy, wz),
            Rotation = new P3Float(0f, 0f, yawZ),
        };
        if (state.LodLayer.HasValue)
            placed.Layer = state.LodLayer.Value.ToNullableLink<ILayerGetter>();
        state.PlacementUtil.AddToTemporary(cell, placed);
    }

    /// <summary>
    /// Picks exterior and interior wall FormIDs for one face.
    /// <paramref name="useSetA"/> is chosen once per building so all faces match.
    /// The entrance always uses the Set B arched variant regardless.
    /// </summary>
    private static (uint ext, uint intr) PickWall(Random rand, bool isEntrance, bool useSetA)
    {
        if (isEntrance) return (ExtWallBArchId, IntWallBArchId);
        if (useSetA)
            // Set A: plain wall most of the time, window occasionally
            return rand.NextDouble() < 0.75
                ? (ExtWallAId,    IntWallAId)
                : (ExtWallAWinId, IntWallAWinId);
        else
            // Set B: plain wall or large-exterior variant
            return rand.NextDouble() < 0.65
                ? (ExtWallBId,   IntWallBId)
                : (ExtWallBLgId, IntWallBLgId);
    }

    /// <summary>Returns the convex-corner piece pair matching the chosen wall set.</summary>
    private static (uint ext, uint intr) PickCorner(bool useSetA) =>
        useSetA ? (ExtCornerAId, IntCornerAId) : (ExtCornerBId, IntCornerBId);

    /// <summary>
    /// Scans the BTD on a coarse grid to find the flattest position
    /// (minimum height range) for the given overlay footprint size.
    /// </summary>
    private static void FindFlatArea(
        BtdFile btd, float footprintOverlay,
        out float bestX, out float bestY, out float bestZ)
    {
        // Editable cell bounds (skip outermost ring)
        float editMinBtdX = (btd.CellMinX + 1) * 4096f;
        float editMinBtdY = (btd.CellMinY + 1) * 4096f;
        float editMaxBtdX = (btd.CellMaxX)      * 4096f; // exclusive
        float editMaxBtdY = (btd.CellMaxY)       * 4096f;

        float halfBtd = footprintOverlay * 0.5f * OverlayToBtd;
        float scanStep = 512f; // ≈12.5 overlay units per step

        float bestRange = float.MaxValue;
        bestX = 0f; bestY = 0f;
        bestZ = btd.SampleHeightAtWorld(0, 0) / 8f;

        for (float cx = editMinBtdX + halfBtd; cx <= editMaxBtdX - halfBtd; cx += scanStep)
        {
            for (float cy = editMinBtdY + halfBtd; cy <= editMaxBtdY - halfBtd; cy += scanStep)
            {
                // Sample centre + 4 footprint corners
                float h0 = btd.SampleHeightAtWorld(cx,           cy);
                float h1 = btd.SampleHeightAtWorld(cx - halfBtd, cy - halfBtd);
                float h2 = btd.SampleHeightAtWorld(cx + halfBtd, cy - halfBtd);
                float h3 = btd.SampleHeightAtWorld(cx - halfBtd, cy + halfBtd);
                float h4 = btd.SampleHeightAtWorld(cx + halfBtd, cy + halfBtd);

                float minH = Math.Min(h0, Math.Min(h1, Math.Min(h2, Math.Min(h3, h4))));
                float maxH = Math.Max(h0, Math.Max(h1, Math.Max(h2, Math.Max(h3, h4))));
                float range = maxH - minH;

                if (range < bestRange)
                {
                    bestRange = range;
                    bestX = cx / OverlayToBtd;
                    bestY = cy / OverlayToBtd;
                    bestZ = (h0 + h1 + h2 + h3 + h4) / 5f / 8f;
                }
            }
        }
    }

    /// <summary>
    /// Writes a flat height to BTD vertices inside the building footprint,
    /// with a hermite blend band at the boundary. Updates <paramref name="buildingZ"/>.
    /// </summary>
    private static void FlattenBtdFootprint(
        BtdFile btd,
        float flatX, float flatY,
        float footprintOverlay,
        float targetZ,          // in PlacedObject / overlay units (already / 8)
        ref float buildingZ)
    {
        float halfOv  = footprintOverlay * 0.5f;
        float halfBtd = halfOv * OverlayToBtd;
        float flatXBtd = flatX * OverlayToBtd;
        float flatYBtd = flatY * OverlayToBtd;

        // BTD height is 8× the overlay PlacedObject height
        float targetHeightBtd = targetZ * 8f;
        ushort targetRaw = btd.HeightToRaw(targetHeightBtd);

        int editMinX = btd.CellMinX + 1;
        int editMinY = btd.CellMinY + 1;
        int editMaxX = btd.CellMaxX - 1;
        int editMaxY = btd.CellMaxY - 1;

        // Cells that intersect the footprint
        int cellX0 = Math.Max(editMinX, (int)Math.Floor((flatXBtd - halfBtd) / 4096f));
        int cellX1 = Math.Min(editMaxX, (int)Math.Floor((flatXBtd + halfBtd) / 4096f));
        int cellY0 = Math.Max(editMinY, (int)Math.Floor((flatYBtd - halfBtd) / 4096f));
        int cellY1 = Math.Min(editMaxY, (int)Math.Floor((flatYBtd + halfBtd) / 4096f));

        const int blendVerts = 8; // ~6.25 overlay units of transition
        float vertSpacing = 4096f / BtdFile.CellResolution; // BTD units per vertex

        var buf = new ushort[BtdFile.CellResolution * BtdFile.CellResolution];

        for (int cy = cellY0; cy <= cellY1; cy++)
        {
            for (int cx = cellX0; cx <= cellX1; cx++)
            {
                btd.GetCellHeightMap(buf, cx, cy);
                bool modified = false;

                for (int vy = 0; vy < BtdFile.CellResolution; vy++)
                {
                    float vWorldY = cy * 4096f + vy * vertSpacing;
                    for (int vx = 0; vx < BtdFile.CellResolution; vx++)
                    {
                        float vWorldX = cx * 4096f + vx * vertSpacing;

                        // Chebyshev distance (in vertices) from this vertex to the footprint box
                        float dx = Math.Max(0f, Math.Abs(vWorldX - flatXBtd) - halfBtd);
                        float dy = Math.Max(0f, Math.Abs(vWorldY - flatYBtd) - halfBtd);
                        float distVerts = Math.Max(dx, dy) / vertSpacing;

                        if (distVerts <= 0f)
                        {
                            buf[vy * BtdFile.CellResolution + vx] = targetRaw;
                            modified = true;
                        }
                        else if (distVerts < blendVerts)
                        {
                            float t = distVerts / blendVerts;
                            float s = t * t * (3f - 2f * t); // hermite: 0 inside → 1 outside
                            ushort original = buf[vy * BtdFile.CellResolution + vx];
                            buf[vy * BtdFile.CellResolution + vx] = BlendRaw(targetRaw, original, s);
                            modified = true;
                        }
                    }
                }

                if (modified)
                    btd.SetCellHeightMap(buf, cx, cy);
            }
        }

        buildingZ = targetZ;
    }

    private static ushort BlendRaw(ushort target, ushort original, float s) =>
        (ushort)Math.Clamp((int)Math.Round(target + (original - target) * s), 0, 65535);
}
