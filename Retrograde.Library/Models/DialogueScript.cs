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

    /// <summary>NPC's voiced reply (≤200 chars).</summary>
    public string NpcReply { get; set; } = "";
}
