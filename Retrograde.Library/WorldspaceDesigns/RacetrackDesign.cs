using Retrograde.Passes.Worldspace;
using System;
using System.Collections.Generic;

namespace Retrograde.WorldspaceDesigns;

/// <summary>
/// Worldspace design for a racetrack POI: a closed oval loop course laid out
/// around a central elevated terrain mound.
///
/// Layout (handled by RacetrackLayoutPass):
///  - Outer ellipse defines the outer edge of the driveable track ring.
///  - Inner island (inside the inner ellipse) is left as natural terrain so the
///    existing rock and vegetation scatter passes can populate it.
///  - Pit-lane start/finish structures sit on the southern straight.
///  - Barrier addon tiles line the inner track edge.
///
/// PackIn tile keys use fort-kit prefabs as stand-ins; swap search strings in
/// RacetrackPackInIds() once dedicated racetrack PackIn assets exist:
///  rt_surface → "to_pkn_landing_"   flat landing-pad panels as track surface
///  rt_box     → "to_pkn_single"     pit box / start-finish structures
///  rt_barrier → "to_pkn_wall_"      inner-edge barrier addons
/// </summary>
public class RacetrackDesign : IWorldspaceDesign
{
    // Overlay worldspaces with usable 4×4 or 5×5 terrain BTDs.
    // These give a large-enough editable cell region for the oval to fit.
    private static readonly List<string> TemplateWorldspaces = new()
    {
        "DR001World",    // 4x4
        "DR009World",    // 4x4
        "DR011World",    // 4x4
        "DR012World",    // 4x4
        "OEBB001World",  // 4x4
        "OEBB003World",  // 4x4
        "OEBB012World",  // 4x4
        "OEBB013World",  // 4x4
        "OEDB508World",  // 5x5
        "OEDB509World",  // 5x5
        "OEOB008World",  // 5x5
        "OESD008World",  // 4x4
        "OESD009World",  // 4x4
    };

    public List<IWorldspacePass> MapPasses { get; set; }
    public List<IWorldspacePass> CellBuildPasses { get; set; }
    public List<IWorldspacePass> ContentPasses { get; set; }

    private readonly string _templateWorldspaceEditorId;
    public string TemplateWorldspaceEditorId => _templateWorldspaceEditorId;

    public int MapSize => 50;
    public float TileWorldSize => 4f;
    public string DesignName => "Racetrack";

    public string WorldspaceName { get; private set; } = string.Empty;
    public string WorldspaceEditorId { get; private set; } = string.Empty;

    /// <param name="templateWorldspaceEditorId">
    /// Overlay worldspace to copy terrain from. Null picks one at random from
    /// the built-in list.
    /// </param>
    /// <param name="trackScale">
    /// Scales the oval radii (0.4–1.0). 1.0 fills the available cells;
    /// smaller values leave more open terrain around the track.
    /// </param>
    public RacetrackDesign(string? templateWorldspaceEditorId = null, float trackScale = 0.85f)
    {
        _templateWorldspaceEditorId = templateWorldspaceEditorId
            ?? TemplateWorldspaces[Random.Shared.Next(TemplateWorldspaces.Count)];

        // No terrain flatten: the oval sits on the natural terrain so the
        // central mound is preserved. TerrainFlattenPass and TerrainRestorePass
        // can be added here once a ring-shaped flatten is implemented.
        MapPasses = new List<IWorldspacePass>
        {
            new PackInLibraryPass(RacetrackPackInIds()),
            new RacetrackLayoutPass(trackScale),
        };

        CellBuildPasses = new List<IWorldspacePass>
        {
            new NavmeshSeedPass(),
            new TileInstantiationPass(),
        };

        ContentPasses = new List<IWorldspacePass>
        {
            new LodLayerPass(),
            // Lower density: the track ring already fills most of the map space.
            new RockScatterPass(0.35f),
            new VegetationScatterPass(0.35f),
            new MapMarkerPass(MapMarkerPass.MarkerType.Landmark),
            new TravelMarkerPass(),
            new PlanetContentManagerPass("rt_blockbranch", "rt_blockcontent"),
            new PlanetScanPass("rt_scanbranch", "rt_scancontent"),
            new PlanetQuestPass("rt_questbranch", "rt_questcontent"),
            new WorldspaceBossPass(),
        };
    }

    public string GeneratePOIName(int seed)
    {
        WorldspaceName = RacetrackNameGenerator.GetRandomPOIName(seed);
        WorldspaceEditorId = WorldspaceName.ToLowerInvariant().Replace(" ", "");
        return WorldspaceName;
    }

    /// <summary>
    /// Maps internal tile keys to PackIn EditorID search strings.
    /// PackInLibraryPass will find all PackIns whose EditorID contains the search string.
    /// </summary>
    internal static Dictionary<string, string> RacetrackPackInIds() => new()
    {
        { "rt_surface", "to_pkn_landing_" },  // flat panels as track surface
        { "rt_box",     "to_pkn_single" },    // pit box / start-finish structures
        { "rt_barrier", "to_pkn_wall_" },     // inner-edge barrier (addon)
    };
}

/// <summary>
/// Generates random racetrack POI names from speed/danger adjectives and venue nouns.
/// </summary>
internal static class RacetrackNameGenerator
{
    private static readonly List<string> Adjectives = new()
    {
        "Abandoned", "Ashen", "Binary", "Burning", "Crimson",
        "Desolate", "Dust", "Forsaken", "Frozen", "Hazard",
        "High-Speed", "Iron", "Lost", "Meteor", "Midnight",
        "Obsidian", "Orbital", "Ruined", "Savage", "Silent",
        "Solar", "Storm", "Thunder", "Twilight", "Void",
        "Wasteland", "Zero-G",
    };

    private static readonly List<string> Nouns = new()
    {
        "Arena", "Basin", "Bowl", "Circuit", "Coliseum",
        "Course", "Crucible", "Derby", "Drag Strip", "Gauntlet",
        "Grand Prix", "Loop", "Oval", "Raceway", "Ring",
        "Run", "Speedway", "Sprint Track", "Trial", "Track",
    };

    public static string GetRandomPOIName(int seed)
    {
        var rand = new Random(seed);
        return Adjectives[rand.Next(Adjectives.Count)] + " " + Nouns[rand.Next(Nouns.Count)];
    }
}
