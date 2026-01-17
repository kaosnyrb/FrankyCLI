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
        private int maxPlans = 50;
        private const int maxPrefabsToTryPerPair = 48;
        private const int targetBridgeCount = 10;

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

            maxPlans = state.scoringSystem?.Effort ?? maxPlans;

            int bestBridgesPlaced = -1;
            List<PlacedRoom> bestPlannedRooms = null;
            List<OpenConnector> bestPlannedOpenConnectors = null;
            List<PlacedObject> bestPlannedPlacements = null;
            double bestPlanScore = double.MinValue;
            PlanScore? bestPlanScoreBreakdown = null;
            int bestPlanAttempt = -1;

            for (int planAttempt = 0; planAttempt < maxPlans; planAttempt++)
            {
                var usedPrefabIds = CollectUsedPrefabIds(state.placedRooms);

                var plannedRooms = new List<PlacedRoom>(state.placedRooms);
                var plannedOpenConnectors = state.openConnectors
                    .Where(c => c.WorldPos.Y >= state.YMin)
                    .OrderBy(_ => RandomUtils.random.Next())
                    .ToList();
                var plannedPlacements = new List<PlacedObject>();

                int bridgesPlaced = PlanBridges(plannedRooms, plannedOpenConnectors, plannedPlacements, usedPrefabIds, collisionPadding, connectorEmbedTolerance, maxPrefabsToTryPerPair, targetBridgeCount);
                var planScore = ScoringUtil.ScorePlan(state.scoringSystem, bridgesPlaced, bridgesPlaced);

                if (planScore.Total > bestPlanScore)
                {
                    bestBridgesPlaced = bridgesPlaced;
                    bestPlannedRooms = plannedRooms;
                    bestPlannedOpenConnectors = plannedOpenConnectors;
                    bestPlannedPlacements = plannedPlacements;
                    bestPlanScore = planScore.Total;
                    bestPlanScoreBreakdown = planScore;
                    bestPlanAttempt = planAttempt;
                }
            }

            var finalRooms = bestPlannedRooms ?? state.placedRooms;
            var finalOpenConnectors = bestPlannedOpenConnectors ?? state.openConnectors;
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

            Console.WriteLine($"[Bridge plan] best of {maxPlans} attempts (attempt {bestPlanAttempt + 1}): placed {bestBridgesPlaced}/{targetBridgeCount} bridge prefabs, score {finalScore.Total:0.00} (placement {finalScore.Components["Placement"]:0.00}, bridging {finalScore.Components["Bridging"]:0.00}).");
        }

        private int PlanBridges(
            List<PlacedRoom> plannedRooms,
            List<OpenConnector> plannedOpenConnectors,
            List<PlacedObject> plannedPlacements,
            HashSet<string> usedPrefabIds,
            float collisionPadding,
            float connectorEmbedTolerance,
            int maxPrefabsToTryPerPair,
            int desiredBridgeCount)
        {
            int bridgesPlaced = 0;
            bool progress = true;

            while (progress && plannedOpenConnectors.Count >= 2 && bridgesPlaced < desiredBridgeCount)
            {
                progress = false;

                for (int i = 0; i < plannedOpenConnectors.Count - 1; i++)
                {
                    for (int j = i + 1; j < plannedOpenConnectors.Count; j++)
                    {
                        var a = plannedOpenConnectors[i];
                        var b = plannedOpenConnectors[j];

                        if (!BridgeUtil.ArePairCompatible(a, b))
                            continue;

                        if (TryPlaceBridgeBetween(a, b, plannedRooms, usedPrefabIds, collisionPadding, connectorEmbedTolerance, maxPrefabsToTryPerPair, out var placedRoom, out var placement, out var newConnectors))
                        {
                            plannedPlacements.Add(placement);
                            plannedRooms.Add(placedRoom);
                            usedPrefabIds.Add(placedRoom.Prefab.PrefabEditorId);

                            plannedOpenConnectors.RemoveAt(j);
                            plannedOpenConnectors.RemoveAt(i);
                            plannedOpenConnectors.AddRange(newConnectors);

                            bridgesPlaced++;
                            progress = true;
                            goto NextIteration;
                        }
                    }
                }

                break;

            NextIteration:
                continue;
            }

            return bridgesPlaced;
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

                    var matchesA = connectors.Where(c => BridgeUtil.MatchesOpenConnector(a, c)).ToList();
                    var matchesB = connectors.Where(c => BridgeUtil.MatchesOpenConnector(b, c)).ToList();

                    if (matchesA.Count == 0 || matchesB.Count == 0)
                        continue;

                    foreach (var connA in matchesA)
                    {
                        foreach (var connB in matchesB)
                        {
                            if (BridgeUtil.IsSameConnector(connA, connB))
                                continue;

                            var prefabPos = a.WorldPos - connA.LocalPos;
                            var expectedB = prefabPos + connB.LocalPos;

                            if (!BridgeUtil.PositionsClose(expectedB, b.WorldPos, ConnectorPositionTolerance))
                                continue;

                            var candidateAabb = ConnectorUtils.ToWorldAabbRotated(prefab.packin_instance.ObjectBounds, prefabPos, yawSteps);
                            if (ConnectorUtils.CollidesWithAny(candidateAabb, plannedRooms, collisionPadding))
                                continue;
                            if (BridgeUtil.AnyConnectorInsideExistingBounds(connectors, prefabPos, plannedRooms, connectorEmbedTolerance))
                                continue;
                            if (BridgeUtil.AnyExistingConnectorInsideCandidate(candidateAabb, plannedRooms, connectorEmbedTolerance))
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
                                if (BridgeUtil.IsSameConnector(c, connA) || BridgeUtil.IsSameConnector(c, connB))
                                    continue;

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

        private static bool IsBlocker(string editorId)
        {
            return editorId.IndexOf("rg_blocker", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<T> Shuffle<T>(IEnumerable<T> source)
        {
            return source.OrderBy(_ => RandomUtils.random.Next()).ToList();
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
