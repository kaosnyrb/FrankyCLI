---
name: elegance
description: Reviews recent git work across 3–7 days and proposes exactly 5 targeted improvements to code elegance, coherence, and style alignment. Run with /elegance at the start or end of a session to keep the codebase healthy.
user-invocable: true
---

# Elegance — Code Quality Review Skill

Reviews what has changed in the last few days of git history and proposes **exactly 5 targeted improvements** across six quality axes:

- **Elegance** — unnecessary complexity, verbose patterns, missed language features
- **Coherence** — logic that is scattered, duplicated, or hard to follow
- **Style alignment** — inconsistencies with the conventions already used in this codebase
- **Dead code** — unused methods, fields, parameters, unreachable branches, commented-out code left in permanently
- **Bad comments** — comments that restate the code, are stale/wrong, are obsolete TODOs, or document things that should be self-evident from naming
- **Bad practices** — C# anti-patterns, Mutagen-specific gotchas (from CLAUDE.md), improper null handling, magic numbers, or other common pitfalls

The goal is not a full refactor. Each suggestion must be **concrete, small, and immediately actionable**.

---

## Step 1 — Gather recent git history

Identify what changed in the last 7 days (or last 10 commits if less):

```bash
git -C c:/Git/FrankyCLI log --oneline --since="7 days ago" --name-only
```

If the output is very sparse (fewer than 5 changed files), widen to 14 days:

```bash
git -C c:/Git/FrankyCLI log --oneline --since="14 days ago" --name-only
```

Focus on `.cs` files only. Ignore generated files, migration scripts, and test fixtures.

---

## Step 2 — Identify the touched files

Before reading any code, read `c:/Git/FrankyCLI/CLAUDE.md` to load the project's documented conventions (nullable FormLink rules, required usings, namespace hazards, etc.). Use these as the baseline for style-alignment suggestions.

Parse the git log output and build a deduplicated list of `.cs` files that were modified. Read each file. Look across all of them as a group — not just each in isolation.

When reading, note:
- Patterns used in one file that are done differently in another
- Methods or logic repeated across files
- Places where C# language features could simplify the code
- Places where naming, structure, or documentation is inconsistent with the surrounding codebase
- Unused methods, fields, parameters, or unreachable branches introduced in the recent changes
- Commented-out code left in without explanation or a removal plan
- Comments that restate the code, are factually stale, or are indefinite TODOs
- C# bad practices: magic numbers, improper null handling, Mutagen FormLink gotchas (CLAUDE.md), and similar pitfalls

---

## Step 3 — Identify the 5 best improvements

Think carefully and select exactly **5 suggestions**. Prioritise suggestions that:

1. Are quick to apply (minutes, not hours)
2. Have a clear before/after
3. Improve something you can see in the changed code — not hypothetical future problems
4. Are consistent with how the rest of the codebase is written
5. Each targets a different file or concern (avoid 5 suggestions about the same file)

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

## Step 4 — Output the 5 suggestions

Present them in this exact format:

```
## Elegance review — [date range]

### 1. [Axis] — [File:line or method name]
**What:** One sentence describing the current code.
**Why:** One sentence explaining the problem.
**How:** Concrete change — ideally a short before/after snippet.

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
- **No hypotheticals.** Only suggest improvements visible in the actual changed code. Do not preemptively fix things that are not yet a problem.
- **Ask before applying.** This skill is advisory only. Do not make any edits unless the user explicitly asks.
