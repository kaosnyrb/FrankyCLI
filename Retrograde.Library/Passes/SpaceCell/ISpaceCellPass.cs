namespace Retrograde.Passes.SpaceCell;

/// <summary>
/// Interface for space cell generation passes.
/// Each pass operates on the shared SpaceCellState.
/// </summary>
public interface ISpaceCellPass
{
    void RunPass(SpaceCellState state);
}
