using Mutagen.Bethesda.Plugins;
using Retrograde.Passes;
using Retrograde.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde;

public static class ScoringUtil
{
    public static PlanScore ScorePlan(ScoringSystem scoringSystem, int roomsPlaced, int bridgeablePairs, int bridgingOverlapCount = 0, int newConnectors = 0, double area = 0, double clustering = 0, double sizeDiversityPenalty = 0, double roomReuseScore = 0, double connectorViability = 0, double duplicateRoomPenalty = 0)
    {
        var components = new Dictionary<string, double>
        {
            { "Placement", roomsPlaced * scoringSystem.PlacementWeight },
            { "Bridging", bridgeablePairs * scoringSystem.BridgingWeight },
            { "BridgingOverlap", bridgingOverlapCount * scoringSystem.BridgingOverlapWeight },
            { "NewConnectors", newConnectors * scoringSystem.NewConnectorsWeight },
            { "Area", (area/10) * scoringSystem.AreaWeight },
            { "Clustering", (clustering/10) * scoringSystem.ClusteringWeight },
            { "SizeDiversity", sizeDiversityPenalty * scoringSystem.SizeDiversityWeight },
            { "RoomReuse", roomReuseScore * scoringSystem.RoomReuseWeight },
            { "ConnectorViability", connectorViability * scoringSystem.ConnectorViabilityWeight },
            { "DuplicateRoomPenalty", duplicateRoomPenalty * scoringSystem.DuplicateRoomPenaltyWeight }
        };

        return new PlanScore
        {
            Total = components.Values.Sum(),
            Components = components
        };
    }

    public static string PrettyPrintScore(PlanScore score, bool includeNewConnectors = false, bool includeBridgingOverlap = false)
    {
        if (score == null)
            return "score unavailable";

        var lines = new List<string>
        {
            $"score {score.Total:0.00}"
        };

        lines.Add("  " + FormatComponent(score, "Placement", "placement"));
        lines.Add("  " + FormatComponent(score, "Bridging", "bridging"));

        if (includeBridgingOverlap)
            lines.Add("  " + FormatComponent(score, "BridgingOverlap", "overlap"));

        if (includeNewConnectors)
            lines.Add("  " + FormatComponent(score, "NewConnectors", "new connectors"));

        lines.Add("  " + FormatComponent(score, "Area", "area"));
        lines.Add("  " + FormatComponent(score, "Clustering", "clustering"));
        lines.Add("  " + FormatComponent(score, "SizeDiversity", "sizeDiversity"));
        lines.Add("  " + FormatComponent(score, "RoomReuse", "roomReuse"));
        lines.Add("  " + FormatComponent(score, "ConnectorViability", "connectorViability"));
        lines.Add("  " + FormatComponent(score, "DuplicateRoomPenalty", "duplicateRoomPenalty"));

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatComponent(PlanScore score, string key, string label)
    {
        if (score.Components != null && score.Components.TryGetValue(key, out var value))
            return $"{label} {value:0.00}";

        return $"{label} n/a";
    }

    public static double CalculateTotalArea(IEnumerable<PlacedRoom> rooms)
    {
        if (rooms == null)
            return 0;

        double total = 0;
        foreach (var room in rooms)
        {
            if (room.Prefab?.packin_instance == null)
                continue;

            var bounds = ConnectorUtils.ToWorldAabbRotated(room.Prefab.packin_instance.ObjectBounds, room.WorldPos, room.YawSteps);
            var width = bounds.Max.X - bounds.Min.X;
            var depth = bounds.Max.Y - bounds.Min.Y;
            total += Math.Max(0, (double)width) * Math.Max(0, (double)depth);
        }

        return total;
    }

    public static double CalculateAverageMinimumDistance(IReadOnlyList<PlacedRoom> rooms)
    {
        if (rooms == null || rooms.Count < 2)
            return 0;

        double sum = 0;
        for (int i = 0; i < rooms.Count; i++)
        {
            double minDistSq = double.MaxValue;
            for (int j = 0; j < rooms.Count; j++)
            {
                if (i == j)
                    continue;

                var distSq = MathUtil.DistanceSquared(rooms[i].WorldPos, rooms[j].WorldPos);
                if (distSq < minDistSq)
                    minDistSq = distSq;
            }

            if (minDistSq < double.MaxValue)
            {
                sum += Math.Sqrt(minDistSq);
            }
        }

        return sum / rooms.Count;
    }

    public static double CalculateSmallRoomChainPenalty(IReadOnlyList<PlacedRoom> rooms, double smallAreaThreshold = 200)
    {
        if (rooms == null || rooms.Count == 0)
            return 0;

        int maxStreak = 0;
        int currentStreak = 0;

        foreach (var room in rooms)
        {
            var area = GetRoomFootprintArea(room);
            if (area > 0 && area <= smallAreaThreshold)
            {
                currentStreak++;
                if (currentStreak > maxStreak)
                    maxStreak = currentStreak;
            }
            else
            {
                currentStreak = 0;
            }
        }

        return Math.Max(0, maxStreak - 1);
    }

    public static double CalculateRoomReuseScore(IReadOnlyList<PlacedRoom> rooms)
    {
        if (rooms == null || rooms.Count == 0)
            return 0;

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var room in rooms)
        {
            var id = room.Prefab?.PrefabEditorId;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            counts[id] = counts.TryGetValue(id, out var c) ? c + 1 : 1;
        }

        double reuse = 0;
        foreach (var kvp in counts)
        {
            if (kvp.Value > 1)
            {
                reuse += kvp.Value - 1;
            }
        }

        return reuse;
    }

    public static double CalculateConnectorViabilityArea(IReadOnlyList<PlacedRoom> rooms, IReadOnlyList<OpenConnector> openConnectors, double defaultDepth = 20, double apertureWidth = 1)
    {
        if (openConnectors == null || openConnectors.Count == 0)
            return 0;

        var roomBounds = new List<RgAabb>();
        if (rooms != null)
        {
            foreach (var room in rooms)
            {
                if (room.Prefab?.packin_instance == null)
                    continue;
                roomBounds.Add(ConnectorUtils.ToWorldAabbRotated(room.Prefab.packin_instance.ObjectBounds, room.WorldPos, room.YawSteps));
            }
        }

        double totalArea = 0;
        foreach (var open in openConnectors)
        {
            var dir = open.Parsed.Direction;
            double minClearance = defaultDepth;

            foreach (var aabb in roomBounds)
            {
                switch (dir)
                {
                    case ConnectorDirection.North:
                        if (open.WorldPos.X < aabb.Min.X || open.WorldPos.X > aabb.Max.X) break;
                        if (open.WorldPos.Z < aabb.Min.Z || open.WorldPos.Z > aabb.Max.Z) break;
                        if (aabb.Min.Y > open.WorldPos.Y)
                            minClearance = Math.Min(minClearance, aabb.Min.Y - open.WorldPos.Y);
                        break;
                    case ConnectorDirection.South:
                        if (open.WorldPos.X < aabb.Min.X || open.WorldPos.X > aabb.Max.X) break;
                        if (open.WorldPos.Z < aabb.Min.Z || open.WorldPos.Z > aabb.Max.Z) break;
                        if (aabb.Max.Y < open.WorldPos.Y)
                            minClearance = Math.Min(minClearance, open.WorldPos.Y - aabb.Max.Y);
                        break;
                    case ConnectorDirection.East:
                        if (open.WorldPos.Y < aabb.Min.Y || open.WorldPos.Y > aabb.Max.Y) break;
                        if (open.WorldPos.Z < aabb.Min.Z || open.WorldPos.Z > aabb.Max.Z) break;
                        if (aabb.Min.X > open.WorldPos.X)
                            minClearance = Math.Min(minClearance, aabb.Min.X - open.WorldPos.X);
                        break;
                    case ConnectorDirection.West:
                        if (open.WorldPos.Y < aabb.Min.Y || open.WorldPos.Y > aabb.Max.Y) break;
                        if (open.WorldPos.Z < aabb.Min.Z || open.WorldPos.Z > aabb.Max.Z) break;
                        if (aabb.Max.X < open.WorldPos.X)
                            minClearance = Math.Min(minClearance, open.WorldPos.X - aabb.Max.X);
                        break;
                }
            }

            minClearance = Math.Max(0, minClearance);
            totalArea += minClearance * apertureWidth;
        }

        return totalArea;
    }

    /// <summary>
    /// Calculates a penalty score based on how many times each room prefab in the
    /// plan already appears as a temporary placed reference in other cells of the mod.
    /// Returns the sum of existing placement counts for all rooms in the plan.
    /// </summary>
    public static double CalculateDuplicateRoomPenalty(IReadOnlyList<PlacedRoom> rooms)
    {
        if (rooms == null || rooms.Count == 0)
            return 0;

        var packInKeys = new HashSet<FormKey>();
        foreach (var room in rooms)
        {
            if (room.Prefab?.packin_instance != null)
                packInKeys.Add(room.Prefab.packin_instance.FormKey);
        }

        if (packInKeys.Count == 0)
            return 0;

        var counts = DuplicateRoomTools.CountPackInPlacements(packInKeys);

        double penalty = 0;
        foreach (var room in rooms)
        {
            if (room.Prefab?.packin_instance == null)
                continue;

            var key = room.Prefab.packin_instance.FormKey;
            if (counts.TryGetValue(key, out var count))
                penalty += count;
        }

        return penalty;
    }

    /// <summary>
    /// Lightweight composite score for comparing placement candidates within a single step.
    /// Uses the same weights from ScoringSystem as the plan-level scoring, applied to
    /// metrics that are cheap to compute per-candidate: bridging, connector viability,
    /// size diversity, area, clustering, room reuse, and new connector count.
    /// </summary>
    public static double ScorePlacementCandidate(
        ScoringSystem scoring,
        int bridgeScore,
        int newConnectorCount,
        IReadOnlyList<PlacedRoom> roomsIncludingCandidate,
        IReadOnlyList<OpenConnector> connectorsAfterPlacement)
    {
        double viability = CalculateConnectorViabilityArea(roomsIncludingCandidate, connectorsAfterPlacement);
        double smallChain = CalculateSmallRoomChainPenalty(roomsIncludingCandidate);
        double area = CalculateTotalArea(roomsIncludingCandidate);
        double clustering = CalculateAverageMinimumDistance(roomsIncludingCandidate);
        double roomReuse = CalculateRoomReuseScore(roomsIncludingCandidate);

        return (bridgeScore * scoring.BridgingWeight)
             + (newConnectorCount * scoring.NewConnectorsWeight)
             + ((area / 10) * scoring.AreaWeight)
             + ((clustering / 10) * scoring.ClusteringWeight)
             + (smallChain * scoring.SizeDiversityWeight)
             + (roomReuse * scoring.RoomReuseWeight)
             + (viability * scoring.ConnectorViabilityWeight);
    }

    private static double GetRoomFootprintArea(PlacedRoom room)
    {
        if (room.Prefab?.packin_instance == null)
            return 0;

        var bounds = ConnectorUtils.ToWorldAabbRotated(room.Prefab.packin_instance.ObjectBounds, room.WorldPos, room.YawSteps);
        var width = bounds.Max.X - bounds.Min.X;
        var depth = bounds.Max.Y - bounds.Min.Y;
        return Math.Max(0, (double)width) * Math.Max(0, (double)depth);
    }
}

public class PlanScore
{
    public double Total { get; set; }
    public Dictionary<string, double> Components { get; set; } = new Dictionary<string, double>();
}
