using Retrograde.Nouns.SpaceCells;
using Retrograde.Passes.SpaceCell;
using System.Collections.Generic;

namespace Retrograde.SpaceCellDesigns;

/// <summary>
/// Pairs an asteroid palette with the ordered set of passes that build the space cell.
/// Analogous to IWorldspaceDesign for dungeon worldspaces.
/// </summary>
public interface ISpaceCellDesign
{
    /// <summary>Asteroid FormID palette to use for this design.</summary>
    SpaceCellPalette Palette { get; }

    /// <summary>Passes executed in order to populate the space cell.</summary>
    List<ISpaceCellPass> Passes { get; }

    /// <summary>Unique human-readable name for this design.</summary>
    string DesignName { get; }
}
