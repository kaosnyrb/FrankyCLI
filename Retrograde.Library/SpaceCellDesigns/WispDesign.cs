using Retrograde.Nouns.SpaceCells;
using Retrograde.Passes.SpaceCell;
using System.Collections.Generic;

namespace Retrograde.SpaceCellDesigns;

/// <summary>
/// Wisp field using CorallineWispLarge* statics.
/// Pass set: chain only.
/// </summary>
public class WispDesign : ISpaceCellDesign
{
    public SpaceCellPalette Palette => SpaceCellPalette.Wisp;
    public string DesignName => "Wisp";

    public List<ISpaceCellPass> Passes { get; } = new()
    {
        new SpaceMarkersPass(),
        new AsteroidChainPass(),
    };
}
