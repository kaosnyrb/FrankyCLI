using Retrograde.Nouns.SpaceCells;
using Retrograde.Passes.SpaceCell;
using System.Collections.Generic;

namespace Retrograde.SpaceCellDesigns;

/// <summary>
/// Sparse glacial crystal field using IceShardHuge* and IceBerg* statics.
/// Only places vanilla markers — no asteroid formations.
/// </summary>
public class IceCrystalsDesign : ISpaceCellDesign
{
    public SpaceCellPalette Palette => SpaceCellPalette.IceShards;
    public string DesignName => "IceCrystals";

    public List<ISpaceCellPass> Passes { get; } = new()
    {
        new SpaceMarkersPass(),
    };
}
