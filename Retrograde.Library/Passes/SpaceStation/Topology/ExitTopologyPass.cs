using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Passes.SpaceStation
{
    /// <summary>
    /// Places a single exit prefab connected to the boss room.
    /// Unlike other topology passes, this expands the prefab contents into the world
    /// individually so the exit door (rg_exitdoor_01) can be tracked for downstream processing.
    /// Falls back to the nearest available connector if the boss room has no compatible openings.
    /// </summary>
    public class ExitTopologyPass : IGenPass
    {
        private const int MaxRoomsToPlace = 1;
        private const int MaxAttempts = 100;
        private const float CollisionPadding = -1.5f;
        private const string ExitDoorEditorId = "rg_exitdoor_01";

        private class ExitPlanMeta
        {
            public int RoomsPlaced;
            public bool ConnectedToBoss;
            public float DistanceToBoss;
            public PlacedRoom ExitRoom;
        }

        /// <summary>
        /// Result of attempting to place an exit room at a connector.
        /// Stores geometry for planning but defers actual object placement to Stage 4.
        /// </summary>
        private class ExitPlacementResult
        {
            public PlacedRoom PlacedRoom { get; set; }
            public List<OpenConnector> NewConnectors { get; set; }
        }

        public void RunPass(DungeonState state)
        {
            int maxPlans = state.scoringSystem?.Effort ?? 20;

            RoomUtils roomUtils = state.GetRoomUtils("rg_exitlist");

            // Find boss room position for proximity scoring
            P3Float bossRoomPos = FindBossRoomPosition(state.placedRooms);

            string chosenExitRoomEditorId = null;
            string GetOrChooseExitRoom(string tileset)
            {
                if (!string.IsNullOrWhiteSpace(chosenExitRoomEditorId))
                    return chosenExitRoomEditorId;

                chosenExitRoomEditorId = roomUtils.GetRoom(tileset, null);

                if (!state.IsHarnessRun)
                {
                    Console.WriteLine($"[Exit plan] Selected exit room prefab: {chosenExitRoomEditorId}");
                }
                return chosenExitRoomEditorId;
            }

            // Stage 1: Multi-plan generation
            var bestOutcome = PlanRunner.RunBest<ExitPlanMeta>(maxPlans, planAttempt =>
            {
                var plannedRooms = new List<PlacedRoom>(state.placedRooms);
                var plannedOpenConnectors = new List<OpenConnector>(state.openConnectors);

                int roomsPlaced = 0;
                int attempts = 0;
                bool connectedToBoss = false;
                float bestDistanceToBoss = float.MaxValue;
                PlacedRoom exitRoom = default;

                // Sort connectors: boss room connectors first, then by proximity to boss room
                var sortedConnectors = plannedOpenConnectors
                    .OrderBy(c => string.Equals(c.DistrictType, "boss", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(c => MathUtil.DistanceSquared(c.WorldPos, bossRoomPos))
                    .ToList();

                plannedOpenConnectors = new List<OpenConnector>(sortedConnectors);

                // Stage 2: Room placement loop
                int connectorIndex = 0;
                while (roomsPlaced < MaxRoomsToPlace && connectorIndex < plannedOpenConnectors.Count && attempts < MaxAttempts)
                {
                    attempts++;

                    var target = plannedOpenConnectors[connectorIndex];
                    plannedOpenConnectors.RemoveAt(connectorIndex);

                    if (target.WorldPos.Y < state.YMin)
                    {
                        continue;
                    }

                    // Stage 2a: Try to place exit room at selected connector
                    var exitPrefabEditorId = GetOrChooseExitRoom(target.Parsed.Tileset);
                    if (string.IsNullOrEmpty(exitPrefabEditorId))
                    {
                        plannedOpenConnectors.Add(target);
                        connectorIndex++;
                        continue;
                    }

                    var exitPrefab = PrefabCache.GetPrefab(exitPrefabEditorId);
                    var placementResult = TryPlaceExitRoomAtConnector(target, exitPrefab, plannedRooms, state.YMin);

                    if (placementResult == null)
                    {
                        plannedOpenConnectors.Add(target);
                        connectorIndex++;
                        continue;
                    }

                    // Stage 2b: Accept the placement
                    plannedRooms.Add(placementResult.PlacedRoom);
                    plannedOpenConnectors.AddRange(placementResult.NewConnectors);
                    roomsPlaced++;
                    exitRoom = placementResult.PlacedRoom;

                    connectedToBoss = string.Equals(target.DistrictType, "boss", StringComparison.OrdinalIgnoreCase);
                    bestDistanceToBoss = (float)Math.Sqrt(MathUtil.DistanceSquared(target.WorldPos, bossRoomPos));
                }

                // Stage 3: Score the plan - heavily prefer boss room connections
                double adjustedScore = double.MinValue;
                if (roomsPlaced > 0)
                {
                    double bossBonus = connectedToBoss ? 100000 : 0;
                    double proximityScore = 1000.0 / (1.0 + bestDistanceToBoss);
                    adjustedScore = bossBonus + proximityScore;
                }

                return new PlanOutcome<ExitPlanMeta>
                {
                    Score = adjustedScore,
                    Rooms = plannedRooms,
                    OpenConnectors = plannedOpenConnectors,
                    Placements = new List<PlacedObject>(), // Content placed individually in Stage 4
                    Metadata = new ExitPlanMeta
                    {
                        RoomsPlaced = roomsPlaced,
                        ConnectedToBoss = connectedToBoss,
                        DistanceToBoss = bestDistanceToBoss,
                        ExitRoom = exitRoom
                    }
                };
            });

            // Stage 4: Apply best plan - expand prefab contents into the world individually
            if (bestOutcome == null || (bestOutcome.Metadata?.RoomsPlaced ?? 0) == 0)
                throw new Exception("Couldn't place exit room");

            state.placedRooms = bestOutcome.Rooms;
            state.openConnectors = bestOutcome.OpenConnectors;

            var exitPlacement = bestOutcome.Metadata.ExitRoom;
            PlaceExitPrefabContents(state, exitPlacement);

            // Stage 5: Link exit door to entrance door bidirectionally
            LinkExitToEntrance(state);

            if (!state.IsHarnessRun)
            {
                var meta = bestOutcome.Metadata;
                string connectionInfo = meta.ConnectedToBoss
                    ? "directly connected to boss room"
                    : $"nearest to boss room (distance: {meta.DistanceToBoss:F1})";
                string doorInfo = state.ExitDoor != null ? ", exit door found" : ", WARNING: no exit door found";
                Console.WriteLine($"[Exit plan] best of {maxPlans} attempts: {connectionInfo}{doorInfo}");
            }
        }

        /// <summary>
        /// Expands the exit prefab contents into the world as individual PlacedObjects.
        /// Identifies the exit door (rg_exitdoor_01) and stores it in DungeonState for downstream passes.
        /// </summary>
        private static void PlaceExitPrefabContents(DungeonState state, PlacedRoom exitRoom)
        {
            var prefab = exitRoom.Prefab;
            var prefabCell = ResolvePrefabCell(prefab);
            if (prefabCell == null)
                throw new Exception($"Could not resolve prefab cell for exit room: {prefab.PrefabEditorId}");

            int objectsPlaced = 0;

            foreach (var entry in prefabCell.Temporary)
            {
                if (entry is IPlacedObjectGetter po)
                {
                    var placed = PlaceContentObject(state, po, exitRoom);
                    if (placed != null)
                    {
                        objectsPlaced++;
                        TryTrackExitDoor(state, po, placed);
                    }
                }
            }

            foreach (var entry in prefabCell.Persistent)
            {
                if (entry is IPlacedObjectGetter po)
                {
                    var placed = PlaceContentObject(state, po, exitRoom);
                    if (placed != null)
                    {
                        objectsPlaced++;
                        TryTrackExitDoor(state, po, placed);
                    }
                }
            }

            if (!state.IsHarnessRun)
                Console.WriteLine($"[Exit plan] Expanded {objectsPlaced} objects from prefab {prefab.PrefabEditorId}");
        }

        /// <summary>
        /// Creates a new PlacedObject in the world from a template mod prefab content object,
        /// applying the exit room's position and rotation transforms.
        /// Only clones objects whose base form is from Starfield.esm.
        /// </summary>
        private static PlacedObject? PlaceContentObject(DungeonState state, IPlacedObjectGetter source, PlacedRoom exitRoom)
        {
            if (source.Base.FormKey.ModKey.Name != "Starfield")
                return null;

            var rotatedLocal = RgRotation.RotateYaw90(source.Position, exitRoom.YawSteps);
            var worldPos = exitRoom.WorldPos + rotatedLocal;
            var worldRot = RgRotation.RotateYaw90(source.Rotation, exitRoom.YawSteps) + RgRotation.RotationToP3Float(exitRoom.YawSteps);

            var placed = new PlacedObject(RetrogradeContext.Current.TargetMod)
            {
                // --- Identity & base ---
                Base = source.Base.FormKey.ToNullableLink<IPlaceableObjectGetter>(),
                StarfieldMajorRecordFlags = source.StarfieldMajorRecordFlags,
                Scale = source.Scale,
                Count = source.Count,
                // --- Transform (overridden) ---
                Position = worldPos,
                Rotation = worldRot,
                // --- Simple value types (assign directly) ---
                Action = source.Action,
                BOLV = source.BOLV,
                BlueprintPartOrigin = source.BlueprintPartOrigin,
                FactionRank = source.FactionRank,
                GeometryDirtinessScale = source.GeometryDirtinessScale,
                HeadTrackingWeight = source.HeadTrackingWeight,
                HealthPercent = source.HealthPercent,
                IsActivationPoint = source.IsActivationPoint,
                IsIgnoredBySandbox = source.IsIgnoredBySandbox,
                IsLinkedRefTransient = source.IsLinkedRefTransient,
                LightArea = source.LightArea?.DeepCopy(),
                LightRoundedness = source.LightRoundedness?.DeepCopy(),
                LightStaticShadowMap = source.LightStaticShadowMap,
                LightVolumetricData = source.LightVolumetricData,
                NumTraversalFluffBytes = source.NumTraversalFluffBytes,
                OpenByDefault = source.OpenByDefault,
                Radius = source.Radius,
                RagdollBipedRotation = source.RagdollBipedRotation,
                XALG = source.XALG,
                // --- FormLink properties ---
                AttachRef = source.AttachRef?.FormKey.ToNullableLink<IPlacedObjectGetter>(),
                Emittance = source.Emittance?.FormKey.ToNullableLink<IEmittanceGetter>(),
                Layer = source.Layer?.FormKey.ToNullableLink<ILayerGetter>(),
                PersistentLocation = source.PersistentLocation?.FormKey.ToNullableLink<ILocationGetter>(),
                ReferenceGroup = source.ReferenceGroup?.FormKey.ToNullableLink<IReferenceGroupGetter>(),
                SourcePackIn = source.SourcePackIn?.FormKey.ToNullableLink<IPackInGetter>(),
                TeleportName = source.TeleportName?.FormKey.ToNullableLink<IMessageGetter>(),
                TimeOfDay = source.TimeOfDay?.FormKey.ToNullableLink<ITimeOfDayRecordGetter>(),
                XPCK = source.XPCK?.FormKey.ToNullableLink<IReferenceGroupGetter>(),
                // --- DeepCopy-able sub-objects ---
                Collision = source.Collision?.DeepCopy(),
                Comments = source.Comments,
                ConstrainedDecal = source.ConstrainedDecal,
                CurrentZoneCell = source.CurrentZoneCell?.DeepCopy(),
                DebugText = source.DebugText?.DeepCopy(),
                EnableParent = source.EnableParent?.DeepCopy(),
                ExternalEmittance = source.ExternalEmittance?.DeepCopy(),
                GoboAnimatedProperties = source.GoboAnimatedProperties?.DeepCopy(),
                GroupedPackIn = source.GroupedPackIn?.DeepCopy(),
                LevelModifier = source.LevelModifier,
                LightBarndoorData = source.LightBarndoorData?.DeepCopy(),
                LightFlicker = source.LightFlicker?.DeepCopy(),
                Lighting = source.Lighting?.DeepCopy(),
                LightLayerData = source.LightLayerData,
                Lock = source.Lock?.DeepCopy(),
                MapMarker = source.MapMarker?.DeepCopy(),
                NavigationDoorLink = source.NavigationDoorLink?.DeepCopy(),
                Ownership = source.Ownership?.DeepCopy(),
                Patrol = source.Patrol?.DeepCopy(),
                Primitive = source.Primitive?.DeepCopy(),
                ProjectedDecal = source.ProjectedDecal?.DeepCopy(),
                ShipArrival = source.ShipArrival?.DeepCopy(),
                Spline = source.Spline?.DeepCopy(),
                TeleportDestination = source.TeleportDestination?.DeepCopy(),
                VolumeData = source.VolumeData?.DeepCopy(),
                VirtualMachineAdapter = source.VirtualMachineAdapter?.DeepCopy(),
                // --- Read-only collections: skip (not needed for prefab cloning) ---
                // Components, LayeredMaterialSwaps, LightColors, LinkedReferences,
                // LocationRefTypes, PowerLinks, ProjectedDecalReferences, Properties,
                // RagdollData, SnapLinks, Traversals, PlacedObjectXCZRXCZA
            };

            state.PlacementUtil.AddToTemporary(state.instance, placed);
            return placed;
        }

        /// <summary>
        /// Checks if the source object is the exit door and stores a reference in DungeonState.
        /// </summary>
        private static void TryTrackExitDoor(DungeonState state, IPlacedObjectGetter source, PlacedObject placed)
        {
            if (string.Equals(source.EditorID, ExitDoorEditorId, StringComparison.OrdinalIgnoreCase))
            {
                state.ExitDoor = placed;
            }
        }

        /// <summary>
        /// Sets the exit door's teleport destination to the entrance door (one-way only).
        /// The entrance door is NOT linked back, so the player cannot re-enter from the exit.
        /// </summary>
        private static void LinkExitToEntrance(DungeonState state)
        {
            if (state.ExitDoor == null || state.EntranceDoor == null)
                return;

            //state.ExitDoor.TeleportDestination = new TeleportDestination();
            state.ExitDoor.TeleportDestination.Door = state.EntranceDoor.ToLink<IPlacedObjectGetter>();
        }

        /// <summary>
        /// Finds the world position of the boss room, or returns the dungeon start if no boss room exists.
        /// </summary>
        private static P3Float FindBossRoomPosition(List<PlacedRoom> placedRooms)
        {
            foreach (var room in placedRooms)
            {
                if (string.Equals(room.DistrictType, "boss", StringComparison.OrdinalIgnoreCase))
                    return room.WorldPos;
            }
            return new P3Float(0, 0, 0);
        }

        /// <summary>
        /// Attempts to place an exit room at the target connector by evaluating all rotations.
        /// Only checks geometry/collision - actual content placement is deferred to Stage 4.
        /// </summary>
        private static ExitPlacementResult TryPlaceExitRoomAtConnector(
            OpenConnector target,
            RoomPrefab exitPrefab,
            List<PlacedRoom> plannedRooms,
            float yMin)
        {
            var requiredDir = ConnectorUtils.Opposite(target.Parsed.Direction);

            var yawOrder = Enumerable.Range(0, 4)
                .OrderBy(_ => RandomProvider.Random.Next())
                .ToList();

            foreach (var yawSteps in yawOrder)
            {
                var nextConnectors = ConnectorUtils.GetConnectors(exitPrefab, yawSteps);

                var compatible = nextConnectors
                    .Where(c =>
                        c.Parsed.Direction == requiredDir &&
                        string.Equals(c.Parsed.DoorSize, target.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Parsed.Tileset, target.Parsed.Tileset, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (compatible.Count == 0)
                    continue;

                var chosen = compatible[RandomProvider.Random.Next(compatible.Count)];

                P3Float nextPos = target.WorldPos - chosen.LocalPos;

                var candidateAabb = ConnectorUtils.ToWorldAabbRotated(exitPrefab.packin_instance.ObjectBounds, nextPos, yawSteps);
                if (ConnectorUtils.IsBelowYMin(candidateAabb, yMin))
                    continue;
                if (ConnectorUtils.CollidesWithAny(candidateAabb, plannedRooms, CollisionPadding))
                    continue;

                var placedRoom = new PlacedRoom
                {
                    Prefab = exitPrefab,
                    WorldPos = nextPos,
                    YawSteps = yawSteps,
                    DistrictType = "exit",
                    Connectors = nextConnectors
                };

                var newConnectors = new List<OpenConnector>();
                foreach (var c in nextConnectors)
                {
                    if (c.EditorId == chosen.EditorId && c.LocalPos.Equals(chosen.LocalPos))
                        continue;

                    newConnectors.Add(new OpenConnector
                    {
                        Parsed = c.Parsed,
                        YawSteps = yawSteps,
                        WorldPos = nextPos + c.LocalPos,
                        DistrictType = "exit"
                    });
                }

                return new ExitPlacementResult
                {
                    PlacedRoom = placedRoom,
                    NewConnectors = newConnectors
                };
            }

            return null;
        }

        private static ICellGetter? ResolvePrefabCell(RoomPrefab prefab)
        {
            var cellFormKey = prefab.packin_instance?.Cell?.FormKey;
            if (cellFormKey == null)
                return null;

            foreach (var mod in RetrogradeContext.Current.TemplateMods)
                foreach (var block in mod.Cells)
                    foreach (var sub in block.SubBlocks)
                        foreach (var cell in sub.Cells)
                            if (cell.FormKey == cellFormKey)
                                return cell;

            return null;
        }
    }
}
