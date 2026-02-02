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
    public class EclipticFactionCrew : IFactionMembers
    {
        List<Npc> LowRank { get; set; }
        List<Npc> HighRank { get; set; }
        List<Npc> Bosses { get; set; }
        string FactionName { get; set; }
        public EclipticFactionCrew()
        {
            string Faction = "Ecliptic";
            Random random = RandomUtils.random;
            string Crewname = Faction;
            FactionName = GetFactionPrefix();

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
                npc.Name = GetFactionPrefix() + " " + GetLowRank_Name();
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
                npc.Name = GetFactionPrefix() + " " + GetHighRank_Name();
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
                npc.Name = GetFactionPrefix() + " " + GetBoss_Name();
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
            npc.HeadParts.Add(GetHaircut(isfemale));
            npc.Items = new ExtendedList<ContainerEntry>
                {
                    new ContainerEntry() { Item = new ContainerItem() { Item = gear, Count = 1 } },
                };
            gen_quest_main.myMod.Npcs.Add(npc);
        }

        private IFormLinkNullable<IHeadPartGetter> GetHaircut(bool female)
        {
            Random random = RandomUtils.random;
            if (female)
            {
                List<uint> hairlist = new List<uint>()
                {
                    0x00127395,//Human_Female_Hair_Bob [HDPT:00127395]
                    0x0015578B,//Human_Female_Hair_Business [HDPT:0015578B]
                    0x00159AF2,//Human_Female_Hair_Buzz_Mohawk [HDPT:00159AF2]
                    0x00172588,//Human_Female_Hair_CyberFade [HDPT:00172588]
                    0x0012FDE2,//Human_Female_Hair_Dreadlocks_HairMesh [HDPT:0012FDE2]
                    0x0012FDE3,//Human_Female_Hair_Dreadlocks_HairTie [HDPT:0012FDE3]
                    0x00132C5A,//Human_Female_Hair_Even_Buzz_Back [HDPT:00132C5A]
                    0x00128008,//Human_Female_Hair_Hairspray_Bob [HDPT:00128008]
                    0x0015B029,//Human_Female_Hair_High_and_Tight [HDPT:0015B029]
                    0x00133E4E,//Human_Female_Hair_Hollywood_curls [HDPT:00133E4E]
                    0x0014AFDD,//Human_Female_Hair_Messy_Bob [HDPT:0014AFDD]
                    0x00134EB1,//Human_Female_Hair_Messy_Business [HDPT:00134EB1]
                    0x0005B53C,//Human_Female_Hair_Messy_Updo [HDPT:0005B53C]
                    0x000D9D3A,//Human_Female_Hair_Mullet [HDPT:000D9D3A]
                };
                return new FormKey(gen_quest_main.StarfieldModKey, hairlist[random.Next(hairlist.Count)]).ToNullableLink<IHeadPartGetter>();
            }
            else
            {
                List<uint> hairlist = new List<uint>()
                {
                    0x00127396,//Human_Male_Hair_Bob [HDPT:00127396]
                    0x0015578A,//Human_Male_Hair_Business [HDPT:0015578A]
                    0x00159AF3,//Human_Male_Hair_Buzz_Mohawk [HDPT:00159AF3]
                    0x00266092,//Human_Male_Hair_Choppy_Bob [HDPT:00266092]
                    0x0013F87D,//Human_Male_Hair_Coily_Mohawk [HDPT:0013F87D]
                    0x001177D1,//Human_Male_Hair_Cornrows_Beads [HDPT:001177D1]
                    0x0013EB51,//Human_Male_Hair_Cropped [HDPT:0013EB51]
                    0x00169ED3,//Human_Male_Hair_CyberFade [HDPT:00169ED3]
                    0x00132C59,//Human_Male_Hair_Even_Buzz_Front [HDPT:00132C59]
                    0x0014781F,//Human_Male_Hair_Flat_Top [HDPT:0014781F]
                    0x00134EB0,//Human_Male_Hair_Messy_Business [HDPT:00134EB0]
                    0x00264EFA,//Human_Male_Hair_None [HDPT:00264EFA]
                    0x000D9D39,//Human_Male_Hair_Mullet [HDPT:000D9D39]
                    0x00141E96,//Human_Male_Hair_Shaggy [HDPT:00141E96]
                    0x0015335C,//Human_Male_Hair_Spiked [HDPT:0015335C]
                    0x0012F26F,//Human_Male_Hair_Viking_Braids [HDPT:0012F26F]
                };
                return new FormKey(gen_quest_main.StarfieldModKey, hairlist[random.Next(hairlist.Count)]).ToNullableLink<IHeadPartGetter>();
            }
        }

        public IFormLinkNullable<IOutfitGetter> GetLowRank_Outfit()
        {
            Random random = RandomUtils.random;
            List<uint> Outfits = new List<uint>
            {
                0x00042D85,// Outfit_Worker [OTFT:00042D85]
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
                0x0027027D,//Outfit_Spacesuit_Ecliptic [OTFT:0027027D]
            };
            IFormLinkNullable<IOutfitGetter> outfit = new FormKey(gen_quest_main.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
            return outfit;
        }

        public IFormLinkNullable<IOutfitGetter> GetBoss_Outfit()
        {
            Random random = RandomUtils.random;
            List<uint> Outfits = new List<uint>
            {
                0x0027027D,//Outfit_Spacesuit_Ecliptic [OTFT:0027027D]
            };
            IFormLinkNullable<IOutfitGetter> outfit = new FormKey(gen_quest_main.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
            return outfit;
        }

        private string GetFactionPrefix()
        {
            if (FactionName != null) return FactionName;
            Random r = RandomUtils.random;
            var prefixes = new List<string>
            {
                "Ecliptic","Eclipse","Merc","Mercenary","Contractor",
                "Operative","Hired Gun","Sellsword","Freelancer","Soldier"
            };
            return prefixes[r.Next(prefixes.Count)];
        }

        public string GetLowRank_Name()
        {
            Random r = RandomUtils.random;
            var roles = new List<string>
            {
                "Recruit","Field Recruit","New Recruit","Fresh Recruit",
                "Trooper","Patrol Trooper","Garrison Trooper","Perimeter Trooper",
                "Operative","Field Operative","Scout Operative","Recon Operative",
                "Grunt","Cargo Grunt","Station Grunt","Outpost Grunt",
                "Sentry","Gate Sentry","Dock Sentry","Tower Sentry",
                "Technician","Field Technician","Comms Technician","Systems Technician",
                "Scout","Forward Scout","Perimeter Scout","Patrol Scout",
                "Guard","Post Guard","Checkpoint Guard","Facility Guard"
            };
            return roles[r.Next(roles.Count)];
        }

        public string GetHighRank_Name()
        {
            Random r = RandomUtils.random;
            var roles = new List<string>
            {
                "Squad Lead","Fire Team Lead","Patrol Lead","Assault Lead",
                "Sergeant","Field Sergeant","Combat Sergeant","Operations Sergeant",
                "Specialist","Weapons Specialist","Demolitions Specialist","Comms Specialist",
                "Veteran","Combat Veteran","Field Veteran","Senior Veteran",
                "Officer","Field Officer","Tactical Officer","Watch Officer",
                "Lieutenant","Junior Lieutenant","Acting Lieutenant"
            };
            return roles[r.Next(roles.Count)];
        }

        public string GetBoss_Name()
        {
            Random r = RandomUtils.random;
            var roles = new List<string>
            {
                "Commander","Field Commander","Station Commander","Operations Commander",
                "Captain","Strike Captain","Garrison Captain","Assault Captain",
                "Colonel","Field Colonel","Senior Colonel",
                "Director","Operations Director","Field Director","Tactical Director",
                "Overseer","Station Overseer","Sector Overseer",
                "Warden","Station Warden","Garrison Warden",
                "General","Brigadier","Marshal"
            };
            return roles[r.Next(roles.Count)];
        }

        public IFormLinkNullable<ILeveledItemGetter> GetLowRank_Gear()
        {
            Random random = RandomUtils.random;
            List<uint> gearlist = new List<uint>()
                {
                    0x003D60AF,//LLI_Ecliptic_AssaultDefaultRole [LVLI:003D60AF]
                    0x003D60B0,//LLI_Ecliptic_Charger [LVLI:003D60B0]
                    0x003D60B1,//LLI_Ecliptic_Heavy [LVLI:003D60B1]
                    0x003D60B4,//LLI_Ecliptic_Sniper [LVLI:003D60B4]
                    0x003D60B3,//LLI_Ecliptic_Recruit [LVLI:003D60B3]
                    0x003D60B5,//LLI_Ecliptic_Support [LVLI:003D60B5]
                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(gen_quest_main.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public IFormLinkNullable<ILeveledItemGetter> GetHighRank_Gear()
        {
            Random random = RandomUtils.random;
            List<uint> gearlist = new List<uint>()
                {
                    0x003D60B2,//LLI_Ecliptic_Officer [LVLI:003D60B2]
                    0x003D60AF,//LLI_Ecliptic_AssaultDefaultRole [LVLI:003D60AF]
                    0x003D60B1,//LLI_Ecliptic_Heavy [LVLI:003D60B1]
                    0x003D60B4,//LLI_Ecliptic_Sniper [LVLI:003D60B4]
                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(gen_quest_main.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public IFormLinkNullable<ILeveledItemGetter> GetBoss_Gear()
        {
            Random random = RandomUtils.random;
            List<uint> gearlist = new List<uint>()
                {
                    0x003D60B2,//LLI_Ecliptic_Officer [LVLI:003D60B2]
                    0x003D60B1,//LLI_Ecliptic_Heavy [LVLI:003D60B1]
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
