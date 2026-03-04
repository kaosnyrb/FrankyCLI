namespace Retrograde.Lore;

public class MissionLore
{
    public string                    Id        { get; set; } = "";
    public string                    Name      { get; set; } = "";
    public string                    Faction   { get; set; } = "";
    public Dictionary<string, string> Constants { get; set; } = new();
    public List<string>              Npcs      { get; set; } = new();
    public List<string>              Chain     { get; set; } = new();
}
