using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;

namespace Retrograde.Abstractions;

/// <summary>
/// Provides access to both the base game data (Starfield.esm) and the mod being generated.
/// This abstraction allows the Retrograde library to be used without depending on
/// a specific mod context implementation.
/// </summary>
public interface IModContext
{
    /// <summary>
    /// The ModKey for Starfield.esm, used for creating FormKey references to base game records.
    /// </summary>
    ModKey StarfieldModKey { get; }

    /// <summary>
    /// Read-only access to the base game (Starfield.esm) for looking up activators, factions, etc.
    /// </summary>
    IStarfieldModGetter StarfieldMod { get; }

    /// <summary>
    /// The writable mod being generated. Used for creating new records and accessing
    /// collections like Cells, PackIns, FormLists, etc.
    /// </summary>
    StarfieldMod TargetMod { get; }
}
