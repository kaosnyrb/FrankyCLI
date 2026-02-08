using Retrograde.Interfaces;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Nouns
{
    public class CrewManager
    {
        /// <summary>
        /// Factory delegate for creating crew instances.
        /// This must be set by the consuming application before calling GetCrew().
        /// </summary>
        public static Func<ICrew>[] CrewFactories { get; set; }

        public static IFormLink<IStarfieldMajorRecordGetter> GetCrew(string Faction, string ShipName)
        {
            if (CrewFactories == null || CrewFactories.Length == 0)
            {
                throw new InvalidOperationException("CrewManager.CrewFactories must be set before calling GetCrew()");
            }

            var crew = CrewFactories[RandomProvider.Random.Next(CrewFactories.Length)]();
            return crew.GetCrewFormList(Faction, ShipName);
        }
    }
}
