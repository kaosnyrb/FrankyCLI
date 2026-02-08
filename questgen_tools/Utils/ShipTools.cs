using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using FrankyCLI.Utils;

namespace FrankyCLI.questgen_tools;

/// <summary>
/// Ship tools that depend on gen_quest_main.
/// Pure data methods are in Retrograde.Utils.ShipTools.
/// </summary>
public static class ShipToolsEx
{
    public static IStarfieldMajorRecordGetter GetFactionShipChance(string faction)
    {
        return faction switch
        {
            "Spacer" => gen_quest_main._StarfieldMod.GenericBaseForms[FormKeyLookup.GetFormKey("LvlShip_Spacer_Combat")],
            "Ecliptic" => gen_quest_main._StarfieldMod.GenericBaseForms[FormKeyLookup.GetFormKey("LvlShip_Ecliptic_Combat")],
            "Crimsonfleet" => gen_quest_main._StarfieldMod.GenericBaseForms[FormKeyLookup.GetFormKey("LvlShip_CrimsonFleet_Combat")],
            "Varuun" => gen_quest_main._StarfieldMod.GenericBaseForms[FormKeyLookup.GetFormKey("LvlShip_HouseVaruun_Combat")],
            _ => gen_quest_main._StarfieldMod.GenericBaseForms[FormKeyLookup.GetFormKey("LvlShip_Spacer_Combat")]
        };
    }

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

        foreach (var record in gen_quest_main.myMod.EnumerateMajorRecords())
        {
            if (record.EditorID != null && record.EditorID.Contains(ganglistEditorID))
            {
                return record.ToLink<IStarfieldMajorRecordGetter>();
            }
        }
        return null;
    }
}
