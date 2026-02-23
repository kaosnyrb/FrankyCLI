---
name: kaizen
description: End-of-session workflow improvement. Reviews what was learnt and makes small, targeted updates to scripts, formlib, designlib, CLAUDE.md, and memory. Run with /kaizen at the end of a working session.
user_invocable: true
---

# Kaizen — Session Improvement Skill

Run at the end of a working session to capture learnings before they're lost. The goal is **small, targeted improvements** — not a full rewrite. One good addition per store is enough.

Kaizen (改善) means "continuous improvement through small steps."

## What to review

Go through the session conversation and identify:

- **New Starfield record facts** — FormIDs confirmed, field meanings, record relationships
- **New design patterns** — placement rules, coordinate formulas, Bethesda conventions
- **Repeated manual steps** — bash commands you ran more than once that could be a script
- **Gotchas encountered** — things that surprised you or caused a wrong-path detour
- **Open questions** — things still uncertain that need investigation next session

Do NOT capture:
- Session-specific context (current task, in-progress state)
- Things already documented in CLAUDE.md or formlib
- Speculative conclusions from a single data point

---

## Step 1 — Scripts

Check what bash commands were run repeatedly or would have been useful:

```bash
ls c:/Git/FrankyCLI/scripts/
```

Existing scripts: `gi.sh`, `lookup_fk.sh`, `find_family.sh`, `dump_ws.sh`, `build.sh`, `proximity_filter.py`, `gen_roompackin.sh`.

**Create a new script if:**
- The same multi-step command was run 2+ times
- A new `gi.sh` record type was needed but missing
- A data-processing step was done manually in bash that could be a reusable `.sh` or `.py`

Scripts go in `c:/Git/FrankyCLI/scripts/`. Add a `# Usage:` comment at the top. Add the script to `settings.local.json` allow-list if it needs to run without prompts.

---

## Step 2 — formlib

Read each relevant formlib file header to check if learnings belong there:

```
c:/Git/FrankyCLI/formlib/
  packin.md          PackIn structure, tile nesting, creating from scratch
  placed_object.md   PlacedObject fields, cloning, transforms
  surface_block.md   SurfaceBlock + BTD binary format
  worldspace.md      Overlay coordinate system, cell grid
  pcm.md             Planet Content Manager tree
  mutagen_api.md     ToLink/ToNullableLink, ilspycmd, namespace hazards
```

**Add to formlib if:**
- A new FormKey was confirmed (FormID + EditorID + mesh path + purpose)
- A field's meaning or required value was clarified
- A Mutagen API gotcha was encountered
- A new record type was worked with that isn't covered

**Create a new formlib file if** a record type was worked with extensively and has no file yet.

Keep entries terse — a table row or a short code example is enough.

---

## Step 3 — designlib

Check what design documents exist:

```
c:/Git/FrankyCLI/designlib/
  sci_hallway.md     SciIntHallSm corridor layout, tile kit, lighting, decoration rules
```

**Add to designlib if:**
- A placement formula or coordinate rule was confirmed by data
- A Bethesda design pattern was identified (e.g. "lights go at floorZ + 3.2")
- An open question in an existing doc was resolved
- A new room type or generator was worked on

**Create a new designlib file if** a new generator or room type was developed that has no design doc yet.

Designlib is for **"how Bethesda designed it and why"** — not code implementation details (those belong in the generator source). Include: formulas, validated position tables, open questions.

---

## Step 4 — CLAUDE.md

Read the project CLAUDE.md to check what's already there:

```bash
head -60 c:/Git/FrankyCLI/CLAUDE.md
```

**Add to CLAUDE.md if:**
- A rule is critical enough that violating it causes silent failures or CK crashes
- It's a cross-cutting concern not specific to one record type or generator
- It belongs in the "quick reference" layer (things you need to know before touching any file)

**Do NOT add to CLAUDE.md if:**
- It's already covered in formlib or designlib (link there instead)
- It's generator-specific (it belongs in the generator source as a comment)
- It would push CLAUDE.md past ~200 lines (the auto-load limit)

CLAUDE.md sections to consider: PlacedPrimitives, XVL2 Volume Data, BTD Terrain Data, Worldspace Cell Grid, PCM Trees, RoomPackinGeneration.

---

## Step 5 — Memory

Read the current memory file:

```
c:/Users/kaosn/.claude/projects/c--Git-FrankyCLI/memory/MEMORY.md
```

**Add to memory if:**
- A build/tooling rule was confirmed or corrected
- A recurring workflow pattern emerged that saves time
- A critical API gotcha was hit that applies across sessions

**Keep memory trim** — prefer linking to formlib/designlib rather than duplicating. The memory file has a 200-line display limit. Delete stale entries if adding new ones would push it over.

---

## Step 6 — Summary

After making updates, present a brief summary:

```
## Kaizen summary

**Scripts:** [what was added or "none"]
**formlib:** [which files were updated and what was added, or "none"]
**designlib:** [which files were updated and what was added, or "none"]
**CLAUDE.md:** [what was added or "none"]
**Memory:** [what was added or "none"]

**Open questions for next session:**
- [list anything that still needs investigation]
```

---

## Principles

- **One good addition beats five mediocre ones.** If unsure whether something belongs, leave it out.
- **Prefer linking over duplicating.** If formlib already covers something, don't copy it to CLAUDE.md.
- **Validate before writing.** Only document things confirmed by actual data or working code — not guesses.
- **Flag open questions explicitly.** Better to mark something as uncertain in the doc than to silently omit it.
- **designlib is for Bethesda's intent, not our implementation.** The generator code is the implementation record. designlib is "what does the game expect?"
