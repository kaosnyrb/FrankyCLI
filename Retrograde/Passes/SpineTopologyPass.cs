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
    public class SpineTopologyPass : IGenPass
    {
        private static readonly Random Rng = new Random();

        public void RunPass(DungeonState state)
        {
            var startingMarker = state.instance.Persistent
                .OfType<PlacedObject>()
                .FirstOrDefault(m => m.EditorID.Contains("rg_conn_n"));

            if (startingMarker == null) throw new Exception("rg_conn_n not found.");
            var startingConnector = RgConnectorParser.Parse(startingMarker.EditorID);

            state.StartingPosition = startingMarker.Position;

            RoomPrefab roomPrefab = null;
            PrefabMarker south0 = new PrefabMarker();
            PrefabMarker north0 = new PrefabMarker();

            RoomUtils roomUtils = new RoomUtils("rg_spinelist");

            for (int i = 0; i < 20; i++)
            {
                var candidate = new RoomPrefab(roomUtils.GetRoom(startingConnector.Tileset, "spine"));

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

            // Inputs / knobs
            int maxRoomsToPlace = 10;          // hard limit (rooms)
            int maxAttempts = 1000;              // hard limit (failed tries) to avoid infinite loops
            float collisionPadding = -0.5f; // tweak: world units clearance
            int maxCandidatePrefabsPerConnector = 8; // avoid thrashing on a single open connector
            const int maxPlans = 30; // cap number of planning retries


            //Sizing tweaks
            switch (state.Size)
            {
                case "Small":
                    maxRoomsToPlace = 2 + RandomUtils.random.Next(2);
                    break;
                case "Medium":
                    maxRoomsToPlace = 4 + RandomUtils.random.Next(4);
                    break;
                case "Large":
                    maxRoomsToPlace = 6 + RandomUtils.random.Next(6);
                    break;
            }
            // Build initial room record (assumes you already placed roomPrefab at prefabWorldPos)
            var startConnectors = ConnectorUtils.GetConnectors(roomPrefab);

            for (int planAttempt = 0; planAttempt < maxPlans; planAttempt++)
            {
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
                            WorldPos = prefabWorldPos + c.LocalPos
                        });
                    }
                }

                int roomsPlaced = 0;
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

                    bool placed = false;

                    for (int prefabTry = 0; prefabTry < maxCandidatePrefabsPerConnector; prefabTry++)
                    {
                        var nextPrefab = new RoomPrefab(roomUtils.GetRoom(target.Parsed.Tileset,"spine"));
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
                                Connectors = nextConnectors
                            });
                            usedPrefabIds.Add(nextPrefab.PrefabEditorId);

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
                                    WorldPos = nextPos + c.LocalPos
                                });
                            }

                            break;
                        }

                        if (placed)
                            break;
                    }

                    if (!placed)
                    {
                        plannedOpenConnectors.Add(target);//Return it to the list so we close it later.
                        continue;
                    }
                }

                bool success = roomsPlaced >= maxRoomsToPlace;
                if (!success)
                {
                    Console.WriteLine("[Spine Plan] {0}/{1} aborted: placed {2}/{3} rooms.", planAttempt + 1, maxPlans, roomsPlaced, maxRoomsToPlace);                            
                    continue;
                }

                foreach (var placement in plannedPlacements)
                {
                    state.instance.Temporary.Add(placement);
                }
                state.placedRooms = plannedRooms;
                state.openConnectors = plannedOpenConnectors;
                state.YMin = yMin;
                Console.WriteLine("[Spine Plan] {0}/{1} success: placed {2}/{3} rooms.", planAttempt + 1, maxPlans, roomsPlaced, maxRoomsToPlace);
                return;
            }

            Console.WriteLine("SpineTopologyPass failed after {0} plan attempts.", maxPlans);
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

    }
}
