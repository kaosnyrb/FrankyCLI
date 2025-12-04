using FrankyCLI.questgen_tools;
using Microsoft.Extensions.Logging;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Noggog.StructuredStrings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core;
using static Loqui.EqualsMaskHelper;

namespace FrankyCLI.questgen_tools
{
    public class CrewTools
    {
        public static IFormLink<IStarfieldMajorRecordGetter> GetCrewFormList(string Faction,string ShipName)
        {
            var frmlst = new FormList(gen_quest.myMod)
            {
                EditorID = ShipName + "_crewlist",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };
            //Dead Named Crew
            var list = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>();
            Random random = new Random();
            //Generate a new NPC
            var outfit = NPCTools.GetRandomFactionOutfit(Faction);

            bool generatebook = false;
            int crewcount = 5;

            for (int i = 0; i < crewcount; i++)
            {
                bool isfemale = false;
                if (random.Next(100) > 50)
                {
                    isfemale = true;
                }

                var NPC = gen_quest.myMod.Npcs[new FormKey(gen_quest.myMod.ModKey, NPCTools.GetTemplateDeadNPC(isfemale))].DeepCopy();
                Npc npc = NPCTools.CloneNPC(gen_quest.myMod, NPC);

                //Name
                Console.WriteLine("Generating Crew Name...");
                string Gender = "Male";
                if (isfemale) Gender = "Female";
                npc.Name = AITools.RunPrompt("Generate a first name and last name for a " 
                    + Gender + " "
                    + Faction + " crew member onboard the " 
                    + ShipName + ". Return only the name in the response.");
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
                    new ContainerEntry() { Item = new ContainerItem() { Item = NPCTools.GetRandomGear(), Count = 1 } },

                };


                //Logfile for Crew Member
                if (i == crewcount-1)
                {
                    //We do this last as we know all the crew now. Also only once as they are a bit samey.
                    Console.WriteLine("Generating Crew Log file...");
                    string BookPrompt = "Write a personal diary entry for " + npc.Name + ", a " + Gender + " crew member aboard the " + ShipName + ".";
                    BookPrompt += "Use a first - person voice that reflects their personality, emotional state, and current circumstances.";
                    BookPrompt += "Use the previously generated crew names for this ship.";
                    BookPrompt += "Make the entry feel immersive, introspective, character-driven and suitable as lore flavor for a quest.";

                    BookPrompt = BookTools.AddFlavourToShipBook(BookPrompt);


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

                gen_quest.myMod.Npcs.Add(npc);
                //Add it to the list
                list.Add(npc);
                frmlst.Items.Add(npc);
            }

            gen_quest.myMod.FormLists.Add(frmlst);
            return gen_quest.myMod.FormLists[frmlst.FormKey].ToLink<IStarfieldMajorRecordGetter>();
        }
    }
}
