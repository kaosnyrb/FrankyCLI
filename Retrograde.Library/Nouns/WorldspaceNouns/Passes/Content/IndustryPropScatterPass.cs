using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Content pass that scatters small industry props (crates, equipment) around the
/// buildings placed by IndustryLayoutPass.  Each building gets 2–4 props placed in
/// an annular zone just outside its footprint.  Props are oriented radially (toward
/// or away from the building) with a small yaw jitter for a natural look.
///
/// Collision is checked against all building exclusion zones and already-placed props
/// so nothing overlaps.  Height is sampled from the BTD terrain.
///
/// Must run in ContentPasses (after CellBuildPasses have populated state.CellLookup).
/// Requires state.PackInRadii (populated by IndustryPackInLibraryPass).
/// </summary>
public class IndustryPropScatterPass : IWorldspacePass
{
    // ── tuning ───────────────────────────────────────────────────────────────

    /// <summary>Maximum props placed per building.</summary>
    private const int PropsMax = 4;

    /// <summary>
    /// Total placement attempts per building before giving up.
    /// High enough to reliably reach PropsMin even in tight clusters.
    /// </summary>
    private const int AttemptsPerBuilding = 20;

    /// <summary>Gap between a building's footprint edge and the nearest prop edge, in overlay units.</summary>
    private const float PropClearance = 1.5f;

    /// <summary>Width of the annular scatter zone beyond PropClearance, in overlay units.</summary>
    private const float ScatterWidth = 6f;

    /// <summary>Minimum centre-to-centre distance between two props, in overlay units.</summary>
    private const float PropExclusion = 3f;

    /// <summary>
    /// Random yaw jitter on top of the radial alignment, in radians (±value).
    /// 0.35 rad ≈ 20° — keeps props roughly radial without a mechanical look.
    /// </summary>
    private const float YawJitter = 0.35f;

    /// <summary>How far props sink into the ground, in overlay units. Keeps bases flush.</summary>
    private const float SinkDepth = 0.05f;

    /// <summary>
    /// Width of the terrain blend transition around each prop footprint, in overlay units.
    /// Smaller than the building blend — props are smaller and sit closer together.
    /// </summary>
    private const float PropBlendOverlay = 6f;

    /// <summary>Flatten radius used when ObjectBounds data is absent, in overlay units.</summary>
    private const float FallbackPropRadius = 2f;

    private const float OverlayToBtd = 4096f / 100f;

    /// <summary>
    /// Tag added to a map tile when a prop lands on it.
    /// Causes RockScatterPass / VegetationScatterPass to treat the tile as occupied.
    /// </summary>
    private const string PropTileTag = "prop";

    // ── prop catalogue ───────────────────────────────────────────────────────

    /// <summary>
    /// PackInLibrary keys populated by IndustryPackInLibraryPass that hold prop variants.
    /// All FormKeys across these categories are pooled and picked from uniformly.
    /// </summary>
    private static readonly string[] PropCategoryKeys =
    [
        "prop_crates_large",
        "prop_crates_medium",
        "prop_crate_pi",
    ];

    // ── entry point ──────────────────────────────────────────────────────────

    public void RunPass(WorldspaceState state)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;
        var rand      = state.Rng;
        var map       = state.Map;
        var btd       = state.BtdFile;
        int blocksize = (int)state.TileWorldSize;

        var props = CollectPropEntries(state.PackInLibrary);
        if (props.Count == 0)
        {
            Console.Error.WriteLine("[IndustryPropScatterPass] WARNING: no prop FormKeys in PackInLibrary — pass skipped.");
            return;
        }

        // Mirror TileInstantiationPass / RockScatterPass origin logic.
        float originX = state.FlatAreaWorldX.HasValue
            ? state.FlatAreaWorldX.Value - blocksize * (map.xsize / 2f)
            : -94f;
        float originY = state.FlatAreaWorldY.HasValue
            ? state.FlatAreaWorldY.Value + blocksize * (map.ysize / 2f)
            : 94f;

        var buildings = CollectBuildings(map, state.PackInRadii, originX, originY, blocksize);
        if (buildings.Count == 0) return;

        // Track placed prop centres for mutual exclusion across all buildings.
        var placed = new List<(float x, float y)>(buildings.Count * PropsMax);
        int total  = 0;

        foreach (var b in buildings)
        {
            float innerR = b.Radius + PropClearance;
            float outerR = innerR + ScatterWidth;

            int count = 0;
            for (int attempt = 0; attempt < AttemptsPerBuilding && count < PropsMax; attempt++)
            {
                double angle = rand.NextDouble() * Math.PI * 2.0;
                float  dist  = innerR + (float)(rand.NextDouble() * (outerR - innerR));

                float pX = b.X + MathF.Cos((float)angle) * dist;
                float pY = b.Y + MathF.Sin((float)angle) * dist;

                if (OverlapsBuilding(buildings, pX, pY)) continue;
                if (OverlapsProp(placed, pX, pY))        continue;

                int cellX = (int)Math.Floor(pX / 100f);
                int cellY = (int)Math.Floor(pY / 100f);
                if (!state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var cell)) continue;

                var entry = props[rand.Next(props.Count)];

                // Flatten terrain under this prop and use the sampled height as Z.
                float z;
                if (btd != null)
                {
                    float btdHeight = btd.FlattenCircle(
                        pX * OverlayToBtd, pY * OverlayToBtd,
                        entry.Radius * OverlayToBtd,
                        PropBlendOverlay * OverlayToBtd);
                    z = btdHeight / 8f - SinkDepth;
                }
                else
                {
                    z = state.TerrainHeight - SinkDepth;
                }

                // Radial yaw: random flip toward/away from building, plus jitter.
                float radialYaw = (float)angle + (rand.Next(2) == 0 ? 0f : MathF.PI);
                float yaw       = radialYaw + (float)(rand.NextDouble() * 2.0 - 1.0) * YawJitter;

                var obj = new PlacedObject(targetMod)
                {
                    Position = new P3Float(pX, pY, z),
                    Rotation = new P3Float(0f, 0f, yaw),
                };
                obj.Base = entry.FormKey.ToNullableLink<IPlaceableObjectGetter>();

                state.PlacementUtil.AddToTemporary(cell, obj);
                placed.Add((pX, pY));

                // Mark the tile occupied so later passes skip it.
                int tileTX = (int)Math.Floor((pX - originX) / blocksize);
                int tileTY = (int)Math.Floor((originY - pY) / blocksize);
                if (tileTX >= 0 && tileTX < map.xsize && tileTY >= 0 && tileTY < map.ysize)
                    map.tiles[tileTX][tileTY].prefabs.Add(PropTileTag);

                count++;
                total++;
            }
        }

        if (btd != null && total > 0)
            btd.SmoothDirtyCellEdges(36);

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[IndustryPropScatterPass] placed {total} props around {buildings.Count} buildings, terrain flattened under each");
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Collects all prop variants from the library, pairing each FormKey with its
    /// ObjectBounds radius looked up from RetrogradeContext.  Falls back to
    /// <see cref="FallbackPropRadius"/> when bounds data is absent.
    /// </summary>
    private static List<PropEntry> CollectPropEntries(Dictionary<string, List<FormKey>> library)
    {
        var ctx     = RetrogradeContext.Current;
        List<IStarfieldModGetter> allMods = [..ctx.TemplateMods, ctx.StarfieldMod];

        var result = new List<PropEntry>();
        foreach (var key in PropCategoryKeys)
        {
            if (!library.TryGetValue(key, out var variants) || variants.Count == 0)
            {
                Console.Error.WriteLine($"[IndustryPropScatterPass] WARNING: prop category '{key}' empty or missing from PackInLibrary");
                continue;
            }

            foreach (var fk in variants)
            {
                var pk = allMods.SelectMany(m => m.PackIns).FirstOrDefault(p => p.FormKey == fk);
                float radius = FallbackPropRadius;
                if (pk?.ObjectBounds != null)
                {
                    float ex = MathF.Abs(pk.ObjectBounds.Second.X - pk.ObjectBounds.First.X) / 2f;
                    float ey = MathF.Abs(pk.ObjectBounds.Second.Y - pk.ObjectBounds.First.Y) / 2f;
                    radius = MathF.Max(MathF.Max(ex, ey), FallbackPropRadius);
                }
                result.Add(new PropEntry(fk, radius));
            }
        }
        return result;
    }

    private static List<Building> CollectBuildings(
        GenerationMap map, Dictionary<string, float> radii,
        float originX, float originY, int blocksize)
    {
        var result = new List<Building>();
        for (int tx = 0; tx < map.xsize; tx++)
        for (int ty = 0; ty < map.ysize; ty++)
        {
            var tile = map.tiles[tx][ty];
            if (tile.prefabs.Count == 0) continue;
            if (!radii.TryGetValue(tile.prefabs[0], out float radius)) continue; // skip fill tiles

            result.Add(new Building(
                originX + blocksize * tx,
                originY - blocksize * ty,
                radius));
        }
        return result;
    }

    private static bool OverlapsBuilding(List<Building> buildings, float pX, float pY)
    {
        foreach (var b in buildings)
        {
            float dx  = pX - b.X;
            float dy  = pY - b.Y;
            float min = b.Radius + PropExclusion;
            if (dx * dx + dy * dy < min * min) return true;
        }
        return false;
    }

    private static bool OverlapsProp(List<(float x, float y)> placed, float pX, float pY)
    {
        float minSq = PropExclusion * PropExclusion;
        foreach (var (ox, oy) in placed)
        {
            float dx = pX - ox, dy = pY - oy;
            if (dx * dx + dy * dy < minSq) return true;
        }
        return false;
    }

    // ── data types ───────────────────────────────────────────────────────────

    private readonly struct PropEntry(FormKey formKey, float radius)
    {
        public readonly FormKey FormKey = formKey;
        public readonly float   Radius  = radius;
    }

    private readonly struct Building(float x, float y, float radius)
    {
        public readonly float X      = x;
        public readonly float Y      = y;
        public readonly float Radius = radius;
    }
}
