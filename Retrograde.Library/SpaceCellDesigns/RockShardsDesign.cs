using Retrograde.Nouns.SpaceCells;
using Retrograde.Passes.SpaceCell;
using System.Collections.Generic;

namespace Retrograde.SpaceCellDesigns;

/// <summary>
/// Rocky shard field using RockShardHuge* statics.
/// Default pass set: markers, chain, comet, wreck, ring, crescent.
/// </summary>
public class RockShardsDesign : ISpaceCellDesign
{
    public SpaceCellPalette Palette => SpaceCellPalette.RockShards;
    public string DesignName => "RockShards";

    public List<ISpaceCellPass> Passes { get; } = new()
    {
        new SpaceMarkersPass(),
        new AsteroidChainPass(),
        new IceCrystalsPass(yawOffsetDegrees: 90f),
        new CrescentBeltPass(),
    };
}
