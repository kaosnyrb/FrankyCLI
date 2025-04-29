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
using Noggog.StructuredStrings;
using System.Globalization;

namespace FrankyCLI
{
    public class gen_upgradegenerator
    {

        //DamageMode Groups
        //0 Energy
        //1 EM
        //2 Phys

        public static int DamageMode = 0;//0 Energy , 1 EM, 2 Phys

        public static List<string> MissingCOs = new List<string>();

        public static Dictionary<string, WeaponModification> modcache = new Dictionary<string, WeaponModification>();
        public static Dictionary<string, ConstructibleObject> cocache = new Dictionary<string, ConstructibleObject>();

        public static Dictionary<string, BaseUpgrade> UpgradeLib = new Dictionary<string, BaseUpgrade>();
        public static Dictionary<string, StatSet> StatLib = new Dictionary<string, StatSet>();

        public static bool CreateUpgrade(StarfieldMod myMod, BaseUpgrade upgrade, StatSet stats, string LevelledListContains, int level, int step)
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
                        //Search for the mod
                        string targetID = upgrade.BaseWeaponModID;

                        var match = env.LoadOrder[0].Mod.ObjectModifications
                            .FirstOrDefault(obj => string.Equals(obj.EditorID, targetID, StringComparison.OrdinalIgnoreCase));

                        if (match != null)
                        {
                            var copy = (WeaponModification)match.DeepCopy();
                            modcache[targetID.ToLower()] = copy;
                            originalmod = modcache[targetID.ToLower()].DeepCopy();
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
                        //We can't find the original construtable object. Here would be the point to make a new one.

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
                
                string amountstring = level.ToString();
                string editorid = "atbb_" + amountstring + "_" + stats.Name + "_" + originalmod.EditorID;                
                
                //Global
                string GlobalEditorid = "atbb_g_" + amountstring + "_" + stats.Name + "_" + originalmod.EditorID;
                var global = new Global(myMod)
                {
                    EditorID = GlobalEditorid,
                    Data = 0                    
                };
                myMod.Globals.Add(global);



                //Add OMOD
                var omod = new WeaponModification(myMod)
                {
                    EditorID = editorid,
                    Name = originalmod.Name,
                    Description = stats.Description,
                    Model = originalmod.Model,
                    TargetOmodKeywords = originalmod.TargetOmodKeywords,
                    FilterKeywords = originalmod.FilterKeywords,
                    AttachPoint = originalmod.AttachPoint,
                    AttachParentSlots = originalmod.AttachParentSlots,
                    Includes = originalmod.Includes,
                    Properties = originalmod.Properties,
                };
                //We need to build the UI based on the weapon stats.
                string ingameName = upgrade.WeaponName + " " + gen_upgradegenerator_utils.getDiscriptiveLevel(step, stats.Theme) + " " + stats.Name + " "+ originalmod.Name;

                //Remove the DontShowInUI [KYWD:00374EFA]
                for (int i = 0; i < omod.Properties.Count; i++)
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
                //The name of the OMOD contains all it's stats.
                string omodName = gen_upgradegenerator_utils.getDiscriptiveLevel(step, stats.Theme) + " " + stats.Name + " " + originalmod.Name;
                string Description = stats.Description;
                foreach (var statname in stats.stats)
                {
                    var stat = gen_upgradegenerator_utils.StatBank[statname];
                    decimal amount = stat.Default + (step * stat.Step);
                    string amountstr = amount.ToString();
                    if (stat.Percentage) { 
                        if (stat.Type == "Float" || stat.Type == "KeywordFloat")
                        {
                            amountstr = (amount * 100).ToString("F0");
                        }
                        amountstr += "%";
                    }
                    //if (amount < 0) { amountstr = "-" + amountstr; }
                    if (amount > 0) { amountstr = "+" + amountstr; }
                    if (stat.Type == "Int")
                    {
                        omod.Properties.Add(new ObjectModIntProperty<Weapon.Property>
                        {
                            Property = stat.property,
                            Value = (uint)amount,
                            FunctionType = stat.floatFunctionType,
                        });
                        Description += " / " + amountstr + " " + stat.StatName;
                        //omodName += "\n " + amountstr + " " + stat.StatName;
                    }
                    if (stat.Type == "Float")
                    {
                        omod.Properties.Add(new ObjectModFloatProperty<Weapon.Property>
                        {
                            Property = stat.property,
                            Value = (float)amount,
                            FunctionType = stat.floatFunctionType,
                        });
                        Description += " / " + amountstr + " " + stat.StatName;
                        //omodName += "\n " + amountstr + " " + stat.StatName;                        
                    }
                    if (stat.Type == "Enum")
                    {
                        omod.Properties.Add(new ObjectModEnumProperty<Weapon.Property>
                        {
                            Property = stat.property,
                            EnumIntValue = (uint)amount,
                            FunctionType = ObjectModProperty.EnumFunctionType.Set,
                        });
                        //omodName += "\n " + stat.StatName;
                    }
                    if (stat.Type == "KeywordFloat")
                    {
                        IFormLinkNullable<IStarfieldMajorRecordGetter> statkeyword = new FormKey(env.LoadOrder[0].ModKey, stat.Keyword).ToNullableLink<IStarfieldMajorRecordGetter>();
                        omod.Properties.Add(new ObjectModFormLinkFloatProperty<Weapon.Property>
                        {
                            Property = stat.property,
                            Record = statkeyword,
                            Value = (float)amount,
                            FunctionType = stat.floatFunctionType,
                        });
                        Description += " / " + amountstr + " " + stat.StatName;
                        //omodName += "\n " + amountstr + " " + stat.StatName;
                    }
                    if (stat.Type == "AddFormInt")
                    {
                        IFormLinkNullable<IStarfieldMajorRecordGetter> statkeyword = new FormKey(env.LoadOrder[0].ModKey, stat.Keyword).ToNullableLink<IStarfieldMajorRecordGetter>();
                        omod.Properties.Add(new ObjectModFormLinkIntProperty<Weapon.Property>
                        {
                            Property = stat.property,
                            Record = statkeyword,
                            Value = (uint)amount,
                            FunctionType = ObjectModProperty.FormLinkFunctionType.Add,
                        });
                        Description += " / " + amountstr + " " + stat.StatName;
                        //omodName += "\n " + amountstr + " " + stat.StatName;
                    }
                }
                omod.Name = omodName;
                myMod.ObjectModifications.Add(omod);
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
                    Description = "Blueprint for a Avontech Blacksite " + upgrade.WeaponName + " weapon mod.\n\n"+ Description + "\n\n" + omodName + "\n\nThis upgrade is now unlocked at the Weapon Workbench.",
                    Value = 500,
                    Weight = 0,
                    VirtualMachineAdapter = new VirtualMachineAdapter()
                    {
                        Scripts = new ExtendedList<ScriptEntry>()
                        {
                            new ScriptEntry()
                            {
                                Name = "atbb_recipepickup",
                                Properties = new ExtendedList<ScriptProperty>()
                                {
                                    new ScriptObjectProperty()
                                    {
                                        Name = "recipeglobal",
                                        Object = global.ToLink<IStarfieldMajorRecordGetter>(),
                                    }
                                }

                            }
                        }
                    },
                };
                Console.WriteLine("Book ID:" + book.FormKey.ToString());
                myMod.Books.Add(book);
                //Add Construct
                IFormLinkNullable<IKeywordGetter> WorkbenchWeaponKeyword = new FormKey(env.LoadOrder[0].ModKey, 0x002CE1C0).ToNullableLink<IKeywordGetter>();//WorkbenchWeaponKeyword "Weapons" [KYWD:002CE1C0]
                IFormLinkNullable<IConstructibleObjectTargetGetter> targetmod = omod.FormKey.ToNullableLink<IConstructibleObjectTargetGetter>();

                IFormLinkNullable<IItemGetter> ResInorgCommonIron = omod.FormKey.ToNullableLink<IItemGetter>();//ResInorgCommonIron "Iron" [IRES:000057C7]

                var co = new ConstructibleObject(myMod)
                {
                    EditorID = editorid,
                    Description = Description,
                    CreatedObject = targetmod,
                    WorkbenchKeyword = WorkbenchWeaponKeyword,
                    AmountProduced = 1,
                    LearnMethod = ConstructibleObject.LearnMethodEnum.DefaultOrConditions,
                    Conditions = new ExtendedList<Condition>()
                    //Categories = originalco.Categories
                };
                co.ConstructableComponents = originalco.ConstructableComponents;
                co.ConstructableComponents = new ExtendedList<ConstructibleObjectComponent>() { new ConstructibleObjectComponent()
                {
                    Count = 1,
                    Component = ResInorgCommonIron
                } };

                var link = global.ToLink<IGlobalGetter>();
                var con = new GetGlobalValueConditionData();
                con.FirstParameter = new FormLinkOrIndex<IGlobalGetter>(con, link.FormKey);
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
                            Level = (short)level,
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

                var request = YamlImporter.getObjectFrom<UpdateSetRequest>(prefix);
                DamageMode = request.DamageMode;


                gen_upgradegenerator_utils.BuildStatBank(request.ScalingStats);
                gen_upgradegenerator_utils.BuildLevelStyles();

                StatLib = new Dictionary<string, StatSet>();
                foreach(var lib in request.StatLibFile)
                {
                    var files = Directory.GetFiles(lib);
                    foreach (var f in files)
                    {
                        var statlib = gen_upgradegenerator_utils.BuildStatLib(f);
                        foreach (var stat in statlib)
                        {
                            StatLib.Add(stat.Key, stat.Value);
                        }
                    }
                }
                gen_upgradegenerator_utils.LoadThemeFile(request.ThemeFile);


                List<string> Weapons = request.Weapons;
                //Some contstructable objects don't follow the name format so we manually map them
                var comap = gen_upgradegenerator_utils.GetCOMap();
                foreach (var weapon in Weapons) {
                    foreach (var objmod in env.LoadOrder[0].Mod.ObjectModifications)
                    {
                        if (objmod.EditorID.ToLower().Contains(weapon.ToLower()))
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
                                var upgrade = new BaseUpgrade()
                                {
                                    BaseWeaponModID = objmod.EditorID,
                                    BaseConstructableEditorId = coid,
                                    WeaponName = weapon,
                                    AttachPoint = gen_upgradegenerator_utils.getAttachPoint(objmod.AttachPoint.FormKey.ToString()),
                                    formKey = objmod.FormKey
                                };
                                UpgradeLib.Add(objmod.EditorID, upgrade);
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
                    string levelledlist = "atbb_" + upgrade.Key.ToString();

                    //Find the Weapon LevelledList
                    bool foundweaponlist = false;
                    foreach (var lvl in myMod.LeveledItems)
                    {
                        if (lvl.EditorID == "atbb_" + upgrade.Value.WeaponName)
                        {
                            foundweaponlist = true;
                            break;
                        }
                    }
                    if (!foundweaponlist)
                    {
                        LeveledItem lvlwi = new LeveledItem(myMod)
                        {
                            EditorID = "atbb_" + upgrade.Value.WeaponName,
                            Entries = new ExtendedList<LeveledItemEntry>(),
                            Flags = LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer
                        };
                        myMod.LeveledItems.Add(lvlwi);
                        //Create a Book that gets built then spawns from the Levelled list
                        var book = new Book(myMod)
                        {
                            EditorID = "atbb_lvlbook" + upgrade.Value.WeaponName,
                            ObjectBounds = new ObjectBounds(),
                            Transforms = new Transforms(),
                            Name = upgrade.Value.WeaponName + " Blacksite Blueprint",
                            Model = new Model()
                            {
                                File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>("avontech\\warpblueprint.nif"),
                            },
                            Description = "Blueprint for a Avontech Blacksite " + upgrade.Value.WeaponName + " weapon mod.",
                            Value = 500,
                            Weight = 0,
                            VirtualMachineAdapter = new VirtualMachineAdapter()
                            {
                                Scripts = new ExtendedList<ScriptEntry>()
                                {
                                    new ScriptEntry()
                                    {
                                        Name = "atbb_additem",
                                        Properties = new ExtendedList<ScriptProperty>()
                                        {
                                            new ScriptObjectProperty()
                                            {
                                                Name = "LevelledItem",
                                                Object = lvlwi.ToLink<IStarfieldMajorRecordGetter>(),
                                            }
                                        }

                                    }
                                }
                            },
                        };
                        myMod.Books.Add(book);
                        //Create a CO for the Book
                        IFormLinkNullable<IKeywordGetter> WorkbenchIndustrialKeyword = new FormKey(env.LoadOrder[0].ModKey, 0x000AA209).ToNullableLink<IKeywordGetter>();//WorkbenchIndustrialKeyword [KYWD:000AA209]
                        IFormLinkNullable<IItemGetter> Iron = new FormKey(env.LoadOrder[0].ModKey, 0x000057C7).ToNullableLink<IItemGetter>();//ResInorgCommonIron "Iron" [IRES:000057C7]
                        var co = new ConstructibleObject(myMod)
                        {
                            EditorID = "atbb_lvlbookco_" + upgrade.Value.WeaponName,
                            Description = "Create a Avontech Blacksite Blueprint for the " + upgrade.Value.WeaponName,
                            CreatedObject = book.ToNullableLink<IConstructibleObjectTargetGetter>(),
                            WorkbenchKeyword = WorkbenchIndustrialKeyword,
                            AmountProduced = 1,
                            LearnMethod = ConstructibleObject.LearnMethodEnum.DefaultOrConditions
                        };
                        co.ConstructableComponents = new ExtendedList<ConstructibleObjectComponent>() { 
                            new ConstructibleObjectComponent() { Component = Iron, Count = 1 } 
                        };
                        myMod.ConstructibleObjects.Add(co);
                    }
                    //Add the levelled list for the upgrade/weapon pairing - used in crafting
                    myMod.LeveledItems.Add(new LeveledItem(myMod)
                    {
                        EditorID = levelledlist,
                        Entries = new ExtendedList<LeveledItemEntry>(),
                        Flags = LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer
                    });
                    //Add the include omod for the upgrade/weapon pairing - used in dropped loot/vendors                    
                    var upgradeinclude = new WeaponModification(myMod)
                    {
                        EditorID = levelledlist,
                        Includes = new ExtendedList<ObjectModInclude>()
                    };
                    /*
                    if (DamageMode == -1)
                    {
                        var test = StatLib.First();
                        StatLib = new Dictionary<string, BonusStats>();
                        StatLib.Add(test.Key, test.Value);
                    }*/
                    foreach (var stat in StatLib)
                    {
                        if (stat.Value.DamageMode == -1 || stat.Value.DamageMode == DamageMode || DamageMode == -1)
                        {
                            if (stat.Value.AllowedAttachPoints != null)
                            {
                                if (stat.Value.AllowedAttachPoints.Contains(UpgradeLib[upgrade.Key].AttachPoint))
                                {
                                    var levelStyle = gen_upgradegenerator_utils.levelStyles[StatLib[stat.Key].LevelStyle];
                                    for (int i = 0; i < levelStyle.StepCount; i++)
                                    {
                                        Console.WriteLine("Creating " + upgrade.Key + " " + stat.Key);
                                        CreateUpgrade(myMod, UpgradeLib[upgrade.Key], StatLib[stat.Key], levelledlist, levelStyle.startLevel + (i * levelStyle.LevelPerStep), i);
                                    }
                                }
                            }
                        }
                    }
                    //Add new Upgrade to weapon list
                    foreach (var lvl in myMod.LeveledItems)
                    {
                        if (lvl.EditorID == "atbb_" + upgrade.Value.WeaponName)
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
                    if (lvl.EditorID == "atbb_mainlist")
                    {
                        foreach (var weap in Weapons)
                        {
                            foreach (var weaplvl in myMod.LeveledItems)
                            {
                                if(weaplvl.EditorID == "atbb_" + weap)
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
            }
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm");
            Console.WriteLine("Finished");
            foreach(var miss in MissingCOs)
            {
                Console.WriteLine(miss);
            }            
            return 0;
        }
    }
}