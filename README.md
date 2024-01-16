# FrankyCLI
CLI Tool for generating the records for Starfield Ship parts.
This tool will create a new ESM or append to an existing ESM if it already exists.

# Records made
CO (Constructable object) - The workshop listing such as price, description and menu positions
GBFM (Generic Base Form) - The parts stats and PKIN links
PKIN - The packin that points to the CELL
CELL - The cell that contains the layout of the parts of the ship
MSTT (Moveable Static) - The mesh and material information

# Parameters:

FrankyCLI.exe modname prefix itemname partname modelfilepath

Mod name is the ESM name without the filetype

Editor id is such:

prefix + "_{type}_" + itemname

Partname is the visible UI names

modelfilepath is the nif location: avontech\ats_cargo_04.nif

# Example

FrankyCLI.exe FrankyTest ft cargo Cargo avontech\ats_cargo_04.nif

# Materials 

The MSTT model record contains MOLM - Material Swaps, these are a texture name tied to a colouring position (Primary, Secondary, Tertiary)

To get these to work make sure your BSGeometry nodes are using one of the following:

Ship_ShipsShplndMetalTileGray_P [LMSW:00099196]
Materials\Ships\ShipCommon\ShipsShplndMetalTileGray.mat

Ship_ShipsPaintedMetalScratched01_S [LMSW:002AF78A]
Materials\Ships\ShipCommon\ShipsPaintedMetalScratched01.mat

Ship_ShipsYellowPaintedMetal01_T [LMSW:000B6B1F]
Materials\Ships\ShipCommon\ShipsYellowPaintedMetal01.mat

You can edit these to another ship materials by looking at the LMSW's, but they contain REFL data so need the CK to make new ones.