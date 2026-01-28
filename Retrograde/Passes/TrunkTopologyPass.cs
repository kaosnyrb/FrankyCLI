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
            var bridgePrefabKeys = state.BridgePrefabKeys ??= BridgeUtil.BuildBridgePrefabKeys(state.TrunkRoomLists);
            RoomUtils roomUtils = state.GetRoomUtils("rg_trunklist");

            if (state.openConnectors == null || state.openConnectors.Count == 0)
            {
                throw new Exception("TrunkTopologyPass requires at least one open connector. Run StationSetupPass first.");
            }

            var bestOutcome = PlanRunner.RunBest<TrunkPlanMeta>(maxPlans, planAttempt =>
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

                var plannedRooms = new List<PlacedRoom>(state.placedRooms);
                var plannedOpenConnectors = new List<OpenConnector>(state.openConnectors);
                var plannedPlacements = new List<PlacedObject>();

                int connectorsAddedCount = 0;
                int roomsPlaced = 0;
                int attempts = 0;
                var yMin = state.YMin;

                while (roomsPlaced < maxRoomsToPlace && plannedOpenConnectors.Count > 0 && attempts < maxAttempts)
                {
                    attempts++;

                    var clusterCenter = ConnectorSelectionUtil.CalculateClusterCenter(plannedRooms, plannedOpenConnectors);
                    var northConnectors = plannedOpenConnectors.Where(c => c.Parsed.Direction == ConnectorDirection.North).ToList();
                    double northBiasWeight = state.scoringSystem.NorthBiasWeight;
                    bool useNorthBias = northConnectors.Count > 0 && Rng.NextDouble() < northBiasWeight;

                    var targetPool = useNorthBias ? northConnectors : plannedOpenConnectors;
                    if (targetPool == null || targetPool.Count == 0)
                    {
                        break;
                    }

                    var target = ConnectorSelectionUtil.ChooseFarthestOpenConnector(targetPool, clusterCenter);
                    int openIndex = plannedOpenConnectors.IndexOf(target);
                    if (openIndex < 0)
                    {
                        continue;
                    }

                    if (target.WorldPos.Y < yMin)
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

                        var nextPrefab = PrefabCache.GetPrefab(prefabId);

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

                            var chosen = ConnectorSelectionUtil.ChooseMostOutwardConnector(compatible, target.WorldPos, clusterCenter);

                            P3Float nextPos = target.WorldPos - chosen.LocalPos;

                            var candidateAabb = ConnectorUtils.ToWorldAabbRotated(nextPrefab.packin_instance.ObjectBounds, nextPos, yawSteps);
                            if (ConnectorUtils.IsBelowYMin(candidateAabb, yMin))
                                continue;
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

                var bridgeablePairs = BridgeUtil.CountBridgeablePairs(plannedOpenConnectors, yMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, bridgePrefabKeys);
                var planArea = ScoringUtil.CalculateTotalArea(plannedRooms);
                var planClustering = ScoringUtil.CalculateAverageMinimumDistance(plannedRooms);
                var planSizeDiversity = ScoringUtil.CalculateSmallRoomChainPenalty(plannedRooms);
                var planRoomReuse = ScoringUtil.CalculateRoomReuseScore(plannedRooms);
                var connectorViability = ScoringUtil.CalculateConnectorViabilityArea(plannedRooms, plannedOpenConnectors);
                var planScore = ScoringUtil.ScorePlan(state.scoringSystem, roomsPlaced, bridgeablePairs, 0, connectorsAddedCount, planArea, planClustering, planSizeDiversity, planRoomReuse, connectorViability);
                int missingRequiredPrefabs = requiredPrefabs.Count;
                double adjustedPlanScore = planScore.Total - (missingRequiredPrefabs > 0 ? 100000 * missingRequiredPrefabs : 0);

                return new PlanOutcome<TrunkPlanMeta>
                {
                    Score = adjustedPlanScore,
                    Rooms = plannedRooms,
                    OpenConnectors = plannedOpenConnectors,
                    Placements = plannedPlacements,
                    YMin = yMin,
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
    }
}
