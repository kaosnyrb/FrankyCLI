using FrankyCLI.questgen_tools;
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
    // A gang is a collection of nameless goons that are used in missions.
    // Spacers are an example of an vanilla gang.
    // This will create a formlist of NPCs with generic names that can be spawned in the missions.

    public  class OutlawGang
    {
        public StarfieldMod myMod;
        public string gangName;

        public OutlawGang(StarfieldMod myModparam, string gangNameparam)
        {
            myMod = myModparam;
            gangName = gangNameparam;
        }

        public static string GetGangTheme()
        {
            Random random = new Random();

            List<string> ganglist = new List<string>()
            {
                "Urban cyberpunk street gang",
                "Rogue space pirate faction",
                "Paramilitary mercenary company",
                "Bio-augmented criminal syndicate",
                "Black-market tech smugglers",
                "Nomadic wasteland raider clan",
                "Elite assassin brotherhood",
                "Underground robotics cult",
                "Rebel freedom-fighter cell",
                "Corporate espionage division",
                "Post-apocalyptic scavenger tribe",
                "Void-dwelling marauder fleet",
                "Mutant undercity crime family",
                "High-society criminal cartel",
                "Fanatical doomsday cult",
                "AI-controlled pirate collective",
                "Stealth-oriented infiltration unit",
                "Chemical-enhanced gladiator gang",
                "Ancient relic-hunters guild",
                "Quantum anomaly worshippers",
                "Smuggler-trader nomad caravans",
                "Deep-space salvage pirates",
                "Telepathic crime circle",
                "Ex-military deserter faction",
                "Genetically-engineered outcast tribe"
            };

            return ganglist[random.Next(ganglist.Count)];
        }

        public Mutagen.Bethesda.Starfield.FormList GenerateGang()
        {
            Random random = new Random();

            var list = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>();
            
            //Generate a new NPC

            var outfit = NPCTools.GetRandomOutfit(true);
            for (int i = 0; i < 3; i++)
            {
                bool isfemale = false;
                if (random.Next(100) > 50)
                {
                    isfemale = true;
                }

                var NPC = myMod.Npcs[new FormKey(myMod.ModKey, NPCTools.GetTemplateNPC(isfemale))].DeepCopy();
                Npc npc = NPCTools.CloneNPC(myMod, NPC);
                npc.Name = gangName;
                npc.EditorID = "npc_" + (gangName.ToLower()).Replace(" ", "");

                Random wrand = new Random();
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
