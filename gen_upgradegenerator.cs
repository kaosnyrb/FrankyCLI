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
using System.Diagnostics;

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
        public static IStarfieldModGetter StarfieldESM;
        public static ModKey StarfieldModKey;
        public static ModKey BlackSiteModKey;

        public static Dictionary<string, string> ModCOMAPPER = new Dictionary<string, string>();//Used to build the CO map


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

                //If we're a Unique_Legendary then calc the level.
                if (level == -1)
                {
                    Random random = new Random();
                    level = 50 + random.Next(150);
                }

                //Figure out the text                
                string amountstring = level.ToString();
                string omodeditorid = "atbb_omod_" + originalmod.EditorID + "_" + stats.Name + "_" +  amountstring;                
                
                //Global
                string GlobalEditorid = "atbb_g_" + originalmod.EditorID + "_" + stats.Name + "_" + amountstring;
                var global = new Global(myMod)
                {
                    EditorID = GlobalEditorid,
                    Data = 0
                };
                myMod.Globals.Add(global);

                //Add OMOD
                var omod = new WeaponModification(myMod)
                {
                    EditorID = omodeditorid,
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
                string ingameName = upgrade.FixedWeaponName + " " + stats.Name + " " + gen_upgradegenerator_utils.getDiscriptiveLevel(step, stats.Theme) + " "+ originalmod.Name;
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
                string Description = "";
                string StatTag = "";
                foreach (var statname in stats.stats)
                {
                    gen_upgradegenerator_utils.AddStat(statname, ref omod, ref Description,ref StatTag,step, false);                    
                }
                string justnumberdesc = Description;
                Description = stats.Description + Description;
                omod.Name = omodName;// + " " + StatTag;
                myMod.ObjectModifications.Add(omod);
                //Add Book
                string editorBookid = "atbb_book_" + originalmod.EditorID + "_" + stats.Name + "_" + amountstring;

                var book = new Book(myMod)
                {
                    EditorID = editorBookid,
                    ObjectBounds = new ObjectBounds(),
                    Transforms = new Transforms(),
                    Name = ingameName,
                    Model = new Model()
                    {
                        File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>("Items\\DataSlate\\DataSlate01.nif"),
                    },
                    Description = "Blueprint for a Avontech Blacksite " + upgrade.FixedWeaponName + " weapon mod.\n\n"+ Description + "\n\n" + omodName + "\n\nThis upgrade is now unlocked at the Weapon Workbench.",
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
                //Console.WriteLine("Book ID:" + book.FormKey.ToString());
                myMod.Books.Add(book);

                //Add Construct
                IFormLinkNullable<IKeywordGetter> WorkbenchWeaponKeyword = new FormKey(StarfieldModKey, 0x002CE1C0).ToNullableLink<IKeywordGetter>();//WorkbenchWeaponKeyword "Weapons" [KYWD:002CE1C0]
                IFormLinkNullable<IConstructibleObjectTargetGetter> targetmod = omod.FormKey.ToNullableLink<IConstructibleObjectTargetGetter>();
                string coeditorid = "atbb_co_" + originalmod.EditorID + "_" + stats.Name + "_" + amountstring;

                var co = new ConstructibleObject(myMod)
                {
                    EditorID = coeditorid,
                    Description = Description,
                    CreatedObject = targetmod,
                    WorkbenchKeyword = WorkbenchWeaponKeyword,
                    AmountProduced = 1,
                    LearnMethod = ConstructibleObject.LearnMethodEnum.DefaultOrConditions,
                    Conditions = new ExtendedList<Condition>(),
                    RequiredPerks = new ExtendedList<ConstructibleRequiredPerk>()
                };
                if (stats.RequiredPerk.Length > 0)
                {
                    var perkuint = gen_upgradegenerator_utils.GetPerk(stats.RequiredPerk);
                    IFormLinkNullable<IPerkGetter> PerkForm = new FormKey(StarfieldModKey, perkuint).ToNullableLink<IPerkGetter>();

                    co.RequiredPerks.Add(new ConstructibleRequiredPerk(){
                        Perk = PerkForm,
                        Rank = stats.RequiredPerkLevel
                    });                    
                }
                //Research Requirements
                Condition Research = null;
                Research = gen_upgradegenerator_utils.GetPartResearchReq(StarfieldModKey, level, upgrade.AttachPoint);
                //Not all upgrades need research.
                if (Research != null)
                {
                    co.Conditions.Add(Research);
                }

                // Build Cost
                co.ConstructableComponents = gen_upgradegenerator_utils.GetUpgradeCost(StarfieldModKey, level);
                
                // Global Unlock
                var link = global.ToLink<IGlobalGetter>();
                var con = new GetGlobalValueConditionData();
                con.FirstParameter = new FormLinkOrIndex<IGlobalGetter>(con, link.FormKey);
                co.Conditions.Add(new ConditionFloat()
                {
                    Data = con,
                    CompareOperator = CompareOperator.GreaterThan,
                    ComparisonValue = 0
                });
                // Complete the Weapon CO
                myMod.ConstructibleObjects.Add(co);

                //Add Book to LevelledList
                foreach( var lvl in myMod.LeveledItems)
                {
                    if (lvl.EditorID.Contains(LevelledListContains))
                    {
                        var bookentry = new LeveledItemEntry()
                        {
                            Count = 1,
                            ChanceNone = Percent.Zero,
                            Level = (short)level,
                            Reference = book.ToLink<IItemGetter>(),
                            Conditions = new ExtendedList<Condition>(),
                        };
                        bookentry.Conditions.Add(new ConditionFloat()
                        {
                            Data = con,
                            CompareOperator = CompareOperator.EqualTo,
                            ComparisonValue = 0
                        });
                        lvl.Entries.Add(bookentry);     
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

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

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


                //Find the SourceESM
                for (int i = 0; i < env.LoadOrder.Count; i++)
                {
                    if (env.LoadOrder[i].FileName == request.WeaponESM)
                    {
                        SourceESM = env.LoadOrder[i].Mod;
                    }
                }
                //SourceESM = env.LoadOrder[0].Mod;
                StarfieldModKey = new ModKey("Starfield", ModType.Master);
                StarfieldESM = env.LoadOrder[0].Mod;
                BlackSiteModKey = new ModKey("AvontechBlacksiteBlueprints", ModType.Master);

                //DEBUG SECTION
                //var match = SourceESM.ObjectModifications[new FormKey(StarfieldModKey, 0x0014AFDB)];
                //var match = SourceESM.ConstructibleObjects[new FormKey(StarfieldModKey, 0x000447C6)];
                //gen_upgradegenerator_utils.ResearchCopy = (IsResearchCompleteConditionData)match.Conditions[0].Data.DeepCopy();


                
                foreach(var file in Directory.EnumerateFiles(request.ScalingStats))
                {
                    gen_upgradegenerator_utils.BuildStatBank(file);
                }

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
                        if (!ModCOMAPPER.ContainsKey(coid))
                        {
                            ModCOMAPPER.Add(coid, objmod.EditorID);
                        }

                        if (comap.ContainsKey(coid))
                        {
                            coid = comap[coid];
                        }
                        if (objmod.EditorID.ToLower().Contains(weapon.ToLower()))
                        {
                            if (!gen_upgradegenerator_utils.IsBanned(coid))
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
                                        FixedWeaponName = gen_upgradegenerator_utils.RenameWeapons(weapon),
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
                int count = 0;
                int total = UpgradeLib.Keys.Count * StatLib.Keys.Count;
                foreach (var upgrade in UpgradeLib)
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
                        IFormLinkNullable<ITransformGetter> Inv_Guns_Workbench3D_01 = new FormKey(StarfieldModKey, 0x000796D5).ToNullableLink<ITransformGetter>();//Inv_Guns_Workbench3D_01 [TRNS:000796D5]

                        var book = new Book(myMod)
                        {
                            EditorID = "atbb_lvlbook" + upgrade.Value.WeaponName,
                            ObjectBounds = new ObjectBounds(),
                            Transforms = new Transforms() { Workbench = Inv_Guns_Workbench3D_01 },
                            Name = upgrade.Value.FixedWeaponName + "",
                            Model = new Model()
                            {
                                File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>(gen_upgradegenerator_utils.GetWeaponModel(upgrade.Value.WeaponName)),
                            },
                            Description = "Blueprint for a Avontech Blacksite " + upgrade.Value.FixedWeaponName + " weapon mod.",
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
                            Description = "Create a Levelled Avontech Blacksite Blueprint Weapon Upgrade for the " + upgrade.Value.FixedWeaponName,
                            CreatedObject = book.ToNullableLink<IConstructibleObjectTargetGetter>(),
                            WorkbenchKeyword = WorkbenchBlacksiteKeyword,
                            AmountProduced = 1,
                            LearnMethod = ConstructibleObject.LearnMethodEnum.DefaultOrConditions,                            
                            Categories = new ExtendedList<IFormLinkGetter<IKeywordGetter>>() { WorkbenchBlacksiteFilterKeyword },                            
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
                        Console.WriteLine(count + "/" + total + " : " + upgrade.Key + stat.Value.Name + " " + UpgradeLib[upgrade.Key].AttachPoint);
                        count++;
                        if (stat.Value.DamageMode == -1 || stat.Value.DamageMode == DamageMode || DamageMode == -1)
                        {
                            if (stat.Value.AllowedAttachPoints != null)
                            {
                                if (stat.Value.AllowedAttachPoints.Contains(UpgradeLib[upgrade.Key].AttachPoint))
                                {
                                    var levelStyle = gen_upgradegenerator_utils.levelStyles[StatLib[stat.Key].LevelStyle];
                                    for (int i = 0; i < levelStyle.StepCount; i++)
                                    {
                                        //Console.WriteLine("Creating " + upgrade.Key + " " + stat.Key);
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

            //System.NotImplementedException: Writing with compression enabled is not currently supported.  https://github.com/Mutagen-Modding/Mutagen/issues/235
            foreach (var rec in myMod.EnumerateMajorRecords())
            {
                rec.IsCompressed = false;
            }
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm");
            Console.WriteLine("Finished");
            stopwatch.Stop();

            //YamlExporter.WriteObjToYaml("COMAPPERTEMP.yaml", ModCOMAPPER);
            
            Console.WriteLine(stopwatch.Elapsed.ToString());
            foreach(var miss in MissingCOs)
            {
                Console.WriteLine(miss);
            }            
            return 0;
        }
    }
}