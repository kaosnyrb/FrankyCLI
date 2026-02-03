using FrankyCLI.questgen_tools;
using Mutagen.Bethesda;
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
    /// - Add locked door prefab with key-lock mechanics (currently uses regular door)
    /// - Add dedicated loot room prefab/room list
    /// - Connect key/door via quest scripting or keyword system
    /// </summary>
    public class LockedLootRoomPass : IGenPass
    {
        // TODO: Replace with actual key prefab when available
        private const string KeyPrefabId = "rg_key_item";

        // TODO: Replace with locked door variant when available
        // For now uses regular door blocker as placeholder
        private const string LockedDoorPrefabId = null; // Will fall back to ConnectorUtils.GetDoor()

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
        /// TODO: Currently uses regular door - replace with locked variant.
        /// </summary>
        private void PlaceLockedDoor(DungeonState state, DoorConnectionInfo connection)
        {
            // Get door prefab - use locked door if available, otherwise fall back to regular
            var doorId = LockedDoorPrefabId ?? ConnectorUtils.GetDoor(connection.DoorSize, connection.Tileset);

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

                state.PlacementUtil.AddToTemporary(state.instance, new PlacedObject(gen_quest_main.myMod)
                {
                    Count = 1,
                    Rotation = RgRotation.RotationToP3Float(yawSteps),
                    Position = doorPos,
                    Base = doorPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                });

                // TODO: Associate this door with a key keyword/quest state
                // For now the door is placed but not actually "locked" in game terms

                return;
            }
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
