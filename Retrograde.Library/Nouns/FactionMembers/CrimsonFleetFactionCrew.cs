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
        private const string FactionKey = "Crimsonfleet";
        List<Npc> LowRank { get; set; }
        List<Npc> HighRank { get; set; }
        List<Npc> Bosses { get; set; }
        string FactionName { get; set; }

        public CrimsonFleetFactionCrew()
        {
            FactionName = GetFactionPrefix();

            LowRank  = new List<Npc>();
            HighRank = new List<Npc>();
            Bosses   = new List<Npc>();

            BuildTier(LowRank,  10, 75, 0.5f,  0.5f, GetLowRank_Outfit,  GetLowRank_Name,  GetLowRank_Gear);
            BuildTier(HighRank,  5, 75, 0.75f, 1.0f, GetHighRank_Outfit, GetHighRank_Name, GetHighRank_Gear);
            BuildTier(Bosses,    1, 50, 1.0f,  1.0f, GetBoss_Outfit,     GetBoss_Name,     GetBoss_Gear);
        }

        private void BuildTier(List<Npc> list, int count, int femaleThreshold,
            float levelBase, float levelRange,
            Func<IFormLinkNullable<IOutfitGetter>> getOutfit,
            Func<string> getName,
            Func<IFormLinkNullable<ILeveledItemGetter>> getGear)
        {
            var random = RandomProvider.Random;
            for (int i = 0; i < count; i++)
            {
                bool isfemale = random.Next(100) > femaleThreshold;
                var NPC = NPCTools.FindTemplateNpc(isfemale);
                Npc npc = NPCTools.CloneNPC(RetrogradeContext.Current.TargetMod, NPC, respawn: true);

                npc.Name = GetFactionPrefix() + " " + getName();
                var lev = new PcLevelMult();
                lev.LevelMult = levelBase + (float)random.NextDouble() * levelRange;
                npc.Level = lev;

                CreateNPC(FactionKey, random, isfemale, npc, getOutfit(), getGear());
                list.Add(npc);
            }
        }

        private void CreateNPC(string Faction, Random random, bool isfemale, Npc npc, IFormLinkNullable<IOutfitGetter> outfit, IFormLinkNullable<ILeveledItemGetter> gear)
        {
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
            if (!isfemale)
            {
                //pirates like beards
                if (RandomProvider.Random.Next(100) > 40)
                {
                    npc.HeadParts.Add(NPCTools.GetExtraHeadParts(isfemale));
                }
            }
            else
            {
                if (RandomProvider.Random.Next(100) > 50)
                {
                    npc.HeadParts.Add(NPCTools.GetExtraHeadParts(isfemale));
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
                "Crimson","Pirate","Raider",
                "Smuggler","Cutthroat","Hijacker","Gunrunner","Rogue",
                "Outcast","Renegade","Bandit","Prowler","Skulker",
                "Exile","Fence","Broker","Handler","Fixer",
                "Supplier","Operative","Stringer","Grifter","Hustler",
                "Bootlegger","Syndicate","Cartel","Racketeer","Trafficker",
                "Spotter","Wheelman","Triggerman","Enforcer","Coyote"
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
                "Guard","Gate Guard","Lockup Guard","Cargo Guard",
                "Stockpiler","Munitions Loader","Manifest Clerk","Supply Grunt",
                "Welder","Hull Welder","Patch Welder","Arc Welder",
                "Machinist","Drive Wrench","Wire Monkey",
                "Mule","Cargo Mule","Drop Mule","Contraband Mule",
                "Courier","Dead Drop Courier","Package Courier"
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
                "Taskmaster","Senior Taskmaster","Gun Taskmaster","Dock Taskmaster",
                "Overseer","Junior Overseer","Acting Overseer",
                "Logistics Officer","Supply Coordinator","Dispatch Lead",
                "Chief Mechanic","Drive Engineer","Reactor Technician",
                "Cargo Broker","Contraband Officer","Procurement Lead"
            };
            return roles[r.Next(roles.Count)];
        }

        public string GetBoss_Name()
        {
            Random r = RandomProvider.Random;
            var roles = new List<string>
            {
                "Crew Boss","Gun Boss","Dock Boss","Raid Boss",
                "Strike Captain","Security Captain","Gun Deck Captain","Boarding Captain",
                "First Mate","Chief Mate","Senior Mate",
                "Quartermaster","Black Quartermaster","Lootmaster",
                "Operations Chief","Dock Chief","Salvage Chief","Security Chief",
                "Warden","Raid Warden","Station Warden",
                "Commander","Raid Commander","Station Commander",
                "Captain","Void Captain","Fleet Captain",
                "Warlord","Syndicate Head","Station Boss",
                "Supply Master","Armory Director","Operations Head"
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
            var random = RandomProvider.Random;
            bool isfemale = random.Next(100) > 50;
            var NPC = NPCTools.FindTemplateNpc(isfemale);
            Npc npc = NPCTools.CloneNPC(RetrogradeContext.Current.TargetMod, NPC, respawn: true);

            var outfit = GetHighRank_Outfit();
            npc.Name = GetFactionPrefix() + " " + GetHighRank_Name();
            var gear = GetHighRank_Gear();
            var lev = new PcLevelMult();
            lev.LevelMult = 0.75f + (float)random.NextDouble();
            npc.Level = lev;

            CreateNPC(FactionKey, random, isfemale, npc, outfit, gear);
            return npc;
        }
    }
}
