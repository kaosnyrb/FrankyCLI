using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde;
using Mutagen.Bethesda;
using System;
using System.Collections.Generic;

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
    }
}
