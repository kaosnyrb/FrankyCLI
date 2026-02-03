using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FrankyCLI.Retrograde;
using FrankyCLI.questgen_tools;
using Noggog;

namespace FrankyCLI.Retrograde.Passes
{
    /// <summary>
    /// Seals all open (unconnected) connectors in the dungeon by placing blocker prefabs.
    /// Runs in two sweeps: first seals explicit open connectors, then sweeps placed rooms
    /// for any stray unmatched connector markers.
    /// </summary>
    public class ConnectorSealingPass : IGenPass
    {
        // ── Position matching ───────────────────────────────────────────
        private const float PositionTolerance = 0.01f;
        private const float StartPosTolerance = 0.01f;

        public string GetDoorBlocker(string doorSize, string tileset, string districtType)
        {
            // Prefer: pull blockers from a FormList named after the tileset (e.g. rg_blocker_station_hab).
            var primaryListId = string.IsNullOrWhiteSpace(districtType)
                ? $"rg_blocker_{tileset}"
                : $"rg_blocker_{tileset}_{districtType}";

            var list = FindBlockerFormList(primaryListId)
                       ?? (string.IsNullOrWhiteSpace(districtType) ? null : FindBlockerFormList($"rg_blocker_{tileset}"));
            var fromList = PickBlockerFromFormList(list, doorSize);
            if (!string.IsNullOrEmpty(fromList))
                return fromList;

            // Fallback: legacy ID-based search to avoid breaking if the FormList is missing.
            var blockerId = doorSize switch
            {
                "D1" => $"rg_blocker_D1_{tileset}",
                "D2" => $"rg_blocker_D2_{tileset}",
                _ => $"rg_blocker_{tileset}"
            };

            // Gather all prefabs whose editor IDs contain the blocker ID, pick one at random.
            var candidates = new List<string>();

            foreach (var packin in gen_quest_main.myMod.PackIns)
            {
                if (packin?.EditorID == null)
                    continue;

                if (packin.EditorID.IndexOf(blockerId, StringComparison.OrdinalIgnoreCase) >= 0)
                    candidates.Add(packin.EditorID);
            }

            if (candidates.Count > 0)
                return candidates[RandomUtils.random.Next(candidates.Count)];

            return blockerId;
        }

        public void RunPass(DungeonState state)
        {
            var sealedPositions = state.SealedConnectorPositionKeys ??= new HashSet<string>();
            sealedPositions.Clear();

            int stage1Sealed = 0;

            // Stage 1: Seal explicit open connectors (below-YMin first to guarantee closure)
            var ordered = state.openConnectors
                .OrderBy(c => c.WorldPos.Y < state.YMin ? 0 : 1)
                .ToList();

            foreach (var open in ordered)
            {
                if (ShouldSkipStartConnector(open, state))
                    continue;

                if (IsWindowPlacedAt(open.WorldPos, state))
                    continue;

                if (TrySealConnector(open, state, sealedPositions))
                {
                    stage1Sealed++;
                    continue;
                }
            }

            // Stage 2: Sweep placed rooms for stray unmatched connector markers
            int stage2Sealed = SealUnconnectedPlacedMarkers(state, sealedPositions);

            Console.WriteLine($"[ConnectorSealing] Sealed {stage1Sealed} open connectors, {stage2Sealed} stray markers");
        }

        /// <summary>
        /// Attempts to seal a single open connector, relaxing tileset matching for below-YMin connectors.
        /// Returns true if a blocker was placed.
        /// </summary>
        private bool TrySealConnector(OpenConnector open, DungeonState state, HashSet<string> sealedPositions)
        {
            bool placed = TryPlaceBlocker(open, state, requireTilesetMatch: true);
            if (!placed && open.WorldPos.Y < state.YMin)
                placed = TryPlaceBlocker(open, state, requireTilesetMatch: false);

            if (placed)
                sealedPositions.Add(PositionKey(open.WorldPos));

            return placed;
        }

        private bool TryPlaceBlocker(OpenConnector open, DungeonState state, bool requireTilesetMatch)
        {
            // Pick blocker prefab based on door size / tileset
            var blockerId = GetDoorBlocker(open.Parsed.DoorSize, open.Parsed.Tileset, open.DistrictType);
            var blockerPrefab = PrefabCache.GetPrefab(blockerId);

            // Blocker should have a connector that will attach to the OPEN connector.
            // If the open connector faces North, the blocker needs a South-facing connector to mate.
            var requiredDir = ConnectorUtils.Opposite(open.Parsed.Direction);

            // Try yaw steps 0..3 to orient blocker correctly (same approach as rooms)
            for (int yawSteps = 0; yawSteps < 4; yawSteps++)
            {
                var blockerConns = ConnectorUtils.GetConnectors(blockerPrefab, yawSteps);

                // Find a connector on the blocker that matches required direction and same door size/tileset.
                var attach = blockerConns.FirstOrDefault(c =>
                    c.Parsed.Direction == requiredDir &&
                    string.Equals(c.Parsed.DoorSize, open.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                    (requireTilesetMatch
                        ? string.Equals(c.Parsed.Tileset, open.Parsed.Tileset, StringComparison.OrdinalIgnoreCase)
                        : true));

                if (!attach.Parsed.IsValid)
                    continue;

                // Align blocker so its attach connector lands exactly at the open connector position
                var blockerPos = open.WorldPos - attach.LocalPos;

                state.PlacementUtil.AddToTemporary(state.instance, new PlacedObject(gen_quest_main.myMod)
                {
                    Count = 1,
                    Rotation = RgRotation.RotationToP3Float(yawSteps),
                    Position = blockerPos,
                    Base = blockerPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                });

                return true;
            }

            return false;
        }

        /// <summary>
        /// Sweeps every placed room for connector markers that didn't pair up with another room,
        /// and seals them with blockers.
        /// </summary>
        /// <returns>Number of connectors sealed in this stage.</returns>
        private int SealUnconnectedPlacedMarkers(DungeonState state, HashSet<string> sealedPositions)
        {
            if (state.placedRooms == null || state.placedRooms.Count == 0)
                return 0;

            // Stage 2a: Collect all connector markers from placed rooms
            var connectors = CollectPlacedConnectors(state);
            if (connectors.Count == 0)
                return 0;

            // Stage 2b: Mark connectors that are properly mated (same position, opposing direction, matching size/tileset)
            var matched = FindMatchedConnectors(connectors);

            int sealedCount = 0;

            // Stage 2c: Seal any unmatched connectors
            for (int i = 0; i < connectors.Count; i++)
            {
                if (matched[i])
                    continue;

                var open = connectors[i].Conn;
                if (ShouldSkipStartConnector(open, state))
                    continue;

                if (IsWindowPlacedAt(open.WorldPos, state))
                    continue;

                var key = PositionKey(open.WorldPos);
                if (sealedPositions.Contains(key))
                    continue;

                if (TrySealConnector(open, state, sealedPositions))
                    sealedCount++;
            }

            return sealedCount;
        }

        /// <summary>
        /// Gathers all connector markers from placed rooms as world-space OpenConnectors.
        /// </summary>
        private static List<(OpenConnector Conn, int RoomIndex)> CollectPlacedConnectors(DungeonState state)
        {
            var connectors = new List<(OpenConnector Conn, int RoomIndex)>();
            for (int roomIndex = 0; roomIndex < state.placedRooms.Count; roomIndex++)
            {
                var room = state.placedRooms[roomIndex];
                if (room.Connectors == null)
                    continue;

                foreach (var connector in room.Connectors)
                {
                    connectors.Add((
                        new OpenConnector
                        {
                            Parsed = connector.Parsed,
                            YawSteps = room.YawSteps,
                            WorldPos = room.WorldPos + connector.LocalPos,
                            DistrictType = room.DistrictType
                        },
                        roomIndex));
                }
            }

            return connectors;
        }

        /// <summary>
        /// Pairs up connectors that face each other at the same position with matching size and tileset.
        /// Returns an array where true means the connector at that index is properly mated.
        /// </summary>
        private static bool[] FindMatchedConnectors(List<(OpenConnector Conn, int RoomIndex)> connectors)
        {
            var matched = new bool[connectors.Count];
            for (int i = 0; i < connectors.Count; i++)
            {
                if (matched[i])
                    continue;

                for (int j = i + 1; j < connectors.Count; j++)
                {
                    if (matched[j])
                        continue;

                    if (!AreConnectorsMated(connectors[i], connectors[j]))
                        continue;

                    matched[i] = matched[j] = true;
                    break;
                }
            }

            return matched;
        }

        /// <summary>
        /// Returns true if two connectors from different rooms are at the same position,
        /// face opposite directions, and share the same door size and tileset.
        /// </summary>
        private static bool AreConnectorsMated(
            (OpenConnector Conn, int RoomIndex) a,
            (OpenConnector Conn, int RoomIndex) b)
        {
            if (a.RoomIndex == b.RoomIndex)
                return false;

            if (!SamePosition(a.Conn.WorldPos, b.Conn.WorldPos))
                return false;

            if (ConnectorUtils.Opposite(a.Conn.Parsed.Direction) != b.Conn.Parsed.Direction)
                return false;

            if (!string.Equals(a.Conn.Parsed.DoorSize, b.Conn.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.Equals(a.Conn.Parsed.Tileset, b.Conn.Parsed.Tileset, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private static bool SamePosition(P3Float a, P3Float b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz <= PositionTolerance * PositionTolerance;
        }

        private static string PositionKey(P3Float pos)
        {
            float scale = 1f / PositionTolerance;
            return $"{MathF.Round(pos.X * scale)}|{MathF.Round(pos.Y * scale)}|{MathF.Round(pos.Z * scale)}";
        }

        private static bool ShouldSkipStartConnector(OpenConnector open, DungeonState state)
        {
            float dx = open.WorldPos.X - state.StartingPosition.X;
            float dy = open.WorldPos.Y - state.StartingPosition.Y;
            float dz = open.WorldPos.Z - state.StartingPosition.Z;
            return dx * dx + dy * dy + dz * dz <= StartPosTolerance * StartPosTolerance;
        }

        private static bool IsWindowPlacedAt(P3Float pos, DungeonState state)
        {
            if (state.windowConnectors == null || state.windowConnectors.Count == 0)
                return false;

            foreach (var windowPos in state.windowConnectors)
            {
                if (SamePosition(windowPos, pos))
                    return true;
            }

            return false;
        }

        private static FormList FindBlockerFormList(string listEditorId)
        {
            foreach (var fl in gen_quest_main.myMod.FormLists)
            {
                if (string.Equals(fl.EditorID, listEditorId, StringComparison.OrdinalIgnoreCase))
                    return fl;
            }

            return null;
        }

        private static string PickBlockerFromFormList(FormList list, string doorSize)
        {
            if (list?.Items == null || list.Items.Count == 0)
                return null;

            var candidates = new List<string>();
            foreach (var item in list.Items)
            {
                if (!gen_quest_main.myMod.PackIns.TryGetValue(item.FormKey, out var packIn))
                    continue;

                if (string.IsNullOrWhiteSpace(packIn?.EditorID))
                    continue;

                candidates.Add(packIn.EditorID);
            }

            if (candidates.Count == 0)
                return null;

            var sizeMatches = string.IsNullOrWhiteSpace(doorSize)
                ? null
                : candidates.Where(id => id.IndexOf(doorSize, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            var pool = sizeMatches != null && sizeMatches.Count > 0 ? sizeMatches : candidates;
            return pool[RandomUtils.random.Next(pool.Count)];
        }
    }
}
