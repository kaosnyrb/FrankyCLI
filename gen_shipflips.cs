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
using DynamicData;

namespace FrankyCLI
{

    enum directions
    {
        ShipModPositionFore,
        ShipModPositionAft,
        ShipModPositionBottom,
        ShipModPositionTop,
        ShipModPositionStbd,
        ShipModPositionPort
    }
    class gen_shipflips
    {
        public static float EulerToRadCardinals(int euler)
        {
            switch (euler)
            {
                case 0:
                    return 0;
                case 90:
                    return 1.57079632f;
                case 180:
                    return 3.14159f;
                case 270:
                    return 4.71239f;
            }
            return 0;
        }

        public static ExtendedList<SnapNodeEntry> CalculateNodes(directions direction, ExtendedList<SnapNodeEntry> nodes, IGameEnvironment env)
        {
            //SnapNode_SHIP_Fore01[STND: 0004AB6F]
            //SnapNode_SHIP_Aft01 [STND:0004AB70 ]   

            //SnapNode_SHIP_Top01 [STND:0004AB77]       
            //SnapNode_SHIP_Bottom01 [STND:0004AB78]

            //SnapNode_SHIP_Starboard01 [STND:0004AB74]
            //SnapNode_SHIP_Port01 [STND:0004AB73]

            //            IFormLinkNullable<IForm> SnapNode_SHIP_Fore01 = new FormKey(env.LoadOrder[0].ModKey, 0x00059B01).ToNullableLink<ISnapTemplateGetter>();
            int fore = 306031;
            int aft = 306032;

            int top = 306039;
            int bottom = 306040;
            
            int starboard = 306036;
            int port = 306035;


            IFormLinkNullable<ISnapTemplateNodeGetter> ForeKey = new FormKey(env.LoadOrder[0].ModKey, 0x0004AB6F).ToNullableLink<ISnapTemplateNodeGetter>();
            IFormLinkNullable<ISnapTemplateNodeGetter> AftKey = new FormKey(env.LoadOrder[0].ModKey, 0x0004AB70).ToNullableLink<ISnapTemplateNodeGetter>();

            IFormLinkNullable<ISnapTemplateNodeGetter> TopKey = new FormKey(env.LoadOrder[0].ModKey, 0x0004AB77).ToNullableLink<ISnapTemplateNodeGetter>();
            IFormLinkNullable<ISnapTemplateNodeGetter> BottomKey = new FormKey(env.LoadOrder[0].ModKey, 0x0004AB78).ToNullableLink<ISnapTemplateNodeGetter>();

            IFormLinkNullable<ISnapTemplateNodeGetter> StarboardKey = new FormKey(env.LoadOrder[0].ModKey, 0x0004AB74).ToNullableLink<ISnapTemplateNodeGetter>();
            IFormLinkNullable<ISnapTemplateNodeGetter> PortKey = new FormKey(env.LoadOrder[0].ModKey, 0x0004AB73).ToNullableLink<ISnapTemplateNodeGetter>();


            if (direction == directions.ShipModPositionFore)
            {
                //Err this is what you passed in.
                return nodes;
            }

            if (direction == directions.ShipModPositionAft)
            {
                //For aft we swap
                //Fore and Aft
                //Port and Starboard
                ExtendedList<SnapNodeEntry> results = new ExtendedList<SnapNodeEntry>();
                foreach (var node in nodes)
                {
                    if (node.Node.FormKey.ID == fore)
                    {
                        SnapNodeEntry newnode = new SnapNodeEntry() { 
                            Node = AftKey,
                            NodeID = node.NodeID, 
                            Rotation = node.Rotation,
                            Offset = node.Offset,                            
                        };
                        results.Add(newnode);
                    }
                    if (node.Node.FormKey.ID == aft)
                    {
                        SnapNodeEntry newnode = new SnapNodeEntry() { 
                            Node = ForeKey,
                            NodeID = node.NodeID, 
                            Rotation = node.Rotation,
                            Offset = node.Offset,                            
                        };
                        results.Add(newnode);
                    }
                    if (node.Node.FormKey.ID == starboard)
                    {
                        SnapNodeEntry newnode = new SnapNodeEntry()
                        {
                            Node = PortKey,
                            NodeID = node.NodeID,
                            Rotation = node.Rotation,
                            Offset = node.Offset,
                        };
                        results.Add(newnode);
                    }
                    if (node.Node.FormKey.ID == port)
                    {
                        SnapNodeEntry newnode = new SnapNodeEntry()
                        {
                            Node = StarboardKey,
                            NodeID = node.NodeID,
                            Rotation = node.Rotation,
                            Offset = node.Offset,
                        };
                        results.Add(newnode);
                    }
                    if (node.Node.FormKey.ID == top)
                    {
                        results.Add(node);
                    }
                    if (node.Node.FormKey.ID == bottom)
                    {
                        results.Add(node);
                    }
                }
                return results;
            }

            return nodes;
        }

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

                //Declares
                IFormLinkNullable<ISnapTemplateGetter> snaplink = new FormKey(env.LoadOrder[0].ModKey, 0x00059B01).ToNullableLink<ISnapTemplateGetter>();
                IFormLinkNullable<ILayeredMaterialSwapGetter> paint1 = new FormKey(env.LoadOrder[0].ModKey, 0x00099196).ToNullableLink<ILayeredMaterialSwapGetter>();
                IFormLinkNullable<ILayeredMaterialSwapGetter> paint2 = new FormKey(env.LoadOrder[0].ModKey, 0x000B6B1F).ToNullableLink<ILayeredMaterialSwapGetter>();
                IFormLinkNullable<ILayeredMaterialSwapGetter> paint3 = new FormKey(env.LoadOrder[0].ModKey, 0x002AF78A).ToNullableLink<ILayeredMaterialSwapGetter>();
                IFormLinkNullable<IKeywordGetter> spaceshipformshipmodule = new FormKey(env.LoadOrder[0].ModKey, 0x001BB401).ToNullableLink<IKeywordGetter>();
                IFormLinkNullable<IKeywordGetter> NavmeshUseDefaultCollisionForGeneration = new FormKey(env.LoadOrder[0].ModKey, 0x00207960).ToNullableLink<IKeywordGetter>();

                //What this does
                // Generates all the flips of a single moveable static

                //Steps:
                MoveableStatic target = null;
                foreach(var ms in myMod.MoveableStatics)
                {
                    if (ms.EditorID == item)
                    {
                        target = ms;
                    }
                }

                //For each flip direction
                //Make Formlist of all the flips and add to it while building
                FormList FlipsList = new FormList(myMod)
                {
                    EditorID = prefix + "_" + target.EditorID + "_" + "franky",
                };


                List<directions> flips = new List<directions>() { directions.ShipModPositionFore, directions.ShipModPositionAft };
                foreach (var direction in flips)
                {
                    //1: find and clone the moveable static 
                    byte[] flldarry = new byte[4] { 1, 0, 0, 0 };
                    byte[] xflgarry = new byte[1] { 2 };

                    MoveableStatic moveableStatic = new MoveableStatic(myMod)
                    {
                        EditorID = item + direction.ToString(),
                        ObjectBounds = target.ObjectBounds,
                        ODTY = target.ODTY,
                        Model = target.Model,
                        DATA = target.DATA,
                        Keywords = target.Keywords
                    };

                    //2: make a new snap template with the snappoints swapped
                    SnapTemplate oldsnap = null;
                    foreach (var st in myMod.SnapTemplates)
                    {
                        if (st.FormKey == target.SnapTemplate.FormKey)
                        {
                            oldsnap = st;
                        }
                    }
                    
                    SnapTemplate snapTemplate = new SnapTemplate(myMod)
                    {
                        EditorID = prefix + "_sn_" + target.EditorID + direction.ToString(),
                    };


                    //Flip logic
                    var nodes = CalculateNodes(direction, oldsnap.Nodes,env);
                    foreach (var node in nodes)
                    {
                        snapTemplate.Nodes.Add(node);
                    }

                    myMod.SnapTemplates.Add(snapTemplate);

                    moveableStatic.SnapTemplate = snapTemplate.ToNullableLink<ISnapTemplateGetter>();

                    myMod.MoveableStatics.Add(moveableStatic);

                    //3: create a new cell with the new moveable static rotated correctly
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
                        EditorID = prefix + "_cell_" + item + direction.ToString(),
                        Temporary = new ExtendedList<IPlaced>(),
                        Flags = Cell.Flag.IsInteriorCell,
                        Lighting = new CellLighting()
                        {
                            DirectionalFade = 1,
                            FogPower = 1,
                            FogMax = 1,
                            NearHeightRange = 10000,
                            Unknown1 = 1951,
                            Unknown2 = 3,
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

                    int rotation = 0;
                    if (direction == directions.ShipModPositionFore) rotation = 0;
                    if (direction == directions.ShipModPositionAft) rotation = 180;
                    if (direction == directions.ShipModPositionPort) rotation = 90;
                    if (direction == directions.ShipModPositionStbd) rotation = 270;


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
                        Rotation = new P3Float(0, 0, EulerToRadCardinals(rotation))
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

                    //5: Packin
                    Console.WriteLine("Building Record : " + prefix + "_pkn_" + item);
                    IFormLink<ITransformGetter> link = new FormKey(env.LoadOrder[0].ModKey, 0x00050FAC).ToLink<ITransformGetter>();

                    byte[] barray = new byte[4] { 14, 00, 00, 00 };
                    var packin = new PackIn(myMod)
                    {
                        EditorID = prefix + "_pkn_" + item + direction.ToString(),
                        ObjectBounds = new ObjectBounds()
                        {
                            First = new P3Float(-4, -4, -1.767578f),
                            Second = new P3Float(4, 4, 1.767578f)
                        },
                        ODTY = 0,
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

                    //6: GBFM ( ShipModPositionPort [KYWD:0027BAC5] )
                    IFormLinkNullable<IGenericBaseFormTemplateGetter> FormSpaceshipModule = new FormKey(env.LoadOrder[0].ModKey, 0x0003058E).ToNullableLink<IGenericBaseFormTemplateGetter>();
                    IFormLinkNullable<IActorValueInformationGetter> SpaceshipPartMass = new FormKey(env.LoadOrder[0].ModKey, 0x0000ACDB).ToNullableLink<IActorValueInformationGetter>();
                    IFormLinkNullable<IActorValueInformationGetter> ShipModuleVariant = new FormKey(env.LoadOrder[0].ModKey, 0x0027BACE).ToNullableLink<IActorValueInformationGetter>();
                    IFormLinkNullable<IKeywordGetter> SpaceshipLinkedExterior = new FormKey(env.LoadOrder[0].ModKey, 0x0000662F).ToNullableLink<IKeywordGetter>();
                    IFormLinkNullable<IKeywordGetter> ShipModuleManufacturerDeimos = new FormKey(env.LoadOrder[0].ModKey, 0x001462C0).ToNullableLink<IKeywordGetter>();

                    IFormLinkNullable<IKeywordGetter> ShipModPositionAft = new FormKey(env.LoadOrder[0].ModKey, 0x0027BABC).ToNullableLink<IKeywordGetter>();
                    IFormLinkNullable<IKeywordGetter> ShipModPositionFore = new FormKey(env.LoadOrder[0].ModKey, 0x0027BABD).ToNullableLink<IKeywordGetter>();
                    IFormLinkNullable<IKeywordGetter> ShipModPositionBottom = new FormKey(env.LoadOrder[0].ModKey, 0x0027BABE).ToNullableLink<IKeywordGetter>();
                    IFormLinkNullable<IKeywordGetter> ShipModPositionTop = new FormKey(env.LoadOrder[0].ModKey, 0x0027BABF).ToNullableLink<IKeywordGetter>();
                    IFormLinkNullable<IKeywordGetter> ShipModPositionStbd = new FormKey(env.LoadOrder[0].ModKey, 0x0027BAC2).ToNullableLink<IKeywordGetter>();
                    IFormLinkNullable<IKeywordGetter> ShipModPositionPort = new FormKey(env.LoadOrder[0].ModKey, 0x0027BAC5).ToNullableLink<IKeywordGetter>();


                    Console.WriteLine("Building Record : " + prefix + "_gbfm_" + item);
                    var keywords = new KeywordFormComponent()
                    {
                        Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>()
                            {
                                ShipModuleManufacturerDeimos
                            }
                    };
                    if (direction == directions.ShipModPositionAft) keywords.Keywords.Add(ShipModPositionAft);
                    if (direction == directions.ShipModPositionFore) keywords.Keywords.Add(ShipModPositionFore);
                    if (direction == directions.ShipModPositionBottom) keywords.Keywords.Add(ShipModPositionBottom);
                    if (direction == directions.ShipModPositionTop) keywords.Keywords.Add(ShipModPositionTop);
                    if (direction == directions.ShipModPositionStbd) keywords.Keywords.Add(ShipModPositionStbd);
                    if (direction == directions.ShipModPositionPort) keywords.Keywords.Add(ShipModPositionPort);

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
                        keywords
                        ,
                        new FullNameComponent()
                        {
                            Name = item
                        }
                    };
                    var gbfm = new GenericBaseForm(myMod)
                    {
                        EditorID = prefix + "_gbfm_" + item + direction.ToString(),
                        ObjectBounds = new ObjectBounds() { First = new P3Float(0, 0, 0), Second = new P3Float(0, 0, 0) },
                        ODTY = 0,
                        Template = FormSpaceshipModule,
                        Components = gbfm_components,
                        STRVs = new ExtendedList<string>()
                        {
                            "BGSMod_Template_Component"
                        }
                    };
                    myMod.GenericBaseForms.Add(gbfm);
                    //7 add to fliplist
                    FlipsList.Items.Add(gbfm);

                }
                myMod.FormLists.Add(FlipsList);

                //7: Constructable
                Console.WriteLine("Building Record : " + prefix + "_co_" + item);
                byte tnamearry = 00;
                IFormLinkNullable<IKeywordGetter> WorkbenchShipBuildingKeyword = new FormKey(env.LoadOrder[0].ModKey, 0x0029C480).ToNullableLink<IKeywordGetter>();
                IFormLinkNullable<IKeywordGetter> Category_ShipMod_Structure = new FormKey(env.LoadOrder[0].ModKey, 0x0029C473).ToNullableLink<IKeywordGetter>();

                var co = new ConstructibleObject(myMod)
                {
                    EditorID = prefix + "_co_" + item,
                    Description = item,
                    CreatedObject = FlipsList.ToNullableLink<IConstructibleObjectTargetGetter>(),
                    AmountProduced = 1,
                    MenuSortOrder = 1,
                    TNAM = tnamearry,
                    LearnMethod = ConstructibleObject.LearnMethodEnum.DefaultOrConditions,
                    Value = 1000,
                    WorkbenchKeyword = WorkbenchShipBuildingKeyword,
                    Categories = new ExtendedList<IFormLinkGetter<IKeywordGetter>>()
                        {
                            Category_ShipMod_Structure
                        },
                    RECF = 0,
                };

                myMod.ConstructibleObjects.Add(co);
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
