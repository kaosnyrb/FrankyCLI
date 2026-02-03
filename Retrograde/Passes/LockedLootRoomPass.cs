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
    /// 1. Places a new loot room from a prefab list
    /// 2. Creates a key by copying from Starfield.esm and adds to mod
    /// 3. Places a locked door on the connector leading to that room (linked to key)
    /// 4. Places the key in an earlier room
    ///
    /// TODOs for later:
    /// - Better key placement (use markers instead of room center)
    /// </summary>
    public class LockedLootRoomPass : IGenPass
    {
        private readonly string _roomList;
        private const string DistrictTypeLabel = "lootroom";

        // Stores the FormKey of the door base object found in the prefab
        private FormKey? _doorBaseFormKey;

        // Stores the placed door reference for wiring up the key later
        private PlacedObject _placedDoor;

        // The created key for this locked room
        private Key _createdKey;

        // The placed loot room
        private PlacedRoom? _placedLootRoom;

        // The connector used to attach the loot room (for door placement)
        private OpenConnector? _usedConnector;

        public LockedLootRoomPass(string roomList)
        {
            _roomList = roomList;
        }

        public void RunPass(DungeonState state)
        {
            if (state?.openConnectors == null || state.openConnectors.Count == 0)
                return; // Need open connectors to attach the loot room

            // Step 1: Place the loot room from prefab list
            if (!PlaceLootRoom(state))
                return;

            // Step 2: Create the key (need FormKey before placing door)
            CreateKey(state);
            if (_createdKey == null)
                return;

            // Step 3: Place the locked door at the connector used for the loot room
            if (_usedConnector.HasValue)
            {
                var doorConnection = new DoorConnectionInfo
                {
                    Position = _usedConnector.Value.WorldPos,
                    Direction = _usedConnector.Value.Parsed.Direction,
                    DoorSize = _usedConnector.Value.Parsed.DoorSize,
                    Tileset = _usedConnector.Value.Parsed.Tileset
                };
                PlaceLockedDoor(state, doorConnection);
            }

            // Step 4: Select a room for key placement (away from loot room, closer to start)
            if (!_placedLootRoom.HasValue)
                return;

            var keyRoom = SelectKeyRoom(state, _placedLootRoom.Value);
            if (keyRoom == null)
                return;

            // Step 5: Place the key in the key room
            PlaceKey(state, keyRoom.Value);

            var lootRoomName = _placedLootRoom?.Prefab?.PrefabEditorId ?? "unknown";
            var keyRoomName = keyRoom.Value.Prefab?.PrefabEditorId ?? "unknown";
            Console.WriteLine($"[LockedLootRoom] Placed loot room '{lootRoomName}', key in '{keyRoomName}'");
        }

        /// <summary>
        /// Places a new loot room from the prefab list at an open connector.
        /// </summary>
        private bool PlaceLootRoom(DungeonState state)
        {
            RoomUtils roomUtils = state.GetRoomUtils(_roomList);
            int maxAttempts = 100;
            float collisionPadding = -0.1f;

            for (int attempt = 0; attempt < maxAttempts && state.openConnectors.Count > 0; attempt++)
            {
                // Pick a random open connector
                int openIndex = RandomUtils.random.Next(state.openConnectors.Count);
                var target = state.openConnectors[openIndex];

                if (target.WorldPos.Y < state.YMin)
                    continue;

                var requiredDir = ConnectorUtils.Opposite(target.Parsed.Direction);

                // Try to find a compatible prefab
                string prefabId = ChoosePrefabId(roomUtils, target.Parsed.Tileset);
                if (string.IsNullOrEmpty(prefabId))
                    continue;

                var nextPrefab = PrefabCache.GetPrefab(prefabId);
                if (nextPrefab?.packin_instance == null)
                    continue;

                // Try all rotations
                for (int yawSteps = 0; yawSteps < 4; yawSteps++)
                {
                    var nextConnectors = ConnectorUtils.GetConnectors(nextPrefab, yawSteps);

                    var compatible = nextConnectors
                        .Where(c =>
                            c.Parsed.Direction == requiredDir &&
                            string.Equals(c.Parsed.DoorSize, target.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(c.Parsed.Tileset, target.Parsed.Tileset, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (compatible.Count == 0)
                        continue;

                    var chosen = compatible[RandomUtils.random.Next(compatible.Count)];

                    // Align using rotated local connector
                    P3Float nextPos = target.WorldPos - chosen.LocalPos;

                    // Collision check
                    var candidateAabb = ConnectorUtils.ToWorldAabbRotated(nextPrefab.packin_instance.ObjectBounds, nextPos, yawSteps);
                    if (ConnectorUtils.IsBelowYMin(candidateAabb, state.YMin))
                        continue;
                    if (ConnectorUtils.CollidesWithAny(candidateAabb, state.placedRooms, collisionPadding))
                        continue;

                    // Place the room
                    state.PlacementUtil.AddToTemporary(state.instance, new PlacedObject(gen_quest_main.myMod)
                    {
                        Count = 1,
                        Rotation = RgRotation.RotationToP3Float(yawSteps),
                        Position = nextPos,
                        Base = nextPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                    });

                    var placedRoom = new PlacedRoom
                    {
                        Prefab = nextPrefab,
                        WorldPos = nextPos,
                        YawSteps = yawSteps,
                        DistrictType = DistrictTypeLabel,
                        Connectors = nextConnectors
                    };

                    state.placedRooms.Add(placedRoom);
                    _placedLootRoom = placedRoom;
                    _usedConnector = target;

                    // Remove the used connector from open list
                    state.openConnectors.RemoveAt(openIndex);

                    // Add new connectors from the placed room (except the one we used)
                    foreach (var c in nextConnectors)
                    {
                        if (c.EditorId == chosen.EditorId && c.LocalPos.Equals(chosen.LocalPos))
                            continue;

                        state.openConnectors.Add(new OpenConnector
                        {
                            Parsed = c.Parsed,
                            YawSteps = yawSteps,
                            WorldPos = nextPos + c.LocalPos,
                            DistrictType = DistrictTypeLabel
                        });
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Chooses a prefab ID from the room list.
        /// </summary>
        private string ChoosePrefabId(RoomUtils roomUtils, string tileset)
        {
            var listKey = roomUtils.listName + "_" + tileset;
            if (roomUtils.roomTemplates.TryGetValue(listKey, out var formList) &&
                formList?.Items != null &&
                formList.Items.Count > 0)
            {
                var candidates = new List<string>();

                foreach (var item in formList.Items)
                {
                    if (!gen_quest_main.myMod.PackIns.TryGetValue(item.FormKey, out var packIn) ||
                        string.IsNullOrEmpty(packIn?.EditorID))
                    {
                        continue;
                    }

                    // Skip blockers
                    if (packIn.EditorID.IndexOf("rg_blocker", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    candidates.Add(packIn.EditorID);
                }

                if (candidates.Count > 0)
                    return candidates[RandomUtils.random.Next(candidates.Count)];
            }

            return null;
        }

        /// <summary>
        /// Creates a key by copying an existing key from Starfield.esm.
        /// The key is renamed using the station name and added to the current mod.
        /// </summary>
        private void CreateKey(DungeonState state)
        {
            // Find a key template from Starfield.esm
            Key templateKey = null;
            foreach (var key in gen_quest_main._StarfieldMod.Keys)
            {
                if (key != null)
                {
                    templateKey = key.DeepCopy();
                    break;
                }
            }

            if (templateKey == null)
                return;

            // Generate unique EditorID using station name
            var stationName = state.stateName ?? "LockedRoom";
            var sanitizedName = stationName.Replace(" ", "").Replace("-", "_");
            var editorId = $"rg_key_{sanitizedName}_{Guid.NewGuid().ToString().Substring(0, 8)}";

            // Create new key by deep copying the template
            _createdKey = templateKey.DeepCopy();
            _createdKey.EditorID = editorId;
            _createdKey.Name = $"{stationName} Key";

            // Add the key to the current mod
            gen_quest_main.myMod.Keys.Add(_createdKey);
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
                        Key = _createdKey.ToLink()
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
        /// Places the created key item in the specified room.
        /// </summary>
        private void PlaceKey(DungeonState state, PlacedRoom keyRoom)
        {
            if (_createdKey == null)
                return;

            // Place key at room center
            // TODO: Could place at a marker position for better placement
            var keyPos = keyRoom.WorldPos;

            state.PlacementUtil.AddToTemporary(state.instance, new PlacedObject(gen_quest_main.myMod)
            {
                Count = 1,
                Rotation = new P3Float(0, 0, 0),
                Position = keyPos,
                Base = _createdKey.ToLink<IPlaceableObjectGetter>()
            });
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
