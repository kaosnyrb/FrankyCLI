# Nouns

Self-contained record builders — each Noun knows how to create one type of Starfield record (NPC, ship, quest, book, etc.) and wire it into the mod.

- **Activators/** — Activator objects (terminals, switches, interactive props)
- **Books/** — Data-slate (BOOK) records
- **Crew/** — Generic crew NPC builders + `ICrew` interface
- **Dialogue/** — NPC dialogue (Scene/DIAL chain) builder
- **Equipment/** — Legendary armour record builder
- **FactionMembers/** — Faction-specific crew implementations + `IFactionMembers` interface
- **Gangs/** — Gang NPC group builders + `IGang` interface
- **Messages/** — MESSAGE record builder
- **NPCs/** — Base outlaw NPC builder
- **Quests/** — Quest record (QUST) builder
- **Ships/** — Encounter ship (GBFM) builder
- **SpaceCells/** — Space cell noun + palette
- **Stations/** — Space station noun
- **WorldspaceNouns/** — Worldspace noun

Managers (`CrewManager`, `GangManager`) coordinate selection across their respective subfolder implementations.
