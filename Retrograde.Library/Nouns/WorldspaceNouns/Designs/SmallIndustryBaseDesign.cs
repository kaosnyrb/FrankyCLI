using Retrograde.Passes.Worldspace;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.WorldspaceDesigns;

public class SmallIndustryBaseDesign : IWorldspaceDesign
{
    public List<IWorldspacePass> MapPasses { get; set; }
    public List<IWorldspacePass> CellBuildPasses { get; set; }
    public List<IWorldspacePass> ContentPasses { get; set; }

    private readonly string? _templateWorldspaceEditorId;
    public string TemplateWorldspaceEditorId => _templateWorldspaceEditorId
        ?? RetrogradeContext.Current.TemplateMods
            .SelectMany(m => m.Worldspaces)
            .FirstOrDefault(w => w.EditorID?.StartsWith("tpl", StringComparison.OrdinalIgnoreCase) == true)
            ?.EditorID
        ?? "DR001World";
    public int MapSize => 50;
    public float TileWorldSize => 4f;
    public string DesignName => "SmallIndustryBase";

    public string WorldspaceName { get; private set; } = string.Empty;
    public string WorldspaceEditorId { get; private set; } = string.Empty;

    public SmallIndustryBaseDesign(string? templateWorldspaceEditorId = null, float scale = 1.0f)
    {
        _templateWorldspaceEditorId = templateWorldspaceEditorId;

        MapPasses = new List<IWorldspacePass>
        {
            new IndustryPackInLibraryPass(),
            //new TerrainFlattenPass(),
            new IndustryLayoutPass(scale),
            new IndustryGroundFlattenPass(),
            //new TerrainRestorePass(),
        };

        CellBuildPasses = new List<IWorldspacePass>
        {
            new NavmeshSeedPass(),
            new TileInstantiationPass(),
        };

        ContentPasses = new List<IWorldspacePass>
        {
            new LodLayerPass(),
            new RockScatterPass(0.4f),
            new VegetationScatterPass(0.2f),
            new MapMarkerPass(MapMarkerPass.MarkerType.Industrial),
            new TravelMarkerPass(),
            new PlanetContentManagerPass("ps_blockbranch", "ps_blockcontent"),
            new PlanetScanPass("ps_scanbranch", "ps_scancontent"),
            new PlanetQuestPass("ps_questbranch", "ps_questcontent"),
            new WorldspaceBossPass(),
            new LvlHumanHostilePass(10,50),
        };
    }

    public string GeneratePOIName(int seed)
    {
        WorldspaceName = IndustryNameGenerator.GetRandomPOIName(seed);
        WorldspaceEditorId = WorldspaceName.ToLowerInvariant().Replace(" ", "");
        return WorldspaceName;
    }
}

internal static class IndustryNameGenerator
{
    private static readonly List<string> Adjectives = new()
    {
        "Derelict", "Abandoned", "Remote", "Stripped", "Overhauled",
        "Operational", "Defunct", "Active", "Neglected", "Converted",
        "Salvaged", "Repurposed", "Isolated", "Contested", "Seized",
    };

    private static readonly List<string> Nouns = new()
    {
        "Processing Plant", "Refinery", "Industrial Site", "Fabrication Bay",
        "Assembly Depot", "Storage Facility", "Extraction Site", "Production Complex",
        "Fuel Depot", "Cargo Hub", "Manufacturing Post", "Smelting Works",
        "Chemical Plant", "Distribution Centre", "Mineral Processing Site", "Operations Hub",
    };

    public static string GetRandomPOIName(int seed)
    {
        var rand = new Random(seed);
        return Adjectives[rand.Next(Adjectives.Count)] + " " + Nouns[rand.Next(Nouns.Count)];
    }
}
