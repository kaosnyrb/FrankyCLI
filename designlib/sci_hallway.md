# SciInt Hallway Design

Science-kit interior straight corridor. Generator: `Retrograde.Library/RoomPackinGeneration/SciHallwayGenerator.cs`.

Source rooms reverse-engineered: `rg_sts_trk_shl_001`–`006` from `du_outlaws_template.esm`.

---

## Coordinate system

The corridor runs along the **Y axis** (south → north). Y increases northward. All positions are local to the PackIn cell (origin at the south flat section start).

```
X = 0          corridor centreline
X = ±1.5       inner wall faces (where addons attach)
X = ±2.0       outer wall faces (structural wall mesh)
Z = southZ     south section floor (always 0)
Z = northZ     north section floor (= stairCount × 2)
Z = floorZ + 4 ceiling at any section (tile height = 4 units)
```

---

## Layout grammar

```
[S connector Y=-6]
[S end cap   Y=-4]          occupies Y ∈ [-6, -2]
[flat tiles  Y=0, 4, …]    flatTilesStart tiles
[stair tiles Y=…]           stairCount tiles, each rising 2 Z
[flat tiles  Y=…]           flatTilesEnd tiles
[N end cap   Y=nCapY]       occupies Y ∈ [nCapY-2, nCapY+2]
[N connector Y=nCapY+2]
```

**Key formula:** `nCapY = (flatTilesStart + stairCount + flatTilesEnd) × 4`

Each tile is **4 units** along Y, centred on its position (extends ±2). No gaps between tiles.

---

## Tile kit

All Starfield.esm PackIns under `Architecture\ScienceKit\Interiors\HallSmall\`.

| FormID | EditorID | Role |
|--------|----------|------|
| `02447F` | `SciIntHallSm1Way01__SC` | Straight segment v1 |
| `02446E` | `SciIntHallSm1Way02__SC` | Straight segment v2 |
| `024466` | `SciIntHallSm1WayStairs01__SC` | Staircase — rises 2 Z per tile |
| `024441` | `SciIntHallSmCapScktA01__SC` | End cap with socket (connector attachment point) |
| `02444C` | `SciIntHallSmCap01__SC` | End cap plain (no socket) — used in non-straight rooms |
| `0185DE` | `SciIntHallSm3Way01__SC` | T-junction (3-way) |
| `012CE8` | `SciIntHallSm1WayScktA01__SC` | Straight with socket A |

**Alternation rule (flat tiles):** south flat tiles alternate v1/v2 starting with v1; north flat tiles alternate v2/v1 (reversed) — this creates a visual seam-break between sections.

---

## Stair placement

```
stair[i].Y = flatTilesStart × 4 + i × 4
stair[i].Z = southZ + i × 2          (base Z of tile i)
```

Stair tile i rises from `baseZ` to `baseZ + 2` over its 4-unit Y span. Floor height at tile boundary = `baseZ + 2`.

`northZ = stairCount × 2` — total rise.

---

## Connectors (Persistent)

XMarkerHeading statics. EditorID encodes direction and a sequence number.

| Position | Rotation Z | EditorID pattern |
|----------|-----------|-----------------|
| South: `(0, -6, southZ)` | `π` (faces south) | `rg_conn_s_D1_station_NNN` |
| North: `(0, nCapY+2, northZ)` | `0` (faces north) | `rg_conn_n_D1_station_NNN` |

FormID: `000034:Starfield.esm` (`XMarkerHeading`).

---

## Enemy spawns (Persistent)

XMarker statics (`00003B:Starfield.esm`). Always 2 minimum; a third mid-corridor spawn is added when total tile count ≥ 6.

| Spawn | Y | Z |
|-------|---|---|
| Near south | `2.8` | `southZ` |
| Near north | `(total × 4) − 4.5` | `northZ` |
| Mid-corridor (if total ≥ 6) | `(flatStart + stairs × 0.5) × 4` | `southZ + stairs × 1` |

---

## Lighting

**Use `LightUtility_A01On` Static (`2ACD6C:Starfield.esm`) + companion Light record (`1B29D1:Starfield.esm`)**, both placed directly in the room PackIn's cell Temporary list.

Do **not** use `LGT_*` PackIns here. When a `LGT_*` PackIn is placed inside a room PackIn's cell, it is one nesting level deeper — its bundled Light record is **not rendered** when the outer room prefab is previewed in CK. Only objects placed directly in the cell (Statics, Light records) are rendered during prefab preview.

The `LGT_*` PackIns are correct for direct worldspace placement, where no outer PackIn wraps them.

### Placement rule

One light at each tile boundary (between consecutive segments), **except**:
- the flat→stair transition boundary (index `flatStart`)
- the stair→flat transition boundary (index `flatStart + stairCount`)

These skips avoid placing lights in the geometrically cluttered step-up/step-down zones.

### Z formula

```
floorZAtBoundary = stairTile.baseZ + 2    (for stair segments — fully risen at boundary end)
                 = segment.Z              (for flat tiles and caps)
lightZ = floorZAtBoundary + 3.2
```

The `+3.2` offset puts lights at mid-wall height (ceiling is at `floorZ + 4`).

### X side

```
south + stair region  →  X = +1.5  (right / +X wall)
north flat region     →  X = −1.5  (left / −X wall)
switch point: boundary index >= flatStart + stairCount + 1
```

### Validated positions (room_001, flatStart=2, stairs=3, flatEnd=2)

| Boundary | Y | expected Z | formula |
|----------|---|-----------|---------|
| cap → flat0 | −2 | 3.2 | `0 + 3.2` |
| flat0 → flat1 | 2 | 3.2 | `0 + 3.2` |
| *(skip: flat→stair)* | 6 | — | — |
| stair0 → stair1 | 10 | 5.2 | `(0+2) + 3.2` |
| stair1 → stair2 | 14 | 7.2 | `(2+2) + 3.2` |
| *(skip: stair→flat)* | 18 | — | — |
| flatN0 → flatN1 | 22 | 9.2 | `6 + 3.2` |
| flatN1 → cap | 26 | 9.2 | `6 + 3.2` |

All values confirmed against `du_outlaws_template.esm`.

### Light panel variants available

| FormID | EditorID |
|--------|----------|
| `1A5FC0` | `LGT_SciIntAddOn_LightPanel_A01` |
| `1A5FBD` | `LGT_SciIntAddOn_LightPanel_A02` |
| `1A5FB9` | `LGT_SciIntAddOn_LightPanel_A03` |
| `1A5FB6` | `LGT_SciIntAddOn_LightPanel_B01` |
| `1A5FAD` | `LGT_SciIntAddOn_LightPanel_B02` |
| `1A5FB0` | `LGT_SciIntAddOn_LightPanel_C01` |
| `1A5FB3` | `LGT_SciIntAddOn_LightPanel_C02` |
| `1A5F9F` | `LGT_SciIntAddOn_LightPanel_D02` |

Also available for wall-mount spots: `LGT_LightUtility_A02` (`1A6092`), `B01` (`1A6020`), `C01` (`1A6008`) and others.

---

## Tile internal structure

Each SciIntHallSm tile PackIn contains its own interior cell with the structural mesh pieces. Knowing these helps diagnose why tiles look wrong and explains the wall face positions.

**SciIntHallSm1Way01__SC** (`02447F`) internal cell `005AFC`:

| FormID | EditorID | Position | Role |
|--------|----------|----------|------|
| `050AFD` | `SciIntSegSmMidCeiling01` | `(0, 0, 4)` | Ceiling mesh (at Z+4) |
| `050AFE` | `SciIntSegSmMidFloor01` | `(0, 0, 0)` | Floor mesh |
| `23AD80` | `SciIntAddOn_PanelSmallFlat01` | `(±2, 0, 0)` | Outer wall panels (at X=±2, not ±1.5) |
| `050B1B` | `SciIntSegSmWallMid01` | `(0, 0, 0)` ×2 | Wall segment geometry |
| `03F808` | `PrefabPackinPivotDummy` | `(0, 0, 0)` | Root pivot |

Key dimensions extracted from internals:
- **Ceiling at Z + 4** above tile base Z (consistent across all tile types)
- **Outer wall face at X = ±2**, inner wall face at X = ±1.5 (where addons attach)
- **Panel addons placed with rotation (0,0,0)** — Bethesda's own `PanelSmallFlat01` uses no rotation at X=±2; addon placement at X=±1.5 follows same convention

## AI markers

The example rooms (`rg_sts_trk_shl_001`) place `ShipMarker_CombatTargetChainMarker` (`18E8C2:Starfield.esm`) as Persistent objects at floor level. Two per room: one near the south entry, one near the north entry.

These are AI combat target chain markers — they wire NPC patrol/combat target sequences. The generator currently does not place them (not a blocker for structural correctness; relevant if AI behaviour needs tuning).

## Wall dressing

SciInt panel addons (Statics, `Architecture\ScienceKit\Interiors\Addons\DisplayPanels\`). Placed at `X = ±1.5` (inner wall face) with rotation `(0, 0, 0)` — consistent with how Bethesda places the `SciIntAddOn_PanelSmallFlat01` inside the tile cells at `X = ±2`.

### South entry panels (one per room, near S cap)

| FormID | EditorID | Position |
|--------|----------|----------|
| `0DB962` | `SciIntAddOn_PanelDetail01a` | `(+1.5, −0.9, southZ + 1.8)` right wall |
| `0DB963` | `SciIntAddOn_PanelDetail01b` | `(−1.5, +0.3, southZ + 2.0)` left wall |

### Scattered wall panel addons (additional variety)

| FormID | EditorID | Notes |
|--------|----------|-------|
| `0C2C27` | `SciIntAddOn_PanelVent01a` | vent panel |
| `0D0912` | `SciIntAddOn_PanelGreeb01a` | greeble detail |
| `0D0910` | `SciIntAddOn_PanelStorage01a` | storage |
| `0C2C19` | `SciIntAddOn_PanelPlain01a` | plain panel A |
| `0C2C15` | `SciIntAddOn_PanelPlain02b` | plain panel B |
| `0C2C1B` | `SciIntAddOn_PanelPlain04a` | plain panel C |
| `0C14E7` | `SciIntAddOn_PanelPlain03d` | PackIn (has geometry depth) |
| `0C14EE` | `SciIntAddOn_PanelPlain02c` | PackIn |

Typical Z offset from local floor: `+1.8` to `+2.5` (lower-wall placement), `+6` to `+7.5` (upper placement in north section).

---

## Pipe dressing

StarStation interior pipes (Statics, `StarStations\Gen\Interiors\AddOns\`). Placed in a cluster on the right wall near the south entry.

| FormID | EditorID |
|--------|----------|
| `097F9F` | `StsGenIntAddOn_Pipe01` |
| `097F9E` | `StsGenIntAddOn_Pipe02` |
| `097FA2` | `StsGenIntAddOn_Pipe03` |

**South pipe cluster (from room_001):** four pipes at `X = 1`, `Z = southZ + 3.6`, Y = `−2, 0, +2, +2` (Pipe03, Pipe01, Pipe01, Pipe03).

---

## Ceiling ducts

`SciIntDuct02` (`066838:Starfield.esm`), short duct segment Static. Placed in a run along the upper flat section ceiling.

```
startY = (flatStart + stairCount) × 4 − 3   (just before upper flat)
endY   = nCapY + 1
step   = 2 Y units
X      = 1 (right side)
Z      = northZ + 3                           (near-ceiling: ceiling is northZ + 4)
```

From room_001 (7 ducts at Y = 17, 19, 21, 23, 25, 27, 29, Z = 9). Runs from the stair exit zone through to just past the north cap.

---

## ObjectBounds

```csharp
First  = new P3Float(-2f, -6f,        southZ - 0.2f)
Second = new P3Float( 2f, nCapY + 2f, northZ + 5.8f)
```

Y extents include the connector markers (Y = `−6` to `nCapY + 2`). Z extents add a small margin below floor and just under ceiling height (`northZ + 6`).

---

## Open questions

- **Wall addon rotation** — not confirmed from data. The `gi.sh` Cell dump does not output rotation. Placement with rotation `(0, 0, 0)` matches how Bethesda places `SciIntAddOn_PanelSmallFlat01` inside tile cells, so this is likely correct, but needs visual verification in CK.
- **`LGT_SciIntAddOn_LightPanel` orientation** — unknown if these need a specific rotation to mount flush to the wall vs. hanging from ceiling. Visual verification needed.
- **North entry panels** — room_001 only has south-side wall panels. Some rooms may want a symmetric north entry panel set.
- **Floor mats** — `FloorMatMedium01/02` (`25B053`, `25B04E`) and `FloorMatOffice_02/03` (`2FCD12`, `2FCD13`) appear in rooms 003/006 but not in the baseline room_001. Not yet implemented.
