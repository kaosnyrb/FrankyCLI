---
name: commit
description: Stages all current changes and commits them to git with a useful message derived from the diff. Run with /commit.
user_invocable: true
---

# Commit — Stage and Commit All Changes

Commits everything that is currently modified or untracked. Derives a meaningful commit message from the actual diff rather than asking the user.

---

## Step 1 — Inspect the working tree

Run these in parallel:

```bash
git -C c:/Git/FrankyCLI status --short
git -C c:/Git/FrankyCLI diff HEAD
git -C c:/Git/FrankyCLI log --oneline -5
```

Read the output carefully:
- What files changed?
- What was added, removed, or modified in the diff?
- What style do recent commit messages use (imperative, lowercase, no period)?

---

## Step 2 — Identify any files to exclude

Do NOT stage:
- `.env` files or anything containing secrets
- Build outputs (`bin/`, `obj/`) — these should already be gitignored
- Lock files that shouldn't change (warn the user if present)

If any suspicious files appear in the status output, flag them and ask the user before proceeding.

---

## Step 3 — Draft the commit message

Write a **single-line subject** (≤72 chars) that:
- Uses **imperative present tense** ("Add", "Fix", "Refactor", "Move", not "Added"/"Adds")
- Names the **what** and, where non-obvious, the **why**
- Mirrors the recent commit style in this repo (see `git log` output)

Examples of good messages for this project:
- `Add IndustryGroundFlattenPass topology pass`
- `Fix nullable FormLink guard in SpaceShipNoun clone`
- `Refactor AITools to separate stateless and history-writing calls`
- `Move MathUtil helpers to shared Retrograde.Library`

If the changes span multiple unrelated concerns, group them into a short multi-line message with a blank line separating the subject from a bulleted body — but prefer a clean single line when possible.

---

## Step 4 — Stage and commit

```bash
git -C c:/Git/FrankyCLI add -A
git -C c:/Git/FrankyCLI commit -m "<subject line>"
```

If a pre-commit hook fails:
- Read the hook output carefully
- Fix the underlying issue (do NOT use `--no-verify`)
- Re-stage and create a **new** commit (never amend after a failed commit)

---

## Step 5 — Confirm

After a successful commit, show the user:
- The commit hash and message (`git log -1 --oneline`)
- A one-line summary of what was staged

Do **not** push unless the user explicitly asks.
