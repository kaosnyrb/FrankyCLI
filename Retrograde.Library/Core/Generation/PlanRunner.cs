using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;

namespace Retrograde;

public class PlanOutcome<TMeta>
{
    public double Score { get; set; }
    public int AttemptIndex { get; set; }
    public List<PlacedRoom>? Rooms { get; set; }
    public List<OpenConnector>? OpenConnectors { get; set; }
    public List<PlacedObject>? Placements { get; set; }
    public float YMin { get; set; }
    public TMeta? Metadata { get; set; }
}

public static class PlanRunner
{
    public static PlanOutcome<TMeta>? RunBest<TMeta>(int maxPlans, Func<int, PlanOutcome<TMeta>?> attempt)
    {
        PlanOutcome<TMeta>? best = null;

        for (int planAttempt = 0; planAttempt < maxPlans; planAttempt++)
        {
            var result = attempt(planAttempt);
            if (result == null)
                continue;

            result.AttemptIndex = planAttempt;

            if (best == null || result.Score > best.Score)
            {
                best = result;
            }
        }

        return best;
    }
}
