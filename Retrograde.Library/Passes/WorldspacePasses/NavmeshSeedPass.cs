using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;

namespace Retrograde.Passes.WorldspacePasses;

/// <summary>
/// Per-cell pass that places navmesh seed markers at the four corners
/// of the current quadrant. Required by Starfield's navmesh generator.
/// Ported from StarTiller FortCellGen.BuildCell().
/// </summary>
public class NavmeshSeedPass : IWorldspacePass
{
    // NavMeshGenSeedMarker [STAT:000001F8]
    private static readonly uint NavmeshSeedMarkerFormId = 0x000001F8;

    public void RunPass(WorldspaceState state)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;
        var starfieldEsm = RetrogradeContext.Current.StarfieldModKey;
        var map = state.Map;
        int blocksize = (int)state.TileWorldSize;

        IFormLinkNullable<IPlaceableObject> navmeshSeedMarker =
            new FormKey(starfieldEsm, NavmeshSeedMarkerFormId).ToNullableLink<IPlaceableObject>();

        // Determine quadrant bounds
        int startx = 0, starty = 0, endx = map.xsize, endy = map.ysize;

        if (state.CurrentCellPos.X == -1) { startx = 0; endx = (map.xsize / 2) - 1; }
        if (state.CurrentCellPos.X == 0) { startx = (map.xsize / 2) - 1; endx = map.xsize; }
        if (state.CurrentCellPos.Y == 0) { starty = 0; endy = (map.ysize / 2) - 1; }
        if (state.CurrentCellPos.Y == -1) { starty = (map.ysize / 2) - 1; endy = map.ysize; }

        float z = -10.0000f;

        // Top-left corner
        PlaceMarker(targetMod, navmeshSeedMarker, state,
            new P3Float(-94 + (blocksize * startx), 94 - (blocksize * (starty + 1)), z));
        // Top-right corner
        PlaceMarker(targetMod, navmeshSeedMarker, state,
            new P3Float(-94 + (blocksize * endx), 94 - (blocksize * (starty + 1)), z));
        // Bottom-left corner
        PlaceMarker(targetMod, navmeshSeedMarker, state,
            new P3Float(-94 + (blocksize * startx), 94 - (blocksize * (endy - 1)), z));
        // Bottom-right corner
        PlaceMarker(targetMod, navmeshSeedMarker, state,
            new P3Float(-94 + (blocksize * endx), 94 - (blocksize * (endy - 1)), z));
    }

    private static void PlaceMarker(StarfieldMod targetMod,
        IFormLinkNullable<IPlaceableObject> markerBase,
        WorldspaceState state, P3Float position)
    {
        var placed = new PlacedObject(targetMod)
        {
            Base = markerBase,
            Position = position,
        };
        state.PlacementUtil.AddToTemporary(state.CurrentCell, placed);
    }
}
