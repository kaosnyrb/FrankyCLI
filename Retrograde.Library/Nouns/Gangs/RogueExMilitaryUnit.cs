using Retrograde.AI;
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

namespace Retrograde.Nouns.Gangs
{
    // A rogue ex-military unit is a breakaway formation of trained soldiers
    // that now operates outside official command structure, often as fugitives
    // or self-directed combatants. Functionally, this is a "gang" of hostile NPCs.
    // This will create a formlist of the NPCs that can be used by scripts to spawn them.
    public class RogueExMilitaryUnit : IGang
    {
        public StarfieldMod myMod;
        public string interal_unitName;
        public Mutagen.Bethesda.Starfield.FormList gangList;


        public RogueExMilitaryUnit()
        {
            myMod = RetrogradeContext.Current.TargetMod;
            interal_unitName = GetUnitName();

            gangList = GenerateGang();
        }

        public string gangName { get => interal_unitName; set => interal_unitName = value; }
        Mutagen.Bethesda.Starfield.FormList IGang.gangList { get => gangList; set => gangList = value; }

        public static string GetUnitName()
        {
            Random random = RandomProvider.Random;

            // Prefixes evoke formal units that have gone off-book.
            List<string> unitPrefixes = new List<string>
            {
                "Disavowed", "Lost", "Breakaway", "Mutineer", "Remnant",
                "Ghost", "Exiled", "Fugitive", "Redacted", "Bloodied",
                "Silent", "Wayward", "Rogue", "Fractured", "Renegade",
                "Blacklisted", "Dishonored", "Forsaken", "Outcast", "Sundered",
                "Iron", "Grim", "Feral", "Deadline", "Obsidian",
            };

            // Suffixes stay strongly military / unit-coded.
            List<string> unitSuffixes = new List<string>
            {
                "Company", "Battalion", "Regiment", "Platoon", "Taskforce",
                "Squadron", "Strike Group", "Fireteam", "Cohort", "Division",
                "Lancers", "Guard", "Detail", "Detachment", "Unit",
                "Vanguard", "Phalanx", "Commandos", "Legion", "Shock Troop"
            };

            return unitPrefixes[random.Next(unitPrefixes.Count)] + " " + unitSuffixes[random.Next(unitSuffixes.Count)];
        }

        public Mutagen.Bethesda.Starfield.FormList GenerateGang()
        {
            Random random = RandomProvider.Random;

            var list = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>();

            // Generate a new NPC group

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

                string namePrompt =
                    "Generate a unique full name (first and last) for a " + Gender +
                    " former military operative now serving in the rogue ex-military unit " + interal_unitName + ".\r\n" +
                    "The name should feel grounded and believable for a trained soldier or officer—credible within a realistic sci-fi or near-future military setting.\r\n" +
                    "Avoid flashy criminal nicknames or stylised monikers.\r\n" +
                    "Do NOT reuse or repeat any names generated earlier in this session.\r\n" +
                    "Do NOT include titles, ranks, callsigns, or extra commentary.\r\n" +
                    "Return only the name.";

                npc.Name = AITools.RunPrompt(namePrompt);

                npc.EditorID = "npc_" + (npc.Name.ToString().ToLower()).Replace(" ", "");
                Random wrand = RandomProvider.Random;
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

                // Logfile for Unit Member
                if (i == gangmembers - 1 && AITools.AIMODE)
                {
                    Console.WriteLine("Generating Rogue Ex-Military Unit Log file...");
                    string BookPrompt =
                        "Write a personal diary entry for " + npc.Name + ", a " + Gender +
                        " member of the rogue ex-military unit " + interal_unitName + ".\r\n" +
                        "Use a first-person voice that reflects their personality, combat history, and conflicted feelings about abandoning official command.\r\n" +
                        "Return only the diary entry.";

                    string BookContents = AITools.RunPrompt(BookPrompt);
                    BookNoun bountybook = new BookNoun(0x000905, gangName + " Log", Guid.NewGuid().ToString().Substring(0, 8), BookContents);
                    npc.Items.Add(new ContainerEntry() { Item = new ContainerItem() { Item = myMod.Books[bountybook.instance.FormKey].ToLink(), Count = 1 } });
                }

                myMod.Npcs.Add(npc);
                // Add it to the list
                list.Add(npc);
            }

            // Save the list
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
