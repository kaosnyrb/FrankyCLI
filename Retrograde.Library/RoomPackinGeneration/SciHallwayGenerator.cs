using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;

namespace Retrograde.RoomPackinGeneration;

/// <summary>
/// Generates parametric Science Hallway (STS_TRK_Shl) PackIn records.
///
/// Reverse-engineered from du_outlaws_template.esm rg_sts_trk_shl_001–006.
/// All placed objects reference only Starfield.esm records — no template-mod dependencies.
///
/// Room layout (N-S axis, Y increases northward):
///   [S connector] — [S end cap] — [flat tiles] — [stair tiles] — [flat tiles] — [N end cap] — [N connector]
///
/// Each tile is 4 units along Y (centered on its Y position ± 2).
/// Each stair tile rises 2 units in Z.
///
/// Tile kit (all Starfield.esm PackIns, Architecture\ScienceKit\Interiors\HallSmall\):
///   02447F = SciIntHallSm1Way01__SC   — straight segment v1
///   02446E = SciIntHallSm1Way02__SC   — straight segment v2
///   024466 = SciIntHallSm1WayStairs01__SC — staircase (rises 2 units per tile)
///   024441 = SciIntHallSmCapScktA01__SC   — end cap with socket
/// </summary>
public class SciHallwayGenerator
{
    // ── Starfield.esm FormKey IDs — structure ─────────────────────────────────
    private const uint IdPivot  = 0x03F808; // PrefabPackinPivotDummy  (Static)
    private const uint IdWay1   = 0x02447F; // SciIntHallSm1Way01__SC  (PackIn)
    private const uint IdWay2   = 0x02446E; // SciIntHallSm1Way02__SC  (PackIn)
    private const uint IdStairs = 0x024466; // SciIntHallSm1WayStairs01__SC (PackIn)
    private const uint IdCap    = 0x024441; // SciIntHallSmCapScktA01__SC   (PackIn)
    private const uint IdConn   = 0x000034; // XMarkerHeading (Static) — connector marker
    private const uint IdSpawn  = 0x00003B; // XMarker        (Static) — enemy spawn marker

    // ── Starfield.esm FormKey IDs — decorations ────────────────────────────────
    // Lighting: Static mesh + companion Light record placed directly in the cell.
    // Do NOT use LGT_* PackIns here — lights inside sub-PackIns are not rendered
    // when the outer room prefab is previewed in CK.
    private const uint IdLightMesh    = 0x2ACD6C; // LightUtility_A01On (Static)
    private const uint IdLightRecord  = 0x1B29D1; // Starfield.esm Light record (used in LGT_LightUtility_A03/A06On)
    // Wall panel addons (Statics): reverse-engineered from rg_sts_trk_shl_001
    private const uint IdPanelDetailA = 0x0DB962; // SciIntAddOn_PanelDetail01a (right wall)
    private const uint IdPanelDetailB = 0x0DB963; // SciIntAddOn_PanelDetail01b (left wall)
    // Ceiling duct segments (Static)
    private const uint IdDuct         = 0x066838; // SciIntDuct02
    // Pipe addons (Statics, StarStation interior style — used in rg_sts_trk_shl_001)
    private const uint IdPipe01       = 0x097F9F; // StsGenIntAddOn_Pipe01
    private const uint IdPipe03       = 0x097FA2; // StsGenIntAddOn_Pipe03

    private readonly StarfieldMod _targetMod;
    private readonly ModKey _starfieldModKey;
    private int _connSeq = 1;
    private int _spawnSeq = 1;

    public SciHallwayGenerator(StarfieldMod targetMod, ModKey starfieldModKey)
    {
        _targetMod = targetMod;
        _starfieldModKey = starfieldModKey;
    }

    /// <summary>
    /// Generates a straight N-S hallway PackIn and adds it to the target mod.
    /// </summary>
    /// <param name="editorId">EditorID for the new PackIn (e.g. "rg_gen_sts_trk_shl_001")</param>
    /// <param name="flatTilesStart">Flat tile count at south end (default 2)</param>
    /// <param name="stairCount">Staircase tile count (0 = flat corridor, each adds 2 units rise)</param>
    /// <param name="flatTilesEnd">Flat tile count at north end (default 2)</param>
    /// <returns>FormKey of the created PackIn</returns>
    public FormKey Generate(string editorId, int flatTilesStart = 2, int stairCount = 3, int flatTilesEnd = 2)
    {
        float southZ = 0f;
        float northZ = stairCount * 2f;

        // Y positions:
        //   S connector  at Y = -6,       Z = southZ
        //   S end cap    at Y = -4,       Z = southZ   (occupies [-6, -2])
        //   flat tiles   at Y = 0, 4, … (flatTilesStart-1)*4
        //   stair tiles  at Y = flatTilesStart*4, …     rising 2/tile
        //   flat tiles   at Y = (flatTilesStart+stairCount)*4, …
        //   N end cap    at Y = nCapY,    Z = northZ   (occupies [nCapY-2, nCapY+2])
        //   N connector  at Y = nCapY+2, Z = northZ
        float nCapY = (flatTilesStart + stairCount + flatTilesEnd) * 4f;

        var cell = CreateCell(editorId + "StorageCell");

        // ── Temporary objects (structural / visual) ───────────────────────────

        // Root pivot dummy — always at origin in every PackIn
        AddTemp(cell, IdPivot, 0f, 0f, 0f);

        // South end cap
        AddTemp(cell, IdCap, 0f, -4f, southZ);

        // Flat tiles at south end (alternating v1/v2 to match original rooms)
        for (int i = 0; i < flatTilesStart; i++)
        {
            float y  = i * 4f;
            uint  id = (i % 2 == 0) ? IdWay1 : IdWay2;
            AddTemp(cell, id, 0f, y, southZ);
        }

        // Staircase tiles — each rises 2 units in Z
        for (int i = 0; i < stairCount; i++)
        {
            float y = flatTilesStart * 4f + i * 4f;
            float z = southZ + i * 2f;
            AddTemp(cell, IdStairs, 0f, y, z);
        }

        // Flat tiles at north end (reverse alternation mirrors the south end)
        for (int i = 0; i < flatTilesEnd; i++)
        {
            float y  = (flatTilesStart + stairCount) * 4f + i * 4f;
            uint  id = (i % 2 == 0) ? IdWay2 : IdWay1;
            AddTemp(cell, id, 0f, y, northZ);
        }

        // North end cap
        AddTemp(cell, IdCap, 0f, nCapY, northZ);

        // ── Persistent objects (game logic) ───────────────────────────────────

        // North connector — XMarkerHeading, faces north (rotation Z = 0)
        AddPersist(cell, IdConn, 0f, nCapY + 2f, northZ,
            editorId: "rg_conn_n_D1_station_" + (_connSeq++).ToString("D3"),
            rot: new P3Float(0f, 0f, 0f));

        // South connector — XMarkerHeading, faces south (rotation Z = π)
        AddPersist(cell, IdConn, 0f, -6f, southZ,
            editorId: "rg_conn_s_D1_station_" + (_connSeq++).ToString("D3"),
            rot: new P3Float(0f, 0f, MathF.PI));

        // Enemy spawns distributed along the corridor
        PlaceSpawns(cell, flatTilesStart, stairCount, flatTilesEnd, southZ, northZ);

        // Decorations: lights, wall panels, pipes, ceiling ducts
        PlaceDecorations(cell, flatTilesStart, stairCount, flatTilesEnd, southZ, northZ, nCapY);

        // ── PackIn record ─────────────────────────────────────────────────────
        var packin = new PackIn(_targetMod)
        {
            EditorID          = editorId,
            MajorRecordFlagsRaw = 512, // Prefab flag
            ObjectBounds = new ObjectBounds
            {
                First  = new P3Float(-2f, -6f,        southZ - 0.2f),
                Second = new P3Float( 2f, nCapY + 2f, northZ + 5.8f),
            },
        };
        // FormLink set after construction per Mutagen nullable FormLink rule
        packin.Cell = cell.ToNullableLink<ICellGetter>();
        _targetMod.PackIns.Add(packin);

        Console.WriteLine($"[SciHallwayGenerator] {editorId}: " +
                          $"flatStart={flatTilesStart} stairs={stairCount} flatEnd={flatTilesEnd} " +
                          $"rise={northZ} totalY={nCapY + 8f}");

        return packin.FormKey;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private void PlaceSpawns(Cell cell, int flatStart, int stairs, int flatEnd, float southZ, float northZ)
    {
        int total = flatStart + stairs + flatEnd;

        // Near south connector
        AddPersist(cell, IdSpawn, 0f, 2.8f, southZ,
            editorId: "rg_enemy_spawn_" + (_spawnSeq++).ToString("D3"),
            rot: new P3Float(0f, 0f, 0f));

        // Near north connector
        float nY = total * 4f - 4.5f;
        AddPersist(cell, IdSpawn, 0f, nY, northZ,
            editorId: "rg_enemy_spawn_" + (_spawnSeq++).ToString("D3"),
            rot: new P3Float(0f, 0f, 0f));

        // Mid-corridor spawn if long enough
        if (total >= 6)
        {
            float midY = (flatStart + stairs * 0.5f) * 4f;
            float midZ = southZ + stairs * 1f; // halfway through stair rise
            AddPersist(cell, IdSpawn, 0f, midY, midZ,
                editorId: "rg_enemy_spawn_" + (_spawnSeq++).ToString("D3"),
                rot: new P3Float(0f, 0f, 0f));
        }
    }

    /// <summary>
    /// Creates a new interior Cell and adds it to the mod using the Bethesda block/subblock hierarchy.
    /// Block and subblock numbers are derived from the last two decimal digits of the Cell FormKey ID,
    /// matching the pattern used by gen_shipstruct.cs.
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

        // Derive block and subblock numbers from the Cell's FormKey ID
        // (last decimal digit → block, second-to-last → subblock)
        int blockNum    = (int)(cell.FormKey.ID % 10);
        int subBlockNum = (int)((cell.FormKey.ID / 10) % 10);

        // Find or create the CellBlock
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

        // Find or create the CellSubBlock
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

    // ── Decoration placement ───────────────────────────────────────────────────

    /// <summary>
    /// Places all decorations: lights, entry panels, south pipe cluster, upper duct run.
    /// Pattern reverse-engineered from rg_sts_trk_shl_001 (du_outlaws_template.esm).
    /// </summary>
    private void PlaceDecorations(Cell cell, int flatStart, int stairCount, int flatEnd,
                                  float southZ, float northZ, float nCapY)
    {
        PlaceLights(cell, flatStart, stairCount, flatEnd, southZ, northZ, nCapY);
        PlaceEntryPanels(cell, southZ);
        PlaceSouthPipes(cell, southZ);
        PlaceUpperDucts(cell, flatStart, stairCount, northZ, nCapY);
    }

    /// <summary>
    /// Places a LightUtility_A01On Static mesh + companion Light record at each tile
    /// boundary except the flat→stair and stair→flat transitions.
    ///
    /// Both objects are placed at the same position — this is equivalent to what the
    /// LGT_LightUtility_* PackIns do internally, but placed directly in the cell so
    /// the light is visible when the room prefab is previewed in CK.
    ///
    /// Light Z = (floor height at boundary end) + 3.2.
    /// For stair tiles, the "end" floor is tileBaseZ + 2.
    /// X = +1.5 (right wall) for south+stair section, -1.5 (left wall) for north flat.
    /// </summary>
    private void PlaceLights(Cell cell, int flatStart, int stairCount, int flatEnd,
                              float southZ, float northZ, float nCapY)
    {
        // Build an ordered list of (centerY, baseZ) for every segment including caps.
        var segs = new List<(float y, float z)>();
        segs.Add((-4f, southZ));                                             // south cap
        for (int i = 0; i < flatStart; i++)
            segs.Add((i * 4f, southZ));                                      // south flat tiles
        for (int i = 0; i < stairCount; i++)
            segs.Add((flatStart * 4f + i * 4f, southZ + i * 2f));           // stair tiles
        for (int i = 0; i < flatEnd; i++)
            segs.Add(((flatStart + stairCount) * 4f + i * 4f, northZ));     // north flat tiles
        segs.Add((nCapY, northZ));                                           // north cap

        // Boundary index at which we switch from right wall (+X) to left wall (-X).
        // Lights on the south+stair side go right; north flat lights go left.
        int northSwitchIdx = flatStart + stairCount + 1;

        for (int i = 0; i < segs.Count - 1; i++)
        {
            // Skip flat→stair and stair→flat transitions when stairs are present.
            // These are geometrically cluttered transition zones.
            if (stairCount > 0 && (i == flatStart || i == flatStart + stairCount))
                continue;

            // For stair tiles, the floor at the boundary END is baseZ + 2 (fully risen).
            bool isStairSeg = stairCount > 0
                              && i >= flatStart + 1
                              && i <= flatStart + stairCount;
            float floorZ    = isStairSeg ? segs[i].z + 2f : segs[i].z;

            float lightY = segs[i].y + 2f;      // boundary midpoint (half-tile into next)
            float lightZ = floorZ + 3.2f;        // mid-wall height above local floor
            float lightX = (i < northSwitchIdx) ? 1.5f : -1.5f;

            AddTemp(cell, IdLightMesh,   lightX, lightY, lightZ);
            AddTemp(cell, IdLightRecord, lightX, lightY, lightZ);
        }
    }

    /// <summary>
    /// Pair of SciInt display panel addons at the south entry — one on each wall.
    /// From rg_sts_trk_shl_001: 01a right wall at Y≈-0.9 Z+1.8, 01b left wall at Y≈0.3 Z+2.0.
    /// </summary>
    private void PlaceEntryPanels(Cell cell, float southZ)
    {
        AddTemp(cell, IdPanelDetailA,  1.5f, -0.9f, southZ + 1.8f);   // right wall
        AddTemp(cell, IdPanelDetailB, -1.5f,  0.3f, southZ + 2.0f);   // left wall
    }

    /// <summary>
    /// Four pipe statics clustered on the right wall near the south entry.
    /// From rg_sts_trk_shl_001: Pipe03/01 at X=1, Y=-2..+2, Z=southZ+3.6.
    /// </summary>
    private void PlaceSouthPipes(Cell cell, float southZ)
    {
        float pipeZ = southZ + 3.6f;
        AddTemp(cell, IdPipe03, 1f, -2f, pipeZ);
        AddTemp(cell, IdPipe01, 1f,  0f, pipeZ);
        AddTemp(cell, IdPipe01, 1f,  2f, pipeZ);
        AddTemp(cell, IdPipe03, 1f,  2f, pipeZ);
    }

    /// <summary>
    /// Run of SciIntDuct02 segments along the upper flat section ceiling.
    /// From rg_sts_trk_shl_001: 7× at X=1, Y=17..29 (every 2 units), Z=northZ+3.
    /// Starts 3 units before the upper flat section to cover the stair exit zone.
    /// </summary>
    private void PlaceUpperDucts(Cell cell, int flatStart, int stairCount,
                                 float northZ, float nCapY)
    {
        float startY = (flatStart + stairCount) * 4f - 3f;  // just before upper flat
        float endY   = nCapY + 1f;
        float ductZ  = northZ + 3f;

        for (float y = startY; y <= endY; y += 2f)
            AddTemp(cell, IdDuct, 1f, y, ductZ);
    }
}
