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
    public class BossTopologyPass : IGenPass
    {
        // Room placement limits
        private const int MaxRoomsToPlace = 1; // Boss rooms: only place a single room
        private const int MaxAttempts = 100; // Hard limit to avoid infinite loops

        // Collision and spacing parameters
        private const float CollisionPadding = -1.5f; // World units clearance

        // Bridge placement constraints (unused for boss, but kept for consistency)
        private const float BridgeMaxHorizontalSpan = 40f;
        private const float BridgeMaxVerticalOffset = 8f;

        private readonly string district;
        private readonly string districtTypeLabel;
        private readonly List<string> prefabsToForcePlacement;

        private class BossPlanMeta
        {
            public int RoomsPlaced;
            public int BridgeablePairs;
            public int MissingRequiredPrefabs;
            public PlanScore Score;
        }

        /// <summary>
        /// Result of attempting to place a boss room at a connector.
        /// </summary>
        private class BossPlacementResult
        {
            public PlacedRoom PlacedRoom { get; set; }
            public PlacedObject PlacementObject { get; set; }
            public List<OpenConnector> NewConnectors { get; set; }
        }

        public BossTopologyPass(string districtType = null, IEnumerable<string> prefabsToForcePlacement = null) {
            district = districtType;
            districtTypeLabel = string.IsNullOrWhiteSpace(districtType) ? "boss" : districtType;
            this.prefabsToForcePlacement = prefabsToForcePlacement?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? new List<string>();
        }

        /// <summary>
        /// Main algorithm: Places a single boss room at the connector farthest from the dungeon start.
        /// Uses a multi-plan approach to find the best placement based on scoring criteria.
        /// Strategy: Boss rooms anchor at the end of the dungeon to create a climactic encounter.
        /// </summary>
        public void RunPass(DungeonState state)
        {
            // Configuration: computed at runtime
            int maxPlans = state.scoringSystem?.Effort ?? 20;
            var bridgePrefabKeys = state.BridgePrefabKeys ??= BridgeUtil.BuildBridgePrefabKeys(state.TrunkRoomLists);

            RoomUtils roomUtils = state.GetRoomUtils("rg_bosslist");

            // Helper: Selects and caches the boss room prefab for consistency across plans
            string chosenBossRoomEditorId = null;
            string GetOrChooseBossRoom(string tileset)
            {
                if (!string.IsNullOrWhiteSpace(chosenBossRoomEditorId))
                    return chosenBossRoomEditorId;

                // Prefer forced prefab if one is specified
                if (prefabsToForcePlacement.Count > 0)
                {
                    chosenBossRoomEditorId = prefabsToForcePlacement[0];
                }
                else
                {
                    chosenBossRoomEditorId = roomUtils.GetRoom(tileset, district);
                }

                if (!state.IsHarnessRun)
                {
                    Console.WriteLine($"[Boss plan] Selected boss room prefab: {chosenBossRoomEditorId}");
                }
                return chosenBossRoomEditorId;
            }

            // Stage 1: Multi-plan generation - run multiple planning attempts to find optimal placement
            var bestOutcome = PlanRunner.RunBest<BossPlanMeta>(maxPlans, planAttempt =>
            {
                var plannedRooms = new List<PlacedRoom>(state.placedRooms);
                var plannedOpenConnectors = new List<OpenConnector>(state.openConnectors);
                var plannedPlacements = new List<PlacedObject>();

                int roomsPlaced = 0;
                int attempts = 0;

                // Stage 2: Room placement loop - find and place the boss room
                while (roomsPlaced < MaxRoomsToPlace && plannedOpenConnectors.Count > 0 && attempts < MaxAttempts)
                {
                    attempts++;

                    // Stage 2a: Select the connector farthest from start
                    // Strategy: Place boss room at dungeon's end for climactic encounter
                    var target = ConnectorSelectionUtil.ChooseFarthestOpenConnector(plannedOpenConnectors, state.StartingPosition);
                    int openIndex = plannedOpenConnectors.IndexOf(target);
                    if (openIndex < 0)
                        break;

                    if (target.WorldPos.Y < state.YMin)
                    {
                        //MAKE SURE YOU DON'T GO -Y
                        continue;
                    }

                    // Remove it now to ensure we "try to iterate through all open connectors"
                    plannedOpenConnectors.RemoveAt(openIndex);

                    // Stage 2b: Try to place boss room at selected connector
                    var bossPrefabEditorId = GetOrChooseBossRoom(target.Parsed.Tileset);
                    var bossPrefab = PrefabCache.GetPrefab(bossPrefabEditorId);

                    var placementResult = TryPlaceBossRoomAtConnector(target, bossPrefab, plannedRooms, state.YMin, districtTypeLabel);

                    if (placementResult == null)
                    {
                        // No valid placement found - return connector to list for later closure
                        plannedOpenConnectors.Add(target);
                        continue;
                    }

                    // Stage 2c: Accept the placement and update state
                    plannedPlacements.Add(placementResult.PlacementObject);
                    plannedRooms.Add(placementResult.PlacedRoom);
                    plannedOpenConnectors.AddRange(placementResult.NewConnectors);
                    roomsPlaced++;
                }

                // Stage 3: Evaluate this plan using scoring metrics
                var bridgeablePairs = 0; // Boss placement ignores bridgeablePairs
                var planClustering = 0; // Boss placement planSizeDiversity
                var planSizeDiversity = 0; // Boss placement planSizeDiversity
                var planRoomReuse = ScoringUtil.CalculateRoomReuseScore(plannedRooms);
                var connectorViability = 0; // Boss placement planSizeDiversity
                const double planArea = 0; // Boss placement ignores area weighting
                var planScore = ScoringUtil.ScorePlan(state.scoringSystem, roomsPlaced, bridgeablePairs, 0, 0, planArea, planClustering, planSizeDiversity, planRoomReuse, connectorViability);

                // Check if forced prefab was actually placed
                int missingRequiredPrefabs = 0;
                if (prefabsToForcePlacement.Count > 0 && roomsPlaced > 0)
                {
                    var placedPrefabIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var room in plannedRooms)
                    {
                        if (!string.IsNullOrEmpty(room.Prefab?.PrefabEditorId))
                            placedPrefabIds.Add(room.Prefab.PrefabEditorId);
                    }
                    missingRequiredPrefabs = prefabsToForcePlacement
                        .Count(id => !placedPrefabIds.Contains(id));
                }
                else if (prefabsToForcePlacement.Count > 0)
                {
                    missingRequiredPrefabs = prefabsToForcePlacement.Count;
                }

                double adjustedScore = planScore.Total - (missingRequiredPrefabs > 0 ? 100000 * missingRequiredPrefabs : 0);

                // Validation: Boss room must have been placed successfully
                if (roomsPlaced == 0)
                {
                    adjustedScore = double.MinValue;
                }

                return new PlanOutcome<BossPlanMeta>
                {
                    Score = adjustedScore,
                    Rooms = plannedRooms,
                    OpenConnectors = plannedOpenConnectors,
                    Placements = plannedPlacements,
                    Metadata = new BossPlanMeta
                    {
                        RoomsPlaced = roomsPlaced,
                        BridgeablePairs = bridgeablePairs,
                        MissingRequiredPrefabs = missingRequiredPrefabs,
                        Score = planScore
                    }
                };
            });

            // Stage 4: Apply the best plan to the dungeon state
            if (bestOutcome?.Placements == null)
                throw new Exception("Couldn't place boss room");

            foreach (var placement in bestOutcome.Placements)
            {
                state.PlacementUtil.AddToTemporary(state.instance, placement);
            }
            state.placedRooms = bestOutcome.Rooms;
            state.openConnectors = bestOutcome.OpenConnectors;

            var finalScore = bestOutcome.Metadata?.Score ?? new PlanScore
            {
                Total = 0,
                Components = new Dictionary<string, double>
                {
                    { "Placement", 0 },
                    { "Bridging", 0 },
                    { "Area", 0 },
                    { "Clustering", 0 },
                    { "SizeDiversity", 0 },
                    { "RoomReuse", 0 },
                    { "ConnectorViability", 0 }
                }
            };

            int bestPlanAttempt = (bestOutcome?.AttemptIndex ?? -1) + 1;
            int bestRoomsPlaced = bestOutcome?.Metadata?.RoomsPlaced ?? 0;
            int bestBridgeablePairs = bestOutcome?.Metadata?.BridgeablePairs ?? -1;

            var forcedInfo = prefabsToForcePlacement.Count > 0
                ? $", forced remaining {bestOutcome?.Metadata?.MissingRequiredPrefabs ?? prefabsToForcePlacement.Count}"
                : string.Empty;

            if (!state.IsHarnessRun)
            {
                Console.WriteLine($"[Boss plan] best of {maxPlans} attempts (attempt {bestPlanAttempt}): placed {bestRoomsPlaced}/{MaxRoomsToPlace} rooms, bridgeable pairs {bestBridgeablePairs}{forcedInfo}, {ScoringUtil.PrettyPrintScore(finalScore)}.");
            }
        }

        /// <summary>
        /// Attempts to place a boss room at the target connector by evaluating all rotations.
        /// Tries all 4 rotations (0°, 90°, 180°, 270°) to find a compatible, non-colliding placement.
        /// </summary>
        /// <returns>Placement result if successful, null if no valid placement found.</returns>
        private static BossPlacementResult TryPlaceBossRoomAtConnector(
            OpenConnector target,
            RoomPrefab bossPrefab,
            List<PlacedRoom> plannedRooms,
            float yMin,
            string districtTypeLabel)
        {
            var requiredDir = ConnectorUtils.Opposite(target.Parsed.Direction);

            // Evaluate all 4 rotations in random order
            var yawOrder = Enumerable.Range(0, 4)
                .OrderBy(_ => RandomProvider.Random.Next())
                .ToList();

            foreach (var yawSteps in yawOrder)
            {
                var nextConnectors = ConnectorUtils.GetConnectors(bossPrefab, yawSteps);

                // Find compatible connectors (matching direction, door size, and tileset)
                var compatible = nextConnectors
                    .Where(c =>
                        c.Parsed.Direction == requiredDir &&
                        string.Equals(c.Parsed.DoorSize, target.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Parsed.Tileset, target.Parsed.Tileset, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (compatible.Count == 0)
                    continue;

                // Randomly choose one compatible connector
                var chosen = compatible[RandomProvider.Random.Next(compatible.Count)];

                // Calculate world position by aligning the chosen connector with the target
                P3Float nextPos = target.WorldPos - chosen.LocalPos;

                // Check collision and height constraints
                var candidateAabb = ConnectorUtils.ToWorldAabbRotated(bossPrefab.packin_instance.ObjectBounds, nextPos, yawSteps);
                if (ConnectorUtils.IsBelowYMin(candidateAabb, yMin))
                    continue;
                if (ConnectorUtils.CollidesWithAny(candidateAabb, plannedRooms, CollisionPadding))
                    continue;

                // Valid placement found - create placement objects
                var placementObject = new PlacedObject(RetrogradeContext.Current.TargetMod)
                {
                    Count = 1,
                    Rotation = RgRotation.RotationToP3Float(yawSteps),
                    Position = nextPos,
                    Base = bossPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                };

                var placedRoom = new PlacedRoom
                {
                    Prefab = bossPrefab,
                    WorldPos = nextPos,
                    YawSteps = yawSteps,
                    DistrictType = districtTypeLabel,
                    Connectors = nextConnectors
                };

                // Collect all new open connectors (excluding the one we connected to)
                var newConnectors = new List<OpenConnector>();
                foreach (var c in nextConnectors)
                {
                    if (c.EditorId == chosen.EditorId && c.LocalPos.Equals(chosen.LocalPos))
                        continue;

                    newConnectors.Add(new OpenConnector
                    {
                        Parsed = c.Parsed,
                        YawSteps = yawSteps,
                        WorldPos = nextPos + c.LocalPos,
                        DistrictType = districtTypeLabel
                    });
                }

                return new BossPlacementResult
                {
                    PlacedRoom = placedRoom,
                    PlacementObject = placementObject,
                    NewConnectors = newConnectors
                };
            }

            // No valid placement found in any rotation
            return null;
        }

    }
}
