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
using System.Reflection.Emit;

namespace FrankyCLI
{
    public class UpdateSetRequest
    {
        public int DamageMode;          //0 Energy, 1 EM, 2 Phys (Used to filter percentage damage etc)
        public List<string> StatLibFile;//Upgrades to use
        public string ScalingStats;     //The folder containing the raw stats
        public string ThemeFile;        //The upgrade theme, switched to roman numerals so this is a little redundnat atm
        public List<string> Weapons;    //The weapon ids that we are processing
        public string WeaponESM = "Starfield.esm";//The ESM the weapons are in.
    }

    public class ThemeFile
    {
        public Dictionary<string, PartTheme> PartThemes;
    }

    public class PartTheme
    {
        public Dictionary<int, string> LevelTheme;
    }

    public class BaseUpgrade
    {
        public string WeaponName;
        public string FixedWeaponName;

        public string BaseWeaponModID;
        public string BaseConstructableEditorId;
        public string AttachPoint;

        public FormKey formKey;
        public FormKey coFormKey;
    }

    public class LevelStyle
    {
        public int StepCount = 1;
        public int startLevel = 0;
        public int LevelPerStep = 10;
    }

    public class StatSet
    {
        public string Name;
        public string Description;

        public string LevelStyle;
        public int DamageMode = -1;//-1 all

        public string Theme = "Miltec";

        public string RequiredPerk = "";
        public uint RequiredPerkLevel = 0;

        public List<string> stats;
        public List<string> AllowedAttachPoints;
    }

    public class BonusStats
    {
        public string Type;
        public bool Percentage;
        public Weapon.Property property;
        public ObjectModProperty.FloatFunctionType floatFunctionType;
        public UInt32 Keyword;
        public string StatName;
        public string ShortName;
        public string EnchantModName;

        public decimal Default;
        public decimal Step;

        public bool Lootable = true;

        public List<string> OtherStats;
    }

    public static class gen_upgradegenerator_utils
    {
        public static Dictionary<string, BonusStats> StatBank = new Dictionary<string, BonusStats>();
        public static Dictionary<string,LevelStyle> levelStyles = new Dictionary<string, LevelStyle>();
        public static ThemeFile LoadedThemeFile = new ThemeFile();

        //Attach a new stat to an OMOD.
        public static void AddStat(string statname,ref WeaponModification omod,ref string Description, ref string stattag, int step, bool silent, ref bool lootable)
        {
            var stat = StatBank[statname];
            decimal amount = stat.Default + (step * stat.Step);
            string amountstr = amount.ToString();
            if (stat.Percentage)
            {
                if (stat.Type == "Float" || stat.Type == "BothFloat" || stat.Type == "KeywordFloat")
                {
                    amountstr = (amount * 100).ToString("F0");
                }
                amountstr += "%";
            }
            if (amount > 0) { amountstr = "+" + amountstr; }
            // Number of things like Projectiles or Ammo
            if (stat.Type == "Int")
            {
                omod.Properties.Add(new ObjectModIntProperty<Weapon.Property>
                {
                    Property = stat.property,
                    Value = (uint)amount,
                    FunctionType = stat.floatFunctionType,
                });
                if (!silent)
                {
                    Description += " / " + amountstr + " " + stat.StatName;
                }
            }
            //Percentages/Flat
            if (stat.Type == "Float")
            {
                omod.Properties.Add(new ObjectModFloatProperty<Weapon.Property>
                {
                    Property = stat.property,
                    Value = (float)amount,
                    FunctionType = stat.floatFunctionType,
                });
                if (!silent)
                {
                    Description += " / " + amountstr + " " + stat.StatName;
                }
            }
            // Where value 1 and 2 need set (Like Range)
            if (stat.Type == "BothFloat")
            {
                omod.Properties.Add(new ObjectModFloatProperty<Weapon.Property>
                {
                    Property = stat.property,
                    Value = (float)amount,
                    Value2 = (float)amount,
                    FunctionType = stat.floatFunctionType,
                });
                if (!silent)
                {
                    Description += " / " + amountstr + " " + stat.StatName;
                }
            }
            // Flags like Silent
            if (stat.Type == "Enum")
            {
                omod.Properties.Add(new ObjectModEnumProperty<Weapon.Property>
                {
                    Property = stat.property,
                    EnumIntValue = (uint)amount,
                    FunctionType = ObjectModProperty.EnumFunctionType.Set,
                });
            }
            // Stats like damage reduction
            if (stat.Type == "KeywordFloat")
            {
                IFormLinkNullable<IStarfieldMajorRecordGetter> statkeyword = new FormKey(gen_upgradegenerator.StarfieldModKey, stat.Keyword).ToNullableLink<IStarfieldMajorRecordGetter>();
                omod.Properties.Add(new ObjectModFormLinkFloatProperty<Weapon.Property>
                {
                    Property = stat.property,
                    Record = statkeyword,
                    Value = (float)amount,
                    FunctionType = stat.floatFunctionType,
                });
                if (!silent)
                {
                    Description += " / " + amountstr + " " + stat.StatName;
                }
            }
            // Enchants
            if (stat.Type == "AddFormInt")
            {
                amountstr = "";
                IFormLinkNullable<IStarfieldMajorRecordGetter> statkeyword = new FormKey(gen_upgradegenerator.StarfieldModKey, stat.Keyword).ToNullableLink<IStarfieldMajorRecordGetter>();
                omod.Properties.Add(new ObjectModFormLinkIntProperty<Weapon.Property>
                {
                    Property = stat.property,
                    Record = statkeyword,
                    Value = (uint)amount,
                    FunctionType = ObjectModProperty.FormLinkFunctionType.Add,
                });
                if (!silent)
                {
                    Description += " / " + stat.StatName;
                }
            }
            // Enchants
            if (stat.Type == "ModEnchant")
            {
                IFormLinkNullable<IStarfieldMajorRecordGetter> statkeyword = new FormKey(gen_upgradegenerator.BlackSiteModKey, stat.Keyword).ToNullableLink<IStarfieldMajorRecordGetter>();
                omod.Properties.Add(new ObjectModFormLinkIntProperty<Weapon.Property>
                {
                    Property = stat.property,
                    Record = statkeyword,
                    Value = (uint)amount,
                    FunctionType = ObjectModProperty.FormLinkFunctionType.Add,
                });
                if (!silent)
                {
                    Description += " / " + amountstr + " " + stat.StatName;
                }
            }
            // Attach another OMOD to this entry, can add templates
            if (stat.Type == "Include")
            {
                IFormLinkNullable<IAObjectModificationGetter> statkeyword = new FormKey(gen_upgradegenerator.StarfieldModKey, stat.Keyword).ToNullableLink<IAObjectModificationGetter>();
                omod.Includes.Add(new ObjectModInclude() { Mod = statkeyword, DoNotUseAll = false, MinimumLevel = 0, Optional = false });
                Description += " / " + stat.StatName;
            }
            // More than one stat that need grouping together.
            if (stat.Type == "Group")
            {
                foreach (var otherstat in stat.OtherStats)
                {
                    //Recursion, we add the stats then label them as one.
                    //This is for things like range which has min and max that both need to be set.
                    AddStat(otherstat, ref omod, ref Description, ref stattag, step, true,ref lootable);
                }
                var firststat = StatBank[stat.OtherStats[0]];
                amount = firststat.Default + (step * firststat.Step);
                amountstr = amount.ToString();
                if (firststat.Percentage)
                {
                    if (firststat.Type == "Float" || firststat.Type == "BothFloat" || firststat.Type == "KeywordFloat")
                    {
                        amountstr = (amount * 100).ToString("F0");
                    }
                    amountstr += "%";
                }
                if (amount > 0) { amountstr = "+" + amountstr; }
                if (!silent)
                {
                    Description += " / " + amountstr + " " + stat.StatName;
                }
            }
            if (!silent)
            {
                if (stattag.Length > 0)
                {
                    stattag += "/" + amountstr + stat.ShortName;
                }
                else
                {
                    stattag += amountstr + stat.ShortName;
                }
            }
            //If any of the stats are unlootable then the whole omod is.
            //This is so that we don't have stats like +HP on looted gear as there's no way to show that on the item card.
            if (lootable && stat.Lootable == false) {
                lootable = false;
            }
        }

        
        static Random random = new Random();
        //Controls the Levels/Steps between the different versions of a upgrade
        public static void BuildLevelStyles()
        {
            int Standardstepcount = 10;
            //Standard
            levelStyles.Add("Standard_Common", new LevelStyle
            {
                startLevel = 0,
                StepCount = Standardstepcount,
                LevelPerStep = 10,
            });
            levelStyles.Add("Standard_Rare", new LevelStyle
            {
                startLevel = 80,
                StepCount = Standardstepcount,
                LevelPerStep = 10,
            });
            levelStyles.Add("Standard_Epic", new LevelStyle
            {
                startLevel = 150,
                StepCount = Standardstepcount,
                LevelPerStep = 10,
            });
            levelStyles.Add("Standard_Legendary", new LevelStyle
            {
                startLevel = 220,
                StepCount = Standardstepcount,
                LevelPerStep = 10,
            });
            levelStyles.Add("Unique_Legendary", new LevelStyle
            {
                startLevel = -1,
                StepCount = 1,
                LevelPerStep = 0,
            });
        }

        public static void BuildStatBank(string statsfile)
        {
            var bank = YamlImporter.getObjectFrom<Dictionary<string, BonusStats>>(statsfile);
            foreach(var en in bank)
            {
                StatBank.Add(en.Key,en.Value);
            }
//            StatBank = YamlImporter.getObjectFrom<Dictionary<string, BonusStats>>(statsfile);
        }

        public static Dictionary<string, StatSet> BuildStatLib(string statlibfile)
        {            
            var StatLib = new Dictionary<string, StatSet>();
            StatLib = YamlImporter.getObjectFrom<Dictionary<string, StatSet>>(statlibfile);
            return StatLib;
        }

        public static void LoadThemeFile(string themeFile)
        {
            LoadedThemeFile = YamlImporter.getObjectFrom<ThemeFile>(themeFile);
        }
        public static string getDiscriptiveLevel(int level, string Theme)
        {
            //level = level - (level % 10);
            if (LoadedThemeFile.PartThemes.ContainsKey(Theme))
            {
                if (LoadedThemeFile.PartThemes[Theme].LevelTheme.ContainsKey(level))
                {
                    return LoadedThemeFile.PartThemes[Theme].LevelTheme[level];
                }
                else
                {
                    return "MissingLevel" + level;
                }
            }
            else
            {
                return "MissingPart" + Theme;
            }
        }

        public static string getAttachPoint(string form)
        {
            //We merge some groups here
            switch (form)
            {
                case "02249C:Starfield.esm":
                    return "Muzzle";
                case "02249D:Starfield.esm":
                    return "Barrel";
                case "02EE28:Starfield.esm":
                    return "Laser";
                case "14D08A:Starfield.esm":
                    return "Foregrip";
                case "0191EE:Starfield.esm":
                    return "Laser";
                case "149CA8:Starfield.esm":
                    return "Receiver";
                case "01BC46:Starfield.esm":
                    return "Receiver";
                case "024004:Starfield.esm":
                    return "Receiver";
                case "02249F:Starfield.esm":
                    return "Grip";
                case "0849A6:Starfield.esm":
                    return "Stock";
                case "147AFE:Starfield.esm":
                    return "Receiver";//"Internal";
                case "05D4D7:Starfield.esm":
                    return "Magazine";
                case "022499:Starfield.esm":
                    return "Optic";
                case "2FB3C2:Starfield.esm":
                    return "Handle";
                case "2FB3C0:Starfield.esm":
                    return "Blade";

            }
            Console.WriteLine("Missing Attach Form:" + form);
            return "";
        }

        public static Dictionary<string, string> GetCOMap()
        {            
            var map = YamlImporter.getObjectFrom<Dictionary<string,string>>("Data/comap.yaml");
            return map;
        }

        public static Dictionary<string, string> WeaponModelCache = new Dictionary<string, string>();
        public static string GetWeaponModel(string weapon)
        {
            if (WeaponModelCache.Count == 0)
            {
                WeaponModelCache = YamlImporter.getObjectFrom<Dictionary<string, string>>("Data/weaponmodel.yaml");
            }
            if (!WeaponModelCache.ContainsKey(weapon.ToLower()))
            {
                return "weapons\\breach\\breach.nif";//Incase we haven't set this up yet.
            }
            return WeaponModelCache[weapon.ToLower()];
        }

        public static List<uint> BasicResourceCache = new List<uint>();
        public static uint GetBasicResource()
        {
            if (BasicResourceCache.Count == 0)
            {
                BasicResourceCache = YamlImporter.getObjectFrom<List<uint>>("Data/basicresources.yaml");
            }
            Random random = new Random();
            return BasicResourceCache[random.Next(BasicResourceCache.Count)];
        }

        public static Dictionary<string, uint> PerkList = new Dictionary<string,uint>();
        public static uint GetPerk(string perkname)
        {
            if (PerkList.Count == 0)
            {
                PerkList = YamlImporter.getObjectFrom<Dictionary<string, uint>>("Data/perks.yaml");
            }

            return PerkList[perkname];
        }

        public static Dictionary<string, string> WordReplaceCache = new Dictionary<string, string>();
        public static string ReplaceWords(string input)
        {
            if (WordReplaceCache.Count == 0)
            {
                WordReplaceCache = YamlImporter.getObjectFrom<Dictionary<string, string>>("Data/replacemap.yaml");
            }
            string result = input;
            foreach(var entry in WordReplaceCache)
            {
                result = result.Replace(entry.Key, entry.Value);
            }
            return result;
        }

        public static Dictionary<string, string> WeaponNameCache = new Dictionary<string, string>();
        public static string RenameWeapons(string input)
        {
            if (WeaponNameCache.Count == 0)
            {
                WeaponNameCache = YamlImporter.getObjectFrom<Dictionary<string, string>>("Data/WeaponNameMap.yaml");
            }
            string result = input;
            foreach (var entry in WeaponNameCache)
            {
                if (result == entry.Key)
                {
                    result = entry.Value;
                }
            }
            return result;
        }


        public static List<string> BannedObjectMods = new List<string>();
        public static bool IsBanned(string input)
        {
            if (BannedObjectMods.Count == 0)
            {
                BannedObjectMods = YamlImporter.getObjectFrom<List<string>> ("Data/bannedomods.yaml");
            }
            bool banned = false;
            foreach(var entry in BannedObjectMods)
            {
                if (input.Contains(entry)) {banned = true;}
            }
            return banned;
        }

        public static Condition GetPartResearchReq(ModKey Starfield, int level, string part)
        {

            //Current plan, can't make research things, boo
            //So we just clone existing IsResearchCompleteConditionData and put it on the new blueprint

            FormKey WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x000447C6);

            if (level < 50)
            {
                return null;
            }
            if (level >= 50 && level < 100)
            {
                switch (part) {
                    case "Magazine":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x0002E1EA);//co_gun_mod_Grendel_Mag_Whitehot [COBJ:0002E1EA]
                        break;
                    case "Muzzle":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x000432AE);//co_gun_mod_PumpShotgun_Muzzle_MuzzleBrake [COBJ:000432AE]
                        break;
                    case "Barrel":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00042D1C);//co_gun_mod_InflictorPistol_Barrel_Long [COBJ:00042D1C]
                        break;
                    case "Laser":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00042D27);//co_gun_mod_InflictorRifle_Optics_ShortScope [COBJ:00042D27]
                        break;
                    case "Foregrip":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x000432CD);//co_gun_mod_M1919_Grip_Tactical [COBJ:000432CD]
                        break;
                    case "Receiver":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00042D0F);//co_gun_mod_InflictorPistol_Receiver_BurstFire [COBJ:00042D0F]
                        break;
                    case "Grip":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x000432CD);//co_gun_mod_M1919_Grip_Tactical [COBJ:000432CD]
                        break;
                    case "Stock":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x000432CD);//co_gun_mod_M1919_Grip_Tactical [COBJ:000432CD]
                        break;
                    case "Internal":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00042D95);//co_gun_mod_AutoRivet_Internal_HairTrigger [COBJ:00042D95]
                        break;
                    case "Optic":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00042D27);//co_gun_mod_InflictorRifle_Optics_ShortScope [COBJ:00042D27]
                        break;
                    case "Handle":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x002FB33B);//co_melee_mod_Gutter_Handle_Ergonomic [COBJ:002FB33B]
                        break;
                    case "Blade":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x002FB33A);//co_melee_mod_Gutter_Blade_StainlessSteel [COBJ:002FB33A]
                        break;
                }
            }
            if (level >= 100 && level < 200)
            {
                switch (part)
                {
                    case "Magazine":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x000432B4);//co_gun_mod_PumpShotgun_Mag_Flechette [COBJ:000432B4]
                        break;
                    case "Muzzle":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00042D14);//co_gun_mod_InflictorPistol_Muzzle_FocusNozzle [COBJ:00042D14]
                        break;
                    case "Barrel":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00042D33);//co_gun_mod_InflictorRifle_Barrel_Stabilizing [COBJ:00042D33]
                        break;
                    case "Laser":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x000454AB);//co_gun_mod_HardTarget_Optics_MediumScope [COBJ:000454AB]
                        break;
                    case "Foregrip":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00044595);//co_gun_mod_RocketLauncher_Grip_Foregrip [COBJ:00044595]
                        break;
                    case "Receiver":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00044C26);//co_gun_mod_Razorback_Receiver_BinaryTrigger [COBJ:00044C26]
                        break;
                    case "Grip":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00044595);//co_gun_mod_RocketLauncher_Grip_Foregrip [COBJ:00044595]
                        break;
                    case "Stock":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00044595);//co_gun_mod_RocketLauncher_Grip_Foregrip [COBJ:00044595]
                        break;
                    case "Internal":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00042D1B);//co_gun_mod_InflictorPistol_Internal_Amplifier [COBJ:00042D1B]
                        break;
                    case "Optic":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x000454AB);//co_gun_mod_HardTarget_Optics_MediumScope [COBJ:000454AB]
                        break;
                    case "Handle":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x002FB345);//co_melee_mod_Gutter_Handle_ForceExtruded [COBJ:002FB345]
                        break;
                    case "Blade":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x002FB340);//co_melee_mod_Gutter_Blade_Irradiated [COBJ:002FB340]
                        break;
                }
            }
            if (level >= 200)
            {
                switch (part)
                {
                    case "Magazine":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00042D17);//co_gun_mod_InflictorPistol_Mag_Annihilator [COBJ:00042D17]
                        break;
                    case "Muzzle":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00044576);//co_gun_mod_BigBang_Muzzle_Double [COBJ:00044576]
                        break;
                    case "Barrel":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00042D33);//co_gun_mod_InflictorRifle_Barrel_Stabilizing [COBJ:00042D33]
                        break;
                    case "Laser":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00042D18);//co_gun_mod_InflictorPistol_Laser_ReconSight [COBJ:00042D18]
                        break;
                    case "Foregrip":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00044599);//co_gun_mod_RocketLauncher_Grip_StabilizingStock [COBJ:00044599]
                        break;
                    case "Receiver":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00044C26);//co_gun_mod_Razorback_Receiver_BinaryTrigger [COBJ:00044C26]
                        break;
                    case "Grip":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00044599);//co_gun_mod_RocketLauncher_Grip_StabilizingStock [COBJ:00044599]
                        break;
                    case "Stock":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00044599);//co_gun_mod_RocketLauncher_Grip_StabilizingStock [COBJ:00044599]
                        break;
                    case "Internal":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x0000C522);//co_gun_mod_Microgun_Internal_BulletHose [COBJ:0000C522]
                        break;
                    case "Optic":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x00042D18);//co_gun_mod_InflictorPistol_Laser_ReconSight [COBJ:00042D18]
                        break;
                    case "Handle":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x002FB345);//co_melee_mod_Gutter_Handle_ForceExtruded [COBJ:002FB345]
                        break;
                    case "Blade":
                        WeaponWithResearch = new FormKey(gen_upgradegenerator.StarfieldModKey, 0x002FB340);//co_melee_mod_Gutter_Blade_Irradiated [COBJ:002FB340]
                        break;
                }
            }

            var match = gen_upgradegenerator.StarfieldESM.ConstructibleObjects[WeaponWithResearch];
            var ResearchCopy = (IsResearchCompleteConditionData)match.Conditions[0].Data.DeepCopy();
            return new ConditionFloat()
            {
                Data = ResearchCopy,
                CompareOperator = CompareOperator.EqualTo,
                ComparisonValue = 1
            };
        }

        public static ExtendedList<ConstructibleObjectComponent> GetUpgradeCost(ModKey Starfield,int level)
        {
            Random random = new Random();

            uint resourcea = GetBasicResource();
            uint resourceb = GetBasicResource();

            IFormLinkNullable<IItemGetter> commonresource = new FormKey(Starfield, resourcea).ToNullableLink<IItemGetter>();

            var cost = new ExtendedList<ConstructibleObjectComponent>() { new ConstructibleObjectComponent()
            {
                RequiredCount = (uint)(1 + random.Next(5)),
                Component = commonresource
            } };

            if ( level > 50)
            {
                //Don't want duplicate resources
                while (resourceb == resourcea)
                {
                    resourceb = GetBasicResource();
                }
                IFormLinkNullable<IItemGetter> commonresourceb = new FormKey(Starfield, resourceb).ToNullableLink<IItemGetter>();

                cost.Add(new ConstructibleObjectComponent()
                {
                    RequiredCount = (uint)(1 + random.Next(8)),
                    Component = commonresourceb
                });
            }
            if (level > 100)
            {
                uint resourcec = GetBasicResource();
                //Don't want duplicate resources
                while (resourcec == resourcea || resourcec == resourceb)
                {
                    resourcec = GetBasicResource();
                }
                IFormLinkNullable<IItemGetter> commonresourcec = new FormKey(Starfield, resourcec).ToNullableLink<IItemGetter>();

                cost.Add(new ConstructibleObjectComponent()
                {
                    RequiredCount = (uint)(1 + random.Next(10)),
                    Component = commonresourcec
                });
            }
            return cost;
        }
    }
}
