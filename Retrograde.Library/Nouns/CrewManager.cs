using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde.Interfaces;
using Retrograde.Nouns.Crew;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Nouns
{
    public class CrewManager
    {
        public static IFormLink<IStarfieldMajorRecordGetter> GetCrew(string Faction, string ShipName)
        {
            Random rand = RandomProvider.Random;
            List<ICrew> crews = new List<ICrew>()
            {
                new NamedFactionCrew(),
                new NamedFactionCrew_Betrayal(),
                new GenericCrew()
            };
            return crews[rand.Next(crews.Count)].GetCrewFormList(Faction, ShipName);
        }
    }
}
