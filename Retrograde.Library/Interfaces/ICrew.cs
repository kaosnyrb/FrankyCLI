using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Interfaces
{
    public interface ICrew
    {
        public IFormLink<IStarfieldMajorRecordGetter> GetCrewFormList(string Faction, string ShipName);
    }
}
