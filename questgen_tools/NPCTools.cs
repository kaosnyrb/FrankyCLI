using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Noggog.StructuredStrings.CSharp;
using OpenAI.Chat;
using OpenAI;
using System.Security.Policy;
using FrankyCLI.questgen_tools;
using static Mutagen.Bethesda.FormKeys.Starfield.Starfield;


namespace FrankyCLI.questgen_tools
{
    public class NPCTools
    {
        public static uint GetTemplateNPC(bool female)
        {
            if (female)
            {
                return 0x000818;
            }
            else
            {
                return 0x000826;
            }
        }
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

        public static string GetEyeColour()
        {
            Random random = new Random();

            List<string> eyelist = new List<string>()
            {
                "Blue",
                "Brown",
                "Red",
                "Iron",
                "Grey",
                "Hazel",
                "Green",
                "Sulfur",
            };

            return eyelist[random.Next(eyelist.Count)];
        }

        public static string SanitiseHairColor(string haircolor)
        {
            switch (haircolor) {
                case "DirtyBlonde":
                    return "Blonde";
                case "BlackBrown":
                    return "Brown";
                case "SaltAndBrown":
                    return "Brown";
                case "BrownDark":
                    return "Brown";
                case "SaltAndPepper":
                    return "Greying";
                default:
                    return haircolor;
            }
        }

        public static string GetHairColour()
        {
            Random random = new Random();

            List<string> hairlist = new List<string>()
            {
                "Jet",
                "DirtyBlonde",
                "BlackBrown",
                "Black",
                "Amber",
                "Copper",
                "Platinum",
                "SaltAndBrown",
                "BrownDark",
                "Violet",
                "White",
                "Ruby",
                "SaltAndPepper",
                "Blonde"
            };

            return hairlist[random.Next(hairlist.Count)];
        }

        public static IFormLinkNullable<IOutfitGetter> GetRandomOutfit(bool spacesuit)
        {
            Random random = new Random();
            if (spacesuit)
            {
                List<uint> outfitlist = new List<uint>()
                {
                    0x0015E248,//Outfit_Spacesuit_BountyHunter_01 [OTFT:0015E248]
                    0x000A5637,//Outfit_Spacesuit_BountyHunter_02 [OTFT:000A5637]
                    0x00018DCF,//Outfit_Spacesuit_CrimsonFleet [OTFT:00018DCF]
                    0x0027027D,//Outfit_Spacesuit_Ecliptic [OTFT:0027027D]
                    0x0026B103,//Outfit_Spacesuit_Miner [OTFT:0026B103]
                    0x00026BF4,//Outfit_Spacesuit_Miner_Deimos [OTFT:00026BF4]
                    0x0006AC02,//Outfit_Spacesuit_Miner_Orange [OTFT:0006AC02]
                };

                IFormLinkNullable<IOutfitGetter> outfit = new FormKey(gen_quest.StarfieldModKey, outfitlist[random.Next(outfitlist.Count)]).ToNullableLink<IOutfitGetter>();
                return outfit;
            }
            else
            {
                List<uint> outfitlist = new List<uint>()
                {
                    0x00253B90,//Outfit_Clothes_Civilian_RubberPants_01 [OTFT:00253B90]
                    0x0006A8B2,//Outfit_Clothes_Civilian_RubberPocketPants_01 [OTFT:0006A8B2]
                    0x00133D76,//Outfit_Clothes_Colonist_Adventurous_01_NoHat [OTFT:00133D76]
                    0x00133D75,//Outfit_Clothes_Colonist_Adventurous_01_with_Hat [OTFT:00133D75]
                    0x00133D74,//Outfit_Clothes_Colonist_Adventurous_Poncho_01_NoHat [OTFT:00133D74]
                    0x00133D73,//Outfit_Clothes_Colonist_Adventurous_Poncho_01_with_Hat [OTFT:00133D73]
                    0x00133D72,//Outfit_Clothes_Colonist_Adventurous_Poncho_Mask_01_NoHat [OTFT:00133D72]
                    0x00133D68,//Outfit_Clothes_Colonist_QuarterPaddedVest_01_NoHat [OTFT:00133D68]
                    0x001341DF//Outfit_Clothes_Akila_Security_NoHeadwear [OTFT:001341DF]

                };
                IFormLinkNullable<IOutfitGetter> outfit = new FormKey(gen_quest.StarfieldModKey, outfitlist[random.Next(outfitlist.Count)]).ToNullableLink<IOutfitGetter>();
                return outfit;
            }
        }
    }
}
