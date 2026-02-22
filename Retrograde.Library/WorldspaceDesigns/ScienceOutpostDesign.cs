using Retrograde.Passes.Worldspace;
using System;
using System.Collections.Generic;

namespace Retrograde.WorldspaceDesigns;

/// <summary>
/// Worldspace design for a hydroponic science outpost POI.
///
/// Unlike <see cref="FortDesign"/>, this design places the building directly from
/// <see cref="ScienceBuildingPass"/> rather than using prefab tile instantiation.
/// The building pass handles its own terrain flattening and exposes the building
/// position to subsequent passes via <c>state.FlatAreaWorldX/Y</c> and
/// <c>state.MarkerPosition</c>.
///
/// Pass order in ContentPasses is significant:
///   ScienceBuildingPass must run first so that scatter, marker, and boss passes
///   can use the building position set on state.
/// </summary>
public class ScienceOutpostDesign : IWorldspaceDesign
{
    // SmallWorld worldspaces with DNAM >= 4 that have BTD files available in the terrain folder.
    // Sourced from Starfield.esm via gen_inspect worldspace_smallworld.
    private static readonly List<string> TemplateWorldspaces = new()
    {
        // DR* dungeon POI terrain templates
        "DR001World",       // 4x4
        "DR002World",       // 5x5
        "DR009World",       // 4x4
        "DR010World",       // 5x5
        "DR011World",       // 4x4
        "DR012World",       // 4x4
        "DR015World",       // 4x4
        "DR016World",       // 4x4
        "DR017World",       // 4x4
        "DR021World",       // 4x4
        "DR022World",       // 5x5
        "DR023World",       // 4x4
        "DR025World",       // 5x5
        "DR026World",       // 4x4
        "DR027World",       // 4x4
        // OEBB* open encounter worldspaces
        "OEBB001World",     // 4x4
        "OEBB003World",     // 4x4
        "OEBB012World",     // 4x4
        "OEBB013World",     // 4x4
        "OEBB021World",     // 4x4
        "OEBB022World",     // 4x4
        "OEBB023World",     // 4x4
        "OEBB024World",     // 4x4
        "OEBB026World",     // 4x4
        "OEBB027World",     // 4x4
        "OEBB028World",     // 4x4
        "OEBB030World",     // 4x4
        "OEBB032World",     // 4x4
        "OEBB034World",     // 4x4
        // OEDB* worldspaces (varied biome terrain)
        "OEDB002World",     // 5x5
        "OEDB505World",     // 5x5
        "OEDB506World",     // 5x5
        "OEDB508World",     // 5x5
        "OEDB509World",     // 5x5
        "OEDB511World",     // 4x4
        // OEJM* worldspaces
        "OEJM006World",     // 4x4
        "OEJM008World",     // 4x4
        // OEJP* worldspaces
        "OEJP029World",     // 4x4
        "OEJP030World",     // 4x4
        "OEJP031World",     // 4x4
        "OEJP032World",     // 4x4
        "OEJP033World",     // 4x4
        // OEOB* worldspaces
        "OEOB001World",     // 5x5
        "OEOB006World",     // 5x5
        "OEOB007World",     // 5x5
        "OEOB008World",     // 5x5
        // OESD* worldspaces
        "OESD008World",     // 4x4
        "OESD009World",     // 4x4
        "OESD010World",     // 4x4
        "OESD015World",     // 5x5
        "OESD017World",     // 5x5
        "OESD018World",     // 4x4
        // OEAF* worldspaces
        "OEAF019World",     // 4x4
        "OEAF022World",     // 4x4
        "OEAF026World",     // 4x4
    };

    public List<IWorldspacePass> MapPasses { get; set; }
    public List<IWorldspacePass> CellBuildPasses { get; set; }
    public List<IWorldspacePass> ContentPasses { get; set; }

    private readonly string _templateWorldspaceEditorId;
    public string TemplateWorldspaceEditorId => _templateWorldspaceEditorId;
    public int MapSize => 50;
    public float TileWorldSize => 4f;
    public string DesignName => "ScienceOutpost";

    public string WorldspaceName { get; private set; } = string.Empty;
    public string WorldspaceEditorId { get; private set; } = string.Empty;

    public ScienceOutpostDesign(string? templateWorldspaceEditorId = null, float size = 0.2f)
    {
        _templateWorldspaceEditorId = templateWorldspaceEditorId
            ?? TemplateWorldspaces[Random.Shared.Next(TemplateWorldspaces.Count)];

        // No map passes: the science building handles its own terrain flatten.
        MapPasses = new List<IWorldspacePass>();

        // Navmesh seed per cell for NPC navigation.
        CellBuildPasses = new List<IWorldspacePass>
        {
            new NavmeshSeedPass(),
        };

        // ScienceBuildingPass must come first — it sets state.FlatAreaWorldX/Y,
        // state.TerrainHeight, and state.MarkerPosition for all passes that follow.
        ContentPasses = new List<IWorldspacePass>
        {
            new LodLayerPass(),
            new ScienceBuildingPass(size: size),
            new RockScatterPass(0.3f),
            new VegetationScatterPass(0.3f),
            new MapMarkerPass(MapMarkerPass.MarkerType.ResearchBase),
            new TravelMarkerPass(),
            new PlanetContentManagerPass("sc_blockbranch", "sc_blockcontent"),
            new PlanetScanPass("sc_scanbranch", "sc_scancontent"),
            new PlanetQuestPass("sc_questbranch", "sc_questcontent"),
            new WorldspaceBossPass(),
        };
    }

    public string GeneratePOIName(int seed)
    {
        WorldspaceName = ScienceOutpostNameGenerator.GetRandomPOIName(seed);
        WorldspaceEditorId = WorldspaceName.ToLowerInvariant().Replace(" ", "");
        return WorldspaceName;
    }
}

internal static class ScienceOutpostNameGenerator
{
    private static readonly List<string> Adjectives = new()
    {
        "Abandoned", "Derelict", "Isolated", "Remote", "Forgotten",
        "Deserted", "Concealed", "Hidden", "Classified", "Secured",
        "Contaminated", "Quarantined", "Sealed", "Restricted", "Compromised",
        "Experimental", "Advanced", "Deep", "Outlying", "Silent",
        "Collapsed", "Ruined", "Dormant", "Darkened", "Lost",
        "Archaic", "Subterranean", "Outer", "Peripheral", "Clandestine",
    };

    private static readonly List<string> Nouns = new()
    {
        "Research Station", "Science Outpost", "Lab Complex", "Survey Site",
        "Analysis Post", "Observation Post", "Xenobiology Lab", "Field Station",
        "Data Relay", "Genetics Lab", "Terraforming Post", "Botanical Station",
        "Atmospheric Post", "Geological Survey", "Research Facility",
        "Science Base", "Xenoscience Post", "Virology Station", "Ecology Post",
        "Biochemical Lab", "Study Site", "Monitoring Station", "Field Lab",
        "Exobiology Post", "Research Post", "Science Camp", "Analysis Site",
    };

    public static string GetRandomPOIName(int seed)
    {
        var rand = new Random(seed);
        return Adjectives[rand.Next(Adjectives.Count)] + " " + Nouns[rand.Next(Nouns.Count)];
    }
}
