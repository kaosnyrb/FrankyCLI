using FrankyCLI.Retrograde.Passes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Retrograde.StationDesigns
{
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
        public string GenerateStationName(string Faction);

        public string dungeonName {  get; set; }

        /// <summary>
        /// Square units of dungeon area per expected enemy spawn.
        /// Lower values produce denser enemy populations.
        /// </summary>
        float AreaPerEnemy { get; set; }
    }


}
