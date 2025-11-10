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
                MaxCount = 1,
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
                0x00052792,//Spacesuit_Miner_Helmet "Deep Mining Space Helmet" [ARMO:00052792]
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
