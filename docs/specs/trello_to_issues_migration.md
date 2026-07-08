# Spike: Migrate the Starfield backlog (Trello → GitHub Issues)

> **Status:** PLANNED (2026-07-08). Not started — this is the executable plan for later.
> **Exit / graduation:** the live backlog is GitHub Issues, a Projects board gives the kanban view,
> the sediment is a committed markdown ledger, and **at least one ticket has gone idea → PR** (the
> loop proven). Then delete this spike or leave a graduation note. Until then it's an open loan.

## The problem

The backlog lives in **Trello** — a room only the owner can walk into. An AI session can read a
static **export** (a frozen snapshot) but can't see the live board or write back. We need a work
queue **both the owner and the office can read *and* write**, with a done-state that can't be faked.

## The decision (settled — reuses the office's own answer)

**GitHub Issues on the owned repo (`kaosnyrb/FrankyCLI`) + a GitHub Projects board for the kanban view.**

This is not a new design — it's the home-office **control-plane doctrine**
(`office/spikes/2026-06-05-github-issue-control-plane.md`, home-office repo) pointed at the Starfield
backlog. Why it's the "both can use it" surface:

- **Zero new infra** — the repo + Issues already exist; the office already has GitHub MCP / `gh`.
- **Symmetric access** — owner keeps a Trello-shaped **Projects board** (columns, drag-drop); a
  session gets live `gh issue` read/write. The frozen-snapshot asymmetry disappears.
- **The PR is the un-fakeable cursor** — a ticket is done when the PR/commit exists, not when someone
  says so. Trello and a markdown checklist can't give that.
- **The convention is already written** — the buildable-cold ticket contract, route labels,
  open/close-with-reasoning lifecycle, and the `worklog.md`+`status.md` birds-eye all live in that
  control-plane spike. Adopt, don't reinvent.

## Source of truth for the migration

The Trello export JSON (owner-provided; e.g. `HFL4nklN - starfield-ideas.json`).
**Snapshot as of 2026-07-08:** 6 lists, **834 open cards** — Done 369 · Review 352 · Blocked
(CS/Mutagen) 51 · Archive 35 · Todo 25 · Doing 2. Labels barely used (≈15 across 834).
**Re-export fresh at migration time** — this count will have drifted.

## Scope discipline — DO NOT migrate all 834 (the load-bearing rule)

369 Done + 35 Archive + a 352-card "Review" graveyard is **sediment**. Importing it wholesale just
rebuilds the graveyard in a nicer house, spams notifications, and buries the ~27 live cards. Reject
legacy at the boundary:

- **Live → open issues.** Todo (25) + Doing (2) + the *genuinely active* Review cards (heuristic:
  `dateLastActivity` within ~6 months; owner eyeballs the borderline).
- **Sediment → a flat markdown ledger, not issues.** Done + Archive + dead Review → a committed
  `docs/backlog-archive.md` (grep-able history). No open issues for finished/abandoned work.
- **CS/Mutagen Blocked (51) → issues, re-triaged against *today's* tooling** (the board predates the
  Creation Kit release; some blockers have expired). Tag each by blocker-type (see labels).

## Proposed label taxonomy (finalize WITH the owner — this is the fun design bit)

Status lives in **Projects columns** (Todo / Doing / Review / Done), *not* labels. Labels carry the
cross-cutting facets:

- **`project:`** — `outlaws` · `avontech` · `retrograde` · `blacksite` · `tooling` (FrankyCLI itself)
- **`type:`** — `quest` · `poi` · `shippart` · `tool` · `content` · `bug`
- **blocker (only on blocked cards):**
  - `blocked:policy` — Bethesda master-type / engine rule; **no tool release unblocks it** (e.g. the
    small/medium weapon-mod limit). A different problem than the rest of the column.
  - `blocked:mutagen-gap` — Mutagen can't build the record type from scratch. **Escape hatch:** if a
    base-game **donor** exists, it's `needs-donor` (clone-and-patch), *not* truly blocked.
  - `needs-donor` — doable via the codebase's standard clone-and-patch pattern
    (`docs/formlib/*` cloning-from-getters), just not tried yet.
- **`maybe`** — captured idea, not greenlit to build (mirrors the office lifecycle's `maybe` state).

## Ticket bodies — don't force half-baked ideas into full contracts

Most cards are **seeds**, not buildable-cold specs. So:
- A **seed** issue = title + the card's original text + labels + `maybe`. Light. Just captured.
- Promote to the **buildable-cold contract** (`## Context → ## Proposal → ## Acceptance` + route
  label, per the control-plane spike) only when the owner greenlights *building* that one. Don't
  inflate 27 rough ideas into full specs up front.

## The migration mechanism (a script — the road is mostly mechanical)

A one-shot script (`scripts/migrate_trello.py` or similar) that:
1. Loads the fresh export JSON.
2. Partitions cards → live / sediment / blocked (by list + `dateLastActivity`).
3. **Dry-run by default** — prints what it *would* create; `--apply` to actually write.
4. Creates live + blocked issues via `gh`/GitHub API, applying `project:`/`type:`/blocker labels
   derived from list + card text.
5. Emits `docs/backlog-archive.md` from the sediment.
6. **Idempotent** — stamps each issue body with the Trello card `id` (or shortLink) and skips a
   card whose id is already an issue, so a re-run doesn't duplicate.

## Open decisions to resolve at execution (owner's call)

1. **One repo or per-mod?** Start: everything on **FrankyCLI Issues** with `project:` labels; split a
   mod to its own repo only when it earns one. (Control-plane spike's ruling: "issues live with the
   work, both repos" — so promotion later is legitimate.)
2. **Final label taxonomy** (the list above is a proposal).
3. **The "live Review" cutoff** — activity window, or a hand-pass on the 352.
4. **Projects board** — reuse the Trello columns as-is (Todo/Doing/Review/Done)?

## Not doing here

- **Not** migrating the 834 wholesale (the whole point).
- **Not** building the unattended Phase-3 floor (label-triggered cloud builds) — that's the
  control-plane spike's own open tail, gated on the PR-cursor honesty; out of scope for the migration.
- **Not** wiring Discord / any control-room front door. This spike only relocates the *queue*.
