namespace Retrograde.Passes;

public class ScoringSystem
{
    public double PlacementWeight;          // How important is placing all the rooms?
    public double BridgingWeight;           // How important is having multiple routes?
    public double BridgingOverlapWeight;    // How important is having multiple routes from A->B
    public double NorthBiasWeight;          // How important is heading north in trunk layout
    public double NewConnectorsWeight;      // How important is exposing new connectors for later passes
    public double AreaWeight;               // How important is rewarding total floor area
    public double ClusteringWeight;         // How important is spacing out rooms instead of clumping
    public double SizeDiversityWeight;      // How important is avoiding chains of tiny rooms
    public double RoomReuseWeight;          // How important is reusing the same prefab multiple times
    public double ConnectorViabilityWeight; // How important is leaving connectors with viable space for the next room
    public double DuplicateRoomPenaltyWeight; // How important is penalising rooms that already appear in other cells

    public int Effort;
}
