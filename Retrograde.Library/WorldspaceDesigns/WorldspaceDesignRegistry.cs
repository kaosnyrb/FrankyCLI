using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Retrograde.WorldspaceDesigns;

public static class WorldspaceDesignRegistry
{
    public static IReadOnlyDictionary<string, Func<IWorldspaceDesign>> Designs { get; } =
        new ReadOnlyDictionary<string, Func<IWorldspaceDesign>>(
            new Dictionary<string, Func<IWorldspaceDesign>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Fort", () => new FortDesign() },
                { "ScienceOutpost", () => new ScienceOutpostDesign() },
                { "Racetrack", () => new RacetrackDesign() },
                { "SmallIndustryBase", () => new SmallIndustryBaseDesign() },
            });
}
