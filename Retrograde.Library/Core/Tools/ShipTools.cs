using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;

namespace Retrograde.Utils;

/// <summary>
/// Provides ship FormIDs and name generation for Starfield factions.
/// </summary>
public static class ShipTools
{
    public static uint GetCargoShip()
    {
        var random = RandomProvider.Random;
        List<uint> shiplist = new List<uint>()
        {
            0x0018D3E2, 0x0018D3E4, 0x0018D3E6, // EncShip_UCCitizen_A_Cargo_MULE
            0x0002C8AA, 0x0002CA6B, 0x0002CAEC, // EncShip_UCCitizen_B_Cargo_CarryAll
            0x0002CB1E, 0x0002CB21, // EncShip_UCCitizen_B_Cargo_Pelican
            0x0002CAFA, 0x0002CB15, 0x0002CB1B, // EncShip_UCCitizen_B_Cargo_SpaceOx
            0x00331AC7, 0x00333D9A, 0x00333D9C, // EncShip_TradeAuthority_A_Atlas
            0x00333D9E, 0x00333E89, 0x000423B1, // EncShip_TradeAuthority_A_Railstar
            0x00347145, 0x0034B5C4, 0x0034B5C7, // EncShip_TradeAuthority_B_WagonTrain
            0x0034B5CC, 0x0034B5F0, 0x0003CF96, // EncShip_TradeAuthority_C_Highlander
            0x00315877, 0x0031587B, 0x0031587D, // EncShip_StarParcel_A_Pikup
            0x0002C269, 0x0002C26B, 0x0002C2C0, // EncShip_StarParcel_B_Spacetruk
            0x0003CFAA, 0x0003CFA8, // EncShip_StarParcel_C
        };
        return shiplist[random.Next(shiplist.Count)];
    }

    public static uint GetAClassShip()
    {
        var random = RandomProvider.Random;
        List<uint> shiplist = new List<uint>()
        {
            0x0031DF00, 0x0031DF60, 0x00322BE1, // EncShip_CrimsonFleet_A
            0x003889AD, 0x003889E7, 0x00187C77, // EncShip_UC
            0x00322BF7, 0x00322BF1, // EncShip_Ecliptic_A
            0x002E74A4, 0x002E749E, 0x002E762D, 0x0004237F, 0x0018D3D5, // EncShip_FreestarCitizen_A
            0x000423AB, 0x000D0AA9, // EncShip_TheFirst_A
            0x00042376, 0x0004238B, // EncShip_UC/Freestar
            0x00322C13, 0x00322C15, 0x00322C17, // EncShip_Spacer_A
        };
        return shiplist[random.Next(shiplist.Count)];
    }

    public static uint GetBClassShip()
    {
        var random = RandomProvider.Random;
        List<uint> shiplist = new List<uint>()
        {
            0x00322BE9, 0x00322BEB, 0x00322BED, // EncShip_CrimsonFleet_B
            0x00322BFF, 0x00322C03, 0x00322C07, // EncShip_Ecliptic_B
            0x002E7530, 0x002E7611, 0x0002C201, 0x0018D3DD, // EncShip_FreestarCitizen_B
            0x00388977, //0x00388985, // EncShip_FreestarSecurity_B
            0x0030ED45, // EncShip_Galbank_B
            0x00323454, // EncShip_HouseVaruun_B
            0x00322C19, 0x00322C1B, 0x00322C1D, // EncShip_Spacer_B
            0x0002C88F, 0x0002C75D, // EncShip_TheFirst_B
            0x00331347, // EncShip_TrackersAlliance_B
            0x001E5E76, 0x001E5E79, 0x001E5E7B, 0x001E5E7D, 0x001E5E7F, // EncShip_UCNavy_B
        };
        return shiplist[random.Next(shiplist.Count)];
    }

    public static string GetFactionShipName(string id)
    {
        return id switch
        {
            "UC Navy" or "UC Vanguard" or "UC SysDef" => GetUCShipCode(),
            "Freestar Security" or "Trackers Alliance" => GetFreestarCodeName(),
            "Galbank" => GetGalBankCodeName(),
            "Crimson Fleet" => GetCrimsonFleetCodeName(),
            "Spacer" => GetSpacerCodeName(),
            "Ecliptic" => GetMercenaryCodeName(),
            "Trade Authority" => GetTradeAuthoritySailingCodeName(),
            _ => GetShipName()
        };
    }

    public static string GetShipName()
    {
        var random = RandomProvider.Random;
        List<string> prefixes = new List<string>
        {
            "Star", "Nova", "Nebula", "Galaxy", "Cosmic", "Solar", "Lunar", "Quantum", "Stellar",
            "Astro", "Meteor", "Comet", "Ion", "Photon", "Plasma", "Warp", "Hyper", "Proto", "Omega",
            "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zenith", "Apex", "Radiant", "Celestial"
        };
        List<string> suffixes = new List<string>
        {
            "Voyager", "Runner", "Spear", "Falcon", "Hawk", "Drifter", "Carrier", "Cruiser", "Ranger",
            "Explorer", "Warrior", "Sentinel", "Guardian", "Hunter", "Pioneer", "Nomad", "Lancer"
        };
        return prefixes[random.Next(prefixes.Count)] + " " + suffixes[random.Next(suffixes.Count)];
    }

    public static string GetUCShipCode()
    {
        var random = RandomProvider.Random;
        List<string> prefixes = new List<string> { "FT", "XR", "NV", "TS", "RG", "VT", "HM", "SK", "DF", "CW" };
        return $"{prefixes[random.Next(prefixes.Count)]}-{random.Next(10, 9999)}";
    }

    public static string GetFreestarCodeName()
    {
        var random = RandomProvider.Random;
        List<string> prefixes = new List<string> { "FS", "FR", "FC", "WC", "LF", "RG", "MD", "PR" };
        List<string> names = new List<string>
        {
            "Dustrunner", "Ironwind", "Sundrifter", "Lonerider", "Redhawk", "Coyoteheart", "Brimstone",
            "Mustang", "Trailborn", "Dustfall", "Wildshot", "Longhorn", "Starforge", "Badlander"
        };
        return $"{prefixes[random.Next(prefixes.Count)]}-{random.Next(10, 9999)} {names[random.Next(names.Count)]}";
    }

    public static string GetCrimsonFleetCodeName()
    {
        var random = RandomProvider.Random;
        List<string> prefixes = new List<string> { "CF", "CR", "RS", "BL", "FM", "DS", "VK", "BR" };
        List<string> names = new List<string>
        {
            "Bloodreaver", "Skullrender", "Voidfang", "Ironmaw", "Redhowl", "Starcutlass",
            "Darkstrike", "Bloodwake", "Nightraider", "Riftclaw", "Dreadskull", "Havocborn"
        };
        return $"{prefixes[random.Next(prefixes.Count)]}-{random.Next(10, 9999)} {names[random.Next(names.Count)]}";
    }

    public static string GetSpacerCodeName()
    {
        var random = RandomProvider.Random;
        List<string> prefixes = new List<string> { "SP", "SB", "JT", "GX", "RD", "BR", "SL", "WK" };
        List<string> names = new List<string>
        {
            "Scrapfang", "Rustburner", "Junkrider", "Boltshank", "Grimejaw", "Wreckborn",
            "Filthrunner", "Trashfire", "Scrapmaw", "Rusthound", "Grindcore", "Crackspine"
        };
        return $"{prefixes[random.Next(prefixes.Count)]}-{random.Next(10, 9999)} {names[random.Next(names.Count)]}";
    }

    public static string GetMercenaryCodeName()
    {
        var random = RandomProvider.Random;
        List<string> prefixes = new List<string> { "MX", "VR", "KT", "HX", "OD", "ST", "BL", "AR", "GS", "RF" };
        List<string> names = new List<string>
        {
            "Blacktalon", "Ironstrike", "Ghostline", "Nightblade", "Steelshade", "Warhawk",
            "Shadowfall", "Hardpoint", "Crossfire", "Deadlock", "Viperline", "Stormpoint"
        };
        return $"{prefixes[random.Next(prefixes.Count)]}-{random.Next(10, 9999)} {names[random.Next(names.Count)]}";
    }

    public static string GetGalBankCodeName()
    {
        var random = RandomProvider.Random;
        List<string> prefixes = new List<string> { "GB", "GC", "GF", "GL", "GA", "BX", "CB", "CR" };
        List<string> names = new List<string>
        {
            "Vaultguard", "Credshield", "Fundrunner", "Mintline", "Bondrider", "Securehaul",
            "Depositrail", "Ledgerstar", "Goldshift", "Coinlance", "Trustwave", "Cashflow"
        };
        return $"{prefixes[random.Next(prefixes.Count)]}-{random.Next(10, 9999)} {names[random.Next(names.Count)]}";
    }

    public static string GetTradeAuthoritySailingCodeName()
    {
        var random = RandomProvider.Random;
        List<string> names = new List<string>
        {
            "Windward", "Starborne", "Wayfarer", "Goldenwake", "Mariner", "Seafarer",
            "Voyager", "Windcrier", "Sunchaser", "Tidebreaker", "Silverwake", "Harbormark"
        };
        List<string> prefixes = new List<string> { "TA", "TB", "TR", "LT", "CT", "AX" };
        return $"{names[random.Next(names.Count)]} {prefixes[random.Next(prefixes.Count)]}-{random.Next(10, 9999)}";
    }

    public static uint GetFactionID(string faction)
    {
        return faction switch
        {
            "Crimson Fleet" => 0x000B1375,
            "CrimsonFleet" => 0x000B1375,
            "Spacer" => 0x000B13A8,
            "Ecliptic" => 0x000AE4F3,
            "Varuun" => 0x000B19CF,
            "UC Navy" => 0x000D320E,
            "UC Vanguard" => 0x000D1859,
            "Freestar Security" => 0x000CA78D,
            "UC SysDef" => 0x000DBC51,
            "Trade Authority" => 0x000AE4D0,
            "Galbank" => 0x0034BB12,
            "Trackers Alliance" => 0x000AE4D3,
            _ => 0
        };
    }

    /// <summary>
    /// Gets the faction ship chance leveled list from Starfield.esm.
    /// </summary>
    public static IStarfieldMajorRecordGetter GetFactionShipChance(string faction)
    {
        string editorId = faction switch
        {
            "Spacer" => "LvlShip_Spacer_Combat",
            "Ecliptic" => "LvlShip_Ecliptic_Combat",
            "Crimsonfleet" => "LvlShip_CrimsonFleet_Combat",
            "Varuun" => "LvlShip_HouseVaruun_Combat",
            _ => "LvlShip_Spacer_Combat"
        };

        foreach (var rec in RetrogradeContext.Current.StarfieldMod.GenericBaseForms)
        {
            if (rec.EditorID == editorId)
                return rec;
        }

        throw new Exception($"Could not find ship chance for faction: {faction}");
    }

    /// <summary>
    /// Gets (or creates) a gang members form list for the specified ship faction.
    /// Creates a copy in the target mod to avoid template dependencies.
    /// </summary>
    public static IFormLink<IStarfieldMajorRecordGetter>? GetGangList(uint ShipFaction)
    {
        string ganglistEditorID = ShipFaction switch
        {
            0x000AE4F3 => "duout_GangMembersList_Space_Ecliptic",     // LShip_Ecliptic_Template
            0x000B1375 => "duout_GangMembersList_Space_Crimsonfleet", // LShip_CrimsonFleet_Template
            0x000B13A8 => "duout_GangMembersList_Space_Spacer",       // LShip_Spacer_Template
            0x000B19CF => "duout_GangMembersList_Space_Varuun",       // LShip_HouseVaruun_Template
            _ => "duout_GangMembersList_Space_Spacer"
        };

        var targetMod = RetrogradeContext.Current.TargetMod;

        // Return already-created copy if present
        string newEditorID = "frmlist_ganglist_" + ganglistEditorID;
        var existing = targetMod.FormLists.FirstOrDefault(fl => fl.EditorID == newEditorID);
        if (existing != null)
            return existing.ToLink<IStarfieldMajorRecordGetter>();

        // Find the template formlist
        IFormListGetter? templateList = null;
        foreach (var tm in RetrogradeContext.Current.TemplateMods)
        {
            templateList = tm.FormLists.FirstOrDefault(fl => fl.EditorID != null && fl.EditorID.Contains(ganglistEditorID));
            if (templateList != null) break;
        }

        if (templateList == null) return null;

        // Create a new formlist in the target mod copying items from the template
        var newList = new FormList(targetMod)
        {
            EditorID = newEditorID,
            Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
        };

        foreach (var item in templateList.Items)
            newList.Items.Add(item);

        targetMod.FormLists.Add(newList);
        return newList.ToLink<IStarfieldMajorRecordGetter>();
    }
}
