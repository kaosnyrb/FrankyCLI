using Mutagen.Bethesda.Plugins;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Passes.WorldspacePasses;

/// <summary>
/// First map pass. Scans TargetMod.PackIns by EditorID wildcard
/// to populate state.PackInLibrary with variant lists.
/// </summary>
public class PackInLibraryPass : IWorldspacePass
{
    private readonly Dictionary<string, string> _packinIds;

    /// <param name="packinIds">
    /// Map of library key → EditorID search string.
    /// e.g. { "to_pkn_base" → "to_pkn_base", "to_pkn_wall_" → "to_pkn_wall_" }
    /// </param>
    public PackInLibraryPass(Dictionary<string, string> packinIds)
    {
        _packinIds = packinIds;
    }

    public void RunPass(WorldspaceState state)
    {
        var templateMods = RetrogradeContext.Current.TemplateMods;
        foreach (var (key, searchId) in _packinIds)
        {
            state.PackInLibrary[key] = templateMods
                .SelectMany(m => m.PackIns)
                .Where(p => p.EditorID != null && p.EditorID.Contains(searchId))
                .Select(p => p.FormKey)
                .ToList();
        }
    }
}
