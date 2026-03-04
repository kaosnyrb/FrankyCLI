using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Retrograde.StationDesigns;

public static class StationDesignRegistry
{
    public static IReadOnlyDictionary<string, Func<IStationDesign>> Designs { get; } =
        new ReadOnlyDictionary<string, Func<IStationDesign>>(
            new Dictionary<string, Func<IStationDesign>>
            {
                { "Hab Station", () => new HabStation() },
                { "Ore Station", () => new OreStation() },
                { "Warren Station", () => new WarrenStation() },
            });
}
