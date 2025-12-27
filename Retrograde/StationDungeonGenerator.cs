using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde;
using FrankyCLI.Retrograde.Passes;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
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

        We have a formlist called rg_roomlist which contains a link to all the formlists for the tilesets.
        Eg
        rg_roomlist_station

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

        public static int YBound = 40;

        
        public StationDungeonGenerator() {

        }



        public void GenerateDungeon(Cell cell, string theme)
        {
            DungeonState state = new DungeonState(cell);

            //Multi-Pass Generation Pipeline
            List<IGenPass> passes = new List<IGenPass>
            {
                new SpineTopologyPass(),
                new DistrictTopologyPass(),
                new ConectorSealingPass(),
                new ContentPass(),
                new LightOccluderPass()
            };

            foreach (IGenPass pas in passes)
            {
                pas.RunPass(state);
            }
        }
    }
}
