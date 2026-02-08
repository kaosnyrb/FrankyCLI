using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;

namespace Retrograde.Utils;

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

    /// <summary>
    /// Creates a deep copy of an NPC in the specified mod.
    /// </summary>
    public static Npc CloneNPC(StarfieldMod myMod, Npc NPC)
    {
        return new Npc(myMod)
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
            Flags = NPC.Flags,
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
            ODTY = NPC.ODTY,
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
}
