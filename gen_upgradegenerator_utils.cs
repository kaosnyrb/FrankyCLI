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

        public decimal Default;
        public decimal Step;
    }

    public static class gen_upgradegenerator_utils
    {
        public static Dictionary<string, BonusStats> StatBank = new Dictionary<string, BonusStats>();
        public static Dictionary<string,LevelStyle> levelStyles = new Dictionary<string, LevelStyle>();
        public static ThemeFile LoadedThemeFile = new ThemeFile();

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

        }

        public static void BuildStatBank(string statsfile)
        {
            StatBank = YamlImporter.getObjectFrom<Dictionary<string, BonusStats>>(statsfile);
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
                    return "Barrel";
                case "02249D:Starfield.esm":
                    return "Barrel";
                case "02EE28:Starfield.esm":
                    return "Laser";
                case "14D08A:Starfield.esm":
                    return "Laser";
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
                    return "Grip";
                case "147AFE:Starfield.esm":
                    return "Grip";
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
