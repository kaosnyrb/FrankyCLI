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
    // A gang is a collection of npcs that fight the player.
    // This will create a formlist of the NPCs that can be used by scripts to spawn them.
    public  class StreetGang : IGang
    {
        public StarfieldMod myMod;
        public string interal_gangName;
        public Mutagen.Bethesda.Starfield.FormList gangList;

        public StreetGang()
        {
            myMod = gen_quest_main.myMod;
            interal_gangName = GetGangName();

            //AITools.RunPrompt("<Lore> There is a gang of people called " + interal_gangName + " who are assiting the target");

            gangList = GenerateGang();
        }

        public string gangName { get => interal_gangName; set => interal_gangName = value; }
        Mutagen.Bethesda.Starfield.FormList IGang.gangList { get => gangList; set => gangList = value; }

        public static string GetGangName()
        {
            Random random = RandomUtils.random;

            List<string> gangPrefixes = new List<string>
            {
                // Neon / Ebbside cyber-noir prefixes
                "Neon", "Chrome", "Glow", "Pulse", "Glass", "Flux", "Neonwave", "Slip",
                "Shimmer", "Ghost", "Wire", "Ion", "Synth", "Drift", "Blueglass", "Ebb",
                "Shard", "Spire", "Circuit", "Pulse", "Silent", "Neonblack", "Heat",
                "Voltage", "Ether", "Silk", "Static", "Phase", "Neonline", "Deep",
                "Slick", "Glowdust", "Coldlight", "Redline", "Spark", "Vapor",
                "Grime", "Backline", "Razor", "Grid", "Shadow", "Chromatic",
            };

            List<string> gangSuffixes = new List<string>
            {
                // Two-word crew names, Neon street style
                "Runners", "Crew", "Fangs", "Slicks", "Sisters", "Boys", "Girls", "Collective",
                "Knives", "Serpents", "Cutters", "Rats", "Jackals", "Drifters", "Breakers",
                "Skulls", "Specters", "Wolves", "Pack", "Slicers", "Phantoms", "Synths",
                "Gunners", "Dealers", "Hackers", "Signals", "Ghosts", "Wreckers", "Lot",
                "Sparks", "Rogues", "Runners", "Crew", "Kings", "Line", "Circuit",
                "Vipers", "Strays", "Lowborn", "Ridge", "Slickline", "Loopers",
            };

            return gangPrefixes[random.Next(gangPrefixes.Count)] + " " +
                   gangSuffixes[random.Next(gangSuffixes.Count)];
        }

        public static string GetGangJobRole()
        {
            Random r = RandomUtils.random;

            var roles = new List<string>
            {
                "Lookout", "Runner", "Enforcer", "Breaker", "Trigger", "Scout",
                "Slicer", "Hacker", "Skimmer", "Ghost", "Fixer", "Broker",
                "Cook", "Mule", "Mixer", "Keeper", "Cracker", "Shiv",
                "Handler", "Watcher", "Ripper", "Gunner", "Pusher",
                "Smuggler", "Reaper", "Dealer", "Sentry", "Scout",
                "Breaker", "Patcher", "Drifter", "Sniper"
            };

            return roles[r.Next(roles.Count)];
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

                npc.Name = gangName + " " + GetGangJobRole();
 
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
