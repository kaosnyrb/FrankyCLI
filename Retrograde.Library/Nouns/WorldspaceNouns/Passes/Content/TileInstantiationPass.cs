using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Passes.Worldspace;

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

        int totalPlaced = 0;

        // If TerrainFlattenPass ran, centre the tile grid on the flat area.
        // Otherwise fall back to the legacy hardcoded origin.
        float originX = state.FlatAreaWorldX.HasValue
            ? state.FlatAreaWorldX.Value - blocksize * (map.xsize / 2f)
            : -94f;
        float originY = state.FlatAreaWorldY.HasValue
            ? state.FlatAreaWorldY.Value + blocksize * (map.ysize / 2f)
            : 94f;

        for (int x = 0; x < map.xsize; x++)
        {
            for (int y = 0; y < map.ysize; y++)
            {
                // Determine which cell this tile belongs to from its world position
                float worldX = originX + (blocksize * x);
                float worldY = originY - (blocksize * y);
                int tileCellX = (int)Math.Floor(worldX / 100f);
                int tileCellY = (int)Math.Floor(worldY / 100f);
                if (tileCellX != state.CurrentCellPos.X || tileCellY != state.CurrentCellPos.Y)
                    continue;

                foreach (var pfb in map.tiles[x][y].prefabs)
                {
                    if (!state.PackInLibrary.TryGetValue(pfb, out var variants))
                        continue;
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
                            variants.RemoveAt(prefabid);
                    }

                    float z = state.TerrainHeight;
                    if (map.tiles[x][y].zoverride != 0)
                        z = state.TerrainHeight + map.tiles[x][y].zoverride;
                    P3Float tilePos = new P3Float(originX + (blocksize * x), originY - (blocksize * y), z);
                    int yawSteps = map.tiles[x][y].rotation / 90;

                    // Resolve the PackIn's cell to unpack its contents
                    var prefabCell = ResolvePrefabCell(prefab, templateMods);
                    if (prefabCell == null)
                    {
                        Console.WriteLine($"[TileInstantiation] WARNING: Could not resolve cell for PackIn {prefab}, skipping");
                        continue;
                    }

                    PlaceEntries(prefabCell.Temporary,  tilePos, yawSteps, persistent: false);
                    PlaceEntries(prefabCell.Persistent, tilePos, yawSteps, persistent: true);
                }
            }
        }

        Console.WriteLine($"[TileInstantiation] Unpacked {totalPlaced} objects from prefabs");

        void PlaceEntries<T>(IEnumerable<T> entries, P3Float tilePos, int yawSteps, bool persistent)
        {
            foreach (var entry in entries)
            {
                if (entry is IPlacedObjectGetter po)
                {
                    var placed = ClonePlacedObject(po, tilePos, yawSteps, targetMod);
                    if (placed == null) continue;
                    if (persistent) state.PlacementUtil.AddToPersistent(placed);
                    else state.PlacementUtil.AddToTemporary(ResolveCell(state, placed.Position), placed);
                    totalPlaced++;
                }
                else if (entry is IPlacedNpcGetter npc)
                {
                    var placed = ClonePlacedNpc(npc, tilePos, yawSteps, targetMod);
                    if (placed == null) continue;
                    if (persistent) state.PlacementUtil.AddToPersistent(placed);
                    else state.PlacementUtil.AddToTemporary(ResolveCell(state, placed.Position), placed);
                    totalPlaced++;
                }
            }
        }
    }

    /// <summary>
    /// Determines which cell a world position belongs to and returns it.
    /// Falls back to state.CurrentCell if the computed cell isn't in the lookup.
    /// Each cell is 100 overlay world units.
    /// </summary>
    private static Cell ResolveCell(WorldspaceState state, P3Float worldPos)
    {
        int cellX = (int)Math.Floor(worldPos.X / 100f);
        int cellY = (int)Math.Floor(worldPos.Y / 100f);
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
            if (packin != null && !packin.Cell.IsNull)
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
        var worldRot = RgRotation.RotateYaw90(source.Rotation, yawSteps) + RgRotation.RotationToP3Float(yawSteps);

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
        var worldRot = RgRotation.RotateYaw90(source.Rotation, yawSteps) + RgRotation.RotationToP3Float(yawSteps);

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
