using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde.Abstractions;

namespace FrankyCLI;

/// <summary>
/// Implementation of IModContext that wraps gen_quest_main static properties.
/// This bridges the existing FrankyCLI code with the Retrograde library.
/// </summary>
public class ModContextImpl : IModContext
{
    public ModKey StarfieldModKey => gen_quest_main.StarfieldModKey;
    public IStarfieldModGetter StarfieldMod => gen_quest_main._StarfieldMod;
    public StarfieldMod TargetMod => gen_quest_main.myMod;
}
