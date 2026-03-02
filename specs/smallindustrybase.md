# Spec: SmallIndustryBase Worldspace Design

## Overview

A small industrial complex POI placed on procedurally flattened terrain. Three to six
man-made prefab PackIn buildings are clustered tightly around a central point. The
result reads as a working (or abandoned) production/processing site rather than a fort
or a research outpost.

Closest analogue in the existing codebase: **FortDesign** — tile-map layout pass feeds
`TileInstantiationPass`. The industrial variant uses a much simpler layout (a few
buildings in a loose cluster, no walls or gates) but follows the same
MapPass → CellBuildPass pipeline.

---

## Pass Structure

```
MapPasses:
  IndustryPackInLibraryPass()              // register building prefab IDs — needs rewrite (see below)
  IndustryLayoutPass(scale)                // place 3–6 building tiles on map
  TerrainFlattenPass()                     // flatten tiles chosen by layout pass
  TerrainRestorePass()                     // restore terrain outside footprint

CellBuildPasses:
  NavmeshSeedPass()
  TileInstantiationPass()                  // converts map tiles → placed PackIns

ContentPasses:
  LodLayerPass()
  RockScatterPass(0.4f)
  VegetationScatterPass(0.2f)              // sparse — industrial sites suppress plants
  MapMarkerPass(MarkerType.Industrial)     // value 21
  TravelMarkerPass()
  PlanetContentManagerPass(...)
  PlanetScanPass(...)
  PlanetQuestPass(...)
  WorldspaceBossPass()
```

No `BuildingDecoratorPass` — the PackIn prefabs already contain interior detail.

**`IndustryPackInLibraryPass` must be rewritten** alongside `IndustryLayoutPass`. The
existing version builds 7 string keys structured around the old inner/outer ring
(`industry_centre`, `industry_solar`, `industry_outer`, etc.). The new version should
build **one key per category** matching the flat pool in Selected Prefabs (e.g.
`industry_abandoned`, `industry_large`, `industry_comms`, …). `IndustryLayoutPass`
uses these keys when writing tiles to the map; `TileInstantiationPass` resolves each
key to a random `FormKey` from the list at instantiation time.

---

## IndustryLayoutPass

### Goal

Place 3–6 industrial building tiles in a compact cluster that fits the natural terrain.
No walls, gates, or gap-fillers. Each building is a single PackIn tile on the map.
The cluster is found by **candidate sampling with scored evaluation** rather than a
fixed grid, so each generation produces a layout shaped by the actual landscape.

### Dead zone

No building anchor may be placed within **4 map units of any map edge**. This prevents
PackIns from intersecting the terrain-merge band at the worldspace boundary.

### Candidate sampling

Generate **300 candidate configurations**. For each:

1. Pick a random anchor point within the valid zone (map bounds inset by 16 on all sides).
2. Determine building count from `scale` (see below). Select that many building types
   by shuffling the category list and dealing one variant per category in order, cycling
   back to the start of the shuffled list if more buildings are needed than categories.
   Pick a random variant from within each selected category.
3. Place the first building at the anchor.
4. For each remaining building, attempt up to **20 tries**:
   - Pick a random angle and a random radial distance in [**3**, 6] map units from the
     **anchor** (fixed throughout the candidate — not a rolling centroid).
   - Accept the slot if it is within the valid zone and passes `GenerationMap.canPlace`
     (checks that the slot and its ±1 tile neighbourhood are all empty — the real
     footprint of `placesmalltile` is 3×3, so two anchors need at least 2 map units
     of clearance; distance ≥ 3 guarantees this).
   - If no valid slot is found in 20 tries, discard this candidate and move on.
5. Assign a random 0 / 90 / 180 / 270 ° rotation to each building independently.

After all 300 candidates are generated (or attempted), score each and select the best.

### Fallback

If fewer than **5 valid candidates** are produced (e.g. extreme terrain, very small
valid zone), fall back to placing buildings in a compact grid centred on the map centre,
using the same building selection and rotation logic. Log a warning so the result can
be reviewed.

### Scoring

Each candidate receives a **weighted score** (higher = better). Each component is
normalised to [0, 1] across the valid candidate set before weighting. If all candidates
score identically on a component (range = 0), treat every candidate's normalised value
for that component as **1.0** (no penalty for a tie).

| Component | Weight | Definition |
|-----------|--------|------------|
| **Flatness** | 0.40 | `1 - normalise(mean height variance)` — for each occupied tile, convert the four corner map coordinates to BTD world coords: `btdX = (mapX - mapCentre) * TileWorldSize * (4096f / 100f)`, then call `BtdFile.SampleHeightAtWorld(btdX, btdY)` (result is already in overlay Z — no further divide needed). Compute variance across all corner samples for all buildings. Lower variance = flatter ground = higher score. |
| **Clustering** | 0.30 | `1 - normalise(mean pairwise distance)` — mean Euclidean distance between every pair of building anchors in map coordinates. Tighter cluster = higher score. |
| **Line-of-sight** | 0.20 | `1 - normalise(total ridge penalty)` — for each pair of buildings, sample 8 evenly-spaced points along the connecting line. Convert each point to BTD world coords using the same formula as Flatness. Call `SampleHeightAtWorld` at each point. For each sample, compute the excess above the linear interpolation between the two endpoint heights; sum all positive excesses across all pairs. Less intervening terrain = higher score. |
| **Variation** | 0.10 | `(categoryVariety + rotationVariety) / 2` — `categoryVariety` = unique building categories ÷ total buildings; `rotationVariety` = unique rotation values ÷ `min(4, buildingCount)` (so a 3-building candidate with 3 unique rotations scores 1.0). |

### Scale parameter

`scale` clamps to [0.1, 1.0] and controls building count:

- `scale < 0.35` → 3 buildings
- `scale ≥ 0.35 and < 0.525` → 4 buildings
- `scale ≥ 0.525 and ≤ 0.7` → 5 buildings
- `scale > 0.7` → 6 buildings

---

## Terrain Flattening

`TerrainFlattenPass` already handles this: it flattens BTD under any tile slot that
has a prefab. With candidate sampling, buildings are placed within 6 map units of the
anchor, so the flattened footprint is at most ~12×12 map units (~48×48 overlay units),
well inside a 4×4 BTD.

No custom flatten logic is needed — the existing pass is sufficient.

---

## Prefab Reference — GPPIPCMManMade PackIns

All records are from `Starfield.esm`. Prefix: `GPPIPCMManMade_`.
All categories below are in the active pool (see Selected Prefabs).

### Abandoned Industrial (large, derelict shells)

| EditorID suffix | FormKey |
|-----------------|---------|
| AbandondedIndustrialLarge01 | 0004AC39 |
| AbandondedIndustrialLarge02 | 006AAD4 |
| AbandondedIndustrialLarge03 | 070C7B |

### Industrial Large (active / intact variants)

| EditorID suffix | FormKey |
|-----------------|---------|
| IndustrialLargeA01 | 302A6D |
| IndustrialLargeA02 | 302B2E |
| IndustrialLargeA03 | 302CB1 |
| IndustrialLargeB01 | 30332E |
| IndustrialLargeB02 | 3033C7 |
| IndustrialLargeB03 | 3037DA |

### Reactor

| EditorID suffix | FormKey |
|-----------------|---------|
| Reactor01 | 00304C3E |
| Reactor02 | 3051BA |

### Storage Bay (warehouse buildings, B/C/D variants + dilapidated)

| EditorID suffix | FormKey |
|-----------------|---------|
| StorageBayB01 | 2FCEC3 |
| StorageBayB02 | 2FD45E |
| StorageBayB03 | 2FDE57 |
| StorageBayB04 | 2FE325 |
| StorageBayB05 | 2FEB98 |
| StorageBayC01 | 2FEF80 |
| StorageBayC02 | 2FF7D3 |
| StorageBayC03 | 2FF8BA |
| StorageBayC04 | 2FFCA5 |
| StorageBayC05 | 3004A1 |
| StorageBayD01 | 3004A4 |
| StorageBayD02 | 30069B |
| StorageBayD03 | 30073D |
| StorageBayD04 | 3008A5 |
| StorageBayD05 | 300CFF |
| StorageBayDilap01 | 300E82 |
| StorageBayDilap02 | 3018A5 |
| StorageBayDilap03 | 301D33 |
| StorageBayDilap04 | 301DCD |
| StorageBayDilap05 | 301F99 |

### Fluid Storage (tanks / silos)

| EditorID suffix | FormKey |
|-----------------|---------|
| FluidStorageMediumA01 | 304355 |
| FluidStorageMediumA02 | 3044DE |
| FluidStorageMediumA03 | 304A87 |
| FluidStorageMediumB01 | 2F4173 |
| FluidStorageMediumB02 | 2F4179 |
| FluidStorageMediumB03 | 2F4180 |
| FluidStorageMediumB04 | 2F4186 |
| FluidStorageMediumD01 | 30420D |
| FluidStorageMediumD02 | 3042D3 |
| FluidStorageLargeA01 | 2F6B1D |
| FluidStorageLargeA02 | 2F6B1E |
| FluidStorageLargeA03 | 2F6B24 |
| FluidStorageLargeB01 | 2F6B25 |
| FluidStorageLargeB02 | 2F6B26 |
| FluidStorageXLargeB01 | 2F6B41 |
| FluidStorageXLargeB02 | 2F6B42 |
| FluidStorageXLargeC01 | 2F6B44 |
| FluidStorageXLargeC02 | 2F6B45 |

### Generic Mechanical (machinery / equipment clusters)

| EditorID suffix | FormKey |
|-----------------|---------|
| GenericMechanicalLargeA01 | 0658F9 |
| GenericMechanicalLargeB01 | 0658FB |
| GenericMechanicalLargeB02 | 06D8BE |
| GenericMechanicalLargeC01 | 076544 |
| GenericMechanicalLargeC02 | 076546 |
| GenericMechanicalMediumA01 | 076548 |
| GenericMechanicalMediumA02 | 07654A |
| GenericMechanicalMediumA03 | 07654C |
| GenericMechanicalMediumA04 | 07654E |
| GenericMechanicalMediumA05 | 076550 |
| GenericMechanicalMediumA06 | 078F4F |
| GenericMechanicalMediumA07 | 078F54 |
| GenericMechanicalMediumA08 | 0658F6 |

### Concrete Foundations (pads / platforms)

| EditorID suffix | FormKey |
|-----------------|---------|
| ConcreteFoundationsLarge01 | 070C78 |
| ConcreteFoundationsLarge02 | 13DE13 |
| ConcreteFoundationsLarge03 | 147238 |
| ConcreteFoundationsLarge04 | 14FF23 |

### Clutter Piles (scatter / dressing)

| EditorID suffix | FormKey |
|-----------------|---------|
| ClutterPileMedium01 | 06EA2A |
| ClutterPileMedium02 | 070C0A |
| ClutterPileMedium03 | 070C5A |
| ClutterPileMedium04 | 070C70 |

### Communications (antennae / relays)

| EditorID suffix | FormKey |
|-----------------|---------|
| CommunicationsA01 | 250739 |
| CommunicationsC01 | 25073E |
| CommunicationsC02 | 278B9C |
| CommunicationsC03 | 2872A5 |
| CommunicationsC04 | 287DAD |
| CommunicationsD01 | 289522 |
| CommunicationsD02 | 2DD558 |
| CommunicationsE01 | 2DD5EF |

### Solar Panels (power infrastructure)

| EditorID suffix | FormKey |
|-----------------|---------|
| SolarPanelsA01 | 256995 |
| SolarPanelsA02 | 257CE6 |
| SolarPanelsA03 | 257CE8 |
| SolarPanelsB01 | 259CBE |
| SolarPanelsB02 | 280967 |
| SolarPanelsB03 | 28A882 |
| SolarPanelsC01 | 2F7FC2 |
| SolarPanelsC02 | 2F866F |
| SolarPanelsC03 | 2F87A5 |

---

## Selected Prefabs for This Design

All categories form a single flat pool. Each building slot picks a random variant from
a randomly selected category, with the sampler biased toward distinct categories (see
Variation score). There is no fixed centre building or outer ring.

| Category | Variants | FormKeys |
|----------|----------|----------|
| AbandonedIndustrial | Large01, Large02, Large03 | 0004AC39, 006AAD4, 070C7B |
| IndustrialLarge | A01, A02, A03, B01, B02, B03 | 302A6D, 302B2E, 302CB1, 30332E, 3033C7, 3037DA |
| Communications | A01, C01, C02, C03, C04, D01, D02, E01 | 250739, 25073E, 278B9C, 2872A5, 287DAD, 289522, 2DD558, 2DD5EF |
| GenericMechanicalLarge | A01, B01, B02, C01, C02 | 0658F9, 0658FB, 06D8BE, 076544, 076546 |
| GenericMechanicalMedium | A01–A08 | 076548, 07654A, 07654C, 07654E, 076550, 078F4F, 078F54, 0658F6 |
| FluidStorageXLarge | B01, B02, C01, C02 | 2F6B41, 2F6B42, 2F6B44, 2F6B45 |
| FluidStorageLarge | A01–A03, B01–B02 | 2F6B1D, 2F6B1E, 2F6B24, 2F6B25, 2F6B26 |
| FluidStorageMedium | A01–A03, B01–B04, D01–D02 | 304355, 3044DE, 304A87, 2F4173, 2F4179, 2F4180, 2F4186, 30420D, 3042D3 |
| SolarPanels | A01, A02, A03, B01, B02, B03, C01, C02, C03 | 256995, 257CE6, 257CE8, 259CBE, 280967, 28A882, 2F7FC2, 2F866F, 2F87A5 |
| Reactor | Reactor01, Reactor02 | 00304C3E, 3051BA |
| StorageBay | B01–B05, C01–C05, D01–D05, Dilap01–Dilap05 | 2FCEC3, 2FD45E, 2FDE57, 2FE325, 2FEB98, 2FEF80, 2FF7D3, 2FF8BA, 2FFCA5, 3004A1, 3004A4, 30069B, 30073D, 3008A5, 300CFF, 300E82, 3018A5, 301D33, 301DCD, 301F99 |
| ConcreteFoundations | Large01–Large04 | 070C78, 13DE13, 147238, 14FF23 |
| ClutterPiles | Medium01–Medium04 | 06EA2A, 070C0A, 070C5A, 070C70 |

All buildings use a random 0 / 90 / 180 / 270 ° rotation independently.

---

## WorldspaceDesignRegistry

Add `SmallIndustryBaseDesign` to `WorldspaceDesignRegistry.cs`. Use a representative
template worldspace (e.g. `DR001World`) as the default; the same pool used by
`ScienceOutpostDesign` is appropriate since terrain size requirements are similar (4×4).

---

## Name Generator

Industrial complex names: `{Adjective} {Noun}`.

Adjective pool (suggestive of neglect/utility):
- Derelict, Abandoned, Remote, Stripped, Overhauled, Operational, Defunct, Active,
  Neglected, Converted, Salvaged, Repurposed, Isolated, Contested, Seized

Noun pool (industrial facilities):
- Processing Plant, Refinery, Industrial Site, Fabrication Bay, Assembly Depot,
  Storage Facility, Extraction Site, Production Complex, Fuel Depot, Cargo Hub,
  Manufacturing Post, Smelting Works, Chemical Plant, Distribution Centre,
  Mineral Processing Site, Operations Hub

---

## Design Intent Notes

- The cluster should feel like a **working site**, not a fortress. Buildings face
  inward toward a central yard rather than presenting a defensive perimeter.
- Flattened terrain should be **clearly artificial** — the abrupt edge of the
  flattened pad is intentional, not a bug. It signals that someone built here.
- `VegetationScatterPass` at 0.2f (rather than the fort's 0.5f) keeps plant density
  low around the site — industrial operations suppress local vegetation.
- No landing pad placed inside the cluster. The travel marker appears at the edge.
- **Flavor alignment (advisory):** AbandonedIndustrial and StorageBayDilap buildings
  read as neglected/derelict; IndustrialLarge, Reactor, and SolarPanels read as active.
  The name generator is independent of building selection, so the two may diverge (e.g.
  "Operational Refinery" with all-derelict prefabs). This is acceptable for now; a
  future pass could weight the adjective pool based on the dominant category.

---

## Open Questions

1. **Candidate count tuning** — 300 candidates is a starting point. If generation is
   slow in practice, drop to 150. If layouts feel repetitive, raise to 500.
2. **Radial spread** — currently [3, 6] map units from anchor. Lower bound 3 enforces
   `canPlace` clearance (3×3 footprint). If buildings cluster too tightly, raise the
   upper bound to 8.
