using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.FactionMembers;
using System;
using System.Collections.Generic;

namespace Retrograde.Passes.SpaceStation;

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
        ItemsToRemove = new List<FormKey>();
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

    private IFactionMembers _factionCrew;
    public IFactionMembers FactionCrew
    {
        get
        {
            if (_factionCrew == null)
            {
                _factionCrew = CreateFactionCrew(Faction);
            }
            return _factionCrew;
        }
    }
    private static IFactionMembers CreateFactionCrew(string faction)
    {
        switch (faction)
        {
            case "Crimsonfleet":
                return new CrimsonFleetFactionCrew();
            case "Ecliptic":
                return new EclipticFactionCrew();
            case "Varuun":
                return new VaruunFactionCrew();
            case "Spacer":
                return new SpacerFactionCrew();
            default:
                return new SpacerFactionCrew();
        }
    }

    // Set by harness runs to suppress noisy pass-level logging.
    public bool IsHarnessRun { get; set; }
    public RgConnector StartingConnector { get; internal set; }

    /// <summary>
    /// The boss NPC placed in the persistent layer. Use FormKey to reference later.
    /// </summary>
    public PlacedNpc BossPlacedNpc { get; set; }

    public List<FormKey> ItemsToRemove { get; set; }

    /// <summary>
    /// The entrance door into the dungeon interior cell.
    /// Set by StationNoun before generation so ExitTopologyPass can link the exit door back to it.
    /// </summary>
    public PlacedObject EntranceDoor { get; set; }

    /// <summary>
    /// The exit door placed by ExitTopologyPass. Linked bidirectionally with EntranceDoor.
    /// </summary>
    public PlacedObject ExitDoor { get; set; }
}
