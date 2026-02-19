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
    public class UtilTopologyPass : IGenPass
    {
        string district = null;
        public string roomlist = "";
        private readonly string districtTypeLabel;

        private float coverage;
        private static readonly List<string> PrefabsToForcePlacement = new List<string>
        {
            // Add prefab EditorIDs here to force a placement attempt for testing new prefabs.
            //"rg_sts_end_workshop_001"
        };

        public UtilTopologyPass(string p_roomlist, float targetcoverage, string districtType = null) {
            district = districtType;
            roomlist = p_roomlist;
            districtTypeLabel = DeriveDistrictType(p_roomlist, districtType, "util");
            coverage = targetcoverage;
        }
        public void RunPass(DungeonState state)
        {
            // Inputs / knobs
            int startingOpenConnectors = state.openConnectors.Count;
            int maxRoomsToPlace = startingOpenConnectors == 0
                ? 0
                : Math.Max(1, (int)Math.Round(startingOpenConnectors * coverage));
            int maxAttempts = 5000;              // hard limit (failed tries) to avoid infinite loops
            int maxCandidatePrefabsPerConnector = 32; // avoid thrashing on a single open connector
            float collisionPadding = -0.1f; // tweak: match DistrictTopologyPass collision clearance

            float samePrefabMinDistance = 30f; // keep identical util prefabs separated
            float similarPrefabMinDistance = 30f; // spread similar prefab archetypes
            float similarPrefabSameParentMinDistance = 25f; // extra spread when siblings off same parent

            RoomUtils roomUtils = state.GetRoomUtils(roomlist);
            var usedPrefabIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var room in state.placedRooms)
            {
                if (!string.IsNullOrEmpty(room.Prefab?.PrefabEditorId))
                {
                    usedPrefabIds.Add(room.Prefab.PrefabEditorId);
                }
            }
            var requiredPrefabs = PrefabsToForcePlacement
                .Where(id => !string.IsNullOrWhiteSpace(id) && !usedPrefabIds.Contains(id))
                .ToList();

            if (maxRoomsToPlace == 0)
                return;

            // Main placement loop: iterates over open connectors, but bounded
            int roomsPlaced = 0;
            int attempts = 0;

            while (roomsPlaced < maxRoomsToPlace && state.openConnectors.Count > 0 && attempts < maxAttempts)
            {
                attempts++;

                // Choose an open connector at random; no clustering bias so we spread small fillers around
                int openIndex = RandomProvider.Random.Next(state.openConnectors.Count);
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

                bool hasParentRoom = TryFindParentRoom(target, state.placedRooms, out var parentRoom);

                bool placed = false;
                bool attemptedRequiredForThisConnector = false;

                for (int prefabTry = 0; prefabTry < maxCandidatePrefabsPerConnector; prefabTry++)
                {
                    bool useRequired = requiredPrefabs.Count > 0 && !attemptedRequiredForThisConnector;
                    if (useRequired)
                        attemptedRequiredForThisConnector = true;

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
                    else
                    {
                        prefabId = ChoosePrefabId(roomUtils, target.Parsed.Tileset, district, usedPrefabIds);
                        if (string.IsNullOrEmpty(prefabId))
                        {
                            continue;
                        }
                    }
                    var nextPrefab = PrefabCache.GetPrefab(prefabId);
                    var similarityKey = GetSimilarityKey(nextPrefab.PrefabEditorId);

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
                        if (ConnectorUtils.CollidesWithAny(candidateAabb, state.placedRooms, collisionPadding))
                            continue;
                        if (IsTooCloseToSamePrefab(nextPrefab.PrefabEditorId, nextPos, state.placedRooms, samePrefabMinDistance))
                            continue;
                        var similarMinDistance = hasParentRoom ? similarPrefabSameParentMinDistance : similarPrefabMinDistance;
                        if (IsTooCloseToSimilarPrefab(similarityKey, nextPos, state.placedRooms, similarMinDistance))
                            continue;

                        // Place it with rotation
                        state.PlacementUtil.AddToTemporary(state.instance, new PlacedObject(RetrogradeContext.Current.TargetMod)
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
                            DistrictType = districtTypeLabel,
                            Connectors = nextConnectors
                        });
                        usedPrefabIds.Add(nextPrefab.PrefabEditorId);
                        if (useRequired && requiredPrefabs.Count > 0 &&
                            string.Equals(prefabId, requiredPrefabs[0], StringComparison.OrdinalIgnoreCase))
                        {
                            requiredPrefabs.RemoveAt(0);
                        }

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
                // (We already removed it from openConnectors to ensure forward progress.)
                if (!placed)
                {
                    state.openConnectors.Add(target);//Return it to the list so we close it later.
                    continue;
                }
            }

            if (!state.IsHarnessRun)
                Console.WriteLine($"[Util] Placed {roomsPlaced}/{maxRoomsToPlace} rooms, {state.openConnectors.Count} open connectors remaining");
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
                var unusedCandidates = new List<string>();

                foreach (var item in formList.Items)
                {
                    var packIn = RoomUtils.FindPackIn(item.FormKey);
                    if (packIn == null || string.IsNullOrEmpty(packIn.EditorID))
                        continue;

                    if (!string.IsNullOrEmpty(district) &&
                        !packIn.EditorID.Contains(district, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (usedPrefabIds.Contains(packIn.EditorID))
                        continue;

                    unusedCandidates.Add(packIn.EditorID);
                }

                var unusedRooms = unusedCandidates
                    .Where(id => id.IndexOf("rg_blocker", StringComparison.OrdinalIgnoreCase) < 0)
                    .ToList();

                if (unusedRooms.Count > 0)
                    return unusedRooms[RandomProvider.Random.Next(unusedRooms.Count)];

                if (unusedCandidates.Count > 0)
                    return unusedCandidates[RandomProvider.Random.Next(unusedCandidates.Count)];
            }

            return null;
        }

        private static bool IsTooCloseToSamePrefab(
            string prefabId,
            P3Float candidatePos,
            List<PlacedRoom> placedRooms,
            float minDistance)
        {
            if (string.IsNullOrEmpty(prefabId))
                return false;

            float minDistSq = minDistance * minDistance;
            for (int i = 0; i < placedRooms.Count; i++)
            {
                var placed = placedRooms[i];
                if (placed.Prefab?.PrefabEditorId == null)
                    continue;

                if (!prefabId.Equals(placed.Prefab.PrefabEditorId, StringComparison.OrdinalIgnoreCase))
                    continue;

                float dx = placed.WorldPos.X - candidatePos.X;
                float dy = placed.WorldPos.Y - candidatePos.Y;
                float dz = placed.WorldPos.Z - candidatePos.Z;
                float distSq = dx * dx + dy * dy + dz * dz;

                if (distSq < minDistSq)
                    return true;
            }

            return false;
        }

        private static bool IsTooCloseToSimilarPrefab(
            string similarityKey,
            P3Float candidatePos,
            List<PlacedRoom> placedRooms,
            float minDistance)
        {
            if (string.IsNullOrEmpty(similarityKey))
                return false;

            float minDistSq = minDistance * minDistance;
            for (int i = 0; i < placedRooms.Count; i++)
            {
                var placed = placedRooms[i];
                var placedKey = GetSimilarityKey(placed.Prefab?.PrefabEditorId);
                if (string.IsNullOrEmpty(placedKey))
                    continue;

                if (!similarityKey.Equals(placedKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                float dx = placed.WorldPos.X - candidatePos.X;
                float dy = placed.WorldPos.Y - candidatePos.Y;
                float dz = placed.WorldPos.Z - candidatePos.Z;
                float distSq = dx * dx + dy * dy + dz * dz;

                if (distSq < minDistSq)
                    return true;
            }

            return false;
        }

        private static string GetSimilarityKey(string prefabId)
        {
            if (string.IsNullOrWhiteSpace(prefabId))
                return null;

            var tokens = prefabId.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 3)
            {
                // Use the first four tokens (e.g., rg_sts_end_office) as the archetype key.
                return string.Join("_", tokens.Take(4)).ToLowerInvariant();
            }

            return StripTrailingDigits(prefabId).ToLowerInvariant();
        }

        private static string StripTrailingDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            int i = value.Length - 1;
            while (i >= 0 && char.IsDigit(value[i]))
            {
                i--;
            }
            return value.Substring(0, i + 1);
        }

        private static bool TryFindParentRoom(OpenConnector target, List<PlacedRoom> placedRooms, out PlacedRoom parentRoom)
        {
            const float positionTolerance = 0.01f;
            foreach (var room in placedRooms)
            {
                if (room.Connectors == null)
                    continue;

                foreach (var conn in room.Connectors)
                {
                    var worldPos = room.WorldPos + conn.LocalPos;
                    if (!ArePositionsClose(worldPos, target.WorldPos, positionTolerance))
                        continue;

                    if (conn.Parsed.Direction != target.Parsed.Direction)
                        continue;
                    if (!string.Equals(conn.Parsed.Tileset, target.Parsed.Tileset, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.Equals(conn.Parsed.DoorSize, target.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase))
                        continue;

                    parentRoom = room;
                    return true;
                }
            }

            parentRoom = default;
            return false;
        }

        private static bool ArePositionsClose(P3Float a, P3Float b, float tolerance)
        {
            return Math.Abs(a.X - b.X) <= tolerance &&
                   Math.Abs(a.Y - b.Y) <= tolerance &&
                   Math.Abs(a.Z - b.Z) <= tolerance;
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
