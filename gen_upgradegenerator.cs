using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData;
using Noggog;
using Mutagen.Bethesda.Plugins.Records;

namespace FrankyCLI
{
    public class BaseUpgrade
    {
        public string WeaponName;
        public string BaseWeaponModID;
        public string BaseConstructableEditorId;
    }

    public class BonusStats
    {
        public string Type;

        public Weapon.Property property;
        public ObjectModProperty.FloatFunctionType floatFunctionType;
        public UInt32 Keyword;
        public string StatName;
    }

    public class gen_upgradegenerator
    {
        public static bool CreateUpgrade(StarfieldMod myMod, BaseUpgrade upgrade, BonusStats stats, float amount)
        {
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                var BaseWeaponModification = env.LinkCache.Resolve(upgrade.BaseWeaponModID);
                var BaseConstructable = env.LinkCache.Resolve(upgrade.BaseConstructableEditorId);
                var originalmod = (WeaponModification)BaseWeaponModification.DeepCopy();
                var originalco = (ConstructibleObject)BaseConstructable.DeepCopy();

                string amountstring = "+" + amount.ToString();
                if (amount < 3)
                {
                    amountstring = "+" + (amount * 100).ToString() + "%";
                }
                /*
                string StatName = "";
                foreach (var item in stats)
                {
                    StatName += item.StatName + "|";
                }
                StatName = StatName.Substring(StatName)
                */
                string editorid = "atwu_" + amountstring + "_" + stats.StatName + "_" + originalmod.EditorID;
                string ingameName = upgrade.WeaponName + " " + originalmod.Name + " (" + amountstring + " " + stats.StatName + ")";
                string omodName = originalmod.Name + " (" + amountstring + " " + stats.StatName + ")";


                //Add Book
                var book = new Book(myMod)
                {
                    EditorID = editorid,
                    ObjectBounds = new ObjectBounds(),
                    Transforms = new Transforms(),
                    Name = ingameName,
                    Model = new Model()
                    {
                        File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>("avontech\\warpblueprint.nif"),
                    },
                    Description = "A blueprint for a Avontech Warptech " + upgrade.WeaponName + " mod.\n" + originalmod.Name + " with (" + amountstring + " " + stats.StatName + ").\nApply at Weapon Workbench.",
                    Value = 1000,
                    Weight = 0.1f
                };
                Console.WriteLine("Book ID:" + book.FormKey.ToString() );
                myMod.Books.Add(book);
                //Add OMOD
                var omod = new WeaponModification(myMod)
                {
                    EditorID = editorid,
                    Name = omodName,
                    Description = originalmod.Description + amountstring + " " + stats.StatName,
                    Model = originalmod.Model,
                    TargetOmodKeywords = originalmod.TargetOmodKeywords,
                    FilterKeywords = originalmod.FilterKeywords,
                    AttachPoint = originalmod.AttachPoint,
                    AttachParentSlots = originalmod.AttachParentSlots,
                    Includes = originalmod.Includes,
                    Properties = originalmod.Properties,
                };
                if (stats.Type == "Int")
                {
                    omod.Properties.Add(new ObjectModIntProperty<Weapon.Property>
                    {
                        Property = stats.property,
                        Value = (uint)amount.ToInt(),
                        FunctionType = stats.floatFunctionType,
                    });
                }
                if (stats.Type == "Float")
                {
                    omod.Properties.Add(new ObjectModFloatProperty<Weapon.Property>
                    {
                        Property = stats.property,
                        Value = amount,
                        FunctionType = stats.floatFunctionType,
                    });
                }
                if (stats.Type == "KeywordFloat")
                {
                    IFormLinkNullable<IStarfieldMajorRecordGetter> statkeyword = new FormKey(env.LoadOrder[0].ModKey, stats.Keyword).ToNullableLink<IStarfieldMajorRecordGetter>();
                    omod.Properties.Add(new ObjectModFormLinkFloatProperty<Weapon.Property>
                    {
                        Property = stats.property,
                        Record = statkeyword,
                        Value = amount,                        
                        FunctionType = stats.floatFunctionType,                        
                    });
                }

                myMod.ObjectModifications.Add(omod);
                //Add Construct
                IFormLinkNullable<IConstructibleObjectTargetGetter> targetmod = omod.FormKey.ToNullableLink<IConstructibleObjectTargetGetter>();
                var co = new ConstructibleObject(myMod)
                {
                    EditorID = editorid,
                    Description = ingameName,
                    CreatedObject = targetmod,
                    WorkbenchKeyword = originalco.WorkbenchKeyword,
                    AmountProduced = originalco.AmountProduced,
                    LearnMethod = originalco.LearnMethod,
                    Categories = originalco.Categories                    
                };
                co.ConstructableComponents = originalco.ConstructableComponents;
                var con = new GetItemCountConditionData()
                {
                    RunOnType = Condition.RunOnType.Subject,
                };
                con.FirstParameter = new FormLinkOrIndex<IPlaceableObjectGetter>(con, book.FormKey);
                co.Conditions.Add(new ConditionFloat()
                {
                    Data = con,
                    CompareOperator = CompareOperator.GreaterThan,
                    ComparisonValue = 0
                });
                myMod.ConstructibleObjects.Add(co);
            }
            return true;
        }

        public static int Generate(string[] args)
        {
            Random random = new Random();
            StarfieldMod myMod;
            string modname = args[0];
            string mode = args[1];
            string prefix = args[2];
            string item = args[3];
            string form = args[4];

            string datapath = "";
            BuildUpgradeLib();
            BuildStatLib();
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                var immutableLoadOrderLinkCache = env.LoadOrder.ToImmutableLinkCache();
                datapath = env.DataFolderPath;
                //Find the modkey 
                ModKey newMod = new ModKey(modname, ModType.Master);
                myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);
                if (!env.LoadOrder.ModExists(newMod))
                {
                    myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);
                }
                else
                {
                    for (int i = 0; i < env.LoadOrder.Count; i++)
                    {
                        if (env.LoadOrder[i].FileName == modname + ".esm")
                        {
                            ModPath modPath = Path.Combine(env.DataFolderPath, env.LoadOrder[i].FileName);
                            myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield);

                        }
                    }
                }
                CreateUpgrade(myMod, UpgradeLib["mod_Beowulf_Barrel_Short"], StatLib["AmmoCapacityMultAndAdd"], 0.5f);
            }
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm");
            Console.WriteLine("Finished");
            return 0;
        }

        public static Dictionary<string,BaseUpgrade> UpgradeLib = new Dictionary<string, BaseUpgrade>();

        public static void BuildUpgradeLib()
        {
            UpgradeLib.Add("mod_Beowulf_Barrel_Short", new BaseUpgrade()
            {
                BaseWeaponModID = "mod_Beowulf_Barrel_Short",
                BaseConstructableEditorId = "co_gun_mod_Beowulf_Barrel_Short",
                WeaponName = "Beowulf"
            });
        }

        public static Dictionary<string, BonusStats> StatLib = new Dictionary<string, BonusStats>();

        public static void BuildStatLib()
        {
            StatLib.Add("EMFlat", new BonusStats()
            {
                Type = "KeywordFloat",
                Keyword = 0x00023190,
                StatName = "EM Damage",
                property = Weapon.Property.DamageTypeValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add
            });
            StatLib.Add("EnergyFlat", new BonusStats()
            {
                Type = "KeywordFloat",
                Keyword = 0x00060A81,
                StatName = "Energy Damage",
                property = Weapon.Property.DamageTypeValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add
            });
            StatLib.Add("ToxicFlat", new BonusStats()
            {
                Type = "KeywordFloat",
                Keyword = 0x00000B79,
                StatName = "Toxic Damage",
                property = Weapon.Property.DamageTypeValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add
            });
            StatLib.Add("PhysicalMultAndAdd", new BonusStats()
            {
                Type = "Float",
                StatName = "Physical Damage",
                property = Weapon.Property.DamagePhysical,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd
            });
            StatLib.Add("AmmoCapacityAdd", new BonusStats()
            {
                Type = "Int",
                StatName = "Ammo Capacity",
                property = Weapon.Property.AmmoCapacity,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add
            });
            StatLib.Add("AmmoCapacityMultAndAdd", new BonusStats()
            {
                Type = "Float",
                StatName = "Ammo Capacity",
                property = Weapon.Property.AmmoCapacity,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd
            });
        }
    }
}