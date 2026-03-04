# Core

Shared infrastructure used across all generation systems.

- **AI/** — LLM prompt management, ElevenLabs speech synthesis, seed and flavour tools
- **Generation/** — Dungeon generation internals: connectors, placement, room caching, scoring, math/geometry utilities, BTD terrain reader
- **Tools/** — Record lookup helpers: NPC, faction, ship, armour, activator, cell, and FormKey tools
- **IModContext.cs** — Core interface for mod context passed through the generation pipeline
