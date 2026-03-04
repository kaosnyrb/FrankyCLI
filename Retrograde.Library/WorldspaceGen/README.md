# WorldspaceGen

Everything needed to generate procedural planetary worldspaces (outdoor bases, forts, industrial sites).

- **WorldspaceDungeonGenerator.cs** — Top-level generator: sequences passes and produces the final worldspace
- **Designs/** — Worldspace design definitions (fort, racetrack, science outpost, small industry base) + design registry
- **Passes/** — Generation passes organised by phase:
  - **Content/** — Building decoration, enemy placement, ponds, rock/vegetation scatter, science buildings, tile instantiation, boss placement
  - **Topology/** — Layout passes (fort, industry, racetrack layouts + terrain shaping)
  - **Utility/** — Support passes (LOD layers, map markers, navmesh seeds, PackIn libraries, PCM, planet quests, planet scan, travel markers)
  - **WorldspaceState.cs** — Shared state passed through all worldspace passes
