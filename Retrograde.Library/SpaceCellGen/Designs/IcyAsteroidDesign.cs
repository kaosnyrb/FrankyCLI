using Retrograde.Nouns.SpaceCells;
using Retrograde.Passes.SpaceCell;
using System.Collections.Generic;

namespace Retrograde.SpaceCellDesigns;

/// <summary>
/// Icy asteroid field using MS_IceAsteroid* variants.
/// Default pass set: markers, chain, comet, wreck, ring, crescent.
/// </summary>
public class IcyAsteroidDesign : ISpaceCellDesign
{
    public SpaceCellPalette Palette => SpaceCellPalette.Icy;
    public string DesignName => "Icy";

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
