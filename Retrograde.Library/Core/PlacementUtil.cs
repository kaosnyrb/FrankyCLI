using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;

namespace Retrograde;

public class PlacementUtil
{
    private readonly List<(Cell cell, IPlaced placedObject)> _pendingPlacements = new();
    private readonly List<(Cell cell, IPlaced placedObject)> _pendingPersistentPlacements = new();

    public readonly List<IPlaced> PlacedObjects = new();

    public void AddToTemporary(Cell cell, PlacedObject placedObject)
    {
        if (cell == null || placedObject == null)
        {
            return;
        }

        _pendingPlacements.Add((cell, placedObject));
    }

    public void NPCAddToTemporary(Cell cell, PlacedNpc placedObject)
    {

        if (cell == null || placedObject == null)
        {
            return;
        }

        placedObject.LevelModifier = Level.Medium;

        _pendingPlacements.Add((cell, placedObject));
    }

    public void NPCAddToPersistent(Cell cell, PlacedNpc placedObject)
    {
        if (cell == null || placedObject == null)
        {
            return;
        }

        placedObject.LevelModifier = Level.Medium;

        _pendingPersistentPlacements.Add((cell, placedObject));
    }

    public void Finalise()
    {
        var targetMod = RetrogradeContext.Current.TargetMod;
        var templateMods = RetrogradeContext.Current.TemplateMods;

        foreach (var (cell, placedObject) in _pendingPlacements)
        {
            if (placedObject is PlacedObject po && po.Base.FormKey.ModKey.Name != "Starfield")
            {
                int unpacked = UnpackPrefab(cell, po, targetMod, templateMods, temporary: true);
                if (unpacked == 0)
                    Console.WriteLine($"[PlacementUtil] WARNING: PackIn {po.Base.FormKey} resolved to 0 objects");
            }
            else
            {
                cell.Temporary.Add(placedObject);
                PlacedObjects.Add(placedObject);
            }
        }

        foreach (var (cell, placedObject) in _pendingPersistentPlacements)
        {
            if (placedObject is PlacedObject po && po.Base.FormKey.ModKey.Name != "Starfield")
            {
                int unpacked = UnpackPrefab(cell, po, targetMod, templateMods, temporary: false);
                if (unpacked == 0)
                    Console.WriteLine($"[PlacementUtil] WARNING: PackIn {po.Base.FormKey} resolved to 0 objects");
            }
            else
            {
                cell.Persistent.Add(placedObject);
                PlacedObjects.Add(placedObject);
            }
        }

        _pendingPlacements.Clear();
        _pendingPersistentPlacements.Clear();
    }

    private int UnpackPrefab(Cell cell, PlacedObject parent, StarfieldMod targetMod,
        IReadOnlyList<IStarfieldModGetter> templateMods, bool temporary)
    {
        var prefabCell = ResolvePrefabCell(parent.Base.FormKey, templateMods);
        if (prefabCell == null)
            return 0;

        var tilePos = parent.Position;
        int yawSteps = RotationZToYawSteps(parent.Rotation.Z);
        int count = 0;

        var entries = temporary ? prefabCell.Temporary : prefabCell.Persistent;
        foreach (var entry in entries)
        {
            if (entry is IPlacedObjectGetter sourcePo)
            {
                var cloned = ClonePlacedObject(sourcePo, tilePos, yawSteps, targetMod);
                if (cloned != null)
                {
                    if (temporary) cell.Temporary.Add(cloned);
                    else cell.Persistent.Add(cloned);
                    PlacedObjects.Add(cloned);
                    count++;
                }
            }
            else if (entry is IPlacedNpcGetter sourceNpc)
            {
                var cloned = ClonePlacedNpc(sourceNpc, tilePos, yawSteps, targetMod);
                if (cloned != null)
                {
                    if (temporary) cell.Temporary.Add(cloned);
                    else cell.Persistent.Add(cloned);
                    PlacedObjects.Add(cloned);
                    count++;
                }
            }
        }

        return count;
    }

    private static int RotationZToYawSteps(float radians)
    {
        int steps = (int)Math.Round(radians / (MathF.PI / 2f));
        return ((steps % 4) + 4) % 4;
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

    public void Reset()
    {
        _pendingPlacements.Clear();
        _pendingPersistentPlacements.Clear();
        PlacedObjects.Clear();
    }
}
