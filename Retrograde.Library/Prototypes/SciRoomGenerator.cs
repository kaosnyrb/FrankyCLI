using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;

namespace Retrograde.RoomPackinGeneration;

/// <summary>
/// Generates parametric Science Room (STS_TRK_Big) PackIn records.
///
/// Reverse-engineered from du_outlaws_template.esm rg_sts_trk_big_001–006.
/// All placed objects reference only Starfield.esm records — no template-mod dependencies.
///
/// Standard room geometry: 20×20 units (X/Y span ±10), 4 units high.
///
/// Layout:
///   Floor:    3×3 grid of SciIntRmSmMidFull01 at 4-unit spacing — combined floor+wall+ceiling modules
///   Walls:    SciIntRmSmWallMid01 ring at ±8 (3 tiles per face), center tile omitted at each exit
///   Corners:  SciIntRmSmWallCorIn01 at all four (±8, ±8) positions
///   Connectors: XMarkerHeading at ±10 on each enabled face (centre of face, Y=0 or X=0)
///
/// Tile kit (all Starfield.esm Statics, Architecture\ScienceKit\Interiors\RoomSmall\):
///   024C99 = SciIntRmSmMidFull01  — combined floor+wall+ceiling module (dominant)
///   024CA3 = SciIntRmSmWallMid01  — perimeter wall section
///   024C9A = SciIntRmSmWallCorIn01 — inside corner
///
/// IMPORTANT: These are Statics, NOT PackIns. The RoomSmall kit places Static meshes
/// directly into the storage cell's Temporary list — there is no recursive unpacking.
/// This contrasts with the SciIntHallSm kit which nests PackIns inside PackIns.
///
/// Wall tile rotation convention (CW, face INTO room — confirmed from big_001 cell dump):
///   W wall (X=−8): rot Z=0       face East  into room
///   N wall (Y=+8): rot Z=π/2     face South into room
///   E wall (X=+8): rot Z=π       face West  into room
///   S wall (Y=−8): rot Z=3π/2    face North into room
///   Corners: SW=3π/2, SE=π, NW=0, NE=π/2
///
/// S connector uses ConnRotNorth (Z=0, inward) — confirmed Bethesda convention for
/// rooms and bridge/corner rooms (big_001–006 all use Z=0 on the S connector).
/// </summary>
public class SciRoomGenerator
{
    // ── Starfield.esm FormKey IDs — structure ─────────────────────────────────
    private const uint IdPivot     = 0x03F808; // PrefabPackinPivotDummy (Static)
    private const uint IdFloorFull = 0x024C99; // SciIntRmSmMidFull01    (Static) — floor+wall+ceiling
    private const uint IdWallMid   = 0x024CA3; // SciIntRmSmWallMid01    (Static) — wall section
    private const uint IdCornerIn  = 0x024C9A; // SciIntRmSmWallCorIn01  (Static) — inside corner
    private const uint IdConn      = 0x000034; // XMarkerHeading         (Static) — connector marker
    private const uint IdSpawn     = 0x00003B; // XMarker                (Static) — enemy spawn marker

    // ── Starfield.esm FormKey IDs — lighting ──────────────────────────────────
    // Direct Static + Light placement (not LGT_* PackIns) so lights render in CK preview.
    // See formlib/packin.md: "LGT_* PackIns inside a room PackIn are not rendered."
    private const uint IdLightMesh   = 0x2ACD6C; // LightUtility_A01On          (Static) — wall-mount strip
    private const uint IdLightRecord = 0x1B29D1; // LGT_LightUtility06_Spot_S_2K (Light, radius 4)

    // ── Room geometry constants (confirmed from big_001 cell dump) ─────────────
    private const float GridStep = 4f;    // floor tile grid spacing; also wall tile spacing along face
    private const float WallAt   = 8f;    // wall tile center offset from room origin
    private const float RoomHalf = 10f;   // exterior edge / connector position
    private const float LightZ   = 3.2f;  // light height above floor

    // ── Wall tile rotations (CW, face INTO room — confirmed big_001) ───────────
    private const float WallRotW = 0f;                     // W wall → face East
    private const float WallRotN = MathF.PI / 2f;          // N wall → face South
    private const float WallRotE = MathF.PI;               // E wall → face West
    private const float WallRotS = 3f * MathF.PI / 2f;    // S wall → face North

    // ── Standard connector rotations ──────────────────────────────────────────
    // S and N connectors both use ConnRotNorth (Z=0):
    //   S: Z=0 is inward  (marker faces north into the room)
    //   N: Z=0 is outward (marker faces north out of the room)
    // Both confirmed from source rooms. E/W use their standard outward rotations.
    public const float ConnRotNorth = 0f;
    public const float ConnRotSouth = MathF.PI;
    public const float ConnRotEast  = MathF.PI / 2f;
    public const float ConnRotWest  = 3f * MathF.PI / 2f;

    private readonly StarfieldMod _targetMod;
    private readonly ModKey _starfieldModKey;
    private int _connSeq  = 1;
    private int _spawnSeq = 1;

    public SciRoomGenerator(StarfieldMod targetMod, ModKey starfieldModKey)
    {
        _targetMod       = targetMod;
        _starfieldModKey = starfieldModKey;
    }

    /// <summary>
    /// Places an XMarkerHeading connector in <paramref name="cell"/>.
    ///
    /// The EditorID sequence counter is shared across all connector placements in this generator
    /// instance so IDs stay unique across multiple Generate calls.
    /// </summary>
    /// <param name="dirCode">"n", "s", "e", or "w" — sets the router-visible direction token.</param>
    /// <param name="rotationZ">Yaw rotation of the marker in radians. Use the ConnRot* constants.</param>
    public void PlaceConnector(Cell cell, string dirCode, float x, float y, float z, float rotationZ)
    {
        AddPersist(cell, IdConn, x, y, z,
            editorId: $"rg_conn_{dirCode}_D1_station_{(_connSeq++):D3}",
            rot: new P3Float(0f, 0f, rotationZ));
    }

    /// <summary>
    /// Generates a standard 20×20 science room PackIn and adds it to the target mod.
    ///
    /// The room is symmetric about the origin. Connectors are centred on each enabled face:
    ///   S connector at (0, −10)   N connector at (0, +10)
    ///   E connector at (+10, 0)   W connector at (−10, 0)
    ///
    /// Wall tiles at the connector openings are omitted automatically.
    /// </summary>
    /// <param name="editorId">EditorID for the new PackIn (e.g. "rg_gen_sts_trk_big_001")</param>
    /// <param name="exitSouth">Enable south connector (Y=−10). Default true.</param>
    /// <param name="exitNorth">Enable north connector (Y=+10). Default false.</param>
    /// <param name="exitEast">Enable east connector (X=+10). Default false.</param>
    /// <param name="exitWest">Enable west connector (X=−10). Default false.</param>
    /// <returns>FormKey of the created PackIn</returns>
    public FormKey Generate(string editorId,
                            bool exitSouth = true,
                            bool exitNorth = false,
                            bool exitEast  = false,
                            bool exitWest  = false)
    {
        const float z = 0f;

        var cell = CreateCell(editorId + "StorageCell");

        // ── Temporary: structure ──────────────────────────────────────────────

        // Root pivot dummy — always first Temporary in every PackIn
        AddTemp(cell, IdPivot, 0f, 0f, z);

        // Floor grid: 3×3 SciIntRmSmMidFull01 tiles at 4-unit spacing.
        // MidFull01 is a combined floor+wall+ceiling module (no separate floor/ceiling pieces
        // in the RoomSmall kit). Grid covers the interior from −6 to +6 on both axes.
        // All tiles at rotation (0,0,0) — confirmed from big_001.
        for (int xi = -1; xi <= 1; xi++)
        for (int yi = -1; yi <= 1; yi++)
            AddTemp(cell, IdFloorFull, xi * GridStep, yi * GridStep, z);

        // Perimeter walls: SciIntRmSmWallMid01 ring at ±WallAt.
        // Each face has up to 3 tiles at positions {−GridStep, 0, +GridStep} along the face.
        // The centre tile (pos=0) is omitted where a connector opening is needed.
        PlaceWallFace(cell, isXFace: false, faceOffset: -WallAt, faceRot: WallRotS, skipCenter: exitSouth, z); // S wall
        PlaceWallFace(cell, isXFace: false, faceOffset: +WallAt, faceRot: WallRotN, skipCenter: exitNorth, z); // N wall
        PlaceWallFace(cell, isXFace: true,  faceOffset: +WallAt, faceRot: WallRotE, skipCenter: exitEast,  z); // E wall
        PlaceWallFace(cell, isXFace: true,  faceOffset: -WallAt, faceRot: WallRotW, skipCenter: exitWest,  z); // W wall

        // Inside corners at all four (±WallAt, ±WallAt) positions.
        // Rotations confirmed from big_001 cell dump. Each corner rotation matches
        // the facing of the adjoining wall on one side (CW convention):
        //   SE (+8,−8): rot=π    (E wall rotation)
        //   SW (−8,−8): rot=3π/2 (S wall rotation)
        //   NE (+8,+8): rot=π/2  (N wall rotation)
        //   NW (−8,+8): rot=0    (W wall rotation)
        AddTemp(cell, IdCornerIn,  WallAt, -WallAt, z, new P3Float(0f, 0f, WallRotE));  // SE
        AddTemp(cell, IdCornerIn, -WallAt, -WallAt, z, new P3Float(0f, 0f, WallRotS));  // SW
        AddTemp(cell, IdCornerIn,  WallAt,  WallAt, z, new P3Float(0f, 0f, WallRotN));  // NE
        AddTemp(cell, IdCornerIn, -WallAt,  WallAt, z, new P3Float(0f, 0f, WallRotW));  // NW

        // ── Temporary: lighting ───────────────────────────────────────────────

        PlaceLights(cell, z);

        // ── Persistent: connectors ────────────────────────────────────────────

        // S uses ConnRotNorth (Z=0, inward) — confirmed Bethesda convention for rooms.
        // N also uses ConnRotNorth (Z=0, outward facing north).
        if (exitSouth) PlaceConnector(cell, "s",     0f, -RoomHalf, z, ConnRotNorth);
        if (exitNorth) PlaceConnector(cell, "n",     0f,  RoomHalf, z, ConnRotNorth);
        if (exitEast)  PlaceConnector(cell, "e",  RoomHalf,    0f,  z, ConnRotEast);
        if (exitWest)  PlaceConnector(cell, "w", -RoomHalf,    0f,  z, ConnRotWest);

        // ── Persistent: enemy spawns ──────────────────────────────────────────

        PlaceSpawns(cell, z);

        // ── PackIn record ─────────────────────────────────────────────────────

        var packin = new PackIn(_targetMod)
        {
            EditorID            = editorId,
            MajorRecordFlagsRaw = 512, // Prefab flag
            ObjectBounds = new ObjectBounds
            {
                First  = new P3Float(-RoomHalf, -RoomHalf, -0.1f),
                Second = new P3Float( RoomHalf,  RoomHalf,  4.0f),
            },
        };
        // FormLink set after construction per Mutagen nullable FormLink rule
        packin.Cell = cell.ToNullableLink<ICellGetter>();
        _targetMod.PackIns.Add(packin);

        int exitCount = (exitSouth ? 1 : 0) + (exitNorth ? 1 : 0)
                      + (exitEast  ? 1 : 0) + (exitWest  ? 1 : 0);
        Console.WriteLine($"[SciRoomGenerator] {editorId}: {exitCount} exits " +
                          $"(S={exitSouth} N={exitNorth} E={exitEast} W={exitWest})");

        return packin.FormKey;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Places a run of SciIntRmSmWallMid01 (wall section) tiles along one room face.
    ///
    /// Tile positions along the face axis: {−GridStep, 0, +GridStep}.
    /// When skipCenter is true, the centre tile (pos=0) is omitted to leave a connector opening.
    /// </summary>
    /// <param name="isXFace">True if the fixed axis is X (E/W walls); false for N/S walls.</param>
    /// <param name="faceOffset">Constant coordinate on the fixed axis (e.g. +WallAt for E wall).</param>
    /// <param name="faceRot">Wall-face yaw in radians — use WallRotN/S/E/W constants.</param>
    /// <param name="skipCenter">If true, omit the tile at pos=0 (connector opening).</param>
    private void PlaceWallFace(Cell cell, bool isXFace, float faceOffset, float faceRot,
                                bool skipCenter, float z)
    {
        var rot = new P3Float(0f, 0f, faceRot);
        foreach (float pos in new[] { -GridStep, 0f, GridStep })
        {
            if (skipCenter && pos == 0f) continue;
            float x = isXFace ? faceOffset : pos;
            float y = isXFace ? pos : faceOffset;
            AddTemp(cell, IdWallMid, x, y, z, rot);
        }
    }

    /// <summary>
    /// Places 4 pairs of (LightUtility_A01On mesh + companion Light) on the E and W inner wall faces.
    /// Two lights per face at Y = ±GridStep, mounted at the inner face of each perimeter wall.
    ///
    /// Rotation:
    ///   E inner face (X = +7.5, facing west): rot Z = π/2
    ///   W inner face (X = −7.5, facing east): rot Z = 3π/2
    /// </summary>
    private void PlaceLights(Cell cell, float z)
    {
        float lightAt = WallAt - 0.5f; // inner face of the perimeter wall
        float lightZ  = z + LightZ;

        // E inner wall — mount flush with east perimeter wall, shine westward
        var eRot = new P3Float(0f, 0f, MathF.PI / 2f);
        foreach (int yi in new[] { -1, 1 })
        {
            float ly = yi * GridStep;
            AddTemp(cell, IdLightMesh,   lightAt, ly, lightZ, eRot);
            AddTemp(cell, IdLightRecord, lightAt, ly, lightZ, eRot);
        }

        // W inner wall — mount flush with west perimeter wall, shine eastward
        var wRot = new P3Float(0f, 0f, 3f * MathF.PI / 2f);
        foreach (int yi in new[] { -1, 1 })
        {
            float ly = yi * GridStep;
            AddTemp(cell, IdLightMesh,   -lightAt, ly, lightZ, wRot);
            AddTemp(cell, IdLightRecord, -lightAt, ly, lightZ, wRot);
        }
    }

    /// <summary>
    /// Places 4 enemy spawn markers, one per room quadrant.
    /// </summary>
    private void PlaceSpawns(Cell cell, float z)
    {
        foreach (float sx in new[] { -GridStep, GridStep })
        foreach (float sy in new[] { -GridStep, GridStep })
            AddPersist(cell, IdSpawn, sx, sy, z,
                editorId: "rg_enemy_spawn_" + (_spawnSeq++).ToString("D3"),
                rot: new P3Float(0f, 0f, 0f));
    }

    // ── Cell infrastructure (mirrors SciHallwayGenerator) ─────────────────────

    /// <summary>
    /// Creates a new interior Cell and routes it into the correct CellBlock / CellSubBlock.
    /// Block and subblock numbers are derived from the last two decimal digits of the Cell FormKey ID.
    /// </summary>
    private Cell CreateCell(string editorId)
    {
        var cell = new Cell(_targetMod)
        {
            EditorID   = editorId,
            Flags      = Cell.Flag.IsInteriorCell,
            Temporary  = new ExtendedList<IPlaced>(),
            Persistent = new ExtendedList<IPlaced>(),
        };

        int blockNum    = (int)(cell.FormKey.ID % 10);
        int subBlockNum = (int)((cell.FormKey.ID / 10) % 10);

        CellBlock? cellBlock = null;
        foreach (var b in _targetMod.Cells)
            if (b.BlockNumber == blockNum) { cellBlock = b; break; }

        if (cellBlock == null)
        {
            cellBlock = new CellBlock
            {
                BlockNumber = blockNum,
                GroupType   = GroupTypeEnum.InteriorCellBlock,
                SubBlocks   = new ExtendedList<CellSubBlock>(),
            };
            _targetMod.Cells.Add(cellBlock);
        }

        CellSubBlock? subBlock = null;
        foreach (var sb in cellBlock.SubBlocks)
            if (sb.BlockNumber == subBlockNum) { subBlock = sb; break; }

        if (subBlock == null)
        {
            subBlock = new CellSubBlock
            {
                BlockNumber = subBlockNum,
                GroupType   = GroupTypeEnum.InteriorCellSubBlock,
                Cells       = new ExtendedList<Cell>(),
            };
            cellBlock.SubBlocks.Add(subBlock);
        }

        subBlock.Cells.Add(cell);
        return cell;
    }

    private void AddTemp(Cell cell, uint formId, float x, float y, float z,
                         P3Float? rotation = null)
    {
        var po = new PlacedObject(_targetMod)
        {
            Base     = new FormKey(_starfieldModKey, formId).ToLink<IPlaceableObjectGetter>(),
            Position = new P3Float(x, y, z),
            Rotation = rotation ?? new P3Float(0f, 0f, 0f),
        };
        cell.Temporary.Add(po);
    }

    private void AddPersist(Cell cell, uint formId, float x, float y, float z,
                             string editorId, P3Float rot)
    {
        var po = new PlacedObject(_targetMod)
        {
            EditorID = editorId,
            Base     = new FormKey(_starfieldModKey, formId).ToLink<IPlaceableObjectGetter>(),
            Position = new P3Float(x, y, z),
            Rotation = rot,
        };
        cell.Persistent.Add(po);
    }
}
