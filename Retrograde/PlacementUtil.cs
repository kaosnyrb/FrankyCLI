using System.Collections.Generic;
using Mutagen.Bethesda.Starfield;

namespace FrankyCLI.Retrograde
{
    public static class PlacementUtil
    {
        private static readonly List<(Cell cell, PlacedObject placedObject)> _pendingPlacements = new();

        public static readonly List<PlacedObject> PlacedObjects = new();

        public static void AddToTemporary(Cell cell, PlacedObject placedObject)
        {
            if (cell == null || placedObject == null)
            {
                return;
            }

            _pendingPlacements.Add((cell, placedObject));
        }

        public static void Finalise()
        {
            foreach (var (cell, placedObject) in _pendingPlacements)
            {
                cell.Temporary.Add(placedObject);
                PlacedObjects.Add(placedObject);
            }

            _pendingPlacements.Clear();
        }

        public static void Reset()
        {
            _pendingPlacements.Clear();
            PlacedObjects.Clear();
        }
    }
}
