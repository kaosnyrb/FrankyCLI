using Retrograde.Passes;
using Retrograde.StationDesigns;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Retrograde.Generator;

/// <summary>
/// Assembles prefabs into space station dungeons.
///
/// Each Room is labelled so:
/// rg_[tileset]_[roomtype]_[variant]
///
/// Examples:
/// rg_station_corridor_01
/// rg_station_deadend_01
/// rg_industrial_room_small_02
/// rg_research_lab_medium_01
/// rg_habitation_sleep_quarters_03
///
/// Each prefab has various markers inside it.
///
/// First is the connectors, these are used to build the topology of the dungeon:
/// rg_conn_[dir]_[door]_[tileset][_flags]
///
/// Examples:
/// rg_conn_n_D1_station
/// rg_conn_s_D1_station
/// rg_conn_e_D3_military_airlock
/// rg_conn_w_D2_derelict_damaged
///
/// The last part are the slots. These represent the contents of the room and are assigned at build.
/// We do a two pass approach, first we layout the rooms then we fill them with stuff.
/// Each tag has a form list of the same name which contains the prefabs that can be there.
///
/// Examples:
/// rg_slot_room_feature
/// rg_slot_crate_large
/// rg_slot_loot_rare
/// rg_slot_enemy_guard
/// rg_slot_clutter_large
/// rg_slot_light_main
/// </summary>
public class StationDungeonGenerator
{
    private readonly IStationDesign stationDesign;

    public StationDungeonGenerator(IStationDesign design)
    {
        stationDesign = design;
    }

    /// <summary>
    /// Generates a complete dungeon in the specified cell.
    /// </summary>
    /// <param name="cell">The cell to generate the dungeon in.</param>
    /// <param name="location">The location record for the dungeon.</param>
    /// <param name="faction">The faction controlling the station.</param>
    /// <param name="size">The size category (Small, Medium, Large).</param>
    public void GenerateDungeon(Cell cell, Location location, string faction, string size)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        DungeonState state = new DungeonState(cell, location)
        {
            Faction = faction,
            Size = size,
            TrunkRoomLists = new List<string> { "rg_trunklist" },
            scoringSystem = stationDesign.scoringSystem,
            stateName = stationDesign.dungeonName,
            AreaPerEnemy = stationDesign.AreaPerEnemy
        };
        state.BridgePrefabKeys = BridgeUtil.BuildBridgePrefabKeys(state.TrunkRoomLists, state.GetRoomUtils);

        // Run main room passes
        foreach (IGenPass pass in stationDesign.MainRoomPasses)
        {
            pass.RunPass(state);
        }

        // Run optional room passes (chance-based)
        foreach (OptionalPass optPass in stationDesign.OptionalRoomPasses)
        {
            if (RandomProvider.Random.NextDouble() < optPass.Chance)
            {
                optPass.Pass.RunPass(state);
            }
        }

        // Run connector sealing passes
        foreach (IGenPass pass in stationDesign.ConnectorSealingPasses)
        {
            pass.RunPass(state);
        }

        // Run content passes
        foreach (IGenPass pass in stationDesign.ContentPasses)
        {
            pass.RunPass(state);
        }

        state.PlacementUtil.Finalise();

        stopwatch.Stop();
        Console.WriteLine("Station Generation Time:" + stopwatch.Elapsed);
    }
}
