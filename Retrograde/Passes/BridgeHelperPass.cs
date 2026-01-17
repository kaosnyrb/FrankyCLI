using FrankyCLI.Retrograde;
using FrankyCLI.Retrograde.Passes;
using Mutagen.Bethesda;
using Noggog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    public class BridgeHelperPass : IGenPass
    {
        private const float CorridorPadding = 0.5f;
        private const float ConnectorMatchTolerance = 0.05f;
        private readonly string outputPath;
        private readonly float maxHorizontalSpan;
        private readonly float maxVerticalOffset;
        private readonly List<string> fallbackBridgeRoomLists;
        private readonly List<RoomUtils> fallbackRoomUtils;

        public BridgeHelperPass(
            string outputPath = "Retrograde/bridge_helper_suggestions.txt",
            float maxHorizontalSpan = 40f,
            float maxVerticalOffset = 8f,
            IEnumerable<string> bridgeRoomLists = null)
        {
            this.outputPath = outputPath;
            this.maxHorizontalSpan = maxHorizontalSpan;
            this.maxVerticalOffset = maxVerticalOffset;

            var lists = bridgeRoomLists ?? new[] { "rg_trunklist", "rg_bridgelist" };
            var bridgeLists = lists
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            fallbackBridgeRoomLists = bridgeLists;
            fallbackRoomUtils = bridgeLists
                .Select(name => new RoomUtils(name))
                .ToList();
        }

        public void RunPass(DungeonState state)
        {
            if (state?.openConnectors == null || state.openConnectors.Count < 2)
                return;

            var roomUtils = ResolveRoomUtils(state);
            var placedRoomBounds = BuildPlacedRoomBounds(state.placedRooms);

            var existingPieces = BuildExistingBridgeKeys(roomUtils);

            var eligible = state.openConnectors
                .Where(c => c.Parsed.IsValid && c.WorldPos.Y >= state.YMin)
                .ToList();

            if (eligible.Count < 2)
                return;

            var suggestions = new Dictionary<string, BridgePrefabSuggestion>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < eligible.Count - 1; i++)
            {
                var a = eligible[i];
                for (int j = i + 1; j < eligible.Count; j++)
                {
                    var b = eligible[j];

                    if (!ArePairCompatible(a, b))
                        continue;

                    if (IsBlockedByExistingRooms(a, b, placedRoomBounds))
                        continue;

                    if (TryBuildSuggestion(a, b, out var suggestion))
                    {
                        var key = suggestion.GetKey();
                        if (existingPieces.Contains(key))
                            continue;

                        if (!suggestions.TryGetValue(key, out var existing))
                        {
                            suggestion.Samples.Add((a.WorldPos, b.WorldPos));
                            suggestions.Add(key, suggestion);
                        }
                        else
                        {
                            if (existing.Samples.Count < 10)
                            {
                                existing.Samples.Add((a.WorldPos, b.WorldPos));
                            }
                            suggestions[key] = existing;
                        }
                    }
                }
            }

            if (suggestions.Count == 0)
                return;

            WriteReport(suggestions.Values);
        }

        private List<RoomUtils> ResolveRoomUtils(DungeonState state)
        {
            var bridgeLists = ResolveBridgeRoomLists(state);
            if (bridgeLists != null && bridgeLists.Count > 0)
            {
                return bridgeLists
                    .Select(name => new RoomUtils(name))
                    .ToList();
            }

            return fallbackRoomUtils;
        }

        private List<string> ResolveBridgeRoomLists(DungeonState state)
        {
            if (state?.BridgeRoomLists != null && state.BridgeRoomLists.Count > 0)
                return state.BridgeRoomLists;

            return fallbackBridgeRoomLists;
        }

        private bool TryBuildSuggestion(OpenConnector a, OpenConnector b, out BridgePrefabSuggestion suggestion)
        {
            suggestion = default;

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

            if (Math.Max(Math.Abs(rx), Math.Abs(ry)) > maxHorizontalSpan)
                return false;
            if (Math.Abs(rz) > maxVerticalOffset)
                return false;

            var otherDir = RgRotation.RotateDir(
                ConnectorUtils.Opposite(b.Parsed.Direction),
                -yawToMatchA);

            suggestion = new BridgePrefabSuggestion
            {
                Tileset = a.Parsed.Tileset,
                DoorSize = a.Parsed.DoorSize,
                AnchorYawToMatch = yawToMatchA,
                OtherConnectorDirection = otherDir,
                OtherConnectorLocalPos = new IntVector3(rx, ry, rz),
                Samples = new List<(P3Float A, P3Float B)>()
            };

            return true;
        }

        private static bool ArePairCompatible(OpenConnector a, OpenConnector b)
        {
            return a.Parsed.IsValid &&
                   b.Parsed.IsValid &&
                   string.Equals(a.Parsed.Tileset, b.Parsed.Tileset, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.Parsed.DoorSize, b.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase);
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

        private void WriteReport(IEnumerable<BridgePrefabSuggestion> suggestions)
        {
            var ordered = suggestions
                .OrderBy(s => s.Tileset, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.DoorSize, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.AnchorYawToMatch)
                .ThenBy(s => s.OtherConnectorDirection.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var lines = new List<string>
            {
                "# Generated by BridgeHelperPass",
                $"# {DateTime.UtcNow:O} (UTC)",
                "# Anchor connector is fixed at (0,0,0) facing North before yaw.",
                "# AnchorYawToMatch tells how many 90deg steps to rotate to face connector A.",
                "# Other connector position is rounded to whole units to guide prefab marker placement.",
                $"# Only pairs within {maxHorizontalSpan}u horizontally and {maxVerticalOffset}u vertically are listed.",
                ""
            };

            foreach (var s in ordered)
            {
                lines.Add($"tileset={s.Tileset} door={s.DoorSize} anchorYaw={s.AnchorYawToMatch} otherDir={s.OtherConnectorDirection}");
                lines.Add($"  connectorA: dir=North pos=(0,0,0)");
                lines.Add($"  connectorB: dir={s.OtherConnectorDirection} pos=({s.OtherConnectorLocalPos.X},{s.OtherConnectorLocalPos.Y},{s.OtherConnectorLocalPos.Z})");

                var samplePairs = s.Samples
                    .Take(3)
                    .Select(p => $"A({FormatPos(p.A)}) -> B({FormatPos(p.B)})");

                lines.Add($"  samples: {string.Join("; ", samplePairs)}");
                lines.Add("");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            File.WriteAllLines(outputPath, lines);

            Console.WriteLine($"[BridgeHelper] Wrote {ordered.Count} prefab suggestions to {outputPath}");
        }

        private static string FormatPos(P3Float pos)
        {
            return $"{pos.X:0.##},{pos.Y:0.##},{pos.Z:0.##}";
        }

        private HashSet<string> BuildExistingBridgeKeys(IEnumerable<RoomUtils> roomUtils)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (roomUtils == null)
                return keys;

            foreach (var utils in roomUtils)
            {
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

        private void TryRegisterPrefabSignature(RgConnectorInstance anchor, RgConnectorInstance other, HashSet<string> keys)
        {
            if (!anchor.Parsed.IsValid || !other.Parsed.IsValid)
                return;

            var a = ToOpenConnector(anchor);
            var b = ToOpenConnector(other);

            if (!ArePairCompatible(a, b))
                return;

            if (!TryBuildSuggestion(a, b, out var suggestion))
                return;

            keys.Add(suggestion.GetKey());
        }

        private static OpenConnector ToOpenConnector(RgConnectorInstance conn)
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

        private static List<(PlacedRoom Room, RgAabb Bounds)> BuildPlacedRoomBounds(List<PlacedRoom> placedRooms)
        {
            var bounds = new List<(PlacedRoom Room, RgAabb Bounds)>();
            if (placedRooms == null)
                return bounds;

            foreach (var room in placedRooms)
            {
                if (room.Prefab?.packin_instance == null)
                    continue;

                bounds.Add((room, ConnectorUtils.ToWorldAabbRotated(room.Prefab.packin_instance.ObjectBounds, room.WorldPos, room.YawSteps)));
            }

            return bounds;
        }

        private bool IsBlockedByExistingRooms(OpenConnector a, OpenConnector b, List<(PlacedRoom Room, RgAabb Bounds)> placedRoomBounds)
        {
            if (placedRoomBounds == null || placedRoomBounds.Count == 0)
                return false;

            var ownerA = FindOwningRoomIndex(a, placedRoomBounds);
            var ownerB = FindOwningRoomIndex(b, placedRoomBounds);

            var corridor = BuildCorridorAabb(a.WorldPos, b.WorldPos, CorridorPadding);

            for (int i = 0; i < placedRoomBounds.Count; i++)
            {
                if (i == ownerA || i == ownerB)
                    continue;

                var bounds = placedRoomBounds[i].Bounds;
                if (ConnectorUtils.Intersects(corridor, bounds))
                    return true;
            }

            return false;
        }

        private static int FindOwningRoomIndex(OpenConnector connector, List<(PlacedRoom Room, RgAabb Bounds)> placedRoomBounds)
        {
            for (int i = 0; i < placedRoomBounds.Count; i++)
            {
                var room = placedRoomBounds[i].Room;
                if (room.Connectors == null)
                    continue;

                foreach (var conn in room.Connectors)
                {
                    var worldPos = room.WorldPos + conn.LocalPos;
                    if (PositionsClose(worldPos, connector.WorldPos, ConnectorMatchTolerance))
                        return i;
                }
            }

            return -1;
        }

        private static bool PositionsClose(P3Float a, P3Float b, float tolerance)
        {
            return Math.Abs(a.X - b.X) <= tolerance &&
                   Math.Abs(a.Y - b.Y) <= tolerance &&
                   Math.Abs(a.Z - b.Z) <= tolerance;
        }

        private static RgAabb BuildCorridorAabb(P3Float a, P3Float b, float padding)
        {
            return new RgAabb
            {
                Min = new P3Float(
                    Math.Min(a.X, b.X) - padding,
                    Math.Min(a.Y, b.Y) - padding,
                    Math.Min(a.Z, b.Z) - padding),
                Max = new P3Float(
                    Math.Max(a.X, b.X) + padding,
                    Math.Max(a.Y, b.Y) + padding,
                    Math.Max(a.Z, b.Z) + padding)
            };
        }

        private struct BridgePrefabSuggestion
        {
            public string Tileset;
            public string DoorSize;
            public int AnchorYawToMatch;
            public ConnectorDirection OtherConnectorDirection;
            public IntVector3 OtherConnectorLocalPos;
            public List<(P3Float A, P3Float B)> Samples;

            public string GetKey()
            {
                return $"{Tileset}|{DoorSize}|{AnchorYawToMatch}|{OtherConnectorDirection}|{OtherConnectorLocalPos.X},{OtherConnectorLocalPos.Y},{OtherConnectorLocalPos.Z}";
            }
        }

        private struct IntVector3
        {
            public int X;
            public int Y;
            public int Z;

            public IntVector3(int x, int y, int z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }
    }
}
