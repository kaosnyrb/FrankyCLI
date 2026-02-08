namespace Retrograde.Passes;

/// <summary>
/// Interface for dungeon generation passes.
/// Each pass operates on the shared DungeonState.
/// </summary>
public interface IGenPass
{
    void RunPass(DungeonState state);
}

/// <summary>
/// Wraps an IGenPass with a chance to run (0.0 to 1.0).
/// Used for optional content passes like loot rooms.
/// </summary>
public class OptionalPass
{
    public IGenPass Pass { get; set; }
    public float Chance { get; set; }

    public OptionalPass(IGenPass pass, float chance)
    {
        Pass = pass;
        Chance = chance;
    }
}
