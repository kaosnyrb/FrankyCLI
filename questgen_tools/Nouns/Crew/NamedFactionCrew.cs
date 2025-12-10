using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Interfaces;
using FrankyCLI.questgen_tools.Utils;
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
    public class NamedFactionCrew : ICrew
    {
        public IFormLink<IStarfieldMajorRecordGetter> GetCrewFormList(string Faction,string ShipName)
        {
            var frmlst = new FormList(gen_quest_main.myMod)
            {
                EditorID = ShipName + "_crewlist",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };
            //Dead Named Crew
            var list = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>();
            Random random = RandomUtils.random;
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

                var NPC = gen_quest_main.myMod.Npcs[new FormKey(gen_quest_main.myMod.ModKey, NPCTools.GetTemplateDeadNPC(isfemale))].DeepCopy();
                Npc npc = NPCTools.CloneNPC(gen_quest_main.myMod, NPC);

                //Name
                Console.WriteLine("Generating Crew Name...");
                string Gender = "Male";
                if (isfemale) Gender = "Female";
                npc.Name = AITools.RunPrompt(
                    "Generate a believable full name (first and last) for a " + Gender +
                    " crew member serving with the " + Faction +
                    " aboard the starship " + ShipName + ".\r\n" +
                    "The name should subtly reflect the faction's culture, tone, and typical naming style.\r\n" +
                    "Do not include titles, ranks, or additional commentary.\r\n" +
                    "Return only the name."
                );
                npc.EditorID = "npc_" + (npc.Name.ToString().ToLower()).Replace(" ", "");
               
                Random wrand = RandomUtils.random;
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
                    string BookPrompt =
                        "Write a personal diary entry for " + npc.Name + ", a " + Gender +
                        " crew member serving aboard the starship " + ShipName + ".\r\n" +
                        "Use a first-person voice that reflects their personality, emotional state, and day-to-day reality onboard.\r\n" +
                        "Incorporate subtle references to the ship’s routine, recent events, and interpersonal dynamics among the crew.\r\n" +
                        "Use the previously generated crew names for this ship naturally, as people the writer interacts with, trusts, avoids, or worries about.\r\n" +
                        "Gently weave in rumours, whispered concerns, unverified secrets, or quiet tensions implied by the LoreContext. Present them as things the writer has overheard, suspects, or privately wonders about.\r\n" +
                        "Do NOT quote or reference the LoreContext directly, and do not introduce new proper nouns that do not appear in the existing lore.\r\n" +
                        "Make the entry immersive, introspective, and character-driven—something suitable as subtle lore flavor for a quest.\r\n" +
                        "Avoid exposition or formal reporting; write as if the character is confiding in their personal diary.\r\n" +
                        "Do not break the fourth wall, mention a prompt, or acknowledge the reader.\r\n" +
                        "Focus on mood, personal thoughts, fears, frustrations, hopes, relationships, and the shifting atmosphere aboard the ship.\r\n" +
                        "Return only the diary entry.";

                    BookPrompt = PromptFlavourTools.AddFlavourToShipBook(BookPrompt);


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
                        Name = npc.Name.ToString() + " " + RandomUtils.GetLogSynonym(),
                        ODTY = Book.ODTY,
                        Value = Book.Value,
                        Weight = Book.Weight,
                        VirtualMachineAdapter = Book.VirtualMachineAdapter,
                        Transforms = Book.Transforms,
                    };

                    gen_quest_main.myMod.Books.Add(bountybook);
                    npc.Items.Add(new ContainerEntry() { Item = new ContainerItem() { Item = gen_quest_main.myMod.Books[bountybook.FormKey].ToLink(), Count = 1 } });
                }

                gen_quest_main.myMod.Npcs.Add(npc);
                //Add it to the list
                list.Add(npc);
                frmlst.Items.Add(npc);
            }

            gen_quest_main.myMod.FormLists.Add(frmlst);
            return gen_quest_main.myMod.FormLists[frmlst.FormKey].ToLink<IStarfieldMajorRecordGetter>();
        }
    }
}
