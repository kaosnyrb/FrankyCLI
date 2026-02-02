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
            if (RandomUtils.random.Next(100) > 50)
            {
                npc.HeadParts.Add(GetExtraHeadParts(isfemale));
            }

            npc.CombatStyle = GetCombatStyle();

            npc.Items = new ExtendedList<ContainerEntry>
                {
                    new ContainerEntry() { Item = new ContainerItem() { Item = gear, Count = 1 } },
                };
            gen_quest_main.myMod.Npcs.Add(npc);
        }

        private IFormLinkNullable<ICombatStyleGetter> GetCombatStyle()
        {
            Random random = RandomUtils.random;
            List<uint> combatlist = new List<uint>()
            {
                0x002C5632,//csEcliptic_Assault [CSTY:002C5632]
                0x002C5631,//csEcliptic_Charger [CSTY:002C5631]
                0x002C5630,//csEcliptic_Heavy [CSTY:002C5630]
                0x0026FDB1,//csEcliptic_Officer [CSTY:0026FDB1]
                0x002C562F,//csEcliptic_Sniper [CSTY:002C562F]
                0x002C562E,//csEcliptic_Support [CSTY:002C562E]
            };
            return new FormKey(gen_quest_main.StarfieldModKey, combatlist[random.Next(combatlist.Count)]).ToNullableLink<ICombatStyleGetter>();
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

        private IFormLinkNullable<IHeadPartGetter> GetExtraHeadParts(bool female)
        {
            Random random = RandomUtils.random;
            if (female)
            {
                List<uint> partlist = new List<uint>()
                {
                    0x001F7EF3,//Human_Female_Jewelry_Bridge_01_F "Human_Female_Jewelry_Bridge_01_F" [HDPT:001F7EF3]
                    0x001F7EF4,//Human_Female_Jewelry_Bridge_02_F "Human_Female_Jewelry_Bridge_02_F" [HDPT:001F7EF4]
                    0x001F7EF5,//Human_Female_Jewelry_Double_Earrings_F "Human_Female_Jewelry_Double_Earrings_F" [HDPT:001F7EF5]
                    0x001F7EF2,//Human_Female_Jewelry_Double_Earrings_Left_F "Human_Female_Jewelry_Double_Earrings_Left_F" [HDPT:001F7EF2]
                    0x001F7EF6,//Human_Female_Jewelry_Double_Earrings_Right_F "Human_Female_Jewelry_Double_Earrings_Right_F" [HDPT:001F7EF6]
                    0x00026EB7,//Human_Female_Jewelry_Lobe_Diamond_F "Human_Female_Jewelry_Lobe_Diamond_F" [HDPT:00026EB7]
                    0x001F7EF8,//Human_Female_Jewelry_Lobe_Gauge_F "Human_Female_Jewelry_Lobe_Gauge_F" [HDPT:001F7EF8]
                    0x001F7EFD,//Human_Female_Jewelry_Nostril_Ball_F "Human_Female_Jewelry_Nostril_Ball_F" [HDPT:001F7EFD]
                    0x001F7EFE,//Human_Female_Jewelry_Nostril_Ball_Left_F "Human_Female_Jewelry_Nostril_Ball_Left_F" [HDPT:001F7EFE]
                    0x001F7EFF,//Human_Female_Jewelry_Nostril_Ball_Right_F "Human_Female_Jewelry_Nostril_Ball_Right_F" [HDPT:001F7EFF]
                    0x001F7F00,//Human_Female_Jewelry_Rings_Assorted_F "Human_Female_Jewelry_Rings_Assorted_F" [HDPT:001F7F00]
                    0x001F7F01,//Human_Female_Jewelry_Rings_Assorted_Left_F "Human_Female_Jewelry_Rings_Assorted_Left_F" [HDPT:001F7F01]
                    0x001F7F02,//Human_Female_Jewelry_Rings_Assorted_Right_F "Human_Female_Jewelry_Rings_Assorted_Right_F" [HDPT:001F7F02]
                    0x001F7F03,//Human_Female_Jewelry_Septum_01_F "Human_Female_Jewelry_Septum_01_F" [HDPT:001F7F03]
                    0x001F7F04,//Human_Female_Jewelry_Septum_02_F "Human_Female_Jewelry_Septum_02_F" [HDPT:001F7F04]
                    0x001F7F05,//Human_Female_Jewelry_Septum_03_F "Human_Female_Jewelry_Septum_03_F" [HDPT:001F7F05]
                };
                return new FormKey(gen_quest_main.StarfieldModKey, partlist[random.Next(partlist.Count)]).ToNullableLink<IHeadPartGetter>();
            }
            else
            {
                List<uint> partlist = new List<uint>()
                {
                    0x00160C2C,//Human_Male_Beard_BeardStache "Human_Male_Beard_BeardStache" [HDPT:00160C2C]
                    0x0015E754,//Human_Male_Beard_ChinCurtain "Human_Male_Beard_ChinCurtain" [HDPT:0015E754]
                    0x00160C2D,//Human_Male_Beard_ChinFade "Human_Male_Beard_ChinFade" [HDPT:00160C2D]
                    0x0015F5A6,//Human_Male_Beard_ClassicBeard "Human_Male_Beard_ClassicBeard" [HDPT:0015F5A6]
                    0x0015FD51,//Human_Male_Beard_CleanThin "Human_Male_Beard_CleanThin" [HDPT:0015FD51]
                    0x002CDD7D,//Human_Male_Beard_Full_rugged "Human_Male_Beard_Full_rugged" [HDPT:002CDD7D]
                    0x00160C2F,//Human_Male_Beard_Goatee "Human_Male_Beard_Goatee" [HDPT:00160C2F]
                    0x00160C2E,//Human_Male_Beard_HeavyStubble "Human_Male_Beard_HeavyStubble" [HDPT:00160C2E]
                    0x00160C2B,//Human_Male_Beard_PaintersMoustache "Human_Male_Beard_PaintersMoustache" [HDPT:00160C2B]
                    0x00160369,//Human_Male_Beard_PatchStache "Human_Male_Beard_PatchStache" [HDPT:00160369]
                    0x001F764A,//Human_Male_Beard_Patchy "Human_Male_Beard_Patchy" [HDPT:001F764A]
                    0x00160C30,//Human_Male_Beard_PencilMoustache "Human_Male_Beard_PencilMoustache" [HDPT:00160C30]
                    0x0016036B,//Human_Male_Beard_PirateThick "Human_Male_Beard_PirateThick" [HDPT:0016036B]
                    0x0015F353,//Human_Male_Beard_PirateThin "Human_Male_Beard_PirateThin" [HDPT:0015F353]
                    0x00193431,//Human_Male_Beard_Stubble_Heavy "Human_Male_Beard_Stubble_Heavy" [HDPT:00193431]
                    0x00193430,//Human_Male_Beard_Stubble_Light "Human_Male_Beard_Stubble_Light" [HDPT:00193430]
                    0x00193432,//Human_Male_Beard_Stubble_Patchy "Human_Male_Beard_Stubble_Patchy" [HDPT:00193432]
                    0x001150DC,//Human_Male_Beard_Wildman "Human_Male_Beard_Wildman" [HDPT:001150DC]
                    0x001676CF,//Human_Male_Beard_Wolfchops "Human_Male_Beard_Wolfchops" [HDPT:001676CF]
                };
                return new FormKey(gen_quest_main.StarfieldModKey, partlist[random.Next(partlist.Count)]).ToNullableLink<IHeadPartGetter>();
            }
        }

        public IFormLinkNullable<IOutfitGetter> GetLowRank_Outfit()
        {
            Random random = RandomUtils.random;
            List<uint> Outfits = new List<uint>
            {
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
                0x0013E5D0,//Outfit_Spacesuit_Ecliptic_NoHelmet [OTFT:0013E5D0]
                0x00056D4A,//Outfit_Spacesuit_Ecliptic_NoHelmet_NoBackpack [OTFT:00056D4A]
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
                0x0013E5D0,//Outfit_Spacesuit_Ecliptic_NoHelmet [OTFT:0013E5D0]
                0x00056D4A,//Outfit_Spacesuit_Ecliptic_NoHelmet_NoBackpack [OTFT:00056D4A]
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
                "Ecliptic","Contractor",
                "Operative",
                "Professional","Militant","Enforcer","Gunhand","Tactician",
                "Vanguard","Sentinel","Warden","Striker","Dragoon",
                "Wardog","Ironside","Partisan","Outrider","Bulwark"
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
