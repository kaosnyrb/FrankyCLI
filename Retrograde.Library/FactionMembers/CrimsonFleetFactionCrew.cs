using Retrograde.Chains.Interfaces;
using Retrograde.Chains;
using Retrograde.FactionMembers;
using Retrograde.Utils;
using Retrograde;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;

namespace Retrograde.FactionMembers
{
    public class CrimsonFleetFactionCrew : IFactionMembers
    {
        List<Npc> LowRank { get; set; }
        List<Npc> HighRank { get; set; }
        List<Npc> Bosses { get; set; }
        string FactionName { get; set; }
        public CrimsonFleetFactionCrew()
        {
            string Faction = "Crimsonfleet";
            Random random = RandomProvider.Random;
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
                var NPC = RetrogradeContext.Current.TargetMod.Npcs[new FormKey(RetrogradeContext.Current.TargetMod.ModKey, NPCTools.GetTemplateNPC(isfemale))].DeepCopy();
                Npc npc = NPCTools.CloneNPC(RetrogradeContext.Current.TargetMod, NPC);

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
                var NPC = RetrogradeContext.Current.TargetMod.Npcs[new FormKey(RetrogradeContext.Current.TargetMod.ModKey, NPCTools.GetTemplateNPC(isfemale))].DeepCopy();
                Npc npc = NPCTools.CloneNPC(RetrogradeContext.Current.TargetMod, NPC);

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
                var NPC = RetrogradeContext.Current.TargetMod.Npcs[new FormKey(RetrogradeContext.Current.TargetMod.ModKey, NPCTools.GetTemplateNPC(isfemale))].DeepCopy();
                Npc npc = NPCTools.CloneNPC(RetrogradeContext.Current.TargetMod, NPC);

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
            if (!isfemale)
            {
                //pirates like beards
                if (RandomProvider.Random.Next(100) > 40)
                {
                    npc.HeadParts.Add(GetExtraHeadParts(isfemale));
                }
            }
            else
            {
                if (RandomProvider.Random.Next(100) > 50)
                {
                    npc.HeadParts.Add(GetExtraHeadParts(isfemale));
                }
            }

            npc.CombatStyle = GetCombatStyle();

            npc.Items = new ExtendedList<ContainerEntry>
                {
                    new ContainerEntry() { Item = new ContainerItem() { Item = gear, Count = 1 } },
                };
            RetrogradeContext.Current.TargetMod.Npcs.Add(npc);
        }

        private IFormLinkNullable<ICombatStyleGetter> GetCombatStyle()
        {
            Random random = RandomProvider.Random;
            List<uint> combatlist = new List<uint>()
            {
                0x002C5632,//csCrimsonFleet_Assault [CSTY:002C5638]
                0x002C5631,//csCrimsonFleet_Charger [CSTY:002C5637]
                0x002C5630,//csCrimsonFleet_Heavy [CSTY:002C5636]
                0x0026FDB1,//csCrimsonFleet_LowLevel [CSTY:00178CA4]
                0x002C562F,//csCrimsonFleet_Recruit [CSTY:002C5635]
                0x002C562E,//csCrimsonFleet_Sniper [CSTY:002C5634]
            };
            return new FormKey(RetrogradeContext.Current.StarfieldModKey, combatlist[random.Next(combatlist.Count)]).ToNullableLink<ICombatStyleGetter>();
        }

        private IFormLinkNullable<IHeadPartGetter> GetExtraHeadParts(bool female)
        {
            Random random = RandomProvider.Random;
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
                return new FormKey(RetrogradeContext.Current.StarfieldModKey, partlist[random.Next(partlist.Count)]).ToNullableLink<IHeadPartGetter>();
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
                return new FormKey(RetrogradeContext.Current.StarfieldModKey, partlist[random.Next(partlist.Count)]).ToNullableLink<IHeadPartGetter>();
            }
        }

        private IFormLinkNullable<IHeadPartGetter> GetHaircut(bool female)
        {
            Random random = RandomProvider.Random;
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
                return new FormKey(RetrogradeContext.Current.StarfieldModKey, hairlist[random.Next(hairlist.Count)]).ToNullableLink<IHeadPartGetter>();
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
                return new FormKey(RetrogradeContext.Current.StarfieldModKey, hairlist[random.Next(hairlist.Count)]).ToNullableLink<IHeadPartGetter>();
            }
        }

        public IFormLinkNullable<IOutfitGetter> GetLowRank_Outfit()
        {
            Random random = RandomProvider.Random;
            List<uint> Outfits = new List<uint>
            {
                0x00279225//Outfit_Spacesuit_CrimsonFleet_NoHelmet [OTFT:00279225]   
            };
            IFormLinkNullable<IOutfitGetter> outfit = new FormKey(RetrogradeContext.Current.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
            return outfit;
        }

        public IFormLinkNullable<IOutfitGetter> GetHighRank_Outfit()
        {
            Random random = RandomProvider.Random;
            List<uint> Outfits = new List<uint>
            {
                0x00018DCF,//Outfit_Spacesuit_CrimsonFleet [OTFT:00018DCF]
                0x00279225//Outfit_Spacesuit_CrimsonFleet_NoHelmet [OTFT:00279225]                
            };
            IFormLinkNullable<IOutfitGetter> outfit = new FormKey(RetrogradeContext.Current.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
            return outfit;
        }

        public IFormLinkNullable<IOutfitGetter> GetBoss_Outfit()
        {
            Random random = RandomProvider.Random;
            List<uint> Outfits = new List<uint>
            {
                0x00018DCF,//Outfit_Spacesuit_CrimsonFleet [OTFT:00018DCF]
                0x00279225,//Outfit_Spacesuit_CrimsonFleet_NoHelmet [OTFT:00279225]
                0x00066ACE//Outfit_Spacesuit_CrimsonFleet_Officer [OTFT:00066ACE]
            };
            IFormLinkNullable<IOutfitGetter> outfit = new FormKey(RetrogradeContext.Current.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
            return outfit;
        }

        private string GetFactionPrefix()
        {
            if (FactionName != null) return FactionName;
            Random r = RandomProvider.Random;
            var prefixes = new List<string>
            {
                "Crimson","Pirate","Buccaneer",
                "Corsair","Raider","Marauder","Freebooter","Privateer",
                "Reaver","Plunderer","Cutthroat","Smuggler","Brigand",
                "Rogue","Outcast","Scourge","Pillager","Blackguard",
                "Desperado","Swashbuckler","Dread","Renegade","Bandit",
                "Ravager","Prowler","Skulker","Warmonger","Exile"
            };
            return prefixes[r.Next(prefixes.Count)];
        }

        public string GetLowRank_Name()
        {
            Random r = RandomProvider.Random;
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
            Random r = RandomProvider.Random;
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
            Random r = RandomProvider.Random;
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
            Random random = RandomProvider.Random;
            List<uint> gearlist = new List<uint>()
                {
                    0x003E8654,//LLI_CrimsonFleet_AssaultDefaultRole [LVLI:003E8654]
                    0x003E8655,//LLI_CrimsonFleet_Charger [LVLI:003E8655]
                    0x003E8656,//LLI_CrimsonFleet_Heavy [LVLI:003E8656]
                    0x003E8659,//LLI_CrimsonFleet_Sniper [LVLI:003E8659]
                    0x003E8658,//LLI_CrimsonFleet_Recruit [LVLI:003E8658]
                    0x003E865A,//LLI_CrimsonFleet_Support [LVLI:003E865A]
                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(RetrogradeContext.Current.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public IFormLinkNullable<ILeveledItemGetter> GetHighRank_Gear()
        {
            Random random = RandomProvider.Random;
            List<uint> gearlist = new List<uint>()
                {
                    0x003E8657,//LLI_CrimsonFleet_Officer [LVLI:003E8657]
                    0x003E8654,//LLI_CrimsonFleet_AssaultDefaultRole [LVLI:003E8654]
                    0x003E8656,//LLI_CrimsonFleet_Heavy [LVLI:003E8656]
                    0x003E8659,//LLI_CrimsonFleet_Sniper [LVLI:003E8659]

                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(RetrogradeContext.Current.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public IFormLinkNullable<ILeveledItemGetter> GetBoss_Gear()
        {
            Random random = RandomProvider.Random;
            List<uint> gearlist = new List<uint>()
                {
                    0x0015E25A,//LLI_CrimsonFleet_Heavy_Boss [LVLI:0015E25A]
                    0x003E8657,//LLI_CrimsonFleet_Officer [LVLI:003E8657]
                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(RetrogradeContext.Current.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public Npc GetCrewMember(string Room)
        {
            Npc selected = null;
            int roll = RandomProvider.Random.Next(100);
            if (roll > 60)
            {
                selected = HighRank[RandomProvider.Random.Next(HighRank.Count)];
            }
            else
            {
                selected = LowRank[RandomProvider.Random.Next(LowRank.Count)];
            }
            return selected;
        }

        public Npc GetBoss(string Room)
        {
            return Bosses[RandomProvider.Random.Next(Bosses.Count)];
        }

        public Npc GetHighLevelTarget()
        {
            string Faction = "Crimsonfleet";
            Random random = RandomProvider.Random;
            bool isfemale = random.Next(100) > 50;
            var NPC = RetrogradeContext.Current.TargetMod.Npcs[new FormKey(RetrogradeContext.Current.TargetMod.ModKey, NPCTools.GetTemplateNPC(isfemale))].DeepCopy();
            Npc npc = NPCTools.CloneNPC(RetrogradeContext.Current.TargetMod, NPC);

            var outfit = GetHighRank_Outfit();
            npc.Name = GetFactionPrefix() + " " + GetHighRank_Name();
            var gear = GetHighRank_Gear();
            var lev = new PcLevelMult();
            lev.LevelMult = 0.75f + (float)random.NextDouble();
            npc.Level = lev;

            CreateNPC(Faction, random, isfemale, npc, outfit, gear);
            return npc;
        }
    }
}
