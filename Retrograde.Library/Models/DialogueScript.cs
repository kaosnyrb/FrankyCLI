namespace Retrograde.Models;

public class DialogueScript
{
    public int StageCount => Stages.Count;
    public List<DialogueStage> Stages { get; set; } = new();
    public string Goodbye { get; set; } = "";
}

public class DialogueStage
{
    /// <summary>NPC's spoken greeting at this stage (ResponseText on the Greeting INFO).</summary>
    public string NpcLine { get; set; } = "";

    /// <summary>Player menu text for the advance choice. Null on the last stage.</summary>
    public string? ProgressPrompt { get; set; }

    public List<DialogueExchange> Explores { get; set; } = new();
}

public class DialogueExchange
{
    /// <summary>Player's menu text (≤60 chars).</summary>
    public string PlayerPrompt { get; set; } = "";

    /// <summary>NPC's voiced reply (≤200 chars) in ResponseText.</summary>
    public string NpcReply { get; set; } = "";
}
