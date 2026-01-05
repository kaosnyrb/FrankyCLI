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
        public BossTopologyPass(string districtType = null) {         
            district = districtType;
        }
        public void RunPass(DungeonState state)
        {
            // Inputs / knobs
            int maxRoomsToPlace = 1;          // boss: only place a single room
            int maxAttempts = 1000;              // hard limit (failed tries) to avoid infinite loops
            float collisionPadding = -1.5f; // tweak: world units clearance
            int maxCandidatePrefabsPerConnector = 32; // avoid thrashing on a single open connector

            RoomUtils roomUtils = new RoomUtils("rg_bosslist");
            RoomUtils spineUtils = new RoomUtils("rg_spinelist");

            // Main placement loop: iterates over open connectors, but bounded
            int roomsPlaced = 0;
            int attempts = 0;

            while (roomsPlaced < maxRoomsToPlace && state.openConnectors.Count > 0 && attempts < maxAttempts)
            {
                attempts++;

                // Choose the NORTH-facing open connector farthest from the starting position to anchor the boss room
                int openIndex = ChooseFarthestNorthFromStart(state.openConnectors, state.StartingPosition);
                if (openIndex < 0)
                {
                    if (TryPlaceSpineNorthConnector(state, spineUtils, collisionPadding, maxCandidatePrefabsPerConnector))
                    {
                        // We added a north-facing connector; try again.
                        continue;
                    }
                    // No suitable north-facing connector remains
                    break;
                }
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
            if (roomsPlaced == 0)
            {
                //We're fucked. Kill the run.
                throw new Exception("Couldn't place boss room");
            }
        }

        private static bool TryPlaceSpineNorthConnector(
            DungeonState state,
            RoomUtils spineUtils,
            float collisionPadding,
            int maxCandidatePrefabsPerConnector)
        {
            if (state.openConnectors.Count == 0)
                return false;

            // Prefer targets further north to grow toward the boss goal.
            var targets = state.openConnectors
                .OrderByDescending(c => c.WorldPos.Y)
                .ThenByDescending(c => DistanceSquared(c.WorldPos, state.StartingPosition))
                .ToList();

            foreach (var target in targets)
            {
                if (target.WorldPos.Y < state.YMin)
                    continue;

                var requiredDir = ConnectorUtils.Opposite(target.Parsed.Direction);
                int targetIndex = state.openConnectors.IndexOf(target);
                if (targetIndex < 0)
                    continue;

                for (int prefabTry = 0; prefabTry < maxCandidatePrefabsPerConnector; prefabTry++)
                {
                    var nextPrefab = new RoomPrefab(spineUtils.GetRoom(target.Parsed.Tileset, "spine"));

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
                            if (ConnectorUtils.CollidesWithAny(candidateAabb, state.placedRooms, collisionPadding))
                                continue;

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

                            state.openConnectors.RemoveAt(targetIndex);

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

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static int ChooseFarthestNorthFromStart(List<OpenConnector> openConnectors, P3Float startingPosition)
        {
            float maxDist = float.MinValue;
            int bestIndex = -1;
            for (int i = 0; i < openConnectors.Count; i++)
            {
                if (openConnectors[i].Parsed.Direction != ConnectorDirection.North)
                    continue;

                var dist = DistanceSquared(openConnectors[i].WorldPos, startingPosition);
                if (dist > maxDist || bestIndex == -1)
                {
                    maxDist = dist;
                    bestIndex = i;
                }
            }
            return bestIndex;
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
