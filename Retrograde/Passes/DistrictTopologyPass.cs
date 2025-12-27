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
        public DistrictTopologyPass(string districtType = null) {         
            district = districtType;
        }
        public void RunPass(DungeonState state)
        {
            // Inputs / knobs
            int maxRoomsToPlace = 10;          // hard limit (rooms)
            int maxAttempts = 500;              // hard limit (failed tries) to avoid infinite loops
            float collisionPadding = -1.5f; // tweak: world units clearance
            int maxCandidatePrefabsPerConnector = 8; // avoid thrashing on a single open connector
            int proximitySample = 5; // bias: pick from the closest N connectors to keep the cluster tight

            RoomUtils roomUtils = new RoomUtils();

            // Main placement loop: iterates over open connectors, but bounded
            int roomsPlaced = 0;
            int attempts = 0;

            while (roomsPlaced < maxRoomsToPlace && state.openConnectors.Count > 0 && attempts < maxAttempts)
            {
                attempts++;

                // Choose an open connector near the current cluster center to keep rooms close together
                var clusterCenter = CalculateClusterCenter(state);
                int openIndex = ChooseConnectorIndexNearCenter(state.openConnectors, clusterCenter, proximitySample);
                var target = state.openConnectors[openIndex];

                if (target.WorldPos.Y < state.YMin)
                {
                    //MAKE SURE YOU DON'T GO -Y
                    continue;
                }

                // Remove it now to ensure we "try to iterate through all open connectors"
                // (if we fail to place, we can choose to discard or re-add; discarding avoids loops)
                state.openConnectors.RemoveAt(openIndex);

                // We need a connector on nextPrefab that is OPPOSITE direction to target,
                // and compatible on door/tileset (simple equality checks here).
                var requiredDir = ConnectorUtils.Opposite(target.Parsed.Direction);

                bool placed = false;

                for (int prefabTry = 0; prefabTry < maxCandidatePrefabsPerConnector; prefabTry++)
                {
                    var nextPrefab = new RoomPrefab(roomUtils.GetRoom(target.Parsed.Tileset, district));

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
                        if (ConnectorUtils.CollidesWithAny(candidateAabb, state.placedRooms, collisionPadding))
                            continue;

                        // Place it with rotation
                        state.instance.Temporary.Add(new PlacedObject(gen_quest_main.myMod)
                        {
                            Count = 1,
                            Rotation = RgRotation.RotationToP3Float(yawSteps),
                            Position = nextPos,
                            Base = nextPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                        });

                        state.placedRooms.Add(new PlacedRoom
                        {
                            Prefab = nextPrefab,
                            WorldPos = nextPos,
                            YawSteps = yawSteps,
                            Connectors = nextConnectors
                        });

                        roomsPlaced++;
                        placed = true;

                        foreach (var c in nextConnectors)
                        {
                            if (c.EditorId == chosen.EditorId && c.LocalPos.Equals(chosen.LocalPos))
                                continue;

                            state.openConnectors.Add(new OpenConnector
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
                // (We already removed it from openConnectors to ensure forward progress.)
                if (!placed)
                {
                    state.openConnectors.Add(target);//Return it to the list so we close it later.
                    continue;
                }
            }
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

        private static P3Float CalculateClusterCenter(DungeonState state)
        {
            if (state.placedRooms.Count > 0)
            {
                float sumX = 0;
                float sumY = 0;
                float sumZ = 0;
                foreach (var room in state.placedRooms)
                {
                    sumX += room.WorldPos.X;
                    sumY += room.WorldPos.Y;
                    sumZ += room.WorldPos.Z;
                }

                float count = state.placedRooms.Count;
                return new P3Float(sumX / count, sumY / count, sumZ / count);
            }

            if (state.openConnectors.Count > 0)
            {
                float sumX = 0;
                float sumY = 0;
                float sumZ = 0;
                foreach (var connector in state.openConnectors)
                {
                    sumX += connector.WorldPos.X;
                    sumY += connector.WorldPos.Y;
                    sumZ += connector.WorldPos.Z;
                }

                float count = state.openConnectors.Count;
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
