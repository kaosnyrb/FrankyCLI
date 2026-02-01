using DynamicData;
using FrankyCLI.questgen_tools.Interfaces;
using FrankyCLI.Retrograde.FactionMembers;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Data;
using static Mutagen.Bethesda.FormKeys.Starfield.Starfield;

namespace FrankyCLI.questgen_tools
{
    public class CrimsonFleetFactionCrew : IFactionMembers
    {
        List<Npc> LowRank { get; set; }
        List<Npc> HighRank { get; set; }
        List<Npc> Bosses { get; set; }
        public CrimsonFleetFactionCrew()
        {
            string Faction = "Crimsonfleet";
            Random random = RandomUtils.random;
            string Crewname = Faction;

            int lowrank_crewcount = 10;
            int highrank_crewcount = 5;
            int boss_crewcount = 1;

            LowRank = new List<Npc>();
            HighRank = new List<Npc>(); 
            Bosses = new List<Npc>();

            //Low Rank
            for (int i = 0; i < lowrank_crewcount; i++)
            {
                bool isfemale = false;
                if (random.Next(100) > 75)
                {
                    isfemale = true;
                }
                var NPC = gen_quest_main.myMod.Npcs[new FormKey(gen_quest_main.myMod.ModKey, NPCTools.GetTemplateNPC(isfemale))].DeepCopy();
                Npc npc = NPCTools.CloneNPC(gen_quest_main.myMod, NPC);

                //Ranked Info
                var outfit = GetLowRank_Outfit();
                npc.Name = "Crimson" + " " + GetLowRank_Name();
                var gear = GetLowRank_Gear();
                var lev = new PcLevelMult();
                lev.LevelMult = 0.5f + ((float)random.NextDouble()/2);
                npc.Level = lev;

                CreateNPC(Faction, random, isfemale, npc, outfit, gear);

                LowRank.Add(npc);
            }

            //High Rank
            for (int i = 0; i < highrank_crewcount; i++)
            {
                bool isfemale = false;
                if (random.Next(100) > 75)
                {
                    isfemale = true;
                }
                var NPC = gen_quest_main.myMod.Npcs[new FormKey(gen_quest_main.myMod.ModKey, NPCTools.GetTemplateNPC(isfemale))].DeepCopy();
                Npc npc = NPCTools.CloneNPC(gen_quest_main.myMod, NPC);

                //Ranked Info
                var outfit = GetHighRank_Outfit();
                npc.Name = "Crimson" + " " + GetHighRank_Name();
                var gear = GetHighRank_Gear();
                var lev = new PcLevelMult();
                lev.LevelMult = 0.75f + (float)random.NextDouble();
                npc.Level = lev;

                CreateNPC(Faction, random, isfemale, npc, outfit, gear);
                HighRank.Add(npc);

            }

            for (int i = 0; i < boss_crewcount; i++)
            {
                bool isfemale = false;
                if (random.Next(100) > 50)
                {
                    isfemale = true;
                }
                var NPC = gen_quest_main.myMod.Npcs[new FormKey(gen_quest_main.myMod.ModKey, NPCTools.GetTemplateNPC(isfemale))].DeepCopy();
                Npc npc = NPCTools.CloneNPC(gen_quest_main.myMod, NPC);

                //Ranked Info
                var outfit = GetBoss_Outfit();
                npc.Name = "Crimson" + " " + GetBoss_Name();
                var gear = GetBoss_Gear();
                var lev = new PcLevelMult();
                lev.LevelMult = 1 + (float)random.NextDouble();
                npc.Level = lev;

                CreateNPC(Faction, random, isfemale, npc, outfit, gear);
                Bosses.Add(npc);
            }
        }

        private void CreateNPC(string Faction, Random random, bool isfemale, Npc npc, IFormLinkNullable<IOutfitGetter> outfit, IFormLinkNullable<ILeveledItemGetter> gear)
        {
            //Generate Member
            string Gender = "Male";
            if (isfemale) Gender = "Female";
            npc.EditorID = "npc_" + (npc.Name.ToString().ToLower()).Replace(" ", "");
            npc.Voice = NPCTools.GetVoice(Faction, isfemale);
            npc.Factions.Clear();
            npc.Factions.Add(NPCTools.GetFaction(Faction));
            npc.Weight = new NpcWeight()
            {
                Fat = (float)random.NextDouble() /2,
                Muscular = (float)random.NextDouble(),
                Thin = (float)random.NextDouble()/2
            };
            npc.SpaceOutfit = outfit;
            npc.EyeColor = NPCTools.GetEyeColour();
            npc.HairColor = NPCTools.GetHairColour();
            npc.SkinToneIndex = (byte)random.Next(8);
            npc.HeadParts.Add(NPCTools.GetHaircut(isfemale));
            npc.Items = new ExtendedList<ContainerEntry>
                {
                    new ContainerEntry() { Item = new ContainerItem() { Item = gear, Count = 1 } },
                };
            gen_quest_main.myMod.Npcs.Add(npc);
        }

        public IFormLinkNullable<IOutfitGetter> GetLowRank_Outfit()
        {
            Random random = RandomUtils.random;
            List<uint> Outfits = new List<uint>
            {
                0x002EB236,// Outfit_Clothes_CrimsonFleet_Any [OTFT:002EB236]
                0x0029B9D4,// LL_Clothes_Settler_Any [LVLI:0029B9D4]
                0x0003A0CF,// LL_Clothes_Worker_Any [LVLI:0003A0CF]

            };
            IFormLinkNullable<IOutfitGetter> outfit = new FormKey(gen_quest_main.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
            return outfit;
        }

        public IFormLinkNullable<IOutfitGetter> GetHighRank_Outfit()
        {
            Random random = RandomUtils.random;
            List<uint> Outfits = new List<uint>
            {
                0x00018DCF,//Outfit_Spacesuit_CrimsonFleet [OTFT:00018DCF]
                0x00279225//Outfit_Spacesuit_CrimsonFleet_NoHelmet [OTFT:00279225]                
            };
            IFormLinkNullable<IOutfitGetter> outfit = new FormKey(gen_quest_main.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
            return outfit;
        }

        public IFormLinkNullable<IOutfitGetter> GetBoss_Outfit()
        {
            Random random = RandomUtils.random;
            List<uint> Outfits = new List<uint>
            {
                0x00018DCF,//Outfit_Spacesuit_CrimsonFleet [OTFT:00018DCF]
                0x00279225,//Outfit_Spacesuit_CrimsonFleet_NoHelmet [OTFT:00279225]
                0x00066ACE//Outfit_Spacesuit_CrimsonFleet_Officer [OTFT:00066ACE]
            };
            IFormLinkNullable<IOutfitGetter> outfit = new FormKey(gen_quest_main.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
            return outfit;
        }

        public string GetLowRank_Name()
        {
            Random r = RandomUtils.random;
            var roles = new List<string>
            {
                "Deckhand","Dockhand","Gun Hand","Blade Hand",
                "Runner","Loot Runner","Cargo Runner","Fuel Runner",
                "Lookout","Gate Lookout","Dock Lookout","Gun Lookout",
                "Thug","Dock Thug","Cargo Thug","Station Thug",
                "Scrapper","Hull Scrapper","Wire Scrapper","Chop Scrapper",
                "Rigger","Cable Rigger","Hull Rigger","Patch Rigger",
                "Hauler","Crate Hauler","Scrap Hauler","Body Hauler",
                "Guard","Gate Guard","Lockup Guard","Cargo Guard"
            };
            return roles[r.Next(roles.Count)];
        }

        public string GetHighRank_Name()
        {
            Random r = RandomUtils.random;
            var roles = new List<string>
            {
                "Crew Lead","Shift Lead","Deck Lead","Dock Lead",
                "Senior Hand","Gun Hand Lead","Boarding Lead","Salvage Lead",
                "Section Lead","Gun Section Lead","Dock Section Lead",
                "Sergeant","Crew Sergeant","Gun Sergeant","Dock Sergeant",
                "Enforcer","Senior Enforcer","Gun Enforcer","Dock Enforcer",
                "Lieutenant","Junior Lieutenant","Acting Lieutenant"
            };
            return roles[r.Next(roles.Count)];
        }

        public string GetBoss_Name()
        {
            Random r = RandomUtils.random;
            var roles = new List<string>
            {
                "Crew Boss","Gun Boss","Dock Boss","Raid Boss",
                "Enforcer Captain","Security Captain","Gun Deck Captain","Boarding Captain",
                "First Mate","Chief Mate","Senior Mate",
                "Quartermaster","Black Quartermaster","Lootmaster",
                "Operations Chief","Dock Chief","Salvage Chief","Security Chief",
                "Lieutenant","Raid Lieutenant","Station Lieutenant",
                "Commander","Raid Commander","Station Commander",
                "Captain","Void Captain","Fleet Captain",
                "Warlord","Pirate Lord","Station Lord"
            };
            return roles[r.Next(roles.Count)];
        }

        public IFormLinkNullable<ILeveledItemGetter> GetLowRank_Gear()
        {
            Random random = RandomUtils.random;
            List<uint> gearlist = new List<uint>()
                {
                    0x003E8654,//LLI_CrimsonFleet_AssaultDefaultRole [LVLI:003E8654]
                    0x003E8655,//LLI_CrimsonFleet_Charger [LVLI:003E8655]
                    0x003E8656,//LLI_CrimsonFleet_Heavy [LVLI:003E8656]
                    0x003E8659,//LLI_CrimsonFleet_Sniper [LVLI:003E8659]
                    0x003E8658,//LLI_CrimsonFleet_Recruit [LVLI:003E8658]
                    0x003E865A,//LLI_CrimsonFleet_Support [LVLI:003E865A]
                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(gen_quest_main.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public IFormLinkNullable<ILeveledItemGetter> GetHighRank_Gear()
        {
            Random random = RandomUtils.random;
            List<uint> gearlist = new List<uint>()
                {
                    0x003E8657,//LLI_CrimsonFleet_Officer [LVLI:003E8657]
                    0x003E8654,//LLI_CrimsonFleet_AssaultDefaultRole [LVLI:003E8654]
                    0x003E8656,//LLI_CrimsonFleet_Heavy [LVLI:003E8656]
                    0x003E8659,//LLI_CrimsonFleet_Sniper [LVLI:003E8659]

                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(gen_quest_main.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public IFormLinkNullable<ILeveledItemGetter> GetBoss_Gear()
        {
            Random random = RandomUtils.random;
            List<uint> gearlist = new List<uint>()
                {
                    0x0015E25A,//LLI_CrimsonFleet_Heavy_Boss [LVLI:0015E25A]
                    0x003E8657,//LLI_CrimsonFleet_Officer [LVLI:003E8657]
                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(gen_quest_main.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public Npc GetCrewMember(string Room)
        {
            Npc selected = null;
            int roll = RandomUtils.random.Next(100);
            if (roll > 60)
            {
                selected = HighRank[RandomUtils.random.Next(HighRank.Count)];
            }
            else
            {
                selected = LowRank[RandomUtils.random.Next(LowRank.Count)];
            }
            return selected;
        }

        public Npc GetBoss(string Room)
        {
            return Bosses[RandomUtils.random.Next(Bosses.Count)];
        }
    }
}
