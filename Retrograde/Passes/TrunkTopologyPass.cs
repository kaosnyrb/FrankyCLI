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
        private static readonly string[] BridgeRoomLists = new[] { "rg_trunklist", "rg_bridgelist" };
        private static readonly Lazy<HashSet<string>> BridgePrefabKeys = new Lazy<HashSet<string>>(() => BridgeUtil.BuildBridgePrefabKeys(BridgeRoomLists));

        public void RunPass(DungeonState state)
        {
            const string districtType = "trunk";
            // Inputs / knobs
            int maxRoomsToPlace = 10;          // hard limit (rooms)
            int maxAttempts = 1000;              // hard limit (failed tries) to avoid infinite loops
            float collisionPadding = -0.5f; // tweak: world units clearance
            int maxCandidatePrefabsPerConnector = 8; // avoid thrashing on a single open connector
            int maxPlans = state.scoringSystem?.Effort ?? 100; // cap number of planning retries
            float bridgeMaxHorizontalSpan = 40f; // keep connectors within ranges bridge prefabs can span
            float bridgeMaxVerticalOffset = 8f;
            const int targetBridgeCount = 50; // aim to leave enough pairs for bridge pass
                                      
            switch (state.Size)
            {
                case "Small":
                    maxRoomsToPlace = 3 + RandomUtils.random.Next(2);
                    break;
                case "Medium":
                    maxRoomsToPlace = 4 + RandomUtils.random.Next(4);
                    break;
                case "Large":
                    maxRoomsToPlace = 6 + RandomUtils.random.Next(2);
                    break;
            }

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
            double bestPlanScore = double.MinValue;
            PlanScore? bestPlanScoreBreakdown = null;
            int bestPlanAttempt = -1;
            float bestYMin = state.StartingPosition.Y;

            for (int planAttempt = 0; planAttempt < maxPlans; planAttempt++)
            {
                for (int i = 0; i < 20; i++)
                {
                    var candidate = new RoomPrefab(roomUtils.GetRoom(startingConnector.Tileset, "_trk_"));

                    var candConnectors = candidate.Markers
                        .Select(m => new
                        {
                            Marker = m,
                            Conn = RgConnectorParser.Parse(m.MarkerEditorId)
                        })
                        .Where(x => x.Conn.IsValid)
                        .ToList();

                    var entry = candConnectors.FirstOrDefault(x => x.Conn.Direction == ConnectorDirection.South)?.Marker;

                    if (entry != null && candConnectors.Any(x => x.Conn.Direction != ConnectorDirection.South))
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


                var usedPrefabIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { roomPrefab.PrefabEditorId };
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

                // Count the initially placed room toward our plan so limits and logs reflect the total.
                int roomsPlaced = 1;
                int attempts = 0;
                var yMin = startingMarker.Position.Y;

                while (roomsPlaced < maxRoomsToPlace && plannedOpenConnectors.Count > 0 && attempts < maxAttempts)
                {
                    attempts++;

                    var clusterCenter = CalculateClusterCenter(plannedRooms, plannedOpenConnectors);
                    var northConnectors = plannedOpenConnectors.Where(c => c.Parsed.Direction == ConnectorDirection.North).ToList();
                    bool useNorthBias = northConnectors.Count > 0 && Rng.NextDouble() < 0.8;

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
                    int bestBridgeScore = CountBridgeablePairs(plannedOpenConnectors, yMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, BridgePrefabKeys.Value);

                    for (int prefabTry = 0; prefabTry < maxCandidatePrefabsPerConnector; prefabTry++)
                    {
                        var nextPrefab = new RoomPrefab(roomUtils.GetRoom(target.Parsed.Tileset,"_trk_"));
                        if (usedPrefabIds.Contains(nextPrefab.PrefabEditorId))
                            continue;

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
                            int bridgeScore = CountBridgeablePairs(connectorsAfterPlacement, yMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, BridgePrefabKeys.Value);

                            if (bestPlacement == null || bridgeScore > bestBridgeScore)
                            {
                                bestBridgeScore = bridgeScore;
                                bestPlacement = candidatePlacement;
                                bestRoom = candidateRoom;
                                bestNewOpenConnectors = newOpenConnectors;
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
                    roomsPlaced++;
                    plannedOpenConnectors.AddRange(bestNewOpenConnectors);
                }

                bool success = roomsPlaced >= maxRoomsToPlace;
                var bridgeablePairs = CountBridgeablePairs(plannedOpenConnectors, yMin, bridgeMaxHorizontalSpan, bridgeMaxVerticalOffset, BridgePrefabKeys.Value);
                var planScore = ScoringUtil.ScorePlan(state.scoringSystem, roomsPlaced, bridgeablePairs);
                if (planScore.Total > bestPlanScore)
                {
                    bestBridgeablePairs = bridgeablePairs;
                    bestPlannedRooms = plannedRooms;
                    bestPlannedOpenConnectors = plannedOpenConnectors;
                    bestPlannedPlacements = plannedPlacements;
                    bestRoomsPlaced = roomsPlaced;
                    bestPlanScore = planScore.Total;
                    bestPlanScoreBreakdown = planScore;
                    bestPlanAttempt = planAttempt;
                    bestYMin = yMin;
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
                    { "Bridging", 0 }
                }
            };

            foreach (var placement in finalPlacements)
            {
                state.instance.Temporary.Add(placement);
            }
            state.placedRooms = finalRooms;
            state.openConnectors = finalOpenConnectors;
            state.YMin = bestYMin;

            Console.WriteLine($"[Trunk Plan] best of {maxPlans} attempts (attempt {bestPlanAttempt + 1}): placed {bestRoomsPlaced}/{maxRoomsToPlace} rooms, bridgeable pairs {bestBridgeablePairs}/{targetBridgeCount}, score {finalScore.Total:0.00} (placement {finalScore.Components["Placement"]:0.00}, bridging {finalScore.Components["Bridging"]:0.00}).");
        }

        private static OpenConnector ChooseFarthestOpenConnector(List<OpenConnector> openConnectors, P3Float clusterCenter)
        {
            float maxDist = float.MinValue;
            OpenConnector best = openConnectors[0];
            for (int i = 0; i < openConnectors.Count; i++)
            {
                var dist = DistanceSquared(openConnectors[i].WorldPos, clusterCenter);
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
            float bestDist = DistanceSquared(targetWorldPos - best.LocalPos, clusterCenter);

            foreach (var c in compatibles)
            {
                float dist = DistanceSquared(targetWorldPos - c.LocalPos, clusterCenter);
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

        private static float DistanceSquared(P3Float a, P3Float b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
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

        private static int CountBridgeablePairs(List<OpenConnector> connectors, float yMin, float maxHorizontalSpan, float maxVerticalOffset, HashSet<string> bridgeKeys)
        {
            if (connectors == null || connectors.Count < 2)
                return 0;

            int count = 0;

            for (int i = 0; i < connectors.Count - 1; i++)
            {
                var a = connectors[i];
                if (!a.Parsed.IsValid || a.WorldPos.Y < yMin)
                    continue;

                for (int j = i + 1; j < connectors.Count; j++)
                {
                    var b = connectors[j];
                    if (!b.Parsed.IsValid || b.WorldPos.Y < yMin)
                        continue;

                    if (!string.Equals(a.Parsed.Tileset, b.Parsed.Tileset, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(a.Parsed.DoorSize, b.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase))
                        continue;

                    float dx = a.WorldPos.X - b.WorldPos.X;
                    float dy = a.WorldPos.Y - b.WorldPos.Y;
                    float dz = a.WorldPos.Z - b.WorldPos.Z;

                    if (MathF.Max(MathF.Abs(dx), MathF.Abs(dy)) > maxHorizontalSpan)
                        continue;

                    if (MathF.Abs(dz) > maxVerticalOffset)
                        continue;

                    if (bridgeKeys != null && bridgeKeys.Count > 0)
                    {
                        if (!BridgeUtil.TryBuildBridgeKey(a, b, out var key))
                            continue;
                        if (!bridgeKeys.Contains(key))
                            continue;
                    }

                    count++;
                }
            }

            return count;
        }
    }
}
