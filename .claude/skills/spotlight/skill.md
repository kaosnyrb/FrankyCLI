---
name: spotlight
description: Picks a random file from Retrograde.Library and runs a focused elegance review against it and its related files, proposing exactly 5 targeted improvements. Run with /spotlight for a quick quality check on undervisited code.
user-invocable: true
---

# Spotlight — Random File Elegance Review

Picks one random `.cs` file from `Retrograde.Library`, pulls in related files for context, and proposes **exactly 5 targeted improvements** across six quality axes:

- **Elegance** — unnecessary complexity, verbose patterns, missed language features
- **Coherence** — logic that is scattered, duplicated, or hard to follow
- **Style alignment** — inconsistencies with the conventions already used in this codebase
- **Dead code** — unused methods, fields, parameters, unreachable branches, commented-out code left in permanently
- **Bad comments** — comments that restate the code, are stale/wrong, are obsolete TODOs, or document things that should be self-evident from naming
- **Bad practices** — C# anti-patterns, Mutagen-specific gotchas (from CLAUDE.md), improper null handling, magic numbers, or other common pitfalls

The goal is not a full refactor. Each suggestion must be **concrete, small, and immediately actionable**.

---

## Step 1 — Pick a random file

Run the project's random-picker script:

```bash
bash c:/Git/FrankyCLI/scripts/random_cs.sh c:/Git/FrankyCLI/Retrograde.Library
```

It excludes `obj/`, `bin/`, and auto-generated files, then returns one path chosen via `$RANDOM`. Announce the chosen file at the top of your output so the user knows what was selected.

---

## Step 2 — Read the target file

Read the chosen file in full. While reading, note:
- What is its responsibility? (Capture a one-sentence role description for the output header.)
- What patterns or abstractions does it use?
- What namespace/folder does it live in? (Capture the folder name as the Layer.)
- Any unused methods, fields, or parameters
- Any commented-out blocks that were never cleaned up
- Any comments that merely restate the code, are incorrect, or are stale TODOs
- Any C# bad practices (magic numbers, improper null handling, Mutagen FormLink gotchas from CLAUDE.md)

---

## Step 3 — Identify and read related files

Pull in up to 4 related files for context. "Related" means:

1. **Base class or interface** — if the file implements or extends something, read that type
2. **Same folder siblings** — 1–2 files from the same folder that do similar things
3. **Callers or collaborators** — if the file has a well-known public API, read one file that calls it (use `Grep` to find callers)

Read these files. Use them to detect inconsistencies, duplication, and missed reuse opportunities.

---

## Step 4 — Identify the 5 best improvements

Think carefully and select exactly **5 suggestions**. Prioritise suggestions that:

1. Are quick to apply (minutes, not hours)
2. Have a clear before/after
3. Improve something visible in the actual code — not hypothetical future problems
4. Are consistent with how the rest of the codebase is written
5. Span different concerns — avoid 5 suggestions about the same issue
6. **No axis may appear more than twice.** If you find 3+ issues in one axis, pick the 2 most impactful and leave the rest.

For each suggestion, classify it under one of the six axes:

| Axis | What it means |
|------|--------------|
| **Elegance** | Code can be simpler, shorter, or more expressive with no loss of clarity |
| **Coherence** | Logic is fragmented, duplicated, or would be clearer if reorganised |
| **Style alignment** | This file does something a different way than the rest of the codebase does |
| **Dead code** | Unused method, field, parameter, unreachable branch, or permanently abandoned commented-out block |
| **Bad comments** | Comment restates the code, is stale/wrong, is an indefinite TODO, or obscures rather than clarifies |
| **Bad practices** | C# anti-pattern, Mutagen gotcha (CLAUDE.md), improper null handling, magic number, or similar pitfall |

---

## Step 5 — Output the 5 suggestions

Present them in this exact format:

```
## Spotlight review — [FileName.cs]
**File:** [FileName.cs] — [one sentence role description]. **Layer:** [folder name].

### 1. [Axis] — [File:line or method name]
**What:** One sentence describing the current code.
**Why:** One sentence explaining the problem.
**How:** Concrete change — ideally a short before/after snippet.
**Confidence:** High/Medium/Low — [one clause explaining the confidence level].

### 2. ...
### 3. ...
### 4. ...
### 5. ...

---
*Apply by hand. Ask before making changes.*
```

---

## Principles

- **Five, not more.** If you find ten problems, pick the five that matter most. A short focused list gets acted on; a long list gets ignored.
- **Show the fix, not just the problem.** Every suggestion must include a concrete "how" — even one line of example code is better than a vague note.
- **Work with the grain of the codebase.** If the rest of the codebase uses a certain pattern, suggestions should align with that pattern, not replace it with something foreign.
- **No hypotheticals.** Only suggest improvements visible in the actual code. Do not preemptively fix things that are not yet a problem.
- **Ask before applying.** This skill is advisory only. Do not make any edits unless the user explicitly asks.
- **Complement with /elegance.** Spotlight reviews a random undervisited file; `/elegance` reviews recent git changes. Use both for broad coverage.
- **Confidence levels:** High = issue is definitely present and fix is clearly correct. Medium = issue is real but fix may need adjustment. Low = speculative; depends on context not visible in the reviewed files. Prefer dropping Low-confidence suggestions in favour of High-confidence ones from a different axis.
