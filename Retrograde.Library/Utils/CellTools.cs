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
            for (int i = 0; i < RetrogradeContext.Current.TargetMod.Cells.Count; i++)
            {
                for (int j = 0; j < RetrogradeContext.Current.TargetMod.Cells[i].SubBlocks.Count; j++)
                {
                    for (int k = 0; k < RetrogradeContext.Current.TargetMod.Cells[i].SubBlocks[j].Cells.Count; k++)
                    {
                        if (RetrogradeContext.Current.TargetMod.Cells[i].SubBlocks[j].Cells[k].EditorID == id)
                        {
                            var refcell = RetrogradeContext.Current.TargetMod.Cells[i].SubBlocks[j].Cells[k].DeepCopy();

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
                                ImageSpace = refcell.ImageSpace,
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
                    }
                }
            }
            return null;
        }
    }
}