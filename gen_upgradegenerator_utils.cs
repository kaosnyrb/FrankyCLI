using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI
{
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

        public LevelStyle LevelStyle;

        public string Theme = "Miltec";

        public List<BonusStats> stats;
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
        public static void BuildLevelStyles()
        {
            //We aim for 500 levels of content, these are how many tiers and how spread out the upgrades are
            levelStyles.Add("Basic", new LevelStyle
            {
                startLevel = 0,
                StepCount = 10,
                LevelPerStep = 10,
            });
            levelStyles.Add("Early", new LevelStyle
            {
                startLevel = 5,
                StepCount = 4,
                LevelPerStep = 5,
            });
            levelStyles.Add("50s", new LevelStyle
            {
                startLevel = 20,
                StepCount = 9,
                LevelPerStep = 50,
            });
            levelStyles.Add("25s", new LevelStyle
            {
                startLevel = 25,
                StepCount = 20,
                LevelPerStep = 25,
            });
            levelStyles.Add("Mid", new LevelStyle
            {
                startLevel = 50,
                StepCount = 5,
                LevelPerStep = 15,
            });
        }

        public static void BuildStatBank()
        {
            //Keep the scaling in one place.
            StatBank.Add("+Physical%", new BonusStats()
            {
                Type = "Float",
                Percentage = true,
                StatName = "Physical Damage",
                property = Weapon.Property.DamagePhysical,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = 0.1M,
                Step = 0.05M,
            });
            StatBank.Add("+Physical", new BonusStats()
            {
                Type = "Float",
                Percentage = false,
                StatName = "Physical Damage",
                property = Weapon.Property.DamagePhysical,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 4,
                Step = 4,
            });
            StatBank.Add("Projectiles+", new BonusStats()
            {
                Type = "Int",
                Percentage = false,
                StatName = "Projectiles",
                property = Weapon.Property.ProjectileCount,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 1,
                Step = 1,
            });
            StatBank.Add("AmmoCapacity+", new BonusStats()
            {
                Type = "Int",
                Percentage = false,
                StatName = "Ammo Capacity",
                property = Weapon.Property.AmmoCapacity,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 2,
                Step = 2,
            });
            StatBank.Add("+AmmoCapacity%", new BonusStats()
            {
                Type = "Float",
                Percentage = true,
                StatName = "Ammo Capacity",
                property = Weapon.Property.AmmoCapacity,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = 0.1M,
                Step = 0.1M,
            });
            StatBank.Add("Spread-", new BonusStats()
            {
                Type = "Float",
                Percentage = true,
                StatName = "Spread",
                property = Weapon.Property.AimModelConeMaxDegrees,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = -0.5M,
                Step = -0.05M,
            });
            StatBank.Add("CritDamage+", new BonusStats()
            {
                Type = "Float",
                Percentage = true,
                StatName = "Crit Damage",
                property = Weapon.Property.CriticalDamageMultiplier,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = 0.5M,
                Step = 0.10M,
            });
            StatBank.Add("BashDamage+", new BonusStats()
            {
                Type = "Float",
                Percentage = true,
                StatName = "Bash Damage",
                property = Weapon.Property.BashDamage,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = 1.0M,
                Step = 0.50M,
            });
            StatBank.Add("DamageTaken-", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Damage Taken",
                property = Weapon.Property.ActorValue,
                Keyword = 0x0030397A,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = -0.10M,
                Step = -0.05M,
            });
            StatBank.Add("+Energy%", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                Keyword = 0x00060A81,
                StatName = "Energy Damage",
                property = Weapon.Property.DamageTypeValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = 0.1M,
                Step = 0.05M,
            });
            StatBank.Add("+EM%", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                Keyword = 0x00023190,
                StatName = "EM Damage",
                property = Weapon.Property.DamageTypeValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.MultAndAdd,
                Default = 0.1M,
                Step = 0.05M,
            });
            StatBank.Add("+EM", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = false,
                Keyword = 0x00023190,
                StatName = "EM Damage",
                property = Weapon.Property.DamageTypeValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 10,
                Step = 5,
            });
            StatBank.Add("+EnergyDamage", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = false,
                Keyword = 0x00060A81,
                StatName = "Energy Damage",
                property = Weapon.Property.DamageTypeValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 10,
                Step = 5,
            });
            StatBank.Add("+Silent", new BonusStats()
            {
                Type = "Enum",
                Percentage = false,
                StatName = "Silencer",
                property = Weapon.Property.SoundLevel,
                Default = 2,
                Step = 0,
            });
            StatBank.Add("pen_+O2%", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "02 Costs",
                property = Weapon.Property.ActorValue,
                Keyword = 0x0022F93D,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 0.35M,
                Step = 0.05M,
            });
            StatBank.Add("-O2%", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "02 Costs",
                property = Weapon.Property.ActorValue,
                Keyword = 0x0022F93D,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = -0.35M,
                Step = -0.05M,
            });
            StatBank.Add("+XP%", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Bonus XP",
                property = Weapon.Property.ActorValue,
                Keyword = 0x002D873C,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 0.05M,
                Step = 0.05M,
            });
            StatBank.Add("pen_+Reload%", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Reload Speed",
                property = Weapon.Property.ActorValue,
                Keyword = 0x002D87C4,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 0.20M,
                Step = 0.05M,
            });
            StatBank.Add("-Reload%", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Reload Speed",
                property = Weapon.Property.ActorValue,
                Keyword = 0x002D87C4,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = -0.20M,
                Step = -0.05M,
            });
            StatBank.Add("+Regen", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = false,
                StatName = "Health Regen",
                property = Weapon.Property.ActorValue,
                Keyword = 0x000002D7,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 0.10M,
                Step = 0.05M,
            });
            StatBank.Add("+CarryWeight", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = false,
                StatName = "Carry Weight",
                property = Weapon.Property.ActorValue,
                Keyword = 0x000002DC,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 15,
                Step = 5,
            });
            StatBank.Add("+Jump", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Jump Strength",
                property = Weapon.Property.ActorValue,
                Keyword = 0x00040CDC,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 1M,
                Step = 0.25M,
            });
            StatBank.Add("+Movement", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                StatName = "Movement Speed",
                property = Weapon.Property.ActorValue,
                Keyword = 0x000002DA,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 0.10M,
                Step = 0.05M,
            });
            StatBank.Add("+StealthLight", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                Keyword = 0x002EC4F5,
                StatName = "Stealth Visibility",
                property = Weapon.Property.ActorValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = -0.2M,
                Step = -0.1M,
            });
            StatBank.Add("+StealthMovement", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = true,
                Keyword = 0x002EC4ED,
                StatName = "Stealth Tracking",
                property = Weapon.Property.ActorValue,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = -0.2M,
                Step = -0.1M,
            });
            StatBank.Add("+Health", new BonusStats()
            {
                Type = "KeywordFloat",
                Percentage = false,
                StatName = "Health",
                property = Weapon.Property.ActorValue,
                Keyword = 0x000002D4,
                floatFunctionType = ObjectModProperty.FloatFunctionType.Add,
                Default = 50M,
                Step = 50M,
            });

        }

        public static Dictionary<string, StatSet> BuildStatLib(int DamageMode)
        {
            BuildStatBank();
            BuildLevelStyles();
            var StatLib = new Dictionary<string, StatSet>();
            //Energy
            if (DamageMode == 0)
            {
                StatLib.Add("Blazetube", new StatSet()
                {
                    Name = "Blazetube",
                    Theme = "Sci",
                    Description = "Boosts Energy output and increases base Energy damage",
                    LevelStyle = levelStyles["25s"],
                    AllowedAttachPoints = new List<string>() { "Barrel" },
                    stats = new List<BonusStats>()
                    {
                        StatBank["+Energy%"],
                        StatBank["+EnergyDamage"],
                    }
                });
                StatLib.Add("Sunpierce", new StatSet()
                {
                    Name = "Sunpierce",
                    Theme = "Sci",
                    Description = "Energy-powered crits that hit like a solar flare",
                    LevelStyle = levelStyles["Mid"],
                    AllowedAttachPoints = new List<string>() { "Barrel" },
                    stats = new List<BonusStats>()
                    {
                        StatBank["+Energy%"],
                        StatBank["CritDamage+"],
                    }
                });

            }
            //EM
            if (DamageMode == 1)
            {
                StatLib.Add("Nullspire", new StatSet()
                {
                    Name = "Nullspire",
                    Theme = "Sci",
                    Description = "Maximizes Electromagnetic damage output",
                    LevelStyle = levelStyles["25s"],
                    AllowedAttachPoints = new List<string>() { "Laser" },
                    stats = new List<BonusStats>()
                    {
                        StatBank["+EM%"],
                        StatBank["+EM"],
                    }
                });
                StatLib.Add("Moonshock", new StatSet()
                {
                    Name = "Moonshock",
                    Theme = "Sci",
                    Description = "Emits stunning EM charges and grants extra XP",
                    LevelStyle = levelStyles["25s"],
                    AllowedAttachPoints = new List<string>() { "Laser" },
                    stats = new List<BonusStats>()
                    {
                        StatBank["+EM"],
                        StatBank["+XP%"],
                    }
                });
            }
            //Phys
            if (DamageMode == 2 || DamageMode == -1)
            {
                StatLib.Add("Slamshot", new StatSet()
                {
                    Name = "Slamshot",
                    Theme = "Miltec",
                    Description = "Adds base Physical damage and a Percentage increase",
                    LevelStyle = levelStyles["25s"],
                    AllowedAttachPoints = new List<string>() { "Receiver" },
                    stats = new List<BonusStats>()
                    {
                        StatBank["+Physical%"],
                        StatBank["+Physical"],
                    }
                });
                StatLib.Add("Frostline", new StatSet()
                {
                    Name = "Frostline",
                    Theme = "Sci",
                    Description = "Adds Physical damage and decreases reload time",
                    LevelStyle = levelStyles["Early"],
                    AllowedAttachPoints = new List<string>() { "Receiver" },
                    stats = new List<BonusStats>()
                    {
                        StatBank["+Physical"],
                        StatBank["-Reload%"],
                    }
                });
                StatLib.Add("Twilightbolt", new StatSet()
                {
                    Name = "Twilightbolt",
                    Theme = "Exp",
                    Description = "Critical strikes hit harder with added physical base",
                    LevelStyle = levelStyles["50s"],
                    AllowedAttachPoints = new List<string>() { "Barrel" },
                    stats = new List<BonusStats>()
                    {
                        StatBank["CritDamage+"],
                        StatBank["+Physical"],
                    }
                });
            }
            StatLib.Add("Flechette", new StatSet()
            {
                Name = "Flechette",
                Theme = "Miltec",
                Description = "Adds Physical damage Percentage and extra projectiles",
                LevelStyle = levelStyles["Basic"],
                AllowedAttachPoints = new List<string>() { "Receiver" },
                stats = new List<BonusStats>()
                    {
                        StatBank["+Physical%"],
                        StatBank["Projectiles+"],
                    }
            });
            StatLib.Add("Stockpile", new StatSet()
            {
                Name = "Stockpile",
                Theme = "Miltec",
                Description = "Adds extra capacity to the magazine",
                LevelStyle = levelStyles["Early"],
                AllowedAttachPoints = new List<string>() { "Magazine" },
                stats = new List<BonusStats>()
                    {
                        StatBank["AmmoCapacity+"]
                    }
            });
            StatLib.Add("Holdfast", new StatSet()
            {
                Name = "Holdfast",
                Theme = "Exp",
                Description = "Increases Stability",
                LevelStyle = levelStyles["Basic"],
                AllowedAttachPoints = new List<string>() { "Grip" },
                stats = new List<BonusStats>()
                    {
                        StatBank["Spread-"]
                    }
            });
            StatLib.Add("Skybind", new StatSet()
            {
                Name = "Skybind",
                Theme = "Exp",
                Description = "Enhances vertical and horizontal mobility",
                LevelStyle = levelStyles["Early"],
                AllowedAttachPoints = new List<string>() { "Grip" },
                stats = new List<BonusStats>()
                    {
                        StatBank["+Jump"],
                        StatBank["+Movement"],
                    }
            });
            StatLib.Add("Deadshot", new StatSet()
            {
                Name = "Deadshot",
                Theme = "Corp",
                Description = "Increases Critical Damage",
                LevelStyle = levelStyles["50s"],
                AllowedAttachPoints = new List<string>() { "Optic" },
                stats = new List<BonusStats>()
                    {
                        StatBank["CritDamage+"]
                    }
            });
            StatLib.Add("Bastion", new StatSet()
            {
                Name = "Bastion",
                Theme = "Sci",
                Description = "Reduces damage taken",
                LevelStyle = levelStyles["50s"],
                AllowedAttachPoints = new List<string>() { "Laser" },
                stats = new List<BonusStats>()
                    {
                        StatBank["DamageTaken-"],
                    }
            });
            StatLib.Add("Cryolens", new StatSet()
            {
                Name = "Cryolens",
                Theme = "Sci",
                Description = "Enhances EM output while reducing visabilty for stealth",
                LevelStyle = levelStyles["Mid"],
                AllowedAttachPoints = new List<string>() { "Optic" },
                stats = new List<BonusStats>()
                    {
                        StatBank["+EM"],
                        StatBank["+StealthLight"],
                    }
            });
            StatLib.Add("Stormrack", new StatSet()
            {
                Name = "Stormrack",
                Theme = "Sci",
                Description = "Greatly expands both base and scalable ammo capacity",
                LevelStyle = levelStyles["Basic"],
                AllowedAttachPoints = new List<string>() { "Magazine" },
                stats = new List<BonusStats>()
                    {
                        StatBank["AmmoCapacity+"],
                        StatBank["+AmmoCapacity%"],
                    }
            });
            StatLib.Add("Stormvein", new StatSet()
            {
                Name = "Stormvein",
                Theme = "Sci",
                Description = "Increases mag size and reloads faster",
                LevelStyle = levelStyles["Mid"],
                AllowedAttachPoints = new List<string>() { "Magazine" },
                stats = new List<BonusStats>()
                    {
                        StatBank["+AmmoCapacity%"],
                        StatBank["-Reload%"],
                    }
            });
            StatLib.Add("Lightcloak", new StatSet()
            {
                Name = "Lightcloak",
                Theme = "Corp",
                Description = "Cloaks in shadows and earns bonus experience",
                LevelStyle = levelStyles["Mid"],
                AllowedAttachPoints = new List<string>() { "Grip" },
                stats = new List<BonusStats>()
                    {
                        StatBank["+StealthLight"],
                        StatBank["+XP%"],
                    }
            });
            StatLib.Add("Neurogrip", new StatSet()
            {
                Name = "Neurogrip",
                Theme = "Corp",
                Description = "Increases health and recovery over time",
                LevelStyle = levelStyles["Early"],
                AllowedAttachPoints = new List<string>() { "Grip" },
                stats = new List<BonusStats>()
                    {
                        StatBank["+Health"],
                        StatBank["+Regen"],
                    }
            });
            StatLib.Add("Thunderdrop", new StatSet()
            {
                Name = "Thunderdrop",
                Theme = "Corp",
                Description = "Adds extra rounds and allows multi-projectile shots",
                LevelStyle = levelStyles["Mid"],
                AllowedAttachPoints = new List<string>() { "Magazine" },
                stats = new List<BonusStats>()
                    {
                        StatBank["AmmoCapacity+"],
                        StatBank["Projectiles+"],
                    }
            });
            StatLib.Add("Starflare", new StatSet()
            {
                Name = "Starflare",
                Theme = "Corp",
                Description = "Radiates high-output energy and damage",
                LevelStyle = levelStyles["Mid"],
                AllowedAttachPoints = new List<string>() { "Laser" },
                stats = new List<BonusStats>()
                    {
                        StatBank["+EnergyDamage"],
                        StatBank["+Energy%"],
                    }
            });
            StatLib.Add("Thornbolt", new StatSet()
            {
                Name = "Thornbolt",
                Theme = "Exp",
                Description = "Piercing shots that tear through enemies",
                LevelStyle = levelStyles["Mid"],
                AllowedAttachPoints = new List<string>() { "Barrel" },
                stats = new List<BonusStats>()
                    {
                        StatBank["+Physical"],
                        StatBank["BashDamage+"],
                    }
            });
            StatLib.Add("Driftstock", new StatSet()
            {
                Name = "Driftstock",
                Theme = "Exp",
                Description = "Nimble footwork with heavy hauls",
                LevelStyle = levelStyles["Mid"],
                AllowedAttachPoints = new List<string>() { "Grip" },
                stats = new List<BonusStats>()
                    {
                        StatBank["+Movement"],
                        StatBank["+CarryWeight"],
                    }
            });
            StatLib.Add("Starfang", new StatSet()
            {
                Name = "Starfang",
                Theme = "Sci",
                Description = "Balanced physical and Energy hybrid output",
                LevelStyle = levelStyles["50s"],
                AllowedAttachPoints = new List<string>() { "Receiver" },
                stats = new List<BonusStats>()
                    {
                        StatBank["+Physical"],
                        StatBank["+EnergyDamage"],
                    }
            });
            StatLib.Add("Mistveil", new StatSet()
            {
                Name = "Mistveil",
                Theme = "Corp",
                Description = "Improved O2 usage with cloaked movement",
                LevelStyle = levelStyles["50s"],
                AllowedAttachPoints = new List<string>() { "Optic" },
                stats = new List<BonusStats>()
                    {
                        StatBank["-O2%"],
                        StatBank["+StealthMovement"],
                    }
            });
            StatLib.Add("Howlround", new StatSet()
            {
                Name = "Howlround",
                Theme = "Miltec",
                Description = "More bullets, more stamina",
                LevelStyle = levelStyles["25s"],
                AllowedAttachPoints = new List<string>() { "Magazine" },
                stats = new List<BonusStats>()
                    {
                        StatBank["-O2%"],
                        StatBank["+AmmoCapacity%"],
                    }
            });
            StatLib.Add("Coretap", new StatSet()
            {
                Name = "Coretap",
                Theme = "Miltec",
                Description = "Slowly regenerates health and increases carrying capacity",
                LevelStyle = levelStyles["Basic"],
                AllowedAttachPoints = new List<string>() { "Grip" },
                stats = new List<BonusStats>()
                    {
                        StatBank["+Regen"],
                        StatBank["+CarryWeight"],
                    }
            });
            return StatLib;
        }
        public static string getDiscriptiveLevel(int level, string Theme)
        {
            level = level - (level % 10);
            if (Theme == "Miltec")
            {
                switch (level)
                {
                    case 0: return "Recruits";
                    case 10: return "Privates";
                    case 20: return "Corporals";
                    case 30: return "Sergeants";
                    case 40: return "Lieutenants";
                    case 50: return "Captains";
                    case 60: return "Majors";
                    case 70: return "Colonels";
                    case 80: return "Generals";
                    case 90: return "Warlords";
                    case 100: return "Ironclads";
                    case 110: return "Juggernauts";
                    case 120: return "Sentinels";
                    case 130: return "Overseers";
                    case 140: return "Wardens";
                    case 150: return "Executors";
                    case 160: return "Vanguards";
                    case 170: return "Spectres";
                    case 180: return "Phantoms";
                    case 190: return "Eclipses";
                    case 200: return "Voidwalkers";
                    case 210: return "Starborn";
                    case 220: return "Celestials";
                    case 230: return "Astrals";
                    case 240: return "Novaforge";
                    case 250: return "Pulsecasters";
                    case 260: return "Singulars";
                    case 270: return "Eventides";
                    case 280: return "Stellarions";
                    case 290: return "Dreadnoughts";
                    case 300: return "Xenoglyphs";
                    case 310: return "Elysians";
                    case 320: return "Myriads";
                    case 330: return "Nephilim";
                    case 340: return "Ascendants";
                    case 350: return "Exarchs";
                    case 360: return "Dominions";
                    case 370: return "Nulllords";
                    case 380: return "Obelisks";
                    case 390: return "Continuums";
                    case 400: return "Anomalons";
                    case 410: return "Paracrypts";
                    case 420: return "Aeons";
                    case 430: return "Eternals";
                    case 440: return "The Unseen";
                    case 450: return "The Forgotten";
                    case 460: return "Abyssals";
                    case 470: return "The Architects";
                    case 480: return "Realitybreakers";
                    case 490: return "Omnicrons";
                }
            }
            if (Theme == "Sci")
            {
                switch (level)
                {
                    case 0: return "Basic";
                    case 10: return "Improvised";
                    case 20: return "Calibrated";
                    case 30: return "Optimized";
                    case 40: return "Refined";
                    case 50: return "Tuned";
                    case 60: return "Harmonic";
                    case 70: return "Resonant";
                    case 80: return "Ionic";
                    case 90: return "Plasmic";
                    case 100: return "Fusion";
                    case 110: return "Cryogenic";
                    case 120: return "Nanophased";
                    case 130: return "Gravitic";
                    case 140: return "Magnetron";
                    case 150: return "Subatomic";
                    case 160: return "Isotopic";
                    case 170: return "Radiant";
                    case 180: return "Chronometric";
                    case 190: return "Dimensional";
                    case 200: return "Quantum";
                    case 210: return "Phase-Locked";
                    case 220: return "Neutrino";
                    case 230: return "Muon-Driven";
                    case 240: return "Antimatter";
                    case 250: return "Void-Linked";
                    case 260: return "Psionic";
                    case 270: return "Darkwave";
                    case 280: return "Anomalous";
                    case 290: return "Fractal";
                    case 300: return "Zero-Point";
                    case 310: return "Entropic";
                    case 320: return "Hypercooled";
                    case 330: return "Exotic";
                    case 340: return "Planck-Tuned";
                    case 350: return "Neural-Synced";
                    case 360: return "Bioadaptive";
                    case 370: return "Polymorphic";
                    case 380: return "Xeno-Integrated";
                    case 390: return "Causality-Bound";
                    case 400: return "Singularity";
                    case 410: return "Spacetime-Warped";
                    case 420: return "Astrometric";
                    case 430: return "Subspace";
                    case 440: return "Event-Horizon";
                    case 450: return "Tachyonic";
                    case 460: return "Hyperspectral";
                    case 470: return "Interlaced";
                    case 480: return "Transdimensional";
                    case 490: return "Cosmic-Infused";
                }
            }
            if(Theme == "Corp")
            {
                switch (level)
                {
                    case 0: return "Contractor-Grade";
                    case 10: return "Series-A";
                    case 20: return "Approved Use";
                    case 30: return "Professional";
                    case 40: return "Authorized";
                    case 50: return "Enterprise";
                    case 60: return "Division-Grade";
                    case 70: return "Executive";
                    case 80: return "Tier-1 Certified";
                    case 90: return "Licensable";
                    case 100: return "Corporate Issue";
                    case 110: return "Black Label";
                    case 120: return "Directors Cut";
                    case 130: return "Strategic-Model";
                    case 140: return "OmniCore";
                    case 150: return "Vanguard-Class";
                    case 160: return "NextPhase™";
                    case 170: return "BetaLine";
                    case 180: return "Synthesis-Tech";
                    case 190: return "FutureProof";
                    case 200: return "NeuroSpec";
                    case 210: return "Redacted Series";
                    case 220: return "Skunkworks";
                    case 230: return "Internal Use Only";
                    case 240: return "Obsidian Tier";
                    case 250: return "Quantum Asset";
                    case 260: return "Zeta Compliance";
                    case 270: return "AlphaSuite";
                    case 280: return "Sentinel-Grade";
                    case 290: return "Project Orion";
                    case 300: return "Clearance-5";
                    case 310: return "Division Zero";
                    case 320: return "X-Series";
                    case 330: return "Echelon Verified";
                    case 340: return "Titanium Protocol";
                    case 350: return "NDA-Bound";
                    case 360: return "Continuum-Tagged";
                    case 370: return "Level Black";
                    case 380: return "Vault-Locked";
                    case 390: return "Echo Model";
                    case 400: return "Primacy Line";
                    case 410: return "Blacksite Alpha";
                    case 420: return "Atlas Variant";
                    case 430: return "Boardroom Edition";
                    case 440: return "Infinity Rights";
                    case 450: return "Legacy Codebase";
                    case 460: return "Mnemonic Series";
                    case 470: return "Helix Certified";
                    case 480: return "Hyperledger";
                    case 490: return "OmniSpec";
                }
            }
            if (Theme == "Exp")
            {
                switch (level)
                {
                    case 0: return "Field-Issue";
                    case 10: return "Trailworn";
                    case 20: return "Surveyors";
                    case 30: return "Scout-Tuned";
                    case 40: return "Pathfinder";
                    case 50: return "Expedition-Grade";
                    case 60: return "Deep Range";
                    case 70: return "Frontier";
                    case 80: return "Longstride";
                    case 90: return "Astro-Hardened";
                    case 100: return "Nomad-Class";
                    case 110: return "Voidwalker";
                    case 120: return "Charted Tech";
                    case 130: return "Navigator Spec";
                    case 140: return "Outpost-Calibrated";
                    case 150: return "Drifters Mark";
                    case 160: return "Terraform-Ready";
                    case 170: return "Cosmos-Rated";
                    case 180: return "Orbital Survey";
                    case 190: return "Planetfall";
                    case 200: return "Starfarer";
                    case 210: return "Grav-Adapted";
                    case 220: return "Cartographer Core";
                    case 230: return "Exoplanetary";
                    case 240: return "Deep-Vacuum";
                    case 250: return "Xeno-Calibrated";
                    case 260: return "Echo-Mapped";
                    case 270: return "Warp-Trail";
                    case 280: return "Eventide Series";
                    case 290: return "Skybreaker";
                    case 300: return "Solstice Frame";
                    case 310: return "Seekers Edition";
                    case 320: return "Pioneer-Tagged";
                    case 330: return "Cryo-Hardened";
                    case 340: return "Expanse-Forged";
                    case 350: return "Aetherbound";
                    case 360: return "Dustline";
                    case 370: return "Nova-Tuned";
                    case 380: return "Cliffwalker";
                    case 390: return "Vault-Marked";
                    case 400: return "Starborn Spec";
                    case 410: return "Rift-Touched";
                    case 420: return "Mythos-Tech";
                    case 430: return "Wayfarers Tier";
                    case 440: return "Stellar-Linked";
                    case 450: return "Outlands-Class";
                    case 460: return "Remnant Enhanced";
                    case 470: return "Ecliptic-Modified";
                    case 480: return "Lighthouse Model";
                    case 490: return "Beyond-Classified";
                    case 500: return "Infinity-Tier";
                }
            }
            if (level >= 500)
            {
                return "Starborn";
            }
            return "Recruits";
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
            }
            Console.WriteLine("Missing Attach Form:" + form);
            return "";
        }

        public static Dictionary<string, string> GetCOMap()
        {
            //This manually fixes some wierdness in co naming conventions
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
            return comap;
        }
    }
}
