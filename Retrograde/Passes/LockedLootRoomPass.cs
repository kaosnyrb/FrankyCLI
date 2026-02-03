using FrankyCLI.questgen_tools;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI.Retrograde.Passes
{
    /// <summary>
    /// Prototype pass that creates a locked loot room scenario:
    /// 1. Designates an existing room as the "loot room"
    /// 2. Places a locked door on the connector leading to that room
    /// 3. Places a key in an earlier room that unlocks the door
    ///
    /// TODOs for later:
    /// - Add actual key item prefab (currently uses placeholder)
    /// - Add dedicated loot room prefab/room list
    /// - Wire up key FormKey to door's Lock.Key property
    /// </summary>
    public class LockedLootRoomPass : IGenPass
    {
        // TODO: Replace with actual key prefab when available
        private const string KeyPrefabId = "rg_key_item";

        // Stores the FormKey of the door base object found in the prefab
        private FormKey? _doorBaseFormKey;

        // Stores the placed door reference for wiring up the key later
        private PlacedObject _placedDoor;

        public void RunPass(DungeonState state)
        {
            if (state?.placedRooms == null || state.placedRooms.Count < 3)
                return; // Need at least 3 rooms for meaningful key placement

            // Step 1: Select the loot room (furthest from starting position)
            var lootRoom = SelectLootRoom(state);
            if (lootRoom == null)
                return;

            // Step 2: Find the connector/door leading into the loot room
            var doorConnection = FindDoorConnection(state, lootRoom.Value);
            if (doorConnection == null)
                return;

            // Step 3: Place the locked door
            PlaceLockedDoor(state, doorConnection.Value);

            // Step 4: Select a room for key placement (away from loot room, closer to start)
            var keyRoom = SelectKeyRoom(state, lootRoom.Value);
            if (keyRoom == null)
                return;

            // Step 5: Place the key in the key room
            PlaceKey(state, keyRoom.Value);
        }

        /// <summary>
        /// Selects the room to be the loot room.
        /// Currently picks the room furthest from the starting position,
        /// preferring non-trunk rooms.
        /// </summary>
        private PlacedRoom? SelectLootRoom(DungeonState state)
        {
            PlacedRoom? bestRoom = null;
            float maxDistance = -1f;

            foreach (var room in state.placedRooms)
            {
                // Skip trunk rooms - they're corridors, not good loot destinations
                if (room.DistrictType?.Contains("trunk", StringComparison.OrdinalIgnoreCase) == true)
                    continue;

                // Skip boss rooms - they have their own rewards
                if (room.DistrictType?.Contains("boss", StringComparison.OrdinalIgnoreCase) == true)
                    continue;

                float distance = MathUtil.DistanceSquared(room.WorldPos, state.StartingPosition);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    bestRoom = room;
                }
            }

            // Fallback: if no suitable room found, pick any non-trunk room
            if (bestRoom == null)
            {
                bestRoom = state.placedRooms
                    .FirstOrDefault(r => r.DistrictType?.Contains("trunk", StringComparison.OrdinalIgnoreCase) != true);
            }

            return bestRoom;
        }

        /// <summary>
        /// Finds the connector pair between the loot room and an adjacent room.
        /// This is where we'll place the locked door.
        /// </summary>
        private DoorConnectionInfo? FindDoorConnection(DungeonState state, PlacedRoom lootRoom)
        {
            const float positionTolerance = 0.01f;

            foreach (var connector in lootRoom.Connectors)
            {
                var connectorWorldPos = lootRoom.WorldPos + connector.LocalPos;
                var requiredDir = ConnectorUtils.Opposite(connector.Parsed.Direction);

                // Find the matching connector from another room
                foreach (var otherRoom in state.placedRooms)
                {
                    if (otherRoom.WorldPos.Equals(lootRoom.WorldPos) &&
                        otherRoom.YawSteps == lootRoom.YawSteps)
                        continue; // Same room

                    foreach (var otherConnector in otherRoom.Connectors)
                    {
                        var otherWorldPos = otherRoom.WorldPos + otherConnector.LocalPos;

                        if (!ArePositionsClose(connectorWorldPos, otherWorldPos, positionTolerance))
                            continue;

                        if (otherConnector.Parsed.Direction != requiredDir)
                            continue;

                        if (!string.Equals(connector.Parsed.DoorSize, otherConnector.Parsed.DoorSize,
                            StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!string.Equals(connector.Parsed.Tileset, otherConnector.Parsed.Tileset,
                            StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Found a valid connection
                        return new DoorConnectionInfo
                        {
                            Position = connectorWorldPos,
                            Direction = connector.Parsed.Direction,
                            DoorSize = connector.Parsed.DoorSize,
                            Tileset = connector.Parsed.Tileset
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Places the locked door at the specified connection.
        /// Finds the actual door object inside the door prefab and places it with lock settings.
        /// </summary>
        private void PlaceLockedDoor(DungeonState state, DoorConnectionInfo connection)
        {
            // Get door prefab
            var doorId = ConnectorUtils.GetDoor(connection.DoorSize, connection.Tileset);

            RoomPrefab doorPrefab;
            try
            {
                doorPrefab = PrefabCache.GetPrefab(doorId);
            }
            catch
            {
                return; // Door prefab not found
            }

            if (doorPrefab?.packin_instance == null)
                return;

            // Find the door object inside the prefab
            var doorObjectInfo = FindDoorInPrefab(doorPrefab);
            if (doorObjectInfo == null)
                return;

            // Store the door base FormKey for later use
            _doorBaseFormKey = doorObjectInfo.Value.BaseFormKey;

            var requiredDir = ConnectorUtils.Opposite(connection.Direction);

            // Try all rotations to find correct orientation
            for (int yawSteps = 0; yawSteps < 4; yawSteps++)
            {
                var doorConnectors = ConnectorUtils.GetConnectors(doorPrefab, yawSteps);

                var attachConnector = doorConnectors.FirstOrDefault(c =>
                    c.Parsed.Direction == requiredDir &&
                    string.Equals(c.Parsed.DoorSize, connection.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.Parsed.Tileset, connection.Tileset, StringComparison.OrdinalIgnoreCase));

                if (!attachConnector.Parsed.IsValid)
                    continue;

                var doorPos = connection.Position - attachConnector.LocalPos;

                // Calculate the door object's world position
                var rotatedDoorLocal = RgRotation.RotateYaw90(doorObjectInfo.Value.LocalPosition, yawSteps);
                var doorObjectWorldPos = doorPos + rotatedDoorLocal;
                var doorObjectWorldRot = doorObjectInfo.Value.LocalRotation + RgRotation.RotationToP3Float(yawSteps);

                // Create the door PlacedObject with Lock settings
                _placedDoor = new PlacedObject(gen_quest_main.myMod)
                {
                    Count = 1,
                    Rotation = doorObjectWorldRot,
                    Position = doorObjectWorldPos,
                    Base = new FormLink<IPlaceableObjectGetter>(doorObjectInfo.Value.BaseFormKey),
                    Lock = new LockData
                    {
                        Level = LockLevel.RequiresKey,
                        // TODO: Set Key property to link to the actual key item
                        // Key = keyFormLink
                    }
                };

                state.PlacementUtil.AddToTemporary(state.instance, _placedDoor);

                return;
            }
        }

        /// <summary>
        /// Finds the door object inside a door prefab by searching the prefab's cell.
        /// Returns the door's base FormKey and local position/rotation.
        /// </summary>
        private DoorObjectInfo? FindDoorInPrefab(RoomPrefab prefab)
        {
            var prefabCell = ResolvePrefabCell(prefab);
            if (prefabCell == null)
                return null;

            // Search Temporary list for door objects
            foreach (var entry in prefabCell.Temporary)
            {
                if (entry is PlacedObject po && IsDoorObject(po))
                {
                    return new DoorObjectInfo
                    {
                        BaseFormKey = po.Base.FormKey,
                        LocalPosition = po.Position,
                        LocalRotation = po.Rotation
                    };
                }
            }

            // Search Persistent list for door objects
            foreach (var entry in prefabCell.Persistent)
            {
                if (entry is PlacedObject po && IsDoorObject(po))
                {
                    return new DoorObjectInfo
                    {
                        BaseFormKey = po.Base.FormKey,
                        LocalPosition = po.Position,
                        LocalRotation = po.Rotation
                    };
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a PlacedObject is a door by checking if its Base is a Door record.
        /// </summary>
        private static bool IsDoorObject(PlacedObject po)
        {
            if (po?.Base == null || po.Base.IsNull)
                return false;

            // Try to find the base in the Doors collection
            // Check both the mod and Starfield.esm
            try
            {
                if (gen_quest_main.myMod.Doors.ContainsKey(po.Base.FormKey))
                    return true;

                if (gen_quest_main._StarfieldMod.Doors.ContainsKey(po.Base.FormKey))
                    return true;
            }
            catch
            {
                // Collection access failed, fall back to EditorID check
            }

            // Fallback: check if EditorID contains "door" (case insensitive)
            if (po.EditorID?.Contains("door", StringComparison.OrdinalIgnoreCase) == true)
                return true;

            return false;
        }

        /// <summary>
        /// Resolves the Cell associated with a prefab's PackIn.
        /// </summary>
        private static Cell ResolvePrefabCell(RoomPrefab prefab)
        {
            var cellFormKey = prefab.packin_instance?.Cell?.FormKey;
            if (cellFormKey == null)
                return null;

            for (int i = 0; i < gen_quest_main.myMod.Cells.Count; i++)
            {
                for (int j = 0; j < gen_quest_main.myMod.Cells[i].SubBlocks.Count; j++)
                {
                    for (int k = 0; k < gen_quest_main.myMod.Cells[i].SubBlocks[j].Cells.Count; k++)
                    {
                        if (gen_quest_main.myMod.Cells[i].SubBlocks[j].Cells[k].FormKey == cellFormKey)
                        {
                            return gen_quest_main.myMod.Cells[i].SubBlocks[j].Cells[k];
                        }
                    }
                }
            }

            return null;
        }

        private struct DoorObjectInfo
        {
            public FormKey BaseFormKey;
            public P3Float LocalPosition;
            public P3Float LocalRotation;
        }

        /// <summary>
        /// Selects a room to place the key in.
        /// Prefers rooms closer to the start and away from the loot room.
        /// </summary>
        private PlacedRoom? SelectKeyRoom(DungeonState state, PlacedRoom lootRoom)
        {
            PlacedRoom? bestRoom = null;
            float bestScore = float.MinValue;

            foreach (var room in state.placedRooms)
            {
                // Don't place key in the loot room itself
                if (room.WorldPos.Equals(lootRoom.WorldPos) && room.YawSteps == lootRoom.YawSteps)
                    continue;

                // Calculate score: prefer rooms close to start and far from loot room
                float distanceFromStart = (float)Math.Sqrt(MathUtil.DistanceSquared(room.WorldPos, state.StartingPosition));
                float distanceFromLoot = (float)Math.Sqrt(MathUtil.DistanceSquared(room.WorldPos, lootRoom.WorldPos));

                // Score: closer to start = better, further from loot = better
                float score = distanceFromLoot - distanceFromStart;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestRoom = room;
                }
            }

            return bestRoom;
        }

        /// <summary>
        /// Places the key item in the specified room.
        /// TODO: Currently uses placeholder prefab - needs actual key item.
        /// </summary>
        private void PlaceKey(DungeonState state, PlacedRoom keyRoom)
        {
            RoomPrefab keyPrefab;
            try
            {
                keyPrefab = PrefabCache.GetPrefab(KeyPrefabId);
            }
            catch
            {
                // TODO: Key prefab not found - this is expected until we have actual key items
                // For now, skip key placement
                return;
            }

            if (keyPrefab?.packin_instance == null)
                return;

            // Place key at room center
            // TODO: Could place at a marker position for better placement
            var keyPos = keyRoom.WorldPos;

            state.PlacementUtil.AddToTemporary(state.instance, new PlacedObject(gen_quest_main.myMod)
            {
                Count = 1,
                Rotation = new P3Float(0, 0, 0),
                Position = keyPos,
                Base = keyPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
            });

            // TODO: Associate this key with the locked door via keyword/quest state
        }

        private static bool ArePositionsClose(P3Float a, P3Float b, float tolerance)
        {
            return Math.Abs(a.X - b.X) <= tolerance &&
                   Math.Abs(a.Y - b.Y) <= tolerance &&
                   Math.Abs(a.Z - b.Z) <= tolerance;
        }

        private struct DoorConnectionInfo
        {
            public P3Float Position;
            public ConnectorDirection Direction;
            public string DoorSize;
            public string Tileset;
        }
    }
}
