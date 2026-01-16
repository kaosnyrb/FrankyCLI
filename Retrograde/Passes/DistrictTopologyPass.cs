using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde;
using FrankyCLI.Retrograde.Passes;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI
{
    public class DistrictTopologyPass : IGenPass
    {
        string district = null;
        public string roomlist = "";
        private readonly string districtTypeLabel;
        private static readonly string[] BridgeRoomLists = new[] { "rg_trunklist", "rg_bridgelist" };
        private static readonly Lazy<HashSet<string>> BridgePrefabKeys = new Lazy<HashSet<string>>(BuildBridgePrefabKeys);

        public DistrictTopologyPass(string p_roomlist, string districtType = null) {         
            district = districtType;
            roomlist = p_roomlist;
            districtTypeLabel = DeriveDistrictType(p_roomlist, districtType, "district");
        }
        public void RunPass(DungeonState state)
        {
            // Inputs / knobs
            int maxRoomsToPlace = 10;          // hard limit (rooms)
            int maxAttempts = 1000;              // hard limit (failed tries) to avoid infinite loops
            float collisionPadding = -0.1f; // tweak: world units clearance
            int maxCandidatePrefabsPerConnector = 16; // avoid thrashing on a single open connector
            int proximitySample = 5; // bias: pick from the closest N connectors to keep the cluster tight
            const int maxPlans = 100; // retry count for full planning attempts
            const float connectorEmbedTolerance = 0.01f; // prevent connectors from sitting inside other room bounds
            float bridgeMaxHorizontalSpan = 40f; // keep connectors within ranges bridge prefabs can span
            float bridgeMaxVerticalOffset = 8f;
            const int targetBridgeCount = 50; // aim to leave enough pairs for bridge pass
            RoomUtils roomUtils = new RoomUtils(roomlist);

            //Sizing tweaks
            switch (state.Size)
            {
                case "Small":
                    maxRoomsToPlace = 3 + RandomUtils.random.Next(2);
                    break;
                case "Medium":
                    maxRoomsToPlace = 4 + RandomUtils.random.Next(4);
                    break;
                case "Large":
                    maxRoomsToPlace = 6 + RandomUtils.random.Next(2);
                    break;
            }


            int bestBridgeablePairs = -1;
            int initialPlacedCount = state.placedRooms?.Count ?? 0;
            List<PlacedRoom> bestPlannedRooms = null;
            List<OpenConnector> bestPlannedOpenConnectors = null;
            List<PlacedObject> bestPlannedPlacements = null;

            for (int planAttempt = 0; planAttempt < maxPlans; planAttempt++)
            {
                var usedPrefabIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var room in state.placedRooms)
                {
                    if (!string.IsNullOrEmpty(room.Prefab?.PrefabEditorId))
                    {
                        usedPrefabIds.Add(room.Prefab.PrefabEditorId);
                    }
                }

                var plannedRooms = new List<PlacedRoom>(state.placedRooms);
                var plannedOpenConnectors = new List<OpenConnector>(state.openConnectors);
                var plannedPlacements = new List<PlacedObject>();

                int roomsPlaced = 0;
                int attempts = 0;
                var yMin = state.YMin;

                // Main placement loop: iterates over open connectors, but bounded
                while (roomsPlaced < maxRoomsToPlace && plannedOpenConnectors.Count > 0 && attempts < maxAttempts)
                {
                    attempts++;

                    // Choose an open connector near the current cluster center to keep rooms close together
                    var clusterCenter = CalculateClusterCenter(plannedRooms, plannedOpenConnectors);
                    int openIndex = ChooseConnectorIndexNearCenter(plannedOpenConnectors, clusterCenter, proximitySample);
                    var target = plannedOpenConnectors[openIndex];

                    if (target.WorldPos.Y < yMin)
                    {
                        continue;
                    }

                    // Remove it now to ensure we "try to iterate through all open connectors"
                    plannedOpenConnectors.RemoveAt(openIndex);

                    // We need a connector on nextPrefab that is OPPOSITE direction to target,
                    // and compatible on door/tileset (simple equality checks here).
                    var requiredDir = ConnectorUtils.Opposite(target.Parsed.Direction);

                    var bestPlacement = (PlacedObject)null;
                    PlacedRoom bestRoom = new PlacedRoom();
                    List<OpenConnector> bestNewOpenConnectors = null;
                    int bestBridgeScore = CountBridgeablePairs(plannedOpenConnectors, yMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, BridgePrefabKeys.Value);

                    for (int prefabTry = 0; prefabTry < maxCandidatePrefabsPerConnector; prefabTry++)
                    {
                        var prefabId = ChoosePrefabId(roomUtils, target.Parsed.Tileset, district, usedPrefabIds);
                        var nextPrefab = new RoomPrefab(prefabId);

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

                            // Align using ROTATED local connector
                            P3Float nextPos = target.WorldPos - chosen.LocalPos;

                            // Collision using ROTATED bounds
                            var candidateAabb = ConnectorUtils.ToWorldAabbRotated(nextPrefab.packin_instance.ObjectBounds, nextPos, yawSteps);
                            if (ConnectorUtils.CollidesWithAny(candidateAabb, plannedRooms, collisionPadding))
                                continue;
                            if (AnyConnectorInsideExistingBounds(nextConnectors, nextPos, plannedRooms, connectorEmbedTolerance))
                                continue;
                            if (AnyExistingConnectorInsideCandidate(candidateAabb, plannedRooms, connectorEmbedTolerance))
                                continue;

                            var candidateRoom = new PlacedRoom
                            {
                                Prefab = nextPrefab,
                                WorldPos = nextPos,
                                YawSteps = yawSteps,
                                DistrictType = districtTypeLabel,
                                Connectors = nextConnectors
                            };

                            var candidatePlacement = new PlacedObject(gen_quest_main.myMod)
                            {
                                Count = 1,
                                Rotation = RgRotation.RotationToP3Float(yawSteps),
                                Position = nextPos,
                                Base = nextPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                            };

                            var newOpenConnectors = BuildOpenConnectors(nextConnectors, chosen, yawSteps, nextPos, districtTypeLabel);
                            var connectorsAfterPlacement = new List<OpenConnector>(plannedOpenConnectors);
                            connectorsAfterPlacement.AddRange(newOpenConnectors);
                            int bridgeScore = CountBridgeablePairs(connectorsAfterPlacement, yMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, BridgePrefabKeys.Value);

                            if (bestPlacement == null || bridgeScore > bestBridgeScore)
                            {
                                bestBridgeScore = bridgeScore;
                                bestPlacement = candidatePlacement;
                                bestRoom = candidateRoom;
                                bestNewOpenConnectors = newOpenConnectors;
                            }
                        }
                    }

                    // If we couldn't place anything for this connector, we just move on.
                    if (bestPlacement == null)
                    {
                        plannedOpenConnectors.Add(target);//Return it to the list so we close it later.
                        continue;
                    }

                    plannedPlacements.Add(bestPlacement);
                    plannedRooms.Add(bestRoom);
                    usedPrefabIds.Add(bestRoom.Prefab.PrefabEditorId);
                    roomsPlaced++;
                    plannedOpenConnectors.AddRange(bestNewOpenConnectors);
                }

                var bridgeablePairs = CountBridgeablePairs(plannedOpenConnectors, yMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, BridgePrefabKeys.Value);
                if (bridgeablePairs > bestBridgeablePairs)
                {
                    bestBridgeablePairs = bridgeablePairs;
                    bestPlannedRooms = plannedRooms;
                    bestPlannedOpenConnectors = plannedOpenConnectors;
                    bestPlannedPlacements = plannedPlacements;
                }

                bool success = roomsPlaced >= maxRoomsToPlace && bridgeablePairs >= targetBridgeCount;
                if (success || planAttempt == maxPlans - 1)
                {
                    var chosenRooms = success ? plannedRooms : bestPlannedRooms ?? plannedRooms;
                    var chosenOpenConnectors = success ? plannedOpenConnectors : bestPlannedOpenConnectors ?? plannedOpenConnectors;
                    var chosenPlacements = success ? plannedPlacements : bestPlannedPlacements ?? plannedPlacements;
                    int placedCount = success ? roomsPlaced : (chosenRooms.Count - initialPlacedCount);
                    int bridgeReport = success ? bridgeablePairs : bestBridgeablePairs;

                    foreach (var placement in chosenPlacements)
                    {
                        state.instance.Temporary.Add(placement);
                    }
                    state.placedRooms = chosenRooms;
                    state.openConnectors = chosenOpenConnectors;

                    var status = success ? "success" : "best-effort";
                    Console.WriteLine($"[District plan] {planAttempt + 1}/{maxPlans} {status}: placed {placedCount}/{maxRoomsToPlace} rooms, bridgeable pairs {bridgeReport}/{targetBridgeCount}.");
                    return;
                }
            }

            //Console.WriteLine("DistrictTopologyPass failed after {0} plan attempts.", maxPlans);
            throw new Exception("DistrictTopologyPass failed after "+ maxPlans+ " plan attempts." );
        }

        private static int ChooseConnectorIndexNearCenter(List<OpenConnector> openConnectors, P3Float clusterCenter, int sampleSize)
        {
            var prioritized = openConnectors
                .Select((c, idx) => new
                {
                    Index = idx,
                    DistSq = DistanceSquared(c.WorldPos, clusterCenter)
                })
                .OrderBy(p => p.DistSq)
                .ToList();

            int takeCount = Math.Min(sampleSize, prioritized.Count);
            return prioritized[RandomUtils.random.Next(takeCount)].Index;
        }

        private static P3Float CalculateClusterCenter(List<PlacedRoom> placedRooms, List<OpenConnector> openConnectors)
        {
            if (placedRooms.Count > 0)
            {
                float sumX = 0;
                float sumY = 0;
                float sumZ = 0;
                foreach (var room in placedRooms)
                {
                    sumX += room.WorldPos.X;
                    sumY += room.WorldPos.Y;
                    sumZ += room.WorldPos.Z;
                }

                float count = placedRooms.Count;
                return new P3Float(sumX / count, sumY / count, sumZ / count);
            }

            if (openConnectors.Count > 0)
            {
                float sumX = 0;
                float sumY = 0;
                float sumZ = 0;
                foreach (var connector in openConnectors)
                {
                    sumX += connector.WorldPos.X;
                    sumY += connector.WorldPos.Y;
                    sumZ += connector.WorldPos.Z;
                }

                float count = openConnectors.Count;
                return new P3Float(sumX / count, sumY / count, sumZ / count);
            }

            return new P3Float(0, 0, 0);
        }

        private static float DistanceSquared(P3Float a, P3Float b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        private static bool AnyConnectorInsideExistingBounds(
            List<RgConnectorInstance> connectors,
            P3Float roomWorldPos,
            List<PlacedRoom> placedRooms,
            float tolerance)
        {
            if (placedRooms == null || placedRooms.Count == 0)
                return false;

            foreach (var placed in placedRooms)
            {
                if (placed.Prefab?.packin_instance == null)
                    continue;

                var placedAabb = ConnectorUtils.ToWorldAabbRotated(placed.Prefab.packin_instance.ObjectBounds, placed.WorldPos, placed.YawSteps);

                foreach (var conn in connectors)
                {
                    var worldPos = roomWorldPos + conn.LocalPos;
                    if (IsPointStrictlyInside(worldPos, placedAabb, tolerance))
                        return true;
                }
            }

            return false;
        }

        private static bool AnyExistingConnectorInsideCandidate(
            RgAabb candidateAabb,
            List<PlacedRoom> placedRooms,
            float tolerance)
        {
            if (placedRooms == null || placedRooms.Count == 0)
                return false;

            foreach (var placed in placedRooms)
            {
                if (placed.Connectors == null)
                    continue;

                foreach (var conn in placed.Connectors)
                {
                    var worldPos = placed.WorldPos + conn.LocalPos;
                    if (IsPointStrictlyInside(worldPos, candidateAabb, tolerance))
                        return true;
                }
            }

            return false;
        }

        private static bool IsPointStrictlyInside(P3Float point, RgAabb aabb, float tolerance)
        {
            return point.X > aabb.Min.X + tolerance &&
                   point.X < aabb.Max.X - tolerance &&
                   point.Y > aabb.Min.Y + tolerance &&
                   point.Y < aabb.Max.Y - tolerance &&
                   point.Z > aabb.Min.Z + tolerance &&
                   point.Z < aabb.Max.Z - tolerance;
        }

        private static string ChoosePrefabId(
            RoomUtils roomUtils,
            string tileset,
            string district,
            HashSet<string> usedPrefabIds)
        {
            var listKey = roomUtils.listName + "_" + tileset;
            if (roomUtils.roomTemplates.TryGetValue(listKey, out var formList) &&
                formList?.Items != null &&
                formList.Items.Count > 0)
            {
                var allCandidates = new List<string>();

                foreach (var item in formList.Items)
                {
                    if (!gen_quest_main.myMod.PackIns.TryGetValue(item.FormKey, out var packIn) ||
                        string.IsNullOrEmpty(packIn?.EditorID))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(district) &&
                        !packIn.EditorID.Contains(district, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    allCandidates.Add(packIn.EditorID);
                }

                var unusedRooms = allCandidates
                    .Where(id => !usedPrefabIds.Contains(id) &&
                                 id.IndexOf("rg_blocker", StringComparison.OrdinalIgnoreCase) < 0)
                    .ToList();

                if (unusedRooms.Count > 0)
                    return unusedRooms[RandomUtils.random.Next(unusedRooms.Count)];

                var unusedAny = allCandidates
                    .Where(id => !usedPrefabIds.Contains(id))
                    .ToList();

                if (unusedAny.Count > 0)
                    return unusedAny[RandomUtils.random.Next(unusedAny.Count)];

                var rooms = allCandidates
                    .Where(id => id.IndexOf("rg_blocker", StringComparison.OrdinalIgnoreCase) < 0)
                    .ToList();

                if (rooms.Count > 0)
                    return rooms[RandomUtils.random.Next(rooms.Count)];

                if (allCandidates.Count > 0)
                    return allCandidates[RandomUtils.random.Next(allCandidates.Count)];
            }

            return roomUtils.GetRoom(tileset, district);
        }

        private static string DeriveDistrictType(string roomList, string provided, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(provided))
                return provided;

            if (string.IsNullOrWhiteSpace(roomList))
                return fallback;

            var normalized = roomList;
            if (normalized.StartsWith("rg_", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(3);
            if (normalized.EndsWith("list", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - 4);

            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }

        private static List<OpenConnector> BuildOpenConnectors(IEnumerable<RgConnectorInstance> connectors, RgConnectorInstance usedConnector, int yawSteps, P3Float roomPos, string districtType)
        {
            var open = new List<OpenConnector>();

            foreach (var c in connectors)
            {
                if (c.EditorId == usedConnector.EditorId && c.LocalPos.Equals(usedConnector.LocalPos))
                    continue;

                open.Add(new OpenConnector
                {
                    Parsed = c.Parsed,
                    YawSteps = yawSteps,
                    WorldPos = roomPos + c.LocalPos,
                    DistrictType = districtType
                });
            }

            return open;
        }

        private static int CountBridgeablePairs(List<OpenConnector> connectors, float yMin, float maxHorizontalSpan, float maxVerticalOffset, HashSet<string> bridgeKeys)
        {
            if (connectors == null || connectors.Count < 2)
                return 0;

            int count = 0;

            for (int i = 0; i < connectors.Count - 1; i++)
            {
                var a = connectors[i];
                if (!a.Parsed.IsValid || a.WorldPos.Y < yMin)
                    continue;

                for (int j = i + 1; j < connectors.Count; j++)
                {
                    var b = connectors[j];
                    if (!b.Parsed.IsValid || b.WorldPos.Y < yMin)
                        continue;

                    if (!string.Equals(a.Parsed.Tileset, b.Parsed.Tileset, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(a.Parsed.DoorSize, b.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase))
                        continue;

                    float dx = a.WorldPos.X - b.WorldPos.X;
                    float dy = a.WorldPos.Y - b.WorldPos.Y;
                    float dz = a.WorldPos.Z - b.WorldPos.Z;

                    if (MathF.Max(MathF.Abs(dx), MathF.Abs(dy)) > maxHorizontalSpan)
                        continue;

                    if (MathF.Abs(dz) > maxVerticalOffset)
                        continue;

                    if (bridgeKeys != null && bridgeKeys.Count > 0)
                    {
                        if (!TryBuildBridgeKey(a, b, out var key))
                            continue;
                        if (!bridgeKeys.Contains(key))
                            continue;
                    }

                    count++;
                }
            }

            return count;
        }

        private static bool TryBuildBridgeKey(OpenConnector a, OpenConnector b, out string key)
        {
            key = null;
            if (!a.Parsed.IsValid || !b.Parsed.IsValid)
                return false;

            var anchorTargetDir = ConnectorUtils.Opposite(a.Parsed.Direction);
            int yawToMatchA = DirectionToYawSteps(anchorTargetDir);
            if (yawToMatchA < 0)
                return false;

            var delta = b.WorldPos - a.WorldPos;
            var localDelta = RgRotation.RotateYaw90(delta, -yawToMatchA);

            int rx = (int)MathF.Round(localDelta.X);
            int ry = (int)MathF.Round(localDelta.Y);
            int rz = (int)MathF.Round(localDelta.Z);

            if (rx == 0 && ry == 0 && rz == 0)
                return false;

            var otherDir = RgRotation.RotateDir(
                ConnectorUtils.Opposite(b.Parsed.Direction),
                -yawToMatchA);

            key = $"{a.Parsed.Tileset}|{a.Parsed.DoorSize}|{yawToMatchA}|{otherDir}|{rx},{ry},{rz}";
            return true;
        }

        private static int DirectionToYawSteps(ConnectorDirection dir)
        {
            return dir switch
            {
                ConnectorDirection.North => 0,
                ConnectorDirection.East => 1,
                ConnectorDirection.South => 2,
                ConnectorDirection.West => 3,
                _ => -1
            };
        }

        private static HashSet<string> BuildBridgePrefabKeys()
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var list in BridgeRoomLists)
            {
                if (string.IsNullOrWhiteSpace(list))
                    continue;

                var utils = new RoomUtils(list);
                if (utils?.roomTemplates == null)
                    continue;

                foreach (var entry in utils.roomTemplates)
                {
                    var formList = entry.Value;
                    if (formList?.Items == null || formList.Items.Count == 0)
                        continue;

                    foreach (var item in formList.Items)
                    {
                        if (!gen_quest_main.myMod.PackIns.TryGetValue(item.FormKey, out var packIn))
                            continue;

                        var editorId = packIn?.EditorID;
                        if (string.IsNullOrWhiteSpace(editorId))
                            continue;

                        var prefab = new RoomPrefab(editorId);
                        var connectors = ConnectorUtils.GetConnectors(prefab);
                        if (connectors.Count < 2)
                            continue;

                        for (int i = 0; i < connectors.Count - 1; i++)
                        {
                            for (int j = i + 1; j < connectors.Count; j++)
                            {
                                TryRegisterPrefabSignature(connectors[i], connectors[j], keys);
                                TryRegisterPrefabSignature(connectors[j], connectors[i], keys);
                            }
                        }
                    }
                }
            }

            return keys;
        }

        private static void TryRegisterPrefabSignature(RgConnectorInstance anchor, RgConnectorInstance other, HashSet<string> keys)
        {
            if (!anchor.Parsed.IsValid || !other.Parsed.IsValid)
                return;

            var a = ToOpenConnector(anchor);
            var b = ToOpenConnector(other);

            if (!ArePairCompatible(a, b))
                return;

            if (!TryBuildBridgeKey(a, b, out var key))
                return;

            keys.Add(key);
        }

        private static OpenConnector ToOpenConnector(RgConnectorInstance conn)
        {
            return new OpenConnector
            {
                Parsed = new RgConnector
                {
                    RawEditorId = conn.Parsed.RawEditorId,
                    Direction = ConnectorUtils.Opposite(conn.Parsed.Direction),
                    DoorSize = conn.Parsed.DoorSize,
                    Tileset = conn.Parsed.Tileset,
                    IsValid = conn.Parsed.IsValid
                },
                WorldPos = conn.LocalPos,
                YawSteps = 0,
                DistrictType = null
            };
        }

        private static bool ArePairCompatible(OpenConnector a, OpenConnector b)
        {
            return a.Parsed.IsValid &&
                   b.Parsed.IsValid &&
                   string.Equals(a.Parsed.Tileset, b.Parsed.Tileset, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.Parsed.DoorSize, b.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase);
        }
    }
}
