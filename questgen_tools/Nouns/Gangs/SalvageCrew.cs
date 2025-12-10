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
    // A salvage crew is a collection of NPCs who board and strip derelict ships.
    // This will create a formlist of the NPCs that can be used by scripts to spawn them.
    public class SalvageCrew : IGang
    {
        public StarfieldMod myMod;
        public string interal_gangName;
        public Mutagen.Bethesda.Starfield.FormList gangList;

        public SalvageCrew()
        {
            myMod = gen_quest_main.myMod;
            interal_gangName = GetCrewName();

            // Optional flavor prompt hook:
            // AITools.RunPrompt("<Lore> There is a salvage crew called " + interal_gangName + " working the wreck fields around the target.");

            gangList = GenerateGang();
        }

        public string gangName { get => interal_gangName; set => interal_gangName = value; }
        Mutagen.Bethesda.Starfield.FormList IGang.gangList { get => gangList; set => gangList = value; }

        // ---------------------------------
        // Name generator (two-word salvage crew names)
        // ---------------------------------
        public static string GetCrewName()
        {
            Random random = RandomUtils.random;

            var prefixes = new List<string>
            {
                "Graveyard", "Drift", "Wreckfield", "Gravwell", "Blacksite", "Deadspace",
                "Rustbelt", "Scrapline", "Hullgrave", "Derelict", "Redshift", "Deepvoid",
                "Breakwater", "Silent", "Shattered", "Lostsignal", "Junkyard", "Orbitfall",
                "Scrapstar", "Grim", "Coldvoid", "Gravlock", "Starbreak", "Vacuum"
            };

            var suffixes = new List<string>
            {
                "Salvors", "Cutters", "Scavs", "Crew", "Divers", "Riggers", "Breakers",
                "Scrappers", "Boarders", "Haulers", "Strippers", "Pryers", "Harvesters",
                "Skiffs", "Torchers", "Pickers", "Jackals", "Rats", "Outfit", "Operators"
            };

            return prefixes[random.Next(prefixes.Count)] + " " +
                   suffixes[random.Next(suffixes.Count)];
        }

        // ---------------------------------
        // One-word salvage job roles
        // ---------------------------------
        public static string GetCrewJobRole()
        {
            Random r = RandomUtils.random;

            var roles = new List<string>
            {
                "Cutter", "Rigger", "Diver", "Torch", "Prybar", "Scout",
                "Pilot", "Spotter", "Hauler", "Breaker", "Gunner", "Tech",
                "Slicer", "Skimmer", "Mapper", "Ropper", "Loader", "Foreman",
                "Handler", "Stripper", "Jumper", "Patch", "Lookout", "Scanner"
            };

            return roles[r.Next(roles.Count)];
        }

        // ---------------------------------
        // NPC generation
        // ---------------------------------
        public Mutagen.Bethesda.Starfield.FormList GenerateGang()
        {
            Random random = RandomUtils.random;

            var list = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>();

            // Salvagers are almost always space-suited
            var outfit = NPCTools.GetRandomOutfit(true);

            int crewSize = 3 + random.Next(5); // 3–7 salvagers

            for (int i = 0; i < crewSize; i++)
            {
                bool isFemale = random.Next(100) > 50;

                var templateKey = new FormKey(myMod.ModKey, NPCTools.GetTemplateNPC(isFemale));
                var templateNpc = myMod.Npcs[templateKey].DeepCopy();
                Npc npc = NPCTools.CloneNPC(myMod, templateNpc);

                npc.Name = gangName + " " + GetCrewJobRole();

                npc.EditorID = "npc_" + npc.Name.ToString().ToLower().Replace(" ", "");

                Random wrand = RandomUtils.random;
                npc.Weight = new NpcWeight()
                {
                    Fat = (float)wrand.NextDouble(),
                    Muscular = (float)wrand.NextDouble(),
                    Thin = (float)wrand.NextDouble()
                };

                var lev = new PcLevelMult
                {
                    LevelMult = 0.25f + (float)random.NextDouble()
                };
                npc.Level = lev;

                npc.SpaceOutfit = outfit;
                npc.EyeColor = NPCTools.GetEyeColour();
                npc.HairColor = NPCTools.GetHairColour();
                npc.SkinToneIndex = (byte)wrand.Next(8);
                npc.HeadParts.Add(NPCTools.GetHaircut(isFemale));

                npc.Items = new ExtendedList<ContainerEntry>
                {
                    new ContainerEntry()
                    {
                        Item = new ContainerItem()
                        {
                            Item = NPCTools.GetRandomGear(),
                            Count = 1
                        }
                    }
                };

                myMod.Npcs.Add(npc);
                list.Add(npc);
            }

            // Clone a base formlist and create a new one
            var baseFormList = myMod.FormLists[new FormKey(myMod.ModKey, 0x000805)].DeepCopy();

            var formList = new Mutagen.Bethesda.Starfield.FormList(myMod)
            {
                EditorID = "frmlist_" + Guid.NewGuid().ToString().Substring(0, 8),
                Items = list,
            };

            myMod.FormLists.Add(formList);

            return formList;
        }
    }
}
