using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde;
using FrankyCLI.Retrograde.Passes;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    public class BridgingTopologyPass : IGenPass
    {
        private const float ConnectorPositionTolerance = 0.05f;
        private float collisionPadding = -0.5f; // prefer to fail rather than overlap
        private const float connectorEmbedTolerance = 0.05f;
        private int maxPlans = 50;
        private const int maxPrefabsToTryPerPair = 48;
        private const int targetBridgeCount = 10;

        private readonly string districtFilter;
        private readonly string districtTypeLabel;

        public BridgingTopologyPass(string roomList, string districtType = null)
            : this(districtType)
        {
        }

        public BridgingTopologyPass(string districtType = null)
        {
            districtFilter = districtType;
            districtTypeLabel = "bridge";
        }


        public void RunPass(DungeonState state)
        {
            if (state.openConnectors == null || state.openConnectors.Count < 2)
                return;

            maxPlans = state.scoringSystem?.Effort ?? maxPlans;
            var activeBridgeLists = state.TrunkRoomLists;
            var activeRoomUtils = activeBridgeLists.Select(name => new RoomUtils(name)).ToList();
            var activeDistrictTypeLabel = DeriveDistrictType(activeBridgeLists.FirstOrDefault(), districtFilter, districtTypeLabel);

            int bestBridgesPlaced = -1;
            int bestOverlapCount = 0;
            List<PlacedRoom> bestPlannedRooms = null;
            List<OpenConnector> bestPlannedOpenConnectors = null;
            List<PlacedObject> bestPlannedPlacements = null;
            double bestPlanScore = double.MinValue;
            PlanScore? bestPlanScoreBreakdown = null;
            int bestPlanAttempt = -1;

            for (int planAttempt = 0; planAttempt < maxPlans; planAttempt++)
            {
                var usedPrefabIds = CollectUsedPrefabIds(state.placedRooms);

                var plannedRooms = new List<PlacedRoom>(state.placedRooms);
                var plannedOpenConnectors = state.openConnectors
                    .Where(c => c.WorldPos.Y >= state.YMin)
                    .Where(c => RoomHasMoreThanTwoConnectors(state.placedRooms, c, ConnectorPositionTolerance))
                    .OrderBy(_ => RandomUtils.random.Next())
                    .ToList();
                var plannedPlacements = new List<PlacedObject>();

                var (bridgesPlaced, overlapCount) = PlanBridges(
                    plannedRooms,
                    plannedOpenConnectors,
                    plannedPlacements,
                    usedPrefabIds,
                    collisionPadding,
                    connectorEmbedTolerance,
                    maxPrefabsToTryPerPair,
                    targetBridgeCount,
                    activeRoomUtils,
                    activeDistrictTypeLabel,
                    state.YMin);
                var planArea = ScoringUtil.CalculateTotalArea(plannedRooms);
                var planClustering = ScoringUtil.CalculateAverageMinimumDistance(plannedRooms);
                var planSizeDiversity = ScoringUtil.CalculateSmallRoomChainPenalty(plannedRooms);
                var planRoomReuse = ScoringUtil.CalculateRoomReuseScore(plannedRooms);
                var connectorViability = ScoringUtil.CalculateConnectorViabilityArea(plannedRooms, plannedOpenConnectors);
                var planScore = ScoringUtil.ScorePlan(state.scoringSystem, bridgesPlaced, bridgesPlaced, overlapCount, 0, planArea, planClustering, planSizeDiversity, planRoomReuse, connectorViability);

                if (planScore.Total > bestPlanScore)
                {
                    bestBridgesPlaced = bridgesPlaced;
                    bestOverlapCount = overlapCount;
                    bestPlannedRooms = plannedRooms;
                    bestPlannedOpenConnectors = plannedOpenConnectors;
                    bestPlannedPlacements = plannedPlacements;
                    bestPlanScore = planScore.Total;
                    bestPlanScoreBreakdown = planScore;
                    bestPlanAttempt = planAttempt;
                }
            }

            var finalRooms = bestPlannedRooms ?? state.placedRooms;
            var finalOpenConnectors = bestPlannedOpenConnectors ?? state.openConnectors;
            var finalPlacements = bestPlannedPlacements ?? new List<PlacedObject>();
            var finalOverlapCount = bestOverlapCount;
            var finalScore = bestPlanScoreBreakdown ?? new PlanScore
            {
                Total = 0,
                Components = new Dictionary<string, double>
                {
                    { "Placement", 0 },
                    { "Bridging", 0 },
                    { "BridgingOverlap", 0 },
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

            Console.WriteLine($"[Bridge plan] best of {maxPlans} attempts (attempt {bestPlanAttempt + 1}): placed {bestBridgesPlaced}/{targetBridgeCount} bridge prefabs, overlap {finalOverlapCount}, {ScoringUtil.PrettyPrintScore(finalScore, includeBridgingOverlap: true)}.");
        }

        private (int bridgesPlaced, int overlapCount) PlanBridges(
            List<PlacedRoom> plannedRooms,
            List<OpenConnector> plannedOpenConnectors,
            List<PlacedObject> plannedPlacements,
            HashSet<string> usedPrefabIds,
            float collisionPadding,
            float connectorEmbedTolerance,
            int maxPrefabsToTryPerPair,
            int desiredBridgeCount,
            List<RoomUtils> roomUtils,
            string districtTypeLabel,
            float yMin)
        {
            int bridgesPlaced = 0;
            int overlapCount = 0;
            bool progress = true;

            while (progress && plannedOpenConnectors.Count >= 2 && bridgesPlaced < desiredBridgeCount)
            {
                progress = false;

                for (int i = 0; i < plannedOpenConnectors.Count - 1; i++)
                {
                    for (int j = i + 1; j < plannedOpenConnectors.Count; j++)
                    {
                        var a = plannedOpenConnectors[i];
                        var b = plannedOpenConnectors[j];

                        if (!RoomHasMoreThanTwoConnectors(plannedRooms, a, ConnectorPositionTolerance) ||
                            !RoomHasMoreThanTwoConnectors(plannedRooms, b, ConnectorPositionTolerance))
                            continue;

                        if (BridgeUtil.HaveSameOwner(plannedRooms, a, b, ConnectorPositionTolerance))
                            continue;

                        if (!BridgeUtil.ArePairCompatible(a, b))
                            continue;

                        if (TryPlaceBridgeBetween(a, b, plannedRooms, usedPrefabIds, collisionPadding, connectorEmbedTolerance, maxPrefabsToTryPerPair, roomUtils, districtTypeLabel, yMin, out var placedRoom, out var placement, out var newConnectors))
                        {
                            plannedPlacements.Add(placement);
                            plannedRooms.Add(placedRoom);
                            usedPrefabIds.Add(placedRoom.Prefab.PrefabEditorId);

                            plannedOpenConnectors.RemoveAt(j);
                            plannedOpenConnectors.RemoveAt(i);
                            plannedOpenConnectors.AddRange(newConnectors);

                            bridgesPlaced++;
                            if (BridgeUtil.HaveSameOwner(plannedRooms, a, b, ConnectorPositionTolerance))
                            {
                                overlapCount++;
                            }
                            progress = true;
                            goto NextIteration;
                        }
                    }
                }

                break;

            NextIteration:
                continue;
            }

            return (bridgesPlaced, overlapCount);
        }

        private bool TryPlaceBridgeBetween(
            OpenConnector a,
            OpenConnector b,
            List<PlacedRoom> plannedRooms,
            HashSet<string> usedPrefabIds,
            float collisionPadding,
            float connectorEmbedTolerance,
            int maxPrefabsToTryPerPair,
            List<RoomUtils> roomUtils,
            string districtTypeLabel,
            float yMin,
            out PlacedRoom placedRoom,
            out PlacedObject placedObject,
            out List<OpenConnector> resultingOpenConnectors)
        {
            placedRoom = default;
            placedObject = null;
            resultingOpenConnectors = null;

            var candidates = BuildPrefabCandidates(a.Parsed.Tileset, usedPrefabIds, roomUtils);
            if (candidates.Count == 0)
                return false;

            var prefabsToTry = candidates
                .OrderBy(_ => RandomUtils.random.Next())
                .Take(Math.Max(1, Math.Min(maxPrefabsToTryPerPair, candidates.Count)))
                .ToList();

            foreach (var prefabId in prefabsToTry)
            {
                var prefab = PrefabCache.GetPrefab(prefabId);

                for (int yawSteps = 0; yawSteps < 4; yawSteps++)
                {
                    var connectors = ConnectorUtils.GetConnectors(prefab, yawSteps);

                    var matchesA = connectors.Where(c => BridgeUtil.MatchesOpenConnector(a, c)).ToList();
                    var matchesB = connectors.Where(c => BridgeUtil.MatchesOpenConnector(b, c)).ToList();

                    if (matchesA.Count == 0 || matchesB.Count == 0)
                        continue;

                    foreach (var connA in matchesA)
                    {
                        foreach (var connB in matchesB)
                        {
                            if (BridgeUtil.IsSameConnector(connA, connB))
                                continue;

                            var prefabPos = a.WorldPos - connA.LocalPos;
                            var expectedB = prefabPos + connB.LocalPos;

                            if (!MathUtil.PositionsClose(expectedB, b.WorldPos, ConnectorPositionTolerance))
                                continue;

                            var candidateAabb = ConnectorUtils.ToWorldAabbRotated(prefab.packin_instance.ObjectBounds, prefabPos, yawSteps);
                            if (ConnectorUtils.IsBelowYMin(candidateAabb, yMin))
                                continue;
                            if (ConnectorUtils.CollidesWithAny(candidateAabb, plannedRooms, collisionPadding))
                                continue;
                            if (BridgeUtil.AnyConnectorInsideExistingBounds(connectors, prefabPos, plannedRooms, connectorEmbedTolerance))
                                continue;
                            if (BridgeUtil.AnyExistingConnectorInsideCandidate(candidateAabb, plannedRooms, connectorEmbedTolerance))
                                continue;

                            placedObject = new PlacedObject(gen_quest_main.myMod)
                            {
                                Count = 1,
                                Rotation = RgRotation.RotationToP3Float(yawSteps),
                                Position = prefabPos,
                                Base = prefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                            };

                            placedRoom = new PlacedRoom
                            {
                                Prefab = prefab,
                                WorldPos = prefabPos,
                                YawSteps = yawSteps,
                                DistrictType = districtTypeLabel,
                                Connectors = connectors
                            };

                            resultingOpenConnectors = new List<OpenConnector>();
                            foreach (var c in connectors)
                            {
                                if (BridgeUtil.IsSameConnector(c, connA) || BridgeUtil.IsSameConnector(c, connB))
                                    continue;

                                resultingOpenConnectors.Add(new OpenConnector
                                {
                                    Parsed = c.Parsed,
                                    YawSteps = yawSteps,
                                    WorldPos = prefabPos + c.LocalPos,
                                    DistrictType = districtTypeLabel
                                });
                            }

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private List<string> BuildPrefabCandidates(string tileset, HashSet<string> usedPrefabIds, List<RoomUtils> roomUtils)
        {
            var allCandidates = new List<string>();

            foreach (var utils in roomUtils)
            {
                var listKey = utils.listName + "_" + tileset;
                if (!utils.roomTemplates.TryGetValue(listKey, out var formList) || formList?.Items == null || formList.Items.Count == 0)
                    continue;

                foreach (var item in formList.Items)
                {
                    if (!gen_quest_main.myMod.PackIns.TryGetValue(item.FormKey, out var packIn) ||
                        string.IsNullOrEmpty(packIn?.EditorID))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(districtFilter) &&
                        !packIn.EditorID.Contains(districtFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    allCandidates.Add(packIn.EditorID);
                }
            }

            var distinct = allCandidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var unused = distinct
                .Where(id => !usedPrefabIds.Contains(id))
                .ToList();

            var unusedRooms = unused.Where(id => !IsBlocker(id)).ToList();
            var unusedAny = unused;

            return Shuffle(unusedRooms)
                .Concat(Shuffle(unusedAny.Except(unusedRooms, StringComparer.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static HashSet<string> CollectUsedPrefabIds(List<PlacedRoom> placedRooms)
        {
            var usedPrefabIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var room in placedRooms)
            {
                if (!string.IsNullOrEmpty(room.Prefab?.PrefabEditorId))
                {
                    usedPrefabIds.Add(room.Prefab.PrefabEditorId);
                }
            }

            return usedPrefabIds;
        }

        private static bool RoomHasMoreThanTwoConnectors(List<PlacedRoom> placedRooms, OpenConnector open, float tolerance)
        {
            if (placedRooms == null || placedRooms.Count == 0)
                return false;

            int ownerIndex = BridgeUtil.ResolveConnectorOwner(placedRooms, open, tolerance);
            if (ownerIndex < 0 || ownerIndex >= placedRooms.Count)
                return false;

            var owner = placedRooms[ownerIndex];
            return owner.Connectors != null && owner.Connectors.Count > 2;
        }

        private static bool IsBlocker(string editorId)
        {
            return editorId.IndexOf("rg_blocker", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<T> Shuffle<T>(IEnumerable<T> source)
        {
            return source.OrderBy(_ => RandomUtils.random.Next()).ToList();
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
    }
}
