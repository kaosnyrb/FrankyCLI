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
        public bool Percentage;
        public Weapon.Property property;
        public ObjectModProperty.FloatFunctionType floatFunctionType;
        public UInt32 Keyword;
        public string StatName;

        public float Default;
        public float Step;
    }

    public class gen_upgradegenerator
    {
        public static List<string> MissingCOs = new List<string>();

        public static bool CreateUpgrade(StarfieldMod myMod, BaseUpgrade upgrade, BonusStats stats, float amount, string LevelledListContains)
        {
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                WeaponModification originalmod = null;
                ConstructibleObject originalco = null;
                try
                {
                    foreach (var obj in env.LoadOrder[0].Mod.ObjectModifications)
                    {
                        if (obj.EditorID.ToLower() == upgrade.BaseWeaponModID.ToLower())
                        {
                            originalmod = (WeaponModification)obj.DeepCopy();
                        }
                    }
                    foreach(var allco in env.LoadOrder[0].Mod.ConstructibleObjects)
                    {
                        if (allco.EditorID.ToLower() == upgrade.BaseConstructableEditorId.ToLower())
                        {
                            originalco = (ConstructibleObject)allco.DeepCopy();
                        }
                    }
                    if (originalmod == null) {
                        Console.WriteLine("Missing OM: " + upgrade.BaseWeaponModID);
                        return false;
                    }
                    if (originalco == null)
                    {
                        Console.WriteLine("Missing CO: " + upgrade.BaseConstructableEditorId);
                        MissingCOs.Add(upgrade.BaseConstructableEditorId);
                        return false;
                    }
                    /*
                    var BaseWeaponModification = env.LinkCache.Resolve(upgrade.BaseWeaponModID);
                    var BaseConstructable = env.LinkCache.Resolve(upgrade.BaseConstructableEditorId);
                    originalmod = (WeaponModification)BaseWeaponModification.DeepCopy();
                    originalco = (ConstructibleObject)BaseConstructable.DeepCopy();*/
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to file: " + upgrade.BaseConstructableEditorId);
                    return false;
                }

                string amountstring = "";
                if (amount > 0) { amountstring += "+"; }

                if (stats.Percentage)
                {
                    amountstring += (amount * 100).ToString() + "%";
                }
                else
                {                    
                    amountstring += amount.ToString();
                }
                if (stats.Type == "Enum") { amountstring = ""; }
                
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
                if (stats.Type == "Enum")
                {
                    omod.Properties.Add(new ObjectModEnumProperty<Weapon.Property>
                    {
                        Property = stats.property,
                        EnumIntValue = (uint)amount.ToInt(),
                        FunctionType = ObjectModProperty.EnumFunctionType.Set,
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
                if (stats.Type == "AddFormInt")
                {
                    IFormLinkNullable<IStarfieldMajorRecordGetter> statkeyword = new FormKey(env.LoadOrder[0].ModKey, stats.Keyword).ToNullableLink<IStarfieldMajorRecordGetter>();
                    omod.Properties.Add(new ObjectModFormLinkIntProperty<Weapon.Property>
                    {
                        Property = stats.property,
                        Record = statkeyword,
                        Value = (uint)amount.ToInt(),
                        FunctionType = ObjectModProperty.FormLinkFunctionType.Add,
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

                //Add Book to LevelledList
                foreach( var lvl in myMod.LeveledItems)
                {
                    if (lvl.EditorID.Contains(LevelledListContains))
                    {
                        lvl.Entries.Add(new LeveledItemEntry()
                        {
                            Count = 1,
                            ChanceNone = Percent.Zero,
                            Level = 1,
                            Reference = book.ToLink<IItemGetter>()
                        });
                    }
                }
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
            //BuildUpgradeLib();
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

                
                /*
                List<string> Weapons = new List<string>() { "AA99", "ArcWelder","AutoRivet", "Beowulf", "BigBang", 
                "Breach", "Bridger","Coachman", "Drumbeat","DrumBeat","Eon","Equinox","Grendel","HardTarget",
                "InflictorPistol","InflictorRifle","Kodama","Kraken","Lawgiver","M1919","Maelstrom","MagPulse","MagShear","MagShot",
                "MagSniper","Magstorm","Microgun","Novablast","Novalight","Orion","Pacifier","PumpShotgun","Rattler","Razorback","Regulator",
                "Rocketlauncher","RussianAssaultRifle","RussianHuntingRifle","Shotty","Sidestar","Solstice","Stinger","Tombstone","UrbanEagle",
                "XM2311"};*/

                List<string> Weapons = new List<string>() { "AA99"};

                Dictionary<string, string> comap = new Dictionary<string, string>
                {
                    { "co_gun_mod_AA99_Grip_StandardStock", "co_gun_mod_AA99_Grip_Standard" },
                    { "co_gun_mod_AA99_Mag_S", "co_gun_mod_AA99_Mag_Standard" },
                    { "co_gun_mod_AA99_Grip_StabilizingStock", "co_gun_mod_AA99_Grip_Stabilizing" },
                    { "co_gun_mod_AA99_Mag_L", "co_gun_mod_AA99_Mag_Large" },
                };

                foreach (var weapon in Weapons) {
                    foreach (var objmod in env.LoadOrder[0].Mod.ObjectModifications)
                    {
                        if (objmod.EditorID.Contains(weapon))
                        {
                            if (!objmod.EditorID.Contains("Quality") &&
                                !objmod.EditorID.Contains("None") &&
                                !objmod.EditorID.Contains("Modgroup"))
                            {
                                string coid = "co_gun_" + objmod.EditorID;
                                if (comap.ContainsKey(coid))
                                {
                                    coid = comap[coid];
                                }
                                UpgradeLib.Add(objmod.EditorID, new BaseUpgrade()
                                {
                                    BaseWeaponModID = objmod.EditorID,
                                    BaseConstructableEditorId = coid,
                                    WeaponName = weapon
                                });
                            }
                            else 
                            {
                                Console.WriteLine("Ignoring:" + objmod.EditorID);
                            }
                        }
                    }
                }
                foreach(var upgrade in UpgradeLib)
                {
                    foreach (var stat in StatLib)
                    {
                        for(int i = 0;i < 5; i++)
                        {
                            float amount = StatLib[stat.Key].Default + (i * StatLib[stat.Key].Step);
                            Console.WriteLine("Creating " + upgrade.Key + " " + stat.Key + " " + amount);
                            CreateUpgrade(myMod, UpgradeLib[upgrade.Key], StatLib[stat.Key], amount, "Mods");
                        }
                    }
                }
                //CreateUpgrade(myMod, UpgradeLib["mod_Solstice_Grip_Tactical"], StatLib["StealthMovementDetectionAdd"], -1000, "Beowulf");
            }
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm");
            Console.WriteLine("Finished");
            foreach(var miss in MissingCOs)
            {
                Console.WriteLine(miss);
            }
            
            return 0;
        }

        public static Dictionary<string,BaseUpgrade> UpgradeLib = new Dictionary<string, BaseUpgrade>();
        public static Dictionary<string, BonusStats> StatLib = new Dictionary<string, BonusStats>();

        public static void BuildStatLib()
        {
            StatLib.Add("EMFlat", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = false,
                Keyword = 0x00023190,
                StatName = "EM Damage",
                property = Weapon.Property.DamageTypeValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 10,
                Step = 5
            });
            StatLib.Add("EnergyFlat", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = false,
                Keyword = 0x00060A81,
                StatName = "Energy Damage",
                property = Weapon.Property.DamageTypeValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 10,
                Step = 5
            });
            /*
            StatLib.Add("EnergyMultAndAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                Keyword = 0x00060A81,
                StatName = "Energy Damage",
                property = Weapon.Property.DamageTypeValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = 0.1f,
                Step = 0.1f
            });*/
            StatLib.Add("ToxicFlat", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = false,
                Keyword = 0x00000B79,
                StatName = "Toxic Damage",
                property = Weapon.Property.DamageTypeValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 10,
                Step = 5
            });
            StatLib.Add("PhysicalMultAndAdd", new BonusStats()
            {
                Type = "Float",
                Percentage = true,
                StatName = "Physical Damage",
                property = Weapon.Property.DamagePhysical,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = 0.1f,
                Step = 0.1f
            });
            StatLib.Add("PhysicalAdd", new BonusStats()
            {
                Type = "Float",
                Percentage = false,
                StatName = "Physical Damage",
                property = Weapon.Property.DamagePhysical,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 10,
                Step = 5
            });
            StatLib.Add("AmmoCapacityAdd", new BonusStats()
            {
                Type = "Int",
                Percentage = false,
                StatName = "Ammo Capacity",
                property = Weapon.Property.AmmoCapacity,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 6,
                Step = 6
            });
            StatLib.Add("ProjectileAdd", new BonusStats()
            {
                Type = "Int",
                Percentage = false,
                StatName = "Projectiles",
                property = Weapon.Property.ProjectileCount,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 1,
                Step = 1
            });
            StatLib.Add("AmmoCapacityMultAndAdd", new BonusStats()
            {
                Type = "Float",
                Percentage = true,
                StatName = "Ammo Capacity",
                property = Weapon.Property.AmmoCapacity,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = 0.1f,
                Step = 0.1f
            });
            StatLib.Add("StabilityMultAndAdd", new BonusStats()
            {
                Type = "Float",
                Percentage = true,
                StatName = "Spread",
                property = Weapon.Property.AimModelConeMaxDegrees,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = -0.5f,
                Step = -0.05f
            });
            StatLib.Add("CritDamageMultAndAdd", new BonusStats()
            {
                Type = "Float",
                Percentage = true,
                StatName = "Crit Damage",
                property = Weapon.Property.CriticalDamageMultiplier,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = 0.5f,
                Step = 0.10f
            });
            StatLib.Add("BashDamageMultAndAdd", new BonusStats()
            {
                Type = "Float",
                Percentage = true,
                StatName = "Bash Damage",
                property = Weapon.Property.BashDamage,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = 1.0f,
                Step = 0.50f
            });
            StatLib.Add("SilentSet", new BonusStats()
            {
                Type = "Enum",
                Percentage = false,
                StatName = "Silencer",
                property = Weapon.Property.SoundLevel,
                Default = 2,
                Step = 0
            });
            StatLib.Add("02MultAndAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "02",
                property = Weapon.Property.ActorValue,
                Keyword = 0x0022F93D,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = -0.35f,
                Step = -0.05f
            });
            StatLib.Add("BonusXPAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Bonus XP",
                property = Weapon.Property.ActorValue,
                Keyword = 0x002D873C,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 0.10f,
                Step = 0.05f
            });
            StatLib.Add("ReloadSpeedAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Reload Speed",
                property = Weapon.Property.ActorValue,
                Keyword = 0x002D87C4,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 0.20f,
                Step = 0.05f
            });
            StatLib.Add("DamageReductionAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Damage Taken",
                property = Weapon.Property.ActorValue,
                Keyword = 0x0030397A,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = -0.10f,
                Step = -0.05f
            });
            StatLib.Add("HealRateAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = false,
                StatName = "Health Regen",
                property = Weapon.Property.ActorValue,
                Keyword = 0x000002D7,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 0.10f,
                Step = 0.05f
            });
            StatLib.Add("CarryWeightAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = false,
                StatName = "Carry Weight",
                property = Weapon.Property.ActorValue,
                Keyword = 0x000002DC,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 50,
                Step = 25
            });
            StatLib.Add("JumpAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Jump Strength",
                property = Weapon.Property.ActorValue,
                Keyword = 0x00040CDC,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 0.10f,
                Step = 0.05f
            });
            StatLib.Add("MovementAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Movement Speed",
                property = Weapon.Property.ActorValue,
                Keyword = 0x000002DA,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 0.10f,
                Step = 0.05f
            });
            StatLib.Add("StealthLightDetectionAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                Keyword = 0x002EC4F5,
                StatName = "Stealth Light Detection",
                property = Weapon.Property.ActorValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = -0.3f,
                Step = -0.1f
            });
            StatLib.Add("StealthMovementDetectionAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                Keyword = 0x002EC4ED,
                StatName = "Stealth Movement Detection",
                property = Weapon.Property.ActorValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = -0.3f,
                Step = -0.1f
            });
        }
    }
}