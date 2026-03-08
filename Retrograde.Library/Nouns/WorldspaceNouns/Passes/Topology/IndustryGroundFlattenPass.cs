using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Flattens the BTD terrain independently under each building placed by IndustryLayoutPass.
/// Each building's footprint radius is derived from ObjectBounds looked up from the context
/// mods, then flattened with a smooth hermite blend out to <see cref="BlendOverlayUnits"/>
/// overlay units.
///
/// Also writes the sampled height into each tile's zoverride so that TileInstantiationPass
/// places buildings at the correct per-tile elevation instead of the global state.TerrainHeight.
///
/// Must run after IndustryLayoutPass (map populated) and before Save.
/// Requires state.BtdFile and state.PackInLibrary.  No-ops if BtdFile is null.
/// </summary>
public class IndustryGroundFlattenPass : IWorldspacePass
{
    /// <summary>Width of the terrain blend transition around each building footprint, in overlay units.</summary>
    private const float BlendOverlayUnits = 15f;

    /// <summary>Minimum radius used when ObjectBounds data is absent, in overlay units.</summary>
    private const float FallbackRadius = 4f;

    // Overlay-to-BTD-world conversion (4096 BTD units per cell / 100 overlay units per cell).
    private const float OverlayToBtd = 4096f / 100f;

    public void RunPass(WorldspaceState state)
    {
        var btd = state.BtdFile;
        if (btd == null) return;

        var allMods = RetrogradeContext.AllMods;

        var map       = state.Map;
        int blocksize = (int)state.TileWorldSize;

        // Mirror TileInstantiationPass origin logic exactly so flatten positions match placement.
        float originX = state.FlatAreaWorldX.HasValue
            ? state.FlatAreaWorldX.Value - blocksize * (map.xsize / 2f)
            : -94f;
        float originY = state.FlatAreaWorldY.HasValue
            ? state.FlatAreaWorldY.Value + blocksize * (map.ysize / 2f)
            : 94f;

        float btdBlend = BlendOverlayUnits * OverlayToBtd;

        // Cache resolved radii per category key so each category is only queried once.
        var radiusCache = new Dictionary<string, float>();

        int flatCount = 0;
        for (int tx = 0; tx < map.xsize; tx++)
        {
            for (int ty = 0; ty < map.ysize; ty++)
            {
                var tile = map.tiles[tx][ty];
                if (tile.prefabs.Count == 0) continue;

                // Skip "floor" fill tiles — they have no PackInLibrary entry.
                string key = tile.prefabs[0];
                if (!state.PackInLibrary.TryGetValue(key, out var variants)) continue;

                if (!radiusCache.TryGetValue(key, out float overlayRadius))
                {
                    overlayRadius = FallbackRadius;
                    foreach (var fk in variants)
                    {
                        var pk = allMods.SelectMany(m => m.PackIns).FirstOrDefault(p => p.FormKey == fk);
                        overlayRadius = MathF.Max(overlayRadius, MathUtil.BoundsRadius(pk?.ObjectBounds, 0f));
                    }
                    radiusCache[key] = overlayRadius;
                }

                // Tile → overlay world space (mirrors TileInstantiationPass exactly).
                float oX = originX + blocksize * tx;
                float oY = originY - blocksize * ty;

                // Overlay → BTD world space.
                float btdX      = oX * OverlayToBtd;
                float btdY      = oY * OverlayToBtd;
                float btdRadius = overlayRadius * OverlayToBtd;

                // FlattenCircle returns the BTD height it sampled (8×-scaled).
                // Store the per-tile game-unit Z as zoverride so TileInstantiationPass
                // places each building at the correct independent elevation.
                float btdHeight = btd.FlattenCircle(btdX, btdY, btdRadius, btdBlend);
                tile.zoverride  = btdHeight / 8f - state.TerrainHeight;
                flatCount++;
            }
        }

        btd.SmoothDirtyCellEdges(36);

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[IndustryGroundFlattenPass] flattened {flatCount} building footprints (blend={BlendOverlayUnits} overlay units)");
    }
}
