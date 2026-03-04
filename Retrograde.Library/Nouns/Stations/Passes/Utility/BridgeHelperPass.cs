using Mutagen.Bethesda;
using Noggog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Retrograde.Passes.SpaceStation
{
    public class BridgeHelperPass : IGenPass
    {
        private const float CorridorPadding = 0.5f;
        private const float ConnectorMatchTolerance = 0.05f;
        private readonly string outputPath;
        private readonly string historyPath;
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
            this.historyPath = Path.ChangeExtension(outputPath, ".hits.tsv");
            this.maxHorizontalSpan = maxHorizontalSpan;
            this.maxVerticalOffset = maxVerticalOffset;

            var lists = bridgeRoomLists ?? new[] { "rg_trunklist"};
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
                            suggestion.HitCount = 1;
                            suggestion.Samples.Add((a.WorldPos, b.WorldPos));
                            suggestions.Add(key, suggestion);
                        }
                        else
                        {
                            existing.HitCount++;
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

            var previousHits = LoadHitHistory();
            var mergedHits = MergeHits(suggestions, previousHits);
            SaveHitHistory(mergedHits);
            WriteReport(suggestions.Values, mergedHits);
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
            if (state?.TrunkRoomLists != null && state.TrunkRoomLists.Count > 0)
                return state.TrunkRoomLists;

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

        private Dictionary<string, int> LoadHitHistory()
        {
            var hits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (!File.Exists(historyPath))
                return hits;

            try
            {
                foreach (var line in File.ReadLines(historyPath))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var tabIndex = line.IndexOf('\t');
                    if (tabIndex <= 0 || tabIndex >= line.Length - 1)
                        continue;

                    var key = line.Substring(0, tabIndex);
                    if (int.TryParse(line.Substring(tabIndex + 1), out var count) && count > 0)
                    {
                        hits[key] = count;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BridgeHelper] Warning: could not load hit history from {historyPath}: {ex.Message}");
            }

            return hits;
        }

        private Dictionary<string, int> MergeHits(
            Dictionary<string, BridgePrefabSuggestion> currentSuggestions,
            Dictionary<string, int> previousHits)
        {
            var merged = new Dictionary<string, int>(previousHits, StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in currentSuggestions)
            {
                merged.TryGetValue(kvp.Key, out var prev);
                merged[kvp.Key] = prev + kvp.Value.HitCount;
            }

            return merged;
        }

        private void SaveHitHistory(Dictionary<string, int> mergedHits)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(historyPath) ?? ".");

                var lines = new List<string>
                {
                    $"# BridgeHelper cumulative hit counts – updated {DateTime.UtcNow:O} (UTC)",
                    "# key\thits"
                };

                foreach (var kvp in mergedHits.OrderByDescending(h => h.Value))
                {
                    lines.Add($"{kvp.Key}\t{kvp.Value}");
                }

                File.WriteAllLines(historyPath, lines);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BridgeHelper] Warning: could not save hit history to {historyPath}: {ex.Message}");
            }
        }

        private void WriteReport(IEnumerable<BridgePrefabSuggestion> suggestions, Dictionary<string, int> cumulativeHits)
        {
            var ordered = suggestions
                .Select(s =>
                {
                    cumulativeHits.TryGetValue(s.GetKey(), out var cumulative);
                    return (Suggestion: s, CumulativeHits: cumulative);
                })
                .OrderByDescending(x => x.CumulativeHits)
                .ThenByDescending(x => x.Suggestion.HitCount)
                .ThenBy(x => x.Suggestion.Tileset, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Suggestion.DoorSize, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var lines = new List<string>
            {
                "# Bridge Prefab Build Guide",
                $"# Generated by BridgeHelperPass – {DateTime.UtcNow:O} (UTC)",
                "#",
                "# Sorted by cumulative hit count (most needed bridge pieces first).",
                "# hits       = connector pairs that needed this bridge in this run",
                "# cumulative = total hits across all runs (higher = build this first)",
                "#",
                "# HOW TO USE: Each entry below describes a bridge prefab to build.",
                "#   1. Create a new PackIn prefab in the Creation Kit.",
                "#   2. Place the two connector markers at the listed positions with the listed EditorIDs.",
                "#      NOTE: The connector directions are the OPPOSITE of the open connectors they mate with.",
                "#      An open connector facing North needs a PackIn connector facing South, etc.",
                "#   3. Build geometry connecting the two markers.",
                "#   4. Set the PackIn's ObjectBounds to the suggested min bounding box (or larger).",
                "#   5. Add the prefab to your bridge room list FormList.",
                "#   6. The system will auto-rotate the prefab at placement time; only one orientation is needed.",
                "#",
                $"# Only pairs within {maxHorizontalSpan}u horizontal and {maxVerticalOffset}u vertical are listed.",
                ""
            };

            int rank = 0;
            foreach (var (s, cumulative) in ordered)
            {
                rank++;
                var anchorDir = ConnectorDirection.North;
                var dirLetterA = DirectionToLetter(anchorDir);
                var dirLetterB = DirectionToLetter(s.OtherConnectorDirection);
                var markerA = $"rg_conn_{dirLetterA}_{s.DoorSize}_{s.Tileset}";
                var markerB = $"rg_conn_{dirLetterB}_{s.DoorSize}_{s.Tileset}";
                var openDirA = ConnectorUtils.Opposite(anchorDir);
                var openDirB = ConnectorUtils.Opposite(s.OtherConnectorDirection);

                var pos = s.OtherConnectorLocalPos;
                var dx = Math.Abs(pos.X);
                var dy = Math.Abs(pos.Y);
                var dz = Math.Abs(pos.Z);

                string shape;
                if (dz > 0 && (dx > 0 || dy > 0))
                    shape = "ramp/stairway";
                else if (dz > 0)
                    shape = "vertical shaft";
                else if (dx > 0 && dy > 0)
                    shape = "L-bend corridor";
                else
                    shape = "straight corridor";

                var boundsMin = new IntVector3(
                    Math.Min(0, pos.X),
                    Math.Min(0, pos.Y),
                    Math.Min(0, pos.Z));
                var boundsMax = new IntVector3(
                    Math.Max(0, pos.X),
                    Math.Max(0, pos.Y),
                    Math.Max(0, pos.Z));

                lines.Add($"--- #{rank}  hits={s.HitCount}  cumulative={cumulative}  ({shape}) ---");
                lines.Add($"  tileset: {s.Tileset}    door: {s.DoorSize}");
                lines.Add($"  marker A:  {markerA}  (faces {anchorDir}, mates with open {openDirA})");
                lines.Add($"             pos = (0, 0, 0)");
                lines.Add($"  marker B:  {markerB}  (faces {s.OtherConnectorDirection}, mates with open {openDirB})");
                lines.Add($"             pos = ({pos.X}, {pos.Y}, {pos.Z})");
                lines.Add($"  min bounds: ({boundsMin.X}, {boundsMin.Y}, {boundsMin.Z}) to ({boundsMax.X}, {boundsMax.Y}, {boundsMax.Z})");

                var samplePairs = s.Samples
                    .Take(3)
                    .Select(p => $"({FormatPos(p.A)}) -> ({FormatPos(p.B)})");
                lines.Add($"  world samples: {string.Join(";  ", samplePairs)}");
                lines.Add("");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            File.WriteAllLines(outputPath, lines);

            Console.WriteLine($"[BridgeHelper] Wrote {ordered.Count} prefab build suggestions to {outputPath} (history: {historyPath})");
        }

        private static string DirectionToLetter(ConnectorDirection dir)
        {
            return dir switch
            {
                ConnectorDirection.North => "n",
                ConnectorDirection.East => "e",
                ConnectorDirection.South => "s",
                ConnectorDirection.West => "w",
                _ => "n"
            };
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
                if (utils == null) continue;

                // Use pre-cached EditorIDs across all tilesets — no FindPackIn calls.
                foreach (var editorId in utils.GetAllCandidatesAllTilesets())
                {
                    if (string.IsNullOrWhiteSpace(editorId))
                        continue;

                    var prefab = PrefabCache.GetPrefab(editorId);
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
            public int HitCount;
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
