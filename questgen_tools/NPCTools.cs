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
            Random random = new Random();

            if (female)
            {
                List<uint> npclist = new List<uint>()
                {
                    0x000818,
                    0x000856,
                    0x000857,
                    0x000858,
                    0x00085C,
                    0x00085D,
                    0x00085E,
                    0x00085F,
                    0x000860,
                    0x000861,
                };
                return npclist[random.Next(npclist.Count)];
            }
            else
            {
                List<uint> npclist = new List<uint>()
                {
                    0x000826,
                    0x000862,
                    0x000863,
                    0x000865,
                    0x000866,
                    0x000867,
                    0x000868,                };
                return npclist[random.Next(npclist.Count)];
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

        public static IFormLinkNullable<ILeveledItemGetter> GetRandomGear()
        {
            Random random = new Random();
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
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(gen_quest.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public static IFormLinkNullable<IOutfitGetter> GetRandomOutfit(bool spacesuit)
        {
            Random random = new Random();
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

                IFormLinkNullable<IOutfitGetter> outfit = new FormKey(gen_quest.StarfieldModKey, outfitlist[random.Next(outfitlist.Count)]).ToNullableLink<IOutfitGetter>();
                return outfit;
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

                IFormLinkNullable<IOutfitGetter> outfit = new FormKey(gen_quest.StarfieldModKey, outfitlist[random.Next(outfitlist.Count)]).ToNullableLink<IOutfitGetter>();
                return outfit;
            }
        }
    }
}
