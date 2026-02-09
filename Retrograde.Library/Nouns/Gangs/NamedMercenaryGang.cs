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
    // A gang is a collection of npcs that fight the player.
    // This will create a formlist of the NPCs that can be used by scripts to spawn them.
    public class NamedMercenaryGang : IGang
    {
        public StarfieldMod myMod;
        public string interal_gangName;
        public Mutagen.Bethesda.Starfield.FormList gangList;


        public NamedMercenaryGang()
        {
            myMod = RetrogradeContext.Current.TargetMod;
            interal_gangName = GetGangName();

            gangList = GenerateGang();
        }

        public string gangName { get => interal_gangName; set => interal_gangName = value; }
        Mutagen.Bethesda.Starfield.FormList IGang.gangList { get => gangList; set => gangList = value; }

        public static string GetGangName()
        {
            Random random = RandomProvider.Random;
            List<string> gangPrefixes = new List<string>
            {
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
                "Vanguard", "Dragoon", "Arclight", "Sentinel", "Bulwark", "Phalanx", "Warden", "Interceptor", "Spearhead", "Ironclad",
                "Overwatch", "Blackguard", "Wardog", "Direwolf", "Stormborn", "Hellfire", "Shockwave", "Nightfall", "Ridgeback", "Longshot",
                "Garrison", "Ironhand", "Fireteam", "Requiem", "Spectre", "Shadowline", "Cutlass", "Warpath", "Sentience", "Pinnacle",
                "Onslaught", "Warbrand", "Stonewall", "Lockstep", "Coldfront", "Hammerfall", "Shatterpoint", "Overlord", "Breach", "Nullpoint",
                "Redline", "Prime", "Outrider", "Hardpoint", "Gunmetal", "Stormforge", "Ironreach", "Deadlock", "Blacksteel", "Hellstrike",
                "Thunderhead", "Ravenscar", "Wolfguard", "Shocklance", "Apex", "Backline", "Crosswind", "Razorpoint", "Steelborne", "Overcast",
                "Highmark", "Stormmark", "Shadowmark", "Ashfall", "Frostline", "Lockdown", "Breakpoint", "Hardline", "Downrange", "Ghostline",
                "Ironpoint", "Vortex", "Dragonstrike", "Sunder", "Nightwatch", "Stoneguard", "Blackfire", "Warforge", "Redshift", "Arbiters",
                "Nullshift", "Crossfire", "Kingslayer", "Deadzone", "Skyfall", "Ridgefire", "Gunshot", "Thunderstrike", "Backdraft", "Hellbound",
                "Stormcaller", "Endline", "Hardstrike", "Warfrost", "Shadowcast", "Forgepoint", "Warborn", "Stormblade"
            };


            return gangPrefixes[random.Next(gangPrefixes.Count)] + " " + gangSuffixes[random.Next(gangSuffixes.Count)];
        }

        public Mutagen.Bethesda.Starfield.FormList GenerateGang()
        {
            Random random = RandomProvider.Random;

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

                string namePrompt =
                    "Generate a unique full name (first and last) for a " + Gender +
                    " operative of the " + interal_gangName + " mercenary unit.\r\n" +
                    "The name should fit a professional, disciplined, combat-trained contractor—credible within a private military or black-ops environment.\r\n" +
                    "Avoid flashy criminal nicknames or stylized monikers.\r\n" +
                    "Do NOT reuse or repeat any names generated earlier in this session.\r\n" +
                    "Do NOT include titles, ranks, codenames, or extra commentary.\r\n" +
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

                //Logfile for Crew Member
                if (i == gangmembers - 1 && AITools.AIMODE)
                {
                    Console.WriteLine("Generating Crew Log file...");
                    string BookPrompt =
                        "Write a personal diary entry for " + npc.Name + ", a " + Gender +
                        " operative of the " + interal_gangName + " mercenary unit.\r\n" +
                        "Use a first-person voice that reflects their personality, mindset, and the daily realities of working within a private military outfit.\r\n" +
                        "Return only the diary entry.";

                    string BookContents = AITools.RunPrompt(BookPrompt);
                    BookNoun bountybook = new BookNoun(0x000905, npc.Name.ToString() + " Log", Guid.NewGuid().ToString().Substring(0, 8), BookContents);
                    npc.Items.Add(new ContainerEntry() { Item = new ContainerItem() { Item = myMod.Books[bountybook.instance.FormKey].ToLink(), Count = 1 } });
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
