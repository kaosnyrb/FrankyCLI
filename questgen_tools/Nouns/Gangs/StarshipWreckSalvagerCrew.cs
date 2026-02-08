using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Nouns;
using FrankyCLI.questgen_tools.Utils;
using Retrograde.Utils;
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
        // A starship wreck salvager crew is a band of spacers who specialize in
        // boarding, claiming, and stripping derelict ships and orbital graveyards.
        // Functionally, this is a "gang" of hostile or neutral NPCs.
        // This will create a formlist of the NPCs that can be used by scripts to spawn them.
        public class StarshipWreckSalvagerCrew : IGang
        {
            public StarfieldMod myMod;
            public string interal_crewName;
            public Mutagen.Bethesda.Starfield.FormList gangList;

            public StarshipWreckSalvagerCrew()
            {
                myMod = gen_quest_main.myMod;
                interal_crewName = GetCrewName();

                gangList = GenerateGang();
            }

            public string gangName { get => interal_crewName; set => interal_crewName = value; }
            Mutagen.Bethesda.Starfield.FormList IGang.gangList { get => gangList; set => gangList = value; }

            public static string GetCrewName()
            {
                Random random = RandomUtils.random;

                // Prefixes evoke salvage outfits and graveyard legends.
                List<string> crewPrefixes = new List<string>
            {
                "Graveyard", "Drift", "Wreckwright", "Scrapline", "Hullborn",
                "Dead Orbit", "Gravestone", "Derelict", "Breakwater", "Black Drift",
                "Nullspace", "Cinderfield", "Ironwake", "Debrisfield", "Junkstar",
                "Red Beacon", "Salvage Crown", "Grim Dock", "Lost Signal", "Vacuum Tide"
            };

                // Suffixes keep it clearly salvage-crew coded.
                List<string> crewSuffixes = new List<string>
            {
                "Salvagers", "Outfit", "Crew", "Recovery Team", "Cutters",
                "Claim Jumpers", "Scavengers", "Boarding Team", "Pullers", "Breakers",
                "Ops", "Syndicate", "Collective", "Union", "Cartel"
            };

                return crewPrefixes[random.Next(crewPrefixes.Count)] + " " + crewSuffixes[random.Next(crewSuffixes.Count)];
            }

            public Mutagen.Bethesda.Starfield.FormList GenerateGang()
            {
                Random random = RandomUtils.random;

                var list = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>();

                // Generate a new NPC group

                var outfit = NPCToolsEx.GetRandomOutfit(true);

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
                        " member of the starship wreck salvage crew known as " + interal_crewName + ".\r\n" +
                        "The name should feel grounded and believable for a spacer who makes a living boarding derelicts, cutting hulls, and working the orbital graveyards.\r\n" +
                        "Avoid flashy criminal nicknames or stylised monikers.\r\n" +
                        "Do NOT reuse or repeat any names generated earlier in this session.\r\n" +
                        "Do NOT include titles, callsigns, or extra commentary.\r\n" +
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
                    npc.HeadParts.Add(NPCToolsEx.GetHaircut(isfemale));
                    npc.Items = new ExtendedList<ContainerEntry>
                {
                    new ContainerEntry() { Item = new ContainerItem() { Item = NPCToolsEx.GetRandomGear(), Count = 1 } }
                };

                    // Logfile for Crew Member
                    if (i == gangmembers - 1)
                    {
                        // We do this last as we know all the crew now. Also only once as they are a bit samey.
                        Console.WriteLine("Generating Salvager Crew Log file...");
                        string BookPrompt =
                            "Write a personal diary entry for " + npc.Name + ", a " + Gender +
                            " member of the starship wreck salvage crew known as " + interal_crewName + ".\r\n" +
                            "Use a first-person voice that reflects their personality, their relationship with danger, and the day-to-day grind of boarding and stripping derelict ships.\r\n" +
                            "Let the tone be shaped by the crew's culture—arguments over salvage rights, unspoken rules about what gets reported, and the quiet fear of what can go wrong in cold vacuum.\r\n" +
                            "Naturally reference previously generated crew members as partners on boarding runs, rival cutters, trusted riggers, or liabilities the writer worries about.\r\n" +
                            "Subtly weave in any relevant rumours, ghost-ship stories, missing crews, or suspiciously valuable finds implied by the LoreContext, but do NOT quote the LoreContext directly or mention it by name.\r\n" +
                            "These elements should appear as half-remembered stories, hangar gossip, uneasy memories from past salvages, or things the character tells themselves not to think about during an EVA.\r\n" +
                            "Make the entry immersive, introspective, and grounded—suitable as lore flavor for a salvage-focused quest.\r\n" +
                            "Avoid exposition or explaining how salvage law works; write as someone who already lives that life.\r\n" +
                            "Do not break the fourth wall or reference prompts or readers.\r\n" +
                            "Return only the diary entry.";

                        BookPrompt = PromptFlavourTools.AddFlavourToGangBook(BookPrompt);

                        string BookContents = AITools.RunPrompt(BookPrompt);
                        BookNoun bountybook = new BookNoun(0x000905, gangName + " " + RandomUtils.GetLogSynonym(), Guid.NewGuid().ToString().Substring(0, 8), BookContents);
                        npc.Items.Add(new ContainerEntry() { Item = new ContainerItem() { Item = gen_quest_main.myMod.Books[bountybook.instance.FormKey].ToLink(), Count = 1 } });

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