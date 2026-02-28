using Retrograde.Nouns.SpaceCells;
using Retrograde.Passes.SpaceCell;
using System.Collections.Generic;

namespace Retrograde.SpaceCellDesigns;

/// <summary>
/// Dark rocky asteroid field using MS_RockAsteroidRough* variants.
/// Default pass set: markers, chain, comet, wreck, ring, crescent.
/// </summary>
public class RockyAsteroidDesign : ISpaceCellDesign
{
    public SpaceCellPalette Palette => SpaceCellPalette.Rocky;
    public string DesignName => "Rocky";

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
