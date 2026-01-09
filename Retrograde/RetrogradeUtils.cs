using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI
{
    public class RoomPrefab
    {
        public RoomPrefab(string EditorId) 
        { 
            PrefabEditorId = EditorId;

            //Find the prefab.
            foreach (var packin in gen_quest_main.myMod.PackIns)
            {
                if (packin != null)
                {
                    if (packin.EditorID == PrefabEditorId)
                    {
                        packin_instance = packin;
                        break;
                    }
                }
            }

            //Fetch the markers in the packin.
            var cellformkey =  packin_instance.Cell.FormKey;
            Cell PrefabCell = null;
            for (int i = 0; i < gen_quest_main.myMod.Cells.Count; i++)
            {
                for (int j = 0; j < gen_quest_main.myMod.Cells[i].SubBlocks.Count; j++)
                {
                    for (int k = 0; k < gen_quest_main.myMod.Cells[i].SubBlocks[j].Cells.Count; k++)
                    {
                        if (gen_quest_main.myMod.Cells[i].SubBlocks[j].Cells[k].FormKey == cellformkey)
                        {
                            PrefabCell = gen_quest_main.myMod.Cells[i].SubBlocks[j].Cells[k];
                            break;
                        }
                    }
                }
            }

            Markers = new List<PrefabMarker>();

            for (int i = 0; i < PrefabCell.Temporary.Count; i++) {
                if (PrefabCell.Temporary[i] != null)
                {
                    var entry = PrefabCell.Temporary[i];

                    switch (entry)
                    {
                        case PlacedObject po:
                            TryAddMarker(po.EditorID, po.Position, po.Rotation, Markers);
                            break;

                        case PlacedNpc pn:
                            TryAddMarker(pn.EditorID, pn.Position, pn.Rotation, Markers);
                            break;
                    }
                }
            }

            for (int i = 0; i < PrefabCell.Persistent.Count; i++)
            {
                if (PrefabCell.Persistent[i] != null)
                {
                    var placed = (PlacedObject)PrefabCell.Persistent[i];
                    if ( placed.EditorID != null)
                    {
                        if (placed.EditorID.Contains(""))
                        {
                            Markers.Add(new PrefabMarker()
                            {
                                MarkerEditorId = placed.EditorID,
                                Position = placed.Position,
                                Rotation = placed.Rotation
                            });
                        }
                    }
                }
            }
        }


        public string PrefabEditorId;
        public PackIn packin_instance { get; set; }

        public List<PrefabMarker> Markers;

        public P3Float StartingMarkerPosition;

        private void TryAddMarker(
            string editorId,
            P3Float position,
            P3Float rotation,
            List<PrefabMarker> markers)
        {
            if (string.IsNullOrEmpty(editorId))
                return;

            if (!editorId.Contains("")) // your filter
                return;

            markers.Add(new PrefabMarker
            {
                MarkerEditorId = editorId,
                Position = position,
                Rotation = rotation
            });
        }

    }


    public struct RgAabb
    {
        public P3Float Min;
        public P3Float Max;
    }

    public class PrefabMarker
    {
        public PrefabMarker() { }

        public string MarkerEditorId;
        public P3Float Position;
        public P3Float Rotation;

    }
    public static class RgConnectorParser
    {
        public static RgConnector Parse(string editorId)
        {
            var result = new RgConnector
            {
                RawEditorId = editorId,
                IsValid = false
            };

            if (string.IsNullOrWhiteSpace(editorId))
                return result;

            var parts = editorId.Split('_');

            // Expected: rg conn n D1 station  => 5 parts
            if (parts.Length < 5)
                return result;

            if (parts[0] != "rg" || parts[1] != "conn")
                return result;

            result.Direction = ParseDirection(parts[2]);
            result.DoorSize = parts[3];
            result.Tileset = parts[4];
            result.IsValid = result.Direction != ConnectorDirection.Unknown;

            return result;
        }

        private static ConnectorDirection ParseDirection(string dir)
        {
            switch (dir.ToLowerInvariant())
            {
                case "n": return ConnectorDirection.North;
                case "s": return ConnectorDirection.South;
                case "e": return ConnectorDirection.East;
                case "w": return ConnectorDirection.West;
                default: return ConnectorDirection.Unknown;
            }
        }
    }

    public static class RgRotation
    {
        // steps: 0=0°, 1=90°, 2=180°, 3=270° (clockwise or CCW depends on your coord system)
        public static P3Float RotateYaw90(P3Float v, int steps)
        {
            steps = ((steps % 4) + 4) % 4;

            // NOTE: This assumes a typical X/Y plane with +Z up.
            // If the direction comes out mirrored in game, swap the sign pattern.
            return steps switch
            {
                0 => v,
                1 => new P3Float(v.Y, -v.X, v.Z),
                2 => new P3Float(-v.X, -v.Y, v.Z),
                3 => new P3Float(-v.Y, v.X, v.Z),
                _ => v
            };
        }

        public static ConnectorDirection RotateDir(ConnectorDirection d, int steps)
        {
            steps = ((steps % 4) + 4) % 4;

            if (d == ConnectorDirection.Unknown)
                return d;

            // Order here must match the vector rotation above.
            // If your RotateYaw90 ends up inverted, flip this mapping too.
            for (int i = 0; i < steps; i++)
            {
                d = d switch
                {
                    ConnectorDirection.North => ConnectorDirection.East,
                    ConnectorDirection.East => ConnectorDirection.South,
                    ConnectorDirection.South => ConnectorDirection.West,
                    ConnectorDirection.West => ConnectorDirection.North,
                    _ => ConnectorDirection.Unknown
                };
            }
            return d;
        }

        public static float EulerToRadCardinals(int euler)
        {
            switch (euler)
            {
                case 0:
                    return 0;
                case 90:
                    return 1.57079632f;
                case 180:
                    return 3.14159f;
                case 270:
                    return 4.71239f;
            }
            return 0;
        }

        public static P3Float RotationToP3Float(int yawSteps)
        {
            // Put yaw on Z assuming X/Y plane.
            return new P3Float(0, 0, EulerToRadCardinals(yawSteps * 90));
        }
    }


    public struct RgConnectorInstance
    {
        public string EditorId;          // rg_conn_n_D1_station
        public RgConnector Parsed;       // parsed metadata
        public P3Float LocalPos;         // marker.Position (local space)
    }

    public struct OpenConnector
    {
        public RgConnector Parsed;       // direction/door/tileset
        public int YawSteps;         // 0..3
        public P3Float WorldPos;         // world-space position of this connector
        public string DistrictType;      // owning room's district (e.g., trunk, hab)
    }

    public struct PlacedRoom
    {
        public RoomPrefab Prefab;
        public P3Float WorldPos;
        public int YawSteps; // 0..3
        public string DistrictType;
        public List<RgConnectorInstance> Connectors;
    }

    public struct RgConnector
    {
        public string RawEditorId;

        public ConnectorDirection Direction;
        public string DoorSize;
        public string Tileset;

        public bool IsValid;
    }
    public enum ConnectorDirection
    {
        North,
        South,
        East,
        West,
        Unknown
    }

    public class RetrogradeUtils
    {
        public static Cell CloneCellById(string id)
        {
            for (int i = 0; i < gen_quest_main.myMod.Cells.Count; i++)
            {
                for (int j = 0; j < gen_quest_main.myMod.Cells[i].SubBlocks.Count; j++)
                {
                    for (int k = 0; k < gen_quest_main.myMod.Cells[i].SubBlocks[j].Cells.Count; k++)
                    {
                        if (gen_quest_main.myMod.Cells[i].SubBlocks[j].Cells[k].EditorID == id)
                        {
                            var refcell = gen_quest_main.myMod.Cells[i].SubBlocks[j].Cells[k].DeepCopy();

                            var persistantItems = new Noggog.ExtendedList<IPlaced>();
                            foreach (var item in refcell.Persistent)
                            {
                                PlacedObject poref = (PlacedObject)item;
                                PlacedObject placedObject = new PlacedObject(gen_quest_main.myMod)
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
                                    LightGobo = poref.LightGobo,
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
                                    XCZA = poref.XCZA,
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
                                PlacedObject placedObject = new PlacedObject(gen_quest_main.myMod)
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
                                    LightGobo = poref.LightGobo,
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
                                    XCZA = poref.XCZA,
                                    XFLG = poref.XFLG,
                                    XNSE = poref.XNSE,
                                    XPCK = poref.XPCK
                                };
                                tempItems.Add(placedObject);
                            }

                            Cell cell = new Cell(gen_quest_main.myMod)
                            {
                                AcousticSpace = refcell.AcousticSpace,
                                CellSkyRegion = refcell.CellSkyRegion,
                                Components = refcell.Components,
                                EditorID = id,
                                EncounterLocation = refcell.EncounterLocation,
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
                                MHDT = refcell.MHDT,
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
                                XCIB = refcell.XCIB,
                                WaterEnvironmentMap = refcell.WaterEnvironmentMap,
                                WaterHeight = refcell.WaterHeight,
                                WaterType = refcell.WaterType,
                                WaterVelocity = refcell.WaterVelocity,
                                XCLAs = refcell.XCLAs,
                                XILS = refcell.XILS,
                                XWCN = refcell.XWCN
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
