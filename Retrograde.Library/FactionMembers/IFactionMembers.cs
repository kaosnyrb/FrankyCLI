using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;

namespace Retrograde.FactionMembers;

/// <summary>
/// Interface for faction-specific crew member and boss creation.
/// Implementations provide the NPCs, outfits, gear, and names for a given faction.
/// </summary>
public interface IFactionMembers
{
    Npc GetCrewMember(string room);
    Npc GetBoss(string room);
    Npc GetHighLevelTarget();

    IFormLinkNullable<IOutfitGetter> GetLowRank_Outfit();
    IFormLinkNullable<IOutfitGetter> GetHighRank_Outfit();
    IFormLinkNullable<IOutfitGetter> GetBoss_Outfit();

    IFormLinkNullable<ILeveledItemGetter> GetLowRank_Gear();
    IFormLinkNullable<ILeveledItemGetter> GetHighRank_Gear();
    IFormLinkNullable<ILeveledItemGetter> GetBoss_Gear();

    string GetLowRank_Name();
    string GetHighRank_Name();
    string GetBoss_Name();
}
