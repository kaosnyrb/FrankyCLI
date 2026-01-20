using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde;
using FrankyCLI.Retrograde.Passes;
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
               
        public StationDungeonGenerator() {

        }



        public void GenerateDungeon(Cell cell, Location location, string faction, string size)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            DungeonState state = new DungeonState(cell, location)
            {
                Faction = faction,
                Size = size,
                TrunkRoomLists = new List<string> { "rg_trunklist" },
                scoringSystem = new ScoringSystem()
                {
                    BridgingWeight = 10,
                    BridgingOverlapWeight = -1,
                    NorthBiasWeight = 0.8,
                    NewConnectorsWeight = 0.75,
                    PlacementWeight = 50,
                    AreaWeight = -0.25,
                    ClusteringWeight = 2,
                    SizeDiversityWeight = 5,
                    RoomReuseWeight = -1,
                    ConnectorViabilityWeight = 0.75,
                    Effort = 100
                },
                //Multi-Pass Generation Pipeline
                passes = new List<IGenPass>
                {
                //Place rooms
                    new TrunkTopologyPass(4),
                    new BossTopologyPass("boss"),
                    new DistrictTopologyPass("rg_hablist",4),
                    new BridgeHelperPass(),
                    new BridgingTopologyPass(),
                    new UtilTopologyPass("rg_utillist",0.8f),
                //Seal connectors
                    new WindowSealingPass(),
                    new ConnectorSealingPass(),
                //Doors and plugs
                    new PlugPass(),
                    new DoorPass(),
                //Fill content
                    new EnemyPass(),
                    new ContentPass(),
                    new ShipMarkerPass(),
                //util
                    new LightOccluderPass()
                }
            };
            state.BridgePrefabKeys = BridgeUtil.BuildBridgePrefabKeys(state.TrunkRoomLists, state.GetRoomUtils);

            foreach (IGenPass pas in state.passes)
            {
                pas.RunPass(state);
            }

            stopwatch.Stop();

            Console.WriteLine("Station Generation Time:"  + stopwatch.Elapsed);
        }
    }
}
