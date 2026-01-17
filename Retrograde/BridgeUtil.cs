using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde;
using Mutagen.Bethesda;
using System;
using System.Collections.Generic;
using Noggog;

namespace FrankyCLI
{
    public static class BridgeUtil
    {
        public static HashSet<string> BuildBridgePrefabKeys(IEnumerable<string> bridgeRoomLists)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var list in bridgeRoomLists)
            {
                if (string.IsNullOrWhiteSpace(list))
                    continue;

                var utils = new RoomUtils(list);
                if (utils?.roomTemplates == null)
                    continue;

                foreach (var entry in utils.roomTemplates)
                {
                    var formList = entry.Value;
                    if (formList?.Items == null || formList.Items.Count == 0)
                        continue;

                    foreach (var item in formList.Items)
                    {
                        if (!gen_quest_main.myMod.PackIns.TryGetValue(item.FormKey, out var packIn))
                            continue;

                        var editorId = packIn?.EditorID;
                        if (string.IsNullOrWhiteSpace(editorId))
                            continue;

                        var prefab = new RoomPrefab(editorId);
                        var connectors = ConnectorUtils.GetConnectors(prefab);
                        if (connectors.Count < 2)
                            continue;

                        for (int i = 0; i < connectors.Count - 1; i++)
                        {
                            for (int j = i + 1; j < connectors.Count; j++)
                            {
                                TryRegisterPrefabSignature(connectors[i], connectors[j], keys);
                                TryRegisterPrefabSignature(connectors[j], connectors[i], keys);
                            }
                        }
                    }
                }
            }

            return keys;
        }

        public static void TryRegisterPrefabSignature(RgConnectorInstance anchor, RgConnectorInstance other, HashSet<string> keys)
        {
            if (!anchor.Parsed.IsValid || !other.Parsed.IsValid)
                return;

            var a = ToOpenConnector(anchor);
            var b = ToOpenConnector(other);

            if (!ArePairCompatible(a, b))
                return;

            if (!TryBuildBridgeKey(a, b, out var key))
                return;

            keys.Add(key);
        }

        public static OpenConnector ToOpenConnector(RgConnectorInstance conn)
        {
            return new OpenConnector
            {
                Parsed = new RgConnector
                {
                    RawEditorId = conn.Parsed.RawEditorId,
                    Direction = ConnectorUtils.Opposite(conn.Parsed.Direction),
                    DoorSize = conn.Parsed.DoorSize,
                    Tileset = conn.Parsed.Tileset,
                    IsValid = conn.Parsed.IsValid
                },
                WorldPos = conn.LocalPos,
                YawSteps = 0,
                DistrictType = null
            };
        }

        public static bool ArePairCompatible(OpenConnector a, OpenConnector b)
        {
            return a.Parsed.IsValid &&
                   b.Parsed.IsValid &&
                   string.Equals(a.Parsed.Tileset, b.Parsed.Tileset, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.Parsed.DoorSize, b.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase);
        }

        public static bool HaveSameOwner(List<PlacedRoom> rooms, OpenConnector a, OpenConnector b, float tolerance)
        {
            int ownerA = ResolveConnectorOwner(rooms, a, tolerance);
            int ownerB = ResolveConnectorOwner(rooms, b, tolerance);
            return ownerA >= 0 && ownerA == ownerB;
        }

        public static int ResolveConnectorOwner(List<PlacedRoom> rooms, OpenConnector open, float tolerance)
        {
            if (rooms == null || rooms.Count == 0)
                return -1;

            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (room.Connectors == null)
                    continue;

                foreach (var conn in room.Connectors)
                {
                    var worldPos = room.WorldPos + conn.LocalPos;
                    if (MathUtil.PositionsClose(worldPos, open.WorldPos, tolerance))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        public static bool MatchesOpenConnector(OpenConnector open, RgConnectorInstance candidate)
        {
            return candidate.Parsed.Direction == ConnectorUtils.Opposite(open.Parsed.Direction) &&
                   string.Equals(candidate.Parsed.DoorSize, open.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(candidate.Parsed.Tileset, open.Parsed.Tileset, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSameConnector(RgConnectorInstance a, RgConnectorInstance b)
        {
            return string.Equals(a.EditorId, b.EditorId, StringComparison.OrdinalIgnoreCase) &&
                   a.LocalPos.Equals(b.LocalPos);
        }

        public static bool AnyConnectorInsideExistingBounds(
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

        public static bool AnyExistingConnectorInsideCandidate(
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

        public static bool IsPointStrictlyInside(P3Float point, RgAabb aabb, float tolerance)
        {
            return point.X > aabb.Min.X + tolerance &&
                   point.X < aabb.Max.X - tolerance &&
                   point.Y > aabb.Min.Y + tolerance &&
                   point.Y < aabb.Max.Y - tolerance &&
                   point.Z > aabb.Min.Z + tolerance &&
                   point.Z < aabb.Max.Z - tolerance;
        }

        public static bool TryBuildBridgeKey(OpenConnector a, OpenConnector b, out string key)
        {
            key = null;
            if (!a.Parsed.IsValid || !b.Parsed.IsValid)
                return false;

            var anchorTargetDir = ConnectorUtils.Opposite(a.Parsed.Direction);
            int yawToMatchA = DirectionToYawSteps(anchorTargetDir);
            if (yawToMatchA < 0)
                return false;

            var delta = b.WorldPos - a.WorldPos;
            var localDelta = RgRotation.RotateYaw90(delta, -yawToMatchA);

            int rx = (int)MathF.Round(localDelta.X);
            int ry = (int)MathF.Round(localDelta.Y);
            int rz = (int)MathF.Round(localDelta.Z);

            if (rx == 0 && ry == 0 && rz == 0)
                return false;

            var otherDir = RgRotation.RotateDir(
                ConnectorUtils.Opposite(b.Parsed.Direction),
                -yawToMatchA);

            key = $"{a.Parsed.Tileset}|{a.Parsed.DoorSize}|{yawToMatchA}|{otherDir}|{rx},{ry},{rz}";
            return true;
        }

        private static int DirectionToYawSteps(ConnectorDirection dir)
        {
            return dir switch
            {
                ConnectorDirection.North => 0,
                ConnectorDirection.East => 1,
                ConnectorDirection.South => 2,
                ConnectorDirection.West => 3,
                _ => -1
            };
        }

        public static int CountBridgeablePairs(List<OpenConnector> connectors, float yMin, float maxHorizontalSpan, float maxVerticalOffset, HashSet<string> bridgeKeys)
        {
            if (connectors == null || connectors.Count < 2)
                return 0;

            int count = 0;

            for (int i = 0; i < connectors.Count - 1; i++)
            {
                var a = connectors[i];
                if (!a.Parsed.IsValid || a.WorldPos.Y < yMin)
                    continue;

                for (int j = i + 1; j < connectors.Count; j++)
                {
                    var b = connectors[j];
                    if (!b.Parsed.IsValid || b.WorldPos.Y < yMin)
                        continue;

                    if (!string.Equals(a.Parsed.Tileset, b.Parsed.Tileset, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(a.Parsed.DoorSize, b.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase))
                        continue;

                    float dx = a.WorldPos.X - b.WorldPos.X;
                    float dy = a.WorldPos.Y - b.WorldPos.Y;
                    float dz = a.WorldPos.Z - b.WorldPos.Z;

                    if (MathF.Max(MathF.Abs(dx), MathF.Abs(dy)) > maxHorizontalSpan)
                        continue;

                    if (MathF.Abs(dz) > maxVerticalOffset)
                        continue;

                    if (bridgeKeys != null && bridgeKeys.Count > 0)
                    {
                        if (!TryBuildBridgeKey(a, b, out var key))
                            continue;
                        if (!bridgeKeys.Contains(key))
                            continue;
                    }

                    count++;
                }
            }

            return count;
        }
    }
}
