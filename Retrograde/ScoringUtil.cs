using FrankyCLI.Retrograde.Passes;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    public static class ScoringUtil
    {
        public static PlanScore ScorePlan(ScoringSystem scoringSystem, int roomsPlaced, int bridgeablePairs, int bridgingOverlapCount = 0, int newConnectors = 0)
        {
            var scoring = scoringSystem ?? new ScoringSystem
            {
                PlacementWieght = 1,
                BridgingWieght = 1,
                BridgingOverlapWieght = 0,
                NewConnectorsWieght = 0,
                NorthBiasWeight = 0.8,
                Effort = 100
            };

            var components = new Dictionary<string, double>
            {
                { "Placement", roomsPlaced * scoring.PlacementWieght },
                { "Bridging", bridgeablePairs * scoring.BridgingWieght },
                { "BridgingOverlap", bridgingOverlapCount * scoring.BridgingOverlapWieght },
                { "NewConnectors", newConnectors * scoring.NewConnectorsWieght }
            };

            return new PlanScore
            {
                Total = components.Values.Sum(),
                Components = components
            };
        }
    }

    public class PlanScore
    {
        public double Total { get; set; }
        public Dictionary<string, double> Components { get; set; } = new Dictionary<string, double>();
    }
}
