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
    public class BossTopologyPass : IGenPass
    {
        string district = null;
        private readonly string districtTypeLabel;
        public BossTopologyPass(string districtType = null) {         
            district = districtType;
            districtTypeLabel = string.IsNullOrWhiteSpace(districtType) ? "boss" : districtType;
        }
        public void RunPass(DungeonState state)
        {
            const string spineDistrictType = "trunk";
            // Inputs / knobs
            int maxRoomsToPlace = 1;          // boss: only place a single room
            int maxAttempts = 100;              // hard limit (failed tries) to avoid infinite loops
            float collisionPadding = -1.5f; // tweak: world units clearance
            int maxCandidatePrefabsPerConnector = 32; // avoid thrashing on a single open connector
            int maxPlans = state.scoringSystem?.Effort ?? 20;
            float bridgeMaxHorizontalSpan = 40f;
            float bridgeMaxVerticalOffset = 8f;
            var bridgePrefabKeys = state.BridgePrefabKeys ??= BridgeUtil.BuildBridgePrefabKeys(state.TrunkRoomLists);

            RoomUtils roomUtils = state.GetRoomUtils("rg_bosslist");
            RoomUtils spineUtils = state.GetRoomUtils("rg_trunklist");

            List<PlacedRoom> bestPlannedRooms = null;
            List<OpenConnector> bestPlannedOpenConnectors = null;
            List<PlacedObject> bestPlannedPlacements = null;
            int bestRoomsPlaced = 0;
            double bestPlanScore = double.MinValue;
            PlanScore? bestPlanScoreBreakdown = null;
            int bestPlanAttempt = -1;
            int bestBridgeablePairs = -1;

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

                // Ensure we start with an open north connector by extending the spine if needed.
                if (!plannedOpenConnectors.Any(c => c.Parsed.Direction == ConnectorDirection.North))
                {
                    if (!TryPlaceSpineNorthConnector(plannedRooms, plannedOpenConnectors, plannedPlacements, state.StartingPosition, state.YMin, spineUtils, collisionPadding, maxCandidatePrefabsPerConnector, spineDistrictType))
                    {
                            Console.WriteLine("[Boss plan] {0}/{1} failed to seed north connector.", planAttempt + 1, maxPlans);
                        continue;
                    }
                }

                // Main placement loop: iterates over open connectors, but bounded
                while (roomsPlaced < maxRoomsToPlace && plannedOpenConnectors.Count > 0 && attempts < maxAttempts)
                {
                    attempts++;

                    // Choose the NORTH-facing open connector farthest from the starting position to anchor the boss room
                    int openIndex = ConnectorUtils.ChooseFarthestNorthFromStart(plannedOpenConnectors, state.StartingPosition);
                    if (openIndex < 0 || attempts > maxAttempts - 5)
                    {
                        if (TryPlaceSpineNorthConnector(plannedRooms, plannedOpenConnectors, plannedPlacements, state.StartingPosition, state.YMin, spineUtils, collisionPadding, maxCandidatePrefabsPerConnector, spineDistrictType))
                        {
                            // We added a north-facing connector; try again.
                            continue;
                        }
                        // No suitable north-facing connector remains
                        break;
                    }
                    var target = plannedOpenConnectors[openIndex];

                    if (target.WorldPos.Y < state.YMin)
                    {
                        //MAKE SURE YOU DON'T GO -Y
                        continue;
                    }

                    // Remove it now to ensure we "try to iterate through all open connectors"
                    plannedOpenConnectors.RemoveAt(openIndex);

                    // We need a connector on nextPrefab that is OPPOSITE direction to target,
                    // and compatible on door/tileset (simple equality checks here).
                    var requiredDir = ConnectorUtils.Opposite(target.Parsed.Direction);

                    bool placed = false;

                    for (int prefabTry = 0; prefabTry < maxCandidatePrefabsPerConnector; prefabTry++)
                    {
                        var nextPrefab = PrefabCache.GetPrefab(roomUtils.GetRoom(target.Parsed.Tileset, district));

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
                            if (ConnectorUtils.IsBelowYMin(candidateAabb, state.YMin))
                                continue;
                            if (ConnectorUtils.CollidesWithAny(candidateAabb, plannedRooms, collisionPadding))
                                continue;

                            // Place it with rotation (planned)
                            plannedPlacements.Add(new PlacedObject(gen_quest_main.myMod)
                            {
                                Count = 1,
                                Rotation = RgRotation.RotationToP3Float(yawSteps),
                                Position = nextPos,
                                Base = nextPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                            });

                            plannedRooms.Add(new PlacedRoom
                            {
                                Prefab = nextPrefab,
                                WorldPos = nextPos,
                                YawSteps = yawSteps,
                                DistrictType = districtTypeLabel,
                                Connectors = nextConnectors
                            });

                            roomsPlaced++;
                            placed = true;

                            foreach (var c in nextConnectors)
                            {
                                if (c.EditorId == chosen.EditorId && c.LocalPos.Equals(chosen.LocalPos))
                                    continue;

                                plannedOpenConnectors.Add(new OpenConnector
                                {
                                    Parsed = c.Parsed,
                                    YawSteps = yawSteps,
                                    WorldPos = nextPos + c.LocalPos,
                                    DistrictType = districtTypeLabel
                                });
                            }

                            break;
                        }

                        if (placed)
                            break;
                    }

                    // If we couldn't place anything for this connector, we just move on.
                    if (!placed)
                    {
                        plannedOpenConnectors.Add(target);//Return it to the list so we close it later.
                        continue;
                    }
                }

                bool success = roomsPlaced >= maxRoomsToPlace;
                var bridgeablePairs = BridgeUtil.CountBridgeablePairs(plannedOpenConnectors, state.YMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, bridgePrefabKeys);
                var planArea = ScoringUtil.CalculateTotalArea(plannedRooms);
                var planClustering = ScoringUtil.CalculateAverageMinimumDistance(plannedRooms);
                var planSizeDiversity = ScoringUtil.CalculateSmallRoomChainPenalty(plannedRooms);
                var planRoomReuse = ScoringUtil.CalculateRoomReuseScore(plannedRooms);
                var connectorViability = ScoringUtil.CalculateConnectorViabilityArea(plannedRooms, plannedOpenConnectors);
                var planScore = ScoringUtil.ScorePlan(state.scoringSystem, roomsPlaced, bridgeablePairs, 0, 0, planArea, planClustering, planSizeDiversity, planRoomReuse, connectorViability);
                
                //Boss room must have placed a room
                if (roomsPlaced == 0)
                {
                    planScore.Total = double.MinValue;
                }

                if (planScore.Total > bestPlanScore)
                {
                    bestPlannedRooms = plannedRooms;
                    bestPlannedOpenConnectors = plannedOpenConnectors;
                    bestPlannedPlacements = plannedPlacements;
                    bestRoomsPlaced = roomsPlaced;
                    bestPlanScore = planScore.Total;
                    bestPlanScoreBreakdown = planScore;
                    bestPlanAttempt = planAttempt;
                    bestBridgeablePairs = bridgeablePairs;
                }
            }

            if (bestPlannedPlacements == null)
                throw new Exception("Couldn't place boss room");

            foreach (var placement in bestPlannedPlacements)
            {
                state.instance.Temporary.Add(placement);
            }
            state.placedRooms = bestPlannedRooms;
            state.openConnectors = bestPlannedOpenConnectors;

            var finalScore = bestPlanScoreBreakdown ?? new PlanScore
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

            Console.WriteLine($"[Boss plan] best of {maxPlans} attempts (attempt {bestPlanAttempt + 1}): placed {bestRoomsPlaced}/{maxRoomsToPlace} rooms, bridgeable pairs {bestBridgeablePairs}, {ScoringUtil.PrettyPrintScore(finalScore)}.");
        }

        private static bool TryPlaceSpineNorthConnector(
            List<PlacedRoom> plannedRooms,
            List<OpenConnector> plannedOpenConnectors,
            List<PlacedObject> plannedPlacements,
            P3Float startingPosition,
            float yMin,
            RoomUtils spineUtils,
            float collisionPadding,
            int maxCandidatePrefabsPerConnector,
            string districtType)
        {
            if (plannedOpenConnectors.Count == 0)
                return false;

            // Prefer targets further north to grow toward the boss goal.
            var targets = plannedOpenConnectors
                .OrderByDescending(c => c.WorldPos.Y)
                .ThenByDescending(c => MathUtil.DistanceSquared(c.WorldPos, startingPosition))
                .ToList();

            foreach (var target in targets)
            {
                if (target.WorldPos.Y < yMin)
                    continue;

                var requiredDir = ConnectorUtils.Opposite(target.Parsed.Direction);
                int targetIndex = plannedOpenConnectors.IndexOf(target);
                if (targetIndex < 0)
                    continue;

                for (int prefabTry = 0; prefabTry < maxCandidatePrefabsPerConnector; prefabTry++)
                {
                    var nextPrefab = PrefabCache.GetPrefab(spineUtils.GetRoom(target.Parsed.Tileset, "_trk_"));

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

                        foreach (var chosen in compatible)
                        {
                            bool hasNorthAvailable = nextConnectors.Any(c =>
                                c.Parsed.Direction == ConnectorDirection.North &&
                                !(c.EditorId == chosen.EditorId && c.LocalPos.Equals(chosen.LocalPos)));

                            if (!hasNorthAvailable)
                                continue;

                            P3Float nextPos = target.WorldPos - chosen.LocalPos;

                            var candidateAabb = ConnectorUtils.ToWorldAabbRotated(nextPrefab.packin_instance.ObjectBounds, nextPos, yawSteps);
                            if (ConnectorUtils.IsBelowYMin(candidateAabb, yMin))
                                continue;
                            if (ConnectorUtils.CollidesWithAny(candidateAabb, plannedRooms, collisionPadding))
                                continue;

                            plannedPlacements.Add(new PlacedObject(gen_quest_main.myMod)
                            {
                                Count = 1,
                                Rotation = RgRotation.RotationToP3Float(yawSteps),
                                Position = nextPos,
                                Base = nextPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                            });

                            plannedRooms.Add(new PlacedRoom
                            {
                                Prefab = nextPrefab,
                                WorldPos = nextPos,
                                YawSteps = yawSteps,
                                DistrictType = districtType,
                                Connectors = nextConnectors
                            });

                            plannedOpenConnectors.RemoveAt(targetIndex);

                            foreach (var c in nextConnectors)
                            {
                                if (c.EditorId == chosen.EditorId && c.LocalPos.Equals(chosen.LocalPos))
                                    continue;

                                plannedOpenConnectors.Add(new OpenConnector
                                {
                                    Parsed = c.Parsed,
                                    YawSteps = yawSteps,
                                    WorldPos = nextPos + c.LocalPos,
                                    DistrictType = districtType
                                });
                            }

                            return true;
                        }
                    }
                }
            }

            return false;
        }


    }
}
