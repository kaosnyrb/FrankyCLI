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
    public class gen_upgradegenerator
    {
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
            
                int value = 100;
                string type = "test01";
                string editorid = value + "_" + type;

                //Add Book
                var book = new Book(myMod)
                {
                    EditorID = "book_" + editorid,
                    ObjectBounds = new ObjectBounds(),
                    Transforms = new Transforms(),
                    Name = value + "_" + type,
                    Model = new Model()
                    {
                        File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>("avontech\\warpblueprint.nif"),
                    },
                    Description = "A blueprint for a warptech barrel",
                    Value = 1000,
                    Weight = 0.1f
                };
                myMod.Books.Add(book);

                //Add OMOD
                IFormLinkNullable<IObjectModification> _Template_mod_Weapon_Barrel_Short = new FormKey(env.LoadOrder[0].ModKey, 0x0014AFDA).ToNullableLink<IObjectModification>();
                IFormLinkNullable<IKeywordGetter> ma_Beowulf = new FormKey(env.LoadOrder[0].ModKey, 0x001F70E6).ToNullableLink<IKeywordGetter>();
                IFormLinkNullable<IKeywordGetter> if_Barrel_Long = new FormKey(env.LoadOrder[0].ModKey, 0x0003E063).ToNullableLink<IKeywordGetter>();

                IFormLinkNullable<IKeywordGetter> ap_gun_Barrel = new FormKey(env.LoadOrder[0].ModKey, 0x0002249D).ToNullableLink<IKeywordGetter>();
                IFormLinkNullable<IKeywordGetter> ap_gun_BarrelDisplay = new FormKey(env.LoadOrder[0].ModKey, 0x00035799).ToNullableLink<IKeywordGetter>();
                IFormLinkNullable<IKeywordGetter> ap_gun_Muzzle = new FormKey(env.LoadOrder[0].ModKey, 0x0002249C).ToNullableLink<IKeywordGetter>();
                IFormLinkNullable<IKeywordGetter> ap_gun_ProjectileNode = new FormKey(env.LoadOrder[0].ModKey, 0x0004B6E1).ToNullableLink<IKeywordGetter>();

                var omod = new WeaponModification(myMod)
                {
                    EditorID = "omod_" + editorid,
                    Name = editorid,
                    Model = new Model() { File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>("Weapons\\Beowulf\\Beowulf_Barrel_Short.nif") },
                    TargetOmodKeywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>() { ma_Beowulf },
                    FilterKeywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>() { if_Barrel_Long },
                    AttachPoint = ap_gun_Barrel,
                    AttachParentSlots = new ExtendedList<IFormLinkGetter<IKeywordGetter>>() { ap_gun_BarrelDisplay, ap_gun_Muzzle, ap_gun_ProjectileNode },
                };

                omod.Includes.Add(new ObjectModInclude()
                {
                    Mod = _Template_mod_Weapon_Barrel_Short,
                    MinimumLevel = 0,
                    DoNotUseAll = true,
                    Optional = false
                });
                omod.Properties.Add(new ObjectModIntProperty<Weapon.Property>
                {
                    Property = Weapon.Property.AmmoCapacity,
                    Value = 22,
                    FunctionType = ObjectModProperty.FloatFunctionType.Add
                });
                myMod.ObjectModifications.Add(omod);

                //Add Construct
                IFormLinkNullable<IKeywordGetter> WorkbenchWeaponKeyword = new FormKey(env.LoadOrder[0].ModKey, 0x002CE1C0).ToNullableLink<IKeywordGetter>();
                IFormLinkNullable<IKeywordGetter> RecipeGunModBarrel = new FormKey(env.LoadOrder[0].ModKey, 0x002D01FC).ToNullableLink<IKeywordGetter>();                
                IFormLinkNullable<IConstructibleObjectTargetGetter> targetmod = omod.FormKey.ToNullableLink<IConstructibleObjectTargetGetter>();
                
                ConstructibleObject constructibleObject = new ConstructibleObject(myMod)
                {
                    EditorID = editorid,
                    Description = editorid,
                    WorkbenchKeyword = WorkbenchWeaponKeyword,
                    CreatedObject = targetmod,
                    AmountProduced = 1,
                    LearnMethod = ConstructibleObject.LearnMethodEnum.DefaultOrConditions,
                    Value = 0,
                    Categories = new ExtendedList<IFormLinkGetter<IKeywordGetter>>()
                };
                constructibleObject.Categories.Add(RecipeGunModBarrel);

                constructibleObject.ConstructableComponents = new ExtendedList<ConstructibleObjectComponent>() {
                new ConstructibleObjectComponent()
                    {
                        Count = 1,
                        Component = book.ToLink<IItemGetter>(),
                    } 
                };

                var con = new GetItemCountConditionData()
                {
                    RunOnType = Condition.RunOnType.Subject,
                };
                con.FirstParameter = new FormLinkOrIndex<IPlaceableObjectGetter>(con, book.FormKey);
                constructibleObject.Conditions.Add(new ConditionFloat()
                {
                    Data = con,
                    CompareOperator = CompareOperator.GreaterThan,
                    ComparisonValue = 0
                }) ;
                myMod.ConstructibleObjects.Add(constructibleObject);
            }
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm");
            Console.WriteLine("Finished");
            return 0;
        }
    }
}
