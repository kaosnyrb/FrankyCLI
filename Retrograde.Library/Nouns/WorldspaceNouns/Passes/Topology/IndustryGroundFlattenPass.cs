using Retrograde.Utils;
using System;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Flattens the BTD terrain independently under each building placed by IndustryLayoutPass.
/// Each building's footprint is sampled at its own centre to determine target height, then
/// flattened with a smooth hermite blend out to <see cref="BlendOverlayUnits"/> overlay units.
///
/// Must run after IndustryLayoutPass (map populated) and before Save.
/// Requires state.BtdFile and state.PackInRadii.  No-ops if BtdFile is null.
/// </summary>
public class IndustryGroundFlattenPass : IWorldspacePass
{
    /// <summary>Width of the terrain blend transition around each building footprint, in overlay units.</summary>
    private const float BlendOverlayUnits = 15f;

    // Overlay-to-BTD-world conversion (4096 BTD units per cell / 100 overlay units per cell).
    private const float OverlayToBtd = 4096f / 100f;

    public void RunPass(WorldspaceState state)
    {
        var btd = state.BtdFile;
        if (btd == null) return;

        var map     = state.Map;
        float centre  = map.xsize / 2f;
        float tileSize = state.TileWorldSize;
        float originX  = state.FlatAreaWorldX ?? 0f;
        float originY  = state.FlatAreaWorldY ?? 0f;

        float btdBlend = BlendOverlayUnits * OverlayToBtd;

        int flatCount = 0;
        for (int tx = 0; tx < map.xsize; tx++)
        {
            for (int ty = 0; ty < map.ysize; ty++)
            {
                var tile = map.tiles[tx][ty];
                if (tile.prefabs.Count == 0) continue;

                // Only flatten under actual buildings — skip "floor" fill tiles that
                // have no entry in PackInRadii.
                string key = tile.prefabs[0];
                if (!state.PackInRadii.TryGetValue(key, out float overlayRadius))
                    continue;

                // Tile → overlay world space (mirrors TileInstantiationPass).
                float oX = originX + (tx - centre) * tileSize;
                float oY = originY - (ty - centre) * tileSize;

                // Overlay → BTD world space.
                float btdX      = oX * OverlayToBtd;
                float btdY      = oY * OverlayToBtd;
                float btdRadius = overlayRadius * OverlayToBtd;

                btd.FlattenCircle(btdX, btdY, btdRadius, btdBlend);
                flatCount++;
            }
        }

        btd.SmoothDirtyCellEdges(36);

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[IndustryGroundFlattenPass] flattened {flatCount} building footprints (blend={BlendOverlayUnits} overlay units)");
    }
}
