using FrankyCLI.Retrograde.Passes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Retrograde.StationDesigns
{
    public class IStationDesign
    {
        public List<IGenPass> stationPasses;
        public ScoringSystem scoringSystem;
    }

    
}
