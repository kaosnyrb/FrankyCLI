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
    class gen_cellfixer
    {
        //V0.0.1 was creating extra Cell Blocks and Sublocks. CellFixer creates a new cell in the correct format without the other parts.

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
                    
                        }
                    }
                }

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
                        break;
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

                IFormLink<IPlaceableObjectGetter> TRP_Canister_Toxic_01 = new FormKey(env.LoadOrder[0].ModKey, 0x00024358).ToLink<IPlaceableObjectGetter>();
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
                    Base = TRP_Canister_Toxic_01,
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
                // Finish up ---------------------------------------------
                Console.WriteLine("New cell at: Block " + cellblockNumber + " sub: " + subBlockNumber);
            }

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm");
            Console.WriteLine("Finished");


            return 0;
        }
    }
}
