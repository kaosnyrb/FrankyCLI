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
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Mutagen.Bethesda.FormKeys.Starfield.Starfield;

namespace FrankyCLI.questgen_tools
{
    // A gang is a collection of nameless goons that are used in missions.
    // Spacers are an example of an vanilla gang.
    // This will create a formlist of NPCs with generic names that can be spawned in the missions.

    public  class GangNoun
    {
        public StarfieldMod myMod;
        public string gangName;
        public Mutagen.Bethesda.Starfield.FormList gangList;

        public GangNoun(StarfieldMod myModparam)
        {
            myMod = myModparam;
            gangName = GetGangName();

            AITools.RunPrompt("<Lore> There is a gang of people called " + gangName + " who are assiting the target");

            gangList = GenerateGang();
        }

        public static string GetGangName()
        {
            Random random = RandomUtils.random;
            List<string> gangPrefixes = new List<string>
            {
                "Red", "Black", "Iron", "Steel", "Rust", "Grim", "Dead", "Broken", "Shadow",
                "Blood", "Night", "Grave", "Ash", "Gutter", "Backstreet", "Lowtown", "Hollow",
                "Cross", "Eastside", "Westside", "Southend", "Northblock", "Dust", "Mud",
                "Scrap", "Brick", "Stone", "Razor", "Chain", "Wire", "Block", "Wasteland",
                "Chrome", "Slag", "Smoke", "Sewer", "Under", "Bleak", "Ridge", "Ironbound",
                "Drift", "Lockjaw", "Blacktop", "Cracked", "Scar", "Vandal", "Pitch",
                "Copper", "Tin", "Lead", "Rot", "Slick", "Grime", "Blight", "Rivet",
                "Forge", "Rusted", "Cold", "Frost", "Burnt", "Charred", "Smolder",
                "Ember", "Thunder", "Storm", "Wild", "Feral", "Nomad", "Stray", "Lone",
                "Bone", "Skull", "Hate", "Vice", "Sorrow", "Dread", "Void", "Wraith",
                "Rumble", "Ruckus", "Scrapper", "Chainlink", "Barbed", "Hellbound", "Crimson",
                "Pale", "Ivory", "Coal", "Shiv", "Needle", "Soot", "Gloom", "Tangle",
                "Vermin", "Slickline", "Ironcore", "Blackwire", "Gravel", "Murk", "Roughcut",
                // Military phonetic alphabet & tactical designators
                "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Gamma", "Hotel",
                "Kilo", "Lima", "Omega", "Sierra", "Tango", "Uniform", "Victor", "Zulu",
                "Squad", "Unit", "Division", "Sector", "Zone", "Company", "Battalion",
                "Tier-One", "Strike", "Recon", "Forward", "Tactical", "Rapid", "Command",
                "Vector", "Grid", "Perimeter", "Outpost", "Protocol", "Cipher", "Directive",

                // Paramilitary / PMC-flavored
                "Blacksite", "Shadowcell", "Darkwatch", "Ironfront", "Redline", "Nightwatch",
                "Warpath", "Overwatch", "Sentinel", "Bulwark", "Vanguard", "Helix", "Crucible",
                "Legion", "Taskforce", "Cerberus", "Executioner", "Skirmish", "Breach",
            };
            List<string> gangSuffixes = new List<string>
            {
                "Reapers", "Rats", "Jackals", "Vipers", "Saints", "Devils", "Serpents", "Breakers", "Wolves",
                "Talons", "Mongrels", "Phantoms", "Specters", "Ghouls", "Grinders", "Cutthroats", "Rogues",
                "Drifters", "Raiders", "Bruisers", "Stalkers", "Outcasts", "Ironclaws", "Deadlights", "Bonecrushers",
                "Shadows", "Hollows", "Nightfolk", "Eclipsers", "Backlot Boys", "Dustwalkers", "Ridge Runners",
                "Streetburners", "Ashborn", "Pack", "Crimson Lot", "Black Fangs", "Gravepack", "Scrapwolves",
                "Rubble Rats", "Steel Vipers", "Gutter Kings", "Wastelanders", "Faultliners", "Lowborn",
                "Blackjacks", "Chain Runners", "Shivmasters", "Lockjaw Crew", "Thunder Dogs", "Pit Wolves",
                "Slick Syndicate", "Rust Syndicate", "Dripline Crew", "Backbreaker Union", "Molten Skulls",
                "Needle Boys", "Ironbloods", "Edgewalkers", "Night Wreckers", "Frosthands", "Grime Pact",
                "Razorbacks", "Slagborn", "Grim Company", "Gravel Kings", "Mirefolk", "Dust Devils", "Hellpack",
                "Red Lanterns", "Chrome Fangs", "Wreckrats", "Basement Lords", "Block Runners", "Gutterline",
                "Ash Syndicate", "Crackstone Crew", "Chainlink Mob", "Rustmarks", "Night Chain", "Hollow Sons",
                "Deadwater Crew", "Blight Riders", "Iron Syndicate", "Ravagers", "Hellchain", "Spinebreakers",
                "Rotfangs", "Blackwater Pact", "Rage Unit", "Broken Crown", "Silk Knives", "Rubble Born",
                "Wire Rats", "Torchline", "Gutter Serpents", "Blood Signal", "Shadow Union", "Vermin Pact"
            };

            return gangPrefixes[random.Next(gangPrefixes.Count)] + " " + gangSuffixes[random.Next(gangSuffixes.Count)];
        }

        public Mutagen.Bethesda.Starfield.FormList GenerateGang()
        {
            Random random = RandomUtils.random;

            var list = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>();
            
            //Generate a new NPC

            var outfit = NPCTools.GetRandomOutfit(true);

            int gangmembers = 2 + random.Next(5);

            for (int i = 0; i < gangmembers; i++)
            {
                bool isfemale = false;
                if (random.Next(100) > 50)
                {
                    isfemale = true;
                }

                var NPC = myMod.Npcs[new FormKey(myMod.ModKey, NPCTools.GetTemplateNPC(isfemale))].DeepCopy();
                Npc npc = NPCTools.CloneNPC(myMod, NPC);
                string Gender = "Male";
                if (isfemale) Gender = "Female";

                 npc.Name = AITools.RunPrompt("Generate a first name and last name for a "
                    + Gender + " "
                    + gangName + " gang member. Return only the name in the response. " +
                    "Don't use any of the names that have appeared before.");
                npc.EditorID = "npc_" + (npc.Name.ToString().ToLower()).Replace(" ", "");
                Random wrand = RandomUtils.random;
                npc.Weight = new NpcWeight()
                {
                    Fat = (float)wrand.NextDouble(),
                    Muscular = (float)wrand.NextDouble(),
                    Thin = (float)wrand.NextDouble()
                };
                var lev = new PcLevelMult();
                lev.LevelMult = 0.25f + (float)random.NextDouble();
                npc.Level = lev;
                npc.SpaceOutfit = outfit;
                npc.EyeColor = NPCTools.GetEyeColour();
                npc.HairColor = NPCTools.GetHairColour();
                npc.SkinToneIndex = (byte)wrand.Next(8);
                npc.HeadParts.Add(NPCTools.GetHaircut(isfemale));
                npc.Items = new ExtendedList<ContainerEntry>
                {
                    new ContainerEntry() { Item = new ContainerItem() { Item = NPCTools.GetRandomGear(), Count = 1 } }
                };

                //Logfile for Crew Member
                if (i == gangmembers - 1)
                {
                    //We do this last as we know all the crew now. Also only once as they are a bit samey.
                    Console.WriteLine("Generating Crew Log file...");
                    string BookPrompt = "Write a personal diary entry for " + npc.Name + ", a " + Gender + " member of the " + gangName + ".";
                    BookPrompt += "Use a first - person voice that reflects their personality, emotional state, and current circumstances.";
                    BookPrompt += "Use the previously generated crew names for this gang.";
                    BookPrompt += "Make the entry feel immersive, introspective, character-driven and suitable as lore flavor for a quest.";

                    BookPrompt = PromptFlavourTools.AddFlavourToGangBook(BookPrompt);


                    string BookContents = AITools.RunPrompt(BookPrompt);
                    var Book = gen_quest_main.myMod.Books[new FormKey(gen_quest_main.myMod.ModKey, 0x000905)].DeepCopy();
                    Book bountybook = new Book(gen_quest_main.myMod)
                    {
                        CNAM = Book.CNAM,
                        Components = Book.Components,
                        Description = BookContents,
                        DNAMUnknown = Book.DNAMUnknown,
                        DropdownSound = Book.DropdownSound,
                        EditorID = "book_" + (npc.Name.ToString().ToLower()).Replace(" ", ""),
                        Keywords = Book.Keywords,
                        ENAM = Book.ENAM,
                        FeaturedItemMessage = Book.FeaturedItemMessage,
                        Flags = Book.Flags,
                        FNAM = Book.FNAM,
                        InventoryArt = Book.InventoryArt,
                        Model = Book.Model,
                        Name = npc.Name.ToString() + " Logs",
                        ODTY = Book.ODTY,
                        Value = Book.Value,
                        Weight = Book.Weight,
                        VirtualMachineAdapter = Book.VirtualMachineAdapter,
                        Transforms = Book.Transforms,
                    };

                    gen_quest_main.myMod.Books.Add(bountybook);
                    npc.Items.Add(new ContainerEntry() { Item = new ContainerItem() { Item = gen_quest_main.myMod.Books[bountybook.FormKey].ToLink(), Count = 1 } });
                }

                myMod.Npcs.Add(npc);
                //Add it to the list
                list.Add(npc);
            }
            //Save the list
            var Formlistclone = myMod.FormLists[new FormKey(myMod.ModKey, 0x000805)].DeepCopy();
            Mutagen.Bethesda.Starfield.FormList formList = new Mutagen.Bethesda.Starfield.FormList(myMod)
            {
                EditorID = "frmlist_" + Guid.NewGuid().ToString().Substring(0, 8),
                Items = list,
            };

            myMod.FormLists.Add(formList);

            return formList;
        }
    }
}
