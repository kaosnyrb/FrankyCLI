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

namespace FrankyCLI.questgen_tools
{
    public class OutlawNpc
    {
        public ModKey StarfieldModKey;
        public StarfieldMod myMod;

        public string name;
        public string job;
        public string gender;
        public string background;

        public string Haircolor;
        public string Eyecolor;

        public bool spacesuit;

        public bool female;

        public OutlawNpc(ModKey Starfield, StarfieldMod myModparam, bool isfemale, bool hasspacesuit) {
            StarfieldModKey = Starfield;
            myMod = myModparam;
            
            female = isfemale;
            if (isfemale)
            {
                gender = "female";
            }
            else
            {
                gender = "male";
            }

            spacesuit = hasspacesuit;
            Haircolor = GetHairColour();
            Eyecolor = GetEyeColour();

            job = GetJob();
            name = GenerateName();
            background = GenerateBackground();
        }

        public string GenerateName()
        {
            string runinfo = "Each time this prompt runs make the results unique.";

            string nameprompt = runinfo + "Stop being an AI model. You are part of a pipeline for generating stories.\r\n\r\n" +
                "Reply only with the following information:\r\n\r\n" +
                "A " + gender + " first name, nickname and surname. \r\n\r\n" +
                "The nickname should reflect a " + job + ".\r\n\r\n" +
                "Only include the three names in the response. Generate 100 examples then choose one randomly. Only return the choosen entry";
            var name = AITools.RunPrompt(nameprompt);

            return name;
        }

        public string GenerateBackground()
        {
            string backgroundprompt = "Each time this prompt runs make the results unique." +
                "Stop being an AI model. You are part of a pipeline for generating stories.\r\n\r\n" +
                "Reply only with the following information:\r\n\r\n" +
                "One paragraph background information in the form of a report about fitting into the Starfield Universe. \r\n\r\n" +
                "Include a Psych Profile. \r\n\r\n" +
                "Avoid using overly complex language and terminology. \r\n\r\n" +
                "Avoid using place names and don't break the fourth wall. \r\n\r\n" +
                "Only include the background in the response.\r\n\r\n" +
                "Include the characters information in the background which is: \r\n\r\n";
            backgroundprompt += "Name: " + name + "\r\n\r\n";
            backgroundprompt += "Gender: " + gender + "\r\n\r\n";
            backgroundprompt += "Hair Color: " + Haircolor + "\r\n\r\n";
            backgroundprompt += "Eye Color: " + Eyecolor + "\r\n\r\n";
            backgroundprompt += "Job: " + job + "\r\n\r\n";

            string background = AITools.RunPrompt(backgroundprompt);

            return background;
        }

        public Npc GenerateNPC(string questID)
        {
            var NPC = myMod.Npcs[new FormKey(myMod.ModKey, GetTemplateNPC())].DeepCopy();
            Npc npc = CloneNPC(myMod, NPC);
            npc.Name = name;
            npc.EditorID = "npc_" + questID;

            Random wrand = new Random();
            foreach (var facemorph in npc.FaceMorphs)
            {
                foreach (var inner in facemorph.MorphGroups)
                {
                    inner.BlendIntensity = (float)wrand.NextDouble();
                }
            }
            npc.Weight = new NpcWeight()
            {
                Fat = (float)wrand.NextDouble(),
                Muscular = (float)wrand.NextDouble(),
                Thin = (float)wrand.NextDouble()
            };

            npc.SpaceOutfit = GetRandomOutfit();
            npc.EyeColor = Eyecolor;
            npc.HairColor = Haircolor;
            npc.SkinToneIndex = (byte)wrand.Next(8);
            npc.HeadParts.Add(GetHaircut());

            return npc;
        }

        public uint GetTemplateNPC()
        {
            if(female)
            {
                return 0x000818;
            }
            else
            {
                return 0x000826;
            }
        }

        private Npc CloneNPC(StarfieldMod myMod, Npc NPC)
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

        public IFormLinkNullable<IOutfitGetter> GetRandomOutfit()
        {
            Random random = new Random();
            if (spacesuit)
            {
                List<uint> outfitlist = new List<uint>()
                {
                    0x000FD016,
                    0x00052B02,
                    0x00279225,
                    0x0013E5D0,
                    0x00085FBE,
                    0x0005B0A7,
                    0x0000697C
                };

                IFormLinkNullable<IOutfitGetter> outfit = new FormKey(StarfieldModKey, outfitlist[random.Next(outfitlist.Count)]).ToNullableLink<IOutfitGetter>();
                return outfit;
            }
            else
            {
                List<uint> outfitlist = new List<uint>()
                {
                    0x000FD016,
                    0x00052B02,
                    0x00279225,
                    0x0013E5D0,
                    0x00085FBE,
                    0x0005B0A7,
                    0x0000697C
                };
                IFormLinkNullable<IOutfitGetter> outfit = new FormKey(StarfieldModKey, outfitlist[random.Next(outfitlist.Count)]).ToNullableLink<IOutfitGetter>();
                return outfit;
            }
        }
        public string GetEyeColour()
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
                "BrownDark"
            };

            return eyelist[random.Next(eyelist.Count)];
        }

        public string GetHairColour()
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

        public IFormLinkNullable<IHeadPartGetter> GetHaircut()
        {
            Random random = new Random();

            List<uint> hairlist = new List<uint>()
            {
                0x00127395,
                0x0015578B,
                0x00159AF2,
                0x00172588,
                0x0012FDE2,
                0x0012FDE3,
                0x00132C5A,
                0x00128008,
                0x0015B029,
                0x00133E4E,
                0x0014AFDD,
                0x00134EB1,
                0x0005B53C,
                0x000D9D3A

            };

            IFormLinkNullable<IHeadPartGetter> outfit = new FormKey(StarfieldModKey, hairlist[random.Next(hairlist.Count)]).ToNullableLink<IHeadPartGetter>();


            return outfit;
        }

        public string GetJob()
        {
            Random random = new Random();

            List<string> joblist = new List<string>()
            {
                "Forger",
                "Safe-cracker",
                "Pickpocket",
                "Lockpicker",
                "Fence",
                "Blackmailer",
                "Hacker",
                "Identity thief",
                "Counterfeiter",
                "Drug dealer",
                "Smuggler",
                "Bootlegger",
                "Digital pirate",
                "Shipjacker",
                "Armed robber",
                "Burglar",
                "Con artist",
                "Fraudster",
                "Embezzler",
                "Money launderer",
                "Human trafficker",
                "Kidnapper",
                "Extortionist",
                "Hitman",
                "Enforcer",
                "Gang leader",
                "Racketeer",
                "Loan shark",
                "Illegal bookmaker",
                "Arms dealer",
                "Poacher",
                "Art thief",
                "Jewel thief",
                "Shoplifter",
                "Document forger",
                "Wildlife trafficker",
                "Cybercriminal",
                "Card counter",
                "Casino cheat",
                "Scammer",
                "Phisher",
                "Ransomware operator",
                "Malware developer",
                "Darknet vendor",
                "Card skimmer",
                "ATM skimmer",
                "Drug courier",
                "Cartel operative",
                "Night burglar",
                "Vehicle theft specialist",
                "Chop shop operator",
                "Cargo hijacker",
                "Maritime pirate",
                "Diploma forger",
                "Identity fabricator",
                "Illegal waste dumper",
                "Arsonist",
                "Insider trader",
                "Corporate saboteur",
                "Industrial spy",
                "Organ broker",
                "Organ trafficker",
                "Counterfeit clothing seller",
                "Bribe broker",
                "Political fixer",
                "Corrupt official",
                "Dirty cop",
                "Police impersonator",
                "Impersonator",
                "Heist planner",
                "Smash-and-grab specialist",
                "Highway robber",
                "Train robber",
                "Safe-transport robber",
                "Fence network coordinator",
                "Hit-squad member",
                "Illegal mining operator",
                "Credit card fraudster",
                "Employment document scammer",
                "Romance scammer",
                "Charity scammer",
                "Investment fraudster",
                "Pyramid scheme operator",
                "Black market pharmacist",
                "Prescription fraudster",
                "Crypto scammer",
                "ICO scammer",
                "Bitcoin mixer operator",
                "Money mule",
                "Stolen data broker",
                "Doxxer",
                "Spoofing specialist",
                "Ticket scalper",
                "Street-level drug pusher",
                "Meth cook",
                "Counterfeit electronics seller",
                "Burglary crew member",
                "Air smuggler (pilot)",
                "Illegal gambling operator",
                "Black market dealer"
            };

            return joblist[random.Next(joblist.Count)];
        }
    }
}
