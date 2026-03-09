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
    public class SpacerFactionCrew : IFactionMembers
    {
        private const string FactionKey = "Spacer";
        List<Npc> LowRank { get; set; }
        List<Npc> HighRank { get; set; }
        List<Npc> Bosses { get; set; }
        string FactionName { get; set; }

        public SpacerFactionCrew()
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
                0x002C562D,//csSpacer_Assault [CSTY:002C562D]
                0x002C562C,//csSpacer_Charger [CSTY:002C562C]
                0x000D2143,//csSpacer_Heavy [CSTY:000D2143]
                0x002C562B,//csSpacer_Recruit [CSTY:002C562B]
                0x000D2144,//csSpacer_Sniper [CSTY:000D2144]
            };
            return new FormKey(RetrogradeContext.Current.StarfieldModKey, combatlist[random.Next(combatlist.Count)]).ToNullableLink<ICombatStyleGetter>();
        }

        public IFormLinkNullable<IOutfitGetter> GetLowRank_Outfit()
        {
            Random random = RandomProvider.Random;
            List<uint> Outfits = new List<uint>
            {
                0x0015E246,//Outfit_Spacesuit_Spacer_Any [OTFT:0015E246]
            };
            IFormLinkNullable<IOutfitGetter> outfit = new FormKey(RetrogradeContext.Current.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
            return outfit;
        }

        public IFormLinkNullable<IOutfitGetter> GetHighRank_Outfit()
        {
            Random random = RandomProvider.Random;
            List<uint> Outfits = new List<uint>
            {
                0x0015E246,//Outfit_Spacesuit_Spacer_Any [OTFT:0015E246]
            };
            IFormLinkNullable<IOutfitGetter> outfit = new FormKey(RetrogradeContext.Current.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
            return outfit;
        }

        public IFormLinkNullable<IOutfitGetter> GetBoss_Outfit()
        {
            Random random = RandomProvider.Random;
            List<uint> Outfits = new List<uint>
            {
                0x0015E246,//Outfit_Spacesuit_Spacer_Any [OTFT:0015E246]
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
                "Spacer","Vagrant","Outlaw","Rogue","Bandit",
                "Scavenger","Drifter","Prowler","Raider","Wretch",
                "Derelict","Squatter","Tweaker","Lowlife","Vermin",
                "Mongrel","Scrapper","Deadbeat","Junker","Castoff",
                "Stray","Skulker","Creeper","Waster","Feral",
                "Hooligan","Burnout","Ruffian","Dropout","Degenerate"
            };
            return prefixes[r.Next(prefixes.Count)];
        }

        public string GetLowRank_Name()
        {
            Random r = RandomProvider.Random;
            var roles = new List<string>
            {
                "Scav","Hull Scav","Junk Scav","Wire Scav",
                "Punk","Dock Punk","Station Punk","Cargo Punk",
                "Rat","Tunnel Rat","Dock Rat","Vent Rat",
                "Nomad","Armed Nomad","Station Nomad","Void Nomad",
                "Thug","Alley Thug","Dock Thug","Cargo Thug",
                "Salvager","Hull Salvager","Junk Salvager","Wire Salvager",
                "Looter","Cargo Looter","Wreck Looter","Station Looter",
                "Goon","Dock Goon","Gate Goon","Cargo Goon",
                "Jury Rigger","Spark Jockey","Grease Rat","Wire Cutter",
                "Torch Hand","Pipe Bender","Bolt Breaker",
                "Pickpocket","Bag Man","Stash Rat","Junk Peddler",
                "Parts Hawker","Goods Mover","Crate Flipper"
            };
            return roles[r.Next(roles.Count)];
        }

        public string GetHighRank_Name()
        {
            Random r = RandomProvider.Random;
            var roles = new List<string>
            {
                "Gang Lead","Dock Lead","Crew Lead","Raid Lead",
                "Enforcer","Senior Enforcer","Head Enforcer","Station Enforcer",
                "Bruiser","Head Bruiser","Dock Bruiser","Senior Bruiser",
                "Veteran","Combat Veteran","Raid Veteran","Station Veteran",
                "Reaver","Senior Reaver","Lead Reaver","Void Reaver",
                "Lieutenant","Gang Lieutenant","Station Lieutenant",
                "Chop Shop Boss","Reactor Cracker","Scrap Engineer",
                "Fence Boss","Black Market Dealer","Salvage Broker"
            };
            return roles[r.Next(roles.Count)];
        }

        public string GetBoss_Name()
        {
            Random r = RandomProvider.Random;
            var roles = new List<string>
            {
                "Gang Boss","Dock Boss","Station Boss","Crew Boss",
                "Warlord","Void Warlord","Station Warlord","Raid Warlord",
                "Kingpin","Station Kingpin","Sector Kingpin",
                "Underboss","Gang Underboss","Station Underboss",
                "Captain","Raid Captain","Crew Captain","Void Captain",
                "Chief","War Chief","Gang Chief","Station Chief",
                "Commander","Raid Commander","Gang Commander"
            };
            return roles[r.Next(roles.Count)];
        }

        public IFormLinkNullable<ILeveledItemGetter> GetLowRank_Gear()
        {
            Random random = RandomProvider.Random;
            List<uint> gearlist = new List<uint>()
                {
                    0x003D0946,//LLI_Spacer_AssaultDefaultRole [LVLI:003D0946]
                    0x003D0947,//LLI_Spacer_Charger [LVLI:003D0947]
                    0x003D0948,//LLI_Spacer_Heavy [LVLI:003D0948]
                    0x003D094B,//LLI_Spacer_Sniper [LVLI:003D094B]
                    0x003D094A,//LLI_Spacer_Recruit [LVLI:003D094A]
                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(RetrogradeContext.Current.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public IFormLinkNullable<ILeveledItemGetter> GetHighRank_Gear()
        {
            Random random = RandomProvider.Random;
            List<uint> gearlist = new List<uint>()
                {
                    0x003D0946,//LLI_Spacer_AssaultDefaultRole [LVLI:003D0946]
                    0x003D0948,//LLI_Spacer_Heavy [LVLI:003D0948]
                    0x003D094B,//LLI_Spacer_Sniper [LVLI:003D094B]
                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(RetrogradeContext.Current.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public IFormLinkNullable<ILeveledItemGetter> GetBoss_Gear()
        {
            Random random = RandomProvider.Random;
            List<uint> gearlist = new List<uint>()
                {
                    0x003D0948,//LLI_Spacer_Heavy [LVLI:003D0948]
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
