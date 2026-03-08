using Retrograde.Passes.Worldspace;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.WorldspaceDesigns;

public readonly struct SmallIndustryBaseConfig
{
    /// <summary>Default configuration matching the original SmallIndustryBaseDesign settings.</summary>
    public static SmallIndustryBaseConfig Default => new()
    {
        HasPlanetContentManager = true,
        HasPlanetScan           = true,
        HasPlanetQuest          = true,
        EnemyCount              = 10,
        HasBoss                 = true,
    };

    /// <summary>Specific template worldspace to clone terrain from. When null, picks a random tpl* worldspace.</summary>
    public string? TemplateWorldspaceEditorId { get; init; }

    /// <summary>When set, overrides the randomly generated POI name.</summary>
    public string? NameOverride { get; init; }

    /// <summary>Whether to include a PlanetContentManagerPass.</summary>
    public bool HasPlanetContentManager { get; init; }

    /// <summary>Whether to include a PlanetScanPass.</summary>
    public bool HasPlanetScan { get; init; }

    /// <summary>Whether to include a PlanetQuestPass.</summary>
    public bool HasPlanetQuest { get; init; }

    /// <summary>Number of hostile NPCs to scatter around the POI. Pass 0 to skip.</summary>
    public int EnemyCount { get; init; }

    /// <summary>Whether to include a WorldspaceBossPass.</summary>
    public bool HasBoss { get; init; }
}

public class SmallIndustryBaseDesign : IWorldspaceDesign
{
    public List<IWorldspacePass> MapPasses { get; set; }
    public List<IWorldspacePass> CellBuildPasses { get; set; }
    public List<IWorldspacePass> ContentPasses { get; set; }

    private readonly SmallIndustryBaseConfig _config;
    public string TemplateWorldspaceEditorId { get; }
    public int MapSize => 50;
    public float TileWorldSize => 4f;
    public string DesignName => "SmallIndustryBase";

    public string WorldspaceName { get; private set; } = string.Empty;
    public string WorldspaceEditorId { get; private set; } = string.Empty;

    public SmallIndustryBaseDesign(SmallIndustryBaseConfig config, float scale = 1.0f)
    {
        _config = config;

        TemplateWorldspaceEditorId = config.TemplateWorldspaceEditorId ?? PickRandomTemplate();

        MapPasses = new List<IWorldspacePass>
        {
            new IndustryPackInLibraryPass(),
            new IndustryLayoutPass(scale),
            new IndustryGroundFlattenPass(),
        };

        CellBuildPasses = new List<IWorldspacePass>
        {
            new NavmeshSeedPass(),
            new TileInstantiationPass(),
        };

        var contentPasses = new List<IWorldspacePass>
        {
            new IndustryPropScatterPass(),
            new LodLayerPass(),
            new RockScatterPass(0.4f),
            new VegetationScatterPass(0.2f),
            new MapMarkerPass(MapMarkerPass.MarkerType.Industrial),
            new TravelMarkerPass(),
        };

        if (config.HasPlanetContentManager)
            contentPasses.Add(new PlanetContentManagerPass("ps_blockbranch", "ps_blockcontent"));
        if (config.HasPlanetScan)
            contentPasses.Add(new PlanetScanPass("ps_scanbranch", "ps_scancontent"));
        if (config.HasPlanetQuest)
            contentPasses.Add(new PlanetQuestPass("ps_questbranch", "ps_questcontent"));
        if (config.HasBoss)
            contentPasses.Add(new WorldspaceBossPass());
        if (config.EnemyCount > 0)
            contentPasses.Add(new LvlHumanHostilePass(config.EnemyCount, 50));

        ContentPasses = contentPasses;
    }

    private static string PickRandomTemplate()
    {
        var candidates = RetrogradeContext.Current.TemplateMods
            .SelectMany(m => m.Worldspaces)
            .Where(w => w.EditorID?.StartsWith("tpl", StringComparison.OrdinalIgnoreCase) == true)
            .Select(w => w.EditorID!)
            .ToList();
        return candidates.Count > 0
            ? candidates[RandomProvider.Random.Next(candidates.Count)]
            : "DR001World";
    }

    public string GeneratePOIName(int seed)
    {
        WorldspaceName = _config.NameOverride ?? IndustryNameGenerator.GetRandomPOIName();
        WorldspaceEditorId = WorldspaceName.ToLowerInvariant().Replace(" ", "").Replace("-", "");
        return WorldspaceName;
    }
}

internal static class IndustryNameGenerator
{
    private static readonly List<string> FacilityTypes =
    [
        "Refinery", "Processing Plant", "Fabrication Bay", "Assembly Depot",
        "Storage Facility", "Extraction Site", "Production Complex", "Fuel Depot",
        "Cargo Hub", "Smelting Works", "Chemical Plant", "Distribution Centre",
        "Mineral Works", "Operations Hub", "Foundry", "Forge",
    ];

    private static readonly List<string> CallLetters =
    [
        "AL","CP","DK","EM","FS","GV","HT","JC","KN","LP",
        "MQ","NW","OY","PR","QS","RU","SV","TY","WX","ZA",
    ];

    public static string GetRandomPOIName()
    {
        var rand       = RandomProvider.Random;
        string type    = FacilityTypes[rand.Next(FacilityTypes.Count)];
        string letters = CallLetters[rand.Next(CallLetters.Count)];
        string number  = rand.Next(10, 1000).ToString("D3");
        return $"{type} {letters}-{number}";
    }
}
