using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Nouns;
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
    public  class NamedMercenaryGang : IGang
    {
        public StarfieldMod myMod;
        public string interal_gangName;
        public Mutagen.Bethesda.Starfield.FormList gangList;

        public NamedMercenaryGang()
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

                npc.Name = AITools.RunPrompt(
                    "Generate a unique full name (first and last) for a " + Gender +
                    " operative of the " + interal_gangName + " mercenary unit.\r\n" +
                    "The name should fit a professional, disciplined, combat-trained contractor—credible within a private military or black-ops environment.\r\n" +
                    "Avoid flashy criminal nicknames or stylized monikers.\r\n" +
                    "Do NOT reuse or repeat any names generated earlier in this session.\r\n" +
                    "Do NOT include titles, ranks, codenames, or extra commentary.\r\n" +
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
                if (i == gangmembers - 1)
                {
                    //We do this last as we know all the crew now. Also only once as they are a bit samey.
                    Console.WriteLine("Generating Crew Log file...");
                    string BookPrompt =
                        "Write a personal diary entry for " + npc.Name + ", a " + Gender +
                        " operative of the " + interal_gangName + " mercenary unit.\r\n" +
                        "Use a first-person voice that reflects their personality, mindset, and the daily realities of working within a private military outfit.\r\n" +
                        "Let the tone be shaped by the unit's culture—its discipline, unspoken rules, operational stresses, shifting loyalties, and the quiet politics that develop among contractors.\r\n" +
                        "Naturally reference previously generated unit members as comrades, rivals, mentors, or liabilities—people the writer depends on, distrusts, or measures themselves against.\r\n" +
                        "Subtly weave in any relevant rumours, classified whispers, operational uncertainties, or unresolved tensions implied by the LoreContext, but do NOT quote the LoreContext directly or mention it by name.\r\n" +
                        "These elements should appear as personal doubts, overheard fragments, mission scuttlebutt, or suspicions the character hopes aren't true.\r\n" +
                        "Make the entry immersive, introspective, and grounded—suitable as lore flavor for a mercenary-focused quest.\r\n" +
                        "Avoid exposition or explaining the unit; write as someone who already lives inside that world.\r\n" +
                        "Do not break the fourth wall or reference prompts or readers.\r\n" +
                        "Return only the diary entry.";


                    BookPrompt = PromptFlavourTools.AddFlavourToGangBook(BookPrompt);
                    string BookContents = AITools.RunPrompt(BookPrompt);
                    BookNoun bountybook = new BookNoun(0x000905, npc.Name.ToString() + " " + RandomUtils.GetLogSynonym(), Guid.NewGuid().ToString().Substring(0, 8), BookContents);
                    npc.Items.Add(new ContainerEntry() { Item = new ContainerItem() { Item = gen_quest_main.myMod.Books[bountybook.instance.FormKey].ToLink(), Count = 1 } });
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
