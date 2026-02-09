using System;
using System.Collections.Generic;

namespace Retrograde.Utils;

/// <summary>
/// Provides random faction name selection for Starfield universe.
/// </summary>
public static class FactionTools
{
    public static string GetFaction()
    {
        var random = RandomProvider.Random;

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
