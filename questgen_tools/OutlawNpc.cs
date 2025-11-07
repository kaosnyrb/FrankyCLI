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
        public StarfieldMod myMod;

        public string name;
        public string job;
        public string gender;
        public string background;

        public string Haircolor;
        public string Eyecolor;

        public bool spacesuit;

        public bool female;

        public Npc GeneratedNPC; 
        public OutlawNpc(StarfieldMod myModparam, bool isfemale, bool hasspacesuit) {
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
            Haircolor = NPCTools.GetHairColour();
            Eyecolor = NPCTools.GetEyeColour();

            job = GetJob();
            name = GenerateName();
            background = GenerateBackground();
        }

        public string GenerateName()
        {
            string runinfo = AITools.GetBackgroundPrompt() + "Each time this prompt runs make the results unique.";

            string nameprompt = runinfo + "Stop being an AI model. You are part of a pipeline for generating stories.\r\n\r\n" +
                "Reply only with the following information:\r\n\r\n" +
                "A " + gender + " first name, nickname and surname. \r\n\r\n" +
                "The nickname should reflect a " + job + ".\r\n\r\n" +
                "The name should reflect the Nationality: " + GetNationality() + ".\r\n\r\n" +
                "Only include the three names in the response. Generate 100 examples then choose one randomly. Only return the choosen entry";
            var name = AITools.RunPrompt(nameprompt);

            return name;
        }

        public string GenerateBackground()
        {
            string backgroundprompt = AITools.GetBackgroundPrompt() + "Each time this prompt runs make the results unique." +
                "Stop being an AI model. You are part of a pipeline for generating stories.\r\n\r\n" +
                "Include newline characters in your response.\r\n" +
                "Reply only with the following information:\r\n\r\n" +
                "Three paragraph on the history of the character, there strengths and weaknesses and past crimes. Keep each under 50 words.\r\n\r\n" +
                "Avoid using overly complex language and terminology. \r\n\r\n" +
                "Avoid using place names and don't break the fourth wall. \r\n\r\n" +
                "Only include the background in the response.\r\n\r\n" +
                "Include the characters information in the background which is: \r\n\r\n";
            backgroundprompt += "Name: " + name + "\r\n\r\n";
            backgroundprompt += "Gender: " + gender + "\r\n\r\n";
            backgroundprompt += "Hair Color: " + NPCTools.SanitiseHairColor(Haircolor) + "\r\n\r\n";
            backgroundprompt += "Eye Color: " + Eyecolor + "\r\n\r\n";
            backgroundprompt += "Job: " + job + "\r\n\r\n";

            string background = AITools.RunPrompt(backgroundprompt);

            
            return background;
        }

        public Npc GenerateNPC()
        {
            var NPC = myMod.Npcs[new FormKey(myMod.ModKey, NPCTools.GetTemplateNPC(female))].DeepCopy();
            Npc npc = NPCTools.CloneNPC(myMod, NPC);
            npc.Name = name;
            npc.EditorID = "npc_" + (name.ToLower()).Replace(" ","");

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

            npc.SpaceOutfit = NPCTools.GetRandomOutfit(spacesuit);
            npc.EyeColor = Eyecolor;
            npc.HairColor = Haircolor;
            npc.SkinToneIndex = (byte)wrand.Next(8);
            npc.HeadParts.Add(GetHaircut());
            var lev = new PcLevelMult();
            lev.LevelMult = 0.25f + (float)wrand.NextDouble();
            npc.Level = lev;

            myMod.Npcs.Add(npc);
            GeneratedNPC = npc;
            return npc;
        }

        public IFormLinkNullable<IHeadPartGetter> GetHaircut()
        {
            Random random = new Random();
            if (female)
            {
                List<uint> hairlist = new List<uint>()
                {
                    0x00127395,//Human_Female_Hair_Bob "Human_Female_Hair_Bob" [HDPT:00127395]
                    0x0015578B,//Human_Female_Hair_Business "Human_Female_Hair_Business" [HDPT:0015578B]
                    0x00159AF2,//Human_Female_Hair_Buzz_Mohawk "Human_Female_Hair_Buzz_Mohawk" [HDPT:00159AF2]
                    0x00172588,//Human_Female_Hair_CyberFade "Human_Female_Hair_CyberFade" [HDPT:00172588]
                    0x0012FDE2,//Human_Female_Hair_Dreadlocks_HairMesh "Human_Female_Hair_Dreadlocks_HairMesh" [HDPT:0012FDE2]
                    0x0012FDE3,//Human_Female_Hair_Dreadlocks_HairTie "Human_Female_Hair_Dreadlocks_HairTie" [HDPT:0012FDE3]
                    0x00132C5A,//Human_Female_Hair_Even_Buzz_Back "Human_Female_Hair_Even_Buzz_Back" [HDPT:00132C5A]
                    0x00128008,//Human_Female_Hair_Hairspray_Bob "Human_Female_Hair_Hairspray_Bob" [HDPT:00128008]
                    0x0015B029,//Human_Female_Hair_High_and_Tight "Human_Female_Hair_High_and_Tight" [HDPT:0015B029]
                    0x00133E4E,//Human_Female_Hair_Hollywood_curls "Human_Female_Hair_Hollywood_curls" [HDPT:00133E4E]
                    0x0014AFDD,//Human_Female_Hair_Messy_Bob "Human_Female_Hair_Messy_Bob" [HDPT:0014AFDD]
                    0x00134EB1,//Human_Female_Hair_Messy_Business "Human_Female_Hair_Messy_Business" [HDPT:00134EB1]
                    0x0005B53C,//Human_Female_Hair_Messy_Updo "Human_Female_Hair_Messy_Updo" [HDPT:0005B53C]
                    0x000D9D3A//Human_Female_Hair_Mullet "Human_Female_Hair_Mullet" [HDPT:000D9D3A]

                };
                IFormLinkNullable<IHeadPartGetter> outfit = new FormKey(gen_quest.StarfieldModKey, hairlist[random.Next(hairlist.Count)]).ToNullableLink<IHeadPartGetter>();
                return outfit;
            }
            else
            {
                List<uint> hairlist = new List<uint>()
                {
                    0x00127396,//Human_Male_Hair_Bob "Human_Male_Hair_Bob" [HDPT:00127396]
                    0x0015578A,//Human_Male_Hair_Business "Human_Male_Hair_Business" [HDPT:0015578A]
                    0x00159AF3,//Human_Male_Hair_Buzz_Mohawk "Human_Male_Hair_Buzz_Mohawk" [HDPT:00159AF3]
                    0x00266092,//Human_Male_Hair_Choppy_Bob "Human_Male_Hair_Choppy_Bob" [HDPT:00266092]
                    0x0013F87D,//Human_Male_Hair_Coily_Mohawk "Human_Male_Hair_Coily_Mohawk" [HDPT:0013F87D]
                    0x001177D1,//Human_Male_Hair_Cornrows_Beads "Human_Male_Hair_Cornrows_Beads" [HDPT:001177D1]
                    0x0013EB51,//Human_Male_Hair_Cropped "Human_Male_Hair_Cropped" [HDPT:0013EB51]
                    0x00169ED3,//Human_Male_Hair_CyberFade "Human_Male_Hair_CyberFade" [HDPT:00169ED3]
                    0x00132C59,//Human_Male_Hair_Even_Buzz_Front "Human_Male_Hair_Even_Buzz_Front" [HDPT:00132C59]
                    0x0014781F,//Human_Male_Hair_Flat_Top "Human_Male_Hair_Flat_Top" [HDPT:0014781F]
                    0x00134EB0,//Human_Male_Hair_Messy_Business "Human_Male_Hair_Messy_Business" [HDPT:00134EB0]
                    0x00264EFA,//Human_Male_Hair_None "Human_Male_Hair_None" [HDPT:00264EFA]
                    0x000D9D39,//Human_Male_Hair_Mullet "Human_Male_Hair_Mullet" [HDPT:000D9D39]
                    0x00141E96,//Human_Male_Hair_Shaggy "Human_Male_Hair_Shaggy" [HDPT:00141E96]
                    0x0015335C,//Human_Male_Hair_Spiked "Human_Male_Hair_Spiked" [HDPT:0015335C]
                    0x0012F26F//Human_Male_Hair_Viking_Braids "Human_Male_Hair_Viking_Braids" [HDPT:0012F26F]

                };
                IFormLinkNullable<IHeadPartGetter> outfit = new FormKey(gen_quest.StarfieldModKey, hairlist[random.Next(hairlist.Count)]).ToNullableLink<IHeadPartGetter>();
                return outfit;
            }
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

        public static string GetNationality()
        {
            Random random = new Random();

            List<string> nationalityList = new List<string>()
            {
                "American",
                "British",
                "Canadian",
                "Mexican",
                "Brazilian",
                "Argentinian",
                "Chilean",
                "Colombian",
                "Peruvian",
                "Venezuelan",
                "Uruguayan",
                "Paraguayan",
                "Bolivian",
                "Ecuadorian",
                "Costa Rican",
                "Panamanian",
                "Cuban",
                "Dominican",
                "Haitian",
                "Puerto Rican",
                "Jamaican",
                "Bahamian",
                "Barbadian",
                "Trinidadian",
                "Guyanese",
                "Belizean",
                "Honduran",
                "Salvadoran",
                "Nicaraguan",
                "Guatemalan",
                "Irish",
                "Scottish",
                "Welsh",
                "English",
                "French",
                "German",
                "Dutch",
                "Belgian",
                "Luxembourgish",
                "Swiss",
                "Austrian",
                "Italian",
                "Spanish",
                "Portuguese",
                "Greek",
                "Turkish",
                "Polish",
                "Czech",
                "Slovak",
                "Hungarian",
                "Romanian",
                "Bulgarian",
                "Serbian",
                "Croatian",
                "Bosnian",
                "Slovenian",
                "Macedonian",
                "Albanian",
                "Lithuanian",
                "Latvian",
                "Estonian",
                "Finnish",
                "Swedish",
                "Norwegian",
                "Danish",
                "Icelandic",
                "Russian",
                "Ukrainian",
                "Belarusian",
                "Kazakh",
                "Uzbek",
                "Turkmen",
                "Kyrgyz",
                "Tajik",
                "Georgian",
                "Armenian",
                "Azerbaijani",
                "Israeli",
                "Lebanese",
                "Syrian",
                "Jordanian",
                "Iraqi",
                "Iranian",
                "Saudi",
                "Emirati",
                "Qatari",
                "Bahraini",
                "Omani",
                "Yemeni",
                "Egyptian",
                "Moroccan",
                "Algerian",
                "Tunisian",
                "Libyan",
                "Sudanese",
                "Kenyan",
                "Tanzanian",
                "Ugandan",
                "Rwandan",
                "Burundian"
            };

            return nationalityList[random.Next(nationalityList.Count)];
        }

    }
}
