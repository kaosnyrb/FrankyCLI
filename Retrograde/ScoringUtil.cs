using FrankyCLI.Retrograde;
using FrankyCLI.Retrograde.Passes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    public static class ScoringUtil
    {
        public static PlanScore ScorePlan(ScoringSystem scoringSystem, int roomsPlaced, int bridgeablePairs, int bridgingOverlapCount = 0, int newConnectors = 0, double area = 0)
        {
            var scoring = scoringSystem ?? new ScoringSystem
            {
                PlacementWeight = 1,
                BridgingWeight = 1,
                BridgingOverlapWeight = 0,
                NewConnectorsWeight = 0,
                AreaWeight = 0,
                NorthBiasWeight = 0.8,
                Effort = 100
            };

            var components = new Dictionary<string, double>
            {
                { "Placement", roomsPlaced * scoring.PlacementWeight },
                { "Bridging", bridgeablePairs * scoring.BridgingWeight },
                { "BridgingOverlap", bridgingOverlapCount * scoring.BridgingOverlapWeight },
                { "NewConnectors", newConnectors * scoring.NewConnectorsWeight },
                { "Area", (area/10) * scoring.AreaWeight }
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
    }

    public class PlanScore
    {
        public double Total { get; set; }
        public Dictionary<string, double> Components { get; set; } = new Dictionary<string, double>();
    }
}
