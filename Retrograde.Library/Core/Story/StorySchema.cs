using System.Text.Json;
using System.Text.Json.Serialization;

namespace Retrograde.Story
{
    /// <summary>
    /// Data-driven story structure definition. Loaded from JSON files
    /// in questgen_quests/schemas/. Defines the shape of a story
    /// without any C# code — beat sequence, character roles, stakes.
    /// </summary>
    public class StorySchema
    {
        [JsonPropertyName("schema")]
        public string Id { get; set; } = "";

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("roles")]
        public Dictionary<string, SchemaRole> Roles { get; set; } = new();

        [JsonPropertyName("stakes")]
        public SchemaStakes Stakes { get; set; } = new();

        [JsonPropertyName("beats")]
        public List<SchemaBeat> Beats { get; set; } = new();

        [JsonPropertyName("arc")]
        public SchemaArc Arc { get; set; } = new();

        [JsonPropertyName("loreTemplate")]
        public string LoreTemplate { get; set; } = "";

        /// <summary>
        /// Load a schema from a JSON file.
        /// </summary>
        public static StorySchema Load(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<StorySchema>(json)
                ?? throw new InvalidOperationException($"Failed to deserialize schema from {path}");
        }

        /// <summary>
        /// Load a schema by name from the default schemas directory.
        /// </summary>
        public static StorySchema LoadByName(string schemaName)
        {
            var path = Path.Combine(".", "questgen_quests", "schemas", schemaName + ".json");
            return Load(path);
        }

        /// <summary>
        /// Find a beat definition by its category (discovery, investigation, showdown).
        /// </summary>
        public SchemaBeat? GetBeat(string category)
            => Beats.FirstOrDefault(b => b.Category == category);
    }

    public class SchemaRole
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("required")]
        public bool Required { get; set; }

        [JsonPropertyName("traitPool")]
        public string TraitPool { get; set; } = "outlaw";
    }

    public class SchemaStakes
    {
        [JsonPropertyName("playerMotivation")]
        public string PlayerMotivation { get; set; } = "";

        [JsonPropertyName("failureConsequence")]
        public string FailureConsequence { get; set; } = "";

        [JsonPropertyName("playerRole")]
        public string PlayerRole { get; set; } = "bounty hunter";
    }

    public class SchemaBeat
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("function")]
        public string Function { get; set; } = "";

        /// <summary>
        /// Maps to ITemplateManager method: "discovery", "investigation", or "showdown".
        /// </summary>
        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("count")]
        public int[] Count { get; set; } = [1, 1];

        [JsonPropertyName("progressRange")]
        public int[] ProgressRange { get; set; } = [0, 0];

        public int MinCount => Count.Length > 0 ? Count[0] : 1;
        public int MaxCount => Count.Length > 1 ? Count[1] : MinCount;
        public int ProgressMin => ProgressRange.Length > 0 ? ProgressRange[0] : 0;
        public int ProgressMax => ProgressRange.Length > 1 ? ProgressRange[1] : ProgressMin;
    }

    public class SchemaArc
    {
        [JsonPropertyName("order")]
        public List<string> Order { get; set; } = new();

        [JsonPropertyName("bridges")]
        public bool Bridges { get; set; } = true;
    }
}
