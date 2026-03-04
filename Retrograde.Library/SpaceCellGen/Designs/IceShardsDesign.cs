using Retrograde.Nouns.SpaceCells;
using Retrograde.Passes.SpaceCell;
using System.Collections.Generic;

namespace Retrograde.SpaceCellDesigns;

/// <summary>
/// Glacial shard field using IceShardHuge* and IceBerg* statics.
/// Default pass set: markers, chain, comet, wreck, ring, crescent.
/// </summary>
public class IceShardsDesign : ISpaceCellDesign
{
    public SpaceCellPalette Palette => SpaceCellPalette.IceShards;
    public string DesignName => "IceShards";

    public List<ISpaceCellPass> Passes { get; } = new()
    {
        new SpaceMarkersPass(),
        new AsteroidChainPass(),
        new CometTailPass(),
        new ShipWreckPass(),
        new LargeAsteroidRingPass(),
        new CrescentBeltPass(),
    };
}
