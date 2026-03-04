namespace Retrograde.Lore;

public class QuestLore
{
    public string Id       { get; set; } = "";
    public string Template { get; set; } = "";

    // Standard quest fields
    public string Name     { get; set; } = "";
    public string LogEntry { get; set; } = "";
    public string Objective { get; set; } = "";

    // Discovery_Dataslate only
    public string BookTitle    { get; set; } = "";
    public string BookContents { get; set; } = "";

    // Discovery_WantedPoster only
    public string PickupMessage { get; set; } = "";

    public Dictionary<string, string>  Npcs       { get; set; } = new();
    public Dictionary<string, object>  Parameters { get; set; } = new();
    public List<string>                Addons     { get; set; } = new();
}
