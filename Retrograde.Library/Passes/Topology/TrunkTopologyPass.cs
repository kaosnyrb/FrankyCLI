using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Passes
{
    public class TrunkTopologyPass : IGenPass
    {
        // Room placement limits
        private readonly int maxRoomsToPlace = 10;          // Maximum number of rooms to place
        private const int MaxAttempts = 1000;      // Hard limit (failed tries) to avoid infinite loops

        // Collision and spacing parameters
        private const float CollisionPadding = -0.5f; // World units clearance for collision detection

        // Prefab selection parameters
        private const int MaxCandidatePrefabsPerConnector = 8; // Avoid thrashing on a single open connector

        // Bridge placement constraints
        private const float BridgeMaxHorizontalSpan = 40f; // Keep connectors within bridge prefab span range
        private const float BridgeMaxVerticalOffset = 8f;  // Maximum vertical difference for bridgeable connectors

        // Testing: force specific prefabs to be placed
        private static readonly List<string> PrefabsToForcePlacement = new List<string>
        {
            // Add prefab EditorIDs here to force a placement attempt for testing new prefabs.
            //"rg_sts_trk_cgo_005"
        };

        public TrunkTopologyPass(int roomtarget)
        {
            maxRoomsToPlace = roomtarget;
        }

        private class TrunkPlanMeta
        {
            public int RoomsPlaced;
            public int BridgeablePairs;
            public int NewConnectors;
            public int MissingRequiredPrefabs;
            public PlanScore Score;
        }

        /// <summary>
        /// Context object that holds the shared state for room placement operations.
        /// Reduces parameter passing between helper methods.
        /// </summary>
        private class PlacementContext
        {
            public List<PlacedRoom> PlannedRooms { get; set; } = new List<PlacedRoom>();
            public List<OpenConnector> PlannedOpenConnectors { get; set; } = new List<OpenConnector>();
            public float YMin { get; set; }
            public HashSet<string> BridgePrefabKeys { get; set; } = new HashSet<string>();
            public string DistrictType { get; set; } = string.Empty;
            public P3Float ClusterCenter { get; set; }
            public List<string> RequiredPrefabs { get; set; } = new List<string>();
            public HashSet<string> UsedPrefabIds { get; set; } = new HashSet<string>();
            public RoomUtils RoomUtils { get; set; } = null!;
        }

        /// <summary>
        /// Main algorithm: Generates trunk room layouts by iteratively placing rooms on open connectors.
        /// Uses a multi-plan approach to find the best layout based on scoring criteria.
        /// </summary>
        public void RunPass(DungeonState state)
        {
            const string districtType = "trunk";

            // Initialize state and utilities
            int maxPlans = state.scoringSystem.Effort; // Cap number of planning retries
            var bridgePrefabKeys = state.BridgePrefabKeys ??= BridgeUtil.BuildBridgePrefabKeys(state.TrunkRoomLists);
            RoomUtils roomUtils = state.GetRoomUtils("rg_trunklist");

            if (state.openConnectors == null || state.openConnectors.Count == 0)
            {
                throw new Exception("TrunkTopologyPass requires at least one open connector. Run StationSetupPass first.");
            }

            // Stage 1: Multi-plan generation - run multiple planning attempts to find optimal layout
            var bestOutcome = PlanRunner.RunBest<TrunkPlanMeta>(maxPlans, planAttempt =>
            {
                // Initialize planning state: track required prefabs and prefab reuse
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

                var plannedRooms = new List<PlacedRoom>(state.placedRooms);
                var plannedOpenConnectors = new List<OpenConnector>(state.openConnectors);
                var plannedPlacements = new List<PlacedObject>();

                // Create placement context to reduce parameter passing
                var context = new PlacementContext
                {
                    PlannedRooms = plannedRooms,
                    PlannedOpenConnectors = plannedOpenConnectors,
                    YMin = state.YMin,
                    BridgePrefabKeys = bridgePrefabKeys,
                    DistrictType = districtType,
                    RequiredPrefabs = requiredPrefabs,
                    UsedPrefabIds = usedPrefabIds,
                    RoomUtils = roomUtils
                };

                int connectorsAddedCount = 0;
                int roomsPlaced = 0;
                int attempts = 0;

                // Stage 2: Iterative room placement loop
                while (roomsPlaced < maxRoomsToPlace && context.PlannedOpenConnectors.Count > 0 && attempts < MaxAttempts)
                {
                    attempts++;

                    // Stage 2a: Select next connector to expand from
                    // Strategy: choose farthest connector from cluster center to encourage sprawl
                    context.ClusterCenter = ConnectorSelectionUtil.CalculateClusterCenter(context.PlannedRooms, context.PlannedOpenConnectors);
                    var northConnectors = context.PlannedOpenConnectors.Where(c => c.Parsed.Direction == ConnectorDirection.North).ToList();
                    double northBiasWeight = state.scoringSystem.NorthBiasWeight;
                    bool useNorthBias = northConnectors.Count > 0 && RandomProvider.Random.NextDouble() < northBiasWeight;

                    var targetPool = useNorthBias ? northConnectors : context.PlannedOpenConnectors;
                    if (targetPool == null || targetPool.Count == 0)
                    {
                        break;
                    }

                    var target = ConnectorSelectionUtil.ChooseFarthestOpenConnector(targetPool, context.ClusterCenter);
                    int openIndex = context.PlannedOpenConnectors.IndexOf(target);
                    if (openIndex < 0)
                    {
                        continue;
                    }

                    if (target.WorldPos.Y < context.YMin)
                    {
                        continue;
                    }

                    context.PlannedOpenConnectors.RemoveAt(openIndex);

                    // Stage 2b: Find best room for this connector
                    // Tries multiple prefab candidates at different rotations
                    var placementResult = TryPlaceRoomOnConnector(target, context);

                    if (placementResult == null)
                    {
                        // No valid placement found - return connector to pool for later closure
                        context.PlannedOpenConnectors.Add(target);
                        continue;
                    }

                    // Stage 2c: Accept the placement and update state
                    plannedPlacements.Add(placementResult.Placement);
                    context.PlannedRooms.Add(placementResult.Room);
                    context.UsedPrefabIds.Add(placementResult.Room.Prefab.PrefabEditorId);
                    if (placementResult.UsesRequiredPrefab && !string.IsNullOrEmpty(placementResult.PrefabId))
                    {
                        context.RequiredPrefabs.RemoveAll(id => id.Equals(placementResult.PrefabId, StringComparison.OrdinalIgnoreCase));
                    }
                    roomsPlaced++;
                    context.PlannedOpenConnectors.AddRange(placementResult.NewOpenConnectors);
                    connectorsAddedCount += placementResult.NewOpenConnectors.Count;
                }

                // Stage 3: Evaluate this plan using multiple scoring metrics
                // Calculate scoring components
                var bridgeablePairs = BridgeUtil.CountBridgeablePairs(context.PlannedOpenConnectors, context.YMin, BridgeMaxHorizontalSpan, BridgeMaxVerticalOffset, bridgePrefabKeys);
                var planArea = ScoringUtil.CalculateTotalArea(context.PlannedRooms);
                var planClustering = ScoringUtil.CalculateAverageMinimumDistance(context.PlannedRooms);
                var planSizeDiversity = ScoringUtil.CalculateSmallRoomChainPenalty(context.PlannedRooms);
                var planRoomReuse = ScoringUtil.CalculateRoomReuseScore(context.PlannedRooms);
                var connectorViability = ScoringUtil.CalculateConnectorViabilityArea(context.PlannedRooms, context.PlannedOpenConnectors);

                // Combine into final plan score
                var planScore = ScoringUtil.ScorePlan(state.scoringSystem, roomsPlaced, bridgeablePairs, 0, connectorsAddedCount, planArea, planClustering, planSizeDiversity, planRoomReuse, connectorViability);
                int missingRequiredPrefabs = context.RequiredPrefabs.Count;
                double adjustedPlanScore = planScore.Total - (missingRequiredPrefabs > 0 ? 100000 * missingRequiredPrefabs : 0);

                return new PlanOutcome<TrunkPlanMeta>
                {
                    Score = adjustedPlanScore,
                    Rooms = context.PlannedRooms,
                    OpenConnectors = context.PlannedOpenConnectors,
                    Placements = plannedPlacements,
                    YMin = context.YMin,
                    Metadata = new TrunkPlanMeta
                    {
                        RoomsPlaced = roomsPlaced,
                        BridgeablePairs = bridgeablePairs,
                        NewConnectors = connectorsAddedCount,
                        MissingRequiredPrefabs = missingRequiredPrefabs,
                        Score = planScore
                    }
                };
            });

            // Stage 4: Apply the best plan to the dungeon state
            var finalRooms = bestOutcome?.Rooms ?? new List<PlacedRoom>();
            var finalOpenConnectors = bestOutcome?.OpenConnectors ?? new List<OpenConnector>();
            var finalPlacements = bestOutcome?.Placements ?? new List<PlacedObject>();
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

            var forcedInfo = PrefabsToForcePlacement.Count > 0
                ? $", forced remaining {bestOutcome?.Metadata?.MissingRequiredPrefabs ?? PrefabsToForcePlacement.Count}"
                : string.Empty;

            int bestPlanAttempt = (bestOutcome?.AttemptIndex ?? -1) + 1;
            int bestRoomsPlaced = bestOutcome?.Metadata?.RoomsPlaced ?? 0;
            int bestBridgeablePairs = bestOutcome?.Metadata?.BridgeablePairs ?? -1;
            int bestNewConnectors = bestOutcome?.Metadata?.NewConnectors ?? 0;

            if (!state.IsHarnessRun)
            {
                Console.WriteLine($"[Trunk Plan] best of {maxPlans} attempts (attempt {bestPlanAttempt}): placed {bestRoomsPlaced}/{maxRoomsToPlace} rooms, bridgeable pairs {bestBridgeablePairs}, new connectors {bestNewConnectors}{forcedInfo}, {ScoringUtil.PrettyPrintScore(finalScore, includeNewConnectors: true)}.");
            }
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

        private class CandidatePlacement
        {
            public PlacedObject? Placement;
            public PlacedRoom Room;
            public List<OpenConnector>? NewOpenConnectors;
            public int BridgeScore;
        }

        private class PlacementResult
        {
            public PlacedObject Placement = null!;
            public PlacedRoom Room = new PlacedRoom();
            public List<OpenConnector> NewOpenConnectors = null!;
            public bool UsesRequiredPrefab;
            public string? PrefabId;
        }

        /// <summary>
        /// Attempts to find the best room placement for a target connector by trying multiple prefab candidates.
        /// Strategy: Tries required prefabs first, then random unused prefabs. Evaluates each prefab at all rotations.
        /// Returns the placement with the highest bridge score, prioritizing required prefabs.
        /// </summary>
        private static PlacementResult? TryPlaceRoomOnConnector(OpenConnector target, PlacementContext context)
        {
            var requiredDir = ConnectorUtils.Opposite(target.Parsed.Direction);

            PlacedObject? bestPlacement = null;
            PlacedRoom bestRoom = new PlacedRoom();
            List<OpenConnector>? bestNewOpenConnectors = null;
            int bestBridgeScore = BridgeUtil.CountBridgeablePairs(
                context.PlannedOpenConnectors,
                context.YMin,
                BridgeMaxHorizontalSpan,
                BridgeMaxVerticalOffset,
                context.BridgePrefabKeys);
            bool bestPlacementUsesRequired = false;
            string? bestPlacementPrefabId = null;
            bool attemptedRequiredForThisConnector = false;

            for (int prefabTry = 0; prefabTry < MaxCandidatePrefabsPerConnector; prefabTry++)
            {
                // Select prefab: prioritize required prefabs, then try random unused ones
                bool useRequired = context.RequiredPrefabs.Count > 0 && !attemptedRequiredForThisConnector;
                if (useRequired)
                {
                    attemptedRequiredForThisConnector = true;
                }

                string prefabId;
                if (useRequired)
                {
                    prefabId = context.RequiredPrefabs.FirstOrDefault(id => !context.UsedPrefabIds.Contains(id));
                    context.RequiredPrefabs.RemoveAll(id => context.UsedPrefabIds.Contains(id));
                    if (string.IsNullOrEmpty(prefabId))
                    {
                        continue;
                    }
                }
                else if (!TryGetUnusedPrefabId(context.RoomUtils, target.Parsed.Tileset, "_trk_", context.UsedPrefabIds, MaxCandidatePrefabsPerConnector * 2, out prefabId))
                {
                    continue;
                }

                var nextPrefab = PrefabCache.GetPrefab(prefabId);

                // Evaluate all rotations of this prefab to find the best fit
                var candidate = EvaluatePrefabRotations(nextPrefab, target, requiredDir, context);

                if (candidate == null)
                    continue;

                // Compare this candidate with the current best, prioritizing required prefabs
                bool candidateIsForced = useRequired;
                if (bestPlacement == null
                    || (candidateIsForced && !bestPlacementUsesRequired)
                    || (candidateIsForced && bestPlacementUsesRequired && candidate.BridgeScore > bestBridgeScore)
                    || (!candidateIsForced && !bestPlacementUsesRequired && candidate.BridgeScore > bestBridgeScore))
                {
                    bestBridgeScore = candidate.BridgeScore;
                    bestPlacement = candidate.Placement;
                    bestRoom = candidate.Room;
                    bestNewOpenConnectors = candidate.NewOpenConnectors;
                    bestPlacementUsesRequired = useRequired;
                    bestPlacementPrefabId = nextPrefab.PrefabEditorId;
                }
            }

            if (bestPlacement == null)
                return null;

            return new PlacementResult
            {
                Placement = bestPlacement,
                Room = bestRoom,
                NewOpenConnectors = bestNewOpenConnectors,
                UsesRequiredPrefab = bestPlacementUsesRequired,
                PrefabId = bestPlacementPrefabId
            };
        }

        /// <summary>
        /// Evaluates all rotations (0°, 90°, 180°, 270°) of a prefab to find the best placement on a target connector.
        /// For each rotation: finds compatible connectors, checks collisions, calculates bridge score.
        /// Returns the rotation with the highest bridge score, or null if no valid placement exists.
        /// </summary>
        private static CandidatePlacement? EvaluatePrefabRotations(
            RoomPrefab nextPrefab,
            OpenConnector target,
            ConnectorDirection requiredDir,
            PlacementContext context)
        {
            CandidatePlacement? bestCandidate = null;
            int bestBridgeScore = -1;

            for (int yawSteps = 0; yawSteps < 4; yawSteps++)
            {
                var nextConnectors = ConnectorUtils.GetConnectors(nextPrefab, yawSteps);

                // Find connectors compatible with target (matching direction, size, tileset)
                var compatible = nextConnectors
                    .Where(c =>
                        c.Parsed.Direction == requiredDir &&
                        string.Equals(c.Parsed.DoorSize, target.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Parsed.Tileset, target.Parsed.Tileset, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (compatible.Count == 0)
                    continue;

                // Choose connector that points most outward (away from cluster center)
                var chosen = ConnectorSelectionUtil.ChooseMostOutwardConnector(compatible, target.WorldPos, context.ClusterCenter);
                P3Float nextPos = target.WorldPos - chosen.LocalPos;

                // Validate placement: check for collisions and height constraints
                var candidateAabb = ConnectorUtils.ToWorldAabbRotated(nextPrefab.packin_instance.ObjectBounds, nextPos, yawSteps);
                if (ConnectorUtils.IsBelowYMin(candidateAabb, context.YMin))
                    continue;
                if (ConnectorUtils.CollidesWithAny(candidateAabb, context.PlannedRooms, CollisionPadding))
                    continue;

                var candidateRoom = new PlacedRoom
                {
                    Prefab = nextPrefab,
                    WorldPos = nextPos,
                    YawSteps = yawSteps,
                    DistrictType = context.DistrictType,
                    Connectors = nextConnectors
                };

                var candidatePlacement = new PlacedObject(RetrogradeContext.Current.TargetMod)
                {
                    Count = 1,
                    Rotation = RgRotation.RotationToP3Float(yawSteps),
                    Position = nextPos,
                    Base = nextPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                };

                // Calculate how many new connector pairs could be bridged with this placement
                var newOpenConnectors = BuildOpenConnectors(nextConnectors, chosen, yawSteps, nextPos, context.DistrictType);
                var connectorsAfterPlacement = new List<OpenConnector>(context.PlannedOpenConnectors);
                connectorsAfterPlacement.AddRange(newOpenConnectors);

                int bridgeScore = BridgeUtil.CountBridgeablePairs(
                    connectorsAfterPlacement,
                    context.YMin,
                    BridgeMaxHorizontalSpan,
                    BridgeMaxVerticalOffset,
                    context.BridgePrefabKeys);

                // Keep the rotation with the best bridge score
                if (bestCandidate == null || bridgeScore > bestBridgeScore)
                {
                    bestBridgeScore = bridgeScore;
                    bestCandidate = new CandidatePlacement
                    {
                        Placement = candidatePlacement,
                        Room = candidateRoom,
                        NewOpenConnectors = newOpenConnectors,
                        BridgeScore = bridgeScore
                    };
                }
            }

            return bestCandidate;
        }
    }
}
