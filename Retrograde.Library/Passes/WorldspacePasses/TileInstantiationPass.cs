using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Utils;
using System;
using System.Linq;

namespace Retrograde.Passes.WorldspacePasses;

/// <summary>
/// Per-cell pass that converts map tiles in the current quadrant
/// into PlacedObject records using the PackIn library.
/// Ported from StarTiller FortCellGen.BuildCell().
/// </summary>
public class TileInstantiationPass : IWorldspacePass
{
    public void RunPass(WorldspaceState state)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;
        var rand = state.Rng;
        var map = state.Map;
        int blocksize = (int)state.TileWorldSize;

        // Determine quadrant bounds from cell position
        int startx = 0;
        int starty = 0;
        int endx = map.xsize;
        int endy = map.ysize;

        if (state.CurrentCellPos.X == -1) { startx = 0; endx = (map.xsize / 2) - 1; }
        if (state.CurrentCellPos.X == 0) { startx = (map.xsize / 2) - 1; endx = map.xsize; }
        if (state.CurrentCellPos.Y == 0) { starty = 0; endy = (map.ysize / 2) - 1; }
        if (state.CurrentCellPos.Y == -1) { starty = (map.ysize / 2) - 1; endy = map.ysize; }

        for (int x = startx; x < endx; x++)
        {
            for (int y = starty; y < endy; y++)
            {
                if (map.tiles[x][y].prefabs.Count > 0)
                {
                    foreach (var pfb in map.tiles[x][y].prefabs)
                    {
                        if (state.PackInLibrary.ContainsKey(pfb))
                        {
                            var variants = state.PackInLibrary[pfb];
                            if (variants.Count == 0) continue;

                            int prefabid = rand.Next(variants.Count);
                            var prefab = variants[prefabid];

                            // Remove non-reusable, non-addon variants after use
                            var packIn = targetMod.PackIns[prefab];
                            if (packIn != null &&
                                packIn.EditorID != null &&
                                !packIn.EditorID.Contains("reuse") &&
                                !packIn.EditorID.Contains("addon"))
                            {
                                if (variants.Count > 1)
                                {
                                    variants.RemoveAt(prefabid);
                                }
                            }

                            float z = -10;
                            if (map.tiles[x][y].zoverride != 0)
                            {
                                z = map.tiles[x][y].zoverride;
                            }
                            P3Float pos = new P3Float(-94 + (blocksize * x), 94 - (blocksize * y), z);
                            P3Float rot = new P3Float(0, 0, RotationUtils.EulerToRadCardinals(map.tiles[x][y].rotation));

                            // Persistent vs temporary (Instanced Static flag = 2560)
                            if (packIn != null && packIn.MajorRecordFlagsRaw == 2560)
                            {
                                var placed = new PlacedObject(targetMod)
                                {
                                    Base = prefab.ToNullableLink<IPlaceableObjectGetter>(),
                                    Position = pos,
                                    Rotation = rot,
                                    MajorRecordFlagsRaw = 66560
                                };
                                state.PlacementUtil.AddToPersistent(placed);
                            }
                            else
                            {
                                var placed = new PlacedObject(targetMod)
                                {
                                    Base = prefab.ToNullableLink<IPlaceableObjectGetter>(),
                                    Position = pos,
                                    Rotation = rot,
                                };
                                state.PlacementUtil.AddToTemporary(state.CurrentCell, placed);
                            }
                        }
                    }
                }
            }
        }
    }
}
