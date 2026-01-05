using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Noggog;

namespace FrankyCLI.Retrograde.Passes
{
    public interface IGenPass
    {
        public void RunPass(DungeonState state);
    }

    public class DungeonState
    {
        public DungeonState(Cell cell, Location plocation) {
            placedRooms = new List<PlacedRoom>();
            openConnectors = new List<OpenConnector>();
            instance = cell;
            location = plocation;
        }
        public Cell instance;
        public Location location;
        public List<PlacedRoom> placedRooms;
        public List<OpenConnector> openConnectors;

        public P3Float StartingPosition;

        public float YMin = 0;

        public string Faction = "spacer";
        public string Size = "Small";

    }
}
