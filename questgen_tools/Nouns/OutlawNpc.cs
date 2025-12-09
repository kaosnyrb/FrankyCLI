using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Noggog.StructuredStrings.CSharp;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

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

        public string BountyFaction;

        public bool spacesuit;

        public bool female;

        public Npc instance;


        public FormKey deathItems;

        public FormKey Logfile;


        public OutlawNpc(StarfieldMod myModparam, bool hasspacesuit) {
            
            if (RandomUtils.random.Next(100) > 50)
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

            job = GetJob();

            Console.WriteLine("Building Outlaw NPC...");
            name = GenerateName();
            background = GenerateBackground();
            //GenerateNPC();
        }

        public string GenerateName()
        {
            string namestogenerate = "first name, nickname and surname.";

            Random rand = RandomUtils.random;
            int nametype = rand.Next(130);
            if(nametype <= 33)
            {
                namestogenerate = "first name, nickname and surname.";
            }
            if (nametype > 33 && nametype <= 66)
            {
                namestogenerate = "nickname and surname.";
            }
            if(nametype >66 && nametype <= 100)
            {
                namestogenerate = "first name and nickname";
            }
            if (nametype > 100 && nametype <= 120)
            {
                namestogenerate = "first name and surname.";
            }
            if (nametype >120)
            {
                namestogenerate = "cool pseudonym";
            }
            Console.WriteLine("Generating Outlaw Name...");

            string nameprompt =
                "Reply only with the following information:\r\n\r\n" +
                "A " + gender + " " + namestogenerate + " \r\n\r\n" +
                "The nickname should reflect a " + job + "  and be in English.\r\n\r\n" +
                "The name should reflect the Nationality: " + GetNationality() + ".\r\n\r\n" +
                "Only include the names in the response. Generate 10 examples then return number " + rand.Next(10) + ". Only return the choosen entry";
            var name = AITools.RunPrompt(nameprompt);

            return name;
        }

        public string GenerateBackground()
        {
            BountyFaction = FactionTools.GetFaction();
            Random random = RandomUtils.random;

            string backgroundprompt = "Create a brief background file on the target.\r\n";

            backgroundprompt = PromptFlavourTools.AddReportTypes(backgroundprompt);

            backgroundprompt += "Ensure the total output stays under 500 words, regardless of format.\r\n";

            backgroundprompt += "The entry must be written in the tone of an internal " + BountyFaction + " report.\r\n";
            backgroundprompt += "Avoid complex terminology. \r\n";
            backgroundprompt += "Use only locations and details present in the provided background information.\r\n";
            backgroundprompt += "Do not break the fourth wall.";
            backgroundprompt += "Avoid fictional place names.";
            backgroundprompt += "Avoid starting every sentence with a pronoun.";

            backgroundprompt += "Include the character’s background details naturally:";

            backgroundprompt += "Name: " + name + "\r\n";
            backgroundprompt += "Upbringing: " + GetUpbringing() + "\r\n";
            backgroundprompt += "Job: " + job + "\r\n";


            //These are optional details to spice things up.
            if (random.Next(100) > 50) backgroundprompt += "Trait: " + GetTrait() + "\r\n";
            if (random.Next(100) > 50) backgroundprompt += "Habits: " + GetHabit() + "\r\n\r\n";
            if (random.Next(100) > 50) backgroundprompt += "Gender: " + gender + "\r\n\r\n";
            if (random.Next(100) > 50) backgroundprompt += "Hair Color: " + NPCTools.SanitiseHairColor(Haircolor) + "\r\n\r\n";
            if (random.Next(100) > 50) backgroundprompt += "Eye Color: " + Eyecolor + "\r\n\r\n";
            if (random.Next(100) > 50) backgroundprompt += "Flaws: " + Getflaws() + "\r\n\r\n";
            if (random.Next(100) > 50) backgroundprompt += "Fears: " + GetFears() + "\r\n\r\n";
            if (random.Next(100) > 50) backgroundprompt += "Goals: " + GetGoals() + "\r\n\r\n";


            Console.WriteLine("Generating Outlaw Background...");

            string background = AITools.RunPrompt(backgroundprompt);

            
            return background;
        }

        public string GenerateLogfile()
        {
            string backgroundprompt =
                "Generate the dairy entries of the bounty target, " +
                "discussing plans and why they've fled to this location. " +
                "Write in the first person in a style that suits the background of the character." +
                "Use all the steps and generated data so far to build the narrative, you don't have to include everything." +                
                "Avoid using place names that aren't in the background infomation and don't break the fourth wall. \r\n\r\n" +
                "Only include the background in the response.\r\n\r\n";
            Console.WriteLine("Generating Outlaw Log...");

            backgroundprompt = PromptFlavourTools.AddFlavourToTargetBook(backgroundprompt);

            string background = AITools.RunPrompt(backgroundprompt);
            return background;
        }

        public Npc GenerateNPC()
        {
            var NPC = myMod.Npcs[new FormKey(myMod.ModKey, NPCTools.GetTemplateNPC(female))].DeepCopy();
            Npc npc = NPCTools.CloneNPC(myMod, NPC);
            npc.Name = name;
            npc.EditorID = "npc_" + (name.ToLower()).Replace(" ","");

            Random wrand = RandomUtils.random;
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
                 //new ContainerEntry() { Item = new ContainerItem() { Item = legendary, Count = 1 } }//Instead of adding the legendary here create a deathitems list
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
            //Console.WriteLine(log);
            //Generate Log file for bounty
            var Book = myMod.Books[new FormKey(myMod.ModKey, 0x000800)].DeepCopy();
            Book logbook = new Book(myMod)
            {
                CNAM = Book.CNAM,
                Components = Book.Components,
                Description = log,
                DNAMUnknown = Book.DNAMUnknown,
                DropdownSound = Book.DropdownSound,
                EditorID = "book_" + (name.ToLower()).Replace(" ", ""),
                Keywords = Book.Keywords,
                ENAM = Book.ENAM,
                FeaturedItemMessage = Book.FeaturedItemMessage,
                Flags = Book.Flags,
                FNAM = Book.FNAM,
                InventoryArt = Book.InventoryArt,
                Model = Book.Model,
                Name = name + " Logs",
                ODTY = Book.ODTY,
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
            Random random = RandomUtils.random;

            List<string> upbringinglist = new List<string>()
            {
                "Grew up in the city of New Atlantis, their parents worked in MAST admin.",
                "Grew up in the city of New Atlantis, their parents worked in the UC Navy.",
                "Grew up in the city of New Atlantis, they were in the UC Vanguard before they betrayed there oath.",
                "Grew up in the city of New Atlantis, they were in the UC Navy before they betrayed there oath.",
                "Grew up in the city of New Atlantis, their parents worked cleaning the New Atlantis Transit system.",
                "Grew up in the city of New Atlantis, their parents worked in the UC Ageis.",
                "Grew up in the city of New Atlantis, their parents worked maintence in The Well, the dark underbelly of the city.",
                "Grew up in the city of New Atlantis, their parents worked at the Spaceport doing repairs on ships.",
                "Grew up in the city of New Atlantis, worked as a Chunks employee before having a breakdown.",
                "Grew up in the city of Cydonia on Mars, their parents worked as miners.",
                "Grew up in the city of Cydonia on Mars, their parents worked running supplies to the Trade Authority.",
                "Grew up in the city of Cydonia on Mars, their parents worked running supplies to the Deimos Staryards.",
                "Grew up in the city of Cydonia on Mars, worked as a Terrabrew Barista before having a breakdown.",
                "Grew up in the city of Gagarin Landing, their parents worked in the Mech Planet during the Colony War.",
                "Grew up in the city of Gagarin Landing, their parents worked maintence.",
                "Grew up in the city of Gagarin Landing, their parents worked in shipping.",
                "Grew up in the city of Neon, catching Chasmbass for Xenofresh Fisheries.",
                "Grew up in the city of Neon, as a dancer in the Astral Lounge.",
                "Grew up in the city of Neon, as a streetrat on the Ebbside.",
                "Grew up in the city of Neon, their parents worked as low level Ryujin employees.",
                "Grew up in the city of Neon, their parents worked as low level Generdyne Industries employees.",
                "Grew up in the city of Neon, their parents worked as low level Xenofresh Fisheries employees.",
                "Grew up in the city of Neon, their parents worked as low level Taiyō Astroneering employees.",
                "Grew up in the city of Neon, their parents worked as low level DRONE employees.",
                "Grew up in the city of Neon, their parents worked as low level Arboron employees.",
                "Grew up in the city of Neon, worked as a Chunks employee before having a breakdown.",
                "Grew up in the city of Neon, worked as a Terrabrew Barista before having a breakdown.",
                "Grew up in the city of Akila, as an orphan on The Stretch.",
                "Grew up in the city of Akila, their parents worked as Farmhands.",
                "Grew up in the city of Akila, their parents worked as Ship technicians.",
                "Grew up in the city of Akila, their parents worked to support the Freestar Rangers.",
                "Grew up in the city of Akila, their parents worked as Mech Pilots in the colony war.",
                "Grew up away for civilisation as a LIST Colonist, trying to build a life on the frontier",
                "Grew up in the city of New Atlantis, their parents ran a small food stall in the Commercial District.",
                "Grew up in the city of New Atlantis, their family lived in The Well struggling to make ends meet.",
                "Grew up in the city of New Atlantis, their parents served as custodians in the MAST Building.",
                "Grew up in the city of Cydonia on Mars, their parents hauled ore for the mining guild.",
                "Grew up in the city of Cydonia on Mars, their parents repaired aging drilling rigs.",
                "Grew up in the city of Cydonia on Mars, worked security for a Trade Authority warehouse before being fired.",
                "Grew up in Gagarin Landing, their family ran a scrapyard dismantling old mech parts.",
                "Grew up in Gagarin Landing, their parents openly criticized Arc Might's automation practices.",
                "Grew up in Gagarin Landing, they refurbished abandoned mining drones as a teen.",
                "Grew up in the city of Neon, working as a courier in the Underbelly.",
                "Grew up in the city of Neon, their parents ran a tiny kiosk in Ikuchi Market.",
                "Grew up in the city of Neon, surviving by doing odd jobs for the Ebbside Strikers.",
                "Grew up in the city of Neon, their parents were Aurora disposal workers for Xenofresh.",
                "Grew up in the city of Neon, their family lived in a small pod above Bayu Plaza.",
                "Grew up in the city of Neon, they worked as a low tier Ryujin intern before being let go for data theft.",
                "Grew up in the city of Akila, their parents maintained the wall protecting the city from Ashta.",
                "Grew up in the city of Akila, they operated a small stall in Coe Plaza selling ship parts.",
                "Grew up in the city of Akila, their family lost their farm to Ashta and moved into Midtown.",
                "Grew up in the city of Akila, their parents served as volunteer militia during border tensions.",
                "Grew up in HopeTown, their parents worked on the HopeTech assembly line.",
                "Grew up in HopeTown, helping test prototype grav drives for HopeTech.",
                "Grew up in New Homestead, their family maintained tourist shuttles landing on Titan.",
                "Grew up in Paradiso, their parents worked long hours in resort hospitality.",
                "Grew up on Waggoner Farm, helping tend livestock and repair old machinery.",
                "Grew up drifting system to system as a spacer kid aboard a family owned hauler.",
                "Grew up in the city of New Atlantis, their parents operated a small water recycling unit in The Well.",
                "Grew up in the city of New Atlantis, raised in the shadow of the MAST Building with dreams of joining the UC Vanguard.",
                "Grew up in the city of New Atlantis, their parents managed cargo intake at the Spaceport.",
                "Grew up in the city of New Atlantis, their parents maintained hydroponic gardens for residential blocks.",
                "Grew up in the city of New Atlantis, their family struggled after losing work due to UC budget cuts.",
                "Grew up in the city of New Atlantis, they spent most of their youth riding the Transit lines to avoid home.",
                "Grew up in the city of Cydonia on Mars, their parents worked repairing atmospheric filters.",
                "Grew up in the city of Cydonia on Mars, their family lived in cramped miner dorms.",
                "Grew up in the city of Cydonia on Mars, their parents worked as loaders for Deimos Staryards shipments.",
                "Grew up in the city of Cydonia on Mars, they ran illicit snacks through the tunnels to earn credits.",
                "Grew up in the city of Cydonia on Mars, their parents served in colony maintenance during dust storms.",
                "Grew up in the city of Cydonia on Mars, their parents were safety officers in the deep tunnels.",
                "Grew up in Gagarin Landing, their parents were laid off when mech production was banned.",
                "Grew up in Gagarin Landing, their parents ran a small tool shop near the old mech plants.",
                "Grew up in Gagarin Landing, they joined other teens scavenging abandoned assembly lines.",
                "Grew up in Gagarin Landing, their parents helped refurbish old corporate housing.",
                "Grew up in Gagarin Landing, their parents worked in a robotics calibration lab.",
                "Grew up in Gagarin Landing, their parents maintained power relays for the settlement.",
                "Grew up in the city of Neon, their parents washed aurora mixing tanks for Xenofresh.",
                "Grew up in the city of Neon, their family slept above a noisy bar in Ebbside.",
                "Grew up in the city of Neon, they sold counterfeit fashion chips in Ikuchi Market.",
                "Grew up in the city of Neon, their parents were temp workers constantly switching corporate contracts.",
                "Grew up in the city of Neon, their parents maintained the lighting grid along the Conduction Span.",
                "Grew up in the city of Neon, their family lived in Underbelly maintenance tunnels.",
                "Grew up in the city of Neon, they danced in back-alley clubs long before working legally.",
                "Grew up in the city of Neon, their parents worked odd jobs for the Seokguh Syndicate.",
                "Grew up in the city of Neon, they hustled tourists to survive.",
                "Grew up in the city of Neon, their parents operated a failing noodle stand near Bayu Plaza.",
                "Grew up in the city of Neon, their parents were low level security contractors for Ryujin.",
                "Grew up in the city of Akila, their parents patrolled the outer fences during Ashta migrations.",
                "Grew up in the city of Akila, they helped run freight to farms across The Stretch.",
                "Grew up in the city of Akila, their family owned a small repair shed for broken frontier tools.",
                "Grew up in the city of Akila, their parents worked as archivists for the historical society.",
                "Grew up in the city of Akila, they spent most days exploring the dusty outskirts.",
                "Grew up in the city of Akila, their parents tended livestock at a ranch outside the walls.",
                "Grew up in the city of Akila, their parents sold handmade gear to Freestar Rangers.",
                "Grew up in the city of Akila, their family lived in a tiny unit in Midtown above a workshop.",
                "Grew up in HopeTown, their parents welded frames for new HopeTech vessels.",
                "Grew up in HopeTown, they tested cockpit systems for new starship models.",
                "Grew up in HopeTown, their parents worked in the paint and finish bay.",
                "Grew up in HopeTown, raised on tales of Ron Hope's early days.",
                "Grew up in New Homestead, their parents worked in the old NASA exhibit hall.",
                "Grew up in New Homestead, they guided tourists through Titan's harsh environment.",
                "Grew up in New Homestead, their family maintained aging biodomes.",
                "Grew up in Paradiso, their parents were lifeguards on the artificial beaches.",
                "Grew up in Paradiso, they handed out overpriced resort flyers to newcomers.",
                "Grew up in Paradiso, their parents worked in resort security keeping guests safe.",
                "Grew up on a remote frontier homestead, living off soil reclamation units.",
                "Grew up aboard a travelling merchant ship, docking only to trade.",
                "Grew up on a small settlement in the Cheyenne system, helping maintain water condensers.",
                "Grew up in a drifting spacer commune, never calling any planet home.",
            };

            return upbringinglist[random.Next(upbringinglist.Count)];

        }

        public string Getflaws()
        {
            Random random = RandomUtils.random;
            List<string> personalityFlaws = new List<string>()
            {
                "Impulsive",
                "Overly stubborn",
                "Easily angered",
                "Overconfident",
                "Pessimistic",
                "Jealous",
                "Distrustful",
                "Overly competitive",
                "Hot-headed",
                "Indecisive",
                "Reckless",
                "Apathetic",
                "Secretive",
                "Vain",
                "Paranoid",
                "Selfish",
                "Lazy",
                "Overly critical",
                "Judgmental",
                "Obsessive",
                "Fearful",
                "Easily discouraged",
                "Passive-aggressive",
                "Manipulative",
                "Insensitive",
                "Forgetful",
                "Gullible",
                "Naive",
                "Sarcastic",
                "Hypocritical",
                "Easily frustrated",
                "Insecure",
                "Procrastinator",
                "Distractible",
                "Short-tempered",
                "Blunt to a fault",
                "Clingy",
                "Overly serious",
                "Pushy",
                "Self-doubting",
                "Moody",
                "Overly sentimental",
                "Talks too much",
                "Interrupts often",
                "Argumentative",
                "Needs constant validation",
                "Fear of failure",
                "Fear of rejection",
                "Cowardly",
                "Arrogant",
                "Resentful",
                "Holds grudges",
                "Perfectionist to a fault",
                "Unreliable",
                "Self-destructive",
                "Obsessed with approval",
                "Overly dramatic",
                "Tends to exaggerate",
                "Lacks empathy",
                "Has trouble apologizing",
                "Blames others frequently",
                "Easily bored",
                "Avoids responsibility",
                "Distrusts authority",
                "Overly dependent",
                "Highly defensive",
                "Insensitive with humor",
                "Prone to overthinking",
                "Prone to panic",
                "Forgetful of commitments",
                "Spends impulsively",
                "Seeks conflict",
                "Avoids conflict",
                "Always assumes the worst",
                "Cannot take criticism",
                "Gives up too easily",
                "Workaholic",
                "Too trusting",
                "Controlling",
                "Impatient",
                "Self-centered",
                "Awkward in conversations",
                "Has trouble listening",
                "Dismissive of others’ ideas",
                "Easily overwhelmed",
                "Talks down to others",
                "Irresponsible with time",
                "Emotionally distant",
                "Fearful of change",
                "Gets jealous of success",
                "Hides true feelings",
                "Acts without thinking",
                "Holds unrealistic expectations",
                "Too focused on rules",
                "Overanalyzes everything",
                "Struggles to make connections",
                "Constant worrier",
                "Avoids difficult situations"
            };

            return personalityFlaws[random.Next(personalityFlaws.Count)];

        }

        public string GetTrait()
        {
            Random random = RandomUtils.random;

            List<string> traitlist = new List<string>()
            {
                 "Short temper",
                "Good hearing",
                "Can't dance",
                "Night owl",
                "Early riser",
                "Tech savvy",
                "Claustrophobic",
                "Fearless",
                "Overconfident",
                "Calm under pressure",
                "Fast learner",
                "Suspicious of strangers",
                "Loyal to a fault",
                "Perfectionist",
                "Messy eater",
                "Strong moral code",
                "Secretive",
                "Forgetful",
                "Great storyteller",
                "Prone to daydreaming",
                "Stubborn",
                "Easily distracted",
                "Addicted to coffee",
                "Talks to themselves",
                "Hates authority",
                "Natural leader",
                "Gets lost easily",
                "Obsessed with gadgets",
                "Enjoys solving puzzles",
                "Afraid of heights",
                "Makes terrible jokes",
                "Competitive",
                "Mild insomnia",
                "Hates confrontation",
                "Risk taker",
                "Always hungry",
                "Doesn’t trust AI",
                "Keeps souvenirs",
                "Writes in a journal",
                "Collects rare minerals",
                "Overthinks everything",
                "Gets motion sickness",
                "Prefers solitude",
                "Friendly with everyone",
                "Talkative",
                "Soft-spoken",
                "Hates small talk",
                "Fidgety",
                "Walks quickly",
                "Heavy sleeper",
                "Light sleeper",
                "Easily annoyed",
                "Worries constantly",
                "Laughs loudly",
                "Whistles often",
                "Has a lucky charm",
                "Skeptical of science",
                "Trusts intuition",
                "Excellent memory",
                "Clumsy",
                "Organized",
                "Obsessive planner",
                "Sings while working",
                "Prone to headaches",
                "Enjoys danger",
                "Pessimistic",
                "Optimistic",
                "Realist",
                "Can’t lie well",
                "Master liar",
                "Overly polite",
                "Gets homesick",
                "Loves spicy food",
                "Vegetarian",
                "Always cold",
                "Always hot",
                "Sensitive to noise",
                "Easily bored",
                "Flirtatious",
                "Confident speaker",
                "Fearful of conflict",
                "Protective of friends",
                "Tries to impress others",
                "Stoic",
                "Has a dark sense of humor",
                "Prone to nostalgia",
                "Talks in their sleep",
                "Snores loudly",
                "Loves animals",
                "Dislikes kids",
                "Trusts everyone too quickly",
                "Hates flying",
                "Tends to exaggerate",
                "Loves surprises",
                "Hates surprises",
                "Often late",
                "Always punctual",
                "Easily embarrassed",
                "Strong sense of justice",
                "Collects secrets",
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
                "Twirls objects when thinking",
                "Writes everything down",
                "Double-checks all locks",
                "Mutters under their breath",
                "Paces when nervous",
                "Organizes items by size",
                "Keeps pockets full of snacks",
                "Checks the time frequently",
                "Randomly hums tunes",
                "Talks to machines",
                "Adjusts their clothing often",
                "Over-explains things",
                "Interrupts often",
                "Finishes other people's sentences",
                "Avoids eye contact",
                "Stares too long at people",
                "Cracks knuckles regularly",
                "Keeps a strict daily routine",
                "Laughs at inappropriate times",
                "Speaks very slowly",
                "Speaks very quickly",
                "Collects odd trinkets",
                "Always early to meetings",
                "Always late to meetings",
                "Fidgets with tools",
                "Sleeps with lights on",
                "Eats extremely fast",
                "Eats extremely slow",
                "Never sits still",
                "Overuses sarcastic comments",
                "Asks too many questions",
                "Repeats themselves",
                "Talks to themselves",
                "Sighs loudly",
                "Quotes ancient proverbs",
                "Tends to wander off",
                "Stands too close to others",
                "Keeps detailed logs",
                "Wakes up before dawn",
                "Sleeps in whenever possible",
                "Hates making decisions",
                "Rarely speaks unless necessary",
                "Chews on their lip",
                "Taps objects absentmindedly",
                "Always cleans up after others",
                "Leaves a mess wherever they go",
                "Listens to music constantly",
                "Refuses to throw anything away",
                "Overpacks for every mission",
                "Hardly brings anything",
                "Worries about equipment constantly",
                "Likes to rearrange furniture",
                "Talks about past adventures often",
                "Repeats jokes",
                "Reads manuals for fun",
                "Fixes things that aren’t broken",
                "Obsessively checks vitals",
                "Polishes weapons daily",
                "Has a strict bedtime",
                "Never sleeps enough",
                "Eats the same meal every day",
                "Tries a new food every chance",
                "Always volunteers first",
                "Avoids volunteering",
                "Writes poetry secretly",
                "Keeps souvenirs from missions",
                "Refuses to sit with their back to a door",
                "Always chooses the window seat",
                "Checks behind themselves frequently",
                "Whistles while working",
                "Collects shiny objects",
                "Talks overly politely",
                "Nods constantly while listening",
                "Tilts head when confused",
                "Giggles nervously",
                "Always wants to negotiate",
                "Avoids confrontation at all costs",
                "Overreacts to small noises",
                "Keeps plants and tends them carefully",
                "Gambles whenever possible",
                "Carries a good-luck charm",
                "Has a ritual before missions",
                "Refuses to walk under ladders",
                "Keeps secrets easily",
                "Shares too much personal info",
                "Hums the same tune repeatedly",
                "Reads every label and warning",
                "Walks very quietly",
                "Walks extremely loudly",
                "Always optimistic about outcomes",
                "Always expects the worst",
                "Gives long speeches",
                "Stretches constantly",
                "Uses humor to defuse tension",
                "Talks big but rarely follows through",
                "Keeps everything meticulously organized"
            };

            Random random = RandomUtils.random;

            return habitsAndBehaviors[random.Next(habitsAndBehaviors.Count)];
        }

        public string GetFears()
        {
            Random random = RandomUtils.random;
            List<string> fearsAndPhobias = new List<string>()
            {
                "Fear of heights",
                "Fear of deep water",
                "Fear of drowning",
                "Fear of small spaces",
                "Fear of the dark",
                "Fear of spiders",
                "Fear of insects",
                "Fear of snakes",
                "Fear of flying",
                "Fear of enclosed spaces",
                "Fear of open spaces",
                "Fear of loud noises",
                "Fear of ghosts",
                "Fear of aliens",
                "Fear of being watched",
                "Fear of failure",
                "Fear of being alone",
                "Fear of large crowds",
                "Fear of betrayal",
                "Fear of pain",
                "Fear of fire",
                "Fear of robots",
                "Fear of AI",
                "Fear of spacewalks",
                "Fear of zero gravity",
                "Fear of losing control",
                "Fear of death",
                "Fear of injury",
                "Fear of germs",
                "Fear of medical procedures",
                "Fear of needles",
                "Fear of blood",
                "Fear of suffocation",
                "Fear of being forgotten",
                "Fear of authority",
                "Fear of confrontation",
                "Fear of commitment",
                "Fear of abandonment",
                "Fear of intimacy",
                "Fear of failure in combat",
                "Fear of navigation errors",
                "Fear of system malfunctions",
                "Fear of explosions",
                "Fear of toxic environments",
                "Fear of extreme temperatures",
                "Fear of poison",
                "Fear of losing oxygen",
                "Fear of void exposure",
                "Fear of meteor storms",
                "Fear of confined crew quarters",
                "Fear of strange planets",
                "Fear of unknown species",
                "Fear of darkness in space",
                "Fear of malfunctioning equipment",
                "Fear of hostile factions",
                "Fear of mind control",
                "Fear of hallucinations",
                "Fear of isolation",
                "Fear of panic attacks",
                "Fear of being judged",
                "Fear of speaking publicly",
                "Fear of being misunderstood",
                "Fear of getting lost",
                "Fear of losing memory",
                "Fear of time loops",
                "Fear of mutiny",
                "Fear of sabotage",
                "Fear of contaminated food",
                "Fear of mold or fungi",
                "Fear of magnetic storms",
                "Fear of deep-space anomalies",
                "Fear of loud alarms",
                "Fear of emergency situations",
                "Fear of unpredictable behavior",
                "Fear of betrayal by allies",
                "Fear of being trapped under rubble",
                "Fear of heights inside large structures",
                "Fear of teleportation",
                "Fear of clonings gone wrong",
                "Fear of seeing dead bodies",
                "Fear of being hunted",
                "Fear of losing teammates",
                "Fear of failing missions",
                "Fear of rapid decompression",
                "Fear of waking up restrained",
                "Fear of disease outbreaks",
                "Fear of radiation",
                "Fear of unstable terrain",
                "Fear of darkness in caves",
                "Fear of giant creatures",
                "Fear of unknown artifacts",
                "Fear of cosmic storms",
                "Fear of malfunctioning AI companions",
                "Fear of contaminated planets",
                "Fear of sudden explosions",
                "Fear of being buried alive",
                "Fear of uncharted space",
                "Fear of silent environments"
            };

            return fearsAndPhobias[random.Next(fearsAndPhobias.Count)];
        }

        public string GetGoals()
        {
            Random random = RandomUtils.random;
            List<string> motivationsAndGoals = new List<string>()
            {
                "Seeking wealth",
                "Seeking fame",
                "Seeking revenge",
                "Searching for lost family",
                "Searching for a missing friend",
                "Trying to clear their name",
                "Hoping to escape their past",
                "Seeking knowledge",
                "Proving themselves worthy",
                "Seeking justice",
                "Protecting someone important",
                "Protecting their homeworld",
                "Trying to rebuild their life",
                "Looking for a new beginning",
                "Trying to atone for past mistakes",
                "Desiring power",
                "Desiring recognition",
                "Ambition to lead a faction",
                "Trying to uncover a conspiracy",
                "Trying to stop a war",
                "Trying to prevent disaster",
                "Collecting rare artifacts",
                "Mapping unexplored regions",
                "Advancing scientific understanding",
                "Becoming the best in their field",
                "Earning the respect of peers",
                "Trying to save a dying planet",
                "Finding a cure for a disease",
                "Trying to become wealthy enough to retire",
                "Trying to protect the frontier",
                "Finding purpose in life",
                "Finding inner peace",
                "Escaping a bounty on their head",
                "Avoiding dangerous enemies",
                "Fulfilling a promise",
                "Honoring a fallen comrade",
                "Fighting for a cause they believe in",
                "Uncovering ancient secrets",
                "Learning forbidden knowledge",
                "Finding spiritual enlightenment",
                "Securing enough resources for their colony",
                "Creating a new invention",
                "Proving a scientific theory",
                "Becoming a legendary explorer",
                "Reuniting with their estranged family",
                "Protecting the weak",
                "Saving their mentor",
                "Finding redemption",
                "Fleeing a prophecy",
                "Trying to fulfill a prophecy",
                "Restoring their family’s honor",
                "Breaking free from a controlling faction",
                "Building a successful business",
                "Creating a safe haven for others",
                "Understanding the universe",
                "Learning the truth about their origins",
                "Becoming a master pilot",
                "Obtaining a rare or forbidden item",
                "Ending corruption in a faction",
                "Saving someone kidnapped",
                "Preventing the spread of a dangerous technology",
                "Building a powerful alliance",
                "Getting revenge on a rival faction",
                "Becoming a top strategist",
                "Completing a lifelong dream",
                "Escaping a collapsing star system",
                "Seeking artistic inspiration",
                "Trying to rescue survivors of a disaster",
                "Aiming to be self-sufficient",
                "Stopping an interstellar crime ring",
                "Recovering stolen property",
                "Trying to win a major competition",
                "Achieving political influence",
                "Protecting their crew",
                "Becoming a mentor to others",
                "Learning a rare skill",
                "Recovering from trauma",
                "Finding a safe new homeworld",
                "Studying alien cultures",
                "Teaching others what they know",
                "Gathering intelligence for a faction",
                "Stopping environmental destruction",
                "Uncovering corruption in leadership",
                "Seeking adventure",
                "Chasing a legendary creature",
                "Establishing a new colony",
                "Reclaiming lost territory",
                "Ending a feud",
                "Building the perfect starship",
                "Solving an ancient mystery",
                "Breaking a powerful curse",
                "Becoming a symbol of hope",
                "Achieving ultimate freedom",
                "Finding true love",
                "Completing a mysterious mission",
                "Finishing their life’s work",
                "Leaving a lasting legacy"
            };


            return motivationsAndGoals[random.Next(motivationsAndGoals.Count)];
        }

        public string GetJob()
        {
            Random random = RandomUtils.random;

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
                "Space pirate",
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
                "Black market dealer",
                "Aurora smuggler",
                "Contraband runner",
                "Crimson Fleet privateer",
                "Freestar deserter-for-hire",
                "Salvage raider",
                "Data slicer",
                "Info-broker",
                "Black market cybertech installer",
                "Illegal neuroamp fitter",
                "Synthetic drug chemist",
                "Ship graveyard scavenger",
                "Frontier rustler",
                "Water ration thief",
                "Power cell black marketer",
                "Prohibited xenotech dealer",
                "Biohazard smuggler",
                "Medical supply thief",
                "Starship part counterfeiter",
                "Sensor jammer technician",
                "Encrypted comms interceptor",
                "Ad board hacker",
                "Clone ID generator",
                "Underground arena organizer",
                "Debt collector for gangs",
                "Va’ruun cult recruiter",
                "Stolen mech part seller",
                "Orbital scrapyard thief",
                "Illegal surveyor",
                "Unlicensed prospector",
                "Artifact poacher",
                "High-risk courier",
                "Frontier lockbreaker",
                "Underbelly fixer",
                "Corporate black-bag operator",
                "Planetary smog-filter thief",
                "Freight container manipulator",
                "Spaceport baggage thief",
                "Asteroid claim jumper",
                "Underground gene-hacker",
                "DNA forger",
                "Black market droid programmer",
                "Covert propulsion tuner",
                "Ship transponder spoiler",
                "Heat signature scrambler",
                "Credit siphoner",
                "Black site interrogator",
                "Illicit mercenary",
                "Hazard zone looter",
                "Off-registry starship builder",
                "Zero-G infiltration specialist",
                "Corporate lobbyist",
                "Risk assessment analyst",
                "Financial auditor",
                "Compliance officer",
                "Supply chain coordinator",
                "Trade route analyst",
                "Market research consultant",
                "Corporate negotiator",
                "Data privacy officer",
                "Interstellar logistics planner",
                "Investment portfolio manager",
                "Commodity futures broker",
                "Resource allocation specialist",
                "Corporate communications advisor",
                "Public relations strategist",
                "Brand reputation manager",
                "Strategic partnerships director",
                "Inter-corporate liaison",
                "Operations efficiency analyst",
                "Procurement specialist",
                "Infrastructure project manager",
                "Systems integration coordinator",
                "Talent acquisition consultant",
                "Human capital strategist",
                "Training and development manager",
                "Corporate policy architect",
                "Workforce optimization analyst",
                "Corporate ethics investigator",
                "Internal process auditor",
                "Quality assurance supervisor",
                "Data governance specialist",
                "Financial compliance reviewer",
                "Executive assistant",
                "Corporate event planner",
                "Client relations manager",
                "Enterprise software consultant",
                "Technical documentation writer",
                "Corporate research archivist",
                "Neuroamp regulations analyst",
                "Freestar trade compliance officer",
                "UC infrastructure bureaucrat",
                "Energy grid efficiency consultant",
                "Orbital station planning analyst",
                "Corporate acquisition reviewer",
                "Economic impact modeler",
                "Interstellar taxation advisor",
                "Shareholder relations coordinator",
                "Resource extraction logistics planner",
                "Corporate arbitration specialist",
                "Strategic forecasting analyst",
                "Neuroamp developer",
                "Cognitive interface engineer",
                "Corporate intelligence analyst",
                "Market influence strategist",
                "R&D lab technician",
                "Cybernetic systems researcher",
                "Neural pattern quality tester",
                "Behavioral data modeler",
                "Biometric security designer",
                "Internal security auditor",
                "Corporate espionage agent",
                "Executive shadow operative",
                "Black-file investigator",
                "Stealth tech engineer",
                "Nanochip calibration technician",
                "Augmented reality interface designer",
                "Sensory feedback engineer",
                "Neuro-response tester",
                "Human-machine integration specialist",
                "Cognitive load analyst",
                "Ryujin Tower compliance officer",
                "Employee performance profiler",
                "Corporate relocation coordinator",
                "Ethical loophole researcher",
                "Talent scouting operative",
                "Innovation pipeline manager",
                "Prototype stress tester",
                "Executive efficiency consultant",
                "Risk mitigation planner",
                "Covert data migration specialist",
                "Corporate culture architect",
                "Decision-matrix analyst",
                "Systems infiltration technician",
                "Trade secret protection officer",
                "Biometric ID investigator",
                "Neuroamp firmware developer",
                "Cognitive uplift consultant",
                "Employee monitoring supervisor",
                "UX neural flow designer",
                "Product influence strategist",
                "Corporate persuasion specialist",
                "Black-budget project handler",
                "Strategic crisis architect",
                "Internal deception profiler",
                "Ryujin brand intelligence scout",
                "Corporate innovation scout",
                "High-value client handler",
                "Competitive advantage analyst",
                "Ryujin Tower floor operations manager",
                "Neuro-signal encryption specialist"
            };

            return joblist[random.Next(joblist.Count)];
        }

        public static string GetNationality()
        {
            Random random = RandomUtils.random;

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
