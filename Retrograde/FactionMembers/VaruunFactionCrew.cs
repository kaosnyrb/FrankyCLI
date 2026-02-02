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
    public class VaruunFactionCrew : IFactionMembers
    {
        List<Npc> LowRank { get; set; }
        List<Npc> HighRank { get; set; }
        List<Npc> Bosses { get; set; }
        string FactionName { get; set; }
        public VaruunFactionCrew()
        {
            string Faction = "Varuun";
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
                0x000D6FA8,// Outfit_Spacesuit_Varuun_NoHelmetNoBackpack [OTFT:000D6FA8]
            };
            IFormLinkNullable<IOutfitGetter> outfit = new FormKey(gen_quest_main.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
            return outfit;
        }

        public IFormLinkNullable<IOutfitGetter> GetHighRank_Outfit()
        {
            Random random = RandomUtils.random;
            List<uint> Outfits = new List<uint>
            {
                0x00278715,//Outfit_Spacesuit_Varuun [OTFT:00278715]
                0x000D6FA8,//Outfit_Spacesuit_Varuun_NoHelmetNoBackpack [OTFT:000D6FA8]
            };
            IFormLinkNullable<IOutfitGetter> outfit = new FormKey(gen_quest_main.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
            return outfit;
        }

        public IFormLinkNullable<IOutfitGetter> GetBoss_Outfit()
        {
            Random random = RandomUtils.random;
            List<uint> Outfits = new List<uint>
            {
                0x00278715,//Outfit_Spacesuit_Varuun [OTFT:00278715]
                0x000D6FA8,//Outfit_Spacesuit_Varuun_NoHelmetNoBackpack [OTFT:000D6FA8]
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
                "Zealot","Varuun","Fanatic","Believer","Devout",
                "Faithful","Chosen","Pilgrim","Disciple","Adherent",
                "Acolyte","Penitent","Ascendant","Prophet","Herald",
                "Ordained","Anointed","Sanctified","Seeker","Witness",
                "Evangel","Crusader","Templar","Inquisitor","Harbinger",
                "Apostle","Confessor","Mystic","Revenant","Oracle"
            };
            return prefixes[r.Next(prefixes.Count)];
        }

        public string GetLowRank_Name()
        {
            Random r = RandomUtils.random;
            var roles = new List<string>
            {
                "Initiate","New Initiate","Sworn Initiate","Bound Initiate",
                "Acolyte","Temple Acolyte","Flame Acolyte","Serpent Acolyte",
                "Pilgrim","Armed Pilgrim","Devoted Pilgrim","Wandering Pilgrim",
                "Disciple","Sworn Disciple","Blade Disciple","Faith Disciple",
                "Watcher","Gate Watcher","Night Watcher","Flame Watcher",
                "Seeker","Truth Seeker","Path Seeker","Void Seeker",
                "Keeper","Relic Keeper","Shrine Keeper","Gate Keeper",
                "Guard","Temple Guard","Sanctum Guard","Shrine Guard"
            };
            return roles[r.Next(roles.Count)];
        }

        public string GetHighRank_Name()
        {
            Random r = RandomUtils.random;
            var roles = new List<string>
            {
                "Crusader","Holy Crusader","Flame Crusader","Void Crusader",
                "Templar","Sworn Templar","Battle Templar","Elder Templar",
                "Inquisitor","Grand Inquisitor","Faith Inquisitor","Temple Inquisitor",
                "Ordained","Ordained Blade","Ordained Flame","Ordained Warden",
                "Zealot Captain","War Captain","Crusade Captain",
                "Prelate","Battle Prelate","Senior Prelate"
            };
            return roles[r.Next(roles.Count)];
        }

        public string GetBoss_Name()
        {
            Random r = RandomUtils.random;
            var roles = new List<string>
            {
                "High Zealot","Supreme Zealot","Grand Zealot",
                "Archon","War Archon","Temple Archon","Void Archon",
                "Prophet","War Prophet","Doom Prophet","Serpent Prophet",
                "Harbinger","Flame Harbinger","Void Harbinger",
                "Exarch","Grand Exarch","Battle Exarch",
                "High Priest","War Priest","Serpent Priest",
                "Fanatic Lord","Crusade Lord","Temple Lord"
            };
            return roles[r.Next(roles.Count)];
        }

        public IFormLinkNullable<ILeveledItemGetter> GetLowRank_Gear()
        {
            Random random = RandomUtils.random;
            List<uint> gearlist = new List<uint>()
                {
                    0x001BEF71,//LLI_Varuun_Charger [LVLI:001BEF71]
                    0x001BEF75,//LLI_Varuun_AssaultDefaultRole [LVLI:001BEF75]
                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(gen_quest_main.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public IFormLinkNullable<ILeveledItemGetter> GetHighRank_Gear()
        {
            Random random = RandomUtils.random;
            List<uint> gearlist = new List<uint>()
                {
                    0x001BEF71,//LLI_Varuun_Charger [LVLI:001BEF71]
                    0x001BEF75,//LLI_Varuun_AssaultDefaultRole [LVLI:001BEF75]
                    0x0025C601,//LLI_Varuun_Heavy_Boss [LVLI:0025C601]

                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(gen_quest_main.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public IFormLinkNullable<ILeveledItemGetter> GetBoss_Gear()
        {
            Random random = RandomUtils.random;
            List<uint> gearlist = new List<uint>()
                {
                    0x001BEF75,//LLI_Varuun_AssaultDefaultRole [LVLI:001BEF75]
                    0x0025C601,//LLI_Varuun_Heavy_Boss [LVLI:0025C601]
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
