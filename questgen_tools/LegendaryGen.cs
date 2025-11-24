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



            var armour = gen_quest._StarfieldMod.Armors[new FormKey(gen_quest.StarfieldModKey, armourid)].DeepCopy();
            var legID = Guid.NewGuid().ToString().Substring(0, 8);

            string Armournameprompt =
                "Reply only with the following information:\r\n\r\n" +
                "A legendary " + Type + " belonging to " + OutlawName + " . \r\n\r\n" +
                "The orginal item this is based on is called the " + armour.Name + ".\r\n\r\n" +
                "Limit it to two to four words and only response with those words";
            string ArmourName = AITools.RunPrompt(Armournameprompt);
            Console.WriteLine(ArmourName);

            //We generate 5 versions of the base, to give high level items
            var baseleveled = new LeveledItem(myModparam)
            {
                EditorID = "lvlstandard_" + legID,
                ChanceNone = 0,
                MaxCount = 1,
                Entries = new ExtendedList<LeveledItemEntry>()
            };

            double armourscaler = rand.NextDouble();
            double energyscaler = rand.NextDouble();
            double EMscaler = rand.NextDouble();

            int pointsperlevel = 50;

            for (int i = 1; i < 5; i++)
            {
                //New Armour
                var newarmour = new Armor(myModparam, "baseleg_" + legID)
                {
                    ObjectBounds = armour.ObjectBounds,
                    Transforms = armour.Transforms,
                    Name = ArmourName,
                    WorldModel = armour.WorldModel,
                    PickupSound = armour.PickupSound,
                    FirstPersonFlags = armour.FirstPersonFlags,
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
                    ObjectEffect = armour.ObjectEffect,
                    Voice = armour.Voice,
                    Value = armour.Value * 2,
                    Weight = armour.Weight,
                    Race = armour.Race,
                };


                //Stat randomiser - Suits have more points to spend
                uint statpoints = 0;
                ushort nextstat = 0;

                newarmour.ArmorRating += (ushort)((pointsperlevel * i)*armourscaler);

                newarmour.Resistances = new ExtendedList<DamageTypeValue>();

                newarmour.Resistances.Add(new DamageTypeValue()
                {
                    DamageType = gen_quest._StarfieldMod.DamageTypes[new FormKey(gen_quest.StarfieldModKey, 0x00023190)].ToLink(),
                    Value = armour.Resistances[0].Value + (ushort)((pointsperlevel * i) * EMscaler)
                });

                newarmour.Resistances.Add(new DamageTypeValue()
                {
                    DamageType = gen_quest._StarfieldMod.DamageTypes[new FormKey(gen_quest.StarfieldModKey, 0x00060A81)].ToLink(),
                    Value = armour.Resistances[1].Value + (ushort)((pointsperlevel * i) * energyscaler)
                });

                myModparam.Armors.Add(newarmour);

                short level = 1;
                if (i > 1)
                {
                    level = (short)(20 * i);
                }

                baseleveled.Entries.Add(new LeveledItemEntry()
                {
                    Count = 1,
                    Reference = newarmour.ToLink<IItemGetter>(),
                    ChanceNone = new Percent(0),
                    Level = level
                });
            }

            
            //Base armour levelled list
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
            var leglevel = new LeveledItem(myModparam)
            {
                EditorID = "lvlleg_" + legID,
                ChanceNone = 0,
                LVLL = new byte[] { 3 },
                MaxCount = 1,
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
                0x00169F59, // Spacesuit_Assault_Backpack_01 "Shocktroop" [ARMO:00169F59]
                0x0003B423, // Spacesuit_Assault_Backpack_01_Cydonia "Cydonia" [ARMO:0003B423]
                0x001C0F35, // SpaceSuit_BountyHunter_01_Backpack_A "Bounty Hunter Stalk" [ARMO:001C0F35]
                0x001C0F34, // SpaceSuit_BountyHunter_01_Backpack_B "Bounty Hunter Seek" [ARMO:001C0F34]
                0x001C0F33, // SpaceSuit_BountyHunter_01_Backpack_C "Bounty Hunter Track" [ARMO:001C0F33]
                0x00166402, // Spacesuit_BountyHunter_02_BackPack "Trackers Alliance" [ARMO:00166402]
                0x001E2B19, // Spacesuit_Constellation_Backpack_01 "Constellation" [ARMO:001E2B19]
                0x00066824, // Spacesuit_CrimsonFleet_Backpack_1 "Pirate Raiding" [ARMO:00066824]
                0x00066825, // Spacesuit_CrimsonFleet_Backpack_2 "Pirate Survival" [ARMO:00066825]
                0x0016D15B, // SpaceSuit_Diver_Backpack_01 "Deepseeker" [ARMO:0016D15B]
                0x00166407, // Spacesuit_Ecliptic_Backpack "Ecliptic" [ARMO:00166407]
                0x00169F51, // Spacesuit_Explorer_Backpack_01 "Explorer" [ARMO:00169F51]
                0x002392B3, // Spacesuit_Groundcrew_Backpack_NoBoostpack "Ground Crew" [ARMO:002392B3]
                0x0001754E, // Spacesuit_Mark1_Backpack "Mark I" [ARMO:0001754E]
                0x0016E0B6, // SpaceSuit_Mercenary_Backpack_01 "Mercenary" [ARMO:0016E0B6]
                0x001D0F95, // Spacesuit_Mercury_Backpack_NoBoostpack "Mercury" [ARMO:001D0F95]
                0x002EDF1F, // Spacesuit_Miner_Backpack "Deep Mining" [ARMO:002EDF1F]
                0x00026BEF, // Spacesuit_Miner_Backpack_Deimos "Deimos" [ARMO:00026BEF]
                0x000FD333, // Spacesuit_Miner_Backpack_Orange "Deepcore" [ARMO:000FD333]
                0x00067C95, // Spacesuit_Navigator_Backpack "Navigator" [ARMO:00067C95]
                0x001E2AF7, // Spacesuit_Ranger_Backpack_01 "Ranger" [ARMO:001E2AF7]
                0x00169F55, // Spacesuit_Recon_Backpack_01 "Deep Recon" [ARMO:00169F55]
                0x0016E0BB, // SpaceSuit_SpaceTrucker_Backpack "Space Trucker" [ARMO:0016E0BB]
                0x00257807, // Spacesuit_UCMarine_Backpack "UC Marine" [ARMO:00257807]
                0x00398105, // Spacesuit_UCMarine_Backpack_SysDef "SysDef" [ARMO:00398105]
                0x000EF9AC, // SpaceSuit_UCMarine_Backpack_UCSEC "UC Security" [ARMO:000EF9AC]
                0x0016640E, // Spacesuit_UCPilot_Backpack_01 "UC Ace Pilot" [ARMO:0016640E]
                0x002AAF43, // Spacesuit_UCPilot_Backpack_SysDef "SysDef Pilot" [ARMO:002AAF43]
                0x003E3D4F, // Spacesuit_UCPilot_Backpack_Vanguard "UC Vanguard Pilot" [ARMO:003E3D4F]
                0x0021A86C, // Spacesuit_UC_ShockArmor_Backpack "UC Shock Armor" [ARMO:0021A86C]
                0x0020612F, // Spacesuit_UC_XenoSpecialist_Backpack "UC AntiXeno" [ARMO:0020612F]
                0x0016D3D0, // SpaceSuit_Varuun_Backpack_01 "Va'ruun" [ARMO:0016D3D0]
            };

            return gearlist[random.Next(gearlist.Count)];
        }

        public static uint GetRandomSpacesuit()
        {
            Random random = new Random();
            List<uint> gearlist = new List<uint>()
            {
                0x002265AD, // Spacesuit_Assault_01 "Shocktroop Spacesuit" [ARMO:002265AD]
                0x0003B422, // Spacesuit_Assault_01Cydonia "Cydonia Spacesuit" [ARMO:0003B422]
                0x00228570, // Spacesuit_BountyHunter_01 "Bounty Hunter Spacesuit" [ARMO:00228570]
                0x00166404, // Spacesuit_BountyHunter_02 "Trackers Alliance Spacesuit" [ARMO:00166404]
                0x001E2B18, // Spacesuit_Constellation_01 "Constellation Spacesuit" [ARMO:001E2B18]
                0x00066821, // Spacesuit_CrimsonFleet_Assault "Pirate Assault Spacesuit" [ARMO:00066821]
                0x00066826, // Spacesuit_CrimsonFleet_Charger "Pirate Charger Spacesuit" [ARMO:00066826]
                0x00066828, // Spacesuit_CrimsonFleet_Officer "Pirate Corsair Spacesuit" [ARMO:00066828]
                0x0006682A, // Spacesuit_CrimsonFleet_Sniper "Pirate Sniper Spacesuit" [ARMO:0006682A]
                0x0016D2C4, // SpaceSuit_Diver_01 "Deepseeker Spacesuit" [ARMO:0016D2C4]
                0x0022856F, // Spacesuit_Ecliptic "Ecliptic Spacesuit" [ARMO:0022856F]
                0x002265AF, // Spacesuit_Explorer_01 "Explorer Spacesuit" [ARMO:002265AF]
                0x002392B5, // Spacesuit_GroundCrew "Ground Crew Spacesuit" [ARMO:002392B5]
                0x0001754D, // Spacesuit_Mark1_Body "Mark I Spacesuit" [ARMO:0001754D]
                0x00226296, // Spacesuit_Mercenary_01 "Mercenary Spacesuit" [ARMO:00226296]
                0x001D0F96, // Spacesuit_Mercury_Body "Mercury Spacesuit" [ARMO:001D0F96]
                0x0005278E, // Spacesuit_Miner "Deep Mining Spacesuit" [ARMO:0005278E]
                0x00026BF1, // Spacesuit_Miner_Deimos "Deimos Spacesuit" [ARMO:00026BF1]
                0x0006AC00, // Spacesuit_Miner_Orange "Deepcore Spacesuit" [ARMO:0006AC00]
                0x00067C94, // Spacesuit_Navigator_Body "Navigator Spacesuit" [ARMO:00067C94]
                0x00227CA0, // Spacesuit_Ranger_01 "Ranger Spacesuit" [ARMO:00227CA0]
                0x002265AE, // Spacesuit_Recon_01 "Deep Recon Spacesuit" [ARMO:002265AE]
                0x0021C780, // Spacesuit_SpaceTrucker "Space Trucker Spacesuit" [ARMO:0021C780]
                0x00004E78, // Spacesuit_SpaceTrucker_Generic "Star Roamer Spacesuit" [ARMO:00004E78]
                0x00257805, // Spacesuit_UCMarine_Body "UC Marine Spacesuit" [ARMO:00257805]
                0x0025780A, // Spacesuit_UCMarine_Body_Armored "UC Wardog Spacesuit" [ARMO:0025780A]
                0x00398104, // Spacesuit_UCMarine_Body_Armored_SysDef "SysDef Assault Spacesuit" [ARMO:00398104]
                0x000EF9AE, // Spacesuit_UCMarine_Body_Armored_UCSEC "UC Sec Starlaw Spacesuit" [ARMO:000EF9AE]
                0x00257809, // Spacesuit_UCMarine_Body_LightArmored "UC Startroop Spacesuit" [ARMO:00257809]
                0x00398108, // Spacesuit_UCMarine_Body_LightArmored_SysDef "SysDef Recon Spacesuit" [ARMO:00398108]
                0x000EF9AF, // Spacesuit_UCMarine_Body_LightArmored_UCSEC "UC Sec Recon Spacesuit" [ARMO:000EF9AF]
                0x00257808, // Spacesuit_UCMarine_Body_Padded "UC Combat Spacesuit" [ARMO:00257808]
                0x0039810A, // Spacesuit_UCMarine_Body_Padded_SysDef "SysDef Combat Spacesuit" [ARMO:0039810A]
                0x000EF9B0, // Spacesuit_UCMarine_Body_Padded_UCSEC "UC Sec Combat Spacesuit" [ARMO:000EF9B0]
                0x00398103, // Spacesuit_UCMarine_Body_SysDef "SysDef Spacesuit" [ARMO:00398103]
                0x000EF9AD, // Spacesuit_UCMarine_Body_UCSEC "UC Security Spacesuit" [ARMO:000EF9AD]
                0x00166410, // Spacesuit_UCPilot_01 "UC Ace Spacesuit" [ARMO:00166410]
                0x002AAF44, // Spacesuit_UCPilot_SysDef "SysDef Ace Spacesuit" [ARMO:002AAF44]
                0x0021A86A, // Spacesuit_UC_ShockArmor "UC Urbanwar Spacesuit" [ARMO:0021A86A]
                0x00206130, // Spacesuit_UC_XenoSpecialist "UC AntiXeno Spacesuit" [ARMO:00206130]
                0x00227CA3, // Spacesuit_Varuun_01 "Va'ruun Spacesuit" [ARMO:00227CA3]
            };
            return gearlist[random.Next(gearlist.Count)];
        }
    }
}
