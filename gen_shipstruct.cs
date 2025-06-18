using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI
{
    class gen_shipstruct
    {
        public static int Generate(string[] args)
        {
            Random random = new Random();
            StarfieldMod myMod;
            string modname = args[0];
            string mode = args[1];
            string prefix = args[2];
            string item = args[3];
            string modelpath = args[4];

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

                            //Check if this mod already contains this entry
                            foreach ( var ms in myMod.MoveableStatics)
                            {
                                if (ms.EditorID == prefix + "_ms_" + item)
                                {
                                    Console.WriteLine("Error, mod already contains : " + prefix + "_ms_" + item);
                                    return 1;
                                }
                            }
                    
                        }
                    }
                }

                // Moveable Static ------------------------------------------
                Console.WriteLine("Building Record : " + prefix + "_ms_" + item);
                IFormLinkNullable<ISnapTemplateGetter> snaplink = new FormKey(env.LoadOrder[0].ModKey, 0x00059B01).ToNullableLink<ISnapTemplateGetter>();
                IFormLinkNullable<ILayeredMaterialSwapGetter> paint1 = new FormKey(env.LoadOrder[0].ModKey, 0x00099196).ToNullableLink<ILayeredMaterialSwapGetter>();
                IFormLinkNullable<ILayeredMaterialSwapGetter> paint2 = new FormKey(env.LoadOrder[0].ModKey, 0x000B6B1F).ToNullableLink<ILayeredMaterialSwapGetter>();
                IFormLinkNullable<ILayeredMaterialSwapGetter> paint3 = new FormKey(env.LoadOrder[0].ModKey, 0x002AF78A).ToNullableLink<ILayeredMaterialSwapGetter>();
                IFormLinkNullable<IKeywordGetter> spaceshipformshipmodule = new FormKey(env.LoadOrder[0].ModKey, 0x001BB401).ToNullableLink<IKeywordGetter>();
                IFormLinkNullable<IKeywordGetter> NavmeshUseDefaultCollisionForGeneration = new FormKey(env.LoadOrder[0].ModKey, 0x00207960).ToNullableLink<IKeywordGetter>();

                byte[] flldarry = new byte[4] { 1, 0, 0, 0 };
                byte[] xflgarry = new byte[1] { 2 };

                MoveableStatic moveableStatic = new MoveableStatic(myMod);
                moveableStatic.EditorID = prefix + "_ms_" + item;
                moveableStatic.ObjectBounds = new ObjectBounds()
                {
                    First = new P3Float(-4, -4, -1.767578f),
                    Second = new P3Float(4, 4, 1.767578f)
                };
                moveableStatic.DirtinessScale = new Percent(0);
                moveableStatic.SnapTemplate = snaplink;
                moveableStatic.Model = new Model()
                {
                    File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>(modelpath),
                    MaterialSwaps = new ExtendedList<IFormLinkGetter<ILayeredMaterialSwapGetter>>()
                    {
                        paint1,
                        paint2,
                        paint3,
                    },
                    LightLayer = 1,
                    Flags = Model.Flag.HasFirstPersonModel,
                };
                moveableStatic.DATA = 4;
                moveableStatic.Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>()
                {
                    spaceshipformshipmodule,
                    NavmeshUseDefaultCollisionForGeneration
                };
                myMod.MoveableStatics.Add(moveableStatic);


                //Cell---------------------------

                /*
                ElminsterAU
                Block and sub-block address the last 2 digits of the object id of the record converted to decimal
                You have no control over these 
                Then it's broken. Because the game engine depends on the records being in the correct block/sub-block to find them
                */
                IFormLinkNullable<IImageSpaceGetter> DefaultImagespacePackin = new FormKey(env.LoadOrder[0].ModKey, 0x0006AD68).ToNullableLink<IImageSpaceGetter>();
                Console.WriteLine("Building Record : " + prefix + "_cell_" + item);
                var newCell = new Cell(myMod)
                {
                    EditorID = prefix + "_cell_" + item,
                    Temporary = new ExtendedList<IPlaced>(),
                    Flags = Cell.Flag.IsInteriorCell,
                    Lighting = new CellLighting()
                    {
                        DirectionalFade = 1,
                        FogPower = 1,
                        FogMax = 1,
                        NearHeightRange = 10000,
                        Unknown1 = 1951,
                        InteriorType = Cell.InteriorType.PackIn,
                    },
                    WaterHeight = 0,
                    XILS = 1.0f,
                    XCLAs = new ExtendedList<CellXCLAItem>()
                    {
                        new CellXCLAItem()
                        {
                            XCLA = 1,
                            XCLD = "Default Layer Name 1"
                        },
                        new CellXCLAItem()
                        {
                            XCLA = 2,
                            XCLD = "Default Layer Name 2"
                        },
                        new CellXCLAItem()
                        {
                            XCLA = 3,
                            XCLD = "Default Layer Name 3"
                        },
                        new CellXCLAItem()
                        {
                            XCLA = 4,
                            XCLD = "Default Layer Name 4"
                        },
                    },
                    ImageSpace = DefaultImagespacePackin,

                };
                var key = newCell.FormKey.ID;
                var stringkey = key.ToString();
                var cellblockNumber = int.Parse(stringkey.Substring(stringkey.Length - 1));
                var subBlockNumber = int.Parse(stringkey.Substring(stringkey.Length - 2, 1));

                //Try and use existing cellblocks and subblocks first.
                CellBlock cellblock = null;
                bool newCellBlock = false;
                for( int i = 0; i < myMod.Cells.Count; i++ )
                {
                    if (myMod.Cells[i].BlockNumber == cellblockNumber )
                    {
                        cellblock = myMod.Cells[i];
                    }
                }
                if (cellblock == null )
                {
                    cellblock = new CellBlock
                    {
                        BlockNumber = cellblockNumber,
                        GroupType = GroupTypeEnum.InteriorCellBlock,
                        SubBlocks = new ExtendedList<CellSubBlock>()
                    };
                    newCellBlock = true;
                }

                bool addSubblock = true;
                for(int i = 0; i < cellblock.SubBlocks.Count; i++ )
                {
                    if (cellblock.SubBlocks[i].BlockNumber == subBlockNumber)
                    {
                        addSubblock = false;
                    }
                }
                if (addSubblock)
                {
                    cellblock.SubBlocks.Add(new CellSubBlock()
                    {
                        BlockNumber = subBlockNumber,
                        GroupType = GroupTypeEnum.InteriorCellSubBlock,
                        Cells = new ExtendedList<Cell>()
                    });
                }


                // Cell contents -------------------------------
                IFormLink<IPlaceableObjectGetter> OutpostGroupPackinDummy = new FormKey(env.LoadOrder[0].ModKey, 0x00015804).ToLink<IPlaceableObjectGetter>();
                IFormLink<IPlaceableObjectGetter> PrefabPackinPivotDummy = new FormKey(env.LoadOrder[0].ModKey, 0x0003F808).ToLink<IPlaceableObjectGetter>();
                IFormLink<IKeywordGetter> UpdatesDynamicNavmeshKeyword = new FormKey(env.LoadOrder[0].ModKey, 0x00140158).ToLink<IKeywordGetter>();
                Console.WriteLine("Building Cell Contents");
                newCell.Temporary.Add(new PlacedObject(myMod)
                {
                    Base = OutpostGroupPackinDummy,
                    Position = new P3Float(0, 0, 0),
                    Rotation = new P3Float(0, 0, 0)
                });
                newCell.Temporary.Add(new PlacedObject(myMod)
                {
                    Base = PrefabPackinPivotDummy,
                    Position = new P3Float(0, 0, 0),
                    Rotation = new P3Float(0, 0, 0)
                });
                var cell_contents_components = new ExtendedList<AComponent>()
                {
                    new KeywordFormComponent()
                    {
                        Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>()
                        {
                            UpdatesDynamicNavmeshKeyword
                        }
                    }
                };
                newCell.Temporary.Add(new PlacedObject(myMod)
                {
                    Base = moveableStatic.ToLink<IPlaceableObjectGetter>(),
                    //Not sure we need ragdoll data, but just copying what I know works
                    RagdollData = new ExtendedList<RagdollData>()
                    {
                        new RagdollData()
                        {
                            BoneId = 0,
                            Position = new P3Float(0, 0, 0),
                            Rotation = new P3Float(0, 0, 0)
                        }
                    },
                    Components = cell_contents_components,
                    Position = new P3Float(0, 0, 0),
                    Rotation = new P3Float(0, 0, 0)
                });
                
                bool addedCell = false;
                for (int i = 0; i < cellblock.SubBlocks.Count && !addedCell; i++)
                {
                    if (cellblock.SubBlocks[i].BlockNumber == subBlockNumber)
                    {
                        cellblock.SubBlocks[i].Cells.Add(newCell);
                        addedCell = true;
                    }
                }
                if(newCellBlock)
                {
                    myMod.Cells.Add(cellblock);
                }


                // Packin --------------------------------------
                Console.WriteLine("Building Record : " + prefix + "_pkn_" + item);
                IFormLink<ITransformGetter> link = new FormKey(env.LoadOrder[0].ModKey, 0x00050FAC).ToLink<ITransformGetter>();

                byte[] barray = new byte[4] { 14, 00, 00, 00 };
                var packin = new PackIn(myMod)
                {
                    EditorID = prefix + "_pkn_" + item,
                    ObjectBounds = new ObjectBounds()
                    {
                        First = new P3Float(-4, -4, -1.767578f),
                        Second = new P3Float(4, 4, 1.767578f)
                    },
                    DirtinessScale = new Percent(0),
                    Transforms = new Transforms
                    {
                        Ship = link
                    },
                    Filter = "\\Ships\\Modules\\Exterior\\Struct\\Deimos\\",
                    Cell = newCell.ToNullableLink<ICellGetter>(),
                    Version = 0,
                    FNAM = new MemorySlice<byte>(barray),
                    MaterialSwaps = new ExtendedList<IFormLinkGetter<ILayeredMaterialSwapGetter>>()
                };
                myMod.PackIns.Add(packin);

                //Generic Base Form -------------------------------------------
                IFormLinkNullable<IGenericBaseFormTemplateGetter> FormSpaceshipModule = new FormKey(env.LoadOrder[0].ModKey, 0x0003058E).ToNullableLink<IGenericBaseFormTemplateGetter>();
                IFormLinkNullable<IActorValueInformationGetter> SpaceshipPartMass = new FormKey(env.LoadOrder[0].ModKey, 0x0000ACDB).ToNullableLink<IActorValueInformationGetter>();
                IFormLinkNullable<IActorValueInformationGetter> ShipModuleVariant = new FormKey(env.LoadOrder[0].ModKey, 0x0027BACE).ToNullableLink<IActorValueInformationGetter>();
                IFormLinkNullable<IKeywordGetter> SpaceshipLinkedExterior = new FormKey(env.LoadOrder[0].ModKey, 0x0000662F).ToNullableLink<IKeywordGetter>();
                IFormLinkNullable<IKeywordGetter> ShipModuleManufacturerDeimos = new FormKey(env.LoadOrder[0].ModKey, 0x001462C0).ToNullableLink<IKeywordGetter>();
                Console.WriteLine("Building Record : " + prefix + "_gbfm_" + item);
                var gbfm_components = new ExtendedList<AComponent>()
                {
                    new PropertySheetComponent()
                    {
                        Properties = new ExtendedList<ObjectProperty>()
                        {
                            new ObjectProperty()
                            {
                                ActorValue = SpaceshipPartMass,
                                Value = 5,
                            },
                            new ObjectProperty()
                            {
                                ActorValue = ShipModuleVariant,
                                Value = 1,
                            }
                        }
                    },
                    new FormLinkDataComponent()
                    {
                        Links=  new ExtendedList<FormLinkComponentLink>
                        {
                            new FormLinkComponentLink()
                            {
                                LinkedForm = packin.ToNullableLink<IStarfieldMajorRecordGetter>(),
                                Keyword = SpaceshipLinkedExterior,
                            }
                        }
                    },
                    new KeywordFormComponent()
                    {
                        Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>()
                        {
                            ShipModuleManufacturerDeimos
                        }
                    },
                    new FullNameComponent()
                    {
                        Name = item
                    }
                };
                var gbfm = new GenericBaseForm(myMod)
                {
                    EditorID = prefix + "_gbfm_" + item,
                    ObjectBounds = new ObjectBounds() { First = new P3Float(0, 0, 0), Second = new P3Float(0, 0, 0) },
                    DirtinessScale = new Percent(0),
                    Template = FormSpaceshipModule,
                    Components = gbfm_components,
                    ObjectTemplateInstanceData = new ExtendedList<string>()
                    {
                        "BGSMod_Template_Component"
                    }
                };
                myMod.GenericBaseForms.Add(gbfm);

                //Constructable object -------------------------
                Console.WriteLine("Building Record : " + prefix + "_co_" + item);
                byte tnamearry = 00;
                IFormLinkNullable<IKeywordGetter> WorkbenchShipBuildingKeyword = new FormKey(env.LoadOrder[0].ModKey, 0x0029C480).ToNullableLink<IKeywordGetter>();
                IFormLinkNullable<IKeywordGetter> Category_ShipMod_Structure = new FormKey(env.LoadOrder[0].ModKey, 0x0029C473).ToNullableLink<IKeywordGetter>();

                var co = new ConstructibleObject(myMod)
                {
                    EditorID = prefix + "_co_" + item,
                    Description = item,
                    CreatedObject = gbfm.ToNullableLink<IConstructibleObjectTargetGetter>(),
                    AmountProduced = 1,
                    MenuSortOrder = 1,
                    Tier = 0,
                    LearnMethod = ConstructibleObject.LearnMethodEnum.DefaultOrConditions,
                    Value = 1000,
                    WorkbenchKeyword = WorkbenchShipBuildingKeyword,
                    RecipeFilters = new ExtendedList<IFormLinkGetter<IKeywordGetter>>()
                    {
                        Category_ShipMod_Structure
                    },
                };

                myMod.ConstructibleObjects.Add(co);
                // Finish up ---------------------------------------------
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
            {
                rec.IsCompressed = false;
            }

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm");
            Console.WriteLine("Finished");
            return 0;
        }
    }
}
