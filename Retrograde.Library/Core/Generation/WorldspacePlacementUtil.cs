using System.Collections.Generic;
using Mutagen.Bethesda.Starfield;

namespace Retrograde;

public class WorldspacePlacementUtil
{
    private readonly List<(Cell cell, IPlaced placed)> _pendingTemporary = new();
    private readonly List<IPlaced> _pendingPersistent = new();
    private Cell _topCell;

    public void SetTopCell(Cell topCell) => _topCell = topCell;

    public void AddToTemporary(Cell cell, PlacedObject placed)
    {
        if (cell == null || placed == null) return;
        _pendingTemporary.Add((cell, placed));
    }

    public void AddToTemporary(Cell cell, PlacedNpc placed)
    {
        if (cell == null || placed == null) return;
        _pendingTemporary.Add((cell, placed));
    }

    public void AddToPersistent(PlacedObject placed)
    {
        if (placed == null) return;
        _pendingPersistent.Add(placed);
    }

    public void AddToPersistent(PlacedNpc placed)
    {
        if (placed == null) return;
        _pendingPersistent.Add(placed);
    }

    public void Finalise()
    {
        foreach (var (cell, placed) in _pendingTemporary)
        {
            cell.Temporary.Add(placed);
        }
        foreach (var placed in _pendingPersistent)
        {
            _topCell.Persistent.Add(placed);
        }

        _pendingTemporary.Clear();
        _pendingPersistent.Clear();
    }
}
