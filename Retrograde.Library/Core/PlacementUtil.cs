using System.Collections.Generic;
using Mutagen.Bethesda.Starfield;

namespace Retrograde;

public class PlacementUtil
{
    private readonly List<(Cell cell, IPlaced placedObject)> _pendingPlacements = new();

    public readonly List<IPlaced> PlacedObjects = new();

    public void AddToTemporary(Cell cell, PlacedObject placedObject)
    {
        if (cell == null || placedObject == null)
        {
            return;
        }

        _pendingPlacements.Add((cell, placedObject));
    }

    public void NPCAddToTemporary(Cell cell, PlacedNpc placedObject)
    {

        if (cell == null || placedObject == null)
        {
            return;
        }

        placedObject.LevelModifier = Level.Medium;

        _pendingPlacements.Add((cell, placedObject));
    }

    public void Finalise()
    {
        foreach (var (cell, placedObject) in _pendingPlacements)
        {
            cell.Temporary.Add(placedObject);
            PlacedObjects.Add(placedObject);
        }

        _pendingPlacements.Clear();
    }

    public void Reset()
    {
        _pendingPlacements.Clear();
        PlacedObjects.Clear();
    }
}
