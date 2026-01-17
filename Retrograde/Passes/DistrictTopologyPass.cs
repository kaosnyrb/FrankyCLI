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
        private static readonly string[] DefaultBridgeRoomLists = new[] { "rg_trunklist", "rg_bridgelist" };
        int maxRoomsToPlace = 10;

        public DistrictTopologyPass(string p_roomlist,int roomtarget, string districtType = null) {         
            district = districtType;
            roomlist = p_roomlist;
            districtTypeLabel = DeriveDistrictType(p_roomlist, districtType, "district");
            maxRoomsToPlace = roomtarget;
        }
        public void RunPass(DungeonState state)
        {
            // Inputs / knobs
            
            int maxAttempts = 1000;              // hard limit (failed tries) to avoid infinite loops
            float collisionPadding = -0.1f; // tweak: world units clearance
            int maxCandidatePrefabsPerConnector = 16; // avoid thrashing on a single open connector
            int proximitySample = 5; // bias: pick from the closest N connectors to keep the cluster tight
            int maxPlans = state.scoringSystem?.Effort ?? 100; // retry count for full planning attempts
            const float connectorEmbedTolerance = 0.01f; // prevent connectors from sitting inside other room bounds
            float bridgeMaxHorizontalSpan = 40f; // keep connectors within ranges bridge prefabs can span
            float bridgeMaxVerticalOffset = 8f;
            const int targetBridgeCount = 50; // aim to leave enough pairs for bridge pass
            RoomUtils roomUtils = new RoomUtils(roomlist);
            var bridgePrefabKeys = BridgeUtil.BuildBridgePrefabKeys(ResolveBridgeRoomLists(state));


            int bestBridgeablePairs = -1;
            List<PlacedRoom> bestPlannedRooms = null;
            List<OpenConnector> bestPlannedOpenConnectors = null;
            List<PlacedObject> bestPlannedPlacements = null;
            int bestRoomsPlaced = 0;
            double bestPlanScore = double.MinValue;
            PlanScore? bestPlanScoreBreakdown = null;
            int bestPlanAttempt = -1;
            float bestYMin = state.YMin;
            int bestNewConnectors = 0;

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
                int connectorsAddedCount = 0;

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
                    int bestBridgeScore = BridgeUtil.CountBridgeablePairs(plannedOpenConnectors, yMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, bridgePrefabKeys);

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
                            int bridgeScore = BridgeUtil.CountBridgeablePairs(connectorsAfterPlacement, yMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, bridgePrefabKeys);

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
                    connectorsAddedCount += bestNewOpenConnectors?.Count ?? 0;
                }

                var bridgeablePairs = BridgeUtil.CountBridgeablePairs(plannedOpenConnectors, yMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, bridgePrefabKeys);
                var planArea = ScoringUtil.CalculateTotalArea(plannedRooms);
                var planScore = ScoringUtil.ScorePlan(state.scoringSystem, roomsPlaced, bridgeablePairs, 0, connectorsAddedCount, planArea);
                if (planScore.Total > bestPlanScore)
                {
                    bestBridgeablePairs = bridgeablePairs;
                    bestPlannedRooms = plannedRooms;
                    bestPlannedOpenConnectors = plannedOpenConnectors;
                    bestPlannedPlacements = plannedPlacements;
                    bestRoomsPlaced = roomsPlaced;
                    bestPlanScore = planScore.Total;
                    bestPlanScoreBreakdown = planScore;
                    bestPlanAttempt = planAttempt;
                    bestYMin = yMin;
                    bestNewConnectors = connectorsAddedCount;
                }
            }

            var finalRooms = bestPlannedRooms ?? new List<PlacedRoom>();
            var finalOpenConnectors = bestPlannedOpenConnectors ?? new List<OpenConnector>();
            var finalPlacements = bestPlannedPlacements ?? new List<PlacedObject>();
            var finalNewConnectors = bestNewConnectors;
            var finalScore = bestPlanScoreBreakdown ?? new PlanScore
            {
                Total = 0,
                Components = new Dictionary<string, double>
                {
                    { "Placement", 0 },
                    { "Bridging", 0 },
                    { "BridgingOverlap", 0 },
                    { "NewConnectors", 0 },
                    { "Area", 0 }
                }
            };

            foreach (var placement in finalPlacements)
            {
                state.instance.Temporary.Add(placement);
            }
            state.placedRooms = finalRooms;
            state.openConnectors = finalOpenConnectors;
            state.YMin = bestYMin;

            Console.WriteLine($"[District plan] best of {maxPlans} attempts (attempt {bestPlanAttempt + 1}): placed {bestRoomsPlaced}/{maxRoomsToPlace} rooms, bridgeable pairs {bestBridgeablePairs}/{targetBridgeCount}, new connectors {finalNewConnectors}, score {finalScore.Total:0.00} (placement {finalScore.Components["Placement"]:0.00}, bridging {finalScore.Components["Bridging"]:0.00}, new connectors {finalScore.Components["NewConnectors"]:0.00}, area {finalScore.Components["Area"]:0.00}).");
        }

        private static int ChooseConnectorIndexNearCenter(List<OpenConnector> openConnectors, P3Float clusterCenter, int sampleSize)
        {
            var prioritized = openConnectors
                .Select((c, idx) => new
                {
                    Index = idx,
                    DistSq = MathUtil.DistanceSquared(c.WorldPos, clusterCenter)
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

        private static List<string> ResolveBridgeRoomLists(DungeonState state)
        {
            if (state?.TrunkRoomLists != null && state.TrunkRoomLists.Count > 0)
                return state.TrunkRoomLists;

            return DefaultBridgeRoomLists.ToList();
        }

    }
}
