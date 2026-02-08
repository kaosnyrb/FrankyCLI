# CLAUDE.md

## Project Overview

FrankyCLI is a .NET 6.0 CLI tool for procedural content generation for **Starfield** modding. It has two major subsystems:

1. **Ship Part Generation** - Creates ESM/ESP mod files for Starfield ship components (CO, GBFM, PKIN, CELL, MSTT records)
2. **Retrograde** - Procedural dungeon/station generation and AI-driven quest creation

## Build & Run

```bash
# Build
dotnet build

# Run (general form)
dotnet run -- <modname> <mode> <prefix> <itemname> <modelfilepath>

# Example: generate ship structure
dotnet run -- FrankyTest struct ft cargo avontech\ats_cargo_04.nif

# Example: generate quest
dotnet run -- du_outlaws_template gen_quest 0 0 0 0
```

There is no automated test suite. Testing is done manually via batch files (e.g., `upgrades_test.bat`, `avontechshipflips.bat`) and CLI execution.

## Available Modes

Defined in `Program.cs`:

| Mode | Generator Class | Purpose |
|------|----------------|---------|
| `struct` | `gen_shipstruct` | Create ship structure records |
| `flip` | `gen_shipflips` | Mirror ship parts |
| `yrotate` | `gen_shipyrotates` | Y-axis rotation |
| `yrotate45` | `shipyfortyfiverotates` | 45-degree rotation |
| `cellfix` | `gen_cellfixer` | Fix cell issues |
| `placer` | `gen_placer` | Place objects |
| `pluginmerger` | `gen_pluginmerger` | Merge plugins |
| `upgradegenerator` | `gen_upgradegenerator` | Weapon upgrade generation |
| `spaceencounterquest` | `gen_spaceencounterquest` | Space encounter quests |
| `branchcreator` | `gen_branchcreator` | Create branches |
| `shipicons` | `gen_msicon` | Ship icon generation |
| `gen_quest` | `gen_quest_main` | Quest generation (Retrograde) |

## Project Structure

```
FrankyCLI/
├── Program.cs                  # Entry point - mode dispatch
├── gen_*.cs                    # Top-level generator modules
├── Retrograde/                 # Procedural station/dungeon system
│   ├── Passes/                 # Generation passes (pipeline architecture)
│   │   ├── Topology/           # Room layout (Trunk, District, Boss, Scatter, Bridging)
│   │   ├── Content/            # Doors, enemies, loot
│   │   ├── Events/             # Optional loot rooms, infections, bounties
│   │   ├── Sealing/            # Connector/window sealing
│   │   ├── Utility/            # Setup, plugins, lights, markers
│   │   └── IGenPass.cs         # Pass interface
│   ├── StationDesigns/         # Station design implementations
│   ├── FactionMembers/         # Faction-specific crew definitions
│   └── [Utils]                 # ConnectorUtils, BridgeUtil, ScoringUtil, etc.
├── questgen_tools/             # Quest generation framework
│   ├── Nouns/                  # Quest entities (Gangs, NPCs, Ships)
│   ├── Chains/                 # Quest chain implementations
│   ├── Interfaces/             # Quest system interfaces
│   └── TemplateEngines/        # Quest template managers
├── questgen_quests/            # Quest content and templates
│   ├── Discovery/              # Discovery quest templates
│   ├── Investigation/          # Investigation quest templates
│   ├── Showdown/               # Showdown quest templates
│   ├── Templates/              # Mission templates
│   └── Lorefiles/              # Faction/character lore (embedded at build)
├── Utils/                      # General utilities (YAML, FormKeys)
└── Properties/                 # .NET launch settings
```

## Key Dependencies

- **Mutagen.Bethesda.Starfield** (v0.44.0) - Bethesda plugin (ESM/ESP) read/write
- **Mutagen.Bethesda.FormKeys.Starfield** (v3.2.0) - Starfield form key definitions
- **OpenAI** (v2.6.0) - AI-driven quest script generation
- **YamlDotNet** (v16.3.0) - YAML serialization

## Architecture: Retrograde Pass Pipeline

The Retrograde system uses a **pass-based pipeline**. Each pass implements `IGenPass` and operates on a shared `DungeonState` object:

1. **Topology passes** - Build room layout (trunk, districts, boss rooms, scatter, bridges)
2. **Content passes** - Populate with doors, enemies, loot, alert coverage
3. **Event passes** - Add optional scenarios (locked loot rooms, infections, bounties)
4. **Sealing passes** - Seal unused connectors and windows
5. **Utility passes** - Setup, plugin wiring, lights, ship markers

A `PlanRunner` evaluates multiple generated plans using a `ScoringSystem` and selects the best.

## Coding Conventions

- **EditorIDs**: `{prefix}_{type}_{itemname}` (e.g., `ats_ms_core01`)
- **Classes**: PascalCase (e.g., `BridgingTopologyPass`)
- **Methods**: PascalCase, verb-based intent names (e.g., `TryPlaceBossRoomAtConnector`)
- **Constants**: PascalCase, descriptive (e.g., `MaxCandidatePrefabsPerConnector`)
- **Utilities**: Static helper classes grouped by domain (`ConnectorUtils`, `BridgeUtil`, `RoomUtils`, `PlacementUtil`, `ScoringUtil`, `MathUtil`)
- **Nullable**: Enabled project-wide
- **Implicit usings**: Enabled

## Refactoring Patterns

When refactoring Retrograde passes, follow the skill at `.claude/skills/refactor/skill.md`. Key patterns:

- **Context objects** - Bundle 5+ related parameters into a class to reduce method signatures
- **Result objects** - Return structured types instead of `out` parameters
- **Stage comments** - Number the algorithmic stages in main methods (`// Stage 1: ...`)
- **Method extraction** - Target max nesting of 2 levels; extract innermost loops first
- **Utility extraction** - Only extract to shared utils if logic appears in 2+ passes; check existing utils first

## Factions

Retrograde supports multiple factions with distinct crew, behavior, and lore:
- Crimson Fleet, Ecliptic, Varuun, Spacers

Faction crew definitions live in `Retrograde/FactionMembers/`. Lore files in `questgen_quests/Lorefiles/` are embedded in the build output.

## Important Notes

- The project targets .NET 6.0 and is primarily a Windows tool (interacts with Starfield game files)
- `dotnet build` is the primary build verification command
- Never allow editing `Starfield.esm` directly (guarded in `Program.cs`)
- Lore markdown files are copied to build output via `<CopyToOutputDirectory>Always</CopyToOutputDirectory>` in the csproj
