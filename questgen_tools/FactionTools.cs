using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public class FactionTools
    {
        public static string GetFaction()
        {
            Random random = new Random();

            List<string> factions = new List<string>()
            {
                "Crimson Fleet",
                "Spacer",
                "Ecliptic",
                "UC Navy",
                "UC Vanguard",
                "Freestar Security",
                "UC SysDef",
                "Trade Authority",
                "Galbank",
                "Trackers Alliance",
                "House Va'ruun",
                "United Colonies",
                "Freestar Collective",
                "Deimos Staryards Inc",
                "HopeTech",
                "Ryujin Industries",
                "Stroud-Eklund",
                "Taiyō Astroneering",
                "League of Independent Settlers",
                "Arc Might",
                "Advanced Nutrition",
                "Allied Armaments",
                "Arboron",
                "Argos Extractors",
                "CAN-uck!",
                "Centauri Mills",
                "Chunks",
                "CombaTech",
                "Kore Kinetics",
                "Laredo",
                "Paradiso Group",
                "Protectorate Systems",
                "Red Harvest",
                "Reliant Medical",
                "Slayton Aerospace",
                "TerraBrew Coffee",
                "Tranquilitea",
                "Trident Luxury Lines",
                "Xenofresh Fisheries"
            };

            return factions[random.Next(factions.Count)];
        }
    }
}
