using FrankyCLI.Retrograde;
using FrankyCLI.Retrograde.Passes;
using System;
using System.Collections.Generic;
using System.Linq;
using Noggog;

namespace FrankyCLI
{
    public static class ScoringUtil
    {
        public static PlanScore ScorePlan(ScoringSystem scoringSystem, int roomsPlaced, int bridgeablePairs, int bridgingOverlapCount = 0, int newConnectors = 0, double area = 0, double clustering = 0, double sizeDiversityPenalty = 0)
        {

            var components = new Dictionary<string, double>
            {
                { "Placement", roomsPlaced * scoringSystem.PlacementWeight },
                { "Bridging", bridgeablePairs * scoringSystem.BridgingWeight },
                { "BridgingOverlap", bridgingOverlapCount * scoringSystem.BridgingOverlapWeight },
                { "NewConnectors", newConnectors * scoringSystem.NewConnectorsWeight },
                { "Area", (area/10) * scoringSystem.AreaWeight },
                { "Clustering", (clustering/10) * scoringSystem.ClusteringWeight },
                { "SizeDiversity", sizeDiversityPenalty * scoringSystem.SizeDiversityWeight }
            };

            return new PlanScore
            {
                Total = components.Values.Sum(),
                Components = components
            };
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

            // Penalize streaks longer than 1 (two tiny rooms back-to-back starts to hurt)
            return Math.Max(0, maxStreak - 1);
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
}
