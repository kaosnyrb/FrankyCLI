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

    public ScienceOutpostDesign(string templateWorldspaceEditorId = "DR001World")
    {
        _templateWorldspaceEditorId = templateWorldspaceEditorId;

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
            new ScienceBuildingPass(),
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
