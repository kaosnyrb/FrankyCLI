using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Interfaces;
using FrankyCLI.questgen_tools.Nouns;
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
    public class NamedFactionCrew_Betrayal : ICrew
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

            //Generate the Betrayer
            bool betrayerisfemale = false;
            if (random.Next(100) > 50)
            {
                betrayerisfemale = true;
            }

            var BetraySourceNPC = gen_quest_main.myMod.Npcs[new FormKey(gen_quest_main.myMod.ModKey, NPCTools.GetTemplateNPC(betrayerisfemale))].DeepCopy();
            Npc Betrayernpc = NPCTools.CloneNPC(gen_quest_main.myMod, BetraySourceNPC);

            //Name
            Console.WriteLine("Generating Crew Name...");
            string betrayerGender = "Male";
            if (betrayerisfemale) betrayerGender = "Female";
            Betrayernpc.Name = AITools.RunPrompt(
                "Generate a believable full name (first and last) for a " + betrayerGender +
                " crew member serving with the " + Faction +
                " aboard the starship " + ShipName + ".\r\n" +
                "The name should subtly reflect the faction's culture, tone, and typical naming style.\r\n" +
                "Do not include titles, ranks, or additional commentary.\r\n" +
                "Return only the name."
            );
            Betrayernpc.EditorID = "npc_" + (Betrayernpc.Name.ToString().ToLower()).Replace(" ", "");

            Random betrayer_wrand = RandomUtils.random;
            Betrayernpc.Weight = new NpcWeight()
            {
                Fat = (float)betrayer_wrand.NextDouble(),
                Muscular = (float)betrayer_wrand.NextDouble(),
                Thin = (float)betrayer_wrand.NextDouble()
            };
            var betrayer_lev = new PcLevelMult();
            betrayer_lev.LevelMult = (float)random.NextDouble();
            Betrayernpc.Level = betrayer_lev;
            Betrayernpc.SpaceOutfit = outfit;

            Betrayernpc.EyeColor = NPCTools.GetEyeColour();
            Betrayernpc.HairColor = NPCTools.GetHairColour();
            Betrayernpc.SkinToneIndex = (byte)betrayer_wrand.Next(8);
            Betrayernpc.HeadParts.Add(NPCTools.GetHaircut(betrayerisfemale));

            Betrayernpc.Items = new ExtendedList<ContainerEntry>
            {
                new ContainerEntry() { Item = new ContainerItem() { Item = NPCTools.GetRandomGear(), Count = 1 } },

            };

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
                        "\r\n" +
                        "Betrayal Context:\r\n" +
                        "- One crew member, named " + Betrayernpc.Name + ", has been secretly paid off by an outside party to betray the ship and its crew.\r\n" +
                        "- The betrayal results in all other crew members dying; only " + Betrayernpc.Name + " survives and remains aboard " + ShipName + ".\r\n" +
                        "- If the writer's name (" + npc.Name + ") IS " + Betrayernpc.Name + ", then this diary entry is their final reflection after the betrayal, written in the aftermath while they are alone on the ship, dealing with the consequences, payment, guilt, or justification.\r\n" +
                        "- If the writer's name (" + npc.Name + ") is NOT " + Betrayernpc.Name + ", then this diary entry is written shortly BEFORE their death, with only suspicions, unease, or small details that, in hindsight, foreshadow the betrayal by " + Betrayernpc.Name + ". The writer does not know they are about to die.\r\n" +
                        "- In either case, the reality that all other crew die and only " + Betrayernpc.Name + " remains aboard must be reflected in tone, implication, or direct acknowledgment appropriate to the writer's perspective.\r\n" +
                        "\r\n" +
                        "Return only the diary entry.";

                    BookPrompt = PromptFlavourTools.AddFlavourToShipBook(BookPrompt);


                    string BookContents = AITools.RunPrompt(BookPrompt);
                    BookNoun bountybook = new BookNoun(0x000905, npc.Name.ToString() + " " + RandomUtils.GetLogSynonym(), "", BookContents);

                    gen_quest_main.myMod.Books.Add(bountybook.instance);
                    npc.Items.Add(new ContainerEntry() { Item = new ContainerItem() { Item = gen_quest_main.myMod.Books[bountybook.instance.FormKey].ToLink(), Count = 1 } });
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
