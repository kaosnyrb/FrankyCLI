using FrankyCLI.questgen_tools;
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

    public  class OutlawGang
    {
        public StarfieldMod myMod;
        public string gangName;

        public OutlawGang(StarfieldMod myModparam, string gangNameparam)
        {
            myMod = myModparam;
            gangName = gangNameparam;

            AITools.RunPrompt("<Lore> There is a gang of people called " + gangName + " who are assiting the target");
        }

        public static string GetGangTheme()
        {
            Random random = new Random();

            List<string> ganglist = new List<string>()
            {
                "Urban cyberpunk street gang",
                "Rogue space pirate faction",
                "Paramilitary mercenary company",
                "Bio-augmented criminal syndicate",
                "Black-market tech smugglers",
                "Nomadic wasteland raider clan",
                "Elite assassin brotherhood",
                "Underground robotics cult",
                "Rebel freedom-fighter cell",
                "Corporate espionage division",
                "Post-apocalyptic scavenger tribe",
                "Void-dwelling marauder fleet",
                "Mutant undercity crime family",
                "High-society criminal cartel",
                "Fanatical doomsday cult",
                "AI-controlled pirate collective",
                "Stealth-oriented infiltration unit",
                "Chemical-enhanced gladiator gang",
                "Ancient relic-hunters guild",
                "Quantum anomaly worshippers",
                "Smuggler-trader nomad caravans",
                "Deep-space salvage pirates",
                "Telepathic crime circle",
                "Ex-military deserter faction",
                "Genetically-engineered outcast tribe",
                "Orbital scrap-reaver union",
                "Neo-frontier dustland bandits",
                "Cold-void infiltration cult",
                "Illicit xenobiology harvesters",
                "Contraband sensor-jammer crew",
                "Black-helm paramilitary enforcers",
                "Gravity-well ambush legion",
                "Underbelly organ-trading ring",
                "Pirate grav-drive hijacker squad",
                "Radiation-zone nomad hunters",
                "Augmetic street-warrior horde",
                "Lawless frontier marshal impostors",
                "Cosmic rift superstition sect",
                "Shadow-market auction guards",
                "Deep-mine exiles turned raiders",
                "Atmospheric processor saboteurs",
                "Shipbreaker hull-clan scavengers",
                "Dishonored UC veteran brotherhood",
                "Freestar-wanted outlaw posse",
                "Off-grid techno-survivalists",
                "Microfusion-lab rogue technicians",
                "Disavowed corporate strike team",
                "Void-mask smuggler assassins",
                "Grav-jump relay saboteur crew",
                "Asteroid claim-jumping militias",
                "Illicit drone-warfare swarmers",
                "Genesplice cult of perfectionists",
                "Underdeck chem-running battalion",
                "Stargrave relic-hungry fanatics",
                "Atmospheric skimmer piracy ring"
            };

            return ganglist[random.Next(ganglist.Count)];
        }

        public Mutagen.Bethesda.Starfield.FormList GenerateGang()
        {
            Random random = new Random();

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
                Random wrand = new Random();
                npc.Weight = new NpcWeight()
                {
                    Fat = (float)wrand.NextDouble(),
                    Muscular = (float)wrand.NextDouble(),
                    Thin = (float)wrand.NextDouble()
                };
                var lev = new PcLevelMult();
                lev.LevelMult = (float)random.NextDouble();
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

                    BookPrompt = PromptFlavourTools.AddFlavourToShipBook(BookPrompt);


                    string BookContents = AITools.RunPrompt(BookPrompt);
                    var Book = gen_quest.myMod.Books[new FormKey(gen_quest.myMod.ModKey, 0x000905)].DeepCopy();
                    Book bountybook = new Book(gen_quest.myMod)
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

                    gen_quest.myMod.Books.Add(bountybook);
                    npc.Items.Add(new ContainerEntry() { Item = new ContainerItem() { Item = gen_quest.myMod.Books[bountybook.FormKey].ToLink(), Count = 1 } });
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
