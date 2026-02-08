using Mutagen.Bethesda.Starfield;
using System;

namespace Retrograde;

/// <summary>
/// Utility methods for Retrograde that depend on gen_quest_main.
/// These stay in FrankyCLI rather than the library.
/// </summary>
public static class RetrogradeUtils
{
    /// <summary>
    /// Clones a cell by its EditorID from the target mod.
    /// </summary>
    public static Cell CloneCellById(string editorId)
    {
        var mod = FrankyCLI.gen_quest_main.myMod;

        for (int i = 0; i < mod.Cells.Count; i++)
        {
            for (int j = 0; j < mod.Cells[i].SubBlocks.Count; j++)
            {
                for (int k = 0; k < mod.Cells[i].SubBlocks[j].Cells.Count; k++)
                {
                    var cell = mod.Cells[i].SubBlocks[j].Cells[k];
                    if (cell.EditorID == editorId)
                    {
                        return cell.DeepCopy();
                    }
                }
            }
        }

        throw new Exception($"Cell not found: {editorId}");
    }
}
