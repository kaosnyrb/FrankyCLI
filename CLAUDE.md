# FrankyCLI - Starfield Bounty Quest Generator

## Project Overview
This is a C# tool that procedurally generates bounty-hunting quests for Starfield using AI-driven lore and prompt systems. It uses the Mutagen library for Bethesda plugin generation.

## Lore System

When generating or editing anything that needs narrative content, lore, quest text, flavour text, character profiles, mission briefings, or any in-universe writing, you MUST reference the existing lore files and follow their structure.

### Lore File Location
All lore files live in `questgen_quests/Lorefiles/` as `.md` files.

### Lore File Structure
Every lore file uses this exact XML-like structure:

```
<LoreContext>
    <Summary>
        <StorySummary>
            3-5 sentence overview: who the fugitive is, why they're hunted,
            and the tone of the pursuit.
        </StorySummary>
    </Summary>

    <LorePrompts>
        <TargetProfile>   - Background, skills, affiliations, triggering crime, rumors, emotional weight
        <Rumors>           - Conflicting accounts, strange sightings, exaggerated tales
        <Leads>            - Physical evidence, testimony, locations visited
        <Locations>        - Hideouts, environments, hazards reflecting the target's strategy
        <Motives>          - Guilt, fear, ideology, hidden connections, personal stakes
        <Threats>          - Rival hunters, traps, environmental dangers
        <MysteryElements>  - Unanswered questions, inconsistencies, hints of something larger
    </LorePrompts>
</LoreContext>
```

### Rules for Creating New Lore Files
- Each lore file defines ONE fugitive/bounty target with a unique archetype and narrative hook.
- The `<StorySummary>` sets the tone for the entire file. It should be evocative but concise.
- Each `<LorePrompts>` subsection is a generation directive ("Generate 1 lore entry about...") that the AI fills at runtime.
- Lore must be original, self-contained, and open-ended enough to generate many different quests.
- Tone: Starfield-adjacent sci-fi. Factions, frontier settlements, grav jumps, Colony War history are all fair game.
- No copyrighted characters or settings. Fully original content only.
- Avoid generic/vague archetypes. Each target should have a specific identity, a concrete triggering event, and motives that create moral complexity.
- See existing files (e.g., `NeonGhost.md`, `STARVEDPROPHET.md`, `IronWarden.md`, `Gilded_Viper_Lore.md`) for reference.

### How Lore Is Used in Code
- `PromptManager.cs` loads a random lore file via `LoadRandomLoreFile()` and stores it in the static `LoreContext` field.
- Every prompt method (`GetQuestName`, `GetActivatorName`, `GetLogMessage`, `GetPickupMessage`, etc.) injects `LoreContext` into the AI prompt between `<LoreContext>` and `</LoreContext>` tags.
- `GenerateLoreFile()` in `PromptManager.cs` can also generate lore files dynamically via AI, using a simpler structure (Summary, StorySeed, TargetProfile, Motives).
- When writing or modifying prompt methods that produce in-universe text, always ensure they reference `LoreContext` for narrative consistency.

## Key Directories
- `questgen_tools/` - Core quest generation logic and utilities
- `questgen_tools/Utils/` - PromptManager, AITools, and other utilities
- `questgen_quests/` - Quest definitions and lore files
- `questgen_quests/Lorefiles/` - All lore files (`.md`)
