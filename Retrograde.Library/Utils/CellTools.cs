using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Utils
{
    public class CellTools
    {
        public static Cell CloneCellById(string id)
        {
            // Search targetMod first
            for (int i = 0; i < RetrogradeContext.Current.TargetMod.Cells.Count; i++)
                for (int j = 0; j < RetrogradeContext.Current.TargetMod.Cells[i].SubBlocks.Count; j++)
                    for (int k = 0; k < RetrogradeContext.Current.TargetMod.Cells[i].SubBlocks[j].Cells.Count; k++)
                        if (RetrogradeContext.Current.TargetMod.Cells[i].SubBlocks[j].Cells[k].EditorID == id)
                            return BuildCell(RetrogradeContext.Current.TargetMod.Cells[i].SubBlocks[j].Cells[k].DeepCopy(), id);

            // Fallback: search template mods
            foreach (var tm in RetrogradeContext.Current.TemplateMods)
                foreach (var block in tm.Cells)
                    foreach (var subBlock in block.SubBlocks)
                        foreach (var cell in subBlock.Cells)
                            if (cell.EditorID == id)
                                return BuildCell(cell.DeepCopy(), id);

            return null;
        }

        private static Cell BuildCell(Cell refcell, string id)
        {
            var persistantItems = new Noggog.ExtendedList<IPlaced>();
            foreach (var item in refcell.Persistent)
            {
                PlacedObject poref = (PlacedObject)item;
                PlacedObject placedObject = new PlacedObject(RetrogradeContext.Current.TargetMod)
                {
                                    Action = poref.Action,
                                    AttachRef = poref.AttachRef,
                                    Base = poref.Base,
                                    BlueprintPartOrigin = poref.BlueprintPartOrigin,
                                    BOLV = poref.BOLV,
                                    Collision = poref.Collision,
                                    Comments = poref.Comments,
                                    Components = poref.Components,
                                    ConstrainedDecal = poref.ConstrainedDecal,
                                    Count = poref.Count,
                                    CurrentZoneCell = poref.CurrentZoneCell,
                                    DebugText = poref.DebugText,
                                    EditorID = poref.EditorID + RandomProvider.Random.Next(10000),
                                    Emittance = poref.Emittance,
                                    EnableParent = poref.EnableParent,
                                    EncounterZone = poref.EncounterZone,
                                    ExternalEmittance = poref.ExternalEmittance,
                                    FactionRank = poref.FactionRank,
                                    GeometryDirtinessScale = poref.GeometryDirtinessScale,
                                    GroupedPackIn = poref.GroupedPackIn,
                                    HeadTrackingWeight = poref.HeadTrackingWeight,
                                    HealthPercent = poref.HealthPercent,
                                    IsActivationPoint = poref.IsActivationPoint,
                                    IsIgnoredBySandbox = poref.IsIgnoredBySandbox,
                                    IsLinkedRefTransient = poref.IsLinkedRefTransient,
                                    Layer = poref.Layer,
                                    LayeredMaterialSwaps = poref.LayeredMaterialSwaps,
                                    LevelModifier = poref.LevelModifier,
                                    LightArea = poref.LightArea,
                                    LightBarndoorData = poref.LightBarndoorData,
                                    LightColors = poref.LightColors,
                                    LightFlicker = poref.LightFlicker,
                                    GoboAnimatedProperties = poref.GoboAnimatedProperties,
                                    Lighting = poref.Lighting,
                                    LightLayerData = poref.LightLayerData,
                                    LightRoundedness = poref.LightRoundedness,
                                    LightStaticShadowMap = poref.LightStaticShadowMap,
                                    LightVolumetricData = poref.LightVolumetricData,
                                    LinkedReferences = poref.LinkedReferences,
                                    LocationRefTypes = poref.LocationRefTypes,
                                    Lock = poref.Lock,
                                    MapMarker = poref.MapMarker,
                                    NavigationDoorLink = poref.NavigationDoorLink,
                                    NumTraversalFluffBytes = poref.NumTraversalFluffBytes,
                                    OpenByDefault = poref.OpenByDefault,
                                    Ownership = poref.Ownership,
                                    Patrol = poref.Patrol,
                                    Position = poref.Position,
                                    PersistentLocation = poref.PersistentLocation,
                                    PowerLinks = poref.PowerLinks,
                                    Primitive = poref.Primitive,
                                    ProjectedDecal = poref.ProjectedDecal,
                                    ProjectedDecalReferences = poref.ProjectedDecalReferences,
                                    Radius = poref.Radius,
                                    RagdollBipedRotation = poref.RagdollBipedRotation,
                                    Properties = poref.Properties,
                                    RagdollData = poref.RagdollData,
                                    ReferenceGroup = poref.ReferenceGroup,
                                    StarfieldMajorRecordFlags = poref.StarfieldMajorRecordFlags,
                                    Rotation = poref.Rotation,
                                    Scale = poref.Scale,
                                    ShipArrival = poref.ShipArrival,
                                    SnapLinks = poref.SnapLinks,
                                    SourcePackIn = poref.SourcePackIn,
                                    TeleportDestination = poref.TeleportDestination,
                                    TeleportName = poref.TeleportName,
                                    Spline = poref.Spline,
                                    TimeOfDay = poref.TimeOfDay,
                                    Traversals = poref.Traversals,
                                    VolumeData = poref.VolumeData,
                                    VirtualMachineAdapter = poref.VirtualMachineAdapter,
                                    XALG = poref.XALG,
                                    PlacedObjectXCZRXCZA = poref.PlacedObjectXCZRXCZA,
                                    XFLG = poref.XFLG,
                                    XNSE = poref.XNSE,
                                    XPCK = poref.XPCK
                                };
                                persistantItems.Add(placedObject);
                            }

                            var tempItems = new Noggog.ExtendedList<IPlaced>();
                            foreach (var item in refcell.Temporary)
                            {
                                PlacedObject poref = (PlacedObject)item;
                                PlacedObject placedObject = new PlacedObject(RetrogradeContext.Current.TargetMod)
                                {
                                    Action = poref.Action,
                                    AttachRef = poref.AttachRef,
                                    Base = poref.Base,
                                    BlueprintPartOrigin = poref.BlueprintPartOrigin,
                                    BOLV = poref.BOLV,
                                    Collision = poref.Collision,
                                    Comments = poref.Comments,
                                    Components = poref.Components,
                                    ConstrainedDecal = poref.ConstrainedDecal,
                                    Count = poref.Count,
                                    CurrentZoneCell = poref.CurrentZoneCell,
                                    DebugText = poref.DebugText,
                                    EditorID = poref.EditorID,
                                    Emittance = poref.Emittance,
                                    EnableParent = poref.EnableParent,
                                    EncounterZone = poref.EncounterZone,
                                    ExternalEmittance = poref.ExternalEmittance,
                                    FactionRank = poref.FactionRank,
                                    GeometryDirtinessScale = poref.GeometryDirtinessScale,
                                    GroupedPackIn = poref.GroupedPackIn,
                                    HeadTrackingWeight = poref.HeadTrackingWeight,
                                    HealthPercent = poref.HealthPercent,
                                    IsActivationPoint = poref.IsActivationPoint,
                                    IsIgnoredBySandbox = poref.IsIgnoredBySandbox,
                                    IsLinkedRefTransient = poref.IsLinkedRefTransient,
                                    Layer = poref.Layer,
                                    LayeredMaterialSwaps = poref.LayeredMaterialSwaps,
                                    LevelModifier = poref.LevelModifier,
                                    LightArea = poref.LightArea,
                                    LightBarndoorData = poref.LightBarndoorData,
                                    LightColors = poref.LightColors,
                                    LightFlicker = poref.LightFlicker,
                                    GoboAnimatedProperties = poref.GoboAnimatedProperties,
                                    Lighting = poref.Lighting,
                                    LightLayerData = poref.LightLayerData,
                                    LightRoundedness = poref.LightRoundedness,
                                    LightStaticShadowMap = poref.LightStaticShadowMap,
                                    LightVolumetricData = poref.LightVolumetricData,
                                    LinkedReferences = poref.LinkedReferences,
                                    LocationRefTypes = poref.LocationRefTypes,
                                    Lock = poref.Lock,
                                    MapMarker = poref.MapMarker,
                                    NavigationDoorLink = poref.NavigationDoorLink,
                                    NumTraversalFluffBytes = poref.NumTraversalFluffBytes,
                                    OpenByDefault = poref.OpenByDefault,
                                    Ownership = poref.Ownership,
                                    Patrol = poref.Patrol,
                                    Position = poref.Position,
                                    PersistentLocation = poref.PersistentLocation,
                                    PowerLinks = poref.PowerLinks,
                                    Primitive = poref.Primitive,
                                    ProjectedDecal = poref.ProjectedDecal,
                                    ProjectedDecalReferences = poref.ProjectedDecalReferences,
                                    Radius = poref.Radius,
                                    RagdollBipedRotation = poref.RagdollBipedRotation,
                                    Properties = poref.Properties,
                                    RagdollData = poref.RagdollData,
                                    ReferenceGroup = poref.ReferenceGroup,
                                    Rotation = poref.Rotation,
                                    Scale = poref.Scale,
                                    ShipArrival = poref.ShipArrival,
                                    SnapLinks = poref.SnapLinks,
                                    SourcePackIn = poref.SourcePackIn,
                                    TeleportDestination = poref.TeleportDestination,
                                    TeleportName = poref.TeleportName,
                                    Spline = poref.Spline,
                                    TimeOfDay = poref.TimeOfDay,
                                    Traversals = poref.Traversals,
                                    VolumeData = poref.VolumeData,
                                    VirtualMachineAdapter = poref.VirtualMachineAdapter,
                                    XALG = poref.XALG,
                                    PlacedObjectXCZRXCZA = poref.PlacedObjectXCZRXCZA,
                                    XFLG = poref.XFLG,
                                    XNSE = poref.XNSE,
                                    XPCK = poref.XPCK
                                };
                                tempItems.Add(placedObject);
                            }

                            Cell cell = new Cell(RetrogradeContext.Current.TargetMod)
                            {
                                AcousticSpace = refcell.AcousticSpace,
                                CellSkyRegion = refcell.CellSkyRegion,
                                Components = refcell.Components,
                                EditorID = id,
                                EnvironmentMap = refcell.EnvironmentMap,
                                Flags = refcell.Flags,
                                GlobalDirtLayerMaterial = refcell.GlobalDirtLayerMaterial,
                                Grid = refcell.Grid,
                                ImageSpace = EnsureImageSpaceImported(refcell.ImageSpace),
                                Lighting = refcell.Lighting,
                                IsLinkedRefTransient = refcell.IsLinkedRefTransient,
                                LightingTemplate = refcell.LightingTemplate,
                                LinkedReferences = refcell.LinkedReferences,
                                Location = refcell.Location,
                                MajorFlags = refcell.MajorFlags,
                                Music = refcell.Music,
                                Name = refcell.Name,
                                Ownership = refcell.Ownership,
                                Persistent = persistantItems,
                                Temporary = tempItems,
                                TimeOfDay = refcell.TimeOfDay,
                                PersistentTimestamp = refcell.PersistentTimestamp,
                                PersistentUnknownGroupData = refcell.PersistentUnknownGroupData,
                                Timestamp = refcell.Timestamp,
                                Water = refcell.Water,
                                WaterEnvironmentMap = refcell.WaterEnvironmentMap,
                                WaterHeight = refcell.WaterHeight,
                                WaterType = refcell.WaterType,
                                WaterVelocity = refcell.WaterVelocity,
                                XCLAs = refcell.XCLAs,
                                XILS = refcell.XILS
                            };

                return cell;
        }

        /// <summary>
        /// If the ImageSpace FormLink points to a template mod (not Starfield.esm),
        /// copies the ImageSpace record into the target mod with a fresh FormKey
        /// and returns a link to the new copy. Otherwise returns the original link.
        /// </summary>
        private static IFormLinkNullable<IImageSpaceGetter> EnsureImageSpaceImported(
            IFormLinkNullable<IImageSpaceGetter> source)
        {
            if (source == null || source.IsNull)
                return source;

            var fk = source.FormKey;

            // Starfield.esm records need no copying
            if (fk.ModKey.Name == "Starfield")
                return source;

            var targetMod = RetrogradeContext.Current.TargetMod;

            // Find the source record in template mods
            IImageSpaceGetter? found = null;
            foreach (var tm in RetrogradeContext.Current.TemplateMods)
            {
                if (tm.ImageSpaces.TryGetValue(fk, out var tmImgs))
                {
                    found = tmImgs;
                    break;
                }
            }

            if (found == null)
                return source; // can't locate it — leave reference as-is

            // Check if we've already imported it (match by EditorID)
            if (found.EditorID != null)
            {
                var existing = targetMod.ImageSpaces.FirstOrDefault(i => i.EditorID == found.EditorID);
                if (existing != null)
                    return existing.ToNullableLink<IImageSpaceGetter>();
            }

            // Create a new ImageSpace in the target mod and copy data across
            var copied = new ImageSpace(targetMod)
            {
                EditorID = found.EditorID,
                StarfieldMajorRecordFlags = found.StarfieldMajorRecordFlags,
                Reflection = found.Reflection?.ToArray(),
                ReflectionParent = found.ReflectionParent.FormKey.ToNullableLink<IWeatherSettingGetter>(),
                ReflectionDiff = found.ReflectionDiff?.ToArray(),
            };

            targetMod.ImageSpaces.Add(copied);
            Console.WriteLine($"[CellTools] Imported ImageSpace {found.EditorID} ({fk}) → {copied.FormKey}");
            return copied.ToNullableLink<IImageSpaceGetter>();
        }
    }
}