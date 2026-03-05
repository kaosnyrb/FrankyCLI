using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Utils;

public struct CreatedNpcResult
{
    public Npc Npc;
    public string VoiceEditorId;
    public bool IsFemale;
}

/// <summary>
/// Provides NPC template FormIDs and utility methods for Starfield NPCs.
/// </summary>
public static class NPCTools
{
    /// <summary>
    /// Gets a random template NPC FormID for the specified gender.
    /// </summary>
    public static uint GetTemplateNPC(bool female)
    {
        var random = RandomProvider.Random;

        if (female)
        {
            List<uint> npclist = new List<uint>()
            {
                0x000818, 0x000856, 0x000857, 0x000858, 0x00085C,
                0x00085D, 0x00085E, 0x00085F, 0x000860, 0x000861,
            };
            return npclist[random.Next(npclist.Count)];
        }
        else
        {
            List<uint> npclist = new List<uint>()
            {
                0x000826, 0x000862, 0x000863, 0x000865,
                0x000866, 0x000867, 0x000868,
            };
            return npclist[random.Next(npclist.Count)];
        }
    }

    /// <summary>
    /// Gets a random dead template NPC FormID for the specified gender.
    /// </summary>
    public static uint GetTemplateDeadNPC(bool female)
    {
        var random = RandomProvider.Random;

        if (female)
        {
            List<uint> npclist = new List<uint>()
            {
                0x000902, 0x00090A, 0x00090B, 0x00090C, 0x000912,
                0x000915, 0x000918, 0x000919, 0x00091A, 0x00091B,
            };
            return npclist[random.Next(npclist.Count)];
        }
        else
        {
            List<uint> npclist = new List<uint>()
            {
                0x000904, 0x00091C, 0x00091D, 0x000920,
                0x000921, 0x000922, 0x000923,
            };
            return npclist[random.Next(npclist.Count)];
        }
    }

    private static INpcGetter FindNpcById(uint id)
    {
        var ctx = RetrogradeContext.Current;
        var targetMod = ctx.TargetMod;
        INpcGetter? npc = targetMod.Npcs.FirstOrDefault(r => r.FormKey == new FormKey(targetMod.ModKey, id));
        if (npc == null)
        {
            foreach (var tm in ctx.TemplateMods)
            {
                npc = tm.Npcs.FirstOrDefault(r => r.FormKey == new FormKey(tm.ModKey, id));
                if (npc != null) break;
            }
        }
        npc ??= ctx.StarfieldMod.Npcs.FirstOrDefault(r => r.FormKey == new FormKey(ctx.StarfieldModKey, id));
        if (npc == null)
            throw new KeyNotFoundException($"NPCTools: no Npc with raw ID 0x{id:X6} found in target mod, template mods, or Starfield.esm.");
        return npc;
    }

    public static Npc FindTemplateNpc(bool female) => FindNpcById(GetTemplateNPC(female)).DeepCopy();
    public static Npc FindTemplateDeadNpc(bool female) => FindNpcById(GetTemplateDeadNPC(female)).DeepCopy();
    public static Npc FindTemplateFriendlyNpc(bool female) => FindNpcById(GetTemplateNPC(female)).DeepCopy();

    /// <summary>
    /// Imports a non-vanilla faction into targetMod (new FormKey, all fields copied).
    /// Deduplicates by EditorID so the same faction is only created once per run.
    /// Returns the new FormKey to use in the NPC's Factions list.
    /// </summary>
    private static FormKey ImportFaction(IFactionGetter source, StarfieldMod targetMod)
    {
        // Deduplicate: if already imported this run, return its FormKey
        if (source.EditorID != null)
        {
            var existing = targetMod.Factions.FirstOrDefault(f => f.EditorID == source.EditorID);
            if (existing != null) return existing.FormKey;
        }

        var faction = new Faction(targetMod)
        {
            EditorID                  = source.EditorID,
            Name                      = source.Name?.DeepCopy(),
            Flags                     = source.Flags,
            StarfieldMajorRecordFlags = source.StarfieldMajorRecordFlags,
            Relations                 = source.Relations.Select(x => x.DeepCopy()).ToExtendedList(),
            Components                = source.Components.Select(x => x.DeepCopy()).ToExtendedList(),
            CrimeValues               = source.CrimeValues?.DeepCopy(),
            Prisons                   = source.Prisons?.Select(x => x.DeepCopy()).ToExtendedList(),
            Ranks                     = source.Ranks.Select(x => x.DeepCopy()).ToExtendedList(),
            VendorValues              = source.VendorValues?.DeepCopy(),
            VendorLocation            = source.VendorLocation?.DeepCopy(),
            Conditions                = source.Conditions?.Select(x => x.DeepCopy()).ToExtendedList(),
            Herd                      = source.Herd?.DeepCopy(),
            FormationRadius           = source.FormationRadius,
        };
        // FormLinkNullable fields must be set after construction
        if (!source.Keyword.IsNull)              faction.Keyword              = source.Keyword.FormKey.ToNullableLink<IKeywordGetter>();
        if (!source.SharedCrimeFactionList.IsNull) faction.SharedCrimeFactionList = source.SharedCrimeFactionList.FormKey.ToNullableLink<IFormListGetter>();
        if (!source.VendorBuySellList.IsNull)    faction.VendorBuySellList    = source.VendorBuySellList.FormKey.ToNullableLink<IFormListGetter>();
        if (!source.MerchantContainer.IsNull)    faction.MerchantContainer    = source.MerchantContainer.FormKey.ToNullableLink<IPlacedObjectGetter>();
        if (!source.VoiceType.IsNull)            faction.VoiceType            = source.VoiceType.FormKey.ToNullableLink<IVoiceTypeOrListGetter>();

        targetMod.Factions.Add(faction);
        return faction.FormKey;
    }

    /// <summary>
    /// For each non-vanilla faction on the NPC, imports it into targetMod and updates
    /// the NPC's Factions list to reference the new FormKey.
    /// </summary>
    private static void EnsureFactionsInMod(Npc npc, StarfieldMod targetMod)
    {
        if (npc.Factions == null || npc.Factions.Count == 0) return;
        if (!RetrogradeContext.IsInitialized) return;

        var ctx = RetrogradeContext.Current;
        var starfieldModKey = ctx.StarfieldModKey;

        for (int i = 0; i < npc.Factions.Count; i++)
        {
            var rp = npc.Factions[i];
            var fk = rp.Faction.FormKey;
            if (fk.IsNull) continue;
            if (fk.ModKey == starfieldModKey) continue;  // vanilla — always reachable
            if (fk.ModKey == targetMod.ModKey) continue; // already in target mod

            IFactionGetter? source = null;
            foreach (var tm in ctx.TemplateMods)
            {
                source = tm.Factions.FirstOrDefault(f => f.FormKey == fk);
                if (source != null) break;
            }

            if (source == null)
            {
                Console.WriteLine($"[NPCTools] Warning: faction {fk} not found in any template mod — skipping import.");
                continue;
            }

            var newFk = ImportFaction(source, targetMod);
            npc.Factions[i] = new RankPlacement { Faction = newFk.ToLink<IFactionGetter>(), Rank = rp.Rank };
        }
    }

    /// <summary>
    /// Creates a deep copy of an NPC in the specified mod. Also imports any non-vanilla
    /// factions referenced by the NPC into targetMod if they are not already present.
    /// </summary>
    public static Npc CloneNPC(StarfieldMod myMod, Npc NPC, bool respawn = false)
    {
        var flags = NPC.Flags;
        if (respawn)
        {
            flags |= Npc.Flag.Respawn;
        }
        var npc = new Npc(myMod)
        {
            EditorID = "npc_" + Guid.NewGuid().ToString().Substring(0, 8),
            ObjectBounds = NPC.ObjectBounds,
            AttackRace = NPC.AttackRace,
            ActorEffect = NPC.ActorEffect,
            AttachParentSlots = NPC.AttachParentSlots,
            BodyMorphRegionValues = NPC.BodyMorphRegionValues,
            CalcMaxLevel = NPC.CalcMaxLevel,
            CalcMinLevel = NPC.CalcMinLevel,
            Class = NPC.Class,
            CalculatedHealth = NPC.CalculatedHealth,
            CombatStyle = NPC.CombatStyle,
            Components = NPC.Components,
            DefaultOutfit = NPC.DefaultOutfit,
            EnergyLevel = NPC.EnergyLevel,
            FaceMorphs = NPC.FaceMorphs,
            EyeColor = NPC.EyeColor,
            CrimeFaction = NPC.CrimeFaction,
            Assistance = NPC.Assistance,
            ActivateTextOverride = NPC.ActivateTextOverride,
            CalculatedActionPoints = NPC.CalculatedActionPoints,
            CombatOverridePackageList = NPC.CombatOverridePackageList,
            Aggression = NPC.Aggression,
            CompanionInfoDialogue = NPC.CompanionInfoDialogue,
            CompanionInfoQuest = NPC.CompanionInfoQuest,
            Confidence = NPC.Confidence,
            DeathItem = NPC.DeathItem,
            DefaultPackageList = NPC.DefaultPackageList,
            DefaultTemplate = NPC.DefaultTemplate,
            DispositionBase = NPC.DispositionBase,
            EyebrowColor = NPC.EyebrowColor,
            FaceDialPositions = NPC.FaceDialPositions,
            FacialHairColor = NPC.FacialHairColor,
            Factions = NPC.Factions,
            FarAwayModelDistance = NPC.FarAwayModelDistance,
            Flags = flags,
            FLEE = NPC.FLEE,
            ForcedLocations = NPC.ForcedLocations,
            FormationFaction = NPC.FormationFaction,
            HairColor = NPC.HairColor,
            HeadParts = NPC.HeadParts,
            HeightMax = NPC.HeightMax,
            HeightMin = NPC.HeightMin,
            GearedUpWeapons = NPC.GearedUpWeapons,
            Items = NPC.Items,
            JewelryColor = NPC.JewelryColor,
            LegendaryChance = NPC.LegendaryChance,
            Level = NPC.Level,
            Keywords = NPC.Keywords,
            LongName = NPC.LongName,
            ObjectTemplates = NPC.ObjectTemplates,
            MajorFlags = NPC.MajorFlags,
            NAM5 = NPC.NAM5,
            MorphBlends = NPC.MorphBlends,
            ONA2 = NPC.ONA2,
            Perks = NPC.Perks,
            SkinToneIndex = NPC.SkinToneIndex,
            Skin = NPC.Skin,
            Mood = NPC.Mood,
            Properties = NPC.Properties,
            RDSAs = NPC.RDSAs,
            Weight = NPC.Weight,
            Tints = NPC.Tints,
            SpaceOutfit = NPC.SpaceOutfit,
            TeethColor = NPC.TeethColor,
            Pronoun = NPC.Pronoun,
            UnknownAIDT = NPC.UnknownAIDT,
            Race = NPC.Race,
            XpValueOffset = NPC.XpValueOffset,
            VirtualMachineAdapter = NPC.VirtualMachineAdapter,
            Packages = NPC.Packages,
            XALG = NPC.XALG
        };
        EnsureFactionsInMod(npc, myMod);
        return npc;
    }

    /// <summary>
    /// Gets a random eye color name.
    /// </summary>
    public static string GetEyeColour()
    {
        var random = RandomProvider.Random;
        List<string> eyelist = new List<string>()
        {
            "Blue", "Brown", "Red", "Iron", "Grey", "Hazel", "Green", "Sulfur",
        };
        return eyelist[random.Next(eyelist.Count)];
    }

    /// <summary>
    /// Sanitizes hair color names to simpler descriptions.
    /// </summary>
    public static string SanitiseHairColor(string haircolor)
    {
        return haircolor switch
        {
            "DirtyBlonde" => "Blonde",
            "BlackBrown" => "Brown",
            "SaltAndBrown" => "Brown",
            "BrownDark" => "Brown",
            "SaltAndPepper" => "Greying",
            _ => haircolor
        };
    }

    /// <summary>
    /// Gets a random hair color name.
    /// </summary>
    public static string GetHairColour()
    {
        var random = RandomProvider.Random;
        List<string> hairlist = new List<string>()
        {
            "Jet", "DirtyBlonde", "BlackBrown", "Black", "Amber", "Copper",
            "Platinum", "SaltAndBrown", "BrownDark", "SaltAndPepper", "Blonde"
        };
        return hairlist[random.Next(hairlist.Count)];
    }

    /// <summary>
    /// Gets a random haircut FormKey for the specified gender.
    /// </summary>
    public static IFormLinkNullable<IHeadPartGetter> GetHaircut(bool female)
    {
        var random = RandomProvider.Random;
        if (female)
        {
            List<uint> hairlist = new List<uint>()
            {
                0x00127395,//Human_Female_Hair_Bob [HDPT:00127395]
                0x0015578B,//Human_Female_Hair_Business [HDPT:0015578B]
                0x00159AF2,//Human_Female_Hair_Buzz_Mohawk [HDPT:00159AF2]
                0x00172588,//Human_Female_Hair_CyberFade [HDPT:00172588]
                0x0012FDE2,//Human_Female_Hair_Dreadlocks_HairMesh [HDPT:0012FDE2]
                0x0012FDE3,//Human_Female_Hair_Dreadlocks_HairTie [HDPT:0012FDE3]
                0x00132C5A,//Human_Female_Hair_Even_Buzz_Back [HDPT:00132C5A]
                0x00128008,//Human_Female_Hair_Hairspray_Bob [HDPT:00128008]
                0x0015B029,//Human_Female_Hair_High_and_Tight [HDPT:0015B029]
                0x00133E4E,//Human_Female_Hair_Hollywood_curls [HDPT:00133E4E]
                0x0014AFDD,//Human_Female_Hair_Messy_Bob [HDPT:0014AFDD]
                0x00134EB1,//Human_Female_Hair_Messy_Business [HDPT:00134EB1]
                0x0005B53C,//Human_Female_Hair_Messy_Updo [HDPT:0005B53C]
                0x000D9D3A//Human_Female_Hair_Mullet [HDPT:000D9D3A]
            };
            return new FormKey(RetrogradeContext.Current.StarfieldModKey, hairlist[random.Next(hairlist.Count)]).ToNullableLink<IHeadPartGetter>();
        }
        else
        {
            List<uint> hairlist = new List<uint>()
            {
                0x00127396,//Human_Male_Hair_Bob [HDPT:00127396]
                0x0015578A,//Human_Male_Hair_Business [HDPT:0015578A]
                0x00159AF3,//Human_Male_Hair_Buzz_Mohawk [HDPT:00159AF3]
                0x00266092,//Human_Male_Hair_Choppy_Bob [HDPT:00266092]
                0x0013F87D,//Human_Male_Hair_Coily_Mohawk [HDPT:0013F87D]
                0x001177D1,//Human_Male_Hair_Cornrows_Beads [HDPT:001177D1]
                0x0013EB51,//Human_Male_Hair_Cropped [HDPT:0013EB51]
                0x00169ED3,//Human_Male_Hair_CyberFade [HDPT:00169ED3]
                0x00132C59,//Human_Male_Hair_Even_Buzz_Front [HDPT:00132C59]
                0x0014781F,//Human_Male_Hair_Flat_Top [HDPT:0014781F]
                0x00134EB0,//Human_Male_Hair_Messy_Business [HDPT:00134EB0]
                0x00264EFA,//Human_Male_Hair_None [HDPT:00264EFA]
                0x000D9D39,//Human_Male_Hair_Mullet [HDPT:000D9D39]
                0x00141E96,//Human_Male_Hair_Shaggy [HDPT:00141E96]
                0x0015335C,//Human_Male_Hair_Spiked [HDPT:0015335C]
                0x0012F26F//Human_Male_Hair_Viking_Braids [HDPT:0012F26F]
            };
            return new FormKey(RetrogradeContext.Current.StarfieldModKey, hairlist[random.Next(hairlist.Count)]).ToNullableLink<IHeadPartGetter>();
        }
    }

    /// <summary>
    /// Gets random gear (leveled item) for NPCs.
    /// </summary>
    public static IFormLinkNullable<ILeveledItemGetter> GetRandomGear()
    {
        var random = RandomProvider.Random;
        List<uint> gearlist = new List<uint>()
        {
            0x003D0946,//LLI_Spacer_AssaultDefaultRole [LVLI:003D0946]
            0x003D0947,//LLI_Spacer_Charger [LVLI:003D0947]
            0x003D0948,//LLI_Spacer_Heavy [LVLI:003D0948]
            0x003D094A,//LLI_Spacer_Recruit [LVLI:003D094A]
            0x003D094B,//LLI_Spacer_Sniper [LVLI:003D094B]
            0x003D60AF,//LLI_Ecliptic_AssaultDefaultRole [LVLI:003D60AF]
            0x003D60B1,//LLI_Ecliptic_Heavy [LVLI:003D60B1]
            0x003D60B2,//LLI_Ecliptic_Officer [LVLI:003D60B2]
            0x003D60B4,//LLI_Ecliptic_Sniper [LVLI:003D60B4]
            0x003D60B5,//LLI_Ecliptic_Support [LVLI:003D60B5]
        };
        return new FormKey(RetrogradeContext.Current.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
    }

    /// <summary>
    /// Gets a random outfit for a specific faction.
    /// </summary>
    public static IFormLinkNullable<IOutfitGetter> GetRandomFactionOutfit(string Faction)
    {
        var random = RandomProvider.Random;
        List<uint> Outfits = new List<uint>();
        switch (Faction)
        {
            case "UC Navy":
                Outfits.Add(0x0015CF45);//Outfit_Clothes_UCNavy_Crew [OTFT:0015CF45]
                break;
            case "UC Vanguard":
                Outfits.Add(0x002B211A);//Outfit_Citizen [OTFT:002B211A]
                Outfits.Add(0x0009653C);//Outfit_Spacesuit_UCVanguard [OTFT:0009653C]
                Outfits.Add(0x0009653C);//Outfit_Spacesuit_UCVanguard_NoHelmet[OTFT: 0000697C]
                break;
            case "UC SysDef":
                Outfits.Add(0x0015CF45);//Outfit_Clothes_UCNavy_Crew [OTFT:0015CF45]
                Outfits.Add(0x002BE711);//Outfit_Spacesuit_UC_Pilot_SysDef_with_Helmet [OTFT:002BE711]
                Outfits.Add(0x0030F3DF);//Outfit_Spacesuit_UC_Pilot_SysDef_NoHelmet [OTFT:0030F3DF]
                break;
            case "Freestar Security":
                Outfits.Add(0x000E6944);//Outfit_Clothes_Akila_Security [OTFT:000E6944]
                break;
            case "Trackers Alliance":
                Outfits.Add(0x00270258);//Outfit_BountyHunter [OTFT:00270258]
                Outfits.Add(0x0026B102);//Outfit_Spacesuit_BountyHunter [OTFT:0026B102]
                Outfits.Add(0x000A5637);//Outfit_Spacesuit_BountyHunter_02 [OTFT:000A5637]
                break;
            case "Galbank":
                Outfits.Add(0x00067C92);//Outfit_Spacesuit_Settler [OTFT:00067C92]
                Outfits.Add(0x00042D85);//Outfit_Worker [OTFT:00042D85]
                break;
            case "Crimson Fleet":
                Outfits.Add(0x002EB236);// Outfit_Clothes_CrimsonFleet_Any [OTFT:002EB236]
                Outfits.Add(0x00018DCF);//Outfit_Spacesuit_CrimsonFleet [OTFT:00018DCF]
                break;
            case "Crimsonfleet":
                Outfits.Add(0x002EB236);// Outfit_Clothes_CrimsonFleet_Any [OTFT:002EB236]
                Outfits.Add(0x00018DCF);//Outfit_Spacesuit_CrimsonFleet [OTFT:00018DCF]
                break;
            case "Spacer":
                Outfits.Add(0x00042D85);//Outfit_Worker [OTFT:00042D85]
                Outfits.Add(0x0015E246);//Outfit_Spacesuit_Spacer_Any [OTFT:0015E246]
                break;
            case "Ecliptic":
                Outfits.Add(0x0027027D);//Outfit_Spacesuit_Ecliptic [OTFT:0027027D]
                Outfits.Add(0x00042D85);//Outfit_Worker [OTFT:00042D85]
                break;
            case "Varuun":
                Outfits.Add(0x00042D85);//Outfit_Worker [OTFT:00042D85]
                Outfits.Add(0x00278715);//Outfit_Spacesuit_Varuun [OTFT:00278715]
                Outfits.Add(0x000D6FA8);//Outfit_Spacesuit_Varuun_NoHelmetNoBackpack [OTFT:000D6FA8]
                break;
            case "Trade Authority":
                Outfits.Add(0x00042D85);//Outfit_Worker [OTFT:00042D85]
                break;
        }

        return new FormKey(RetrogradeContext.Current.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
    }

    /// <summary>
    /// Gets a random outfit.
    /// </summary>
    public static IFormLinkNullable<IOutfitGetter> GetRandomOutfit(bool spacesuit)
    {
        var random = RandomProvider.Random;
        if (spacesuit)
        {
            List<uint> outfitlist = new List<uint>()
            {
                0x0015E248,//Outfit_Spacesuit_BountyHunter [OTFT:0026B102]
                0x000A5637,//Outfit_Spacesuit_BountyHunter_02 [OTFT:000A5637]
                0x00018DCF,//Outfit_Spacesuit_CrimsonFleet [OTFT:00018DCF]
                0x0027027D,//Outfit_Spacesuit_Ecliptic [OTFT:0027027D]
                0x0026B103,//Outfit_Spacesuit_Miner [OTFT:0026B103]
                0x00026BF4,//Outfit_Spacesuit_Miner_Deimos [OTFT:00026BF4]
                0x0006AC02,//Outfit_Spacesuit_Miner_Orange [OTFT:0006AC02]
                0x0006AC02,//Outfit_Spacesuit_Settler [OTFT:00067C92]
                0x0006AC02,//Outfit_Spacesuit_ShockArmor [OTFT:00203FB7]
                0x0006AC02,//Outfit_Spacesuit_Spacer_Any [OTFT:0015E246]
                0x0006AC02,//Outfit_Spacesuit_TheFirst [OTFT:0012B42F]
                0x0006AC02,//Outfit_Spacesuit_UCVanguard [OTFT:0009653C]
            };

            return new FormKey(RetrogradeContext.Current.StarfieldModKey, outfitlist[random.Next(outfitlist.Count)]).ToNullableLink<IOutfitGetter>();
        }
        else
        {
            List<uint> outfitlist = new List<uint>()
            {
                0x002B211A, // Outfit_Citizen [OTFT:002B211A]
                0x00270258, // Outfit_BountyHunter [OTFT:00270258]
                0x002E2BBC, // Outfit_Citizen_UC [OTFT:002E2BBC]
                0x000E6944, // Outfit_Clothes_Akila_Security [OTFT:000E6944]
                0x001341D9, // Outfit_Clothes_Argos_Jumpsuit [OTFT:001341D9]
                0x002EB236, // Outfit_Clothes_CrimsonFleet_Any [OTFT:002EB236]
                0x0015CF45, // Outfit_Clothes_UCNavy_Crew [OTFT:0015CF45]
                0x0026B0FC, // Outfit_Colonist [OTFT:0026B0FC]
                0x00253B9B, // Outfit_Clothes_ScienceLabTec [OTFT:00253B9B]
                0x00034115, // Outfit_Clothes_ScienceLabTec_02 [OTFT:00034115]
                0x00392EE8, // Outfit_Clothes_Service_Uniform_RedMile [OTFT:00392EE8]
                0x00253B8A, // Outfit_Clothes_BusinessSuit [OTFT:00253B8A]
                0x00133D75, // Outfit_Clothes_Colonist_Adventurous_01_with_Hat [OTFT:00133D75]
                0x00133D56, // Outfit_Clothes_Farmer_01_NoHat [OTFT:00133D56]
                0x0026FB5C, // Outfit_TheFirst [OTFT:0026FB5C]
                0x00042D85, // Outfit_Worker [OTFT:00042D85]
            };

            return new FormKey(RetrogradeContext.Current.StarfieldModKey, outfitlist[random.Next(outfitlist.Count)]).ToNullableLink<IOutfitGetter>();
        }
    }

    /// <summary>
    /// Gets the faction rank placement for an NPC.
    /// </summary>
    public static RankPlacement GetFaction(string Faction)
    {
        var starfieldMod = RetrogradeContext.Current.StarfieldMod;
        var starfieldModKey = RetrogradeContext.Current.StarfieldModKey;

        switch (Faction)
        {
            case "Crimsonfleet":
                return new RankPlacement()
                {
                    Faction = starfieldMod.Factions[new FormKey(starfieldModKey, 0x00010B30)].ToLink(), //CrimeFactionCrimsonFleet [FACT:00010B30]
                    Rank = 0
                };
            case "Ecliptic":
                return new RankPlacement()
                {
                    Faction = starfieldMod.Factions[new FormKey(starfieldModKey, 0x0027028D)].ToLink(), //EclipticFaction [FACT:0027028D]
                    Rank = 0
                };
            case "Varuun":
                return new RankPlacement()
                {
                    Faction = starfieldMod.Factions[new FormKey(starfieldModKey, 0x0027872A)].ToLink(), //VaruunFaction [FACT:0027872A]
                    Rank = 0
                };
            case "Spacer":
                return new RankPlacement()
                {
                    Faction = starfieldMod.Factions[new FormKey(starfieldModKey, 0x0027BB8C)].ToLink(), //SpacerFaction [FACT:0027BB8C]
                    Rank = 0
                };
            default:
                return new RankPlacement()
                {
                    Faction = starfieldMod.Factions[new FormKey(starfieldModKey, 0x0027BB8C)].ToLink(), //SpacerFaction [FACT:0027BB8C]
                    Rank = 0
                };
        }
    }

    /// <summary>
    /// Creates a fully randomised NPC, adds it to the mod, and returns the result.
    /// </summary>
    /// <param name="myMod">Target mod to add the NPC to.</param>
    /// <param name="isDead">Use a dead-pose template instead of a living one.</param>
    /// <param name="nameContext">Injected into the AI name prompt (e.g. "criminal gang culture").</param>
    /// <param name="isFriendly">Use a friendly (talkable) template and set non-hostile AI settings.</param>
    public static CreatedNpcResult CreateRandomNpc(StarfieldMod myMod, bool isDead, string nameContext, bool isFriendly = false, FormKey? factionId = null)
    {
        bool isfemale = RandomProvider.Random.Next(100) > 50;
        string gender = isfemale ? "Female" : "Male";

        Npc template = isDead ? FindTemplateDeadNpc(isfemale)
                     : isFriendly ? FindTemplateFriendlyNpc(isfemale)
                     : FindTemplateNpc(isfemale);
        Npc npc = CloneNPC(myMod, template);

        if (isFriendly)
        {
            npc.Aggression = Npc.AggressionType.Unaggressive;
            npc.Confidence = Npc.ConfidenceType.Average;
        }

        npc.Name = AITools.RunPrompt(
            "Generate a unique full name (first and last) for a " + gender + ". " +
            nameContext + "\r\n" +
            "Do NOT reuse or repeat any names that have appeared previously in this session.\r\n" +
            "Do NOT include titles, ranks, nicknames, or extra commentary.\r\n" +
            "Return only the name."
        );
        npc.EditorID = "npc_" + npc.Name.ToString().ToLower().Replace(" ", "");

        Random wrand = RandomProvider.Random;
        npc.Weight = new NpcWeight()
        {
            Fat = (float)wrand.NextDouble(),
            Muscular = (float)wrand.NextDouble(),
            Thin = (float)wrand.NextDouble()
        };
        var lev = new PcLevelMult();
        lev.LevelMult = 0.25f + (float)RandomProvider.Random.NextDouble();
        npc.Level = lev;
        npc.SpaceOutfit = GetRandomOutfit(true);
        npc.EyeColor = GetEyeColour();
        npc.HairColor = GetHairColour();
        npc.SkinToneIndex = (byte)wrand.Next(8);
        npc.HeadParts.Add(GetHaircut(isfemale));
        npc.Items = new ExtendedList<ContainerEntry>
        {
            new ContainerEntry() { Item = new ContainerItem() { Item = GetRandomGear(), Count = 1 } }
        };

        var npcVoice = GetVoice("", isfemale);
        string voiceEditorId = string.Empty;
        if (!npcVoice.IsNull)
        {
            npc.Voice.SetTo(npcVoice.FormKey);
            var vtRec = RetrogradeContext.Current.StarfieldMod.VoiceTypes.FirstOrDefault(v => v.FormKey == npcVoice.FormKey);
            voiceEditorId = vtRec?.EditorID ?? npcVoice.FormKey.ID.ToString("X6");
        }

        if (factionId.HasValue)
        {
            npc.Factions.Clear();
            npc.Factions.Add(new RankPlacement { Faction = factionId.Value.ToLink<IFactionGetter>(), Rank = 0 });
        }

        myMod.Npcs.Add(npc);
        return new CreatedNpcResult { Npc = npc, VoiceEditorId = voiceEditorId, IsFemale = isfemale };
    }

    /// <summary>
    /// Gets a random voice type for the specified faction and gender.
    /// </summary>
    public static IFormLinkNullable<IVoiceTypeGetter> GetVoice(string Faction, bool isfemale)
    {
        var random = RandomProvider.Random;
        var starfieldMod = RetrogradeContext.Current.StarfieldMod;
        var starfieldModKey = RetrogradeContext.Current.StarfieldModKey;

        switch (Faction)
        {
            case "Crimsonfleet":
                if (isfemale)
                {
                    List<uint> voices = new List<uint>()
                    {
                        0x00010B2C,//CrimsonFleetFemale01 [VTYP:00010B2C]
                        0x00010B2D,//CrimsonFleetFemale02 [VTYP:00010B2D]
                        0x002BCA4B,//CrimsonFleetFemale03 [VTYP:002BCA4B]
                        0x002BCA4C,//CrimsonFleetFemale04 [VTYP:002BCA4C]
                    };
                    return starfieldMod.VoiceTypes[new FormKey(starfieldModKey, voices[random.Next(voices.Count)])].ToNullableLink();
                }
                else
                {
                    List<uint> voices = new List<uint>()
                    {
                        0x00010B2A,//CrimsonFleetMale01 [VTYP:00010B2A]
                        0x00010B2B,//CrimsonFleetMale02 [VTYP:00010B2B]
                        0x0024A9FD,//CrimsonFleetMale03 [VTYP:0024A9FD]
                        0x0024A9FE,//CrimsonFleetMale04 [VTYP:0024A9FE]
                    };
                    return starfieldMod.VoiceTypes[new FormKey(starfieldModKey, voices[random.Next(voices.Count)])].ToNullableLink();
                }
            case "Ecliptic":
                if (isfemale)
                {
                    List<uint> voices = new List<uint>()
                    {
                        0x002BCA4D,//EclipticFemale01 [VTYP:002BCA4D]
                    };
                    return starfieldMod.VoiceTypes[new FormKey(starfieldModKey, voices[random.Next(voices.Count)])].ToNullableLink();
                }
                else
                {
                    List<uint> voices = new List<uint>()
                    {
                        0x002BCA4E,//EclipticMale01 [VTYP:002BCA4E]
                    };
                    return starfieldMod.VoiceTypes[new FormKey(starfieldModKey, voices[random.Next(voices.Count)])].ToNullableLink();
                }
            case "Varuun":
                if (isfemale)
                {
                    List<uint> voices = new List<uint>()
                    {
                        0x0028DFF4,//VaruunZealotFemale01 [VTYP:0028DFF4]
                        0x0028DFF3,//VaruunZealotFemale02 [VTYP:0028DFF3]
                    };
                    return starfieldMod.VoiceTypes[new FormKey(starfieldModKey, voices[random.Next(voices.Count)])].ToNullableLink();
                }
                else
                {
                    List<uint> voices = new List<uint>()
                    {
                        0x0028DFF1,//VaruunZealotMale01 [VTYP:0028DFF1]
                        0x0028DFF2,//VaruunZealotMale02 [VTYP:0028DFF2]
                    };
                    return starfieldMod.VoiceTypes[new FormKey(starfieldModKey, voices[random.Next(voices.Count)])].ToNullableLink();
                }
            case "Spacer":
                if (isfemale)
                {
                    List<uint> voices = new List<uint>()
                    {
                        0x002BCA3D,//SpacerFemale01 [VTYP:002BCA3D]
                        0x002A2C77,//SpacerFemale02 [VTYP:002A2C77]
                        0x002A2C78,//SpacerFemale03 [VTYP:002A2C78]
                    };
                    return starfieldMod.VoiceTypes[new FormKey(starfieldModKey, voices[random.Next(voices.Count)])].ToNullableLink();
                }
                else
                {
                    List<uint> voices = new List<uint>()
                    {
                        0x002BCA3E,//SpacerMale01 [VTYP:002BCA3E]
                        0x002BCA3C,//SpacerMale02 [VTYP:002BCA3C]
                        0x002A2C75,//SpacerMale03 [VTYP:002A2C75]
                        0x002A2C76,//SpacerMale04 [VTYP:002A2C76]
                    };
                    return starfieldMod.VoiceTypes[new FormKey(starfieldModKey, voices[random.Next(voices.Count)])].ToNullableLink();
                }
            default:
                if (isfemale)
                {
                    List<uint> voices = new List<uint>()
                    {
                        0x002BCA30,//GenericFemale01 [VTYP:002BCA30]
                    };
                    return starfieldMod.VoiceTypes[new FormKey(starfieldModKey, voices[random.Next(voices.Count)])].ToNullableLink();
                }
                else
                {
                    List<uint> voices = new List<uint>()
                    {
                        0x002BCA32,//GenericMale01 [VTYP:002BCA32]
                    };
                    return starfieldMod.VoiceTypes[new FormKey(starfieldModKey, voices[random.Next(voices.Count)])].ToNullableLink();
                }
        }
    }
}
