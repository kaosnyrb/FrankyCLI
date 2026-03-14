using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Starfield;
using Retrograde.Abstractions;
using Retrograde.Utils;
using System.Collections.Generic;
using System.IO;

namespace FrankyCLI;

/// <summary>
/// Implementation of IModContext that wraps gen_quest_main static properties.
/// This bridges the existing FrankyCLI code with the Retrograde library.
/// </summary>
public class ModContextImpl : IModContext
{
    /// <summary>
    /// Static list of template mods discovered from the load order.
    /// Populated by <see cref="DiscoverTemplateMods"/>.
    /// </summary>
    public static List<IStarfieldModGetter> TemplateModsList { get; set; } = [];

    public ModKey StarfieldModKey => gen_quest_main.StarfieldModKey;
    public IStarfieldModGetter StarfieldMod => gen_quest_main._StarfieldMod;
    public StarfieldMod TargetMod => gen_quest_main.myMod;
    public IReadOnlyList<IStarfieldModGetter> TemplateMods => TemplateModsList;

    /// <summary>
    /// Discovers template mods from the load order (Starfield.esm + any mod with "template"
    /// in its filename), populates <see cref="TemplateModsList"/>, and initialises the
    /// FormKeyLookup disk cache so subsequent runs skip the full ESM scan.
    /// Call this once per command entry point after setting gen_quest_main statics.
    /// </summary>
    public static void DiscoverTemplateMods(
        ILoadOrderGetter<IModListingGetter<IStarfieldModGetter>> loadOrder,
        string datapath)
    {
        TemplateModsList = [];
        List<string> esmPaths = [];

        foreach (var listing in loadOrder.ListedOrder)
        {
            var fileName = listing.FileName.ToString();
            if (listing.Mod != null &&
                (fileName.Contains("template", StringComparison.OrdinalIgnoreCase)
                 || fileName.Equals("Starfield.esm", StringComparison.OrdinalIgnoreCase))
                && fileName.EndsWith(".esm", StringComparison.OrdinalIgnoreCase))
            {
                TemplateModsList.Add(listing.Mod);
                esmPaths.Add(Path.Combine(datapath, fileName));
                Console.WriteLine($"Template mod found: {listing.FileName}");
            }
        }

        FormKeyLookup.InitializeCache(datapath, esmPaths);
    }
}
