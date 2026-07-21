# Ship Module (Structural Part) — From Scratch

Creating a custom ship structural module for Starfield's ship builder. This is distinct from encounter ships (see `ship.md`) — these are parts the player can attach in the ship builder UI.

## Record chain

A single ship module **authors five records** — MSTT, CELL, PKIN, GBFM, COBJ. The SnapTemplate in the
tree below is *linked*, not created: `gen_shipstruct` points every base part at the vanilla
`ShipSnap_SMOD_Generic_1x1x1_All01` (`0x00059B01`).

A **flipped variant is the exception and authors its own SnapTemplate**, because the snap nodes have to
be remapped for the rotation (see *Multi-directional modules* below) — so `gen_shipflips` writes seven
record types per set: FormList + MSTT + SNTP + CELL + PKIN + GBFM + COBJ. Count `new X(myMod)` in the
generator if you're ever unsure; the indentation in the tree below marks what is referenced rather than
created.

```
MoveableStatic (MSTT) — 3D model + snap template + paint swaps
    └─ SnapTemplate (SNTP) — connection points (fore/aft/top/bottom/port/starboard)

Cell (CELL) — interior cell holding the placed MoveableStatic
    └─ PlacedObject × 3: OutpostGroupPackinDummy + PrefabPackinPivotDummy + the MoveableStatic

PackIn (PKIN) — wraps the Cell for the ship builder system

GenericBaseForm (GBFM) — the ship module record itself
    └─ Components: PropertySheet + FormLinkData + Keywords + FullName

ConstructibleObject (COBJ) — workbench recipe for the ship builder
```

### EditorID conventions

```
{prefix}_ms_{item}       — MoveableStatic
{prefix}_sn_{item}       — SnapTemplate (flipped variants append direction)
{prefix}_cell_{item}     — Cell
{prefix}_pkn_{item}      — PackIn
{prefix}_gbfm_{item}     — GenericBaseForm
{prefix}_co_{item}       — ConstructibleObject
```

## MoveableStatic (MSTT)

The visual mesh with snap points and paintable surfaces.

```csharp
MoveableStatic moveableStatic = new MoveableStatic(myMod);
moveableStatic.EditorID = prefix + "_ms_" + item;
moveableStatic.ObjectBounds = new ObjectBounds()
{
    First = new P3Float(-4, -4, -1.767578f),
    Second = new P3Float(4, 4, 1.767578f)
};
moveableStatic.SnapTemplate = snaplink;    // 0x00059B01
moveableStatic.Model = new Model()
{
    File = new AssetLink<StarfieldModelAssetType>(modelpath),
    MaterialSwaps = new ExtendedList<IFormLinkGetter<ILayeredMaterialSwapGetter>>()
    {
        paint1, paint2, paint3   // three paint layers
    },
};
moveableStatic.DATA = 4;
moveableStatic.Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>()
{
    spaceshipformshipmodule,                    // 0x001BB401
    NavmeshUseDefaultCollisionForGeneration     // 0x00207960
};
myMod.MoveableStatics.Add(moveableStatic);
```

### Key FormIDs — MoveableStatic

| FormID | Record | Purpose |
|--------|--------|---------|
| `0x00059B01` | SnapTemplate [SNTP] | Default ship module snap template |
| `0x00099196` | LayeredMaterialSwap | Paint layer 1 |
| `0x000B6B1F` | LayeredMaterialSwap | Paint layer 2 |
| `0x002AF78A` | LayeredMaterialSwap | Paint layer 3 |
| `0x001BB401` | `SpaceshipFormShipModule` [KYWD] | Marks record as a ship module |
| `0x00207960` | `NavmeshUseDefaultCollisionForGeneration` [KYWD] | Enables default navmesh collision |

## SnapTemplate — connection points

Ship modules connect via **snap nodes**. Each node defines a direction (fore, aft, top, bottom, port, starboard) using `SnapTemplateNode` FormKeys.

### Snap node FormIDs (Starfield.esm)

| Direction | SnapTemplateNode FormID | Internal ID |
|-----------|------------------------|-------------|
| Fore | `0x0004AB6F` | 306031 |
| Aft | `0x0004AB70` | 306032 |
| Port | `0x0004AB73` | 306035 |
| Starboard | `0x0004AB74` | 306036 |
| Top | `0x0004AB77` | 306039 |
| Bottom | `0x0004AB78` | 306040 |

### Creating a snap template

```csharp
SnapTemplate snapTemplate = new SnapTemplate(myMod)
{
    EditorID = prefix + "_sn_" + item,
    NextNodeID = oldSnap.NextNodeID,
    STPT = oldSnap.STPT,
};
// Add nodes with position/rotation offsets
foreach (var node in calculatedNodes)
    snapTemplate.Nodes.Add(node);
myMod.SnapTemplates.Add(snapTemplate);
```

### SnapNodeEntry structure

Each entry in `SnapTemplate.Nodes`:

```csharp
new SnapNodeEntry()
{
    Node = ForeKey,              // FormLink to SnapTemplateNode
    NodeID = originalNode.NodeID,
    Rotation = new P3Float(...), // Euler angles (degrees in snap context)
    Offset = originalNode.Offset,
}
```

## Cell — interior cell for the module

Ship module cells are interior cells containing the placed MoveableStatic. Each cell needs proper CellBlock/SubBlock routing.

```csharp
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
        new CellXCLAItem() { XCLA = 1, XCLD = "Default Layer Name 1" },
        new CellXCLAItem() { XCLA = 2, XCLD = "Default Layer Name 2" },
        new CellXCLAItem() { XCLA = 3, XCLD = "Default Layer Name 3" },
        new CellXCLAItem() { XCLA = 4, XCLD = "Default Layer Name 4" },
    },
    ImageSpace = DefaultImagespacePackin,  // 0x0006AD68
};
```

### CellBlock / SubBlock routing — critical

From ElminsterAU: *"Block and sub-block address the last 2 digits of the object ID of the record converted to decimal. You have no control over these. The game engine depends on the records being in the correct block/sub-block to find them."*

The block/sub-block numbers are derived from the Cell's FormKey ID:

```csharp
var key = newCell.FormKey.ID;
var stringkey = key.ToString();
var cellblockNumber = int.Parse(stringkey.Substring(stringkey.Length - 1));      // last digit
var subBlockNumber = int.Parse(stringkey.Substring(stringkey.Length - 2, 1));    // second-to-last digit
```

Always check for existing CellBlocks/SubBlocks before creating new ones:

```csharp
CellBlock? cellblock = null;
bool newCellBlock = false;
for (int i = 0; i < myMod.Cells.Count; i++)
{
    if (myMod.Cells[i].BlockNumber == cellblockNumber)
        cellblock = myMod.Cells[i];
}
if (cellblock == null)
{
    cellblock = new CellBlock
    {
        BlockNumber = cellblockNumber,
        GroupType = GroupTypeEnum.InteriorCellBlock,
        SubBlocks = new ExtendedList<CellSubBlock>()
    };
    newCellBlock = true;
}
// Same pattern for SubBlocks...
```

### Cell contents — three placed objects

Every ship module cell needs exactly three PlacedObjects in `Temporary`:

| Object | FormID | Purpose |
|--------|--------|---------|
| `OutpostGroupPackinDummy` | `0x00015804` | Required group marker |
| `PrefabPackinPivotDummy` | `0x0003F808` | Pivot point marker |
| The MoveableStatic itself | (your new record) | The actual ship part mesh |

The MoveableStatic placement gets a `KeywordFormComponent` with `UpdatesDynamicNavmeshKeyword` (`0x00140158`) and `RagdollData` (BoneId=0, zero position/rotation):

```csharp
newCell.Temporary.Add(new PlacedObject(myMod)
{
    Base = moveableStatic.ToLink<IPlaceableObjectGetter>(),
    RagdollData = new ExtendedList<RagdollData>()
    {
        new RagdollData()
        {
            BoneId = 0,
            Position = new P3Float(0, 0, 0),
            Rotation = new P3Float(0, 0, 0)
        }
    },
    Components = new ExtendedList<AComponent>()
    {
        new KeywordFormComponent()
        {
            Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>()
            {
                UpdatesDynamicNavmeshKeyword  // 0x00140158
            }
        }
    },
    Position = new P3Float(0, 0, 0),
    Rotation = new P3Float(0, 0, 0)   // rotated for directional variants
});
```

## PackIn — wraps cell for ship builder

```csharp
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
        Ship = shipTransformLink   // 0x00050FAC
    },
    Filter = "\\Ships\\Modules\\Exterior\\Struct\\Deimos\\",
    Cell = newCell.ToNullableLink<ICellGetter>(),
    Version = 0,
    FNAM = new MemorySlice<byte>(new byte[4] { 14, 0, 0, 0 }),
    MaterialSwaps = new ExtendedList<IFormLinkGetter<ILayeredMaterialSwapGetter>>()
};
myMod.PackIns.Add(packin);
```

### Key FormIDs — PackIn

| FormID | Record | Purpose |
|--------|--------|---------|
| `0x00050FAC` | Transform [TRNS] | Ship module transform |
| `0x0006AD68` | ImageSpace [IMGS] | `DefaultImagespacePackin` — cell image space |

## GenericBaseForm (GBFM) — the ship module

The GBFM is what the ship builder sees. It links to the PackIn via `FormLinkDataComponent` and carries metadata via component system.

```csharp
var gbfm = new GenericBaseForm(myMod)
{
    EditorID = prefix + "_gbfm_" + item,
    ObjectBounds = new ObjectBounds() { First = new P3Float(0, 0, 0), Second = new P3Float(0, 0, 0) },
    Template = FormSpaceshipModule,   // set after construction (0x0003058E)
    Components = gbfm_components,
};
myMod.GenericBaseForms.Add(gbfm);
```

**Template must be set after construction** (nullable FormLink rule):
```csharp
// Template = 0x0003058E is set IN the initializer in gen_shipstruct,
// but the safe pattern from CLAUDE.md applies if using ToNullableLink
```

### GBFM Components

Four components are required:

```csharp
var gbfm_components = new ExtendedList<AComponent>()
{
    // 1. PropertySheet — mass and variant
    new PropertySheetComponent()
    {
        Properties = new ExtendedList<ObjectProperty>()
        {
            new ObjectProperty()
            {
                ActorValue = SpaceshipPartMass,    // 0x0000ACDB
                Value = 5,
            },
            new ObjectProperty()
            {
                ActorValue = ShipModuleVariant,    // 0x0027BACE
                Value = 1,
            }
        }
    },
    // 2. FormLinkData — links PackIn via SpaceshipLinkedExterior
    new FormLinkDataComponent()
    {
        Links = new ExtendedList<FormLinkComponentLink>
        {
            new FormLinkComponentLink()
            {
                LinkedForm = packin.ToNullableLink<IStarfieldMajorRecordGetter>(),
                Keyword = SpaceshipLinkedExterior,   // 0x0000662F
            }
        }
    },
    // 3. Keywords — manufacturer + position
    new KeywordFormComponent()
    {
        Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>()
        {
            ShipModuleManufacturerDeimos    // 0x001462C0
        }
    },
    // 4. FullName — display name in ship builder
    new FullNameComponent()
    {
        Name = displayName
    }
};
```

### Key FormIDs — GBFM

| FormID | Record | Purpose |
|--------|--------|---------|
| `0x0003058E` | GenericBaseFormTemplate | `FormSpaceshipModule` — template for all ship modules |
| `0x0000ACDB` | ActorValueInformation | `SpaceshipPartMass` — module weight |
| `0x0027BACE` | ActorValueInformation | `ShipModuleVariant` — variant index |
| `0x0000662F` | Keyword [KYWD] | `SpaceshipLinkedExterior` — links GBFM to the exterior PackIn |
| `0x000055E9` | Keyword [KYWD] | `SpaceshipLinkedInterior` — links GBFM to the **interior** PackIn |
| `0x001462C0` | Keyword [KYWD] | `ShipModuleManufacturerDeimos` — manufacturer tag |

### Modules with an interior (cockpits, habs) need TWO FormLinkData links

The `FormLinkDataComponent` example above shows one link because it was written from a
**structural** module, which has no interior. A cockpit or hab has two PackIns — exterior and
interior — and needs a link for each, or the interior never loads:

```csharp
new FormLinkComponentLink() { LinkedForm = packinExt.ToNullableLink<IStarfieldMajorRecordGetter>(),
                              Keyword = SpaceshipLinkedExterior },   // 0x0000662F
new FormLinkComponentLink() { LinkedForm = packinInt.ToNullableLink<IStarfieldMajorRecordGetter>(),
                              Keyword = SpaceshipLinkedInterior },   // 0x000055E9
```

Confirmed against a shipped hand-authored cockpit (`atsd_bf_sherpa`, Avontech Stardust) via
`gen_inspect gbfm`. Note that module also uses `VisibleFromShipExterior` (`0x0009C20A`) among its
keywords — an interior module that should still render on the outside hull.

### Additional GBFM component types (vanilla)

Scanned from 1592 ship modules in Starfield.esm via `gen_shipmodulestats`:

| Component | Count | Purpose |
|-----------|-------|---------|
| `PropertySheetComponent` | 1592 | Stats (see below) — every module has one |
| `FormLinkDataComponent` | 1592 | PackIn link — every module has one |
| `KeywordFormComponent` | 1592 | Manufacturer + position keywords |
| `FullNameComponent` | 1592 | Display name |
| `AttachParentArrayComponent` | 1071 | Attach point slots for sub-modules |
| `DestructibleObjectComponent` | 743 | Destruction stages and debris |
| `ObjectWindowFilterComponent` | 177 | CK object window categorisation |
| `StoredTraversalsComponent` | 34 | NPC navmesh traversal data |
| `AddToInventoryOnDestroyComponent` | 12 | Loot drops when module is destroyed |

### PropertySheetComponent — ship module stats catalogue

All ActorValues used by vanilla ship modules. Grouped by module type.

#### Universal (all module types)

| FormID | EditorID | Count | Range | Notes |
|--------|----------|-------|-------|-------|
| `0x0000ACDB` | `SpaceshipPartMass` | 1592 | 1–130 | Module weight — every module |
| `0x000002D4` | `Health` | 1404 | 1–6 | Hull HP tier |
| `0x00019080` | `SpaceshipCrewRating` | 595 | 0.5–1 | Crew skill requirement |
| `0x0027BACE` | `ShipModuleVariant` | 413 | 10–30 | Variant index |

#### Weapons

| FormID | EditorID | Count | Range | Notes |
|--------|----------|-------|-------|-------|
| `0x0021961F` | `SpaceshipWeaponPower` | 243 | 2–6 | Power draw |
| `0x001EC77E` | `ShipSystemWeaponEMHealth` | 227 | 42–89 | EM damage resistance |
| `0x001EC77F` | `ShipSystemWeaponHealth` | 227 | 42–89 | System HP |
| `0x001D3D7A` | `ShipSystemDamageWeightWeapon` | 225 | 1 | Damage priority weight (always 1) |

#### Engines

| FormID | EditorID | Count | Range | Notes |
|--------|----------|-------|-------|-------|
| `0x0000ACDD` | `SpaceshipEnginePartMaxPower` | 179 | 2 | Max power allocation |
| `0x0000ACDC` | `SpaceshipEnginePartForce` | 174 | 6910–7620 | Forward thrust |
| `0x0000ACDE` | `SpaceshipThrusterPartForce` | 174 | 1530–1610 | Maneuvering thrust |
| `0x0000ACDF` | `SpaceshipThrusterPartMaxPower` | 174 | 2 | Thruster max power |
| `0x00278988` | `SpaceshipEnginePartMaxForwardSpeed` | 174 | 150 | Top speed |
| `0x00278986` | `SpaceshipEnginePartMaxBackwardSpeed` | 174 | 32 | Reverse speed |
| `0x002A9542` | `SpaceshipThrusterPartStrafeForce` | 173 | 19000 | Strafe force |
| `0x00278987` | `SpaceshipThrusterPartMaxStrafeSpeed` | 173 | 50 | Max strafe speed |
| `0x00001885` | `SpaceshipBoostFuel` | 176 | 3 | Boost fuel capacity |
| `0x00001886` | `SpaceshipBoostSpeed` | 176 | 2 | Boost speed |
| `0x0006A256` | `SpaceshipBoostRechargeRate` | 168 | 0.3 | Boost regen rate |
| `0x00011589` | `ShipSystemDamageWeightEngine` | 163 | 1 | Damage priority weight |
| `0x001EF0CD` | `ShipSystemEngineHealth` | 157 | 84–88 | System HP |
| `0x001EF0C2` | `ShipSystemEngineEMHealth` | 130 | 84–88 | EM HP |
| `0x002E6679` | `SpaceshipEnginePartMaxYawVelocity` | 151 | 0 | Yaw cap |
| `0x002DF170` | `SpaceshipEnginePartMaxPitchVelocity` | 151 | 0 | Pitch cap |
| `0x002DF171` | `SpaceshipEnginePartMaxRollVelocity` | 151 | 0 | Roll cap |

#### Shields

| FormID | EditorID | Count | Range | Notes |
|--------|----------|-------|-------|-------|
| `0x0024A05F` | `ShieldHealth` | 121 | 75–1450 | Shield capacity |
| `0x0005BFA8` | `ShieldMaxHealth` | 121 | 75–1450 | Max shield capacity |
| `0x0001ECCD` | `SpaceshipShieldPartMaxPower` | 121 | 2–12 | Power draw |
| `0x0005BFA7` | `ShieldRegenRate` | 121 | 0.005–0.1 | Combat regen |
| `0x000090A9` | `ShieldRegenRateNonCombat` | 120 | 0.15–0.2 | Out-of-combat regen |
| `0x0005C74B` | `ShieldVolatileHealth` | 121 | 0 | Volatile HP (always 0) |
| `0x0001158A` | `ShipSystemDamageWeightShield` | 120 | 1–3 | Damage weight |
| `0x001EE8C9` | `ShipSystemShieldsHealth` | 108 | 50–660 | System HP |
| `0x001EF0CC` | `ShipSystemShieldsEMHealth` | 96 | 50–660 | EM HP |
| `0x000090A6` | `ShieldRegenRateDelayDestroyed` | 14 | 6–12 | Regen delay after destroyed |
| `0x000090A7` | `ShieldRegenRateDelayDamaged` | 14 | 4–10 | Regen delay after damaged |

#### Reactors

| FormID | EditorID | Count | Range | Notes |
|--------|----------|-------|-------|-------|
| `0x00001018` | `SpaceshipMaxAvailablePower` | 106 | 16–30 | Total power budget |
| `0x0001CAC0` | `SpaceshipRepairRate` | 59 | 1.25–7.2 | Auto-repair speed |
| `0x001EF0CA` | `ShipSystemReactorHealth` | 71 | 25–57 | Reactor system HP |

#### Grav drives

| FormID | EditorID | Count | Range | Notes |
|--------|----------|-------|-------|-------|
| `0x00008223` | `SpaceshipGravJumpMaxPower` | 63 | 8–12 | Grav drive power |
| `0x002BFAFD` | `SpaceshipGravJumpThrust` | 63 | 13–53 | Jump thrust |
| `0x0000854E` | `SpaceshipGravJumpDistancePerFuel` | 63 | 1 | LY per fuel unit |
| `0x0000855E` | `SpaceshipGravJumpInterplanetaryDistanceMultiplier` | 63 | 0.5 | In-system multiplier |
| `0x0001158B` | `ShipSystemDamageWeightGravDrive` | 63 | 1–4 | Damage weight |
| `0x001EF0CB` | `ShipSystemGravDriveHealth` | 51 | 52–440 | System HP |
| `0x001EF0C1` | `ShipSystemGravDriveEMHealth` | 41 | 52–275 | EM HP |

#### Cargo and fuel

| FormID | EditorID | Count | Range | Notes |
|--------|----------|-------|-------|-------|
| `0x000002DC` | `CarryWeight` | 114 | 200–1000 | Cargo capacity |
| `0x0002B344` | `CarryWeightShielded` | 25 | 150–320 | Shielded cargo (contraband) |
| `0x0000854F` | `SpaceshipGravJumpFuel` | 53 | 50–1500 | Fuel tank capacity |

#### Habs and cockpits

| FormID | EditorID | Count | Range | Notes |
|--------|----------|-------|-------|-------|
| `0x00040CE0` | `SpaceshipCrew` | 341 | 0–3 | Crew slots provided |
| `0x002CC9EA` | `SpaceshipCrewSlots` | 102 | 1–4 | Crew station count |
| `0x0001E7DE` | `SpaceshipPassengerSlots` | 68 | 2–3 | Passenger berths |
| `0x00002963` | `SpaceshipTargetLockTime` | 44 | 3 | Cockpit target lock time |

#### Landing gear

| FormID | EditorID | Count | Range | Notes |
|--------|----------|-------|-------|-------|
| `0x0030B58A` | `SpaceshipLanderRating` | 36 | 1–4 | Planet gravity rating |

#### Misc / quest-specific

| FormID | EditorID | Count | Range | Notes |
|--------|----------|-------|-------|-------|
| `0x001AE52C` | `SpaceshipScanJammer` | 3 | 1–3 | Scan jammer tier |
| `0x0016AA02` | `CF06_ConductionGrid_AV` | 2 | 1 | Crimson Fleet quest part |
| `0x0016AA03` | `CF05_ComSpike_AV` | 2 | 1 | Crimson Fleet quest part |
| `0x0004B3BF` | `OutpostStarstationShips` | 4 | 1 | Starstation docking slots |
| `0x002CBE74` | `OutpostScannerRadius` | 2 | 250 | Station scanner range |

## ConstructibleObject — ship builder recipe

```csharp
var co = new ConstructibleObject(myMod)
{
    EditorID = prefix + "_co_" + item,
    Description = item,
    CreatedObject = gbfm.ToNullableLink<IConstructibleObjectTargetGetter>(),
    AmountProduced = 1,
    MenuSortOrder = 1,
    LearnMethod = ConstructibleObject.LearnMethodEnum.DefaultOrConditions,
    Value = 1000,
    WorkbenchKeyword = WorkbenchShipBuildingKeyword,   // 0x0029C480
};
myMod.ConstructibleObjects.Add(co);
```

For multi-directional modules, `CreatedObject` points to a **FormList** instead of a single GBFM.

### Key FormIDs — COBJ

| FormID | Record | Purpose |
|--------|--------|---------|
| `0x0029C480` | Keyword [KYWD] | `WorkbenchShipBuildingKeyword` — routes to ship builder |
| `0x0029C473` | Keyword [KYWD] | `Category_ShipMod_Structure` — structure category |

## Multi-directional modules (flips)

`gen_shipflips` and `gen_shipyrotates` generate directional variants of a module. The pattern:

1. For each direction (Port, Starboard, Top, Bottom), create a full MoveableStatic → SnapTemplate → Cell → PackIn → GBFM chain
2. Collect all GBFM variants into a **FormList**
3. Create a single COBJ that produces the FormList — the ship builder picks the appropriate direction

### Rotation in cell

The MoveableStatic is rotated around the Y axis inside the cell:

| Direction | Y rotation (radians) | 45-degree variant |
|-----------|---------------------|-------------------|
| Top | 0 | 0.785 (45) |
| Port | 1.571 (90) | 2.356 (135) |
| Bottom | 3.142 (180) | 3.927 (225) |
| Starboard | 4.712 (270) | 5.498 (315) |

### Snap node remapping

When rotating a module, snap nodes must be remapped to match the new orientation. The four lateral directions cycle:

**90-degree rotation (Port):**
- Starboard → Top
- Top → Port
- Port → Bottom
- Bottom → Starboard
- Fore/Aft: rotation.Y -= 90

**180-degree rotation (Bottom):**
- Starboard ↔ Port (swap)
- Top ↔ Bottom (swap)
- Fore/Aft: rotation.Y -= 90

**270-degree rotation (Starboard):**
- Starboard → Bottom
- Bottom → Port
- Port → Top
- Top → Starboard
- Fore/Aft: rotation.Y -= 90

For 45-degree variants (`gen_shipyfortyfiverotates`), the same remapping applies but Fore/Aft get rotated by -45, -135, or -225 degrees instead.

### Ship position keywords

Each directional GBFM gets a position keyword:

| Direction | Keyword FormID |
|-----------|---------------|
| Fore | `0x0027BABD` |
| Aft | `0x0027BABC` |
| Top | `0x0027BABF` |
| Bottom | `0x0027BABE` |
| Starboard | `0x0027BAC2` |
| Port | `0x0027BAC5` |

### FormList pattern

```csharp
FormList FlipsList = new FormList(myMod)
{
    EditorID = prefix + "_" + target.EditorID + "_franky",
};
// After building each directional GBFM:
FlipsList.Items.Add(gbfm);
// ...
myMod.FormLists.Add(FlipsList);

// COBJ creates the FormList, not a single GBFM:
co.CreatedObject = FlipsList.ToNullableLink<IConstructibleObjectTargetGetter>();
```

## Inspecting an existing chain

`gen_inspect` reaches every record in the chain, so a part can be verified end to end without
opening xEdit. Search by EditorID prefix (contains-match) or `0x` FormID:

```
dotnet run -- gen_inspect moveablestatic <prefix>   # model path + MaterialSwaps + keywords
dotnet run -- gen_inspect sntp           <prefix>   # snap nodes, decoded to Fore/Aft/Port/...
dotnet run -- gen_inspect cell           <prefix>   # the three placed objects
dotnet run -- gen_inspect packin         <prefix>   # Filter path + Cell link
dotnet run -- gen_inspect gbfm           <prefix>   # components, FormKeys resolved to EditorIDs
dotnet run -- gen_inspect cobj           <prefix>   # recipe, workbench, CreatedObject
dotnet run -- gen_inspect lmsw           <prefix>   # paint swaps — EditorID/FormKey only
```

Two cautions, both learned the hard way:

- **`lmsw` does not show the material mapping.** A CK-authored swap keeps its payload in `REFL`,
  which reads as opaque binary. Use it to confirm *which* swap records exist and what a
  MoveableStatic points at — not to prove which textures a swap binds. That still needs xEdit.
- **A count is evidence; the reason for it is not.** `gen_inspect` will happily show a module
  with fewer MaterialSwaps or no SnapTemplate than this doc describes. That usually means
  hand-authored and unfinished, not broken — the generator's chain is the spec, and a CK-built
  part is that spec minus whatever hasn't been done yet. Ask before calling a gap a defect.

## Generators

| Generator | Purpose |
|-----------|---------|
| `gen_shipstruct` | Creates a single ship module (no flips) — MoveableStatic from a NIF model path |
| `gen_shipflips` | Takes an existing MoveableStatic and generates 4 directional variants (90-degree rotations) |
| `gen_shipyrotates` | Same as flips but with different Y-axis rotation logic |
| `gen_shipyfortyfiverotates` | 45-degree increment variants |
| `gen_shipcompare` | Diagnostic — compares working vs failing GBFM records to find cloning bugs |
| `gen_shiptest` | Test harness — clones vanilla ships via `SpaceShipNoun` and diffs against originals |

## Key differences: ship module vs encounter ship

| | Ship module (this doc) | Encounter ship (`ship.md`) |
|---|---|---|
| Purpose | Player-placeable structural part | NPC ship spawned in encounters |
| Created from | Scratch (NIF model path) | Cloned from vanilla GBFM |
| GBFM Template | `FormSpaceshipModule` (0x0003058E) | Copied from source ship |
| Components | PropertySheet + FormLinkData + Keywords + FullName | Deep-copied, faction sources rewired |
| COBJ target | GBFM or FormList of flips | Not craftable — spawned via leveled lists |
| Workbench | Ship builder (0x0029C480) | N/A |
| Cell | Custom interior with the module mesh | N/A (ship uses existing cells) |
