using Retrograde.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Nouns
{
    public class GangManager
    {
        /// <summary>
        /// Factory delegate for creating gang instances.
        /// This must be set by the consuming application before calling GetGang().
        /// </summary>
        public static Func<string, IGang> GangFactory { get; set; }

        public static IGang GetGang()
        {
            List<string> gangs = new List<string>()
            {
                "NamedStreetGang",
                "NamedMercenaryGang",
                "RogueExMilitaryUnit",
                "StarshipWreckSalvagerCrew",
                "StreetGang",
                "SalvageCrew",
            };

            var gangType = gangs[RandomProvider.Random.Next(gangs.Count)];

            if (GangFactory != null)
            {
                return GangFactory(gangType);
            }

            throw new InvalidOperationException("GangManager.GangFactory must be set before calling GetGang()");
        }
    }
}
