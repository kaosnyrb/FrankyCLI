using System;
using System.Collections.Generic;

namespace Retrograde.WorldspaceDesigns;

public static class WorldspaceDesignRegistry
{
    public static IReadOnlyDictionary<string, Func<IWorldspaceDesign>> Designs { get; } =
        new Dictionary<string, Func<IWorldspaceDesign>>(StringComparer.OrdinalIgnoreCase)
        {
            { "Fort", () => new FortDesign() },
            { "ScienceOutpost", () => new ScienceOutpostDesign() },
            { "Racetrack", () => new RacetrackDesign() },
            { "SmallIndustryBase", () => new SmallIndustryBaseDesign(SmallIndustryBaseConfig.Default) },
        };
}
