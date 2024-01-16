using DynamicData;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System.Globalization;

Console.WriteLine("Hello, World!");
var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
var immutableLoadOrderLinkCache = env.LoadOrder.ToImmutableLinkCache();

string prefix = "test";
string item = "wing05";
string modelpath = "avontech\\ats_cargo_02.nif";
string modname = "FrankyTest";

foreach( var cell in env.LoadOrder[71].Mod.Cells)
{
    foreach(var subblock in cell.SubBlocks)
    {
        foreach(var finalcell in subblock.Cells)
        {
            Console.WriteLine(finalcell.EditorID);
        }
    }
}
ModKey newMod = new ModKey(modname, ModType.Master);
StarfieldMod myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);

// Moveable Static ------------------------------------------
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
moveableStatic.ODTY = 0;
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
    FLLD = new MemorySlice<byte>(flldarry),
    XFLG = new MemorySlice<byte>(xflgarry),
};
moveableStatic.DATA = 4;
moveableStatic.Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>()
{
    spaceshipformshipmodule,
    NavmeshUseDefaultCollisionForGeneration
};
myMod.MoveableStatics.Add(moveableStatic);


//Cell---------------------------
var cellblock = new CellBlock
{
    BlockNumber = 1,
    GroupType = GroupTypeEnum.InteriorCellBlock,
    SubBlocks = new ExtendedList<CellSubBlock>()    
};
cellblock.SubBlocks.Add(new CellSubBlock()
{
    BlockNumber = 1,
    GroupType = GroupTypeEnum.InteriorCellSubBlock,
    Cells = new ExtendedList<Cell>()    
});
var newCell = new Cell(myMod)
{
    EditorID = prefix + "_cell_" + item,
    WaterHeight = 0,
    XILS = 1.0f,
    Temporary = new ExtendedList<IPlaced>(),
};

// Cell contents
IFormLink<IPlaceableObjectGetter> OutpostGroupPackinDummy = new FormKey(env.LoadOrder[0].ModKey, 0x00015804).ToLink<IPlaceableObjectGetter>();
IFormLink<IPlaceableObjectGetter> PrefabPackinPivotDummy = new FormKey(env.LoadOrder[0].ModKey, 0x0003F808).ToLink<IPlaceableObjectGetter>();
IFormLink<IKeywordGetter> UpdatesDynamicNavmeshKeyword = new FormKey(env.LoadOrder[0].ModKey, 0x00140158).ToLink<IKeywordGetter>();

newCell.Temporary.Add(new PlacedObject(myMod)
{
    Base = OutpostGroupPackinDummy,
    Position = new P3Float(0,0,0),
    Rotation = new P3Float(0,0,0)
});
newCell.Temporary.Add(new PlacedObject(myMod)
{
    Base = PrefabPackinPivotDummy,
    Position = new P3Float(0, 0, 0),
    Rotation = new P3Float(0, 0, 0)
});
var components = new ExtendedList<AComponent>()
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
    Components = components,
    Position = new P3Float(0, 0, 0),
    Rotation = new P3Float(0, 0, 0)
});

cellblock.SubBlocks[0].Cells.Add(newCell);
myMod.Cells.Add(cellblock);


// Packin --------------------------------------
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




// Finish up ---------------------------------------------

myMod.WriteToBinary(env.DataFolderPath + "\\" + modname + ".esm");
Console.WriteLine("Finished");
Console.ReadLine();