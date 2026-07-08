---
name: investigate
description: Investigate a Starfield form or record type to understand its structure
user_invocable: true
---

# Form Investigation Skill

When the user asks you to investigate a Starfield form, record type, or game concept, follow this workflow to research it and document findings in CLAUDE.md.

## Arguments

The user will provide context about what to investigate, e.g.:
- `/investigate SurfaceBlock` — investigate a record type
- `/investigate the worldspace terrain setup` — investigate a concept
- `/investigate 0x00000C36` — investigate a specific form

## Workflow

### Step 1: Run gen_inspect

If investigating a specific record type or form, run gen_inspect to dump its properties:

```bash
cd /c/Git/FrankyCLI && dotnet run -- gen_inspect <RecordType> <search>
```

Supported record types: `SurfaceBlock`, `Worldspace`, `PackIn`, `Cell`, `Static`, `Activator`, `Npc`, `Location`

Use `list` as record type to see available groups and counts.

If investigating a concept rather than a specific record, skip to Step 2.

### Step 2: Search the Codebase

Search for how the form/concept is used in the codebase:
- Grep for the record type name, EditorID, or FormKey
- Read relevant files that create, modify, or reference the form
- Check CLAUDE.md for existing documentation on the topic

### Step 3: Search Mutagen Source (if needed)

If the record's Mutagen type is unclear, search for the interface/class definition:
- Check what properties are available on the getter/setter types
- Look at how other gen_ files or passes use the same record type

### Step 4: Summarize Findings

Present findings to the user, including:
- What the record/form is and what it does
- Key properties and their purposes
- How it's currently used in the codebase
- Any Mutagen API notes (getter vs setter types, DeepCopy behavior, etc.)

### Step 5: Update CLAUDE.md

If the investigation revealed useful, stable knowledge (not session-specific), add it to CLAUDE.md under an appropriate section. Follow the existing documentation style — concise, code-example-heavy, focused on practical usage.

Only add to CLAUDE.md if the user confirms the findings are worth documenting.

## Notes

- gen_inspect uses reflection to dump properties, so output may include internal Mutagen fields — focus on the meaningful ones
- The tool loads the full Starfield.esm load order, so it takes a moment to start
- For binary data (BTD files, etc.), use gen_btd_info instead
