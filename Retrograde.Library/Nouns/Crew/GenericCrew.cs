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

namespace Retrograde.Nouns.Crew
{
    public class GenericCrew : ICrew
    {

        public static string GetCrewName()
        {
            Random random = RandomProvider.Random;

            var prefixes = new List<string>
            {
                "Central", "Unity", "Prime", "Silver", "Blue", "North",
                "South", "East", "West", "Standard", "Common",
                "First", "Second", "Pioneer", "Open", "Clear",
                "Bright", "True", "Core", "Union", "Alliance",
                "Summit", "Horizon", "Forward", "Primeway"
            };

            var suffixes = new List<string>
            {
                "Crew", "Team", "Unit", "Flight", "Wing", "Division",
                "Operators", "Rangers", "Hands", "Corps", "Watch",
                "Group", "Collective", "Deckhands", "Navigators",
                "Specialists", "Cadre", "Staff", "Section", "Outfit"
            };

            return prefixes[random.Next(prefixes.Count)] + " " +
                   suffixes[random.Next(suffixes.Count)];
        }

        public static string GetCrewJobRole()
        {
            Random r = RandomProvider.Random;

            var roles = new List<string>
            {
                "Pilot", "Navigator", "Engineer", "Technician", "Operator",
                "Specialist", "Medic", "Scout", "Analyst", "Researcher",
                "Observer", "Technician", "Steward", "Quartermaster",
                "Communications", "Sensor", "Handler", "Assistant",
                "Coordinator", "Deckhand"
            };

            return roles[r.Next(roles.Count)];
        }



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

            string Crewname = GetCrewName();

            bool generatebook = false;
            int crewcount = 5;

            for (int i = 0; i < crewcount; i++)
            {
                bool isfemale = false;
                if (random.Next(100) > 50)
                {
                    isfemale = true;
                }

                var NPC = NPCTools.FindTemplateDeadNpc(isfemale);
                Npc npc = NPCTools.CloneNPC(targetMod, NPC);

                //Name
                Console.WriteLine("Generating Crew Name...");
                string Gender = "Male";
                if (isfemale) Gender = "Female";
                npc.Name = Crewname + " " + GetCrewJobRole();
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
                if (i == crewcount-1 && AITools.AIMODE)
                {
                    //We do this last as we know all the crew now. Also only once as they are a bit samey.
                    Console.WriteLine("Generating Crew Log file...");
                    string BookPrompt =
                        "Write a short in-world lore excerpt about the starship crew known as " + Crewname + " and their role in the region surrounding " + ShipName + ".\r\n" +
                        "This should not be written by a crew member or from any character's first-person perspective.\r\n" +
                        "Instead, write it as an objective lore tidbit—something that might appear in a datapad, archived report, trader rumor collection, or a minor encyclopedic entry.\r\n" +
                        "Return only the lore excerpt.";

                    string BookContents = AITools.RunPrompt(BookPrompt);
                    BookNoun bountybook = new BookNoun(0x000905, Crewname.ToString() + " Log", Guid.NewGuid().ToString().Substring(0, 8), BookContents);

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
