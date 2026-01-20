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
        public double ClusteringWeight;         //How important is spacing out rooms instead of clumping
        public double SizeDiversityWeight;      //How important is avoiding chains of tiny rooms
        public double RoomReuseWeight;          //How important is reusing the same prefab multiple times
        public double ConnectorViabilityWeight; //How important is leaving connectors with viable space for the next room

        public int Effort;
    }

    public class DungeonState
    {
        public DungeonState(Cell cell, Location plocation) {
            placedRooms = new List<PlacedRoom>();
            openConnectors = new List<OpenConnector>();
            windowConnectors = new List<P3Float>();
            TrunkRoomLists = new List<string>();
            SealedConnectorPositionKeys = new HashSet<string>();
            instance = cell;
            location = plocation;
            RoomUtilsCache = new Dictionary<string, RoomUtils>(StringComparer.OrdinalIgnoreCase);
        }

        public string stateName;

        public Cell instance;
        public Location location;
        public List<PlacedRoom> placedRooms;
        public List<OpenConnector> openConnectors;
        public List<P3Float> windowConnectors;
        public List<string> TrunkRoomLists;
        public HashSet<string> SealedConnectorPositionKeys;

        public ScoringSystem scoringSystem;

        public P3Float StartingPosition;
        public HashSet<string> BridgePrefabKeys;
        public Dictionary<string, RoomUtils> RoomUtilsCache;

        public float YMin = 0;

        public string Faction = "spacer";
        public string Size = "Small";

        public List<IGenPass> passes;

        public RoomUtils GetRoomUtils(string listName)
        {
            if (string.IsNullOrWhiteSpace(listName))
                throw new ArgumentException("Room list name cannot be null or empty.", nameof(listName));

            if (!RoomUtilsCache.TryGetValue(listName, out var utils))
            {
                utils = new RoomUtils(listName);
                RoomUtilsCache[listName] = utils;
            }

            return utils;
        }
    }
}
