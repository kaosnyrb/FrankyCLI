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
        private const float BridgeAlignmentTolerance = 1.0f;
        private const float ConnectorEmbedTolerance = 0.05f;

        // Collision and spacing parameters
        private const float CollisionPadding = -0.5f; // Prefer to fail rather than overlap

        // Bridge placement limits
        private const int MaxPrefabsToTryPerPair = 48;
        private const int TargetBridgeCount = 10;
        private const int DefaultMaxPlans = 50;

        private readonly string districtFilter;
        private readonly string districtTypeLabel;
        private readonly int maxOverlapBridges;

        public BridgingTopologyPass(string roomList, string districtType = null, int maxOverlapBridges = int.MaxValue)
            : this(districtType, maxOverlapBridges)
        {
        }

        public BridgingTopologyPass(string districtType = null, int maxOverlapBridges = int.MaxValue)
        {
            districtFilter = districtType;
            districtTypeLabel = "bridge";
            this.maxOverlapBridges = maxOverlapBridges;
        }

        private class BridgePlanMeta
        {
            public int BridgesPlaced;
            public int OverlapCount;
            public PlanScore Score;
            public BridgeRejectCounts RejectCounts;
        }

        /// <summary>
        /// Bundles mutable plan state and shared configuration for bridge placement methods.
        /// </summary>
        private class BridgeRejectCounts
        {
            public int Alignment;
            public int BelowYMin;
            public int Collision;
            public int ConnectorEmbed;
            public int ExistingConnectorEmbed;
            public int NoCandidates;
            public int NoConnectorMatch;
            public float ClosestAlignmentMiss = float.MaxValue;

            public override string ToString()
            {
                string closest = ClosestAlignmentMiss < float.MaxValue ? $"{ClosestAlignmentMiss:F2}" : "n/a";
                return $"alignment={Alignment} (closest miss={closest}), noCandidates={NoCandidates}, noConnMatch={NoConnectorMatch}, belowYMin={BelowYMin}, collision={Collision}, connEmbed={ConnectorEmbed}, existConnEmbed={ExistingConnectorEmbed}";
            }
        }

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
            public int MaxOverlapBridges { get; set; } = int.MaxValue;
            public BridgeRejectCounts RejectCounts { get; set; } = new BridgeRejectCounts();
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
                        .Where(c => RoomHasMultipleConnectors(state.placedRooms, c, ConnectorPositionTolerance))
                        .OrderBy(_ => RandomProvider.Random.Next())
                        .ToList(),
                    PlannedPlacements = new List<PlacedObject>(),
                    UsedPrefabIds = CollectUsedPrefabIds(state.placedRooms),
                    RoomUtils = activeRoomUtils,
                    DistrictTypeLabel = activeDistrictTypeLabel,
                    DistrictFilter = districtFilter,
                    YMin = state.YMin,
                    MaxOverlapBridges = maxOverlapBridges
                };

                // Stage 2: Iterative bridge placement loop
                var (bridgesPlaced, overlapCount) = PlanBridges(context);

                // Stage 3: Evaluate this plan using scoring metrics
                var bridgePrefabKeys = state.BridgePrefabKeys ??= BridgeUtil.BuildBridgePrefabKeys(state.TrunkRoomLists);
                var bridgeablePairs = BridgeUtil.CountBridgeablePairs(context.PlannedOpenConnectors, context.YMin, 40f, 8f, bridgePrefabKeys);
                var newConnectors = context.PlannedOpenConnectors.Count - state.openConnectors.Count;
                var planArea = ScoringUtil.CalculateTotalArea(context.PlannedRooms);
                var planClustering = ScoringUtil.CalculateAverageMinimumDistance(context.PlannedRooms);
                var planSizeDiversity = ScoringUtil.CalculateSmallRoomChainPenalty(context.PlannedRooms);
                var planRoomReuse = ScoringUtil.CalculateRoomReuseScore(context.PlannedRooms);
                var connectorViability = ScoringUtil.CalculateConnectorViabilityArea(context.PlannedRooms, context.PlannedOpenConnectors);
                var duplicateRoomPenalty = ScoringUtil.CalculateDuplicateRoomPenalty(context.PlannedRooms);
                var planBaseline = ScoringUtil.ComputePlacementBaseline(context.PlannedRooms);
                var planCompactness = planBaseline.Compactness;
                var planDeadConnectors = ScoringUtil.CountDeadConnectors(planBaseline.RoomBounds, null, context.PlannedOpenConnectors);
                var planScore = ScoringUtil.ScorePlan(state.scoringSystem, bridgesPlaced, bridgeablePairs, overlapCount, Math.Max(0, newConnectors), planArea, planClustering, planSizeDiversity, planRoomReuse, connectorViability, duplicateRoomPenalty, planCompactness, planDeadConnectors);

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
                        Score = planScore,
                        RejectCounts = context.RejectCounts
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

            if (!state.IsHarnessRun && !RetrogradeContext.Quiet)
            {
                Console.WriteLine($"[Bridge plan] best of {maxPlans} attempts (attempt {bestPlanAttempt}): placed {bestBridgesPlaced}/{TargetBridgeCount} bridge prefabs, overlap {finalOverlapCount}, {ScoringUtil.PrettyPrintScore(finalScore, includeBridgingOverlap: true)}.");
                var rejects = bestOutcome?.Metadata?.RejectCounts;
                if (rejects != null)
                    Console.WriteLine($"[Bridge plan] rejects: {rejects}");
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

                // Build candidate pairs, prioritizing cross-district bridges
                var candidatePairs = new List<(int i, int j, bool crossDistrict)>();
                for (int i = 0; i < ctx.PlannedOpenConnectors.Count - 1; i++)
                {
                    for (int j = i + 1; j < ctx.PlannedOpenConnectors.Count; j++)
                    {
                        var a = ctx.PlannedOpenConnectors[i];
                        var b = ctx.PlannedOpenConnectors[j];

                        if (!IsValidBridgePair(a, b, ctx.PlannedRooms))
                            continue;

                        bool crossDistrict = !string.Equals(a.DistrictType, b.DistrictType, StringComparison.OrdinalIgnoreCase);
                        candidatePairs.Add((i, j, crossDistrict));
                    }
                }

                // Try cross-district pairs first, then closer pairs before distant ones
                candidatePairs.Sort((x, y) =>
                {
                    int crossCmp = y.crossDistrict.CompareTo(x.crossDistrict);
                    if (crossCmp != 0) return crossCmp;

                    float distX = SquaredDistance(ctx.PlannedOpenConnectors[x.i], ctx.PlannedOpenConnectors[x.j]);
                    float distY = SquaredDistance(ctx.PlannedOpenConnectors[y.i], ctx.PlannedOpenConnectors[y.j]);
                    return distX.CompareTo(distY);
                });

                foreach (var (ci, cj, _) in candidatePairs)
                {
                    var a = ctx.PlannedOpenConnectors[ci];
                    var b = ctx.PlannedOpenConnectors[cj];

                    // Stage 2b: Try to place a bridge prefab connecting this pair
                    var result = TryPlaceBridgeBetween(a, b, ctx);
                    if (result == null)
                        continue;

                    // Stage 2c: Accept placement and update state
                    AcceptBridgePlacement(ctx, result, ci, cj);

                    bridgesPlaced++;
                    if (BridgeUtil.HaveSameOwner(ctx.PlannedRooms, a, b, ConnectorPositionTolerance))
                    {
                        overlapCount++;
                        if (overlapCount >= ctx.MaxOverlapBridges)
                            return (bridgesPlaced, overlapCount);
                    }
                    progress = true;
                    break;
                }
            }

            return (bridgesPlaced, overlapCount);
        }

        /// <summary>
        /// Checks whether two open connectors form a valid candidate pair for bridging:
        /// both owners must have 2+ connectors, belong to different rooms, and be compatible.
        /// </summary>
        private static bool IsValidBridgePair(OpenConnector a, OpenConnector b, List<PlacedRoom> plannedRooms)
        {
            if (!RoomHasMultipleConnectors(plannedRooms, a, ConnectorPositionTolerance) ||
                !RoomHasMultipleConnectors(plannedRooms, b, ConnectorPositionTolerance))
                return false;

            if (BridgeUtil.HaveSameOwner(plannedRooms, a, b, ConnectorPositionTolerance))
                return false;

            if (HaveIdenticalPrefab(plannedRooms, a, b))
                return false;

            return BridgeUtil.ArePairCompatible(a, b);
        }

        /// <summary>
        /// Returns true if the two connectors belong to rooms that use the same prefab,
        /// preventing identical rooms from being placed next to each other via bridging.
        /// </summary>
        private static bool HaveIdenticalPrefab(List<PlacedRoom> plannedRooms, OpenConnector a, OpenConnector b)
        {
            int ownerA = BridgeUtil.ResolveConnectorOwner(plannedRooms, a, ConnectorPositionTolerance);
            int ownerB = BridgeUtil.ResolveConnectorOwner(plannedRooms, b, ConnectorPositionTolerance);

            if (ownerA < 0 || ownerB < 0)
                return false;

            var prefabA = plannedRooms[ownerA].Prefab?.PrefabEditorId;
            var prefabB = plannedRooms[ownerB].Prefab?.PrefabEditorId;

            if (string.IsNullOrEmpty(prefabA) || string.IsNullOrEmpty(prefabB))
                return false;

            return string.Equals(prefabA, prefabB, StringComparison.OrdinalIgnoreCase);
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
            {
                ctx.RejectCounts.NoCandidates++;
                return null;
            }

            // Prefer smaller bridge prefabs to conserve collision space, with random jitter
            var prefabsToTry = candidates
                .Select(id => (id, prefab: PrefabCache.GetPrefab(id)))
                .Select(p =>
                {
                    var bounds = p.prefab.packin_instance?.ObjectBounds;
                    float volume = bounds == null ? 0f :
                        Math.Abs((bounds.Second.X - bounds.First.X) *
                                 (bounds.Second.Y - bounds.First.Y) *
                                 (bounds.Second.Z - bounds.First.Z));
                    return (p.id, volume);
                })
                .OrderBy(p => p.volume + RandomProvider.Random.NextDouble() * 500.0)
                .Take(Math.Max(1, Math.Min(MaxPrefabsToTryPerPair, candidates.Count)))
                .Select(p => p.id)
                .ToList();

            foreach (var prefabId in prefabsToTry)
            {
                var prefab = PrefabCache.GetPrefab(prefabId);

                // Pre-check: does this prefab have connectors compatible with both sides
                // at any rotation? Tileset/doorsize are rotation-invariant, so check once.
                var rawConnectors = ConnectorUtils.GetConnectors(prefab, 0);
                bool hasMatchA = rawConnectors.Any(c =>
                    string.Equals(c.Parsed.DoorSize, a.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.Parsed.Tileset, a.Parsed.Tileset, StringComparison.OrdinalIgnoreCase));
                bool hasMatchB = rawConnectors.Any(c =>
                    string.Equals(c.Parsed.DoorSize, b.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.Parsed.Tileset, b.Parsed.Tileset, StringComparison.OrdinalIgnoreCase));
                if (!hasMatchA || !hasMatchB)
                {
                    ctx.RejectCounts.NoConnectorMatch++;
                    continue;
                }

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

                    if (!MathUtil.PositionsClose(expectedB, b.WorldPos, BridgeAlignmentTolerance))
                    {
                        float maxAxis = Math.Max(Math.Abs(expectedB.X - b.WorldPos.X),
                            Math.Max(Math.Abs(expectedB.Y - b.WorldPos.Y), Math.Abs(expectedB.Z - b.WorldPos.Z)));
                        if (maxAxis < ctx.RejectCounts.ClosestAlignmentMiss)
                            ctx.RejectCounts.ClosestAlignmentMiss = maxAxis;
                        ctx.RejectCounts.Alignment++;
                        continue;
                    }

                    // Validate placement constraints
                    var candidateAabb = ConnectorUtils.ToWorldAabbRotated(prefab.packin_instance.ObjectBounds, prefabPos, yawSteps);
                    if (ConnectorUtils.IsBelowYMin(candidateAabb, ctx.YMin))
                    {
                        ctx.RejectCounts.BelowYMin++;
                        continue;
                    }
                    if (ConnectorUtils.CollidesWithAny(candidateAabb, ctx.PlannedRooms, CollisionPadding))
                    {
                        ctx.RejectCounts.Collision++;
                        continue;
                    }
                    if (BridgeUtil.AnyConnectorInsideExistingBounds(connectors, prefabPos, ctx.PlannedRooms, ConnectorEmbedTolerance))
                    {
                        ctx.RejectCounts.ConnectorEmbed++;
                        continue;
                    }
                    if (BridgeUtil.AnyExistingConnectorInsideCandidate(candidateAabb, ctx.PlannedRooms, ConnectorEmbedTolerance))
                    {
                        ctx.RejectCounts.ExistingConnectorEmbed++;
                        continue;
                    }

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

            var unusedRooms = distinct.Where(id => !usedPrefabIds.Contains(id) && !IsBlocker(id)).ToList();
            var usedRooms = distinct.Where(id => usedPrefabIds.Contains(id) && !IsBlocker(id)).ToList();
            var blockers = distinct.Where(id => IsBlocker(id)).ToList();

            return Shuffle(unusedRooms)
                .Concat(Shuffle(usedRooms))
                .Concat(Shuffle(blockers))
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

        private static bool RoomHasMultipleConnectors(List<PlacedRoom> placedRooms, OpenConnector open, float tolerance)
        {
            if (placedRooms == null || placedRooms.Count == 0)
                return false;

            int ownerIndex = BridgeUtil.ResolveConnectorOwner(placedRooms, open, tolerance);
            if (ownerIndex < 0 || ownerIndex >= placedRooms.Count)
                return false;

            var owner = placedRooms[ownerIndex];
            return owner.Connectors != null && owner.Connectors.Count > 1;
        }

        private static float SquaredDistance(OpenConnector a, OpenConnector b)
        {
            float dx = a.WorldPos.X - b.WorldPos.X;
            float dy = a.WorldPos.Y - b.WorldPos.Y;
            float dz = a.WorldPos.Z - b.WorldPos.Z;
            return dx * dx + dy * dy + dz * dz;
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
