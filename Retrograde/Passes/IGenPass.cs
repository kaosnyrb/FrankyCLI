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

    public class ScoringSystem
    {
        public double PlacementWeight;          //How important is placing all the rooms?
        public double BridgingWeight;           //How important is having multiple routes?
        public double BridgingOverlapWeight;    //How important is having multiple routes from A->B
        public double NorthBiasWeight;          //How important is heading north in trunk layout
        public double NewConnectorsWeight;      //How important is exposing new connectors for later passes
        public double AreaWeight;               //How important is rewarding total floor area

        public int Effort;
    }

    public class DungeonState
    {
        public DungeonState(Cell cell, Location plocation) {
            placedRooms = new List<PlacedRoom>();
            openConnectors = new List<OpenConnector>();
            windowConnectors = new List<P3Float>();
            TrunkRoomLists = new List<string>();
            instance = cell;
            location = plocation;
        }

        public string stateName;

        public Cell instance;
        public Location location;
        public List<PlacedRoom> placedRooms;
        public List<OpenConnector> openConnectors;
        public List<P3Float> windowConnectors;
        public List<string> TrunkRoomLists;

        public ScoringSystem scoringSystem;

        public P3Float StartingPosition;

        public float YMin = 0;

        public string Faction = "spacer";
        public string Size = "Small";

        public List<IGenPass> passes;

    }
}
