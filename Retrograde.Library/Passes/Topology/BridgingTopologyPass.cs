using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Passes
{
    /// <summary>
    /// Places bridge prefabs between pairs of open connectors on different rooms,
    /// linking separate dungeon branches into a connected topology.
    /// </summary>
    public class BridgingTopologyPass : IGenPass
    {
        // Connector matching tolerances
        private const float ConnectorPositionTolerance = 0.05f;
        private const float ConnectorEmbedTolerance = 0.05f;

        // Collision and spacing parameters
        private const float CollisionPadding = -0.5f; // Prefer to fail rather than overlap

        // Bridge placement limits
        private const int MaxPrefabsToTryPerPair = 48;
        private const int TargetBridgeCount = 10;
        private const int DefaultMaxPlans = 50;

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

        private class BridgePlanMeta
        {
            public int BridgesPlaced;
            public int OverlapCount;
            public PlanScore Score;
        }

        /// <summary>
        /// Bundles mutable plan state and shared configuration for bridge placement methods.
        /// </summary>
        private class BridgePlacementContext
        {
            public List<PlacedRoom> PlannedRooms { get; set; }
            public List<OpenConnector> PlannedOpenConnectors { get; set; }
            public List<PlacedObject> PlannedPlacements { get; set; }
            public HashSet<string> UsedPrefabIds { get; set; }
            public List<RoomUtils> RoomUtils { get; set; }
            public string DistrictTypeLabel { get; set; }
            public string DistrictFilter { get; set; }
            public float YMin { get; set; }
        }

        /// <summary>
        /// Result of successfully placing a bridge prefab between two connectors.
        /// </summary>
        private class BridgePlacementResult
        {
            public PlacedRoom PlacedRoom { get; set; }
            public PlacedObject PlacementObject { get; set; }
            public List<OpenConnector> NewConnectors { get; set; }
        }

        /// <summary>
        /// Main algorithm: Connects dungeon branches by placing bridge prefabs between
        /// compatible open connector pairs on different rooms.
        /// Uses multi-plan evaluation to find the best bridging layout.
        /// </summary>
        public void RunPass(DungeonState state)
        {
            if (state.openConnectors == null || state.openConnectors.Count < 2)
                return;

            int maxPlans = state.scoringSystem?.Effort ?? DefaultMaxPlans;
            var activeBridgeLists = state.TrunkRoomLists;
            var activeRoomUtils = activeBridgeLists.Select(name => state.GetRoomUtils(name)).ToList();
            var activeDistrictTypeLabel = DeriveDistrictType(activeBridgeLists.FirstOrDefault(), districtFilter, districtTypeLabel);

            // Stage 1: Multi-plan generation - run multiple attempts to find optimal bridging
            var bestOutcome = PlanRunner.RunBest<BridgePlanMeta>(maxPlans, planAttempt =>
            {
                var context = new BridgePlacementContext
                {
                    PlannedRooms = new List<PlacedRoom>(state.placedRooms),
                    PlannedOpenConnectors = state.openConnectors
                        .Where(c => c.WorldPos.Y >= state.YMin)
                        .Where(c => RoomHasMoreThanTwoConnectors(state.placedRooms, c, ConnectorPositionTolerance))
                        .OrderBy(_ => RandomProvider.Random.Next())
                        .ToList(),
                    PlannedPlacements = new List<PlacedObject>(),
                    UsedPrefabIds = CollectUsedPrefabIds(state.placedRooms),
                    RoomUtils = activeRoomUtils,
                    DistrictTypeLabel = activeDistrictTypeLabel,
                    DistrictFilter = districtFilter,
                    YMin = state.YMin
                };

                // Stage 2: Iterative bridge placement loop
                var (bridgesPlaced, overlapCount) = PlanBridges(context);

                // Stage 3: Evaluate this plan using scoring metrics
                var planArea = ScoringUtil.CalculateTotalArea(context.PlannedRooms);
                var planClustering = ScoringUtil.CalculateAverageMinimumDistance(context.PlannedRooms);
                var planSizeDiversity = ScoringUtil.CalculateSmallRoomChainPenalty(context.PlannedRooms);
                var planRoomReuse = ScoringUtil.CalculateRoomReuseScore(context.PlannedRooms);
                var connectorViability = ScoringUtil.CalculateConnectorViabilityArea(context.PlannedRooms, context.PlannedOpenConnectors);
                var planScore = ScoringUtil.ScorePlan(state.scoringSystem, bridgesPlaced, bridgesPlaced, overlapCount, 0, planArea, planClustering, planSizeDiversity, planRoomReuse, connectorViability);

                return new PlanOutcome<BridgePlanMeta>
                {
                    Score = planScore.Total,
                    Rooms = context.PlannedRooms,
                    OpenConnectors = context.PlannedOpenConnectors,
                    Placements = context.PlannedPlacements,
                    Metadata = new BridgePlanMeta
                    {
                        BridgesPlaced = bridgesPlaced,
                        OverlapCount = overlapCount,
                        Score = planScore
                    }
                };
            });

            // Stage 4: Apply the best plan to the dungeon state
            var finalRooms = bestOutcome?.Rooms ?? state.placedRooms;
            var finalOpenConnectors = bestOutcome?.OpenConnectors ?? state.openConnectors;
            var finalPlacements = bestOutcome?.Placements ?? new List<PlacedObject>();
            var finalOverlapCount = bestOutcome?.Metadata?.OverlapCount ?? 0;
            var finalScore = bestOutcome?.Metadata?.Score ?? new PlanScore
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
                state.PlacementUtil.AddToTemporary(state.instance, placement);
            }
            state.placedRooms = finalRooms;
            state.openConnectors = finalOpenConnectors;

            int bestPlanAttempt = (bestOutcome?.AttemptIndex ?? -1) + 1;
            int bestBridgesPlaced = bestOutcome?.Metadata?.BridgesPlaced ?? -1;

            if (!state.IsHarnessRun)
            {
                Console.WriteLine($"[Bridge plan] best of {maxPlans} attempts (attempt {bestPlanAttempt}): placed {bestBridgesPlaced}/{TargetBridgeCount} bridge prefabs, overlap {finalOverlapCount}, {ScoringUtil.PrettyPrintScore(finalScore, includeBridgingOverlap: true)}.");
            }
        }

        /// <summary>
        /// Iteratively pairs open connectors and attempts to place bridge prefabs between them.
        /// Restarts pair search after each successful placement since connector lists change.
        /// </summary>
        private static (int bridgesPlaced, int overlapCount) PlanBridges(BridgePlacementContext ctx)
        {
            int bridgesPlaced = 0;
            int overlapCount = 0;
            bool progress = true;

            while (progress && ctx.PlannedOpenConnectors.Count >= 2 && bridgesPlaced < TargetBridgeCount)
            {
                progress = false;

                for (int i = 0; i < ctx.PlannedOpenConnectors.Count - 1; i++)
                {
                    for (int j = i + 1; j < ctx.PlannedOpenConnectors.Count; j++)
                    {
                        var a = ctx.PlannedOpenConnectors[i];
                        var b = ctx.PlannedOpenConnectors[j];

                        // Stage 2a: Filter to valid connector pairs
                        if (!IsValidBridgePair(a, b, ctx.PlannedRooms))
                            continue;

                        // Stage 2b: Try to place a bridge prefab connecting this pair
                        var result = TryPlaceBridgeBetween(a, b, ctx);
                        if (result == null)
                            continue;

                        // Stage 2c: Accept placement and update state
                        AcceptBridgePlacement(ctx, result, i, j);

                        bridgesPlaced++;
                        if (BridgeUtil.HaveSameOwner(ctx.PlannedRooms, a, b, ConnectorPositionTolerance))
                        {
                            overlapCount++;
                        }
                        progress = true;
                        goto NextIteration;
                    }
                }

                break;

            NextIteration:
                continue;
            }

            return (bridgesPlaced, overlapCount);
        }

        /// <summary>
        /// Checks whether two open connectors form a valid candidate pair for bridging:
        /// both owners must have 3+ connectors, belong to different rooms, and be compatible.
        /// </summary>
        private static bool IsValidBridgePair(OpenConnector a, OpenConnector b, List<PlacedRoom> plannedRooms)
        {
            if (!RoomHasMoreThanTwoConnectors(plannedRooms, a, ConnectorPositionTolerance) ||
                !RoomHasMoreThanTwoConnectors(plannedRooms, b, ConnectorPositionTolerance))
                return false;

            if (BridgeUtil.HaveSameOwner(plannedRooms, a, b, ConnectorPositionTolerance))
                return false;

            return BridgeUtil.ArePairCompatible(a, b);
        }

        /// <summary>
        /// Updates plan state after a successful bridge placement: adds room/placement,
        /// removes consumed connectors, and adds new open connectors from the bridge.
        /// </summary>
        private static void AcceptBridgePlacement(BridgePlacementContext ctx, BridgePlacementResult result, int connectorIndexA, int connectorIndexB)
        {
            ctx.PlannedPlacements.Add(result.PlacementObject);
            ctx.PlannedRooms.Add(result.PlacedRoom);
            ctx.UsedPrefabIds.Add(result.PlacedRoom.Prefab.PrefabEditorId);

            ctx.PlannedOpenConnectors.RemoveAt(connectorIndexB);
            ctx.PlannedOpenConnectors.RemoveAt(connectorIndexA);
            ctx.PlannedOpenConnectors.AddRange(result.NewConnectors);
        }

        /// <summary>
        /// Attempts to find a bridge prefab that connects two open connectors.
        /// Evaluates random candidate prefabs across all rotations, checking that
        /// the prefab aligns both connectors and does not collide with existing rooms.
        /// </summary>
        /// <returns>Placement result if a valid bridge was found, null otherwise.</returns>
        private static BridgePlacementResult TryPlaceBridgeBetween(OpenConnector a, OpenConnector b, BridgePlacementContext ctx)
        {
            var candidates = BuildPrefabCandidates(a.Parsed.Tileset, ctx.UsedPrefabIds, ctx.RoomUtils, ctx.DistrictFilter);
            if (candidates.Count == 0)
                return null;

            var prefabsToTry = candidates
                .OrderBy(_ => RandomProvider.Random.Next())
                .Take(Math.Max(1, Math.Min(MaxPrefabsToTryPerPair, candidates.Count)))
                .ToList();

            foreach (var prefabId in prefabsToTry)
            {
                var prefab = PrefabCache.GetPrefab(prefabId);

                var yawOrder = Enumerable.Range(0, 4)
                    .OrderBy(_ => RandomProvider.Random.Next())
                    .ToList();

                foreach (var yawSteps in yawOrder)
                {
                    var result = EvaluatePrefabRotation(a, b, prefab, yawSteps, ctx);
                    if (result != null)
                        return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Evaluates a single prefab at a specific rotation to see if it bridges two connectors.
        /// Finds connector pairs that match both endpoints, verifies position alignment,
        /// then checks collision, embedding, and height constraints.
        /// </summary>
        private static BridgePlacementResult EvaluatePrefabRotation(
            OpenConnector a,
            OpenConnector b,
            RoomPrefab prefab,
            int yawSteps,
            BridgePlacementContext ctx)
        {
            var connectors = ConnectorUtils.GetConnectors(prefab, yawSteps);

            var matchesA = connectors.Where(c => BridgeUtil.MatchesOpenConnector(a, c)).ToList();
            var matchesB = connectors.Where(c => BridgeUtil.MatchesOpenConnector(b, c)).ToList();

            if (matchesA.Count == 0 || matchesB.Count == 0)
                return null;

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

                    // Validate placement constraints
                    var candidateAabb = ConnectorUtils.ToWorldAabbRotated(prefab.packin_instance.ObjectBounds, prefabPos, yawSteps);
                    if (ConnectorUtils.IsBelowYMin(candidateAabb, ctx.YMin))
                        continue;
                    if (ConnectorUtils.CollidesWithAny(candidateAabb, ctx.PlannedRooms, CollisionPadding))
                        continue;
                    if (BridgeUtil.AnyConnectorInsideExistingBounds(connectors, prefabPos, ctx.PlannedRooms, ConnectorEmbedTolerance))
                        continue;
                    if (BridgeUtil.AnyExistingConnectorInsideCandidate(candidateAabb, ctx.PlannedRooms, ConnectorEmbedTolerance))
                        continue;

                    return BuildBridgePlacementResult(prefab, prefabPos, yawSteps, connectors, connA, connB, ctx.DistrictTypeLabel);
                }
            }

            return null;
        }

        /// <summary>
        /// Constructs the placement result objects for a validated bridge placement.
        /// </summary>
        private static BridgePlacementResult BuildBridgePlacementResult(
            RoomPrefab prefab,
            P3Float prefabPos,
            int yawSteps,
            List<RgConnectorInstance> connectors,
            RgConnectorInstance connA,
            RgConnectorInstance connB,
            string districtTypeLabel)
        {
            var placementObject = new PlacedObject(RetrogradeContext.Current.TargetMod)
            {
                Count = 1,
                Rotation = RgRotation.RotationToP3Float(yawSteps),
                Position = prefabPos,
                Base = prefab.packin_instance.ToLink<IPlaceableObjectGetter>()
            };

            var placedRoom = new PlacedRoom
            {
                Prefab = prefab,
                WorldPos = prefabPos,
                YawSteps = yawSteps,
                DistrictType = districtTypeLabel,
                Connectors = connectors
            };

            var newConnectors = new List<OpenConnector>();
            foreach (var c in connectors)
            {
                if (BridgeUtil.IsSameConnector(c, connA) || BridgeUtil.IsSameConnector(c, connB))
                    continue;

                newConnectors.Add(new OpenConnector
                {
                    Parsed = c.Parsed,
                    YawSteps = yawSteps,
                    WorldPos = prefabPos + c.LocalPos,
                    DistrictType = districtTypeLabel
                });
            }

            return new BridgePlacementResult
            {
                PlacedRoom = placedRoom,
                PlacementObject = placementObject,
                NewConnectors = newConnectors
            };
        }

        private static List<string> BuildPrefabCandidates(string tileset, HashSet<string> usedPrefabIds, List<RoomUtils> roomUtils, string districtFilter)
        {
            var allCandidates = new List<string>();

            foreach (var utils in roomUtils)
            {
                var listKey = utils.listName + "_" + tileset;
                if (!utils.roomTemplates.TryGetValue(listKey, out var formList) || formList?.Items == null || formList.Items.Count == 0)
                    continue;

                foreach (var item in formList.Items)
                {
                    if (!RetrogradeContext.Current.TargetMod.PackIns.TryGetValue(item.FormKey, out var packIn) ||
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
            return source.OrderBy(_ => RandomProvider.Random.Next()).ToList();
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
