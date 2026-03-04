# FrankyCLI

Procedural dungeon and quest generation tool for Starfield, built on [Mutagen.Bethesda](https://github.com/Mutagen-Modding/Mutagen).

Generates interior space stations, planetary worldspaces, space cells, and quest chains by composing PackIn tiles, placing objects, writing Starfield records, and outputting a ready-to-load `.esm`.

---

## Project Structure

```
commands/           Gen command entry points, grouped by domain
  btd/              BTD terrain tools (flatten, inspect, test)
  quest/            Quest generation and dialogue tests
  ship/             Ship record tools (struct, flips, rotates)
  tools/            Dev utilities (inspect, report, placer, merger)
  upgrade/          Upgrade generator
  world/            Worldspace and cell tools (roompackin, cellfixer)
  gen_retrograde.cs Main Retrograde procedural generation entry point

Retrograde.Library/ Core library
  Core/             Shared infrastructure
    AI/             LLM prompt management, ElevenLabs speech synthesis
    Generation/     Dungeon gen internals: connectors, placement, scoring, math
    Tools/          Record lookup helpers: NPCs, ships, factions, FormKeys
  Models/           Shared data models passed between passes
  Nouns/            Self-contained record builders, one per Starfield record type
    Quests/         Quest chains, templates, template engines, quest implementations
    SpaceCells/     Space cell noun + designs + generation passes
    Stations/       Station noun + designs + generation passes
    WorldspaceNouns/ Worldspace noun + designs + generation passes
    Crew/           Crew NPC builders
    Gangs/          Gang builders
    FactionMembers/ Faction-specific crew implementations
    Ships/          Encounter ship builder
    Dialogue/       NPC dialogue (Scene/DIAL chain) builder
    Books/          Data-slate (BOOK) builder
    ...
  Prototypes/       Low-level PackIn room/hallway generators (SciHallway, SciRoom)

docs/
  formlib/          Starfield record type reference (PackIn, PlacedObject, Quest, etc.)
  designlib/        Bethesda design patterns reverse-engineered from vanilla content
  specs/            Feature and system specifications

scripts/            Shell and Python utility scripts (gi.sh, lookup_fks.sh, etc.)
bat/                Batch file launchers for common gen runs
Templates/          ESM template files loaded at generation time
```

---

## Running

```bat
bat\gen_quest_main.bat       — Generate a quest chain
bat\build_retrograde.bat     — Full Retrograde generation run
bat\build_station.bat        — Station-only generation run
```

Or directly:

```
dotnet run --project FrankyCLI.csproj -- <command> [args]
```

---

## Documentation

See [`docs/formlib/`](docs/formlib/) for record type references and [`docs/designlib/`](docs/designlib/) for design patterns before working with unfamiliar systems.
