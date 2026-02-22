using Retrograde.Passes.Worldspace;
using System;
using System.Collections.Generic;

namespace Retrograde.WorldspaceDesigns;

public class FortDesign : IWorldspaceDesign
{
    public List<IWorldspacePass> MapPasses { get; set; }
    public List<IWorldspacePass> CellBuildPasses { get; set; }
    public List<IWorldspacePass> ContentPasses { get; set; }

    private readonly string _templateWorldspaceEditorId;
    public string TemplateWorldspaceEditorId => _templateWorldspaceEditorId;
    public int MapSize => 50;
    public float TileWorldSize => 4f;
    public string DesignName => "Fort";

    /// <summary>The chosen worldspace name, e.g. "Forsaken Fort". Set by GeneratePOIName.</summary>
    public string WorldspaceName { get; private set; } = string.Empty;

    /// <summary>Editor-friendly version of WorldspaceName: lowercase, spaces removed, e.g. "forsakenfort".</summary>
    public string WorldspaceEditorId { get; private set; } = string.Empty;

    public FortDesign(string templateWorldspaceEditorId = "DR001World", float scale = 0.25f)
    {
        _templateWorldspaceEditorId = templateWorldspaceEditorId;
        MapPasses = new List<IWorldspacePass>
        {
            new PackInLibraryPass(FortPackInIds()),
            new TerrainFlattenPass(),
            new FortLayoutPass(scale),
            new TerrainRestorePass(),
        };

        CellBuildPasses = new List<IWorldspacePass>
        {
            new NavmeshSeedPass(),
            new TileInstantiationPass(),
        };

        ContentPasses = new List<IWorldspacePass>()
        {
            new PondPass(),
            new RockScatterPass(0.5f),
            new VegetationScatterPass(0.5f),
            new MapMarkerPass(MapMarkerPass.MarkerType.MilitaryBase),
            new PlanetContentManagerPass("ps_blockbranch", "ps_blockcontent"),
            new PlanetScanPass("ps_scanbranch", "ps_scancontent"),
            new PlanetQuestPass("ps_questbranch", "ps_questcontent")
        };
    }

    public string GeneratePOIName(int seed)
    {
        WorldspaceName = FortNameGenerator.GetRandomPOIName(seed);
        WorldspaceEditorId = WorldspaceName.ToLowerInvariant().Replace(" ", "");
        return WorldspaceName;
    }

    private static Dictionary<string, string> FortPackInIds()
    {
        return new Dictionary<string, string>
        {
            { "to_pkn_base", "to_pkn_base" },
            { "to_pkn_wall_", "to_pkn_wall_" },
            { "to_pkn_wallcorner_", "to_pkn_wallcorner_" },
            { "to_pkn_wallinside_", "to_pkn_wallinside_" },
            { "to_pkn_addon_wall_", "to_pkn_addon_wall_" },
            { "to_pkn_gate_", "to_pkn_gate_" },
            { "to_pkn_stairs_", "to_pkn_stairs_" },
            { "to_pkn_single", "to_pkn_single" },
            { "to_pkn_large", "to_pkn_large" },
            { "to_pkn_large_barracks", "to_pkn_large_barracks" },
            { "to_pkn_large_armory", "to_pkn_large_armory" },
            { "to_pkn_landing_", "to_pkn_landing_" },
            { "to_pkn_small_", "to_pkn_small_" },
        };
    }
}

/// <summary>
/// Generates random fort POI names from adjective + noun combinations.
/// Ported from StarTiller NameGenerator.
/// </summary>
internal static class FortNameGenerator
{
    private static readonly List<string> Adjectives = new()
    {
        "Forsaken", "Deserted", "Neglected", "Discarded", "Forgotten",
        "Abandoned", "Relinquished", "Vacant", "Desolate", "Unoccupied",
        "Derelict", "Despoiled", "Isolated", "Renounced", "Surrendered",
        "Disowned", "Disregarded", "Unclaimed", "Ignored", "Forsook",
        "Vacated", "Uninhabited", "Unprotected", "Destitute", "Dilapidated",
        "Ramshackle", "Decrepit", "Decayed", "Distant", "Faraway",
        "Secluded", "Outlying", "Inaccessible", "Far flung", "Lonely",
        "Aloof", "Sequestered", "Outer", "Peripheral", "Rural",
        "Removed", "Solitary", "Unreachable", "Cut off", "Shielded",
        "Protected", "Fortified", "Armoured", "Plated", "Ironclad",
        "Reinforced", "Secured", "Safeguarded", "Defended", "Covered",
        "Bulletproof", "Steel clad", "Armored up", "Encased", "Hardened",
        "Armor plated", "Metal clad", "Armored", "Combat ready", "Unyielding"
    };

    private static readonly List<string> Nouns = new()
    {
        "Transit Point", "Holding Area", "Base", "Garrison", "Fort",
        "Camp", "Outpost", "Barracks", "Station", "Encampment",
        "Installation", "Stronghold", "Command Center", "Military Base", "Post",
        "Cantonment", "Headquarters", "Defense Post", "Army Post", "Operational Base",
        "Staging Area", "Military Installation", "Fortification", "Stronghold", "Military Base",
        "Outpost", "Station", "Encampment", "Armory", "Command Post",
        "Depot", "Military Post", "Cantonment", "Installation", "Quarters",
        "Military Station", "Defense Post", "Citadel", "Billet", "Bunker",
        "Fortress", "Compound", "Watchtower", "Barricade", "Hold",
        "Redoubt", "Bastion", "Defensive Line", "War camp", "Blockhouse",
        "Stockade", "Palisade", "Enclave", "Entrenchment", "Parapet",
        "Bulwark", "Guardhouse", "Enclosure", "Presidio", "Armament",
        "Fortlet", "Field Camp", "Barrage", "Sentry Post", "Fire Base",
        "Guard Post", "Military Encampment", "Siege Camp", "Command center",
        "Security Station", "Battle Station", "Trench", "War Station",
        "Defensive Position", "Combat Outpost", "Reinforced Position",
        "Quartier", "Redoute", "Keep", "Alcazar", "Battlement", "Armored Bastion"
    };

    public static string GetRandomPOIName(int seed)
    {
        Random rand = new Random(seed);
        return Adjectives[rand.Next(Adjectives.Count - 1)] + " " + Nouns[rand.Next(Nouns.Count - 1)];
    }
}
