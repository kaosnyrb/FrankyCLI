using Retrograde.Interfaces;
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

namespace Retrograde.Nouns.Crew
{
    public class NamedFactionCrew_Betrayal : ICrew
    {
        /// <summary>
        /// Delegate for generating names using AI or other methods.
        /// Must be set by the consuming application.
        /// </summary>
        public static Func<string, string> NameGenerator { get; set; }

        /// <summary>
        /// Delegate for generating book content.
        /// Must be set by the consuming application.
        /// </summary>
        public static Func<string, string> BookGenerator { get; set; }

        public IFormLink<IStarfieldMajorRecordGetter> GetCrewFormList(string Faction,string ShipName)
        {
            var targetMod = RetrogradeContext.Current.TargetMod;

            var frmlst = new FormList(targetMod)
            {
                EditorID = ShipName + "_crewlist",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };
            //Dead Named Crew
            var list = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>();
            Random random = RandomProvider.Random;
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

            var BetraySourceNPC = targetMod.Npcs[new FormKey(targetMod.ModKey, NPCTools.GetTemplateNPC(betrayerisfemale))].DeepCopy();
            Npc Betrayernpc = NPCTools.CloneNPC(targetMod, BetraySourceNPC);

            //Name
            Console.WriteLine("Generating Crew Name...");
            string betrayerGender = "Male";
            if (betrayerisfemale) betrayerGender = "Female";

            string betrayerNamePrompt =
                "Generate a believable full name (first and last) for a " + betrayerGender +
                " crew member serving with the " + Faction +
                " aboard the starship " + ShipName + ".\r\n" +
                "The name should subtly reflect the faction's culture, tone, and typical naming style.\r\n" +
                "Do not include titles, ranks, or additional commentary.\r\n" +
                "Return only the name.";

            Betrayernpc.Name = NameGenerator?.Invoke(betrayerNamePrompt) ?? (Faction + " Betrayer");
            Betrayernpc.EditorID = "npc_" + (Betrayernpc.Name.ToString().ToLower()).Replace(" ", "");

            Random betrayer_wrand = RandomProvider.Random;
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

                var NPC = targetMod.Npcs[new FormKey(targetMod.ModKey, NPCTools.GetTemplateDeadNPC(isfemale))].DeepCopy();
                Npc npc = NPCTools.CloneNPC(targetMod, NPC);

                //Name
                Console.WriteLine("Generating Crew Name...");
                string Gender = "Male";
                if (isfemale) Gender = "Female";

                string namePrompt =
                    "Generate a believable full name (first and last) for a " + Gender +
                    " crew member serving with the " + Faction +
                    " aboard the starship " + ShipName + ".\r\n" +
                    "The name should subtly reflect the faction's culture, tone, and typical naming style.\r\n" +
                    "Do not include titles, ranks, or additional commentary.\r\n" +
                    "Return only the name.";

                npc.Name = NameGenerator?.Invoke(namePrompt) ?? (Faction + " Crew Member");
                npc.EditorID = "npc_" + (npc.Name.ToString().ToLower()).Replace(" ", "");

                Random wrand = RandomProvider.Random;
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
                if (i == crewcount-1 && BookGenerator != null)
                {
                    //We do this last as we know all the crew now. Also only once as they are a bit samey.
                    Console.WriteLine("Generating Crew Log file...");
                    string BookPrompt =
                        "Write a personal diary entry for " + npc.Name + ", a " + Gender +
                        " crew member serving aboard the starship " + ShipName + ".\r\n" +
                        "Use a first-person voice that reflects their personality, emotional state, and day-to-day reality onboard.\r\n" +
                        "One crew member, named " + Betrayernpc.Name + ", has betrayed the crew.\r\n" +
                        "Return only the diary entry.";

                    string BookContents = BookGenerator(BookPrompt);
                    BookNoun bountybook = new BookNoun(0x000905, npc.Name.ToString() + " Log", Guid.NewGuid().ToString().Substring(0, 8), BookContents);

                    npc.Items.Add(new ContainerEntry() { Item = new ContainerItem() { Item = targetMod.Books[bountybook.instance.FormKey].ToLink(), Count = 1 } });
                }

                targetMod.Npcs.Add(npc);
                //Add it to the list
                list.Add(npc);
                frmlst.Items.Add(npc);
            }

            targetMod.FormLists.Add(frmlst);
            return targetMod.FormLists[frmlst.FormKey].ToLink<IStarfieldMajorRecordGetter>();
        }
    }
}
