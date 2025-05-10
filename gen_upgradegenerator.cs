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

        public static IStarfieldModGetter SourceESM;
        public static ModKey StarfieldModKey;
        public static ModKey BlackSiteModKey;

        public static string csvoutput;
        public static bool CreateUpgrade(StarfieldMod myMod, BaseUpgrade upgrade, StatSet stats, string LevelledListContains, int level, int step)
        {
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                //Find the weapon mod and recipe to copy
                WeaponModification originalmod = null;             
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

                        var match = SourceESM.ObjectModifications
                            .FirstOrDefault(obj => string.Equals(obj.EditorID, targetID, StringComparison.OrdinalIgnoreCase));

                        if (match != null)
                        {
                            var copy = (WeaponModification)match.DeepCopy();
                            modcache[targetID.ToLower()] = copy;
                            originalmod = modcache[targetID.ToLower()].DeepCopy();
                        }
                    }
                    if (originalmod == null) {
                        Console.WriteLine("Missing OM: " + upgrade.BaseWeaponModID);
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
                    Data = 1
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
                string ingameName = upgrade.WeaponName + " " + stats.Name + " " + gen_upgradegenerator_utils.getDiscriptiveLevel(step, stats.Theme) + " "+ originalmod.Name;
                ingameName = gen_upgradegenerator_utils.ReplaceWords(ingameName);
                ingameName = ingameName.Trim();
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
                string omodName = stats.Name + " " + gen_upgradegenerator_utils.getDiscriptiveLevel(step, stats.Theme) + " " + originalmod.Name;
                omodName = gen_upgradegenerator_utils.ReplaceWords(omodName);
                omodName = omodName.Trim();
                string Description = "";//
                foreach (var statname in stats.stats)
                {
                    gen_upgradegenerator_utils.AddStat(statname, ref omod, ref Description,step,false);                    
                }
                string justnumberdesc = Description;
                Description = stats.Description + Description;
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
                        File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>("Items\\DataSlate\\DataSlate01.nif"),
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

                var co = new ConstructibleObject(myMod)
                {
                    EditorID = editorid,
                    Description = Description,
                    CreatedObject = targetmod,
                    WorkbenchKeyword = WorkbenchWeaponKeyword,
                    AmountProduced = 1,
                    LearnMethod = ConstructibleObject.LearnMethodEnum.DefaultOrConditions,
                    Conditions = new ExtendedList<Condition>()
                };                
                co.ConstructableComponents = gen_upgradegenerator_utils.GetUpgradeCost(env.LoadOrder[0].ModKey,level);

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
                //Add to modgroups
                IFormLinkNullable<IAObjectModificationGetter> includemod = omod.FormKey.ToNullableLink<IAObjectModificationGetter>();
                //Does the modgroup already exist?
                byte safelevel = 0;
                if (level < 255) { safelevel = (byte)level; }
                else safelevel = 255;

                bool added = false;
                foreach (var modgroup in myMod.ObjectModifications)
                {
                    foreach (var includedobjmod in modgroup.Includes)
                    {
                        if (includedobjmod.Mod.FormKey == originalmod.FormKey)
                        {
                            if (!added)
                            {
                                modgroup.Includes.Add(new ObjectModInclude()
                                {
                                    DoNotUseAll = true,
                                    MinimumLevel = safelevel,
                                    Mod = includemod,
                                    Optional = true
                                });
                                added = true;
                                break;
                            }
                        }
                    }
                }
                //Can't find it so add it to our mod.
                if (!added)
                {
                    foreach (var objmod in SourceESM.ObjectModifications)
                    {
                        foreach (var includedobjmod in objmod.Includes)
                        {
                            if (includedobjmod.Mod.FormKey == originalmod.FormKey)
                            {
                                //This mod is in this this modgroup
                                var group = objmod.DeepCopy();
                                group.Includes.Add(new ObjectModInclude()
                                {
                                    DoNotUseAll = true,
                                    MinimumLevel = safelevel,
                                    Mod = includemod,
                                    Optional = true
                                });
                                myMod.ObjectModifications.Add(group);
                                added = true;
                                break;
                            }
                        }
                    }
                }


                //Write to CSV
                csvoutput += upgrade.WeaponName + "," + upgrade.BaseWeaponModID + "," + level + "," + omodName + "," + justnumberdesc + "\n";
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

                SourceESM = env.LoadOrder[0].Mod;
                StarfieldModKey = new ModKey("Starfield", ModType.Master);
                BlackSiteModKey = new ModKey("AvontechBlacksiteBlueprints", ModType.Master);

                //DEBUG SECTION
                //var match = SourceESM.ObjectModifications[new FormKey(StarfieldModKey, 0x0014AFDB)];

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
                    foreach (var objmod in SourceESM.ObjectModifications)
                    {
                        string coid = "co_gun_" + objmod.EditorID;
                        if (comap.ContainsKey(coid))
                        {
                            coid = comap[coid];
                        }
                        if (objmod.EditorID.ToLower().Contains(weapon.ToLower()))
                        {
                            if (!coid.Contains("Quality") &&
                                //!coid.Contains("None") &&
                                !coid.Contains("Modgroup") &&
                                !coid.Contains("OLD") &&
                                !coid.Contains("AVM") &&
                                !coid.Contains("CUT"))
                            {
                                bool foundblueprint = false;
                                foreach (var allco in SourceESM.ConstructibleObjects)
                                {
                                    if (allco.EditorID.ToLower() == coid.ToLower())
                                    {
                                        foundblueprint = true;
                                    }
                                }
                                if (!foundblueprint)
                                {
                                    IFormLinkNullable<IKeywordGetter> WorkbenchWeaponKeyword = new FormKey(StarfieldModKey, 0x002CE1C0).ToNullableLink<IKeywordGetter>();//WorkbenchWeaponKeyword "Weapons" [KYWD:002CE1C0]
                                    IFormLinkNullable<IItemGetter> commonresource = new FormKey(StarfieldModKey, gen_upgradegenerator_utils.GetBasicResource()).ToNullableLink<IItemGetter>();//ResInorgCommonIron "Iron" [IRES:000057C7]

                                    var co = new ConstructibleObject(myMod)
                                    {
                                        EditorID = coid,
                                        Description = " ",
                                        CreatedObject = objmod.ToNullableLink<IConstructibleObjectTargetGetter>(),
                                        WorkbenchKeyword = WorkbenchWeaponKeyword,
                                        AmountProduced = 1,
                                        LearnMethod = ConstructibleObject.LearnMethodEnum.DefaultOrConditions,
                                        Conditions = new ExtendedList<Condition>()
                                    };
                                    co.ConstructableComponents = new ExtendedList<ConstructibleObjectComponent>() { new ConstructibleObjectComponent()
                                        {
                                            Count = (uint)random.Next(4),
                                            Component = commonresource
                                        } };
                                    myMod.ConstructibleObjects.Add(co);
                                }
                                var attach = gen_upgradegenerator_utils.getAttachPoint(objmod.AttachPoint.FormKey.ToString());
                                if (attach.Length > 0)
                                {
                                    var upgrade = new BaseUpgrade()
                                    {
                                        BaseWeaponModID = objmod.EditorID,
                                        BaseConstructableEditorId = coid,
                                        WeaponName = weapon,
                                        AttachPoint = attach,
                                        formKey = objmod.FormKey
                                    };
                                    if (!UpgradeLib.ContainsKey(objmod.EditorID))
                                    {
                                        UpgradeLib.Add(objmod.EditorID, upgrade);
                                    }
                                }
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
                            Name = upgrade.Value.WeaponName + "",
                            Model = new Model()
                            {
                                File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>(gen_upgradegenerator_utils.GetWeaponModel(upgrade.Value.WeaponName)),
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
                        IFormLinkNullable<IKeywordGetter> WorkbenchBlacksiteFilterKeyword = new FormKey(BlackSiteModKey, 0x0000080C).ToNullableLink<IKeywordGetter>(); //WorkbenchBlacksiteFilterKeyword [KYWD:0000080C]
                        IFormLinkNullable<IKeywordGetter> WorkbenchBlacksiteKeyword = new FormKey(BlackSiteModKey, 0x0000080A).ToNullableLink<IKeywordGetter>(); //WorkbenchBlacksiteKeyword[KYWD: 0000080A]
                        IFormLinkNullable<IItemGetter> atbb_upgradeitem = new FormKey(BlackSiteModKey, 0x00000809).ToNullableLink<IItemGetter>();//atbb_upgradeitem "Blacksite Blueprint" [MISC:01000809]

                        var co = new ConstructibleObject(myMod)
                        {
                            EditorID = "atbb_lvlbookco_" + upgrade.Value.WeaponName,
                            Description = "Create a Levelled Avontech Blacksite Blueprint Weapon Upgrade for the " + upgrade.Value.WeaponName,
                            CreatedObject = book.ToNullableLink<IConstructibleObjectTargetGetter>(),
                            WorkbenchKeyword = WorkbenchBlacksiteKeyword,
                            AmountProduced = 1,
                            LearnMethod = ConstructibleObject.LearnMethodEnum.DefaultOrConditions,                            
                            Categories = new ExtendedList<IFormLinkGetter<IKeywordGetter>>() { WorkbenchBlacksiteFilterKeyword }
                        };
                        co.ConstructableComponents = new ExtendedList<ConstructibleObjectComponent>() { 
                            new ConstructibleObjectComponent() { Component = atbb_upgradeitem, Count = 1 } 
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
                    File.AppendAllText("output.csv", csvoutput);
                    csvoutput = "";
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