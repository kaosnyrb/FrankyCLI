using Retrograde.Passes.SpaceStation;
using System.Collections.Generic;

namespace Retrograde.StationDesigns;

/// <summary>
/// Interface for space station dungeon designs.
/// Defines the passes to run and configuration for generation.
/// </summary>
public interface IStationDesign
{
    /// <summary>
    /// Core room topology passes (setup, trunk, districts, boss, bridging, util).
    /// </summary>
    List<IGenPass> MainRoomPasses { get; set; }

    /// <summary>
    /// Optional room passes with a chance to run (e.g., loot rooms).
    /// </summary>
    List<OptionalPass> OptionalRoomPasses { get; set; }

    /// <summary>
    /// Connector sealing passes.
    /// </summary>
    List<IGenPass> ConnectorSealingPasses { get; set; }

    /// <summary>
    /// Content passes (doors, plugs, enemies, loot, etc.).
    /// </summary>
    List<IGenPass> ContentPasses { get; set; }

    ScoringSystem scoringSystem { get; set; }

    string GenerateStationName(string faction);

    string dungeonName { get; set; }

    /// <summary>
    /// Square units of dungeon area per expected enemy spawn.
    /// Lower values produce denser enemy populations.
    /// </summary>
    float AreaPerEnemy { get; set; }
}
