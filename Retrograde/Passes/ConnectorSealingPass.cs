using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FrankyCLI.questgen_tools;
using Noggog;

namespace FrankyCLI.Retrograde.Passes
{
    public class ConnectorSealingPass : IGenPass
    {
        public string GetDoorBlocker(string doorSize, string tileset)
        {
            // Prefer: tileset-specific blockers, fallback to generic
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
            const float positionTolerance = 0.01f;
            const float startPosTolerance = 0.01f; // start connector sits exactly at StartingPosition
            var sealedPositions = new HashSet<string>();

            // Close any connectors below the allowed Y plane first, then process the rest.
            var ordered = state.openConnectors
                .OrderBy(c => c.WorldPos.Y < state.YMin ? 0 : 1)
                .ToList();

            foreach (var open in ordered)
            {
                if (ShouldSkipStartConnector(open, state, startPosTolerance))
                    continue;

                bool belowYMin = open.WorldPos.Y < state.YMin;

                // First try with strict matching; if we're below YMin, relax tileset matching to ensure we seal it.
                bool placed = TryPlaceBlocker(open, state, requireTilesetMatch: true);
                if (!placed && belowYMin)
                {
                    placed = TryPlaceBlocker(open, state, requireTilesetMatch: false);
                }

                if (placed)
                {
                    sealedPositions.Add(PositionKey(open.WorldPos, positionTolerance));
                }

                // Optional: log missing blocker connector rather than hard fail
                if (!placed)
                {
                    // You may want to add logging here, e.g. Debug.WriteLine(...)
                }
            }

            // After the usual open-connector sealing, sweep every placed room for any stray connectors
            // that failed to connect and close them off.
            SealUnconnectedPlacedMarkers(state, sealedPositions, positionTolerance);
        }

        private bool TryPlaceBlocker(OpenConnector open, DungeonState state, bool requireTilesetMatch)
        {
            // Pick blocker prefab based on door size / tileset
            var blockerId = GetDoorBlocker(open.Parsed.DoorSize, open.Parsed.Tileset);
            var blockerPrefab = new RoomPrefab(blockerId);

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

                state.instance.Temporary.Add(new PlacedObject(gen_quest_main.myMod)
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

        private void SealUnconnectedPlacedMarkers(DungeonState state, HashSet<string> sealedPositions, float tolerance)
        {
            if (state.placedRooms == null || state.placedRooms.Count == 0)
                return;

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
                            WorldPos = room.WorldPos + connector.LocalPos
                        },
                        roomIndex));
                }
            }

            if (connectors.Count == 0)
                return;

            var matched = new bool[connectors.Count];
            for (int i = 0; i < connectors.Count; i++)
            {
                if (matched[i])
                    continue;

                for (int j = i + 1; j < connectors.Count; j++)
                {
                    if (matched[j])
                        continue;

                    if (connectors[i].RoomIndex == connectors[j].RoomIndex)
                        continue;

                    if (!SamePosition(connectors[i].Conn.WorldPos, connectors[j].Conn.WorldPos, tolerance))
                        continue;

                    if (ConnectorUtils.Opposite(connectors[i].Conn.Parsed.Direction) != connectors[j].Conn.Parsed.Direction)
                        continue;

                    if (!string.Equals(connectors[i].Conn.Parsed.DoorSize, connectors[j].Conn.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.Equals(connectors[i].Conn.Parsed.Tileset, connectors[j].Conn.Parsed.Tileset, StringComparison.OrdinalIgnoreCase))
                        continue;

                    matched[i] = matched[j] = true;
                    break;
                }
            }

            for (int i = 0; i < connectors.Count; i++)
            {
                if (matched[i])
                    continue;

                var open = connectors[i].Conn;
                if (ShouldSkipStartConnector(open, state, tolerance))
                    continue;

                var key = PositionKey(open.WorldPos, tolerance);
                if (sealedPositions.Contains(key))
                    continue;

                bool placed = TryPlaceBlocker(open, state, requireTilesetMatch: true);
                if (!placed && open.WorldPos.Y < state.YMin)
                {
                    placed = TryPlaceBlocker(open, state, requireTilesetMatch: false);
                }

                if (placed)
                {
                    sealedPositions.Add(key);
                }
            }
        }

        private static bool SamePosition(P3Float a, P3Float b, float tolerance)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz <= tolerance * tolerance;
        }

        private static string PositionKey(P3Float pos, float tolerance)
        {
            float scale = 1f / tolerance;
            return $"{MathF.Round(pos.X * scale)}|{MathF.Round(pos.Y * scale)}|{MathF.Round(pos.Z * scale)}";
        }

        private static bool ShouldSkipStartConnector(OpenConnector open, DungeonState state, float posTolerance)
        {
            // Protect the initial spine/start connector: sits exactly at StartingPosition.
            return SamePosition(open.WorldPos, state.StartingPosition, posTolerance);
        }
    }
}
