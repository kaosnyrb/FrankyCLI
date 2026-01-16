using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde;
using FrankyCLI.Retrograde.Passes;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    public class BridgingTopologyPass : IGenPass
    {
        private const float ConnectorPositionTolerance = 0.05f;
        private float collisionPadding = -0.5f; // prefer to fail rather than overlap
        private const float connectorEmbedTolerance = 0.05f;
        private const int maxPlans = 50;
        private const int maxPrefabsToTryPerPair = 48;

        private readonly List<string> bridgeRoomLists;
        private readonly List<RoomUtils> roomUtils;
        private readonly string districtFilter;
        private readonly string districtTypeLabel;
        public IReadOnlyList<string> BridgeRoomLists => bridgeRoomLists;

        public BridgingTopologyPass(string roomList, string districtType = null)
            : this(new[] { roomList }, districtType)
        {
        }

        public BridgingTopologyPass(IEnumerable<string> roomLists, string districtType = null)
        {
            if (roomLists == null)
                throw new ArgumentNullException(nameof(roomLists));

            bridgeRoomLists = roomLists
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (bridgeRoomLists.Count == 0)
                throw new ArgumentException("At least one bridge room list is required.", nameof(roomLists));

            districtFilter = districtType;
            districtTypeLabel = DeriveDistrictType(bridgeRoomLists[0], districtType, "bridge");
            roomUtils = bridgeRoomLists.Select(name => new RoomUtils(name)).ToList();
        }
        public void RunPass(DungeonState state)
        {


            if (state.openConnectors == null || state.openConnectors.Count < 2)
                return;

            for (int planAttempt = 0; planAttempt < maxPlans; planAttempt++)
            {
                var usedPrefabIds = CollectUsedPrefabIds(state.placedRooms);

                var plannedRooms = new List<PlacedRoom>(state.placedRooms);
                var plannedOpenConnectors = state.openConnectors
                    .Where(c => c.WorldPos.Y >= state.YMin)
                    .OrderBy(_ => RandomUtils.random.Next())
                    .ToList();
                var plannedPlacements = new List<PlacedObject>();

                bool placedAny = PlanBridges(plannedRooms, plannedOpenConnectors, plannedPlacements, usedPrefabIds, collisionPadding, connectorEmbedTolerance, maxPrefabsToTryPerPair);

                if (placedAny || planAttempt == maxPlans - 1)
                {
                    foreach (var placement in plannedPlacements)
                    {
                        state.instance.Temporary.Add(placement);
                    }
                    state.placedRooms = plannedRooms;
                    state.openConnectors = plannedOpenConnectors;

                    Console.WriteLine($"[Bridge plan] {planAttempt + 1}/{maxPlans} {(placedAny ? "success" : "no fits")} - placed {plannedPlacements.Count} bridge prefabs.");
                    return;
                }
            }

        }

        private bool PlanBridges(
            List<PlacedRoom> plannedRooms,
            List<OpenConnector> plannedOpenConnectors,
            List<PlacedObject> plannedPlacements,
            HashSet<string> usedPrefabIds,
            float collisionPadding,
            float connectorEmbedTolerance,
            int maxPrefabsToTryPerPair)
        {
            bool placedAny = false;
            bool progress = true;

            while (progress && plannedOpenConnectors.Count >= 2)
            {
                progress = false;

                for (int i = 0; i < plannedOpenConnectors.Count - 1; i++)
                {
                    for (int j = i + 1; j < plannedOpenConnectors.Count; j++)
                    {
                        var a = plannedOpenConnectors[i];
                        var b = plannedOpenConnectors[j];

                        if (!ArePairCompatible(a, b))
                            continue;

                        if (TryPlaceBridgeBetween(a, b, plannedRooms, usedPrefabIds, collisionPadding, connectorEmbedTolerance, maxPrefabsToTryPerPair, out var placedRoom, out var placement, out var newConnectors))
                        {
                            plannedPlacements.Add(placement);
                            plannedRooms.Add(placedRoom);
                            usedPrefabIds.Add(placedRoom.Prefab.PrefabEditorId);

                            plannedOpenConnectors.RemoveAt(j);
                            plannedOpenConnectors.RemoveAt(i);
                            plannedOpenConnectors.AddRange(newConnectors);

                            placedAny = true;
                            progress = true;
                            goto NextIteration;
                        }
                    }
                }

                break;

            NextIteration:
                continue;
            }

            return placedAny;
        }

        private bool TryPlaceBridgeBetween(
            OpenConnector a,
            OpenConnector b,
            List<PlacedRoom> plannedRooms,
            HashSet<string> usedPrefabIds,
            float collisionPadding,
            float connectorEmbedTolerance,
            int maxPrefabsToTryPerPair,
            out PlacedRoom placedRoom,
            out PlacedObject placedObject,
            out List<OpenConnector> resultingOpenConnectors)
        {
            placedRoom = default;
            placedObject = null;
            resultingOpenConnectors = null;

            var candidates = BuildPrefabCandidates(a.Parsed.Tileset, usedPrefabIds);
            if (candidates.Count == 0)
                return false;

            var prefabsToTry = candidates
                .OrderBy(_ => RandomUtils.random.Next())
                .Take(Math.Max(1, Math.Min(maxPrefabsToTryPerPair, candidates.Count)))
                .ToList();

            foreach (var prefabId in prefabsToTry)
            {
                var prefab = new RoomPrefab(prefabId);

                for (int yawSteps = 0; yawSteps < 4; yawSteps++)
                {
                    var connectors = ConnectorUtils.GetConnectors(prefab, yawSteps);

                    var matchesA = connectors.Where(c => MatchesOpenConnector(a, c)).ToList();
                    var matchesB = connectors.Where(c => MatchesOpenConnector(b, c)).ToList();

                    if (matchesA.Count == 0 || matchesB.Count == 0)
                        continue;

                    foreach (var connA in matchesA)
                    {
                        foreach (var connB in matchesB)
                        {
                            if (IsSameConnector(connA, connB))
                                continue;

                            var prefabPos = a.WorldPos - connA.LocalPos;
                            var expectedB = prefabPos + connB.LocalPos;

                            if (!PositionsClose(expectedB, b.WorldPos, ConnectorPositionTolerance))
                                continue;

                            var candidateAabb = ConnectorUtils.ToWorldAabbRotated(prefab.packin_instance.ObjectBounds, prefabPos, yawSteps);
                            if (ConnectorUtils.CollidesWithAny(candidateAabb, plannedRooms, collisionPadding))
                                continue;
                            if (AnyConnectorInsideExistingBounds(connectors, prefabPos, plannedRooms, connectorEmbedTolerance))
                                continue;
                            if (AnyExistingConnectorInsideCandidate(candidateAabb, plannedRooms, connectorEmbedTolerance))
                                continue;

                            placedObject = new PlacedObject(gen_quest_main.myMod)
                            {
                                Count = 1,
                                Rotation = RgRotation.RotationToP3Float(yawSteps),
                                Position = prefabPos,
                                Base = prefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                            };

                            placedRoom = new PlacedRoom
                            {
                                Prefab = prefab,
                                WorldPos = prefabPos,
                                YawSteps = yawSteps,
                                DistrictType = districtTypeLabel,
                                Connectors = connectors
                            };

                            resultingOpenConnectors = new List<OpenConnector>();
                            foreach (var c in connectors)
                            {
                                resultingOpenConnectors.Add(new OpenConnector
                                {
                                    Parsed = c.Parsed,
                                    YawSteps = yawSteps,
                                    WorldPos = prefabPos + c.LocalPos,
                                    DistrictType = districtTypeLabel
                                });
                            }

                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private List<string> BuildPrefabCandidates(string tileset, HashSet<string> usedPrefabIds)
        {
            var allCandidates = new List<string>();

            foreach (var utils in roomUtils)
            {
                var listKey = utils.listName + "_" + tileset;
                if (!utils.roomTemplates.TryGetValue(listKey, out var formList) || formList?.Items == null || formList.Items.Count == 0)
                    continue;

                foreach (var item in formList.Items)
                {
                    if (!gen_quest_main.myMod.PackIns.TryGetValue(item.FormKey, out var packIn) ||
                        string.IsNullOrEmpty(packIn?.EditorID))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(districtFilter) &&
                        !packIn.EditorID.Contains(districtFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    allCandidates.Add(packIn.EditorID);
                }
            }

            var distinct = allCandidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var unusedRooms = distinct
                .Where(id => !usedPrefabIds.Contains(id) && !IsBlocker(id))
                .ToList();
            var unusedAny = distinct
                .Where(id => !usedPrefabIds.Contains(id))
                .ToList();
            var rooms = distinct
                .Where(id => !IsBlocker(id))
                .ToList();

            return Shuffle(unusedRooms)
                .Concat(Shuffle(unusedAny))
                .Concat(Shuffle(rooms))
                .Concat(Shuffle(distinct))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static HashSet<string> CollectUsedPrefabIds(List<PlacedRoom> placedRooms)
        {
            var usedPrefabIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var room in placedRooms)
            {
                if (!string.IsNullOrEmpty(room.Prefab?.PrefabEditorId))
                {
                    usedPrefabIds.Add(room.Prefab.PrefabEditorId);
                }
            }

            return usedPrefabIds;
        }

        private static bool ArePairCompatible(OpenConnector a, OpenConnector b)
        {
            if (!a.Parsed.IsValid || !b.Parsed.IsValid)
                return false;

            return string.Equals(a.Parsed.Tileset, b.Parsed.Tileset, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.Parsed.DoorSize, b.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesOpenConnector(OpenConnector open, RgConnectorInstance candidate)
        {
            return candidate.Parsed.Direction == ConnectorUtils.Opposite(open.Parsed.Direction) &&
                   string.Equals(candidate.Parsed.DoorSize, open.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(candidate.Parsed.Tileset, open.Parsed.Tileset, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameConnector(RgConnectorInstance a, RgConnectorInstance b)
        {
            return string.Equals(a.EditorId, b.EditorId, StringComparison.OrdinalIgnoreCase) &&
                   a.LocalPos.Equals(b.LocalPos);
        }

        private static bool PositionsClose(P3Float a, P3Float b, float tolerance)
        {
            return Math.Abs(a.X - b.X) <= tolerance &&
                   Math.Abs(a.Y - b.Y) <= tolerance &&
                   Math.Abs(a.Z - b.Z) <= tolerance;
        }

        private static bool IsBlocker(string editorId)
        {
            return editorId.IndexOf("rg_blocker", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<T> Shuffle<T>(IEnumerable<T> source)
        {
            return source.OrderBy(_ => RandomUtils.random.Next()).ToList();
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
