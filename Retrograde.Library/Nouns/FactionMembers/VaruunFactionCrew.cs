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
    public class VaruunFactionCrew : IFactionMembers
    {
        private const string FactionKey = "Varuun";
        List<Npc> LowRank { get; set; }
        List<Npc> HighRank { get; set; }
        List<Npc> Bosses { get; set; }
        string FactionName { get; set; }

        public VaruunFactionCrew()
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
            if (RandomProvider.Random.Next(100) > 80)
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
            // Use extras as theres only 2
            List<uint> combatlist = new List<uint>()
            {
                0x0026FDB6,//csVaruun_Assault [CSTY:0026FDB6]
                0x002C562A,//csVaruun_Charger [CSTY:002C562A]
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
                0x000D6FA8,// Outfit_Spacesuit_Varuun_NoHelmetNoBackpack [OTFT:000D6FA8]
            };
            IFormLinkNullable<IOutfitGetter> outfit = new FormKey(RetrogradeContext.Current.StarfieldModKey, Outfits[random.Next(Outfits.Count)]).ToNullableLink<IOutfitGetter>();
            return outfit;
        }

        public IFormLinkNullable<IOutfitGetter> GetHighRank_Outfit()
        {
            Random random = RandomProvider.Random;
            List<uint> Outfits = new List<uint>
            {
                0x00278715,//Outfit_Spacesuit_Varuun [OTFT:00278715]
                0x000D6FA8,//Outfit_Spacesuit_Varuun_NoHelmetNoBackpack [OTFT:000D6FA8]
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
                "Zealot","Varuun","Fanatic","Believer","Devout",
                "Faithful","Chosen","Pilgrim","Disciple","Adherent",
                "Penitent","Anointed","Sanctified","Seeker","Witness",
                "Ordained","Militant","Radical","Remnant","Emissary",
                "Convert","Loyalist","Insider","Devotee","Operative",
                "Extremist","Separatist","Purist","Partisan","Ideologue"
            };
            return prefixes[r.Next(prefixes.Count)];
        }

        public string GetLowRank_Name()
        {
            Random r = RandomProvider.Random;
            var roles = new List<string>
            {
                "Initiate","New Initiate","Sworn Initiate","Bound Initiate",
                "Novice","Temple Novice","Flame Novice","Serpent Novice",
                "Wanderer","Armed Wanderer","Devoted Wanderer","Roaming Wanderer",
                "Supplicant","Sworn Supplicant","Blade Supplicant","Faith Supplicant",
                "Watcher","Gate Watcher","Night Watcher","Flame Watcher",
                "Aspirant","Truth Aspirant","Path Aspirant","Void Aspirant",
                "Keeper","Relic Keeper","Shrine Keeper","Gate Keeper",
                "Guard","Temple Guard","Sanctum Guard","Shrine Guard",
                "Technician","Comms Technician","Hull Technician","Systems Technician",
                "Fabricator","Module Fabricator","Circuit Fabricator",
                "Tithe Collector","Asset Runner","Supply Carrier","Relic Courier",
                "Stockroom Attendant","Cache Tender","Offering Bearer"
            };
            return roles[r.Next(roles.Count)];
        }

        public string GetHighRank_Name()
        {
            Random r = RandomProvider.Random;
            var roles = new List<string>
            {
                "Enforcer","Serpent Enforcer","Void Enforcer","Devoted Enforcer",
                "Handler","Cell Handler","Field Handler","Senior Handler",
                "Interrogator","Grand Interrogator","Faith Interrogator","Temple Interrogator",
                "Indoctrinator","Combat Indoctrinator","Field Indoctrinator","Senior Indoctrinator",
                "Strike Captain","War Captain","Serpent Captain",
                "Commissar","Battle Commissar","Senior Commissar",
                "Lead Technician","Systems Overseer","Reactor Specialist",
                "Acquisitions Handler","Relic Procurer","Tithe Auditor"
            };
            return roles[r.Next(roles.Count)];
        }

        public string GetBoss_Name()
        {
            Random r = RandomProvider.Random;
            var roles = new List<string>
            {
                "High Overseer","Supreme Overseer","Grand Overseer",
                "Commandant","War Commandant","Temple Commandant","Void Commandant",
                "Deacon","War Deacon","Senior Deacon","Serpent Deacon",
                "Director","Doctrine Director","Operations Director",
                "Regent","Grand Regent","Battle Regent",
                "High Priest","War Priest","Serpent Priest",
                "Cell Leader","Crusade Leader","Temple Leader"
            };
            return roles[r.Next(roles.Count)];
        }

        public IFormLinkNullable<ILeveledItemGetter> GetLowRank_Gear()
        {
            Random random = RandomProvider.Random;
            List<uint> gearlist = new List<uint>()
                {
                    0x001BEF71,//LLI_Varuun_Charger [LVLI:001BEF71]
                    0x001BEF75,//LLI_Varuun_AssaultDefaultRole [LVLI:001BEF75]
                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(RetrogradeContext.Current.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public IFormLinkNullable<ILeveledItemGetter> GetHighRank_Gear()
        {
            Random random = RandomProvider.Random;
            List<uint> gearlist = new List<uint>()
                {
                    0x001BEF71,//LLI_Varuun_Charger [LVLI:001BEF71]
                    0x001BEF75,//LLI_Varuun_AssaultDefaultRole [LVLI:001BEF75]
                    0x0025C601,//LLI_Varuun_Heavy_Boss [LVLI:0025C601]
                };
            IFormLinkNullable<ILeveledItemGetter> gear = new FormKey(RetrogradeContext.Current.StarfieldModKey, gearlist[random.Next(gearlist.Count)]).ToNullableLink<ILeveledItemGetter>();
            return gear;
        }

        public IFormLinkNullable<ILeveledItemGetter> GetBoss_Gear()
        {
            Random random = RandomProvider.Random;
            List<uint> gearlist = new List<uint>()
                {
                    0x001BEF75,//LLI_Varuun_AssaultDefaultRole [LVLI:001BEF75]
                    0x0025C601,//LLI_Varuun_Heavy_Boss [LVLI:0025C601]
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
