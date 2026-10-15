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
- **QuestAuthoring/** - reusable pieces any quest command builds with (the gen_delve split, 2026-10-15): `AliasGraph` (which alias fills from which, and the list order that lets every one fill). These pieces return what they found and never print or return exit codes, because a UI as well as the CLI calls them; that is a rule for this folder, not the library's existing convention
- **Ships/** — Encounter ship (GBFM) builder
- **SpaceCells/** — Space cell noun + palette
- **Stations/** — Space station noun
- **WorldspaceNouns/** — Worldspace noun

Managers (`CrewManager`, `GangManager`) coordinate selection across their respective subfolder implementations.
