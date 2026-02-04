using DynamicData;
using FrankyCLI.questgen_tools.Interfaces;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;

namespace FrankyCLI.Retrograde.FactionMembers
{
    public interface IFactionMembers
    {

        public Npc GetCrewMember(string Room);

        public Npc GetBoss(string Room);



        IFormLinkNullable<IOutfitGetter> GetLowRank_Outfit();
        IFormLinkNullable<IOutfitGetter> GetHighRank_Outfit();
        IFormLinkNullable<IOutfitGetter> GetBoss_Outfit();

        IFormLinkNullable<IOutfitGetter> GetLowRank_SpaceOutfit();
        IFormLinkNullable<IOutfitGetter> GetHighRank_SpaceOutfit();
        IFormLinkNullable<IOutfitGetter> GetBoss_SpaceOutfit();

        IFormLinkNullable<ILeveledItemGetter> GetLowRank_Gear();
        IFormLinkNullable<ILeveledItemGetter> GetHighRank_Gear();
        IFormLinkNullable<ILeveledItemGetter> GetBoss_Gear();

        string GetLowRank_Name();
        string GetHighRank_Name();
        string GetBoss_Name();

    }
}
