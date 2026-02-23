# Designlib Principles

How to read and write entries in this folder.

---

## Bethesda's rooms are built with intent

Every element in a source room — light position, rotation, which Static variant, which side of the wall — was a deliberate choice. The goal of a designlib entry is to extract that intent, not just transcribe measurements.

**Ask why, not just where.**

Examples of intent already extracted in `sci_hallway.md`:
- Lights alternate sides at the stair transition — because the stair is a visual gear-change and the side-switch marks it
- Flat tiles alternate v1/v2 — because the seam-break gives the corridor visual rhythm
- Stair transition boundaries are skipped for lights — because those zones are geometrically busy (step-up geometry)
- Pipe clusters go near the south entry — entry points are where Bethesda drops high-density dressing to reward first impressions
- Ducts run the upper flat section only — they follow the "arrived at destination" zone, not the transition zone

If you can't explain why something is placed where it is, note it as an open question rather than transcribing the number blindly.

---

## Variation within the grammar, not outside it

We want different room shapes, lengths, and arrangements — but they should feel like they belong to the same kit. The grammar is the constraint; variation happens inside it.

What can vary:
- Corridor length (flatTilesStart, stairCount, flatTilesEnd)
- Room orientation and connections (straight, L-shaped, T-junction)
- Decoration density (more or fewer wall panels)
- Which light panel variant is used

What should stay consistent:
- Light placement rule (one per tile boundary, skip stair transitions, side-switch at north section)
- Pipe cluster near south entry
- Wall panels at inner face X = ±1.5
- Ceiling ducts in the upper flat section

When adding a new room shape (e.g. L-bend), derive the lighting/decoration rules from the same principles rather than copying coordinates wholesale from a different source room.

---

## Template mod provenance doesn't matter at design time

Designlib entries reference PackIn and Static FormIDs from Starfield.esm and from template mods (e.g. `du_outlaws_template.esm`). During actual generation these are resolved — either the original form is used directly or the record is imported into the target mod.

**Don't avoid documenting a form just because it comes from a template mod.** Note where it came from, and let the generation pipeline handle resolution. The designlib is a design reference, not a build manifest.

---

## What a good designlib entry contains

1. **Source rooms** — which EditorIDs were reverse-engineered. Ties the entry to verifiable data.
2. **Coordinate system** — local origin, axis convention, key landmark positions (wall faces, ceiling height).
3. **Layout grammar** — the rule that generates any valid variant, not just the measured positions of one room.
4. **Design intent notes** — why elements are placed where they are (see above).
5. **Validated positions** — a table of known-good coordinates confirmed against the source data, as a sanity check for the generator.
6. **Open questions** — things observed but not yet explained. Better to flag uncertainty than to guess and encode it as fact.
