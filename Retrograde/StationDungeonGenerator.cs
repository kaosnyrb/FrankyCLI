using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde;
using FrankyCLI.Retrograde.Passes;
using FrankyCLI.Retrograde.StationDesigns;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI
{
    public class StationDungeonGenerator
    {
        /*
        Assembles prefabs into a dungeons.

        Each Room is labelled so:
        rg_<tileset>_<roomtype>_<variant>

        egs
        rg_station_corridor_01
        rg_station_deadend_01
        rg_industrial_room_small_02
        rg_research_lab_medium_01
        rg_habitation_sleep_quarters_03

        Each prefab has various markers inside it.

        First is the connectors, these are used to build the topolgy of the dungeon:
        rg_conn_<dir>_<door>_<tileset>[_<flags>]

        eg:
        rg_conn_n_D1_station
        rg_conn_s_D1_station
        rg_conn_e_D3_military_airlock
        rg_conn_w_D2_derelict_damaged

        The last part are the slots. These represent the contents of the room and are assigned at build.
        We do a two pass approach, first we layout the rooms then we fill them with stuff.
        Each tag has a form list that of the same name which contains the prefabs that can be there.
        

        eg:
        rg_slot_room_feature
        rg_slot_crate_large
        rg_slot_loot_rare
        rg_slot_enemy_guard
        rg_slot_clutter_large
        rg_slot_light_main
        */

        IStationDesign stationDesign;


        public StationDungeonGenerator(IStationDesign design) {
            stationDesign = design;
        }

        public void GenerateDungeon(Cell cell, Location location, string faction, string size)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            bool harnesstest = false;
            //
            if (harnesstest)
            {
                var harness = new OreStationWeightHarness(designFactory: (() => new HabStation()));
                harness.FindBest(cell, location, runs: 10);
                stopwatch.Stop();
                Console.WriteLine("Harness Time:" + stopwatch.Elapsed);
                return;
            }

            DungeonState state = new DungeonState(cell, location)
            {
                Faction = faction,
                Size = size,
                TrunkRoomLists = new List<string> { "rg_trunklist" },
                scoringSystem = stationDesign.scoringSystem,
                passes = stationDesign.stationPasses,
                stateName = stationDesign.dungeonName,
                AreaPerEnemy = stationDesign.AreaPerEnemy
            };
            state.BridgePrefabKeys = BridgeUtil.BuildBridgePrefabKeys(state.TrunkRoomLists, state.GetRoomUtils);

            foreach (IGenPass pas in state.passes)
            {
                pas.RunPass(state);
            }

            state.PlacementUtil.Finalise();

            stopwatch.Stop();

            Console.WriteLine("Station Generation Time:"  + stopwatch.Elapsed);
        }
    }
}
