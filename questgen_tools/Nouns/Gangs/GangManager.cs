
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools.Utils
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

            //We generate when we create them.
            switch(gangs[RandomUtils.random.Next(gangs.Count)])
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
