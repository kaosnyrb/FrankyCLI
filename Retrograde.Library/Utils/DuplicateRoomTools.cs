using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System.Collections.Generic;

namespace Retrograde.Utils;

/// <summary>
/// Counts how many times each room prefab (PackIn) has been placed across
/// all cells in the current mod.  Used by the scoring system to penalise
/// rooms that already appear too frequently.
/// </summary>
public static class DuplicateRoomTools
{
    /// <summary>
    /// Scans every cell in the target mod and counts temporary placed references
    /// whose Base matches each provided PackIn FormKey.
    /// </summary>
    /// <returns>
    /// A dictionary mapping PackIn FormKey → number of existing temporary
    /// placements found across all other cells in the mod.
    /// </returns>
    public static Dictionary<FormKey, int> CountPackInPlacements(IEnumerable<FormKey> packInKeys)
    {
        var counts = new Dictionary<FormKey, int>();
        var keysOfInterest = new HashSet<FormKey>();

        foreach (var key in packInKeys)
        {
            keysOfInterest.Add(key);
            if (!counts.ContainsKey(key))
                counts[key] = 0;
        }

        if (keysOfInterest.Count == 0)
            return counts;

        var mod = RetrogradeContext.Current.TargetMod;

        for (int i = 0; i < mod.Cells.Count; i++)
        {
            for (int j = 0; j < mod.Cells[i].SubBlocks.Count; j++)
            {
                for (int k = 0; k < mod.Cells[i].SubBlocks[j].Cells.Count; k++)
                {
                    var cell = mod.Cells[i].SubBlocks[j].Cells[k];
                    foreach (var placed in cell.Temporary)
                    {
                        if (placed is PlacedObject po)
                        {
                            var baseKey = po.Base.FormKey;
                            if (keysOfInterest.Contains(baseKey))
                                counts[baseKey]++;
                        }
                    }
                }
            }
        }

        return counts;
    }

    /// <summary>
    /// Counts how many times a single PackIn FormKey appears as a temporary
    /// placed reference across all cells in the mod.
    /// </summary>
    public static int CountPackInPlacement(FormKey packInKey)
    {
        int count = 0;
        var mod = RetrogradeContext.Current.TargetMod;

        for (int i = 0; i < mod.Cells.Count; i++)
        {
            for (int j = 0; j < mod.Cells[i].SubBlocks.Count; j++)
            {
                for (int k = 0; k < mod.Cells[i].SubBlocks[j].Cells.Count; k++)
                {
                    var cell = mod.Cells[i].SubBlocks[j].Cells[k];
                    foreach (var placed in cell.Temporary)
                    {
                        if (placed is PlacedObject po && po.Base.FormKey == packInKey)
                            count++;
                    }
                }
            }
        }

        return count;
    }
}
