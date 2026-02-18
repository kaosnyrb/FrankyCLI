using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Passes.WorldspacePasses;

/// <summary>
/// Per-cell pass that converts map tiles in the current quadrant
/// into placed records by unpacking PackIn prefab contents.
/// Only places objects whose Base form comes from Starfield.esm,
/// eliminating dependencies on template library mods.
/// </summary>
public class TileInstantiationPass : IWorldspacePass
{
    public void RunPass(WorldspaceState state)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;
        var templateMods = RetrogradeContext.Current.TemplateMods;
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

        int totalPlaced = 0;

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
                            var packIn = templateMods
                                .SelectMany(m => m.PackIns)
                                .FirstOrDefault(p => p.FormKey == prefab);
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

                            float z = state.TerrainHeight;
                            if (map.tiles[x][y].zoverride != 0)
                            {
                                z = state.TerrainHeight + map.tiles[x][y].zoverride;
                            }
                            P3Float tilePos = new P3Float(-94 + (blocksize * x), 94 - (blocksize * y), z);
                            int yawSteps = map.tiles[x][y].rotation / 90;

                            // Resolve the PackIn's cell to unpack its contents
                            var prefabCell = ResolvePrefabCell(prefab, templateMods);
                            if (prefabCell == null)
                            {
                                Console.WriteLine($"[TileInstantiation] WARNING: Could not resolve cell for PackIn {prefab}, skipping");
                                continue;
                            }

                            // Unpack all placed entries from the prefab cell
                            foreach (var entry in prefabCell.Temporary)
                            {
                                if (entry is IPlacedObjectGetter po)
                                {
                                    var placed = ClonePlacedObject(po, tilePos, yawSteps, targetMod);
                                    if (placed != null)
                                    {
                                        var targetCell = ResolveCell(state, placed.Position);
                                        state.PlacementUtil.AddToTemporary(targetCell, placed);
                                        totalPlaced++;
                                    }
                                }
                                else if (entry is IPlacedNpcGetter npc)
                                {
                                    var placed = ClonePlacedNpc(npc, tilePos, yawSteps, targetMod);
                                    if (placed != null)
                                    {
                                        var targetCell = ResolveCell(state, placed.Position);
                                        state.PlacementUtil.AddToTemporary(targetCell, placed);
                                        totalPlaced++;
                                    }
                                }
                            }

                            foreach (var entry in prefabCell.Persistent)
                            {
                                if (entry is IPlacedObjectGetter po)
                                {
                                    var placed = ClonePlacedObject(po, tilePos, yawSteps, targetMod);
                                    if (placed != null)
                                    {
                                        state.PlacementUtil.AddToPersistent(placed);
                                        totalPlaced++;
                                    }
                                }
                                else if (entry is IPlacedNpcGetter npc)
                                {
                                    var placed = ClonePlacedNpc(npc, tilePos, yawSteps, targetMod);
                                    if (placed != null)
                                    {
                                        state.PlacementUtil.AddToPersistent(placed);
                                        totalPlaced++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        Console.WriteLine($"[TileInstantiation] Unpacked {totalPlaced} objects from prefabs");
    }

    /// <summary>
    /// Determines which cell a world position belongs to and returns it.
    /// Falls back to state.CurrentCell if the computed cell isn't in the lookup.
    /// Each cell is 4096 world units.
    /// </summary>
    private static Cell ResolveCell(WorldspaceState state, P3Float worldPos)
    {
        int cellX = (int)Math.Floor(worldPos.X / 4096f);
        int cellY = (int)Math.Floor(worldPos.Y / 4096f);
        var cellPoint = new P2Int(cellX, cellY);

        if (state.CellLookup.TryGetValue(cellPoint, out var cell))
            return cell;

        return state.CurrentCell;
    }

    private static ICellGetter? ResolvePrefabCell(FormKey packinFormKey, IReadOnlyList<IStarfieldModGetter> templateMods)
    {
        foreach (var mod in templateMods)
        {
            var packin = mod.PackIns.FirstOrDefault(p => p.FormKey == packinFormKey);
            if (packin?.Cell.FormKey != null)
            {
                foreach (var block in mod.Cells)
                    foreach (var subBlock in block.SubBlocks)
                        foreach (var cell in subBlock.Cells)
                            if (cell.FormKey == packin.Cell.FormKey)
                                return cell;
            }
        }
        return null;
    }

    private static PlacedObject? ClonePlacedObject(IPlacedObjectGetter source, P3Float tilePos, int yawSteps, StarfieldMod targetMod)
    {
        // Only place Starfield.esm forms
        if (source.Base.FormKey.ModKey.Name != "Starfield") return null;

        var rotatedLocal = RgRotation.RotateYaw90(source.Position, yawSteps);
        var worldPos = tilePos + rotatedLocal;
        var worldRot = source.Rotation + RgRotation.RotationToP3Float(yawSteps);

        return new PlacedObject(targetMod)
        {
            Base = source.Base.FormKey.ToNullableLink<IPlaceableObjectGetter>(),
            Position = worldPos,
            Rotation = worldRot,
            Scale = source.Scale,
            Count = source.Count,
            Primitive = source.Primitive?.DeepCopy(),
            VolumeData = source.VolumeData?.DeepCopy(),
            Lighting = source.Lighting?.DeepCopy(),
            EnableParent = source.EnableParent?.DeepCopy(),
            Ownership = source.Ownership?.DeepCopy(),
            MapMarker = source.MapMarker?.DeepCopy(),
        };
    }

    private static PlacedNpc? ClonePlacedNpc(IPlacedNpcGetter source, P3Float tilePos, int yawSteps, StarfieldMod targetMod)
    {
        // Only place Starfield.esm forms
        if (source.Base.FormKey.ModKey.Name != "Starfield") return null;

        var rotatedLocal = RgRotation.RotateYaw90(source.Position, yawSteps);
        var worldPos = tilePos + rotatedLocal;
        var worldRot = source.Rotation + RgRotation.RotationToP3Float(yawSteps);

        return new PlacedNpc(targetMod)
        {
            Base = source.Base.FormKey.ToLink<INpcGetter>(),
            Position = worldPos,
            Rotation = worldRot,
            Scale = source.Scale,
            LevelModifier = source.LevelModifier,
            Ownership = source.Ownership?.DeepCopy(),
        };
    }
}
