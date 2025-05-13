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
        public int DamageMode;
        public List<string> StatLibFile;
        public string ScalingStats;
        public string ThemeFile;
        public List<string> Weapons;
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
        public decimal Default;
        public decimal Step;

        public List<string> OtherStats;
    }

    public static class gen_upgradegenerator_utils
    {
        public static Dictionary<string, BonusStats> StatBank = new Dictionary<string, BonusStats>();
        public static Dictionary<string,LevelStyle> levelStyles = new Dictionary<string, LevelStyle>();
        public static ThemeFile LoadedThemeFile = new ThemeFile();

        public static void AddStat(string statname,ref WeaponModification omod,ref string Description, ref string stattag, int step, bool silent)
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
                    AddStat(otherstat, ref omod, ref Description, ref stattag, step, true);
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
        }

        static Random random = new Random();
        public static void BuildLevelStyles()
        {
            int Standardstepcount = 1;
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
                startLevel = 50 + random.Next(150),
                StepCount = 1,
                LevelPerStep = 1,
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
                    return "Internal";
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

        public static Condition GetPartResearchReq(ModKey Starfield, int level, string part)
        {
            uint research = 0x00389F1B;
            IFormLinkOrIndex<IResearchProjectGetter> ResearchRequired =(IFormLinkOrIndex <IResearchProjectGetter>)new FormKey(Starfield, research).ToNullableLinkGetter<IResearchProjectGetter>();

            var con = new IsResearchCompleteConditionData()
            {
                FirstParameter = ResearchRequired
            };
            return new ConditionFloat()
            {
                Data = con,
                CompareOperator = CompareOperator.EqualTo,
                ComparisonValue = 1
            };
            /*
            var Skill_WeaponEngineeringuint = gen_upgradegenerator_utils.GetPerk("Skill_WeaponEngineering");
            IFormLinkNullable<IPerkGetter> Skill_WeaponEngineering = new FormKey(Starfield, Skill_WeaponEngineeringuint).ToNullableLink<IPerkGetter>();
            if (level >= 50 && level < 100)
            {
                co.RequiredPerks.Add(new ConstructibleRequiredPerk()
                {
                    Perk = Skill_WeaponEngineering,
                    Rank = 1
                });
            }
            if (level >= 100 && level < 175)
            {
                co.RequiredPerks.Add(new ConstructibleRequiredPerk()
                {
                    Perk = Skill_WeaponEngineering,
                    Rank = 2
                });
            }
            if (level >= 175 && level < 250)
            {
                co.RequiredPerks.Add(new ConstructibleRequiredPerk()
                {
                    Perk = Skill_WeaponEngineering,
                    Rank = 3
                });
            }
            if (level >= 250)
            {
                co.RequiredPerks.Add(new ConstructibleRequiredPerk()
                {
                    Perk = Skill_WeaponEngineering,
                    Rank = 4
                });
            }*/
        }

        public static ExtendedList<ConstructibleObjectComponent> GetUpgradeCost(ModKey Starfield,int level)
        {
            Random random = new Random();

            uint resourcea = GetBasicResource();
            uint resourceb = GetBasicResource();

            IFormLinkNullable<IItemGetter> commonresource = new FormKey(Starfield, resourcea).ToNullableLink<IItemGetter>();

            var cost = new ExtendedList<ConstructibleObjectComponent>() { new ConstructibleObjectComponent()
            {
                Count = (uint)(1 + random.Next(5)),
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
                    Count = (uint)(1 + random.Next(8)),
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
                    Count = (uint)(1 + random.Next(10)),
                    Component = commonresourcec
                });
            }
            return cost;
        }
    }
}
