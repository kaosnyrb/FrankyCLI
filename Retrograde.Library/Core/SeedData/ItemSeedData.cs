using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public static class ItemSeedData
    {
        /// <summary>
        /// Names for clue objects the player picks up to advance the investigation.
        /// 2-3 words, plain and concrete — document/data artefact style.
        /// </summary>
        public static readonly List<string> ClueItemNames = new List<string>
        {
            "Navigation Data",
            "Cargo Manifest",
            "Transit Log",
            "Personnel File",
            "Station Records",
            "Encrypted Message",
            "Shipping Ledger",
            "Access Credentials",
            "System Log",
            "Maintenance Report",
            "Inventory Record",
            "Transmission Log",
            "Flight Plan",
            "Dock Authorization",
            "Import Declaration",
            "Signal Intercept",
            "Contact List",
            "Financial Record",
            "Survey Data",
            "Expedition Notes",
            "Crew Roster",
            "Fuel Log",
            "Cargo Receipt",
            "Waypoint Data",
            "Operations Report",
        };

        /// <summary>
        /// Names for contraband items the player must destroy.
        /// Illicit goods, tampered records, black-market artefacts.
        /// </summary>
        public static readonly List<string> ContrabandItemNames = new List<string>
        {
            "Stolen Data Core",
            "Forged Manifests",
            "Contraband Cache",
            "Black Market Goods",
            "Illegal Cargo",
            "Bootleg Supplies",
            "Tampered Records",
            "Illicit Data Shard",
            "Counterfeit Credentials",
            "Pirated Software",
            "Stolen Tech",
            "Smuggled Goods",
            "Corrupted Evidence",
            "Embezzled Assets",
            "Unlicensed Hardware",
            "Tainted Supplies",
            "Falsified Data",
            "Black Market Cache",
            "Stolen Prototype",
            "Restricted Materials",
        };
    }
}
