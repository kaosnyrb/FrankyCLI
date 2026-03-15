namespace Retrograde.Models;

public class DialogueScript
{
    /// <summary>NPC's opening voiced line when the player activates them (≤150 chars).</summary>
    public string NpcGreeting { get; set; } = "";

    /// <summary>Player choice + NPC reply pairs presented in the choice menu.</summary>
    public List<DialogueExchange> Exchanges { get; set; } = new();
}

public class DialogueExchange
{
    /// <summary>Player's voiced question shown in the choice menu (≤60 chars).</summary>
    public string PlayerPrompt { get; set; } = "";

    /// <summary>NPC's voiced reply — up to 2 lines, each ≤150 chars.</summary>
    public List<string> NpcReply { get; set; } = [];

    /// <summary>
    /// Optional color side topics. When non-null, two extra player choices appear alongside
    /// this exchange — one for extra detail, one for a lighter aside. Neither advances the stage.
    /// </summary>
    public DialogueSideOptions? SideOptions { get; set; }
}

public class SideOption
{
    /// <summary>Player's voiced question shown in the choice menu (≤50 chars).</summary>
    public string PlayerPrompt { get; set; } = "";

    /// <summary>NPC's voiced reply — up to 2 lines, each ≤150 chars.</summary>
    public List<string> NpcReply { get; set; } = [];
}

public class DialogueSideOptions
{
    public SideOption ExtraInfo { get; set; } = new();
    public SideOption Joke      { get; set; } = new();
}
