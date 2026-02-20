using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Passes.SpaceStation
{
    public class DistrictTopologyPass : IGenPass
    {
        string district = null;
        public string roomlist = "";
        private readonly string districtTypeLabel;
        private readonly List<string> prefabsToForcePlacement;
        int maxRoomsToPlace = 10;

        public DistrictTopologyPass(string p_roomlist, int roomtarget, string districtType = null, IEnumerable<string> prefabsToForcePlacement = null)
        {
            district = districtType;
            roomlist = p_roomlist;
            districtTypeLabel = DeriveDistrictType(p_roomlist, districtType, "district");
            maxRoomsToPlace = roomtarget;
            this.prefabsToForcePlacement = prefabsToForcePlacement?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();
        }
        private class DistrictPlanMeta
        {
            public int RoomsPlaced;
            public int BridgeablePairs;
            public int NewConnectors;
            public int MissingRequiredPrefabs;
            public PlanScore Score;
        }

        public void RunPass(DungeonState state)
        {
            // Inputs / knobs
            int maxAttempts = 1000;              // hard limit (failed tries) to avoid infinite loops
            float collisionPadding = -0.1f; // tweak: world units clearance
            int maxCandidatePrefabsPerConnector = 16; // avoid thrashing on a single open connector
            int proximitySample = 5; // bias: pick from the closest N connectors to keep the cluster tight
            int maxPlans = state.scoringSystem.Effort; // retry count for full planning attempts
            const float connectorEmbedTolerance = 0.01f; // prevent connectors from sitting inside other room bounds
            float bridgeMaxHorizontalSpan = 40f; // keep connectors within ranges bridge prefabs can span
            float bridgeMaxVerticalOffset = 8f;
            RoomUtils roomUtils = state.GetRoomUtils(roomlist);
            var bridgePrefabKeys = state.BridgePrefabKeys ??= BridgeUtil.BuildBridgePrefabKeys(state.TrunkRoomLists);

            var bestOutcome = PlanRunner.RunBest<DistrictPlanMeta>(maxPlans, planAttempt =>
            {
                var requiredPrefabs = prefabsToForcePlacement
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList();
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
                    var clusterCenter = ConnectorSelectionUtil.CalculateClusterCenter(plannedRooms, plannedOpenConnectors);
                    int openIndex = ConnectorSelectionUtil.ChooseConnectorIndexNearCenter(plannedOpenConnectors, clusterCenter, proximitySample);
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
                    var baseline = ScoringUtil.ComputePlacementBaseline(plannedRooms);
                    int currentBridgeCount = BridgeUtil.CountBridgeablePairs(plannedOpenConnectors, yMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, bridgePrefabKeys);
                    double bestScore = ScoringUtil.ScoreBaseline(
                        state.scoringSystem, baseline, currentBridgeCount, 0, plannedOpenConnectors);
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
                        else
                        {
                            prefabId = ChoosePrefabId(roomUtils, target.Parsed.Tileset, district, usedPrefabIds);
                        }

                        if (string.IsNullOrEmpty(prefabId))
                        {
                            break;
                        }
                        var nextPrefab = PrefabCache.GetPrefab(prefabId);

                        var yawOrder = Enumerable.Range(0, 4)
                            .OrderBy(_ => RandomProvider.Random.Next())
                            .ToList();

                        foreach (var yawSteps in yawOrder)
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

                            var chosen = compatible[RandomProvider.Random.Next(compatible.Count)];

                            // Align using ROTATED local connector
                            P3Float nextPos = target.WorldPos - chosen.LocalPos;

                            // Collision using ROTATED bounds
                            var candidateAabb = ConnectorUtils.ToWorldAabbRotated(nextPrefab.packin_instance.ObjectBounds, nextPos, yawSteps);
                            if (ConnectorUtils.IsBelowYMin(candidateAabb, state.YMin))
                                continue;
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

                            var candidatePlacement = new PlacedObject(RetrogradeContext.Current.TargetMod)
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

                            double candidateScore = ScoringUtil.ScorePlacementCandidate(
                                state.scoringSystem, baseline, candidateRoom, bridgeScore, newOpenConnectors.Count, connectorsAfterPlacement);

                            bool candidateIsForced = useRequired;
                            if (bestPlacement == null
                                || (candidateIsForced && !bestPlacementUsesRequired)
                                || (candidateIsForced && bestPlacementUsesRequired && candidateScore > bestScore)
                                || (!candidateIsForced && !bestPlacementUsesRequired && candidateScore > bestScore))
                            {
                                bestScore = candidateScore;
                                bestPlacement = candidatePlacement;
                                bestRoom = candidateRoom;
                                bestNewOpenConnectors = newOpenConnectors;
                                bestPlacementUsesRequired = candidateIsForced;
                                bestPlacementPrefabId = nextPrefab.PrefabEditorId;
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
                    if (bestPlacementUsesRequired && !string.IsNullOrEmpty(bestPlacementPrefabId))
                    {
                        requiredPrefabs.RemoveAll(id => id.Equals(bestPlacementPrefabId, StringComparison.OrdinalIgnoreCase));
                    }
                    roomsPlaced++;
                    plannedOpenConnectors.AddRange(bestNewOpenConnectors);
                    connectorsAddedCount += bestNewOpenConnectors?.Count ?? 0;
                }

                var bridgeablePairs = BridgeUtil.CountBridgeablePairs(plannedOpenConnectors, yMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, bridgePrefabKeys);
                var planArea = ScoringUtil.CalculateTotalArea(plannedRooms);
                var planClustering = ScoringUtil.CalculateAverageMinimumDistance(plannedRooms);
                var planSizeDiversity = ScoringUtil.CalculateSmallRoomChainPenalty(plannedRooms);
                var planRoomReuse = ScoringUtil.CalculateRoomReuseScore(plannedRooms);
                var connectorViability = ScoringUtil.CalculateConnectorViabilityArea(plannedRooms, plannedOpenConnectors);
                var duplicateRoomPenalty = ScoringUtil.CalculateDuplicateRoomPenalty(plannedRooms);
                var planBaseline = ScoringUtil.ComputePlacementBaseline(plannedRooms);
                var planCompactness = planBaseline.Compactness;
                var planDeadConnectors = ScoringUtil.CountDeadConnectors(planBaseline.RoomBounds, null, plannedOpenConnectors);
                var planScore = ScoringUtil.ScorePlan(state.scoringSystem, roomsPlaced, bridgeablePairs, 0, connectorsAddedCount, planArea, planClustering, planSizeDiversity, planRoomReuse, connectorViability, duplicateRoomPenalty, planCompactness, planDeadConnectors);
                int missingRequiredPrefabs = requiredPrefabs.Count;
                double adjustedPlanScore = planScore.Total - (missingRequiredPrefabs > 0 ? 100000 * missingRequiredPrefabs : 0);

                return new PlanOutcome<DistrictPlanMeta>
                {
                    Score = adjustedPlanScore,
                    Rooms = plannedRooms,
                    OpenConnectors = plannedOpenConnectors,
                    Placements = plannedPlacements,
                    YMin = yMin,
                    Metadata = new DistrictPlanMeta
                    {
                        RoomsPlaced = roomsPlaced,
                        BridgeablePairs = bridgeablePairs,
                        NewConnectors = connectorsAddedCount,
                        MissingRequiredPrefabs = missingRequiredPrefabs,
                        Score = planScore
                    }
                };
            });

            var finalRooms = bestOutcome?.Rooms ?? new List<PlacedRoom>();
            var finalOpenConnectors = bestOutcome?.OpenConnectors ?? new List<OpenConnector>();
            var finalPlacements = bestOutcome?.Placements ?? new List<PlacedObject>();
            var finalNewConnectors = bestOutcome?.Metadata?.NewConnectors ?? 0;
            var finalScore = bestOutcome?.Metadata?.Score ?? new PlanScore
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
                state.PlacementUtil.AddToTemporary(state.instance, placement);
            }
            state.placedRooms = finalRooms;
            state.openConnectors = finalOpenConnectors;
            state.YMin = bestOutcome?.YMin ?? state.YMin;

            var forcedInfo = prefabsToForcePlacement.Count > 0
                ? $", forced remaining {bestOutcome?.Metadata?.MissingRequiredPrefabs ?? prefabsToForcePlacement.Count}"
                : string.Empty;

            int bestAttemptIndex = (bestOutcome?.AttemptIndex ?? -1) + 1;
            int bestRoomsPlaced = bestOutcome?.Metadata?.RoomsPlaced ?? 0;
            int bestBridgeablePairs = bestOutcome?.Metadata?.BridgeablePairs ?? -1;

            if (!state.IsHarnessRun && !RetrogradeContext.Quiet)
            {
                Console.WriteLine($"[District plan] best of {maxPlans} attempts (attempt {bestAttemptIndex}): placed {bestRoomsPlaced}/{maxRoomsToPlace} rooms, bridgeable pairs {bestBridgeablePairs}, new connectors {finalNewConnectors}{forcedInfo}, {ScoringUtil.PrettyPrintScore(finalScore, includeNewConnectors: true)}.");
            }
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
            // allCandidates is pre-built and cached by RoomUtils — no FindPackIn calls here.
            var allCandidates = roomUtils.GetAllCandidatesForDistrict(tileset, district);
            if (allCandidates.Count == 0)
                return null;

            List<string> unusedNonBlockers = null;
            List<string> unusedBlockers = null;

            foreach (var id in allCandidates)
            {
                if (usedPrefabIds.Contains(id)) continue;

                if (id.IndexOf("rg_blocker", StringComparison.OrdinalIgnoreCase) < 0)
                    (unusedNonBlockers ??= new List<string>()).Add(id);
                else
                    (unusedBlockers ??= new List<string>()).Add(id);
            }

            if (unusedNonBlockers?.Count > 0)
                return GlobalRoomTracker.IsLoaded
                    ? GlobalRoomTracker.ChooseWeighted(unusedNonBlockers)
                    : unusedNonBlockers[RandomProvider.Random.Next(unusedNonBlockers.Count)];

            if (unusedBlockers?.Count > 0)
                return GlobalRoomTracker.IsLoaded
                    ? GlobalRoomTracker.ChooseWeighted(unusedBlockers)
                    : unusedBlockers[RandomProvider.Random.Next(unusedBlockers.Count)];

            return null;
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
    }
}
