namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Flattens the BTD terrain independently under each building placed by IndustryLayoutPass.
/// Each building's footprint radius is read from state.PackInRadii (pre-computed by
/// IndustryPackInLibraryPass), then flattened with a smooth hermite blend out to
/// <see cref="BlendOverlayUnits"/> overlay units.
///
/// Also writes the sampled height into each tile's zoverride so that TileInstantiationPass
/// places buildings at the correct per-tile elevation instead of the global state.TerrainHeight.
///
/// Must run after IndustryLayoutPass (map populated) and before Save.
/// Requires state.BtdFile, state.PackInLibrary, and state.PackInRadii.  No-ops if BtdFile is null.
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

        var map       = state.Map;
        int blocksize = (int)state.TileWorldSize;

        var (originX, originY) = state.GetTileOrigin(blocksize);

        float btdBlend = BlendOverlayUnits * OverlayToBtd;

        int flatCount = 0;
        for (int tx = 0; tx < map.xsize; tx++)
        {
            for (int ty = 0; ty < map.ysize; ty++)
            {
                var tile = map.tiles[tx][ty];
                if (tile.prefabs.Count == 0) continue;

                // Skip "floor" fill tiles — they have no PackInLibrary entry.
                string key = tile.prefabs[0];
                if (!state.PackInLibrary.ContainsKey(key)) continue;

                if (!state.PackInRadii.TryGetValue(key, out float overlayRadius))
                    overlayRadius = FallbackRadius;

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
