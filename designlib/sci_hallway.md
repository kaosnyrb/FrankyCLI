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
| `01803B` | `SciIntHallSm2Way02__SC` | **Corner tile** — 90° bend, pivot at inside corner |
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
| East: `(xConnX, y, z)` | `3π/2` (faces +X) | `rg_conn_e_D1_station_NNN` |
| West: `(xConnX, y, z)` | `π/2` (faces −X) | `rg_conn_w_D1_station_NNN` |

E/W rotations are derived from the S/N convention (Z=0 → faces +Y; each +π/2 turns 90° CCW). Not yet independently CK-validated via a cell dump of a bridge with E/W connectors.

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

### Rotation

`LightUtility_A01On` is wall-mounted; it needs Z-axis rotation to face the corridor:

| Wall | Rotation Z | Value |
|------|-----------|-------|
| +X wall (right) | π/2 | `1.5707964` |
| −X wall (left)  | 3π/2 | `4.712389` |

Only the Z component is non-zero (pure yaw; no tilt). Companion Light records use matching orientation.

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
- **`PanelSmallFlat01` and symmetric grilles use rotation (0,0,0)** — these are flat symmetric panels placed directly in the tile cell. Decorative addons placed in the room cell (not the tile cell) are not symmetric and require per-Static rotations.

**SciIntHallSm2Way02__SC** (`01803B`) internal cell `0110A3` — corner tile (90° bend):

| FormID | EditorID | Position | Rotation | Role |
|--------|----------|----------|----------|------|
| `050AFD` | `SciIntSegSmMidCeiling01` | `(0, 0, 4)` | `(0,0,0)` | Ceiling mesh |
| `050AFE` | `SciIntSegSmMidFloor01` | `(0, 0, 0)` | `(0,0,0)` | Floor (one arm) |
| `050B11` | `SciIntSegSmWallCorIn01` | `(0, 0, 0)` | `(0,0,0)` | Inner corner wall mesh |
| `050B15` | `SciIntSegSmWallCorOut01` | `(0, 0, 0)` | `(0,0,π)` | Outer corner wall mesh |
| `050B1B` | `SciIntSegSmWallMid01` | `(0, 0, 0)` | `(0,0,π/2)` | Wall segment — arm 1 |
| `050B1B` | `SciIntSegSmWallMid01` | `(0, 0, 0)` | `(0,0,0)` | Wall segment — arm 2 (perpendicular) |
| `23AD74` | `SciIntAddOn_PanelSmallCornerIn02` | `(-2, 2, 0)` | `(0,0,0)` | Corner detail panel |
| `23AD77` | `SciIntAddOn_PanelSmallFlatA03` | `(-2, 0, 3)` | `(0,0,0)` | Flat panel — arm 1 wall |
| `23AD77` | `SciIntAddOn_PanelSmallFlatA03` | `(0, 2, 3)` | `(0,0,π/2)` | Flat panel — arm 2 wall |
| `03F808` | `PrefabPackinPivotDummy` | `(0, 0, 0)` | — | Root pivot |

**Confirmed:** pivot `(0,0,0)` is at the **inside corner** of the bend. Two `SciIntSegSmWallMid01` instances at perpendicular rotations (0 and π/2) serve the two corridor arms. The outer corner piece (`SciIntSegSmWallCorOut01`) is rotated π to face outward; the inner corner mesh (`SciIntSegSmWallCorIn01`) sits at (0,0,0). The L-shape spans arm-1 in the +Y direction and arm-2 in the −X direction from the pivot.

---

## Bridge and connector prefab catalog

Source: `du_outlaws_template.esm`. These are the short bridging PackIns used to connect rooms. Unlike the full numbered corridors (`_001`–`_006`), bridges have **no stairs** and no stair-aware lighting logic — all floor Z = 0.

### Complete prefab list

| EditorID | FormKey | Connectors | Shape | Notes |
|----------|---------|------------|-------|-------|
| `rg_sts_trk_shl_sn_12` | `003204` | S(0,0) N(0,8) | cap→cap stub | Minimal — two caps meeting, no straight tile |
| `rg_sts_trk_shl_sn_08` | `00321A` | S(0,0) N(0,12) | cap→1Way→cap | Short straight |
| `rg_sts_trk_shl_sn_20` | `003181` | S(0,0) N(0,20) | cap→3×Way→cap | Medium straight with scatter props |
| `NArg_sts_trk_shl_bend` | `0031A3` | S(0,0) E(6,6) | pure 90° corner | No straight legs; minimal variant |
| `rg_sts_trk_shl_ne_10x6y` | `00568E` | S(0,0) W(−10,6) | corner + legs | S-arm 6Y, W-arm 10X |
| `rg_sts_trk_shl_ss_n08` | `0031EA` | S(0,0) S(−8,0) | U-stub | Both exits face south; 8-unit X gap |
| `rg_sts_trk_shl_ss_n12` | `0031CC` | S(0,0) S(−12,0) | U-stub | 12-unit gap |
| `rg_sts_trk_shl_ss_n16` | `0031DC` | S(0,0) S(−16,0) | U-stub | 16-unit gap |
| `rg_sts_trk_shl_ss_n20` | `0031AC` | S(0,0) S(−20,0) | U-stub | 20-unit gap |
| `rg_sts_trk_shl_sn_n20_20` | `00318D` | S(0,0) N(−20,20) | S-bend | 4 corners, spans −20X and +20Y |
| `rg_sts_trk_shl_sw_n06` | `0031BF` | S(0,0) W(−6,?) | corner | West-exit variant |
| `NArg_sts_trk_shl_ss_threeway` | `00323C` | S(0,0) + 2nd S + ... | 3-way | Multiple south exits |
| `NArg_sts_trk_shl_ss_u4way` | `003249` | 4-way | U-4-way | |
| `NArg_sts_trk_shl_se_10` | `0031CB` | S + E exit 10X | SE corner | |
| `rg_sts_trk_shl_se_14` | `0015A5` | S + E exit 14X | SE corner | |

### Connector direction naming convention

Connector EditorID suffix encodes exit direction:
- `rg_conn_s_D1_station_NNN` — exits **south** (rotation Z = π)
- `rg_conn_n_D1_station_NNN` — exits **north** (rotation Z = 0)
- `rg_conn_e_D1_station_NNN` — exits **east** (+X)
- `rg_conn_w_D1_station_NNN` — exits **west** (−X)

### Prefab naming taxonomy (bridging rooms)

`rg_sts_trk_shl_<dir1><dir2>_<dims>`:
- `sn` — south entry, north exit (both connectors on Y axis, different ends)
- `ss` — south+south: both connectors face south (U-turn / parallel stub)
- `se` — south entry, east exit
- `sw` — south entry, west exit
- `ne` — north entry, east exit (rotated equivalents of se/sw)
- `n<X>` in dims — second connector offset X units in −X direction (e.g. `n20` = X=−20)
- `<X>x<Y>y` — X-arm length and Y-arm length for corner rooms
- `<X>_<Y>` — for S-bends: X displacement and Y displacement

**The number does not equal connector-to-connector distance** in the Y-axis rooms (see validated data table above). Treat the name as an identifier, not a measurement.

---

## Corner tile grammar

The `SciIntHallSm2Way02__SC` tile (`01803B`) is the only bend piece in the kit. It acts as a pivot: one corridor arm arrives from +Y (or −Y), the other exits along ±X.

**Placement rule:** place the corner tile at the intersection of the two corridor centrelines. For a south-to-east turn:
```
S connector at (0,  0, 0)  → rotation Z=π
S cap       at (0,  2, 0)  → occupies Y ∈ [0, 4]
corner      at (0,  6, 0)  → pivot at inside corner of the turn
E cap       at (armX, 6, 0)  → occupies X ∈ [0, 4] from corner
E connector at (armX+2, 6, 0)  → rotation pointing east
```

Where `armX` = corner.X + arm_straight_tiles × 4 + 2 (for end cap). Minimum arm (no straights): armX=4, connector at X=6.

**Arm extension formula:**
```
arm_origin   = corner_position + 2 along arm direction (start of first tile)
cap_position = arm_origin + (n_straight_tiles × 4)
connector    = cap_position + 2
total reach  = 6 + n_straight_tiles × 4  (from corner centre to connector)
```

**Validated from NArg_sts_trk_shl_bend (minimum corner, zero straight legs):**
```
S: connector(0,0) → cap(0,2) → corner(0,6) → cap(4,6) → connector(6,6):E
```

**Validated from rg_sts_trk_shl_ne_10x6y (1 straight leg on W arm):**
```
S: connector(0,0) → cap(0,2) → corner(0,6) → Way02(-4,6) → cap(-8,6) → connector(-10,6):W
```

### Corner tile PlacedObject rotation

The corner tile default orientation (Z=0) has arms going +Y and −X (from pivot internals analysis of cell `0110A3`). Rotating the `PlacedObject` by Z maps to turn direction:

| Rotation Z | Arms from pivot | Turn |
|-----------|-----------------|------|
| `0` | +Y and −X | N-to-W |
| `π/2` | −X and −Y | **S-to-W** |
| `π` | −Y and +X | **S-to-E** |
| `3π/2` | +X and +Y | E-to-N |

Derived analytically from the arm directions confirmed in the tile internals. CK validation still pending (no raw cell dump of a placed corner tile with known rotation).

**X-arm straight tile rotation** (for tiles running along X, not Y): a Y-axis tile rotated to run along −X needs Z = +π/2; along +X needs Z = 3π/2. **Not yet CK-validated** — see Open questions.

### S-bend (sn_n20_20) — chained corners

An S-bend chains 4 corners to route from (0,0) to an offset N exit at (−20,20):
```
S(0,0) → cap(0,2) → corner(0,6)      [S→W turn]
→ Way(-4,6), Way(-8,6) → corner(-12,6) [W→N turn]
→ corner(-12,10)                        [N→W turn — double corner, U mid-turn]
→ Way(-16,10) → corner(-20,10)        [W→N turn]
→ Way(-20,14) → cap(-20,18) → N(-20,20)
```

The two corners at (−12,6) and (−12,10) form a "dog-leg" at the same X: this creates a Z-shaped path that nets +4Y and 0X through two right-angle turns.

### U-stub (ss_n08) — mirrored corners

Two corners face each other, bridged by one or more straight tiles:
```
S(0,0)  → cap(0,2)  → corner(0,6)  → Way(-4,6) → corner(-8,6) → cap(-8,2) → S(-8,0)
```
Name encodes gap between the two south connectors: `ss_n08` = 8 units.

**Bridge tile count:** `nBridge = (xGap − 4) / 4`. Requires xGap ≥ 8 and divisible by 4. Left corner: Z=π/2 (S-to-W). Right corner: Z=π (arms +X east + −Y south).

---

## Bridge lighting

Two tiers, depending on room complexity.

### Tier 1 — Minimal bridges (sn_12, sn_08, NArg_bend)

Single ceiling light PackIn from the template mod, placed directly above the corridor midpoint:

```
EditorID: rgp_hab_light_ceil_cool01_5k
FormKey: 000F25:du_outlaws_template.esm
Position: (0, midY, 3.6777)
```

`midY` = Y position of the structural centre tile (the straight tile or corner, not a cap).

This PackIn contains its own Light record — no separate Static or Light entry needed. The template mod provenance is acceptable; at generation time it must either be imported or replaced by an equivalent ceiling light.

**Design intent:** minimal bridges are transitional spaces — one light overhead is sufficient. More lighting would visually "weight" these as destinations rather than connections.

### Tier 2 — Full corner rooms (ne_10x6y, ss_n08)

One light cluster **above each corner junction**, using:
- `LightUtility_A07On` Static (`2ACD6B:Starfield.esm`) — visible lamp mesh
- A companion Light record — spot light, radius 8
- A template-mod packaging form (`000F1F:du_outlaws_template.esm`)

**Light records used in corner rooms (radius 8, both non-shadow spot lights):**

| FormID | EditorID | Color | Rooms |
|--------|----------|-------|-------|
| `07BC89` | `LGT_Interior_Spot_NS_Warm_002_2k` | RGB(250, 233, 207) warm | ne_10x6y, ss_n08 |
| `133B05` | `LGT_Interior_Spot_NS_Cool_001_4k` | RGB(201, 226, 255) cool | sn_n20_20 |

Contrast with straight corridors which use `LGT_LightUtility06_Spot_S_2K` (`1B29D1`, radius 4, RGB(255, 251, 234)). Bridge corners use **double the radius** — the larger light throw compensates for the open corner geometry where two corridor directions converge.

Cluster placement (from ne_10x6y, one corner):
```
07BC89 (Light)   at (-2.0, 7.02, 3.125)
000F1F (tmpl)    at (-2.0, 7.27, 3.125)
2ACD6B (Static)  at (-2.0, 7.52, 3.125)
```
Offset from corner: ~X=−2 (toward the outer wall), Y=+1 from corner Y, Z=3.13 (near ceiling).

**Design intent:** corners are visual gear-changes. Lighting the corner directly helps the player track the turn; placement on the outer corner wall face (not the inner wall) means the light faces *into* the turn rather than away from it.

### Bridge vs straight corridor — key difference

Straight corridors (`_001`–`_006`) light each tile boundary on the wall face (X=±1.5), alternating sides. Bridge rooms light each **corner** above the turn, and use a ceiling-hung fixture rather than a wall panel. This is consistent: in a straight corridor the wall face is the dominant surface; in a bend the turn itself is the focal point.

---

## Bridge floor dressing

Bridges with enough length (4+ tiles) receive scattered floor props at corridor centre. These are Starfield.esm PackIns — no template mod dependency.

**Cargo/crate PackIns used:**

| FormID | EditorID | Category |
|--------|----------|----------|
| `023F29` | `SC_LD_Carts_A11` | Cart |
| `023E76` | `SC_LD_Carts_D01` | Cart |
| `01E820` | `SC_LD_CratesMedium08` | Crate |
| `012D94` | `SC_LD_CratesMedium12` | Crate |
| `01A0DD` | `SC_LD_CratesLarge23` | Crate |

Typical placement: floor Z=0, X near 0 (corridor centre), Y at a mid-corridor tile. One or two pieces per room — these props signal that this is a working station corridor, not decorative space.

**Design intent:** crates/carts imply cargo movement, reinforcing the "functional route" character of bridge spaces. Rooms (`_001`–`_006`) use wall panels and ducts to signal "here is equipment to inspect"; bridges use cargo to signal "this is how things get from A to B".

**Pipe dressing on inner corner walls (ne_10x6y):**
```
PipeIndSM_Str04_Broken01  (1ECFCB)  at (-2.5, 4.75, 0.0 / 1.94)
PipeIndSM_Str02           (2C5F46)  at (-2.5, 4.75, 0.0)
PipeIndSM_Con2WayL01      (2C5F4B)  at (-2.5, 4.75, 0.0)
```
These are on the **inner corner wall face** (the concave side of the turn), running floor-to-mid-height. Design intent: the concave wall is dead geometry with no structural function — pipes and runs dress it so it reads as purposeful.

---

## AI markers

The example rooms (`rg_sts_trk_shl_001`) place `ShipMarker_CombatTargetChainMarker` (`18E8C2:Starfield.esm`) as Persistent objects at floor level. Two per room: one near the south entry, one near the north entry.

These are AI combat target chain markers — they wire NPC patrol/combat target sequences. The generator currently does not place them (not a blocker for structural correctness; relevant if AI behaviour needs tuning).

## Wall dressing

SciInt panel addons (Statics, `Architecture\ScienceKit\Interiors\Addons\DisplayPanels\`). Placed at `X = ±1.5` (inner wall face). **Rotation is per-Static** — decorative addons are not symmetric and must be oriented to face the corridor. The `SciIntAddOn_PanelSmallFlat01` used inside tile cells is flat and symmetric (rotation `(0,0,0)`); freestanding room addons are not.

### South entry panels (one per room, near S cap)

| FormID | EditorID | Position | Rotation |
|--------|----------|----------|----------|
| `0DB962` | `SciIntAddOn_PanelDetail01a` | `(+1.5, −0.9, southZ + 1.8)` right wall | `(1.222, 1.5708, 4.364)` |
| `0DB963` | `SciIntAddOn_PanelDetail01b` | `(−1.5, +0.3, southZ + 2.0)` left wall | `(-1.107, -1.5708, 1.107)` |

The X (pitch) and Y (roll) components are non-zero — these panels are angled/tilted, not just yaw-rotated. The values are precise from the room_001 cell dump.

### Scattered wall panel addons — confirmed rotations

All rotations are for panels on N-S corridor walls (corridor runs along Y, walls at X=±1.5). The Y (roll) component is the dominant signal: Y=+π/2 for +X wall, Y=−π/2 for −X wall. The other two components encode panel-specific orientation in the face plane.

| FormID | EditorID | Wall | Rotation | Notes |
|--------|----------|------|----------|-------|
| `0C2C27` | `SciIntAddOn_PanelVent01a` | −X | `(π, -π/2, 3π/2)` = `(3.1416, -1.5708, 4.7124)` | vent panel |
| `0C14E7` | `SciIntAddOn_PanelPlain03d` | +X | `(π/2, π/2, 0)` = `(1.5708, 1.5708, 0)` | PackIn (geometry depth) |
| `0C14EE` | `SciIntAddOn_PanelPlain02c` | +X | `(0, π/2, 3π/2)` = `(0, 1.5708, 4.7124)` | PackIn |
| `0D0912` | `SciIntAddOn_PanelGreeb01a` | −X | `(-π/2, -π/2, π)` = `(-1.5708, -1.5708, 3.1416)` | south entry greeble |
| `0C2C19` | `SciIntAddOn_PanelPlain01a` | +X | `(π, π/2, π)` = `(3.1416, 1.5708, 3.1416)` | plain panel A |
| `0C2C15` | `SciIntAddOn_PanelPlain02b` | +X | `(-3π/4, π/2, 3π/4)` = `(-2.356, 1.5708, 2.356)` | plain panel B |
| `0D0910` | `SciIntAddOn_PanelStorage01a` | — | E-W corridor only: north face `(-π/2, 0, π)` | not used in N-S corridors |
| `0C2C1B` | `SciIntAddOn_PanelPlain04a` | — | E-W corridor only: south face `(π/2, 0, π/2)` | not used in N-S corridors |

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

- **Wall addon rotation** — *Resolved.* `LightUtility_A01On` uses Z-only rotation: `(0, 0, π/2)` on +X wall, `(0, 0, 3π/2)` on −X wall. Freestanding panel Statics (PanelDetail01a/b, PanelVent01a, etc.) use complex multi-axis rotations that are per-Static — see Wall dressing tables. The assumption that `(0,0,0)` was universal was wrong; it only holds for flat symmetric panels inside tile cells.
- **`LGT_SciIntAddOn_LightPanel` orientation** — *Resolved for LightUtility_A01On.* Wall-mounted, Z-rotation only (π/2 / 3π/2). The `LGT_*` PackIn variants are not placed inside room cells at all (double-nesting suppresses the bundled Light); they are only valid for direct worldspace placement. No further rotation question for room usage.
- **Corner tile internal structure** — *Resolved.* Cell `0110A3` fully dumped; pivot confirmed at inside corner. Inner corner: `SciIntSegSmWallCorIn01` (`050B11`). Outer corner: `SciIntSegSmWallCorOut01` (`050B15`, Rot π). Two perpendicular wall mid segments. See Tile internal structure section.
- **North entry panels** — room_001 only has south-side wall panels. Some rooms may want a symmetric north entry panel set.
- **Floor mats** — `FloorMatMedium01/02` (`25B053`, `25B04E`) and `FloorMatOffice_02/03` (`2FCD12`, `2FCD13`) appear in rooms 003/006 but not in the baseline room_001. Not yet implemented.
- **Bridge Light record types** — *Resolved.* `07BC89` = `LGT_Interior_Spot_NS_Warm_002_2k` (warm, r=8), `133B05` = `LGT_Interior_Spot_NS_Cool_001_4k` (cool, r=8). See Bridge lighting section.
- **Bridge naming number** — the number suffix in `sn_08`, `sn_12`, `sn_20` does not straightforwardly map to connector-to-connector distance. Treat as an opaque identifier.
- **`sn_20` lighting** — uses `AK_CeilingLight01_ON` (`1378AB:Starfield.esm`, Akila ceiling light) rather than LightUtility. This is the longest bridge (20Y); the choice may be intentional (different feel for longer spans) or incidental. Not yet resolved whether this is a design rule or just a recycled fixture.
- **X-arm tile and cap rotations** — Not yet CK-validated. Generator uses Z=π/2 for −X tiles, Z=3π/2 for +X tiles (logical values from axis rotation). Validate by dumping a corner bridge cell (e.g. `NArg_sts_trk_shl_bend` or `rg_sts_trk_shl_ne_10x6y`) and reading the rotation of a `SciIntHallSm1Way02__SC` entry in the X arm. Same question applies to `SciIntHallSmCapScktA01__SC` cap rotation on X arm.
- **E/W connector rotation** — Derived as Z=3π/2 (east) and Z=π/2 (west) from the S/N convention. Not yet CK-validated — no cell dump of a bridge with E/W connectors was performed.
- **Scattered wall panel rotations** — *Resolved.* All 8 panels now have confirmed rotations in the Wall dressing table. `PanelStorage01a` and `PanelPlain04a` are E-W corridor addons only — they don't appear in any standard N-S room (`_001`–`_006`). `PanelGreeb01a` on −X wall: `(-1.5708, -1.5708, 3.1416)`; `PanelPlain01a` on +X: `(3.1416, 1.5708, 3.1416)`; `PanelPlain02b` on +X: `(-2.356, 1.5708, 2.356)`. General rule: Y component = +π/2 for +X wall, −π/2 for −X wall.
