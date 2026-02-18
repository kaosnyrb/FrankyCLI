using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;

namespace Retrograde.Passes.WorldspacePasses;

/// <summary>
/// Per-cell pass that places navmesh seed markers at the four corners
/// of the current quadrant. Required by Starfield's navmesh generator.
/// Only places markers in cells that contain tiles (skips empty cells).
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

        // Check if any tiles in this cell have prefabs — skip empty cells
        bool hasTiles = false;
        for (int x = 0; x < map.xsize && !hasTiles; x++)
        {
            for (int y = 0; y < map.ysize && !hasTiles; y++)
            {
                float worldX = -94 + (blocksize * x);
                float worldY = 94 - (blocksize * y);
                int tileCellX = (int)Math.Floor(worldX / 4096f);
                int tileCellY = (int)Math.Floor(worldY / 4096f);
                if (tileCellX == state.CurrentCellPos.X && tileCellY == state.CurrentCellPos.Y
                    && map.tiles[x][y].prefabs.Count > 0)
                {
                    hasTiles = true;
                }
            }
        }

        if (!hasTiles) return;

        IFormLinkNullable<IPlaceableObject> navmeshSeedMarker =
            new FormKey(starfieldEsm, NavmeshSeedMarkerFormId).ToNullableLink<IPlaceableObject>();

        // Find the tile bounds for this cell
        int startx = int.MaxValue, starty = int.MaxValue;
        int endx = int.MinValue, endy = int.MinValue;
        for (int x = 0; x < map.xsize; x++)
        {
            for (int y = 0; y < map.ysize; y++)
            {
                float worldX = -94 + (blocksize * x);
                float worldY = 94 - (blocksize * y);
                int tileCellX = (int)Math.Floor(worldX / 4096f);
                int tileCellY = (int)Math.Floor(worldY / 4096f);
                if (tileCellX == state.CurrentCellPos.X && tileCellY == state.CurrentCellPos.Y)
                {
                    if (x < startx) startx = x;
                    if (x > endx) endx = x;
                    if (y < starty) starty = y;
                    if (y > endy) endy = y;
                }
            }
        }

        float z = state.TerrainHeight;

        // Place markers at the four corners of the cell's tile region
        PlaceMarker(targetMod, navmeshSeedMarker, state,
            new P3Float(-94 + (blocksize * startx), 94 - (blocksize * starty), z));
        PlaceMarker(targetMod, navmeshSeedMarker, state,
            new P3Float(-94 + (blocksize * endx), 94 - (blocksize * starty), z));
        PlaceMarker(targetMod, navmeshSeedMarker, state,
            new P3Float(-94 + (blocksize * startx), 94 - (blocksize * endy), z));
        PlaceMarker(targetMod, navmeshSeedMarker, state,
            new P3Float(-94 + (blocksize * endx), 94 - (blocksize * endy), z));
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
