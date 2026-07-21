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

            // ---- optional flags; every one defaults to the previous hardcoded behaviour ----
            //
            // The defaults below (generic 1x1x1 all-faces snap, three vanilla paints, grid
            // bounds) are right for a 1x1x1 structural cube and wrong for anything else. A
            // SnapTemplate is a function of the part's SHAPE -- vanilla runs 1 node on an
            // engine, 2-3 on a cockpit, 4 on a wing or docker, 6 on the generic cube, 10 on
            // a hab -- and a handed part (wing, side engine) needs one PER SIDE.
            //
            //   --snap <EditorID>          link an existing SnapTemplate instead of the cube
            //   --snap-nodes <spec>        author one, named <prefix>_sntp_<item>. Spec is
            //                              face@x,y,z pairs separated by ';' --
            //                              e.g. "Starboard@-4,0,0" for a port wing whose
            //                              inboard face sits on the grid at x=-4.
            //                              Faces: Fore Aft Port Starboard Top Bottom.
            //   --swaps <EditorID,...>     material swaps, replacing the three vanilla paints
            //   --bounds <minX,minY,minZ,maxX,maxY,maxZ>  ObjectBounds, min then max (a part is
            //                              not necessarily centred on its own origin)
            //
            // Each authored node is a VERBATIM copy of the matching node on vanilla's
            // ShipSnap_SMOD_Generic_1x1x1_All01 with only its Offset moved, so the node
            // record and its rotation come from the game, never from here. Rotation is a
            // property of the face (confirmed: the Nova wing templates carry the identical
            // rotations for their Starboard/Port/Aft nodes).
            string? optSnap = null, optSnapNodes = null, optSwaps = null, optBounds = null;
            for (int i = 5; i < args.Length; i++)
            {
                bool hasValue = i + 1 < args.Length;
                switch (args[i])
                {
                    case "--snap": if (!hasValue) { Console.WriteLine("Error: --snap needs a value"); return 1; } optSnap = args[++i]; break;
                    case "--snap-nodes": if (!hasValue) { Console.WriteLine("Error: --snap-nodes needs a value"); return 1; } optSnapNodes = args[++i]; break;
                    case "--swaps": if (!hasValue) { Console.WriteLine("Error: --swaps needs a value"); return 1; } optSwaps = args[++i]; break;
                    case "--bounds": if (!hasValue) { Console.WriteLine("Error: --bounds needs a value"); return 1; } optBounds = args[++i]; break;
                    default: Console.WriteLine("Error: unknown option " + args[i]); return 1;
                }
            }
            if (optSnap != null && optSnapNodes != null)
            {
                Console.WriteLine("Error: --snap links an existing template and --snap-nodes authors a new one; pick one");
                return 1;
            }

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
                            myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                            gen_quest_main.FixNextFormId(myMod);

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

                // ---- snap template: link a named one, or author one from a node spec ----
                if (optSnap != null)
                {
                    var found = FindSnapTemplate(myMod, env, optSnap);
                    if (found == null)
                    {
                        Console.WriteLine("Error: no SnapTemplate with EditorID '" + optSnap + "' in " + modname + " or Starfield.esm");
                        return 1;
                    }
                    snaplink = found.ToNullableLink<ISnapTemplateGetter>();
                    Console.WriteLine("Snap template : linking " + optSnap);
                }
                else if (optSnapNodes != null)
                {
                    var authored = BuildSnapTemplate(myMod, env, prefix + "_sntp_" + item, optSnapNodes);
                    if (authored == null) return 1;
                    myMod.SnapTemplates.Add(authored);
                    snaplink = authored.ToNullableLink<ISnapTemplateGetter>();
                    Console.WriteLine("Building Record : " + authored.EditorID + " (" + authored.Nodes.Count + " node(s))");
                }

                // ---- material swaps ----
                var swaps = new ExtendedList<IFormLinkGetter<ILayeredMaterialSwapGetter>>();
                if (optSwaps != null)
                {
                    foreach (var editorId in optSwaps.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var swap = FindMaterialSwap(myMod, env, editorId.Trim());
                        if (swap == null)
                        {
                            Console.WriteLine("Error: no LayeredMaterialSwap with EditorID '" + editorId.Trim() + "' in " + modname + " or Starfield.esm");
                            return 1;
                        }
                        swaps.Add(swap);
                    }
                    Console.WriteLine("Material swaps : " + optSwaps);
                }
                else
                {
                    swaps.Add(paint1);
                    swaps.Add(paint2);
                    swaps.Add(paint3);
                }

                // ---- object bounds ----
                // Six numbers, min then max -- NOT half-extents, because a part is not
                // necessarily centred on its own origin (a wing sits entirely outboard).
                var boundsFirst = new P3Float(-4, -4, -1.767578f);
                var boundsSecond = new P3Float(4, 4, 1.767578f);
                if (optBounds != null)
                {
                    var parts = optBounds.Split(',');
                    var n = new float[6];
                    if (parts.Length != 6)
                    {
                        Console.WriteLine("Error: --bounds wants six numbers, minX,minY,minZ,maxX,maxY,maxZ");
                        return 1;
                    }
                    for (int i = 0; i < 6; i++)
                    {
                        if (!float.TryParse(parts[i], out n[i]))
                        {
                            Console.WriteLine("Error: --bounds value '" + parts[i] + "' is not a number");
                            return 1;
                        }
                    }
                    boundsFirst = new P3Float(n[0], n[1], n[2]);
                    boundsSecond = new P3Float(n[3], n[4], n[5]);
                }

                MoveableStatic moveableStatic = new MoveableStatic(myMod);
                moveableStatic.EditorID = prefix + "_ms_" + item;
                moveableStatic.ObjectBounds = new ObjectBounds()
                {
                    First = boundsFirst,
                    Second = boundsSecond
                };
                moveableStatic.SnapTemplate = snaplink;
                moveableStatic.Model = new Model()
                {
                    File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>(modelpath),
                    MaterialSwaps = swaps,
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
                CellBlock? cellblock = null;
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
                    Template = FormSpaceshipModule,
                    Components = gbfm_components,
                };
                myMod.GenericBaseForms.Add(gbfm);

                //Constructable object -------------------------
                Console.WriteLine("Building Record : " + prefix + "_co_" + item);
                IFormLinkNullable<IKeywordGetter> WorkbenchShipBuildingKeyword = new FormKey(env.LoadOrder[0].ModKey, 0x0029C480).ToNullableLink<IKeywordGetter>();
                IFormLinkNullable<IKeywordGetter> Category_ShipMod_Structure = new FormKey(env.LoadOrder[0].ModKey, 0x0029C473).ToNullableLink<IKeywordGetter>();

                var co = new ConstructibleObject(myMod)
                {
                    EditorID = prefix + "_co_" + item,
                    Description = item,
                    CreatedObject = gbfm.ToNullableLink<IConstructibleObjectTargetGetter>(),
                    AmountProduced = 1,
                    MenuSortOrder = 1,
                    LearnMethod = ConstructibleObject.LearnMethodEnum.DefaultOrConditions,
                    Value = 1000,
                    WorkbenchKeyword = WorkbenchShipBuildingKeyword,
                };

                myMod.ConstructibleObjects.Add(co);
                // Finish up ---------------------------------------------
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
            {
                rec.IsCompressed = false;
            }

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine("Finished");
            return 0;
        }

        // ShipSnap_SMOD_Generic_1x1x1_All01 -- the canonical six-face cube every authored
        // node here is copied from, so the node record and its rotation come from the game.
        const uint CanonicalCube = 0x00059B01;

        static readonly Dictionary<string, uint> SnapFaceNodes = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            { "Fore", 0x0004AB6F },
            { "Aft", 0x0004AB70 },
            { "Port", 0x0004AB73 },
            { "Starboard", 0x0004AB74 },
            { "Top", 0x0004AB77 },
            { "Bottom", 0x0004AB78 },
        };

        static SnapTemplate? FindSnapTemplate(StarfieldMod myMod, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, string editorId)
        {
            foreach (var st in myMod.SnapTemplates)
                if (string.Equals(st.EditorID, editorId, StringComparison.OrdinalIgnoreCase)) return st;
            foreach (var st in env.LoadOrder[0].Mod!.SnapTemplates)
                if (string.Equals(st.EditorID, editorId, StringComparison.OrdinalIgnoreCase)) return st.DeepCopy();
            return null;
        }

        static IFormLinkGetter<ILayeredMaterialSwapGetter>? FindMaterialSwap(StarfieldMod myMod, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, string editorId)
        {
            foreach (var sw in myMod.LayeredMaterialSwaps)
                if (string.Equals(sw.EditorID, editorId, StringComparison.OrdinalIgnoreCase))
                    return sw.ToLink<ILayeredMaterialSwapGetter>();
            foreach (var sw in env.LoadOrder[0].Mod!.LayeredMaterialSwaps)
                if (string.Equals(sw.EditorID, editorId, StringComparison.OrdinalIgnoreCase))
                    return sw.ToLink<ILayeredMaterialSwapGetter>();
            return null;
        }

        // "Starboard@-4,0,0;Aft@0,-3.65,0" -> a SnapTemplate carrying those faces, each node
        // lifted verbatim off the vanilla cube with only its Offset moved.
        static SnapTemplate? BuildSnapTemplate(StarfieldMod myMod, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, string editorId, string spec)
        {
            SnapTemplate? canonical = null;
            foreach (var st in env.LoadOrder[0].Mod!.SnapTemplates)
                if (st.FormKey.ID == CanonicalCube) canonical = st.DeepCopy();
            if (canonical == null)
            {
                Console.WriteLine("Error: could not read vanilla ShipSnap_SMOD_Generic_1x1x1_All01 to copy nodes from");
                return null;
            }

            var template = new SnapTemplate(myMod)
            {
                EditorID = editorId,
                NextNodeID = canonical.NextNodeID,
                STPT = canonical.STPT,
            };

            foreach (var entry in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var bits = entry.Split('@');
                if (bits.Length != 2)
                {
                    Console.WriteLine("Error: bad node spec '" + entry + "' -- want Face@x,y,z");
                    return null;
                }
                var face = bits[0].Trim();
                if (!SnapFaceNodes.TryGetValue(face, out var nodeId))
                {
                    Console.WriteLine("Error: unknown face '" + face + "'. Faces: " + string.Join(" ", SnapFaceNodes.Keys));
                    return null;
                }
                var nums = bits[1].Split(',');
                if (nums.Length != 3
                    || !float.TryParse(nums[0], out var ox)
                    || !float.TryParse(nums[1], out var oy)
                    || !float.TryParse(nums[2], out var oz))
                {
                    Console.WriteLine("Error: bad offset in '" + entry + "' -- want three numbers");
                    return null;
                }

                SnapNodeEntry? source = null;
                foreach (var n in canonical.Nodes)
                    if (n.Node.FormKey.ID == nodeId) source = n;
                if (source == null)
                {
                    Console.WriteLine("Error: the vanilla cube carries no " + face + " node");
                    return null;
                }

                template.Nodes.Add(new SnapNodeEntry()
                {
                    Node = source.Node,
                    NodeID = source.NodeID,
                    Rotation = source.Rotation,
                    Offset = new P3Float(ox, oy, oz),
                });
            }

            if (template.Nodes.Count == 0)
            {
                Console.WriteLine("Error: --snap-nodes produced no nodes");
                return null;
            }
            return template;
        }
    }
}
