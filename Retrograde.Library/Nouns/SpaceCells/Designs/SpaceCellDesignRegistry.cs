using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Retrograde.SpaceCellDesigns;

public enum SpaceCellDesignType
{
    Rocky,
    Icy,
    IceShards,
    IceCrystals,
    RockShards,
    Wisp,
}

public static class SpaceCellDesignRegistry
{
    public static IReadOnlyDictionary<SpaceCellDesignType, Func<ISpaceCellDesign>> Designs { get; } =
        new ReadOnlyDictionary<SpaceCellDesignType, Func<ISpaceCellDesign>>(
            new Dictionary<SpaceCellDesignType, Func<ISpaceCellDesign>>
            {
                { SpaceCellDesignType.Rocky,     () => new RockyAsteroidDesign() },
                { SpaceCellDesignType.Icy,       () => new IcyAsteroidDesign()   },
                { SpaceCellDesignType.IceShards,   () => new IceShardsDesign()    },
                { SpaceCellDesignType.IceCrystals, () => new IceCrystalsDesign() },
                { SpaceCellDesignType.RockShards,  () => new RockShardsDesign()  },
                { SpaceCellDesignType.Wisp,        () => new WispDesign()        },
            });
}
