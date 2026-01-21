using Mutagen.Bethesda.Starfield;

namespace FrankyCLI.Retrograde
{
    public static class PlacementUtil
    {
        public static void AddToTemporary(Cell cell, PlacedObject placedObject)
        {
            if (cell == null || placedObject == null)
            {
                return;
            }

            cell.Temporary.Add(placedObject);
        }
    }
}
