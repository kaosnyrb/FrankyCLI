using Retrograde.Interfaces;
using Retrograde.Nouns.Gangs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Nouns
{
    public class GangManager
    {
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

            Random rand = RandomProvider.Random;

            //We generate when we create them.
            switch (gangs[rand.Next(gangs.Count)])
            {
                case "NamedStreetGang":
                    return new NamedStreetGang();
                case "NamedMercenaryGang":
                    return new NamedMercenaryGang();
                case "RogueExMilitaryUnit":
                    return new RogueExMilitaryUnit();
                case "StarshipWreckSalvagerCrew":
                    return new StarshipWreckSalvagerCrew();
                case "StreetGang":
                    return new StreetGang();
                case "SalvageCrew":
                    return new SalvageCrew();

                default:
                    return new NamedStreetGang();
            }
        }
    }
}
