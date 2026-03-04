namespace Retrograde.Lore;

public class DialogueLore
{
    public string Id    { get; set; } = "";
    public string Npc   { get; set; } = "";
    public string Start { get; set; } = "";
    public Dictionary<string, DialogueNode> Nodes { get; set; } = new();
}

/// <summary>
/// Flat node — no polymorphism. Type field drives runtime dispatch.
/// All fields are nullable; unused fields for a given type will be null/default.
/// </summary>
public class DialogueNode
{
    public string Type { get; set; } = "";  // "npc_line" | "player_choices" | "end"

    // npc_line fields
    public string       Text               { get; set; } = "";
    public List<string> Segments           { get; set; } = new();
    public string       Next               { get; set; } = "";
    public int?         SetConversationStage { get; set; }
    public string       VoiceNote          { get; set; } = "";

    // player_choices fields
    public List<PlayerChoice> Choices { get; set; } = new();
}

public class PlayerChoice
{
    public string Text          { get; set; } = "";
    public string Next          { get; set; } = "";
    public int?   RequiresStage { get; set; }
}
