using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Retrograde.Passes
{
    public interface IGenPass
    {
        public void RunPass(DungeonState state);
    }

    public class DungeonState
    {
        public DungeonState(Cell cell) {
            placedRooms = new List<PlacedRoom>();
            openConnectors = new List<OpenConnector>();
            instance = cell;
        }
        public Cell instance;
        public List<PlacedRoom> placedRooms;
        public List<OpenConnector> openConnectors;
    }
}
