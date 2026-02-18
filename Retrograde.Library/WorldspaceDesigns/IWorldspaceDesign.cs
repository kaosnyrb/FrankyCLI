using Retrograde.Passes;
using System.Collections.Generic;

namespace Retrograde.WorldspaceDesigns;

public interface IWorldspaceDesign
{
    /// <summary>
    /// Passes that run once to build the 2D tile map (layout generation).
    /// These operate on WorldspaceState.Map before any cells are populated.
    /// </summary>
    List<IWorldspacePass> MapPasses { get; set; }

    /// <summary>
    /// Passes that run per-cell to convert map tiles into placed objects.
    /// </summary>
    List<IWorldspacePass> CellBuildPasses { get; set; }

    /// <summary>
    /// Content passes that run after all cells are built (enemies, loot, triggers).
    /// </summary>
    List<IWorldspacePass> ContentPasses { get; set; }

    /// <summary>
    /// Template worldspace EditorID to clone from the TargetMod.
    /// </summary>
    string TemplateWorldspaceEditorId { get; }

    /// <summary>
    /// Template SurfaceBlock EditorID for terrain overlay.
    /// </summary>
    string TemplateSurfaceBlockEditorId { get; }

    /// <summary>
    /// Tile grid size (square). StarTiller uses 50.
    /// </summary>
    int MapSize { get; }

    /// <summary>
    /// World units per tile. StarTiller uses 4 (blocksize).
    /// </summary>
    float TileWorldSize { get; }

    /// <summary>
    /// Cell grid dimensions for the SurfaceBlock DNAM (square).
    /// Determines how many cells the worldspace terrain has.
    /// stbblock001 uses 4 (4x4 cells).
    /// </summary>
    int CellGridSize { get; }

    string GeneratePOIName(int seed);
    string DesignName { get; }
}
