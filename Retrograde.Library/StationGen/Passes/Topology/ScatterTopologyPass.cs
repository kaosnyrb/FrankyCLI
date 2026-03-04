using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Passes.SpaceStation
{
    public class ScatterTopologyPass : IGenPass
    {
        string district = null;
        public string roomlist = "";
        private readonly string districtTypeLabel;
        int maxroomcount = 0;

        private class ScatterPlanMeta
        {
            public int RoomsPlaced;
        }

        public ScatterTopologyPass(string p_roomlist, int maxcount, string districtType = null) {
            district = districtType;
            roomlist = p_roomlist;
            maxroomcount = maxcount;
            districtTypeLabel = DeriveDistrictType(p_roomlist, districtType, "district");
        }
        public void RunPass(DungeonState state)
        {
            // Inputs / knobs
            int maxRoomsToPlace = 10;          // hard limit (rooms)
            int maxAttempts = 1000;              // hard limit (failed tries) to avoid infinite loops
            float collisionPadding = -0.1f; // tweak: world units clearance
            int maxCandidatePrefabsPerConnector = 16; // avoid thrashing on a single open connector
            const int maxPlans = 50; // retry count for full planning attempts
            const float connectorEmbedTolerance = 0.01f; // prevent connectors from sitting inside other room bounds
            RoomUtils roomUtils = state.GetRoomUtils(roomlist);

            maxRoomsToPlace = 1 + RandomProvider.Random.Next(maxroomcount);

            var bestOutcome = PlanRunner.RunBest<ScatterPlanMeta>(maxPlans, planAttempt =>
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

                    // Pick a truly random connector to scatter placement rather than clustering
                    int openIndex = RandomProvider.Random.Next(plannedOpenConnectors.Count);
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
                        var nextPrefab = PrefabCache.GetPrefab(prefabId);

                        var yawOrder = Enumerable.Range(0, 4)
                            .OrderBy(_ => RandomProvider.Random.Next())
                            .ToList();

                        foreach (var yawSteps in yawOrder)
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

                            var chosen = compatible[RandomProvider.Random.Next(compatible.Count)];

                            // Align using ROTATED local connector
                            P3Float nextPos = target.WorldPos - chosen.LocalPos;

                            // Collision using ROTATED bounds
                            var candidateAabb = ConnectorUtils.ToWorldAabbRotated(nextPrefab.packin_instance.ObjectBounds, nextPos, yawSteps);
                            if (ConnectorUtils.IsBelowYMin(candidateAabb, state.YMin))
                                continue;
                            if (ConnectorUtils.CollidesWithAny(candidateAabb, plannedRooms, collisionPadding))
                                continue;
                            if (AnyConnectorInsideExistingBounds(nextConnectors, nextPos, plannedRooms, connectorEmbedTolerance))
                                continue;
                            if (AnyExistingConnectorInsideCandidate(candidateAabb, plannedRooms, connectorEmbedTolerance))
                                continue;

                            // Place it with rotation (planned)
                            plannedPlacements.Add(new PlacedObject(RetrogradeContext.Current.TargetMod)
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
                if (!success)
                {
                    if (!state.IsHarnessRun)
                    {
                        Console.WriteLine("[Scatter plan] {0}/{1} aborted: placed {2}/{3} rooms.", planAttempt + 1, maxPlans, roomsPlaced, maxRoomsToPlace);
                    }
                    return new PlanOutcome<ScatterPlanMeta>
                    {
                        Score = double.MinValue
                    };
                }

                return new PlanOutcome<ScatterPlanMeta>
                {
                    Score = roomsPlaced,
                    Rooms = plannedRooms,
                    OpenConnectors = plannedOpenConnectors,
                    Placements = plannedPlacements,
                    Metadata = new ScatterPlanMeta
                    {
                        RoomsPlaced = roomsPlaced
                    }
                };
            });

            if (bestOutcome?.Placements == null)
                throw new Exception("ScatterTopologyPass failed after " + maxPlans+ " plan attempts.");

            foreach (var placement in bestOutcome.Placements)
            {
                state.PlacementUtil.AddToTemporary(state.instance, placement);
            }
            state.placedRooms = bestOutcome.Rooms;
            state.openConnectors = bestOutcome.OpenConnectors;

            int bestPlanAttempt = (bestOutcome.AttemptIndex) + 1;
            int bestRoomsPlaced = bestOutcome.Metadata?.RoomsPlaced ?? 0;

            if (!state.IsHarnessRun)
            {
                Console.WriteLine("[Scatter plan] {0}/{1} success: placed {2}/{3} rooms.", bestPlanAttempt, maxPlans, bestRoomsPlaced, maxRoomsToPlace);
            }
        }

        private static bool AnyConnectorInsideExistingBounds(
            List<RgConnectorInstance> connectors,
            P3Float roomWorldPos,
            List<PlacedRoom> placedRooms,
            float tolerance)
        {
            if (placedRooms == null || placedRooms.Count == 0)
                return false;

            foreach (var placed in placedRooms)
            {
                if (placed.Prefab?.packin_instance == null)
                    continue;

                var placedAabb = ConnectorUtils.ToWorldAabbRotated(placed.Prefab.packin_instance.ObjectBounds, placed.WorldPos, placed.YawSteps);

                foreach (var conn in connectors)
                {
                    var worldPos = roomWorldPos + conn.LocalPos;
                    if (IsPointStrictlyInside(worldPos, placedAabb, tolerance))
                        return true;
                }
            }

            return false;
        }

        private static bool AnyExistingConnectorInsideCandidate(
            RgAabb candidateAabb,
            List<PlacedRoom> placedRooms,
            float tolerance)
        {
            if (placedRooms == null || placedRooms.Count == 0)
                return false;

            foreach (var placed in placedRooms)
            {
                if (placed.Connectors == null)
                    continue;

                foreach (var conn in placed.Connectors)
                {
                    var worldPos = placed.WorldPos + conn.LocalPos;
                    if (IsPointStrictlyInside(worldPos, candidateAabb, tolerance))
                        return true;
                }
            }

            return false;
        }

        private static bool IsPointStrictlyInside(P3Float point, RgAabb aabb, float tolerance)
        {
            return point.X > aabb.Min.X + tolerance &&
                   point.X < aabb.Max.X - tolerance &&
                   point.Y > aabb.Min.Y + tolerance &&
                   point.Y < aabb.Max.Y - tolerance &&
                   point.Z > aabb.Min.Z + tolerance &&
                   point.Z < aabb.Max.Z - tolerance;
        }

        private static string ChoosePrefabId(
            RoomUtils roomUtils,
            string tileset,
            string district,
            HashSet<string> usedPrefabIds)
        {
            // allCandidates is pre-built and cached by RoomUtils — no FindPackIn calls here.
            var allCandidates = roomUtils.GetAllCandidatesForDistrict(tileset, district);
            if (allCandidates.Count == 0)
                return roomUtils.GetRoom(tileset, district);

            // Single pass: sort into four buckets matching the original fallback priority.
            List<string> unusedNonBlockers = null; // preferred
            List<string> unusedBlockers    = null; // fallback 1: any unused
            List<string> usedNonBlockers   = null; // fallback 2: non-blockers (reuse allowed)
            // usedBlockers not tracked separately — allCandidates covers fallback 3

            foreach (var id in allCandidates)
            {
                bool isBlocker = id.IndexOf("rg_blocker", StringComparison.OrdinalIgnoreCase) >= 0;
                if (usedPrefabIds.Contains(id))
                {
                    if (!isBlocker) (usedNonBlockers ??= new List<string>()).Add(id);
                }
                else
                {
                    if (!isBlocker) (unusedNonBlockers ??= new List<string>()).Add(id);
                    else            (unusedBlockers    ??= new List<string>()).Add(id);
                }
            }

            if (unusedNonBlockers?.Count > 0)
                return unusedNonBlockers[RandomProvider.Random.Next(unusedNonBlockers.Count)];

            if (unusedBlockers?.Count > 0)
                return unusedBlockers[RandomProvider.Random.Next(unusedBlockers.Count)];

            if (usedNonBlockers?.Count > 0)
                return usedNonBlockers[RandomProvider.Random.Next(usedNonBlockers.Count)];

            return allCandidates[RandomProvider.Random.Next(allCandidates.Count)];
        }

        private static string DeriveDistrictType(string roomList, string provided, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(provided))
                return provided;

            if (string.IsNullOrWhiteSpace(roomList))
                return fallback;

            var normalized = roomList;
            if (normalized.StartsWith("rg_", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(3);
            if (normalized.EndsWith("list", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - 4);

            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }
    }
}
