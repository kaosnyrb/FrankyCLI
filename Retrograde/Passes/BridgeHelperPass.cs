using FrankyCLI.Retrograde;
using FrankyCLI.Retrograde.Passes;
using Noggog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    public class BridgeHelperPass : IGenPass
    {
        private readonly string outputPath;
        private readonly float maxHorizontalSpan;
        private readonly float maxVerticalOffset;

        public BridgeHelperPass(
            string outputPath = "Retrograde/bridge_helper_suggestions.txt",
            float maxHorizontalSpan = 40f,
            float maxVerticalOffset = 8f)
        {
            this.outputPath = outputPath;
            this.maxHorizontalSpan = maxHorizontalSpan;
            this.maxVerticalOffset = maxVerticalOffset;
        }

        public void RunPass(DungeonState state)
        {
            if (state?.openConnectors == null || state.openConnectors.Count < 2)
                return;

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

                    if (TryBuildSuggestion(a, b, out var suggestion))
                    {
                        var key = suggestion.GetKey();
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
