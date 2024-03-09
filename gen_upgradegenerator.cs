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

        public decimal Default;
        public decimal Step;

        public int StepCount = 1;
    }

    public class gen_upgradegenerator
    {

        public static List<string> MissingCOs = new List<string>();

        public static Dictionary<string, WeaponModification> modcache = new Dictionary<string, WeaponModification>();
        public static Dictionary<string, ConstructibleObject> cocache = new Dictionary<string, ConstructibleObject>();

        public static bool CreateUpgrade(StarfieldMod myMod, BaseUpgrade upgrade, BonusStats stats, decimal amount, string LevelledListContains)
        {
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                //Find the weapon mod and recipe to copy
                WeaponModification originalmod = null;
                ConstructibleObject originalco = null;                
                try
                {
                    if (modcache.ContainsKey(upgrade.BaseWeaponModID.ToLower()))
                    {
                        originalmod = modcache[upgrade.BaseWeaponModID.ToLower()].DeepCopy();
                    }
                    else
                    {
                        foreach (var obj in env.LoadOrder[0].Mod.ObjectModifications)
                        {
                            if (obj.EditorID.ToLower() == upgrade.BaseWeaponModID.ToLower())
                            {
                                modcache.Add(upgrade.BaseWeaponModID.ToLower(), (WeaponModification)obj.DeepCopy());
                                originalmod = (WeaponModification)obj.DeepCopy();
                                break;
                            }
                        }
                    }
                    if (cocache.ContainsKey(upgrade.BaseConstructableEditorId.ToLower()))
                    {
                        originalco = cocache[upgrade.BaseConstructableEditorId.ToLower()].DeepCopy();
                    }
                    else
                    {
                        foreach (var allco in env.LoadOrder[0].Mod.ConstructibleObjects)
                        {
                            if (allco.EditorID.ToLower() == upgrade.BaseConstructableEditorId.ToLower())
                            {
                                cocache.Add(upgrade.BaseConstructableEditorId.ToLower(), allco.DeepCopy());
                                originalco = allco.DeepCopy();
                                break;
                            }
                        }
                    }
                    if (originalmod == null) {
                        Console.WriteLine("Missing OM: " + upgrade.BaseWeaponModID);
                        return false;
                    }
                    if (originalco == null)
                    {
                        Console.WriteLine("Missing CO: " + upgrade.BaseConstructableEditorId);
                        if (!MissingCOs.Contains(upgrade.BaseConstructableEditorId))
                        {
                            MissingCOs.Add(upgrade.BaseConstructableEditorId);
                        }
                        return false;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to file: " + upgrade.BaseConstructableEditorId);
                    return false;
                }

                //Figure out the text
                string amountstring = "";
                if (amount > 0) { amountstring += "+"; }

                if (stats.Percentage)
                {
                    var str = (amount * 100).ToString();                    
                    amountstring += str + "%";
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
                    Description = "Blueprint for a Avontech " + upgrade.WeaponName + " mod.\n\n" + originalmod.Name + " with (" + amountstring + " " + stats.StatName + ").\n\nApply at Weapon Workbench.",
                    Value = 1000,
                    Weight = 0
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

                //Remove the DontShowInUI [KYWD:00374EFA]
                for(int i = 0; i < omod.Properties.Count; i++)
                {
                    try
                    {
                        if (((ObjectModFormLinkIntProperty<Weapon.Property>)omod.Properties[i]).Record.FormKey.ID == 0x00374EFA)
                        {
                            omod.Properties.RemoveAt(i);
                            break;
                        }
                    }
                    catch { }
                }

                if (stats.Type == "Int")
                {
                    omod.Properties.Add(new ObjectModIntProperty<Weapon.Property>
                    {
                        Property = stats.property,
                        Value = (uint)amount,
                        FunctionType = stats.floatFunctionType,
                    });
                }
                if (stats.Type == "Float")
                {
                    omod.Properties.Add(new ObjectModFloatProperty<Weapon.Property>
                    {
                        Property = stats.property,
                        Value = (float)amount,
                        FunctionType = stats.floatFunctionType,
                    });
                }
                if (stats.Type == "Enum")
                {
                    omod.Properties.Add(new ObjectModEnumProperty<Weapon.Property>
                    {
                        Property = stats.property,
                        EnumIntValue = (uint)amount,
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
                        Value = (float)amount,                        
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
                        Value = (uint)amount,
                        FunctionType = ObjectModProperty.FormLinkFunctionType.Add,
                    });
                }
                myMod.ObjectModifications.Add(omod);
                AddOModToUpgradeInclude(LevelledListContains, omod);
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

        public static Dictionary<string, WeaponModification> upgrademodlist = new Dictionary<string, WeaponModification>();
        public static List<string> rtfpsettings = new List<string>();
        public static void AddOModToUpgradeInclude(string upgradelist, WeaponModification weaponModification)
        {            
            if (upgrademodlist.ContainsKey(upgradelist))
            {
                //Starfield.esm~0028E02A|incl_add(AvontechWeaponUpgrades.esm~0010CB:2:1:1)
                rtfpsettings.Add("Starfield.esm~0028E02A|incl_add(AvontechWeaponUpgrades.esm~" + weaponModification.FormKey.ID.ToString("X") + ":2:1:1)");
                /*
                upgrademodlist[upgradelist].Includes.Add(new ObjectModInclude()
                {
                    DoNotUseAll = true,
                    Optional = true,
                    MinimumLevel = 0,
                    Mod = weaponModification.ToLink<IAObjectModificationGetter>()
                });*/
            }
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
            
            DamageMode = int.Parse(prefix);
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

                List<string> Weapons = new List<string>();
                if (DamageMode == 0)//Energy
                {
                    Weapons = new List<string>() { "ArcWelder", "BigBang", "Equinox", "InflictorPistol", "InflictorRifle", "Novalight", "Orion", "Solstice" };

                }
                if (DamageMode == 1)//EM
                {
                    Weapons = new List<string>() { "Novablast", };
                }

                if(DamageMode == 2)//Phys
                {
                    Weapons = new List<string>() { "AA99", "AutoRivet", "Beowulf",
                    "Breach", "Bridger","Coachman", "DrumBeat","Eon","Grendel","HardTarget",
                    "Kodama","Kraken","Lawgiver","M1919","Maelstrom","MagPulse","MagShear","MagShot",
                    "MagSniper","Magstorm","Microgun","Pacifier","PumpShotgun","Rattler","Razorback","Regulator",
                    "Rocketlauncher","RussianAssaultRifle","RussianHuntingRifle","Shotty","Sidestar","Tombstone","UrbanEagle",
                    "XM2311"};
                }

                //Some contstructable objects don't follow the name format so we manually map them
                Dictionary<string, string> comap = new Dictionary<string, string>
                {
                    { "co_gun_mod_AA99_Grip_StandardStock", "co_gun_mod_AA99_Grip_Standard" },
                    { "co_gun_mod_AA99_Mag_S", "co_gun_mod_AA99_Mag_Standard" },
                    { "co_gun_mod_AA99_Grip_StabilizingStock", "co_gun_mod_AA99_Grip_Stabilizing" },
                    { "co_gun_mod_AA99_Mag_L", "co_gun_mod_AA99_Mag_Large" },
                    { "co_gun_mod_Coachman_Grip_StandardStock", "co_gun_mod_Coachman_Grip_Standard" },
                    { "co_gun_mod_Eon_Mag_L", "co_gun_mod_Eon_Mag_Large" },
                    { "co_gun_mod_Equinox_Mag_L", "co_gun_mod_Equinox_Mag_Large" },
                    { "co_gun_mod_Grendel_Mag_Standard", "co_gun_mod_Grendel_Mag_Medium_Standard" },
                    { "co_gun_mod_Grendel_Receiver_BurstFire", "co_gun_mod_Grendel_Receiver_Burst" },
                    { "co_gun_mod_Grendel_Laser_Empty", "co_gun_mod_Grendel_Laser_None" },
                    { "co_gun_mod_HardTarget_Mag_L", "co_gun_mod_HardTarget_Mag_Large" },
                    { "co_gun_mod_InflictorPistol_Mag_L", "co_gun_mod_HardTarget_Mag_Large" },
                    { "co_gun_mod_Kodama_Mag_Standard_Flechette", "co_gun_mod_Kodama_Mag_Standard" },
                    { "co_gun_mod_Kodama_Mag_Drum_Flechette", "co_gun_mod_Kodama_Mag_Drum" },
                    { "co_gun_mod_Kodama_Mag_Tactical_Flechette", "co_gun_mod_Kodama_Mag_Tactical" },
                    { "co_gun_mod_Kodama_Mag_L_Flechette", "co_gun_mod_Kodama_Mag_Large" },
                    { "co_gun_mod_Kraken_Mag_L", "co_gun_mod_Kraken_Mag_Large" },
                    { "co_gun_mod_M1919_Mag_L", "co_gun_mod_M1919_Mag_Large" },
                    { "co_gun_mod_Maelstrom_Mag_L", "co_gun_mod_Maelstrom_Mag_Large" },
                    { "co_gun_mod_MagSniper_Mag_L", "co_gun_mod_MagSniper_Mag_Large" },
                    { "co_gun_mod_Microgun_Mag_S", "co_gun_mod_Microgun_Mag_Small" },
                    { "co_gun_mod_Novalight_Mag_L", "co_gun_mod_Novalight_Mag_Large" },
                    { "co_gun_mod_Orion_Optics_ShortScope", "co_gun_mod_Orion_Optics_ShortScope_Standard" },
                    { "co_gun_mod_Orion_Mag_L", "co_gun_mod_Orion_Mag_Large" },
                    { "co_gun_mod_Pacifier_Mag_L", "co_gun_mod_Pacifier_Mag_Large" },
                    { "co_gun_mod_Regulator_Internal_MuzzleHighVelocity", "co_gun_mod_Regulator_Internal_HighVelocity" },
                    { "co_gun_mod_RussianAssaultRifle_Optics_ReflexSight", "co_gun_mod_OldEarthAssaultRifle_Optics_ReflexSight" },
                    { "co_gun_mod_RussianAssaultRifle_Optics_IronSights", "co_gun_mod_OldEarthAssaultRifle_Optics_IronSights" },
                    { "co_gun_mod_RussianAssaultRifle_Mag_Tactical", "co_gun_mod_OldEarthAssaultRifle_Mag_Tactical" },
                    { "co_gun_mod_RussianAssaultRifle_Mag_Small", "co_gun_mod_OldEarthAssaultRifle_Mag_Small" },
                    { "co_gun_mod_RussianAssaultRifle_Mag_Drum", "co_gun_mod_OldEarthAssaultRifle_Mag_Drum" },
                    { "co_gun_mod_RussianAssaultRifle_Mag_ArmorPiercing", "co_gun_mod_OldEarthAssaultRifle_Mag_ArmorPiercing" },
                    { "co_gun_mod_RussianAssaultRifle_Grip_Tactical", "co_gun_mod_OldEarthAssaultRifle_Grip_Tactical" },
                    { "co_gun_mod_RussianAssaultRifle_Grip_Ergonomic", "co_gun_mod_OldEarthAssaultRifle_Grip_Ergonomic" },
                    { "co_gun_mod_RussianAssaultRifle_Muzzle_Suppressor", "co_gun_mod_OldEarthAssaultRifle_Muzzle_Suppressor" },
                    { "co_gun_mod_RussianAssaultRifle_Muzzle_MuzzleBrake", "co_gun_mod_OldEarthAssaultRifle_Muzzle_MuzzleBrake" },
                    { "co_gun_mod_RussianAssaultRifle_Barrel_Short", "co_gun_mod_OldEarthAssaultRifle_Barrel_Short" },
                    {"co_gun_mod_RussianHuntingRifle_Internal_HairTrigger" ,"co_gun_mod_OldEarthHuntingRifle_Internal_HairTrigger"},
                    {"co_gun_mod_RussianHuntingRifle_Internal_HighPowered" ,"co_gun_mod_OldEarthHuntingRifle_Internal_HighPowered"},
                    {"co_gun_mod_RussianHuntingRifle_Internal_HighVelocity" ,"co_gun_mod_OldEarthHuntingRifle_Internal_HighVelocity"},
                    {"co_gun_mod_RussianHuntingRifle_Laser_LaserSight" ,"co_gun_mod_OldEarthHuntingRifle_Laser_LaserSight"},
                    {"co_gun_mod_RussianHuntingRifle_Mag_ArmorPiercing" ,"co_gun_mod_OldEarthHuntingRifle_Mag_ArmorPiercing"},
                    {"co_gun_mod_RussianHuntingRifle_Mag_Small" ,"co_gun_mod_OldEarthHuntingRifle_Mag_Small"},
                    {"co_gun_mod_RussianHuntingRifle_Mag_Standard" ,"co_gun_mod_OldEarthHuntingRifle_Mag_Standard"},
                    { "co_gun_mod_Shotty_Mag_L", "co_gun_mod_Shotty_Mag_Large" },
                    { "co_gun_mod_Sidestar_Mag_L", "co_gun_mod_Sidestar_Mag_Large" },
                    { "co_gun_mod_Tombstone_Mag_L", "co_gun_mod_Tombstone_Mag_Large" },
                    { "co_gun_mod_UrbanEagle_Mag_L", "co_gun_mod_UrbanEagle_Mag_Large" },
                    { "co_gun_mod_XM2311_Mag_L", "co_gun_mod_XM2311_Mag_Large" },

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
                    string levelledlist = "atwu_" + upgrade.Key.ToString();

                    //Find the Weapon LevelledList
                    bool foundweaponlist = false;
                    foreach (var lvl in myMod.LeveledItems)
                    {
                        if (lvl.EditorID == "atwu_" + upgrade.Value.WeaponName)
                        {
                            foundweaponlist = true;
                            break;
                        }
                    }
                    if (!foundweaponlist)
                    {
                        myMod.LeveledItems.Add(new LeveledItem(myMod)
                        {
                            EditorID = "atwu_" + upgrade.Value.WeaponName,
                            Entries = new ExtendedList<LeveledItemEntry>()
                        });
                    }
                    //Add the levelled list for the upgrade/weapon pairing - used in crafting
                    myMod.LeveledItems.Add(new LeveledItem(myMod)
                    {
                        EditorID = levelledlist,
                        Entries = new ExtendedList<LeveledItemEntry>()
                    });
                    //Add the include omod for the upgrade/weapon pairing - used in dropped loot/vendors                    
                    var upgradeinclude = new WeaponModification(myMod)
                    {
                        EditorID = levelledlist,
                        Includes = new ExtendedList<ObjectModInclude>()
                    };
                    //Need to find the modgroup.
                    upgrademodlist.Add(levelledlist, upgradeinclude);
//                    myMod.ObjectModifications.Add(upgradeinclude);
                   
                    foreach (var stat in StatLib)
                    {
                        for (int i = 0; i < StatLib[stat.Key].StepCount; i++)
                        {
                            decimal amount = StatLib[stat.Key].Default + (i * StatLib[stat.Key].Step);
                            Console.WriteLine("Creating " + upgrade.Key + " " + stat.Key + " " + amount);
                            CreateUpgrade(myMod, UpgradeLib[upgrade.Key], StatLib[stat.Key], amount, levelledlist);
                        }
                    }

                    //Add new Upgrade to weapon list
                    foreach (var lvl in myMod.LeveledItems)
                    {
                        if (lvl.EditorID == "atwu_" + upgrade.Value.WeaponName)
                        {
                            foreach (var newlist in myMod.LeveledItems)
                            {
                                if (newlist.EditorID == levelledlist)
                                {
                                    if (newlist.Entries.Count > 0)
                                    {
                                        lvl.Entries.Add(new LeveledItemEntry()
                                        {
                                            Reference = newlist.ToLink<IItemGetter>(),
                                            Level = 1,
                                            Count = 1
                                        });
                                    }
                                    break;
                                }
                            }
                            break;
                        }
                    }
                }
                //Add the weapon lists to a global list
                foreach (var lvl in myMod.LeveledItems)
                {
                    if (lvl.EditorID == "atwu_mainlist")
                    {
                        foreach (var weap in Weapons)
                        {
                            foreach (var weaplvl in myMod.LeveledItems)
                            {
                                if(weaplvl.EditorID == "atwu_" + weap)
                                {
                                    lvl.Entries.Add(new LeveledItemEntry()
                                    {
                                        Reference = weaplvl.ToLink<IItemGetter>(),
                                        Level = 1,
                                        Count = 1
                                    });
                                }
                            }
                        }
                    }
                }
                //CreateUpgrade(myMod, UpgradeLib["mod_Solstice_Grip_Tactical"], StatLib["PhysicalMultAndAdd"], 2, "Beowulf");
            }
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm");
            Console.WriteLine("Finished");
            foreach(var miss in MissingCOs)
            {
                Console.WriteLine(miss);
            }

            foreach (var setting in rtfpsettings)
            {
                Console.WriteLine(setting);
            }
            
            return 0;
        }

        public static int DamageMode = 0;//0 Energy , 1 EM, 2 Phys

        public static Dictionary<string,BaseUpgrade> UpgradeLib = new Dictionary<string, BaseUpgrade>();
        public static Dictionary<string, BonusStats> StatLib = new Dictionary<string, BonusStats>();

        public static void BuildStatLib()
        {
            if (DamageMode == 0)
            {
                StatLib.Add("EnergyMultAndAdd", new BonusStats()
                {
                    Type = "KeywordFloat",
                    Percentage = true,
                    Keyword = 0x00060A81,
                    StatName = "Energy Damage",
                    property = Weapon.Property.DamageTypeValue,
                    floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                    Default = 0.1M,
                    Step = 0.1M,
                    StepCount = 5
                });
            }

            if (DamageMode == 1)
            {
                StatLib.Add("EMMultAndAdd", new BonusStats()
                {
                    Type = "KeywordFloat",
                    Percentage = true,
                    Keyword = 0x00023190,
                    StatName = "EM Damage",
                    property = Weapon.Property.DamageTypeValue,
                    floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                    Default = 0.1M,
                    Step = 0.1M,
                    StepCount = 5
                });
            }

            if (DamageMode == 2)
            {
                StatLib.Add("PhysicalMultAndAdd", new BonusStats()
                {
                    Type = "Float",
                    Percentage = true,
                    StatName = "Physical Damage",
                    property = Weapon.Property.DamagePhysical,
                    floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                    Default = 0.1M,
                    Step = 0.1M,
                    StepCount = 5
                });
            }

            StatLib.Add("EMFlat", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = false,
                Keyword = 0x00023190,
                StatName = "EM Damage",
                property = Weapon.Property.DamageTypeValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 10,
                Step = 5,
                StepCount = 5
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
                Step = 5,
                StepCount = 5
            });           
            StatLib.Add("ToxicFlat", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = false,
                Keyword = 0x00000B79,
                StatName = "Toxic Damage",
                property = Weapon.Property.DamageTypeValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 10,
                Step = 5,
                StepCount = 5
            });

            StatLib.Add("PhysicalAdd", new BonusStats()
            {
                Type = "Float",
                Percentage = false,
                StatName = "Physical Damage",
                property = Weapon.Property.DamagePhysical,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 10,
                Step = 5,
                StepCount = 5
            });

            StatLib.Add("AmmoCapacityAdd", new BonusStats()
            {
                Type = "Int",
                Percentage = false,
                StatName = "Ammo Capacity",
                property = Weapon.Property.AmmoCapacity,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 6,
                Step = 6,
                StepCount = 5
            });
            StatLib.Add("ProjectileAdd", new BonusStats()
            {
                Type = "Int",
                Percentage = false,
                StatName = "Projectiles",
                property = Weapon.Property.ProjectileCount,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 1,
                Step = 1,
                StepCount = 3
            });
            StatLib.Add("AmmoCapacityMultAndAdd", new BonusStats()
            {
                Type = "Float",
                Percentage = true,
                StatName = "Ammo Capacity",
                property = Weapon.Property.AmmoCapacity,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = 0.1M,
                Step = 0.1M,
                StepCount = 5
            });
            StatLib.Add("StabilityMultAndAdd", new BonusStats()
            {
                Type = "Float",
                Percentage = true,
                StatName = "Spread",
                property = Weapon.Property.AimModelConeMaxDegrees,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = -0.5M,
                Step = -0.05M,
                StepCount = 5
            });
            StatLib.Add("CritDamageMultAndAdd", new BonusStats()
            {
                Type = "Float",
                Percentage = true,
                StatName = "Crit Damage",
                property = Weapon.Property.CriticalDamageMultiplier,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = 0.5M,
                Step = 0.10M,
                StepCount = 5
            });
            StatLib.Add("BashDamageMultAndAdd", new BonusStats()
            {
                Type = "Float",
                Percentage = true,
                StatName = "Bash Damage",
                property = Weapon.Property.BashDamage,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = 1.0M,
                Step = 0.50M,
                StepCount = 5
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
                StatName = "02 Costs",
                property = Weapon.Property.ActorValue,
                Keyword = 0x0022F93D,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = -0.35M,
                Step = -0.05M,
                StepCount = 5
            });
            StatLib.Add("BonusXPAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Bonus XP",
                property = Weapon.Property.ActorValue,
                Keyword = 0x002D873C,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 0.10M,
                Step = 0.05M,
                StepCount = 5
            });
            StatLib.Add("ReloadSpeedAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Reload Speed",
                property = Weapon.Property.ActorValue,
                Keyword = 0x002D87C4,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 0.20M,
                Step = 0.05M,
                StepCount = 5
            });
            StatLib.Add("DamageReductionAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Damage Taken",
                property = Weapon.Property.ActorValue,
                Keyword = 0x0030397A,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = -0.10M,
                Step = -0.05M,
                StepCount = 5
            });
            StatLib.Add("HealRateAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = false,
                StatName = "Health Regen",
                property = Weapon.Property.ActorValue,
                Keyword = 0x000002D7,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 0.10M,
                Step = 0.05M,
                StepCount = 2
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
                Step = 25,
                StepCount = 5
            });
            StatLib.Add("JumpAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Jump Strength",
                property = Weapon.Property.ActorValue,
                Keyword = 0x00040CDC,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = 0.10M,
                Step = 0.05M,
                StepCount = 5
            });
            StatLib.Add("MovementAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Movement Speed",
                property = Weapon.Property.ActorValue,
                Keyword = 0x000002DA,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 0.10M,
                Step = 0.05M,
                StepCount = 5
            });
            StatLib.Add("StealthLightDetectionAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                Keyword = 0x002EC4F5,
                StatName = "Stealth Light Detection",
                property = Weapon.Property.ActorValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = -0.3M,
                Step = -0.1M,
                StepCount = 5
            });
            StatLib.Add("StealthMovementDetectionAdd", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                Keyword = 0x002EC4ED,
                StatName = "Stealth Movement Detection",
                property = Weapon.Property.ActorValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = -0.3M,
                Step = -0.1M,
                StepCount = 5
            });
        }
    }
}