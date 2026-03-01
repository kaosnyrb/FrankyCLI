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
  PackInLibraryPass(IndustryPackInIds())   // register building prefab IDs
  TerrainFlattenPass()                     // flatten tiles occupied by buildings
  IndustryLayoutPass(scale)                // place 3–6 building tiles on map
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

---

## IndustryLayoutPass

### Goal

Place 3–6 industrial building tiles in a compact cluster near the map centre.
No walls, gates, or gap-fillers. Each building is a single PackIn tile on the map.

### Layout algorithm

The cluster uses a **fixed slot grid** rather than random offsets, so the result is
always roughly square. Buildings are drawn from the Selected Prefabs pool (see below);
solar panels are always placed on the outer ring.

**Inner ring — 8 candidate slots** at step ±3 from centre (24, 24):

```
( 21, 27 )  ( 24, 27 )  ( 27, 27 )
( 21, 24 )  [ centre ]  ( 27, 24 )
( 21, 21 )  ( 24, 21 )  ( 27, 21 )
```

Procedure:
1. Place the first selected building at (24, 24).
2. Shuffle the 8 inner slots and the remaining selected buildings (both) with the RNG.
3. Fill slots in order until all selected buildings are placed.
   Each building gets a random 0/90/180/270° rotation.
4. **Outer ring:** 4 candidate slots at step ±6 from centre on the cardinal axes:
   (24,30), (24,18), (18,24), (30,24). Shuffle, pick 1–2. Each slot draws one random
   variant from the combined outer pool (solar panels, reactors, storage bays, fluid
   storage, mechanical medium, concrete foundations, clutter piles). Solar panel
   variants use 0° rotation; all others get a random 0/90/180/270° rotation.
5. Buildings are standalone — they must not touch each other. No shared doorways,
   connectors, or corridors. No wall pass. No gate/stair pass. No small scatter on
   base tiles.

The inner ring produces a ≤3×3 footprint of 3-unit tiles = ~9×9 map units
(~36×36 overlay units). Outer solar slots add another 3 units of clearance on
whichever cardinal side they land.

### Scale parameter

`scale` clamps to [0.1, 1.0] and controls how many buildings from the pool are placed:
- `scale < 0.35` → first 2 buildings from pool (+ centre = 3 total)
- `scale 0.35–0.7` → first 3–4 buildings
- `scale > 0.7` → all buildings in the pool

Solar panel count (1 or 2) is also scaled: `scale < 0.5` → 1 panel, else 2.

---

## Terrain Flattening

`TerrainFlattenPass` already handles this: it flattens BTD under any tile slot that
has a prefab. Because the cluster is small (3–6 tiles at grid step 3), the flattened
footprint stays within a ~18×18 map-unit square (~72×72 overlay units), well inside
a 4×4 BTD.

No custom flatten logic is needed — the existing pass is sufficient.

---

## Prefab Reference — GPPIPCMManMade PackIns

All records are from `Starfield.esm`. Prefix: `GPPIPCMManMade_`.
**★** = category used by this design (see Selected Prefabs table).

### Abandoned Industrial ★ (large, derelict shells)

| EditorID suffix | FormKey |
|-----------------|---------|
| AbandondedIndustrialLarge01 | 0004AC39 |
| AbandondedIndustrialLarge02 | 006AAD4 |
| AbandondedIndustrialLarge03 | 070C7B |

### Industrial Large ★ (active / intact variants)

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

### Fluid Storage (tanks / silos) — XLarge variants ★

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

### Generic Mechanical ★ (machinery / equipment clusters)

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

### Communications ★ (antennae / relays)

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

Each inner ring slot picks a random variant from within its assigned category each
generation. The first slot (centre) always draws from the AbandonedIndustrial category.
The remaining slots draw from the other categories in shuffled order.

**Inner ring buildings — one random variant per category per generation:**

| Category | Variants in pool | FormKeys |
|----------|-----------------|----------|
| AbandonedIndustrial | Large01, Large02, Large03 | 0004AC39, 006AAD4, 070C7B |
| IndustrialLarge | A01, A02, A03, B01, B02, B03 | 302A6D, 302B2E, 302CB1, 30332E, 3033C7, 3037DA |
| Communications | A01, C01, C02, C03, C04, D01, D02, E01 | 250739, 25073E, 278B9C, 2872A5, 287DAD, 289522, 2DD558, 2DD5EF |
| GenericMechanicalLarge | A01, B01, B02, C01, C02 | 0658F9, 0658FB, 06D8BE, 076544, 076546 |
| FluidStorageXLarge | B01, B02, C01, C02 | 2F6B41, 2F6B42, 2F6B44, 2F6B45 |

Scale controls how many categories participate (see Scale parameter above). The
AbandonedIndustrial category (centre slot) is always included.

**Outer ring** (pick 1–2 slots from the combined pool below):

| Category | EditorID suffixes | FormKeys |
|----------|-------------------|----------|
| SolarPanels | A01, A02, A03, B01, B02, B03, C01, C02, C03 | 256995, 257CE6, 257CE8, 259CBE, 280967, 28A882, 2F7FC2, 2F866F, 2F87A5 |
| Reactor | Reactor01, Reactor02 | 00304C3E, 3051BA |
| StorageBay | B01–B05, C01–C05, D01–D05, Dilap01–Dilap05 | 2FCEC3, 2FD45E, 2FDE57, 2FE325, 2FEB98, 2FEF80, 2FF7D3, 2FF8BA, 2FFCA5, 3004A1, 3004A4, 30069B, 30073D, 3008A5, 300CFF, 300E82, 3018A5, 301D33, 301DCD, 301F99 |
| FluidStorageMedium | A01–A03, B01–B04, D01–D02 | 304355, 3044DE, 304A87, 2F4173, 2F4179, 2F4180, 2F4186, 30420D, 3042D3 |
| FluidStorageLarge | A01–A03, B01–B02 | 2F6B1D, 2F6B1E, 2F6B24, 2F6B25, 2F6B26 |
| GenericMechanicalMedium | A01–A08 | 076548, 07654A, 07654C, 07654E, 076550, 078F4F, 078F54, 0658F6 |
| ConcreteFoundations | Large01–Large04 | 070C78, 13DE13, 147238, 14FF23 |
| ClutterPiles | Medium01–Medium04 | 06EA2A, 070C0A, 070C5A, 070C70 |

Each outer slot picks one random variant from the entire combined pool. Solar panel slots use 0° rotation; all other categories use a random 0/90/180/270° rotation.

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

---

## Open Questions

1. ~~**MapMarkerPass.MarkerType** — resolved: use `MarkerType.Industrial` (value 21).~~
2. ~~**Rotation of large PackIns** — resolved: 90° increments are fine for man-made
   building PackIns. `TileInstantiationPass` applies rotation as-is.~~
3. ~~**Connector/door alignment** — resolved: buildings do **not** connect. Each PackIn
   is standalone with open space between it and its neighbours. No doorway or corridor
   alignment is required. If playtesting shows buildings touching, increase the inner
   ring slot step from ±3 to ±4.~~
4. ~~**Landing pad** — resolved: no landing pad. Industrial sites are not player
   landing destinations.~~
