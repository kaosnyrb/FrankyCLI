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
                int unpacked = UnpackPrefab(cell, po.Base.FormKey, po.Position,
                    RotationZToYawSteps(po.Rotation.Z), targetMod, templateMods, temporary: true);
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
                int unpacked = UnpackPrefab(cell, po.Base.FormKey, po.Position,
                    RotationZToYawSteps(po.Rotation.Z), targetMod, templateMods, temporary: false);
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

    /// <summary>
    /// Immediately unpacks a PackIn into the given cell, bypassing the deferred queue.
    /// Returns all placed objects paired with their source EditorID, so callers can identify
    /// specific objects (e.g. the exit door) by name without re-reading the prefab cell.
    /// </summary>
    public List<(string? SourceEditorId, IPlaced Placed)> UnpackNow(
        Cell cell, FormKey packinFormKey, P3Float worldPos, int yawSteps, bool temporary = true)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;
        var templateMods = RetrogradeContext.Current.TemplateMods;
        var results = new List<(string?, IPlaced)>();
        int count = UnpackPrefab(cell, packinFormKey, worldPos, yawSteps, targetMod, templateMods, temporary, results);
        if (count == 0)
            Console.WriteLine($"[PlacementUtil] WARNING: PackIn {packinFormKey} resolved to 0 objects");
        return results;
    }

    private int UnpackPrefab(Cell cell, FormKey packinFormKey, P3Float worldPos, int yawSteps,
        StarfieldMod targetMod, IReadOnlyList<IStarfieldModGetter> templateMods, bool temporary,
        List<(string? SourceEditorId, IPlaced Placed)> results = null)
    {
        var prefabCell = ResolvePrefabCell(packinFormKey, templateMods);
        if (prefabCell == null)
            return 0;

        // Buffer clones at this level so we can patch ProjectedDecalReferences before committing.
        var clonedAtThisLevel = new List<(string? SourceEditorId, IPlaced Placed)>();
        var formKeyRemap = new Dictionary<FormKey, FormKey>();
        int count = 0;

        var entries = temporary ? prefabCell.Temporary : prefabCell.Persistent;
        foreach (var entry in entries)
        {
            if (entry is IPlacedObjectGetter sourcePo)
            {
                // If this entry is itself a nested PackIn, recurse with composed transforms.
                // Nested objects are added directly; cross-level decal refs are not remapped.
                if (sourcePo.Base.FormKey.ModKey.Name != "Starfield" &&
                    IsPackIn(sourcePo.Base.FormKey, templateMods))
                {
                    var nestedWorldPos = worldPos + RgRotation.RotateYaw90(sourcePo.Position, yawSteps);
                    var nestedWorldRot = RgRotation.RotateYaw90(sourcePo.Rotation, yawSteps)
                                        + RgRotation.RotationToP3Float(yawSteps);
                    int nestedYawSteps = RotationZToYawSteps(nestedWorldRot.Z);
                    count += UnpackPrefab(cell, sourcePo.Base.FormKey, nestedWorldPos, nestedYawSteps,
                        targetMod, templateMods, temporary, results);
                }
                else
                {
                    var cloned = ClonePlacedObject(sourcePo, worldPos, yawSteps, targetMod);
                    if (cloned != null)
                    {
                        formKeyRemap[sourcePo.FormKey] = cloned.FormKey;
                        clonedAtThisLevel.Add((sourcePo.EditorID, cloned));
                    }
                }
            }
            else if (entry is IPlacedNpcGetter sourceNpc)
            {
                var cloned = ClonePlacedNpc(sourceNpc, worldPos, yawSteps, targetMod);
                if (cloned != null)
                {
                    formKeyRemap[sourceNpc.FormKey] = cloned.FormKey;
                    clonedAtThisLevel.Add((sourceNpc.EditorID, cloned));
                }
            }
        }

        // Patch ProjectedDecalReferences so they point to the cloned objects, not the prefab originals.
        foreach (var (_, obj) in clonedAtThisLevel)
        {
            if (obj is PlacedObject po && po.ProjectedDecalReferences != null)
            {
                for (int i = 0; i < po.ProjectedDecalReferences.Count; i++)
                {
                    var oldFk = po.ProjectedDecalReferences[i].FormKey;
                    if (formKeyRemap.TryGetValue(oldFk, out var newFk))
                        po.ProjectedDecalReferences[i] = newFk.ToLink<IPlacedGetter>();
                }
            }
        }

        // Add buffered clones to cell.
        var targetList = temporary ? cell.Temporary : cell.Persistent;
        foreach (var (srcId, obj) in clonedAtThisLevel)
        {
            targetList.Add(obj);
            PlacedObjects.Add(obj);
            results?.Add((srcId, obj));
            count++;
        }

        return count;
    }

    private static bool IsPackIn(FormKey formKey, IReadOnlyList<IStarfieldModGetter> templateMods)
    {
        foreach (var tm in templateMods)
            if (tm.PackIns.ContainsKey(formKey))
                return true;
        return false;
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
        var baseFormKey = EnsureBaseImported(source.Base.FormKey, targetMod);
        if (baseFormKey == null) return null;

        var rotatedLocal = RgRotation.RotateYaw90(source.Position, yawSteps);
        var worldPos = tilePos + rotatedLocal;
        var worldRot = RgRotation.RotateYaw90(source.Rotation, yawSteps) + RgRotation.RotationToP3Float(yawSteps);

        var placed = new PlacedObject(targetMod)
        {
            // Identity / transform
            Base = baseFormKey.Value.ToNullableLink<IPlaceableObjectGetter>(),
            Position = worldPos,
            Rotation = worldRot,
            Scale = source.Scale,
            StarfieldMajorRecordFlags = source.StarfieldMajorRecordFlags,

            // Primitive / volume
            Primitive = source.Primitive?.DeepCopy(),
            VolumeData = source.VolumeData?.DeepCopy(),
            VolumeReflectionProbeOffsetIntensity = source.VolumeReflectionProbeOffsetIntensity?.DeepCopy(),

            // Lighting
            Lighting = source.Lighting?.DeepCopy(),
            LightBarndoorData = source.LightBarndoorData?.DeepCopy(),
            LightArea = source.LightArea?.DeepCopy(),
            LightFlicker = source.LightFlicker?.DeepCopy(),
            LightRoundedness = source.LightRoundedness?.DeepCopy(),
            LightColors = source.LightColors?.Select(lc => lc.DeepCopy()).ToExtendedList()!,
            GoboAnimatedProperties = source.GoboAnimatedProperties?.DeepCopy(),
            LightLayerData = source.LightLayerData,
            LightStaticShadowMap = source.LightStaticShadowMap,
            LightVolumetricData = source.LightVolumetricData,
            LightRadiusFalloutExponent = source.LightRadiusFalloutExponent,

            // Ownership / lock
            Ownership = source.Ownership?.DeepCopy(),
            Lock = source.Lock?.DeepCopy(),
            FactionRank = source.FactionRank,

            // References / links
            EnableParent = source.EnableParent?.DeepCopy(),
            LinkedReferences = source.LinkedReferences?.Select(lr => lr.DeepCopy()).ToExtendedList()!,
            LocationRefTypes = source.LocationRefTypes?.ToExtendedList(),
            LayeredMaterialSwaps = source.LayeredMaterialSwaps?.ToExtendedList(),
            SnapLinks = source.SnapLinks?.Select(s => s.DeepCopy()).ToExtendedList()!,
            PowerLinks = source.PowerLinks?.Select(pl => pl.DeepCopy()).ToExtendedList()!,
            ProjectedDecalReferences = source.ProjectedDecalReferences?.ToExtendedList(),

            // Complex sub-records
            ExternalEmittance = source.ExternalEmittance?.DeepCopy(),
            TeleportDestination = source.TeleportDestination?.DeepCopy(),
            NavigationDoorLink = source.NavigationDoorLink?.DeepCopy(),
            MapMarker = source.MapMarker?.DeepCopy(),
            Patrol = source.Patrol?.DeepCopy(),
            Collision = source.Collision?.DeepCopy(),
            CurrentZoneCell = source.CurrentZoneCell?.DeepCopy(),
            DebugText = source.DebugText?.DeepCopy(),
            ProjectedDecal = source.ProjectedDecal?.DeepCopy(),
            Spline = source.Spline?.DeepCopy(),
            //GroupedPackIn = source.GroupedPackIn?.DeepCopy(),  //This is just groups and we don't need this

            // Properties / components / scripts
            Properties = source.Properties?.Select(p => p.DeepCopy()).ToExtendedList(),
            Components = source.Components?.Select(c => c.DeepCopy()).ToExtendedList()!,
            VirtualMachineAdapter = source.VirtualMachineAdapter?.DeepCopy(),
            RagdollData = source.RagdollData?.Select(r => r.DeepCopy()).ToExtendedList(),
            Traversals = source.Traversals?.Select(t => t.DeepCopy()).ToExtendedList(),
            PlacedObjectXCZRXCZA = source.PlacedObjectXCZRXCZA?.Select(x => x.DeepCopy()).ToExtendedList()!,

            // Simple value fields
            Count = source.Count,
            Action = source.Action,
            LevelModifier = source.LevelModifier,
            Radius = source.Radius,
            IsIgnoredBySandbox = source.IsIgnoredBySandbox,
            IsLinkedRefTransient = source.IsLinkedRefTransient,
            IsActivationPoint = source.IsActivationPoint,
            OpenByDefault = source.OpenByDefault,
            BlueprintPartOrigin = source.BlueprintPartOrigin,
            BOLV = source.BOLV,
            XTRI = source.XTRI,
            XALG = source.XALG,
            HeadTrackingWeight = source.HeadTrackingWeight,
            HealthPercent = source.HealthPercent,
            GeometryDirtinessScale = source.GeometryDirtinessScale,
            NumTraversalFluffBytes = source.NumTraversalFluffBytes,
            RagdollBipedRotation = source.RagdollBipedRotation,
            ConstrainedDecal = source.ConstrainedDecal,
            Comments = source.Comments,

            // Raw byte fields
            XFLG = source.XFLG?.ToArray(),
            XNSE = source.XNSE?.ToArray(),
            XWCU = source.XWCU?.ToArray(),
        };

        // FormLink fields — only set when source has a value (setter crashes on FormKey.Null)
        if (!source.Emittance.IsNull) placed.Emittance = source.Emittance.FormKey.ToNullableLink<IEmittanceGetter>();
        if (!source.Layer.IsNull) placed.Layer = source.Layer.FormKey.ToNullableLink<ILayerGetter>();
        if (!source.EncounterZone.IsNull) placed.EncounterZone = source.EncounterZone.FormKey.ToNullableLink<ILocationGetter>();
        if (!source.PersistentLocation.IsNull) placed.PersistentLocation = source.PersistentLocation.FormKey.ToNullableLink<ILocationGetter>();
        if (!source.Location.IsNull) placed.Location = source.Location.FormKey.ToNullableLink<ILocationGetter>();
        if (!source.SourcePackIn.IsNull) placed.SourcePackIn = source.SourcePackIn.FormKey.ToNullableLink<IPackInGetter>();
        if (!source.AttachRef.IsNull) placed.AttachRef = source.AttachRef.FormKey.ToNullableLink<IPlacedGetter>();
        if (!source.TeleportName.IsNull) placed.TeleportName = source.TeleportName.FormKey.ToNullableLink<IMessageGetter>();
        if (!source.TimeOfDay.IsNull) placed.TimeOfDay = source.TimeOfDay.FormKey.ToNullableLink<ITimeOfDayRecordGetter>();
        if (!source.XLIB.IsNull) placed.XLIB = source.XLIB.FormKey.ToNullableLink<ILeveledItemGetter>();

        return placed;
    }

    private static PlacedNpc? ClonePlacedNpc(IPlacedNpcGetter source, P3Float tilePos, int yawSteps, StarfieldMod targetMod)
    {
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

    /// <summary>
    /// Returns the FormKey to use as the Base of a cloned PlacedObject.
    /// For Starfield.esm records, returns the original key unchanged.
    /// For template-mod records, copies the record into the target mod (with a fresh FormKey)
    /// and returns that new key. Returns null for unsupported record types (the placed object
    /// will be skipped).
    /// </summary>
    private static FormKey? EnsureBaseImported(FormKey baseFormKey, StarfieldMod targetMod)
    {
        if (baseFormKey.ModKey.Name == "Starfield")
            return baseFormKey;

        if (baseFormKey.ModKey.Name == targetMod.ModKey.Name)
            return baseFormKey;


        var templateMods = RetrogradeContext.Current.TemplateMods;

        foreach (var tm in templateMods)
        {
            if (tm.Lights.TryGetValue(baseFormKey, out var light))
                return EnsureLightImported(light, targetMod);

            // Add further record-type checks here as new template-mod dependencies arise.
        }

        return null; // unsupported or not found — caller will skip this placed object
    }

    /// <summary>
    /// Copies a Light from a template mod into the target mod, returning the
    /// new FormKey. If an identical EditorID already exists in the target mod,
    /// returns that existing record's FormKey instead.
    /// </summary>
    private static FormKey EnsureLightImported(ILightGetter source, StarfieldMod targetMod)
    {
        if (source.EditorID != null)
        {
            var existing = targetMod.Lights.FirstOrDefault(l => l.EditorID == source.EditorID);
            if (existing != null)
                return existing.FormKey;
        }

        var copy = new Light(targetMod)
        {
            EditorID = source.EditorID,
            StarfieldMajorRecordFlags = source.StarfieldMajorRecordFlags,
            ObjectBounds = source.ObjectBounds.DeepCopy(),
            DirtinessScale = source.DirtinessScale,
            XALG = source.XALG,
            Name = source.Name?.DeepCopy(),
            Time = source.Time,
            Radius = source.Radius,
            Color = source.Color,
            Flags = source.Flags,
            FalloffExponent = source.FalloffExponent,
            FOV = source.FOV,
            NearClip = source.NearClip,
            FlickerPeriod = source.FlickerPeriod,
            FlickerIntensityAmplitude = source.FlickerIntensityAmplitude,
            FlickerMovementAmplitude = source.FlickerMovementAmplitude,
            ShadowOffset = source.ShadowOffset,
            InnerFOV = source.InnerFOV,
            PbrLightTemperatureK = source.PbrLightTemperatureK,
            PbrLuminousPowerLm = source.PbrLuminousPowerLm,
            Type = source.Type,
            FlickerEffect = source.FlickerEffect,
            UseAdaptiveLighting = source.UseAdaptiveLighting,
            AdaptiveLightEc = source.AdaptiveLightEc,
            AdaptiveLightEv100Min = source.AdaptiveLightEv100Min,
            AdaptiveLightEv100Max = source.AdaptiveLightEv100Max,
            RadiusFalloutExponent = source.RadiusFalloutExponent,
            Gobo = source.Gobo,
            Barndoors = source.Barndoors.DeepCopy(),
            Roundness = source.Roundness.DeepCopy(),
            GoboData = source.GoboData.DeepCopy(),
            Layer = source.Layer,
            AreaLight = source.AreaLight.DeepCopy(),
            VolumetricLightIntensityScale = source.VolumetricLightIntensityScale,
            Model = source.Model?.DeepCopy(),
            SoundReference = source.SoundReference?.DeepCopy(),
        };

        if (!source.Lens.IsNull) copy.Lens = source.Lens.FormKey.ToNullableLink<ILensFlareGetter>();
        if (!source.DefaultLayer.IsNull) copy.DefaultLayer = source.Lens.FormKey.ToNullableLink<ILayerGetter>();


        targetMod.Lights.Add(copy);
        Console.WriteLine($"[PlacementUtil] Imported Light {source.EditorID} → {copy.FormKey}");
        return copy.FormKey;
    }

    public void Reset()
    {
        _pendingPlacements.Clear();
        _pendingPersistentPlacements.Clear();
        PlacedObjects.Clear();
    }
}
