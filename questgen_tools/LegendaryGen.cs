using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public class LegendaryGen
    {
        public static IFormLinkNullable<ILeveledItemGetter> GenerateLegendaryArmour(StarfieldMod myModparam,string OutlawName)
        {
            uint armourid = 0;

            Random rand = new Random();
            int type = rand.Next(100);
            string Type = "";
            if (type < 33)
            {
                armourid = GetRandomHelmet();
                Type = "Spacesuit Helmet";
            }
            else if  (type >= 33 && type <= 66)
            {
                armourid = GetRandomPack();
                Type = "Spacesuit Pack";
            }
            else
            {
                armourid = GetRandomSpacesuit();
                Type = "Spacesuit";
            }
            string Armournameprompt = AITools.GetBackgroundPrompt() +
                "Reply only with the following information:\r\n\r\n" +
                "A legendary " + Type +" belonging to "+ OutlawName  + " . \r\n\r\n" +
                "Limit it to three words and only response with those three words";
            string ArmourName = AITools.RunPrompt(Armournameprompt);
            Console.WriteLine(ArmourName); 

            var armour = gen_quest._StarfieldMod.Armors[new FormKey(gen_quest.StarfieldModKey, armourid)].DeepCopy();
            var legID = Guid.NewGuid().ToString().Substring(0, 8);

            //New Armour
            var newarmour = new Armor(myModparam, "baseleg_" + legID)
            {
                ObjectBounds = armour.ObjectBounds,
                Transforms = armour.Transforms,
                Name = ArmourName,
                WorldModel = armour.WorldModel,
                PickupSound = armour.PickupSound,
                FirstPersonFlags=armour.FirstPersonFlags,
                ArmorRating = armour.ArmorRating,
                Armatures = armour.Armatures,
                Components = armour.Components,
                Description = armour.Description,
                Health = armour.Health,
                ObjectTemplates = armour.ObjectTemplates,
                AttachParentSlots = armour.AttachParentSlots,
                Footstep = armour.Footstep,
                DropdownSound = armour.DropdownSound,
                //InstanceNaming = armour.InstanceNaming,
                Keywords = armour.Keywords,
                Resistances = armour.Resistances,
                ObjectEffect =  armour.ObjectEffect,
                Voice = armour.Voice,
                Value = armour.Value * 2,
                Weight = armour.Weight,
                Race = armour.Race,
            };
            myModparam.Armors.Add(newarmour);
            //Base armour levelled list

            var baseleveled = new LeveledItem(myModparam)
            {
                EditorID = "lvlstandard_" + legID,
                ChanceNone = 0,
                Entries = new ExtendedList<LeveledItemEntry>()
                {
                    new LeveledItemEntry()
                    {
                        Count = 1,
                        Reference = newarmour.ToLink<IItemGetter>(),
                        ChanceNone = new Percent(0),
                        Level = 1
                    }
                }
            };
            myModparam.LeveledItems.Add(baseleveled);
            //New Legendary using list
            //Fetch standard
            var DefaultLegendaryArmor = gen_quest._StarfieldMod.LegendaryItems[new FormKey(gen_quest.StarfieldModKey, 0x001336C3)].DeepCopy();//DefaultLegendaryArmor [LGDI:001336C3]

            var newleg = new LegendaryItem(myModparam)
            {
                EditorID = "leg_" + legID,
                LegendaryMods = DefaultLegendaryArmor.LegendaryMods,
                ApplicableItemList = baseleveled.ToNullableLink<ILeveledItemGetter>(),
                IncludeFilters = DefaultLegendaryArmor.IncludeFilters,
            };
            myModparam.LegendaryItems.Add(newleg);
            //New levelled list with legendary
            //if_tmp_Armor_Quality_02_Restricted [KYWD:0011E2BF]
            var if_tmp_Armor_Quality_02_Restricted = gen_quest._StarfieldMod.Keywords[new FormKey(gen_quest.StarfieldModKey, 0x0011E2BF)];//if_tmp_Armor_Quality_02_Restricted [KYWD:0011E2BF]

            //Hmm do I want levelled stuff? probs
            var leglevel = new LeveledItem(myModparam)
            {
                EditorID = "lvlleg_" + legID,
                ChanceNone = 0,
                LVLL = new byte[] { 3 },
                MaxCount = 0,
                FilterKeywordChances = new ExtendedList<FilterKeywordChance>()
                {
                    new FilterKeywordChance(){
                        Keyword = if_tmp_Armor_Quality_02_Restricted.ToLink<IKeywordGetter>(),
                        Chance = Percent.One,
                    },
                },
                Entries = new ExtendedList<LeveledItemEntry>()
                {
                    new LeveledItemEntry()
                    {
                        Count = 1,
                        Reference = newleg.ToLink<IItemGetter>(),
                        ChanceNone = new Percent(0),
                        Level = 1
                    }
                }
            };
            myModparam.LeveledItems.Add(leglevel);

            return leglevel.ToNullableLink<ILeveledItemGetter>();
        }

        public static uint GetRandomHelmet()
        {
            Random random = new Random();
            List<uint> gearlist = new List<uint>()
            {
                0x00169F58,//Spacesuit_Assault_Helmet_01 "Shocktroop Space Helmet" [ARMO:00169F58]
                0x0003B424,//Spacesuit_Assault_Helmet_01_Cydonia "Cydonia Space Helmet" [ARMO:0003B424]
                0x001C0F32,//SpaceSuit_BountyHunter_01_Helmet "Bounty Hunter Space Helmet" [ARMO:001C0F32]
                0x00166403,//SpaceSuit_BountyHunter_02_Helmet "Trackers Alliance Space Helmet" [ARMO:00166403]
                0x001E2B17,//Spacesuit_Constellation_Helmet_01 "Constellation Space Helmet" [ARMO:001E2B17]
                0x00066822,//Spacesuit_CrimsonFleet_Assault_Helmet "Pirate Assault Space Helmet" [ARMO:00066822]
                0x00066827,//Spacesuit_CrimsonFleet_Charger_Helmet "Pirate Charger Space Helmet" [ARMO:00066827]
                0x00066829,//Spacesuit_CrimsonFleet_Officer_Helmet "Pirate Corsair Space Helmet" [ARMO:00066829]
                0x0006682B,//Spacesuit_CrimsonFleet_Sniper_Helmet "Pirate Sniper Space Helmet" [ARMO:0006682B]
                0x0016D15C,//SpaceSuit_Diver_Helmet_01 "Deepseeker Space Helmet" [ARMO:0016D15C]
                0x00228829,//Spacesuit_Ecliptic_Helmet "Ecliptic Space Helmet" [ARMO:00228829]
                0x00169F50,//Spacesuit_Explorer_Helmet_01 "Explorer Space Helmet" [ARMO:00169F50]
                0x002392B4,//Spacesuit_Groundcrew_Helmet "Ground Crew Space Helmet" [ARMO:002392B4]
                0x0001754F,//Spacesuit_Mark1_Helmet "Mark I Space Helmet" [ARMO:0001754F]
                0x0016E0B5,//SpaceSuit_Mercenary_Helmet_01 "Mercenary Space Helmet" [ARMO:0016E0B5]
                0x001D0F94,//Spacesuit_Mercury_Helmet "Mercury Space Helmet" [ARMO:001D0F94]
                0x00052792,//Spacesuit_Miner_Helmet "Deep Mining Space Helmet" [ARMO:00052792]
                0x00026BF0,//Spacesuit_Miner_Helmet_Deimos "Deimos Space Helmet" [ARMO:00026BF0]
                0x0006ABFF,//Spacesuit_Miner_Helmet_Orange "Deepcore Space Helmet" [ARMO:0006ABFF]
                0x00067C93,//Spacesuit_Navigator_Helmet "Navigator Space Helmet" [ARMO:00067C93]
                0x001E2AC1,//Spacesuit_Ranger_Helmet_01 "Ranger Space Helmet" [ARMO:001E2AC1]
                0x00169F54,//Spacesuit_Recon_Helmet_01 "Deep Recon Space Helmet" [ARMO:00169F54]
                0x00003E8F,//SpaceSuit_SpaceTrucker_Generic_Helmet "Star Roamer Space Helmet" [ARMO:00003E8F]
                0x0016E0BD,//SpaceSuit_SpaceTrucker_Helmet "Space Trucker Space Helmet" [ARMO:0016E0BD]
                0x00257806,//Spacesuit_UCMarine_Helmet "UC Marine Space Helmet" [ARMO:00257806]
                0x0025780B,//Spacesuit_UCMarine_Helmet_Armored "UC Armored Space Helmet" [ARMO:0025780B]
                0x00398107,//Spacesuit_UCMarine_Helmet_Armored_SysDef "SysDef Armored Space Helmet" [ARMO:00398107]
                0x000EF9B2,//Spacesuit_UCMarine_Helmet_Armored_UCSEC "UC Sec Spaceriot Helmet" [ARMO:000EF9B2]
                0x00398106,//Spacesuit_UCMarine_Helmet_SysDef "SysDef Space Helmet" [ARMO:00398106]
                0x000EF9B1,//SpaceSuit_UCMarine_Helmet_UCSEC "UC Security Space Helmet" [ARMO:000EF9B1]
                0x0016640F,//Spacesuit_UCPilot_Helmet_01 "UC Ace Pilot Space Helmet" [ARMO:0016640F]
                0x002AAF45,//Spacesuit_UCPilot_Helmet_SysDef "SysDef Ace Space Helmet" [ARMO:002AAF45]
                0x00248C0E,//Spacesuit_UCVanguard_Helmet_Armored "UC Vanguard Space Helmet" [ARMO:00248C0E]
                0x0021A86B,//Spacesuit_UC_ShockArmor_Helmet "UC Urbanwar Space Helmet" [ARMO:0021A86B]
                0x0020612E,//Spacesuit_UC_XenoSpecialist_Helmet "UC AntiXeno Space Helmet" [ARMO:0020612E]
                0x0016D3D1,//SpaceSuit_Varuun_Helmet_01 "Va'ruun Space Helmet" [ARMO:0016D3D1]
            };


            return gearlist[random.Next(gearlist.Count)];
        }
        public static uint GetRandomPack()
        {
            Random random = new Random();
            List<uint> gearlist = new List<uint>()
            {
                0x00257807,//Spacesuit_UCMarine_Backpack "UC Marine" [ARMO:00257807]
            };


            return gearlist[random.Next(gearlist.Count)];
        }

        public static uint GetRandomSpacesuit()
        {
            Random random = new Random();
            List<uint> gearlist = new List<uint>()
            {
                0x0025780A,//SSpacesuit_UCMarine_Body_Armored "UC Wardog Spacesuit" [ARMO:0025780A]
            };


            return gearlist[random.Next(gearlist.Count)];
        }
    }
}
