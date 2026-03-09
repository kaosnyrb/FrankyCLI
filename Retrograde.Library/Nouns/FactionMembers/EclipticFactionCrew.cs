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
    public class EclipticFactionCrew : IFactionMembers
    {
        private const string FactionKey = "Ecliptic";
        List<Npc> LowRank { get; set; }
        List<Npc> HighRank { get; set; }
        List<Npc> Bosses { get; set; }
        string FactionName { get; set; }

        public EclipticFactionCrew()
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
            if (RandomProvider.Random.Next(100) > 50)
            {
                npc.HeadParts.Add(NPCTools.GetExtraHeadParts(isfemale));
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
                0x002C5632,//csEcliptic_Assault [CSTY:002C5632]
                0x002C5631,//csEcliptic_Charger [CSTY:002C5631]
                0x002C5630,//csEcliptic_Heavy [CSTY:002C5630]
                0x0026FDB1,//csEcliptic_Officer [CSTY:0026FDB1]
                0x002C562F,//csEcliptic_Sniper [CSTY:002C562F]
                0x002C562E,//csEcliptic_Support [CSTY:002C562E]
            };
            return new FormKey(RetrogradeContext.Current.StarfieldModKey, combatlist[random.Next(combatlist.Count)]).ToNullableLink<ICombatStyleGetter>();
        }

        public IFormLinkNullable<IOutfitGetter> GetLowRank_Outfit()
        {
            Random random = RandomProvider.Random;
            List<uint> Outfits = new List<uint>
            {
                0x00056D4A,//Outfit_Spacesuit_Ecliptic_NoHelmet_NoBackpack [OTFT:00056D4A]
            };
            IFormLinkNullable<IOutfitGetter> outfit = new FormKey(RetrogradeContext.Current.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
            return outfit;
        }

        public IFormLinkNullable<IOutfitGetter> GetHighRank_Outfit()
        {
            Random random = RandomProvider.Random;
            List<uint> Outfits = new List<uint>
            {
                0x0027027D,//Outfit_Spacesuit_Ecliptic [OTFT:0027027D]
                0x0013E5D0,//Outfit_Spacesuit_Ecliptic_NoHelmet [OTFT:0013E5D0]
                0x00056D4A,//Outfit_Spacesuit_Ecliptic_NoHelmet_NoBackpack [OTFT:00056D4A]
            };
            IFormLinkNullable<IOutfitGetter> outfit = new FormKey(RetrogradeContext.Current.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
            return outfit;
        }

        public IFormLinkNullable<IOutfitGetter> GetBoss_Outfit() => GetHighRank_Outfit();

        private string GetFactionPrefix()
        {
            if (FactionName != null) return FactionName;
            Random r = RandomProvider.Random;
            var prefixes = new List<string>
            {
                "Ecliptic","Contractor",
                "Operative",
                "Professional","Militant","Enforcer","Gunhand","Tactician",
                "Vanguard","Sentinel","Warden","Striker","Spearhead",
                "Wardog","Hardliner","Partisan","Outrider","Pointman"
            };
            return prefixes[r.Next(prefixes.Count)];
        }

        public string GetLowRank_Name()
        {
            Random r = RandomProvider.Random;
            var roles = new List<string>
            {
                "Recruit","Field Recruit","New Recruit","Fresh Recruit",
                "Trooper","Patrol Trooper","Garrison Trooper","Perimeter Trooper",
                "Agent","Field Agent","Scout Agent","Recon Agent",
                "Grunt","Cargo Grunt","Station Grunt","Outpost Grunt",
                "Sentry","Gate Sentry","Dock Sentry","Tower Sentry",
                "Technician","Field Technician","Comms Technician","Systems Technician",
                "Scout","Forward Scout","Perimeter Scout","Patrol Scout",
                "Guard","Post Guard","Checkpoint Guard","Facility Guard",
                "Mechanic","Hull Mechanic","Systems Mechanic","Drive Mechanic",
                "Wireman","Calibrator","Reactor Attendant",
                "Inventory Clerk","Requisitions Aide","Manifest Runner","Supply Hand",
                "Warehouse Attendant","Depot Loader","Quartermaster Aide"
            };
            return roles[r.Next(roles.Count)];
        }

        public string GetHighRank_Name()
        {
            Random r = RandomProvider.Random;
            var roles = new List<string>
            {
                "Squad Lead","Fire Team Lead","Patrol Lead","Assault Lead",
                "Sergeant","Field Sergeant","Combat Sergeant","Operations Sergeant",
                "Specialist","Weapons Specialist","Demolitions Specialist","Comms Specialist",
                "Veteran","Combat Veteran","Field Veteran","Senior Veteran",
                "Officer","Field Officer","Tactical Officer","Watch Officer",
                "Lieutenant","Junior Lieutenant","Acting Lieutenant",
                "Chief Engineer","Systems Architect","Ordnance Expert",
                "Acquisitions Officer","Supply Chain Lead","Arms Dealer"
            };
            return roles[r.Next(roles.Count)];
        }

        public string GetBoss_Name()
        {
            Random r = RandomProvider.Random;
            var roles = new List<string>
            {
                "Commander","Field Commander","Station Commander","Operations Commander",
                "Captain","Strike Captain","Garrison Captain","Assault Captain",
                "Colonel","Field Colonel","Senior Colonel",
                "Director","Operations Director","Field Director","Tactical Director",
                "Overseer","Station Overseer","Sector Overseer",
                "Superintendent","Station Superintendent","Garrison Superintendent",
                "General","Brigadier","Marshal"
            };
            return roles[r.Next(roles.Count)];
        }

        public IFormLinkNullable<ILeveledItemGetter> GetLowRank_Gear()
        {
            Random random = RandomProvider.Random;
            List<uint> gearlist = new List<uint>()
                {
                    0x003D60AF,//LLI_Ecliptic_AssaultDefaultRole [LVLI:003D60AF]
                    0x003D60B0,//LLI_Ecliptic_Charger [LVLI:003D60B0]
                    0x003D60B1,//LLI_Ecliptic_Heavy [LVLI:003D60B1]
                    0x003D60B4,//LLI_Ecliptic_Sniper [LVLI:003D60B4]
                    0x003D60B5,//LLI_Ecliptic_Support [LVLI:003D60B5]
                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(RetrogradeContext.Current.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public IFormLinkNullable<ILeveledItemGetter> GetHighRank_Gear()
        {
            Random random = RandomProvider.Random;
            List<uint> gearlist = new List<uint>()
                {
                    0x003D60B2,//LLI_Ecliptic_Officer [LVLI:003D60B2]
                    0x003D60AF,//LLI_Ecliptic_AssaultDefaultRole [LVLI:003D60AF]
                    0x003D60B1,//LLI_Ecliptic_Heavy [LVLI:003D60B1]
                    0x003D60B4,//LLI_Ecliptic_Sniper [LVLI:003D60B4]
                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(RetrogradeContext.Current.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public IFormLinkNullable<ILeveledItemGetter> GetBoss_Gear()
        {
            Random random = RandomProvider.Random;
            List<uint> gearlist = new List<uint>()
                {
                    0x003D60B2,//LLI_Ecliptic_Officer [LVLI:003D60B2]
                    0x003D60B1,//LLI_Ecliptic_Heavy [LVLI:003D60B1]
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
