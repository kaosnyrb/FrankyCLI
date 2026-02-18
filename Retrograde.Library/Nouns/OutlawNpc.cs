using Retrograde.AI;
using Retrograde.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Nouns
{
    public class OutlawNpc
    {
        public StarfieldMod myMod;

        public string name;
        public string gender;

        public string Haircolor;
        public string Eyecolor;

        public string BountyFaction;

        public bool spacesuit;

        public bool female;

        public Npc instance;

        public FormKey deathItems;

        public FormKey Logfile;




        public OutlawNpc(StarfieldMod myModparam, bool hasspacesuit) {

            if (RandomProvider.Random.Next(100) > 50)
            {
                female = true;
            }
            myMod = myModparam;

            if (female)
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

            Console.WriteLine("Building Outlaw NPC...");
            name = GenerateName();
        }

        public string GenerateName()
        {
            var random = RandomProvider.Random;

            var seedOccupations = new List<string>()
            {
                "salvage runner",
                "data slicer",
                "frontier prospector",
                "blockade runner",
                "exo-suit merc",
                "station fixer",
                "freight hijacker",
                "deep space surveyor",
                "guild enforcer",
                "black-market courier",
                "asteroid miner",
                "stealth pilot",
                "shipboard engineer",
                "contraband broker",
                "gunship navigator"
            };

            var seedTraits = new List<string>()
            {
                "ruthless patience",
                "cold precision",
                "reckless daring",
                "quiet calculation",
                "loyal streak",
                "grudge holder",
                "opportunist",
                "methodical thinker",
                "sharp tongue",
                "stoic resolve",
                "paranoid caution",
                "flashy bravado"
            };

            var seedGearOptions = new List<string>()
            {
                "coil pistol",
                "ion cutter",
                "scrambler rig",
                "grav boots",
                "patched flight jacket",
                "signal jammer",
                "lockbreaker kit",
                "plasma blade",
                "long-range scope",
                "cargo drone"
            };

            var seedElements = new List<string>()
            {
                "cinder",
                "frost",
                "ember",
                "iron",
                "cobalt",
                "amber",
                "slate",
                "quartz",
                "ash",
                "jet"
            };

            var seedLocales = new List<string>()
            {
                "outer ring stations",
                "frontier refineries",
                "illicit starports",
                "ore haulers",
                "low-orbit docks",
                "dust-world towns",
                "frozen moons",
                "orbital scrapyards",
                "smuggler lanes",
                "secluded research outposts"
            };

            string seedOccupation = seedOccupations[random.Next(seedOccupations.Count)];
            string seedTrait = seedTraits[random.Next(seedTraits.Count)];
            string seedGearCue = seedGearOptions[random.Next(seedGearOptions.Count)];
            string seedElement = seedElements[random.Next(seedElements.Count)];
            string seedLocale = seedLocales[random.Next(seedLocales.Count)];

            string nameprompt =
                "Generate a single character name following all rules below. " +
                "Do NOT explain anything. Output ONLY one final name.\r\n\r\n" +

                "NAME STYLE SELECTION:\r\n" +
                "- Choose ONE of the following naming formats:\r\n" +
                "  1. First name, nickname, and surname.\r\n" +
                "  2. Nickname and surname.\r\n" +
                "  3. First name and nickname.\r\n" +
                "  4. First name and surname.\r\n" +
                "  5. A cool pseudonym (a single stylized alias).\r\n\r\n" +

                "The AI must choose the format naturally. Just produce a single final name.\r\n\r\n" +

                "CONSTRAINTS:\r\n" +
                "- Gender of character: " + gender + ".\r\n" +
                "- Nationality should influence any real-life name components: " + GetNationality() + ".\r\n" +
                "- Nicknames must be in English, short, sharp, and evocative.\r\n" +
                "- Avoid comedy or cliché names.\r\n\r\n" +

                "SEED CUES:\r\n" +
                "- Occupation vibe: " + seedOccupation + ".\r\n" +
                "- Personality edge: " + seedTrait + ".\r\n" +
                "- Signature gear: " + seedGearCue + ".\r\n" +
                "- Element/colour motif: " + seedElement + ".\r\n" +
                "- Operating locale: " + seedLocale + ".\r\n\r\n" +

                "Now output the ONE final character name.";

            var generatedName = AITools.RunPrompt(nameprompt);

            return generatedName;
        }

        public string GenerateBackground()
        {
            BountyFaction = FactionTools.GetFaction();
            Random random = RandomProvider.Random;

            var sb = new StringBuilder();

            sb.AppendLine("You are writing an internal " + BountyFaction + " intelligence report on a bounty target.");
            sb.AppendLine("Create a concise background file on the target for use by field operatives.");
            sb.AppendLine();
            sb.AppendLine("Character details:");
            sb.AppendLine("Name: " + name);
            sb.AppendLine("Upbringing: " + GetUpbringing());

            // Optional flavor details
            if (random.Next(100) > 50) sb.AppendLine("Trait: " + GetTrait());
            if (random.Next(100) > 50) sb.AppendLine("Habits: " + GetHabit());
            if (random.Next(100) > 50) sb.AppendLine("Gender: " + gender);
            if (random.Next(100) > 50) sb.AppendLine("Hair Color: " + NPCTools.SanitiseHairColor(Haircolor));
            if (random.Next(100) > 50) sb.AppendLine("Eye Color: " + Eyecolor);
            if (random.Next(100) > 50) sb.AppendLine("Flaws: " + Getflaws());
            if (random.Next(100) > 50) sb.AppendLine("Fears: " + GetFears());
            if (random.Next(100) > 50) sb.AppendLine("Goals: " + GetGoals());

            Console.WriteLine("Generating Outlaw Background...");

            string background = AITools.RunPrompt(sb.ToString());
            return background;
        }

        public string GenerateLogfile()
        {
            var sb = new StringBuilder();
            DateTime dateTime = new DateTime(2330, 5, 6);

            sb.AppendLine("You are writing a series of personal diary/log entries from the perspective of a bounty target.");
            sb.AppendLine("These entries should cover their plans, their fears, and the reasons they fled to their current location.");
            sb.AppendLine();
            sb.AppendLine("Keep the total length reasonably concise (aim for under 300 words).");

            Console.WriteLine("Generating Outlaw Log...");

            string background = AITools.RunPrompt(sb.ToString());
            return background;
        }


        public Npc GenerateNPC()
        {
            var NPC = myMod.Npcs[new FormKey(myMod.ModKey, NPCTools.GetTemplateNPC(female))].DeepCopy();
            Npc npc = NPCTools.CloneNPC(myMod, NPC);
            npc.Name = name;
            npc.EditorID = "npc_" + (name.ToLower()).Replace(" ","");

            Random wrand = RandomProvider.Random;
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
            npc.HeadParts.Add(NPCTools.GetHaircut(female));
            var lev = new PcLevelMult();
            lev.LevelMult = 0.25f + (float)wrand.NextDouble();
            npc.Level = lev;

            var legendary = new LegendaryArmourNoun(name);

            npc.Items = new ExtendedList<ContainerEntry>
            {
                new ContainerEntry() { Item = new ContainerItem() { Item = NPCTools.GetRandomGear(), Count = 1 } },
            };

            var frmlst = new FormList(myMod)
            {
                EditorID = npc.EditorID + "_deathitems",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };

            frmlst.Items.Add(legendary.LeveledItemGetter);
            deathItems = frmlst.FormKey;

            myMod.FormLists.Add(frmlst);

            myMod.Npcs.Add(npc);
            instance = npc;

            return npc;
        }

        //We do this last as we've built all the infomation to use in it.
        public void GenerateLog()
        {
            var log = GenerateLogfile();
            var Book = myMod.Books[new FormKey(myMod.ModKey, 0x000800)].DeepCopy();
            string logSynonym = RandomProvider.GetLogSynonym();
            Book logbook = new Book(myMod)
            {
                Components = Book.Components,
                Description = log,
                DropdownSound = Book.DropdownSound,
                EditorID = "book_" + (name.ToLower()).Replace(" ", ""),
                Keywords = Book.Keywords,
                FeaturedItemMessage = Book.FeaturedItemMessage,
                Flags = Book.Flags,
                InventoryArt = Book.InventoryArt,
                Model = Book.Model,
                Name = name + " " + logSynonym,
                Value = Book.Value,
                Weight = Book.Weight,
                Transforms = Book.Transforms,
            };

            myMod.Books.Add(logbook);

            //Add logbook to death items
            Logfile = logbook.FormKey;

            myMod.FormLists[deathItems].Items.Add(logbook);
        }



        public string GetUpbringing()
        {
            Random random = RandomProvider.Random;

            List<string> upbringinglist = new List<string>()
            {
                "Grew up in the city of New Atlantis, their parents worked in MAST admin.",
                "Grew up in the city of New Atlantis, their parents worked in the UC Navy.",
                "Grew up in the city of Neon, as a streetrat on the Ebbside.",
                "Grew up in the city of Akila, as an orphan on The Stretch.",
                "Grew up drifting system to system as a spacer kid aboard a family owned hauler.",
            };

            return upbringinglist[random.Next(upbringinglist.Count)];
        }

        public string Getflaws()
        {
            Random random = RandomProvider.Random;
            List<string> personalityFlaws = new List<string>()
            {
                "Impulsive",
                "Overly stubborn",
                "Easily angered",
                "Overconfident",
                "Pessimistic",
            };

            return personalityFlaws[random.Next(personalityFlaws.Count)];
        }

        public string GetTrait()
        {
            Random random = RandomProvider.Random;

            List<string> traitlist = new List<string>()
            {
                "Short temper",
                "Good hearing",
                "Night owl",
                "Tech savvy",
                "Fearless",
            };

            return traitlist[random.Next(traitlist.Count)];
        }

        public string GetHabit()
        {
            List<string> habitsAndBehaviors = new List<string>()
            {
                "Always cleans their gear",
                "Talks with their hands",
                "Constantly taps their foot",
                "Writes everything down",
                "Double-checks all locks",
            };

            Random random = RandomProvider.Random;

            return habitsAndBehaviors[random.Next(habitsAndBehaviors.Count)];
        }

        public string GetFears()
        {
            Random random = RandomProvider.Random;
            List<string> fearsAndPhobias = new List<string>()
            {
                "Fear of heights",
                "Fear of deep water",
                "Fear of small spaces",
                "Fear of the dark",
                "Fear of being alone",
            };

            return fearsAndPhobias[random.Next(fearsAndPhobias.Count)];
        }

        public string GetGoals()
        {
            Random random = RandomProvider.Random;
            List<string> motivationsAndGoals = new List<string>()
            {
                "Seeking wealth",
                "Seeking fame",
                "Seeking revenge",
                "Searching for lost family",
                "Trying to escape their past",
            };

            return motivationsAndGoals[random.Next(motivationsAndGoals.Count)];
        }

        public static string GetNationality()
        {
            Random random = RandomProvider.Random;

            List<string> nationalityList = new List<string>()
            {
                "American",
                "British",
                "Canadian",
                "Mexican",
                "Brazilian",
                "French",
                "German",
                "Japanese",
                "Chinese",
                "Russian",
            };

            return nationalityList[random.Next(nationalityList.Count)];
        }

    }
}
