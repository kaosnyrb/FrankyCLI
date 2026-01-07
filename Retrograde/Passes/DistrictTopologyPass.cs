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
    public class DistrictTopologyPass : IGenPass
    {
        string district = null;
        public string roomlist = "";

        public DistrictTopologyPass(string p_roomlist, string districtType = null) {         
            district = districtType;
            roomlist = p_roomlist;
        }
        public void RunPass(DungeonState state)
        {
            // Inputs / knobs
            int maxRoomsToPlace = 10;          // hard limit (rooms)
            int maxAttempts = 1000;              // hard limit (failed tries) to avoid infinite loops
            float collisionPadding = -0.1f; // tweak: world units clearance
            int maxCandidatePrefabsPerConnector = 16; // avoid thrashing on a single open connector
            int proximitySample = 5; // bias: pick from the closest N connectors to keep the cluster tight
            const int maxPlans = 10; // retry count for full planning attempts
            RoomUtils roomUtils = new RoomUtils(roomlist);

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
                var yMin = state.YMin;

                // Main placement loop: iterates over open connectors, but bounded
                while (roomsPlaced < maxRoomsToPlace && plannedOpenConnectors.Count > 0 && attempts < maxAttempts)
                {
                    attempts++;

                    // Choose an open connector near the current cluster center to keep rooms close together
                    var clusterCenter = CalculateClusterCenter(plannedRooms, plannedOpenConnectors);
                    int openIndex = ChooseConnectorIndexNearCenter(plannedOpenConnectors, clusterCenter, proximitySample);
                    var target = plannedOpenConnectors[openIndex];

                    if (target.WorldPos.Y < yMin)
                    {
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
                        var prefabId = ChoosePrefabId(roomUtils, target.Parsed.Tileset, district, usedPrefabIds);
                        var nextPrefab = new RoomPrefab(prefabId);

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

                    // If we couldn't place anything for this connector, we just move on.
                    if (!placed)
                    {
                        plannedOpenConnectors.Add(target);//Return it to the list so we close it later.
                        continue;
                    }
                }

                bool success = roomsPlaced >= maxRoomsToPlace;
                if (!success)
                {
                    Console.WriteLine("[District plan] {0}/{1} aborted: placed {2}/{3} rooms.", planAttempt + 1, maxPlans, roomsPlaced, maxRoomsToPlace);
                    continue;
                }

                foreach (var placement in plannedPlacements)
                {
                    state.instance.Temporary.Add(placement);
                }
                state.placedRooms = plannedRooms;
                state.openConnectors = plannedOpenConnectors;

                Console.WriteLine("[District plan] {0}/{1} success: placed {2}/{3} rooms.", planAttempt + 1, maxPlans, roomsPlaced, maxRoomsToPlace);
                return;
            }

            Console.WriteLine("DistrictTopologyPass failed after {0} plan attempts.", maxPlans);

        }

        private static int ChooseConnectorIndexNearCenter(List<OpenConnector> openConnectors, P3Float clusterCenter, int sampleSize)
        {
            var prioritized = openConnectors
                .Select((c, idx) => new
                {
                    Index = idx,
                    DistSq = DistanceSquared(c.WorldPos, clusterCenter)
                })
                .OrderBy(p => p.DistSq)
                .ToList();

            int takeCount = Math.Min(sampleSize, prioritized.Count);
            return prioritized[RandomUtils.random.Next(takeCount)].Index;
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

        private static string ChoosePrefabId(
            RoomUtils roomUtils,
            string tileset,
            string district,
            HashSet<string> usedPrefabIds)
        {
            var listKey = roomUtils.listName + "_" + tileset;
            if (roomUtils.roomTemplates.TryGetValue(listKey, out var formList) &&
                formList?.Items != null &&
                formList.Items.Count > 0)
            {
                var allCandidates = new List<string>();

                foreach (var item in formList.Items)
                {
                    if (!gen_quest_main.myMod.PackIns.TryGetValue(item.FormKey, out var packIn) ||
                        string.IsNullOrEmpty(packIn?.EditorID))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(district) &&
                        !packIn.EditorID.Contains(district, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    allCandidates.Add(packIn.EditorID);
                }

                var unusedRooms = allCandidates
                    .Where(id => !usedPrefabIds.Contains(id) &&
                                 id.IndexOf("rg_blocker", StringComparison.OrdinalIgnoreCase) < 0)
                    .ToList();

                if (unusedRooms.Count > 0)
                    return unusedRooms[RandomUtils.random.Next(unusedRooms.Count)];

                var unusedAny = allCandidates
                    .Where(id => !usedPrefabIds.Contains(id))
                    .ToList();

                if (unusedAny.Count > 0)
                    return unusedAny[RandomUtils.random.Next(unusedAny.Count)];

                var rooms = allCandidates
                    .Where(id => id.IndexOf("rg_blocker", StringComparison.OrdinalIgnoreCase) < 0)
                    .ToList();

                if (rooms.Count > 0)
                    return rooms[RandomUtils.random.Next(rooms.Count)];

                if (allCandidates.Count > 0)
                    return allCandidates[RandomUtils.random.Next(allCandidates.Count)];
            }

            return roomUtils.GetRoom(tileset, district);
        }
    }
}
