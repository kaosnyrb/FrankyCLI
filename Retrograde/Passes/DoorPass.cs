using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using FrankyCLI.questgen_tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Noggog;

namespace FrankyCLI.Retrograde.Passes
{
    public class DoorPass : IGenPass
    {
        public void RunPass(DungeonState state)
        {
            const float positionTolerance = 0.01f;
            const float coverageRatio = 0.5f;

            var connections = CollectConnectorPairs(state.placedRooms, positionTolerance);
            if (connections.Count == 0)
                return;

            // Favor doors between larger rooms, but still keep some randomness
            var prioritized = connections
                .OrderByDescending(c => c.SizeScore)
                .ThenBy(_ => RandomUtils.random.Next())
                .ToList();

            int toCover = (int)Math.Floor(prioritized.Count * coverageRatio);
            if (toCover <= 0)
                return;

            for (int idx = 0; idx < toCover; idx++)
            {
                var connection = prioritized[idx];
                var open = connection.A;

                // Pick blocker prefab based on door size / tileset
                var blockerId = ConnectorUtils.GetDoor(open.Parsed.DoorSize, open.Parsed.Tileset);
                var blockerPrefab = new RoomPrefab(blockerId);

                // Blocker should have a connector that will attach to the OPEN connector.
                // If the open connector faces North, the blocker needs a South-facing connector to mate.
                var requiredDir = ConnectorUtils.Opposite(open.Parsed.Direction);

                // Try yaw steps 0..3 to orient blocker correctly (same approach as rooms)
                bool placed = false;

                for (int yawSteps = 0; yawSteps < 4; yawSteps++)
                {
                    var blockerConns = ConnectorUtils.GetConnectors(blockerPrefab, yawSteps);

                    // Find a connector on the blocker that matches required direction and same door size/tileset.
                    // If your blocker is generic and doesn’t encode tileset, drop that constraint.
                    var attach = blockerConns.FirstOrDefault(c =>
                        c.Parsed.Direction == requiredDir &&
                        string.Equals(c.Parsed.DoorSize, open.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Parsed.Tileset, open.Parsed.Tileset, StringComparison.OrdinalIgnoreCase));

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

                    placed = true;
                    break;
                }

                // Optional: log missing blocker connector rather than hard fail
                if (!placed)
                {
                    // You may want to add logging here, e.g. Debug.WriteLine(...)
                }
            }
        }

        private static List<DoorConnection> CollectConnectorPairs(List<PlacedRoom> placedRooms, float tolerance)
        {
            var roomVolumes = placedRooms
                .Select(ComputeRoomVolume)
                .ToList();

            var connectors = new List<(OpenConnector Conn, int RoomIndex)>();
            for (int roomIndex = 0; roomIndex < placedRooms.Count; roomIndex++)
            {
                var room = placedRooms[roomIndex];
                foreach (var c in room.Connectors)
                {
                    connectors.Add((
                        new OpenConnector
                        {
                            Parsed = c.Parsed,
                            YawSteps = room.YawSteps,
                            WorldPos = room.WorldPos + c.LocalPos,
                            DistrictType = room.DistrictType
                        },
                        roomIndex));
                }
            }

            var pairs = new List<DoorConnection>();
            var used = new bool[connectors.Count];

            for (int i = 0; i < connectors.Count; i++)
            {
                if (used[i])
                    continue;

                for (int j = i + 1; j < connectors.Count; j++)
                {
                    if (used[j])
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

                    used[i] = used[j] = true;
                    pairs.Add(new DoorConnection
                    {
                        A = connectors[i].Conn,
                        B = connectors[j].Conn,
                        SizeScore = (roomVolumes[connectors[i].RoomIndex] + roomVolumes[connectors[j].RoomIndex]) * 0.5f
                    }); // place door using one side of the matched pair
                    break;
                }
            }

            return pairs;
        }

        private struct DoorConnection
        {
            public OpenConnector A;
            public OpenConnector B;
            public float SizeScore;
        }

        private static bool SamePosition(P3Float a, P3Float b, float tolerance)
        {
            return MathUtil.DistanceSquared(a, b) <= tolerance * tolerance;
        }

        private static float ComputeRoomVolume(PlacedRoom room)
        {
            if (room.Prefab?.packin_instance == null)
                return 0f;

            var aabb = ConnectorUtils.ToWorldAabbRotated(room.Prefab.packin_instance.ObjectBounds, room.WorldPos, room.YawSteps);
            var dx = Math.Max(0f, aabb.Max.X - aabb.Min.X);
            var dy = Math.Max(0f, aabb.Max.Y - aabb.Min.Y);
            var dz = Math.Max(0f, aabb.Max.Z - aabb.Min.Z);
            return dx * dy * dz;
        }
    }
}
