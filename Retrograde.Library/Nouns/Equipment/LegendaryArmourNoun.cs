using Retrograde.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Parameters;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Retrograde.AI;

namespace Retrograde.Nouns
{
    public class LegendaryArmourNoun
    {
        public IFormLinkNullable<ILeveledItemGetter> LeveledItemGetter { get; set; }

        public LegendaryArmourNoun(string OutlawName)
        {
            var starfieldMod = RetrogradeContext.Current.StarfieldMod;
            var starfieldModKey = RetrogradeContext.Current.StarfieldModKey;
            var targetMod = RetrogradeContext.Current.TargetMod;

            uint armourid = 0;

            Random rand = RandomProvider.Random;
            int type = rand.Next(100);
            string Type = "";
            if (type < 33)
            {
                armourid = ArmourSeedData.GetRandomHelmet();
                Type = "Spacesuit Helmet";
            }
            else if  (type >= 33 && type <= 66)
            {
                armourid = ArmourSeedData.GetRandomPack();
                Type = "Spacesuit Pack";
            }
            else
            {
                armourid = ArmourSeedData.GetRandomSpacesuit();
                Type = "Spacesuit";
            }



            var armour = starfieldMod.Armors[new FormKey(starfieldModKey, armourid)].DeepCopy();
            var legID = Guid.NewGuid().ToString().Substring(0, 8);

            Console.WriteLine("Generating Legendary Name...");
            string Armournameprompt =
                "Generate an evocative legendary name for a piece of " + Type +
                " belonging to the outlaw " + OutlawName + ".\r\n" +
                "The original base item is called \"" + armour.Name + "\".\r\n\r\n" +

                "Guidelines:\r\n" +
                "- The legendary name must feel iconic, mysterious, and feared—something spoken about in frontier bars, black markets, or bounty briefings.\r\n" +
                "- Capture themes that might surround " + OutlawName +
                " such as their reputation, crimes, fighting style, rumours, or symbolic traits.\r\n" +
                "- The name should NOT include the outlaw's actual name.\r\n" +
                "- The name must be 2–4 words total.\r\n" +
                "- Do not include punctuation, numbers, subtitles, or explanations.\r\n" +
                "- Return ONLY the final legendary item name.\r\n";

            
            string ArmourName = AITools.RunPrompt(Armournameprompt);
            //Console.WriteLine(ArmourName);

            //We generate 5 versions of the base, to give high level items
            var baseleveled = new LeveledItem(targetMod)
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
                var newarmour = new Armor(targetMod, "baseleg_" + legID)
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
                    DamageType = starfieldMod.DamageTypes[new FormKey(starfieldModKey, 0x00023190)].ToLink(),
                    Value = armour.Resistances[0].Value + (ushort)((pointsperlevel * i) * EMscaler)
                });

                newarmour.Resistances.Add(new DamageTypeValue()
                {
                    DamageType = starfieldMod.DamageTypes[new FormKey(starfieldModKey, 0x00060A81)].ToLink(),
                    Value = armour.Resistances[1].Value + (ushort)((pointsperlevel * i) * energyscaler)
                });

                targetMod.Armors.Add(newarmour);

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
            targetMod.LeveledItems.Add(baseleveled);
            //New Legendary using list
            //Fetch standard
            var DefaultLegendaryArmor = starfieldMod.LegendaryItems[new FormKey(starfieldModKey, 0x001336C3)].DeepCopy();//DefaultLegendaryArmor [LGDI:001336C3]

            var newleg = new LegendaryItem(targetMod)
            {
                EditorID = "leg_" + legID,
                LegendaryMods = DefaultLegendaryArmor.LegendaryMods,
                IncludeFilters = DefaultLegendaryArmor.IncludeFilters,
                BaseObjectList = baseleveled.ToNullableLink()
            };

            targetMod.LegendaryItems.Add(newleg);

            var leglevel = new LeveledItem(targetMod)
            {
                EditorID = "lvlleg_" + legID,
                ChanceNone = 0,
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

            targetMod.LeveledItems.Add(leglevel);

            LeveledItemGetter = leglevel.ToNullableLink<ILeveledItemGetter>();
        }


    }
}
