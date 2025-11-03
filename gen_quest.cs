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

namespace FrankyCLI
{
    public class gen_quest
    {
        public static ModKey StarfieldModKey;
        public static int Generate(string[] args)
        {
            Random random = new Random();
            StarfieldMod myMod;
            string modname = args[0];
            string mode = args[1];
            string prefix = args[2];
            string item = args[3];
            string form = args[4];

            string datapath = "";
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                StarfieldModKey = new ModKey("Starfield", ModType.Master);
                var immutableLoadOrderLinkCache = env.LoadOrder.ToImmutableLinkCache();
                datapath = env.DataFolderPath;
                //Find the modkey 
                ModKey newMod = new ModKey(modname, ModType.Master);
                myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);
                if (!env.LoadOrder.ModExists(newMod))
                {
                    myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);
                }
                else
                {
                    for (int i = 0; i < env.LoadOrder.Count; i++)
                    {
                        if (env.LoadOrder[i].FileName == modname + ".esm")
                        {
                            ModPath modPath = Path.Combine(env.DataFolderPath, env.LoadOrder[i].FileName);
                            myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield);

                        }
                    }
                }

                string runinfo = "Each time this prompt runs make the results unique.";
                string gender = "female";

                string jobprompt = runinfo + "Stop being an AI model. You are part of a pipeline for generating stories.\r\n\r\nReply only with the following information:\r\n\r\n a Job that a criminal would have.";
                var job = GetJob(); //RunPrompt(jobprompt);//Really loved safe crackers  and art forgers

                string nameprompt = runinfo + "Stop being an AI model. You are part of a pipeline for generating stories.\r\n\r\nReply only with the following information:\r\n\r\nA "  + gender + " first name, nickname and surname. \r\n\r\nThe nickname should reflect a " + job + ".\r\n\r\nOnly include the three names in the response. Generate 100 examples then choose one randomly. Only return the choosen entry";
                var name = RunPrompt(nameprompt);

                string backgroundprompt = runinfo + "Stop being an AI model. You are part of a pipeline for generating stories.\r\n\r\nReply only with the following information:\r\n\r\nTwo paragraph background information in the form of a offical report about a "  + gender + " " + job + " fitting into the Starfield Universe. \r\n\r\nAvoid using place names and don't break the fourth wall \r\n\r\nOnly include the background in the response.\r\n\r\nInclude the characters name in the background which is: \r\n\r\n";
                backgroundprompt += name;
                string background = RunPrompt(backgroundprompt);

                var questprompt = runinfo + "Stop being an AI model. You are part of a pipeline for generating stories.\r\n\r\nA four word or less quest name.\r\nOnly include the quest name in the response.\r\n\r\nUse the following information to build the quest name:\r\n\r\n";

                questprompt += name + "\r\n";
                questprompt += background + "\r\n";

                var questname = RunPrompt(questprompt);


                var questID = Guid.NewGuid().ToString().Substring(0, 8);

                Console.WriteLine(questname);
                Console.WriteLine(name);
                Console.WriteLine(background);


                // NPC Target
                var NPC = myMod.Npcs[new FormKey(myMod.ModKey, 0x000818)].DeepCopy();
                Npc npc = CloneNPC(myMod, NPC);
                npc.Name = name;
                npc.EditorID = "npc_" + questID;

                Random wrand = new Random();
                foreach (var facemorph in  npc.FaceMorphs) 
                { 
                    foreach(var inner in facemorph.MorphGroups)
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
                npc.EyeColor = GetEyeColour();
                npc.HairColor = GetHairColour();
                npc.SkinToneIndex = (byte)wrand.Next(8);
                npc.HeadParts.Add(GetHaircut());



                 myMod.Npcs.Add(npc);

                // Quest
                var Quest = myMod.Quests[new FormKey(myMod.ModKey, 0x000803)].DeepCopy();
                Quest newQuest = new Quest(myMod)
                {
                    Name = questname,
                    Aliases = Quest.Aliases,
                    Components = Quest.Components,
                    Data = Quest.Data,
                    EditorID = "quest_" + questID,
                    Keywords = Quest.Keywords,
                    Location = Quest.Location,
                    MajorFlags = Quest.MajorFlags,
                    Objectives = Quest.Objectives,
                    MissionTypeKeyword = Quest.MissionTypeKeyword,
                    QuestType = Quest.QuestType,
                    ScriptComment = Quest.ScriptComment,
                    Stages = Quest.Stages,
                    Summary = Quest.Summary,
                    VirtualMachineAdapter = Quest.VirtualMachineAdapter                    
                };
                
                newQuest.Stages[0].LogEntries[0].Entry = "I've found a dataslate containing the location of <Alias=BountyTarget>, who is hiding out at <Alias=DungeonLocation> on <Alias=TargetPlanet>. The Trackers Alliance will pay for taking out the bounty.";
                //scripts
                ((ScriptObjectProperty)newQuest.VirtualMachineAdapter.Scripts[0].Properties[0]).Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();
                newQuest.VirtualMachineAdapter.Aliases[0].Property.Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();

                //Set the NPC to be the quest target
                ((IQuestReferenceAlias)Quest.Aliases[3]).CreateReferenceToObject.Object = npc.ToLink<IStarfieldMajorRecordGetter>();

                //Set the location
                //IFormLinkNullableGetter<IKeywordGetter> LocTypeOE_ThemeMiningKeyword = new FormKey(StarfieldModKey, 0x00226AEF).ToNullableLink<IKeywordGetter>();//LocTypeOE_ThemeMiningKeyword [KYWD:00226AEF]
                //var thing = FormLinkOrIndex<IKeywordGetter>.Factory(null, new FormKey(StarfieldModKey, 0x00226AEF), 0);
                //((LocationHasKeywordConditionData)((IQuestLocationAlias)Quest.Aliases[0]).Conditions[2].Data).FirstParameter = thing;// (IFormLinkOrIndex<IKeywordGetter>)LocTypeOE_ThemeMiningKeyword;

                myMod.Quests.Add(newQuest);
                // Book
                var Book = myMod.Books[new FormKey(myMod.ModKey, 0x000800)].DeepCopy();
                Book bountybook = new Book(myMod)
                {
                    CNAM = Book.CNAM,
                    Components = Book.Components,
                    Description = background,
                    DNAMUnknown = Book.DNAMUnknown,
                    DropdownSound = Book.DropdownSound,
                    EditorID = "book_" + questID,
                    Keywords = Book.Keywords,
                    ENAM = Book.ENAM,
                    FeaturedItemMessage = Book.FeaturedItemMessage,
                    Flags = Book.Flags,
                    FNAM = Book.FNAM,   
                    InventoryArt = Book.InventoryArt,
                    Model = Book.Model,
                    Name = "Bounty: " + name,
                    ODTY = Book.ODTY,
                    Value = Book.Value,
                    Weight = Book.Weight,
                    VirtualMachineAdapter = Book.VirtualMachineAdapter
                };
                //set  the  book to start the new quest
                ((ScriptObjectProperty)bountybook.VirtualMachineAdapter.Scripts[0].Properties[0]).Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();

                bountybook.ENAM = "Data Slate #" + questID;
                myMod.Books.Add(bountybook);

            }

            foreach (var rec in myMod.EnumerateMajorRecords())
            {
                rec.IsCompressed = false;
            }

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm");
            Console.WriteLine("Finished");
            return 0;
        }

        private static Npc CloneNPC(StarfieldMod myMod, Npc NPC)
        {
            
            return new Npc(myMod)
            {                
                EditorID = "npc_" + Guid.NewGuid().ToString().Substring(0,8),
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

        public static IFormLinkNullable<IOutfitGetter> GetRandomOutfit()
        {
            Random random = new Random();

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

        public static string GetJob()
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
                "BrownDark"
            };

            return eyelist[random.Next(eyelist.Count)];
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

        public static IFormLinkNullable<IHeadPartGetter> GetHaircut()
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
        // Make sure to set your API key in an environment variable: OPENAI_API_KEY
        public static string RunPrompt(string prompt)
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var client = new OpenAIClient(apiKey);

            var chat = client.GetChatClient("gpt-5");
            //var chat = client.GetChatClient("gpt-5-mini");
            var res = chat.CompleteChat(new UserChatMessage(prompt));
             return res.Value.Content[0].Text;
        }


    }


}
