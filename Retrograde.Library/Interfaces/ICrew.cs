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
        /// <summary>
        /// Returns the crew FormList link, the NPC designated as the dataslate speaker
        /// (the last crew member created, who carries the log entry), and whether that NPC is female.
        /// </summary>
        public (IFormLink<IStarfieldMajorRecordGetter> crewList, Npc speaker, bool isFemale) GetCrewFormList(string Faction, string ShipName);
    }
}
