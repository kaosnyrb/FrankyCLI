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
        List<IGenPass> stationPasses { get; set; }
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
