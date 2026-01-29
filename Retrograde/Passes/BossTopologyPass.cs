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

        private class BossPlanMeta
        {
            public int RoomsPlaced;
            public int BridgeablePairs;
            public PlanScore Score;
        }

        public BossTopologyPass(string districtType = null) {         
            district = districtType;
            districtTypeLabel = string.IsNullOrWhiteSpace(districtType) ? "boss" : districtType;
        }
        public void RunPass(DungeonState state)
        {
            // Inputs / knobs
            int maxRoomsToPlace = 1;          // boss: only place a single room
            int maxAttempts = 100;              // hard limit (failed tries) to avoid infinite loops
            float collisionPadding = -1.5f; // tweak: world units clearance
            int maxPlans = state.scoringSystem?.Effort ?? 20;
            float bridgeMaxHorizontalSpan = 40f;
            float bridgeMaxVerticalOffset = 8f;
            var bridgePrefabKeys = state.BridgePrefabKeys ??= BridgeUtil.BuildBridgePrefabKeys(state.TrunkRoomLists);

            RoomUtils roomUtils = state.GetRoomUtils("rg_bosslist");

            string chosenBossRoomEditorId = null;
            string GetOrChooseBossRoom(string tileset)
            {
                if (!string.IsNullOrWhiteSpace(chosenBossRoomEditorId))
                    return chosenBossRoomEditorId;

                chosenBossRoomEditorId = roomUtils.GetRoom(tileset, district);
                if (!state.IsHarnessRun)
                {
                    Console.WriteLine($"[Boss plan] Selected boss room prefab: {chosenBossRoomEditorId}");
                }
                return chosenBossRoomEditorId;
            }

            var bestOutcome = PlanRunner.RunBest<BossPlanMeta>(maxPlans, planAttempt =>
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

                // Main placement loop: iterates over open connectors, but bounded
                while (roomsPlaced < maxRoomsToPlace && plannedOpenConnectors.Count > 0 && attempts < maxAttempts)
                {
                    attempts++;

                    // Choose the open connector farthest from the starting position to anchor the boss room
                    int openIndex = -1;
                    float bestDist = float.MinValue;
                    for (int i = 0; i < plannedOpenConnectors.Count; i++)
                    {
                        float dist = MathUtil.DistanceSquared(plannedOpenConnectors[i].WorldPos, state.StartingPosition);
                        if (dist > bestDist)
                        {
                            bestDist = dist;
                            openIndex = i;
                        }
                    }
                    if (openIndex < 0)
                        break;
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

                    var bossPrefabEditorId = GetOrChooseBossRoom(target.Parsed.Tileset);
                    var nextPrefab = PrefabCache.GetPrefab(bossPrefabEditorId);

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

                    // If we couldn't place anything for this connector, we just move on.
                    if (!placed)
                    {
                        plannedOpenConnectors.Add(target);//Return it to the list so we close it later.
                        continue;
                    }
                }

                var bridgeablePairs = 0; // Boss placement ignores bridgeablePairs
                var planClustering = 0; // Boss placement planSizeDiversity
                var planSizeDiversity = 0; // Boss placement planSizeDiversity
                var planRoomReuse = ScoringUtil.CalculateRoomReuseScore(plannedRooms);
                var connectorViability = 0; // Boss placement planSizeDiversity
                const double planArea = 0; // Boss placement ignores area weighting
                var planScore = ScoringUtil.ScorePlan(state.scoringSystem, roomsPlaced, bridgeablePairs, 0, 0, planArea, planClustering, planSizeDiversity, planRoomReuse, connectorViability);
                
                //Boss room must have placed a room
                if (roomsPlaced == 0)
                {
                    planScore.Total = double.MinValue;
                }

                return new PlanOutcome<BossPlanMeta>
                {
                    Score = planScore.Total,
                    Rooms = plannedRooms,
                    OpenConnectors = plannedOpenConnectors,
                    Placements = plannedPlacements,
                    Metadata = new BossPlanMeta
                    {
                        RoomsPlaced = roomsPlaced,
                        BridgeablePairs = bridgeablePairs,
                        Score = planScore
                    }
                };
            });

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

            if (!state.IsHarnessRun)
            {
                Console.WriteLine($"[Boss plan] best of {maxPlans} attempts (attempt {bestPlanAttempt}): placed {bestRoomsPlaced}/{maxRoomsToPlace} rooms, bridgeable pairs {bestBridgeablePairs}, {ScoringUtil.PrettyPrintScore(finalScore)}.");
            }
        }

    }
}
