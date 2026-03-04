# StationGen

Everything needed to generate procedural interior space stations.

- **StationDungeonGenerator.cs** — Top-level dungeon generator: sequences passes and produces the final station cell
- **StationPreviewGenerator.cs** — Lightweight generator for previewing station layouts without full record creation
- **Designs/** — Station design definitions (hab, ore, warren stations) + design registry
- **Passes/** — Generation passes organised by phase:
  - **Content/** — Doors, enemies, alert primitives, general content placement
  - **Events/** — Special event passes (bounty targets, infection events, key loot rooms)
  - **Sealing/** — Connector and window sealing
  - **Topology/** — Room layout passes (boss, bridging, district, exit, scatter, trunk, util)
  - **Utility/** — Support passes (bridge helpers, light occluders, plugs, ship markers, station setup)
  - **DungeonState.cs / ScoringSystem.cs** — Shared state and scoring used across passes
