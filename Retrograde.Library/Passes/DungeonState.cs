using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.FactionMembers;
using System;
using System.Collections.Generic;

namespace Retrograde.Passes;

/// <summary>
/// Shared mutable state passed to all generation passes.
/// Holds the dungeon cell, placed rooms, open connectors, and configuration.
/// </summary>
public class DungeonState
{
    public DungeonState(Cell cell, Location location)
    {
        placedRooms = new List<PlacedRoom>();
        openConnectors = new List<OpenConnector>();
        windowConnectors = new List<P3Float>();
        TrunkRoomLists = new List<string>();
        SealedConnectorPositionKeys = new HashSet<string>();
        UsedDoorPositions = new HashSet<string>();
        instance = cell;
        this.location = location;
        RoomUtilsCache = new Dictionary<string, RoomUtils>(StringComparer.OrdinalIgnoreCase);
        PlacementUtil = new PlacementUtil();
    }

    public string stateName;

    public Cell instance;
    public Location location;
    public List<PlacedRoom> placedRooms;
    public List<OpenConnector> openConnectors;
    public List<P3Float> windowConnectors;
    public List<string> TrunkRoomLists;
    public HashSet<string> SealedConnectorPositionKeys;
    public HashSet<string> UsedDoorPositions;

    public ScoringSystem? scoringSystem;

    public P3Float StartingPosition;
    public HashSet<string>? BridgePrefabKeys;
    public Dictionary<string, RoomUtils> RoomUtilsCache;
    public PlacementUtil PlacementUtil { get; }

    /// <summary>
    /// Gets or creates a RoomUtils for the given list name.
    /// </summary>
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

    public float YMin = 0;

    public string Faction = "spacer";
    public string Size = "Small";
    public float AreaPerEnemy = 512f;

    /// <summary>
    /// Factory function to create faction crew members.
    /// Must be set by the host application before passes that need faction NPCs.
    /// </summary>
    public Func<string, IFactionMembers>? FactionCrewFactory { get; set; }

    private IFactionMembers? _factionCrew;

    /// <summary>
    /// Lazily-initialized faction crew. Uses FactionCrewFactory if set.
    /// </summary>
    public IFactionMembers? FactionCrew
    {
        get
        {
            if (_factionCrew == null && FactionCrewFactory != null)
            {
                _factionCrew = FactionCrewFactory(Faction);
            }
            return _factionCrew;
        }
        set => _factionCrew = value;
    }

    // Set by harness runs to suppress noisy pass-level logging.
    public bool IsHarnessRun { get; set; }
    public RgConnector StartingConnector { get; internal set; }
}
