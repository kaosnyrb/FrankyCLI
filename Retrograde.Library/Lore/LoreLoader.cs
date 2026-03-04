using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Retrograde.Lore;

/// <summary>
/// Loads a mission folder into a fully-linked LoadedMission.
/// Uses UnderscoredNamingConvention so snake_case YAML keys map to PascalCase C# properties.
/// </summary>
public static class LoreLoader
{
    private static readonly IDeserializer _deserializer =
        new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

    /// <param name="missionFolder">
    /// Path to the mission folder (e.g. "lore/bad_batch").
    /// Resolved relative to working directory if not absolute.
    /// </param>
    public static LoadedMission Load(string missionFolder)
    {
        string folder = Path.GetFullPath(missionFolder);
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"Mission folder not found: {folder}");

        // 1. Mission entry point
        var mission = Deserialize<MissionLore>(File.ReadAllText(Path.Combine(folder, "mission.yaml")));
        var constants = mission.Constants ?? new Dictionary<string, string>();

        // 2. NPC files
        var npcs = mission.Npcs
            .Select(p => Deserialize<NpcLore>(ApplyConstants(ReadRelative(folder, p), constants)))
            .ToList();

        // 3. Quest chain files
        var quests = mission.Chain
            .Select(p => Deserialize<QuestLore>(ApplyConstants(ReadRelative(folder, p), constants)))
            .ToList();

        // 4. Dialogue files — for each NPC that declares an ambient_dialogue id
        var dialogues = new Dictionary<string, DialogueLore>();
        foreach (var npc in npcs.Where(n => !string.IsNullOrEmpty(n.AmbientDialogue)))
        {
            if (dialogues.ContainsKey(npc.AmbientDialogue))
                continue;  // already loaded (shared NPC across multiple missions)

            var dlgPath = ResolveDialoguePath(folder, npc.AmbientDialogue);
            var dlg = Deserialize<DialogueLore>(ApplyConstants(File.ReadAllText(dlgPath), constants));
            dialogues[dlg.Id] = dlg;
        }

        return new LoadedMission
        {
            Mission   = mission,
            Npcs      = npcs,
            Quests    = quests,
            Dialogues = dialogues,
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static T Deserialize<T>(string yaml) =>
        _deserializer.Deserialize<T>(yaml)
            ?? throw new InvalidDataException($"YAML deserialized to null for type {typeof(T).Name}");

    /// <summary>Substitutes {{KEY}} tokens with values from the constants dict.</summary>
    private static string ApplyConstants(string yaml, Dictionary<string, string> constants)
    {
        if (constants.Count == 0)
            return yaml;
        return Regex.Replace(yaml, @"\{\{(\w+)\}\}", m =>
            constants.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
    }

    /// <summary>
    /// Reads a file at a path relative to the mission folder.
    /// Paths prefixed "shared/" resolve from lore/shared/ alongside the mission folder.
    /// </summary>
    private static string ReadRelative(string missionFolder, string relativePath)
    {
        string fullPath = relativePath.StartsWith("shared/")
            ? Path.Combine(missionFolder, "..", "shared", relativePath["shared/".Length..])
            : Path.Combine(missionFolder, relativePath);
        fullPath = Path.GetFullPath(fullPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Lore file not found: {fullPath}");
        return File.ReadAllText(fullPath);
    }

    /// <summary>
    /// Resolves a dialogue id to a file path.
    /// Searches mission/dialogue/{id}.yaml first, then lore/shared/dialogue/{id}.yaml.
    /// </summary>
    private static string ResolveDialoguePath(string missionFolder, string dialogueId)
    {
        string missionPath = Path.GetFullPath(Path.Combine(missionFolder, "dialogue", dialogueId + ".yaml"));
        if (File.Exists(missionPath))
            return missionPath;

        string sharedPath = Path.GetFullPath(Path.Combine(missionFolder, "..", "shared", "dialogue", dialogueId + ".yaml"));
        if (File.Exists(sharedPath))
            return sharedPath;

        throw new FileNotFoundException(
            $"Dialogue file '{dialogueId}.yaml' not found in mission/dialogue/ or shared/dialogue/.");
    }
}

public class LoadedMission
{
    public MissionLore                      Mission   { get; set; } = new();
    public List<NpcLore>                    Npcs      { get; set; } = new();
    public List<QuestLore>                  Quests    { get; set; } = new();
    public Dictionary<string, DialogueLore> Dialogues { get; set; } = new();
}
