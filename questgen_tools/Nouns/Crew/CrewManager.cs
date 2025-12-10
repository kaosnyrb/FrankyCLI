using FrankyCLI.questgen_tools.Interfaces;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools.Nouns.Crew
{
    public class CrewManager
    {
        public static IFormLink<IStarfieldMajorRecordGetter> GetCrew(string Faction, string ShipName)
        {

            List<ICrew> crews = new List<ICrew>()
            {
                new NamedFactionCrew(),
                new NamedFactionCrew_Betrayal(),
                new GenericCrew()
            };
            return crews[RandomUtils.random.Next(crews.Count)].GetCrewFormList(Faction, ShipName);
        }
    }
}
