using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde.Abstractions;
using System.Collections.Generic;

namespace FrankyCLI;

/// <summary>
/// Implementation of IModContext that wraps gen_quest_main static properties.
/// This bridges the existing FrankyCLI code with the Retrograde library.
/// </summary>
public class ModContextImpl : IModContext
{
    /// <summary>
    /// Static list of template mods discovered from the load order.
    /// Set by gen_*.cs entry points before initializing RetrogradeContext.
    /// </summary>
    public static List<IStarfieldModGetter> TemplateModsList { get; set; } = new();

    public ModKey StarfieldModKey => gen_quest_main.StarfieldModKey;
    public IStarfieldModGetter StarfieldMod => gen_quest_main._StarfieldMod;
    public StarfieldMod TargetMod => gen_quest_main.myMod;
    public IReadOnlyList<IStarfieldModGetter> TemplateMods => TemplateModsList;
}
