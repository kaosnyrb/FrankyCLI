using Mutagen.Bethesda.Plugins;
using Noggog;
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
    /// Precomputed metrics for the current set of placed rooms, enabling O(n) or O(1)
    /// delta scoring per candidate instead of full O(n²) recomputation.
    /// </summary>
    public class PlacementBaseline
    {
        public int RoomCount;
        public double Area;
        public double SumMinDists;
        public double[] PerRoomMinDist;
        public P3Float[] RoomPositions;
        public int MaxSmallStreak;
        public int EndSmallStreak;
        public Dictionary<string, int> PrefabCounts;
        public double RoomReuseScore;
        public List<RgAabb> RoomBounds;
    }

    public static PlacementBaseline ComputePlacementBaseline(IReadOnlyList<PlacedRoom> rooms)
    {
        int n = rooms?.Count ?? 0;
        var baseline = new PlacementBaseline
        {
            RoomCount = n,
            PerRoomMinDist = new double[n],
            RoomPositions = new P3Float[n],
            PrefabCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            RoomBounds = new List<RgAabb>(n)
        };

        if (n == 0)
            return baseline;

        // Cache positions and bounds
        for (int i = 0; i < n; i++)
        {
            baseline.RoomPositions[i] = rooms[i].WorldPos;
            if (rooms[i].Prefab?.packin_instance != null)
                baseline.RoomBounds.Add(ConnectorUtils.ToWorldAabbRotated(
                    rooms[i].Prefab.packin_instance.ObjectBounds, rooms[i].WorldPos, rooms[i].YawSteps));
        }

        // Area
        double totalArea = 0;
        foreach (var room in rooms)
        {
            totalArea += GetRoomFootprintArea(room);
        }
        baseline.Area = totalArea;

        // Clustering: per-room min distances (O(n²) once, then O(n) per candidate)
        double sumMinDists = 0;
        if (n >= 2)
        {
            for (int i = 0; i < n; i++)
            {
                double minDist = double.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    double d = Math.Sqrt(MathUtil.DistanceSquared(baseline.RoomPositions[i], baseline.RoomPositions[j]));
                    if (d < minDist) minDist = d;
                }
                baseline.PerRoomMinDist[i] = minDist < double.MaxValue ? minDist : 0;
                sumMinDists += baseline.PerRoomMinDist[i];
            }
        }
        baseline.SumMinDists = sumMinDists;

        // Small chain penalty
        int maxStreak = 0, currentStreak = 0;
        foreach (var room in rooms)
        {
            double area = GetRoomFootprintArea(room);
            if (area > 0 && area <= 200)
            {
                currentStreak++;
                if (currentStreak > maxStreak) maxStreak = currentStreak;
            }
            else
            {
                currentStreak = 0;
            }
        }
        baseline.MaxSmallStreak = maxStreak;
        baseline.EndSmallStreak = currentStreak;

        // Room reuse
        double reuseScore = 0;
        foreach (var room in rooms)
        {
            var id = room.Prefab?.PrefabEditorId;
            if (string.IsNullOrWhiteSpace(id)) continue;
            baseline.PrefabCounts[id] = baseline.PrefabCounts.TryGetValue(id, out var c) ? c + 1 : 1;
        }
        foreach (var kvp in baseline.PrefabCounts)
        {
            if (kvp.Value > 1) reuseScore += kvp.Value - 1;
        }
        baseline.RoomReuseScore = reuseScore;

        return baseline;
    }

    /// <summary>
    /// Scores the baseline state (no candidate added) using precomputed metrics.
    /// Used as the "do nothing" comparison threshold.
    /// </summary>
    public static double ScoreBaseline(
        ScoringSystem scoring,
        PlacementBaseline baseline,
        int bridgeScore,
        int newConnectorCount,
        IReadOnlyList<OpenConnector> openConnectors)
    {
        double clustering = baseline.RoomCount >= 2
            ? baseline.SumMinDists / baseline.RoomCount
            : 0;
        double smallChain = Math.Max(0, baseline.MaxSmallStreak - 1);
        double viability = CalculateConnectorViabilityFromBounds(
            baseline.RoomBounds, null, openConnectors);

        return (bridgeScore * scoring.BridgingWeight)
             + (newConnectorCount * scoring.NewConnectorsWeight)
             + ((baseline.Area / 10) * scoring.AreaWeight)
             + ((clustering / 10) * scoring.ClusteringWeight)
             + (smallChain * scoring.SizeDiversityWeight)
             + (baseline.RoomReuseScore * scoring.RoomReuseWeight)
             + (viability * scoring.ConnectorViabilityWeight);
    }

    /// <summary>
    /// Delta-scored placement candidate. Computes metrics incrementally from the baseline:
    /// clustering O(n), area/reuse/chain O(1), viability uses cached room bounds.
    /// </summary>
    public static double ScorePlacementCandidate(
        ScoringSystem scoring,
        PlacementBaseline baseline,
        PlacedRoom candidateRoom,
        int bridgeScore,
        int newConnectorCount,
        IReadOnlyList<OpenConnector> connectorsAfterPlacement)
    {
        int n = baseline.RoomCount;

        // Area: O(1) delta
        double candidateArea = GetRoomFootprintArea(candidateRoom);
        double totalArea = baseline.Area + candidateArea;

        // Clustering: O(n) delta instead of O(n²)
        double clustering;
        if (n == 0)
        {
            clustering = 0;
        }
        else
        {
            double deltaSum = 0;
            double candidateMinDist = double.MaxValue;
            for (int i = 0; i < n; i++)
            {
                double dist = Math.Sqrt(MathUtil.DistanceSquared(
                    baseline.RoomPositions[i], candidateRoom.WorldPos));
                // Existing room might now have a closer neighbor
                if (dist < baseline.PerRoomMinDist[i])
                    deltaSum += dist - baseline.PerRoomMinDist[i];
                // Track candidate's nearest neighbor
                if (dist < candidateMinDist)
                    candidateMinDist = dist;
            }
            if (candidateMinDist == double.MaxValue) candidateMinDist = 0;
            double newSumMinDists = baseline.SumMinDists + deltaSum + candidateMinDist;
            clustering = newSumMinDists / (n + 1);
        }

        // Small chain: O(1) delta
        int newMaxStreak;
        if (candidateArea > 0 && candidateArea <= 200)
        {
            int newEndStreak = baseline.EndSmallStreak + 1;
            newMaxStreak = Math.Max(baseline.MaxSmallStreak, newEndStreak);
        }
        else
        {
            newMaxStreak = baseline.MaxSmallStreak;
        }
        double smallChain = Math.Max(0, newMaxStreak - 1);

        // Room reuse: O(1) delta
        double roomReuse = baseline.RoomReuseScore;
        var prefabId = candidateRoom.Prefab?.PrefabEditorId;
        if (!string.IsNullOrWhiteSpace(prefabId)
            && baseline.PrefabCounts.TryGetValue(prefabId, out var count)
            && count >= 1)
        {
            roomReuse += 1;
        }

        // Viability: uses cached room bounds, avoids recomputing AABBs
        RgAabb? candidateAabb = null;
        if (candidateRoom.Prefab?.packin_instance != null)
            candidateAabb = ConnectorUtils.ToWorldAabbRotated(
                candidateRoom.Prefab.packin_instance.ObjectBounds,
                candidateRoom.WorldPos, candidateRoom.YawSteps);
        double viability = CalculateConnectorViabilityFromBounds(
            baseline.RoomBounds, candidateAabb, connectorsAfterPlacement);

        return (bridgeScore * scoring.BridgingWeight)
             + (newConnectorCount * scoring.NewConnectorsWeight)
             + ((totalArea / 10) * scoring.AreaWeight)
             + ((clustering / 10) * scoring.ClusteringWeight)
             + (smallChain * scoring.SizeDiversityWeight)
             + (roomReuse * scoring.RoomReuseWeight)
             + (viability * scoring.ConnectorViabilityWeight);
    }

    /// <summary>
    /// Connector viability using pre-cached room bounds plus an optional extra AABB (the candidate).
    /// Avoids rebuilding the room bounds list and recomputing ToWorldAabbRotated per candidate.
    /// </summary>
    private static double CalculateConnectorViabilityFromBounds(
        IReadOnlyList<RgAabb> roomBounds,
        RgAabb? extraBounds,
        IReadOnlyList<OpenConnector> openConnectors,
        double defaultDepth = 20,
        double apertureWidth = 1)
    {
        if (openConnectors == null || openConnectors.Count == 0)
            return 0;

        double totalArea = 0;
        foreach (var open in openConnectors)
        {
            var dir = open.Parsed.Direction;
            double minClearance = defaultDepth;

            for (int i = 0; i < roomBounds.Count; i++)
            {
                UpdateClearance(ref minClearance, open.WorldPos, dir, roomBounds[i]);
            }
            if (extraBounds.HasValue)
            {
                UpdateClearance(ref minClearance, open.WorldPos, dir, extraBounds.Value);
            }

            totalArea += Math.Max(0, minClearance) * apertureWidth;
        }

        return totalArea;
    }

    private static void UpdateClearance(ref double minClearance, P3Float connPos, ConnectorDirection dir, RgAabb aabb)
    {
        switch (dir)
        {
            case ConnectorDirection.North:
                if (connPos.X < aabb.Min.X || connPos.X > aabb.Max.X) break;
                if (connPos.Z < aabb.Min.Z || connPos.Z > aabb.Max.Z) break;
                if (aabb.Min.Y > connPos.Y)
                    minClearance = Math.Min(minClearance, aabb.Min.Y - connPos.Y);
                break;
            case ConnectorDirection.South:
                if (connPos.X < aabb.Min.X || connPos.X > aabb.Max.X) break;
                if (connPos.Z < aabb.Min.Z || connPos.Z > aabb.Max.Z) break;
                if (aabb.Max.Y < connPos.Y)
                    minClearance = Math.Min(minClearance, connPos.Y - aabb.Max.Y);
                break;
            case ConnectorDirection.East:
                if (connPos.Y < aabb.Min.Y || connPos.Y > aabb.Max.Y) break;
                if (connPos.Z < aabb.Min.Z || connPos.Z > aabb.Max.Z) break;
                if (aabb.Min.X > connPos.X)
                    minClearance = Math.Min(minClearance, aabb.Min.X - connPos.X);
                break;
            case ConnectorDirection.West:
                if (connPos.Y < aabb.Min.Y || connPos.Y > aabb.Max.Y) break;
                if (connPos.Z < aabb.Min.Z || connPos.Z > aabb.Max.Z) break;
                if (aabb.Max.X < connPos.X)
                    minClearance = Math.Min(minClearance, connPos.X - aabb.Max.X);
                break;
        }
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
