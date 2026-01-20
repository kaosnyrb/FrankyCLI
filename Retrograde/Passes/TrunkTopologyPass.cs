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
    public class TrunkTopologyPass : IGenPass
    {
        private static readonly Random Rng = new Random();
        int maxRoomsToPlace = 10;          // hard limit (rooms)
        private static readonly List<string> PrefabsToForcePlacement = new List<string>
        {
            // Add prefab EditorIDs here to force a placement attempt for testing new prefabs.
            //"rg_sts_trk_big_006"
        };

        public TrunkTopologyPass(int roomtarget)
        {
            maxRoomsToPlace = roomtarget;
        }

        public void RunPass(DungeonState state)
        {
            const string districtType = "trunk";
            // Inputs / knobs            
            int maxAttempts = 1000;              // hard limit (failed tries) to avoid infinite loops
            float collisionPadding = -0.5f; // tweak: world units clearance
            int maxCandidatePrefabsPerConnector = 8; // avoid thrashing on a single open connector
            int maxPlans = state.scoringSystem.Effort; // cap number of planning retries
            float bridgeMaxHorizontalSpan = 40f; // keep connectors within ranges bridge prefabs can span
            float bridgeMaxVerticalOffset = 8f;
            var bridgePrefabKeys = BridgeUtil.BuildBridgePrefabKeys(state.TrunkRoomLists);

            var startingMarker = state.instance.Persistent
                .OfType<PlacedObject>()
                .FirstOrDefault(m => m.EditorID.Contains("rg_conn_n"));

            if (startingMarker == null) throw new Exception("rg_conn_n not found.");
            var startingConnector = RgConnectorParser.Parse(startingMarker.EditorID);

            state.StartingPosition = startingMarker.Position;

            RoomPrefab roomPrefab = null;
            PrefabMarker south0 = new PrefabMarker();
            PrefabMarker north0 = new PrefabMarker();
            RoomUtils roomUtils = new RoomUtils("rg_trunklist");

            int bestBridgeablePairs = -1;
            List<PlacedRoom> bestPlannedRooms = null;
            List<OpenConnector> bestPlannedOpenConnectors = null;
            List<PlacedObject> bestPlannedPlacements = null;
            int bestRoomsPlaced = 0;
            double bestPlanScoreWithPenalty = double.MinValue;
            PlanScore? bestPlanScoreBreakdown = null;
            int bestPlanAttempt = -1;
            float bestYMin = state.StartingPosition.Y;
            int bestNewConnectors = 0;
            int bestMissingRequiredPrefabs = PrefabsToForcePlacement.Count;

            for (int planAttempt = 0; planAttempt < maxPlans; planAttempt++)
            {
                var requiredPrefabs = PrefabsToForcePlacement
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList();
                var usedPrefabIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var room in state.placedRooms)
                {
                    if (!string.IsNullOrWhiteSpace(room.Prefab?.PrefabEditorId))
                    {
                        usedPrefabIds.Add(room.Prefab.PrefabEditorId);
                    }
                }

                for (int i = 0; i < 20; i++)
                {
                    var candidate = new RoomPrefab(roomUtils.GetRoom(startingConnector.Tileset));

                    var candConnectors = candidate.Markers
                        .Select(m => new
                        {
                            Marker = m,
                            Conn = RgConnectorParser.Parse(m.MarkerEditorId)
                        })
                        .Where(x => x.Conn.IsValid)
                        .ToList();

                    var entry = candConnectors.FirstOrDefault(x => x.Conn.Direction == ConnectorDirection.South)?.Marker;

                    if (entry != null && candConnectors.Count(x => x.Conn.Direction != ConnectorDirection.South) >= 2)
                    {
                        roomPrefab = candidate;
                        var connectors = candConnectors;
                        south0 = connectors.First(x => x.Conn.Direction == ConnectorDirection.South).Marker;
                        north0 = connectors.FirstOrDefault(x => x.Conn.Direction == ConnectorDirection.North)?.Marker;
                        break;
                    }
                }

                if (roomPrefab == null)
                    throw new Exception("Failed to find a starting room with open connectors.");

                // Place first prefab so its SOUTH marker lands on the starting marker.
                // prefabWorldPos + southLocal = startWorld  =>  prefabWorldPos = startWorld - southLocal
                P3Float prefabWorldPos = startingMarker.Position - south0.Position;
                // Build initial room record (assumes you already placed roomPrefab at prefabWorldPos)
                var startConnectors = ConnectorUtils.GetConnectors(roomPrefab);


                usedPrefabIds.Add(roomPrefab.PrefabEditorId);
                requiredPrefabs.RemoveAll(id => usedPrefabIds.Contains(id));
                var plannedRooms = new List<PlacedRoom>();
                var plannedOpenConnectors = new List<OpenConnector>();
                var plannedPlacements = new List<PlacedObject>();

                plannedPlacements.Add(new PlacedObject(gen_quest_main.myMod)
                {
                    Count = 1,
                    Rotation = new P3Float(),
                    Position = prefabWorldPos,
                    Base = roomPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                });

                plannedRooms.Add(new PlacedRoom
                {
                    Prefab = roomPrefab,
                    WorldPos = prefabWorldPos,
                    YawSteps = 0,
                    DistrictType = districtType,
                    Connectors = startConnectors
                });

                foreach (var c in startConnectors)
                {
                    if (c.Parsed.Direction != ConnectorDirection.South)
                    {
                        plannedOpenConnectors.Add(new OpenConnector
                        {
                            Parsed = c.Parsed,
                            YawSteps = 0,
                            WorldPos = prefabWorldPos + c.LocalPos,
                            DistrictType = districtType
                        });
                    }
                }
                int connectorsAddedCount = plannedOpenConnectors.Count;

                // Count the initially placed room toward our plan so limits and logs reflect the total.
                int roomsPlaced = 1;
                int attempts = 0;
                var yMin = startingMarker.Position.Y;

                while (roomsPlaced < maxRoomsToPlace && plannedOpenConnectors.Count > 0 && attempts < maxAttempts)
                {
                    attempts++;

                    var clusterCenter = CalculateClusterCenter(plannedRooms, plannedOpenConnectors);
                    var northConnectors = plannedOpenConnectors.Where(c => c.Parsed.Direction == ConnectorDirection.North).ToList();
                    double northBiasWeight = state.scoringSystem.NorthBiasWeight;
                    bool useNorthBias = northConnectors.Count > 0 && Rng.NextDouble() < northBiasWeight;

                    var target = ChooseFarthestOpenConnector(useNorthBias ? northConnectors : plannedOpenConnectors, clusterCenter);
                    int openIndex = plannedOpenConnectors.IndexOf(target);
                    if (openIndex < 0)
                    {
                        continue;
                    }

                    if (target.WorldPos.Y < startingMarker.Position.Y)
                    {
                        continue;
                    }

                    plannedOpenConnectors.RemoveAt(openIndex);

                    var requiredDir = ConnectorUtils.Opposite(target.Parsed.Direction);

                    var bestPlacement = (PlacedObject)null;
                    PlacedRoom bestRoom = new PlacedRoom();
                    List<OpenConnector> bestNewOpenConnectors = null;
                    int bestBridgeScore = BridgeUtil.CountBridgeablePairs(plannedOpenConnectors, yMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, bridgePrefabKeys);
                    bool bestPlacementUsesRequired = false;
                    string bestPlacementPrefabId = null;
                    bool attemptedRequiredForThisConnector = false;

                    for (int prefabTry = 0; prefabTry < maxCandidatePrefabsPerConnector; prefabTry++)
                    {
                        bool useRequired = requiredPrefabs.Count > 0 && !attemptedRequiredForThisConnector;
                        if (useRequired)
                        {
                            attemptedRequiredForThisConnector = true;
                        }

                        string prefabId;
                        if (useRequired)
                        {
                            prefabId = requiredPrefabs.FirstOrDefault(id => !usedPrefabIds.Contains(id));
                            requiredPrefabs.RemoveAll(id => usedPrefabIds.Contains(id));
                            if (string.IsNullOrEmpty(prefabId))
                            {
                                continue;
                            }
                        }
                        else if (!TryGetUnusedPrefabId(roomUtils, target.Parsed.Tileset, "_trk_", usedPrefabIds, maxCandidatePrefabsPerConnector * 2, out prefabId))
                        {
                            continue;
                        }

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

                            var chosen = ChooseMostOutwardConnector(compatible, target.WorldPos, clusterCenter);

                            P3Float nextPos = target.WorldPos - chosen.LocalPos;

                            var candidateAabb = ConnectorUtils.ToWorldAabbRotated(nextPrefab.packin_instance.ObjectBounds, nextPos, yawSteps);
                            if (ConnectorUtils.CollidesWithAny(candidateAabb, plannedRooms, collisionPadding))
                                continue;

                            var candidateRoom = new PlacedRoom
                            {
                                Prefab = nextPrefab,
                                WorldPos = nextPos,
                                YawSteps = yawSteps,
                                DistrictType = districtType,
                                Connectors = nextConnectors
                            };

                            var candidatePlacement = new PlacedObject(gen_quest_main.myMod)
                            {
                                Count = 1,
                                Rotation = RgRotation.RotationToP3Float(yawSteps),
                                Position = nextPos,
                                Base = nextPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                            };

                            var newOpenConnectors = BuildOpenConnectors(nextConnectors, chosen, yawSteps, nextPos, districtType);
                            var connectorsAfterPlacement = new List<OpenConnector>(plannedOpenConnectors);
                            connectorsAfterPlacement.AddRange(newOpenConnectors);
                            
                            int bridgeScore = BridgeUtil.CountBridgeablePairs(connectorsAfterPlacement, yMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, bridgePrefabKeys);

                            bool candidateIsForced = useRequired;
                            if (bestPlacement == null
                                || (candidateIsForced && !bestPlacementUsesRequired)
                                || (candidateIsForced && bestPlacementUsesRequired && bridgeScore > bestBridgeScore)
                                || (!candidateIsForced && !bestPlacementUsesRequired && bridgeScore > bestBridgeScore))
                            {
                                bestBridgeScore = bridgeScore;
                                bestPlacement = candidatePlacement;
                                bestRoom = candidateRoom;
                                bestNewOpenConnectors = newOpenConnectors;
                                bestPlacementUsesRequired = useRequired;
                                bestPlacementPrefabId = nextPrefab.PrefabEditorId;
                            }
                        }
                    }

                    if (bestPlacement == null)
                    {
                        plannedOpenConnectors.Add(target);//Return it to the list so we close it later.
                        continue;
                    }

                    plannedPlacements.Add(bestPlacement);
                    plannedRooms.Add(bestRoom);
                    usedPrefabIds.Add(bestRoom.Prefab.PrefabEditorId);
                    if (bestPlacementUsesRequired && !string.IsNullOrEmpty(bestPlacementPrefabId))
                    {
                        requiredPrefabs.RemoveAll(id => id.Equals(bestPlacementPrefabId, StringComparison.OrdinalIgnoreCase));
                    }
                    roomsPlaced++;
                    plannedOpenConnectors.AddRange(bestNewOpenConnectors);
                    connectorsAddedCount += bestNewOpenConnectors?.Count ?? 0;
                }

                bool success = roomsPlaced >= maxRoomsToPlace;
                var bridgeablePairs = BridgeUtil.CountBridgeablePairs(plannedOpenConnectors, yMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, bridgePrefabKeys);
                var planArea = ScoringUtil.CalculateTotalArea(plannedRooms);
                var planClustering = ScoringUtil.CalculateAverageMinimumDistance(plannedRooms);
                var planSizeDiversity = ScoringUtil.CalculateSmallRoomChainPenalty(plannedRooms);
                var planRoomReuse = ScoringUtil.CalculateRoomReuseScore(plannedRooms);
                var connectorViability = ScoringUtil.CalculateConnectorViabilityArea(plannedRooms, plannedOpenConnectors);
                var planScore = ScoringUtil.ScorePlan(state.scoringSystem, roomsPlaced, bridgeablePairs, 0, connectorsAddedCount, planArea, planClustering, planSizeDiversity, planRoomReuse, connectorViability);
                int missingRequiredPrefabs = requiredPrefabs.Count;
                double adjustedPlanScore = planScore.Total - (missingRequiredPrefabs > 0 ? 100000 * missingRequiredPrefabs : 0);
                if (adjustedPlanScore > bestPlanScoreWithPenalty)
                {
                    bestBridgeablePairs = bridgeablePairs;
                    bestPlannedRooms = plannedRooms;
                    bestPlannedOpenConnectors = plannedOpenConnectors;
                    bestPlannedPlacements = plannedPlacements;
                    bestRoomsPlaced = roomsPlaced;
                    bestPlanScoreWithPenalty = adjustedPlanScore;
                    bestPlanScoreBreakdown = planScore;
                    bestPlanAttempt = planAttempt;
                    bestYMin = yMin;
                    bestNewConnectors = connectorsAddedCount;
                    bestMissingRequiredPrefabs = missingRequiredPrefabs;
                }
            }

            // Apply the best scoring plan after all attempts.
            var finalRooms = bestPlannedRooms ?? new List<PlacedRoom>();
            var finalOpenConnectors = bestPlannedOpenConnectors ?? new List<OpenConnector>();
            var finalPlacements = bestPlannedPlacements ?? new List<PlacedObject>();
            var finalScore = bestPlanScoreBreakdown ?? new PlanScore
            {
                Total = 0,
                Components = new Dictionary<string, double>
                {
                    { "Placement", 0 },
                    { "Bridging", 0 },
                    { "BridgingOverlap", 0 },
                    { "NewConnectors", 0 },
                    { "Area", 0 },
                    { "Clustering", 0 },
                    { "SizeDiversity", 0 },
                    { "RoomReuse", 0 },
                    { "ConnectorViability", 0 }
                }
            };

            foreach (var placement in finalPlacements)
            {
                state.instance.Temporary.Add(placement);
            }
            state.placedRooms = finalRooms;
            state.openConnectors = finalOpenConnectors;
            state.YMin = bestYMin;

            var forcedInfo = PrefabsToForcePlacement.Count > 0
                ? $", forced remaining {bestMissingRequiredPrefabs}"
                : string.Empty;

            Console.WriteLine($"[Trunk Plan] best of {maxPlans} attempts (attempt {bestPlanAttempt + 1}): placed {bestRoomsPlaced}/{maxRoomsToPlace} rooms, bridgeable pairs {bestBridgeablePairs}, new connectors {bestNewConnectors}{forcedInfo}, {ScoringUtil.PrettyPrintScore(finalScore, includeNewConnectors: true)}.");
        }

        private static OpenConnector ChooseFarthestOpenConnector(List<OpenConnector> openConnectors, P3Float clusterCenter)
        {
            float maxDist = float.MinValue;
            OpenConnector best = openConnectors[0];
            for (int i = 0; i < openConnectors.Count; i++)
            {
                var dist = MathUtil.DistanceSquared(openConnectors[i].WorldPos, clusterCenter);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    best = openConnectors[i];
                }
            }
            return best;
        }

        private static RgConnectorInstance ChooseMostOutwardConnector(List<RgConnectorInstance> compatibles, P3Float targetWorldPos, P3Float clusterCenter)
        {
            RgConnectorInstance best = compatibles[0];
            float bestDist = MathUtil.DistanceSquared(targetWorldPos - best.LocalPos, clusterCenter);

            foreach (var c in compatibles)
            {
                float dist = MathUtil.DistanceSquared(targetWorldPos - c.LocalPos, clusterCenter);
                if (dist > bestDist)
                {
                    bestDist = dist;
                    best = c;
                }
            }

            return best;
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

        private static bool TryGetUnusedPrefabId(
            RoomUtils roomUtils,
            string tileset,
            string typeFilter,
            HashSet<string> usedPrefabIds,
            int maxTries,
            out string prefabId)
        {
            prefabId = null;

            for (int i = 0; i < maxTries; i++)
            {
                var candidate = roomUtils.GetRoom(tileset, typeFilter);
                if (!usedPrefabIds.Contains(candidate))
                {
                    prefabId = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
