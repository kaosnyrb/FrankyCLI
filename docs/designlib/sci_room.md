# SciInt Room Design

Science-kit interior **room** tiles (SciIntRmSm kit). Larger enclosed spaces — destinations, not transit corridors. Source rooms reverse-engineered: `rg_sts_trk_big_001`–`006` from `du_outlaws_template.esm`.

See also: [sci_hallway.md](sci_hallway.md) for the hallway corridor kit used to connect these rooms.

---

## Coordinate system

Local origin is near the room's south-west entry corner. The room extends into positive X (east) and positive Y (north).

```
X = 0..~20      E-W span of the standard 20×20 room
Y = -10         south face (where S connector lives)
Y = +10         north face (where N connector lives)
X = -10         west face
X = +10 (or 26) east face — 26 for elongated rooms
Z = 0           floor level (all connectors are at Z=0 except elevated rooms)
Z = ~4          ceiling for standard-height rooms (same as hallway tile height)
Z = 8           upper-tier floor in multi-level rooms (big_006)
Z = ~12         ceiling of tall/multi-level rooms
```

---

## Wall tile kit

All Statics under `Architecture\ScienceKit\Interiors\RoomSmall\`.

### Perimeter walls

| FormID | EditorID | Role |
|--------|----------|------|
| `024C99` | `SciIntRmSmMidFull01` | Full mid wall section — dominant straight-wall piece |
| `024CA3` | `SciIntRmSmWallMid01` | Wall mid section v1 |
| `042C1F` | `SciIntRmSmWallMid02` | Wall mid section v2 |
| `024CA7` | `SciIntRmSmWallMid_ScktA01` | Wall mid with socket A (addon attachment) |
| `024CA9` | `SciIntRmSmWallMid_ScktB01` | Wall mid with socket B |
| `024CAA` | `SciIntRmSmWallMid_ScktC01` | Wall mid with socket C |
| `024CA6` | `SciIntRmSmWallTransLg_ScktA01` | Large wall transition with socket A |
| `024CA8` | `SciIntRmSmWallTransLg_ScktB01` | Large wall transition with socket B |
| `024C9A` | `SciIntRmSmWallCorIn01` | Inside corner |
| `024C9B` | `SciIntRmSmWallCorIn_ScktA_Dbl01` | Inside corner — double socket A |
| `024C9C` | `SciIntRmSmWallCorIn_ScktA_L01` | Inside corner — socket A, left-biased |
| `024C9D` | `SciIntRmSmWallCorIn_ScktA_R01` | Inside corner — socket A, right-biased |
| `024C9E` | `SciIntRmSmWallCorOut01` | Outside corner |

**Usage pattern:** `SciIntRmSmMidFull01` (024C99) is used most heavily (9–21× per room) and forms the bulk of straight wall runs. Inside-corner pieces (`024C9A` family) appear 2–4× per room. Socket variants create interactive/decorative wall panel attachment points.

**"Full" means combined floor + wall + ceiling.** `MidFull01` is a single tile that includes the floor slab, wall panel, and ceiling slab together. There are no separate floor/ceiling Statics in the RoomSmall kit — unlike the HallSm kit where floor and ceiling are independent pieces (`SciIntSegSmMidFloor01`, `SciIntSegSmMidCeiling01`). The `WallMid01` variant is a wall-only piece (no floor/ceiling) used in specific configurations where the floor/ceiling is handled elsewhere.

### Partition walls (interior dividers)

All Statics under `Architecture\ScienceKit\Interiors\PartitionSmall\`.

| FormID | EditorID | Role |
|--------|----------|------|
| `0563A5` | `SciIntParSmWallA_MidFull01` | Partition mid full |
| `0563A6` | `SciIntParSmWallA_MidMed01` | Partition mid medium |
| `0563A7` | `SciIntParSmWallA_MidSm01` | Partition mid small |
| `0563A3` | `SciIntParSmWallA_MidFull_ExSm01` | Partition mid full extra-small |
| `0563A4` | `SciIntParSmWallA_MidFull_Win01` | Partition mid full with window |
| `0563A2` | `SciIntParSmWallA_CorInSm_R01` | Partition inside corner small (R) |

**Usage pattern:** Used 8–10× per compact room (big_001, big_002) to subdivide the floor area into sub-zones. Not present in the elongated rooms (big_004, big_005), which instead use the length itself for zoning. The window variant (`0563A4`) provides visual connectivity between sub-zones without breaking the light model.

**Design intent:** Partitions signal that the room has distinct function areas — a patrol area vs. a work area, or a security post vs. open storage. Rooms without partitions read as undivided warehouses or transit spaces.

---

## Connector layout

Same XMarkerHeading (`000034:Starfield.esm`) and naming convention as hallways and bridges.

### Rotation convention

Big rooms use **inward-facing S connectors** (RotZ=0, same as bridge/corner rooms), not the outward-facing RotZ=π used in straight corridors:

| Direction | RotZ | Notes |
|-----------|------|-------|
| S (Y=−10) | `0` (faces north, inward) | Same as bridge room convention |
| N (Y=+10) | `0` (faces north, outward) | |
| E (X=+10/26) | `1.5708` (π/2) | |
| W (X=−10) | `4.7124` (3π/2) | |
| Elevated (Z=8) | as above | Z position encodes tier height |

### X/Y offset convention

Connector positions are **not centred on walls** — they sit at whichever X or Y position corresponds to the corridor opening in the wall tile. The position encodes which exact wall-tile opening the connector belongs to.

Elevated connectors in big_006 (Z=8) are reached via `SciIntHallSm1WayStairs01__SC` stair tiles — the same stair tile used in hallways. The stair section terminates at a standard XMarkerHeading connector at the upper Z.

---

## Room catalog

Source: `du_outlaws_template.esm`. FormKeys are from that mod.

### Summary

| EditorID | FormKey | Bounds (W × D × H) | Shape | Connectors | Enemies | Theme |
|----------|---------|---|---|---|---|---|
| `rg_sts_trk_big_001` | `000FFD` | 19.8 × 20.9 × 3.9 | Square | 4 (S/W/E/N) | 12 | Partitioned utility |
| `rg_sts_trk_big_002` | `00107A` | 19.8 × 19.8 × 4.6 | Square | 4 (S/W/E/N) | 7 | Lightly guarded utility |
| `rg_sts_trk_big_003` | `001105` | 19.6 × 19.6 × 13 | Square + tall | 4 (S/W/E/N) | 14 | Vertical equipment tower |
| `rg_sts_trk_big_004` | `001206` | 35.5 × 20 × 4 | Long E-W | 5 (S/W/2×N/E) | 20 | Flat warehouse |
| `rg_sts_trk_big_005` | `001278` | 35.6 × 19.6 × 6 | Long E-W + two-tier | 5 (S/W/2×N/E) | 27 | Two-tier warehouse |
| `rg_sts_trk_big_006` | `0012E6` | 36 × 24 × 12 | Large + multi-level | 6 (S/W/2×N/2×E) | 23 | Multi-level hub |

---

### big_001 — Partitioned utility room

**PackIn:** `000FFD:du_outlaws_template.esm`
**Cell:** `000FFE:du_outlaws_template.esm`
**ObjectBounds:** First(−9.8, −9.97, −0.1) Second(9.8, 10.92, 3.81)

**Connectors:**
| EditorID | Position | RotZ |
|----------|----------|------|
| `rg_conn_s_D1_station_030` | (0, −10, 0) | 0 |
| `rg_conn_w_D1_station_031` | (−10, 4, 0) | 4.7124 |
| `rg_conn_e_D1_station_032` | (10, 0, 0) | 1.5708 |
| `rg_conn_n_D1_station_031` | (8, 10, 0) | 0 |

**Enemy spawns:** 12 (spread across sub-zones)
**Temporary objects:** 194

**Structure:**
- Perimeter: `SciIntRmSmMidFull01` (9×) + `SciIntRmSmWallMid01` (8×) + corners (3×)
- Internal partitions: `SciIntParSmWallA_*` (~9× partition panels)
- Conduit runs: `SciIntParSmWallA_MidFull01` / `SciIntParSmWallA_MidSm01` used as conduit/pipe routing
- Lighting: `LightUtility_A01On` (10×) + `LightUtility_A04On` (3×)

**Design intent:** Standard four-connector junction room. High enemy density (12) suggests a strongly-contested utility hub. Partitions break sightlines, creating cover for defenders.

---

### big_002 — Lightly guarded utility room

**PackIn:** `00107A:du_outlaws_template.esm`
**Cell:** `00109B:du_outlaws_template.esm`
**ObjectBounds:** First(−9.8, −9.8, −0.11) Second(10, 10, 4.64)

**Connectors:**
| EditorID | Position | RotZ |
|----------|----------|------|
| `rg_conn_s_D1_station_032` | (4, −10, 0) | 0 |
| `rg_conn_w_D1_station_033` | (−10, 4, 0) | 4.7124 |
| `rg_conn_e_D1_station_034` | (10, 8, 0) | 1.5708 |
| `rg_conn_n_D1_station_033` | (8, 10, 0) | 0 |

**Enemy spawns:** 7 (sparse)
**Temporary objects:** 182

**Structure:** Near-identical footprint and kit usage to big_001 — same 9× MidFull01, 8× WallMid01, similar partitions and lighting. Fewer enemies (7 vs 12) — this is a lower-threat variant of the same room archetype.

**Design intent:** A "quieter" version of big_001. Same connector layout positions suggest these two are interchangeable from a routing perspective but present different difficulty profiles.

---

### big_003 — Vertical equipment tower

**PackIn:** `001105:du_outlaws_template.esm`
**Cell:** `001106:du_outlaws_template.esm`
**ObjectBounds:** First(−9.8, −9.8, −0.65) Second(9.8, 9.8, 12.27)

**Connectors:**
| EditorID | Position | RotZ |
|----------|----------|------|
| `rg_conn_s_D1_station_033` | (4, −10, 0) | 0 |
| `rg_conn_w_D1_station_034` | (−10, −4, 0) | 4.7124 |
| `rg_conn_e_D1_station_035` | (10, 0, 0) | 1.5708 |
| `rg_conn_n_D1_station_034` | (0, 10, 0) | 0 |

**Enemy spawns:** 14
**Temporary objects:** 433 (most of any room — all vertical fill)

**Key structural difference — vertical stacking:**
- Same 20×20 footprint as big_001/002 (same 9× MidFull01, 8× WallMid01)
- All 4 connectors remain at Z=0 — entry/exit is at floor level, same as flat rooms
- Height filled with `MacKitSmGreeble07` (`29AE8D`, 26×+) and related machine kit pieces
- `ConduitStr04` (`075B1D`, 16×) and `ConduitEndCap01` (`075B29`, 10×) run vertically
- The extra 9 units of ceiling height are fully occupied by stacked equipment

**Design intent:** The room has the same four-exit layout as big_001/002 but reads as a large equipment bay or server stack. The vertical fill makes it feel purposeful and dense — this is a room full of machinery, not open space. Enemy spawns (14) are mid-range — enemies have cover from the equipment stacking but no elevated vantage points (all Z=0 spawns).

**Key rule:** The tall bounding box is driven by visual stacking, not by any vertical gameplay — all connectors and spawns remain at Z=0. The height is purely decorative volume.

---

### big_004 — Flat warehouse

**PackIn:** `001206:du_outlaws_template.esm`
**Cell:** `001207:du_outlaws_template.esm`
**ObjectBounds:** First(−9.71, −9.8, −0.30) Second(25.8, 10.11, 3.81)

**Connectors:**
| EditorID | Position | RotZ |
|----------|----------|------|
| `rg_conn_s_D1_station_039` | (8, −10, 0) | 0 |
| `rg_conn_w_D1_station_035` | (−10, −8, 0) | 4.7124 |
| `rg_conn_n_D1_station_032` | (0, 10, 0) | 0 |
| `rg_conn_n_D1_station_036` | (24, 10, 0) | 0 |
| `rg_conn_e_D1_station_036` | (26, −4, 0) | 1.5708 |

**Enemy spawns:** 20
**Temporary objects:** 318

**Key structural difference — elongated X axis:**
- Extends from X=−10 to X=+26 (36 units wide)
- Two N connectors at X=0 and X=24 — at opposite ends of the long room
- Perimeter filled by `SciIntRmSmMidFull01` (21× — dominant, repeated for long runs)
- No partition walls — the length is the zone separator
- Floor mats: `FloorMatOffice_02` (`2FCD12`, 4×) and `FloorMatOffice_01` (`2FCD11`, 1×) marking work zones
- Heavy utility lighting: `LightUtility_A01On` (29× — most of any room)

**Design intent:** An open warehouse floor. The two north connectors make this a through-space — entering from one N side, exiting from the other, with S and W as secondary access. Floor mats zone the space without walls. High enemy count (20) with no cover partitions makes this an exposed encounter — enemies can see across the full width, but so can the player.

---

### big_005 — Two-tier warehouse

**PackIn:** `001278:du_outlaws_template.esm`
**Cell:** `001279:du_outlaws_template.esm`
**ObjectBounds:** First(−9.8, −9.8, −0.25) Second(25.84, 9.82, 5.88)

**Connectors:**
| EditorID | Position | RotZ |
|----------|----------|------|
| `rg_conn_s_D1_station_041` | (4, −10, 0) | 0 |
| `rg_conn_w_D1_station_036` | (−10, 0, 0) | 4.7124 |
| `rg_conn_n_D1_station_038` | (−4, 10, 0) | 0 |
| `rg_conn_n_D1_station_037` | (16, 10, 0) | 0 |
| `rg_conn_e_D1_station_037` | (26, −4, 0) | 1.5708 |

**Enemy spawns:** 27 (highest of all rooms)
**Temporary objects:** 342

**Key structural difference — same footprint as big_004 but 6 units tall:**
- Same 36×20 elongated X footprint
- Same two-N-connector layout (X=−4 and X=16)
- 6-unit ceiling height (vs 4 in big_004) accommodates a second layer of storage/equipment
- Extensive `PipeIndSM_End*` pipe kit dressing (12× total) running along walls and ceiling
- Machine kit and shelving stacking on upper tier
- Highest enemy count (27) — the elevated visual complexity provides more combat cover

**Design intent:** The two-tier variant of big_004. Where big_004 reads as an open floor, big_005 reads as a working industrial space with overhead equipment runs and storage at height. The extra 2 units of ceiling provides just enough vertical space for a catwalk-level dressing layer without qualifying as a true second floor (no elevated Z connectors).

---

### big_006 — Multi-level industrial hub

**PackIn:** `0012E6:du_outlaws_template.esm`
**Cell:** `0012E7:du_outlaws_template.esm`
**ObjectBounds:** First(−6, −10.12, −0.20) Second(30, 14, 11.80)

**Connectors:**
| EditorID | Position | RotZ | Tier |
|----------|----------|------|------|
| `rg_conn_s_D1_station_042` | (0, −10, 0) | 0 | ground |
| `rg_conn_w_D1_station_139` | (−6, 12, 0) | 1.5708 | ground |
| `rg_conn_n_D1_station_030` | (28, 14, 0) | 0 | ground |
| `rg_conn_e_D1_station_038` | (30, 0, 0) | 1.5708 | ground |
| `rg_conn_n_D1_station_029` | (20, 14, 8) | 0 | **upper (Z=8)** |
| `rg_conn_e_D1_station_041` | (30, 12, 8) | 1.5708 | **upper (Z=8)** |

**Enemy spawns:** 23 (with Z>0 spawns at Z=4.4, 6.6, 8 — enemies on upper levels)
**Temporary objects:** 419 (largest by count)

**Key structural difference — multiple levels with actual elevated access:**
- 36×24 unit footprint (largest)
- `SciIntHallSm1WayStairs01__SC` (`024466`, 4×) used as **internal stair section** to reach upper level at Z=8
- `SciIntHallSmCapScktA01__SC` (`024441`, 3×) and `SciIntHallSm1WayScktA01__SC` (`012CE8`, 3×) used alongside stairs
- `Ind_ShelfKitA02` (`257AB7`, 25×) is the dominant dressing piece — industrial shelf kits covering the walls
- Enemy spawns at Z=4.4, 6.6, and 8 — the upper level is a true gameplay area, not just decorative fill
- 6 connectors including 2 at Z=8 — the upper level is accessible from both N and E at height

**Design intent:** The hub of the dungeon. Six connectors make this a crossroads that multiple paths flow through. The ground-to-upper vertical transition (via stair tiles) creates an asymmetric encounter: players entering at ground must deal with enemies above before or during ascent. The dominant `Ind_ShelfKitA02` shelving suggests a massive storage/cargo facility — the hub is a warehouse through which everything passes.

**Cross-kit note:** big_006 mixes the RoomSmall wall kit with SciIntHallSm hallway tiles for the internal stair section. This confirms the kits are composable — hallway tiles can appear inside room cells when a vertical connection is needed.

---

## Lighting

**Primary fixture:** `LightUtility_A01On` (`2ACD6C:Starfield.esm`) — wall-mounted strip light. Dominant across all rooms (9–35× per room). Same Static used in hallways. Requires a companion Light record (see sci_hallway.md for details).

**Secondary fixture:** `LightUtility_A04On` (`2ACD6F:Starfield.esm`) — ceiling-mount or accent variant. Used 2–4× per room as supplemental fill.

**No tier-specific lighting rule** has been established yet. Rooms with elevated areas (big_006) likely use the same LightUtility fixtures but at the upper Z — unconfirmed.

---

## Decoration dressing

### Structural decoration (vertical volume)

| FormID | EditorID | Rooms | Role |
|--------|----------|-------|------|
| `29AE8D` | `MacKitSmGreeble07` | big_003 | Machine kit greeble — equipment stack fill |
| `075B1D` | `ConduitStr04` | big_003, big_006 | Conduit straight — vertical run |
| `075B29` | `ConduitEndCap01` | big_003 | Conduit end cap |
| `257AB7` | `Ind_ShelfKitA02` | big_006 | Industrial shelf kit — wall/storage fill |

### Storage and containers

| FormID | EditorID | Rooms | Role |
|--------|----------|-------|------|
| `23753F` | `CrateGeneric_Cloth_A01` | big_006 | Generic cloth crate |
| `237543` | `StorageBinA_01` | big_006 | Storage bin |
| `2A0E38` | `FilingStorageBoxBlack_01Static` | big_002, big_006 | Filing storage box |

### Floor dressing

| FormID | EditorID | Rooms | Role |
|--------|----------|-------|------|
| `2FCD12` | `FloorMatOffice_02` | big_004 | Office floor mat v2 |
| `2FCD11` | `FloorMatOffice_01` | big_004 | Office floor mat v1 |

### Pipe dressing

| FormID | EditorID | Rooms | Role |
|--------|----------|-------|------|
| `0C45A1` | `PipeIndSM_End01` | big_005 | Industrial small pipe end 1 |
| `0C459E` | `PipeIndSM_End02` | big_005 | Industrial small pipe end 2 |
| `0C459A` | `PipeIndSM_End03` | big_005 | Industrial small pipe end 3 |
| `0C4596` | `PipeIndSM_End01B` | big_005 | Industrial small pipe end 1B |
| `0C4597` | `PipeIndSM_End02B` | big_005 | Industrial small pipe end 2B |

**Design intent:**
- Conduit/MacKit greebles (big_003): vertical visual density — the room is a piece of functional equipment, not an enclosed space
- Ind_ShelfKitA02 (big_006): the room is primarily a storage facility — shelves are the dominant architectural element
- Floor mats (big_004): zone the open warehouse floor into work areas without using walls
- Pipes (big_005): reinforce the industrial two-tier character; pipes run at height marking ceiling level

---

## Design principles

### Scale and role
- **20×20 rooms (001–003):** Four-connector junction or endpoint. Scale is human — you can see across the room, making it feel like an accessible goal.
- **36×20 elongated (004–005):** Two-N-connector transit/warehouse. Scale exceeds human comfort — the room feels like a place that exists for the facility, not for people.
- **36×24 hub (006):** Six-connector nexus. This is the dungeon's spine — multiple paths merge here. Its scale and visual complexity reward players who reach it.

### Vertical expression
- **Standard height (Z=4):** Flat rooms where gameplay is horizontal. Cover comes from partitions or containers.
- **Tall decorative (Z=12, Z=0 connectors, big_003):** Room reads as a machine space. The extra height is equipment, not gameplay. All engagement at floor level.
- **Tall functional (Z=12, Z=8 connectors, big_006):** Multi-tier engagement. Upper level is part of the encounter, not just atmosphere.

### Enemy scaling
Enemy counts don't scale linearly with room size. Compact rooms (big_001: 12, big_002: 7) can have high density; the elongated big_005 has the most enemies (27) because its width gives more room for spread. The design signal is threat density per square unit, not raw count.

### Kit composability
SciIntHallSm tiles (stair sections, caps) can be placed inside a RoomSmall cell to provide inter-level access. The room and hallway kits are composable — design decisions should consider which tile achieves the right visual and functional result, regardless of nominal kit membership.

---

## Generator

`Retrograde.Library/RoomPackinGeneration/SciRoomGenerator.cs` — parametric 20×20 room generator.

```csharp
var room = new SciRoomGenerator(targetMod, sfModKey);
room.Generate("rg_gen_sts_trk_big_snew",
    exitSouth: true, exitNorth: true, exitEast: true, exitWest: true);
// → PackIn + Cell written to targetMod
```

**What the generator produces** (standard 20×20 room):
- Floor: 3×3 `SciIntRmSmMidFull01` grid at ±4 spacing (interior area −6..+6)
- Perimeter: `SciIntRmSmWallMid01` ring at ±8, centre tile omitted per exit
- Corners: `SciIntRmSmWallCorIn01` at (±8, ±8) with confirmed rotations
- Lighting: 4 pairs of `LightUtility_A01On` + companion Light on E/W inner faces
- Connectors: centred on each enabled face at ±10; S uses ConnRotNorth (Z=0, inward)
- Spawns: 4 `XMarker`, one per floor quadrant

**Validated tile positions** (confirmed from big_001 cell dump):

| Tile | Position | Rotation Z |
|------|----------|-----------|
| `SciIntRmSmMidFull01` (×9) | {−4,0,+4} × {−4,0,+4} | 0 |
| `SciIntRmSmWallMid01` S wall | (±4, −8) | 3π/2 |
| `SciIntRmSmWallMid01` N wall | (±4, +8) | π/2 |
| `SciIntRmSmWallMid01` E wall | (+8, ±4) | π |
| `SciIntRmSmWallMid01` W wall | (−8, ±4) | 0 |
| `SciIntRmSmWallCorIn01` SE | (+8, −8) | π |
| `SciIntRmSmWallCorIn01` SW | (−8, −8) | 3π/2 |
| `SciIntRmSmWallCorIn01` NE | (+8, +8) | π/2 |
| `SciIntRmSmWallCorIn01` NW | (−8, +8) | 0 |

Run standalone: `dotnet run -- gen_roompackin`

---

## Open questions

- **Tile dimensions** — Exact width of `SciIntRmSmMidFull01` not yet measured. With 9–21 instances covering 20–36 unit wall runs, tiles are likely 4–6 units wide. Needs CK inspection.
- **Socket addon placement** — Wall mid socket variants (`024CA7/9/A`) create attachment points for decorative addons. Which addons are designed for these vs. which addons are placed freestanding is not yet documented.
- **Upper-level lighting (big_006)** — Lighting fixtures at Z=8 not yet extracted. May be same `LightUtility_A01On` at upper Z.
- **W connector rotation in big_006** — `rg_conn_w_D1_station_139` has RotZ=1.5708 (east-facing) rather than 4.7124 (west-facing). This room's W connector is at (−6, 12, 0) — an unusual position. May be a special-case connector that faces the interior, not the exterior. Verify in CK.
- **`rg_slot_*` placement anchors** — Some cells contain `rg_slot_spine_large_*` PlacedObjects referencing template-mod PackIns. These appear to be furniture/equipment slot anchors (where specific items are placed). Not yet documented.
