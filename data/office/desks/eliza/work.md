# Eliza — work memory (FrankyCLI)

*My engine-local desk for FrankyCLI: current threads, decisions, open spikes. Kept sharp —
dense reference knowledge lives in [`reference/`](reference/), not here.*

## Standing

- I own **code health** on FrankyCLI — module boundaries, refactors, schema/doc discipline,
  spike cleanup, and keeping this repo's docs **true**. I reach FrankyCLI through the
  home-office workspace junction (`workspaces/frankycli`).
- **Don't cross the streams (the boundary):** engine/corpus facts — Mutagen gotchas, record
  formats, the Creation-Engine knowledge — belong in **this repo's committed docs** (`docs/formlib/`,
  `docs/designlib/`, `CLAUDE.md`) and, until graduated, in [`reference/`](reference/). My **portable
  self** (who I am, what I've earned about the owner, my engineering craft) lives in the home-office
  room (`office/staff/eliza/`), never here.
- **`gen_inspect` invocation (correct as of 2026-07-07):** `cd /c/Git/FrankyCLI && dotnet run --
  gen_inspect <recordtype> <search>` — search is EditorID-contains or `0x…` FormID. `list` enumerates
  all groups. (The old `dummy gen_inspect dummy …` form is dead — `Program.cs` dispatches on `args[0]`.)
- **The tool is fan-made over a 20-year engine — some record shapes read flaky.** A `gen_inspect` dump
  is an *instrument reading*, not ground truth; corroborate a load-bearing claim against a second oracle
  (xEdit / raw subrecord bytes / the authoring source) before trusting it.

## Open

- **2026-07-07 — Sherpa ship-part investigation (OPEN, needs xEdit).** First live inspection of
  `avontechstardust` (a Taiyo-style cockpit module — cassette-futurism, confirmed working in-game).
  Reachable chain: MoveableStatic `atsd_ms_sherpa` (**`000828` since 2026-07-21 — was `00088A`; he
  repaired a mangled header in xEdit, which reassigns ids from a base and re-sorts, so every FormID in
  this plugin moved. Any id written down here is a snapshot, not an identity — the EditorID is the
  identity**) + two PackIns (`atsd_pk_sherpa_ext`
  `00080A` → Cell `00085A`; `atsd_pk_sherpa_int` `00080B` → Cell `00080C`) + the two storage Cells they
  pack (ext: 8 objects; int: 61 — a dressed cockpit interior). **Two unresolved questions, both the
  flaky-provenance class:** (1) every Sherpa record dumps under *both* `.esm` and `.esp` masters, and the
  same local FormID `000822`/`00080C` appears under both — either legit override-layering or a **FormID
  master-index misparse**; matters to the owner's ESM-only rule if real content hides in the `.esp`.
  (2) the two material swaps (`000813`/`000822`) on the mesh — the owner flagged the dump "might not be
  what it looks like." **Resolve both in xEdit** (owner's tool, pinned to taskbar): does `00080C` really
  live in the `.esp`, and is that field literally a material swap.
- **2026-07-07 — `gen_inspect` GAP: no case for GBFM / COBJ / SnapTemplate.** The ship-module chain
  (`docs/formlib/ship_module.md`) needs GenericBaseForm (the placeable part), ConstructibleObject (the
  builder recipe), and SnapTemplate (attach nodes) — `gen_inspect` reaches none of them, so the layer
  that makes a part snappable/buildable is invisible to the tool. **Graduation-by-doing:** add those
  cases *after* xEdit shows the true record shape — don't author from a guess at the format.

## Done

- **2026-07-07 — Fixed `gen_inspect`'s stale invocation docs (commit `9d82d0a`, branch `DeAi`).** The
  XML-summary usage and the `/investigate` skill both documented the dead `dummy gen_inspect dummy …`
  form; corrected to `gen_inspect <recordtype> <search>`. **Latent follow-up (flagged, not touched):**
  because `Program.cs` always passes exactly 5 args, `gen_inspect.cs`'s own `args.Length < 5` usage block
  is now **unreachable** — a dead-doc branch to prune (rule 4) on the owner's say-so.
- **2026-07-07 — Established this desk + rescued the stranded harness memory.** The old (memoryless)
  Claude's per-user auto-memory (`~/.claude/projects/c--Git-FrankyCLI/memory/`) was machine-local and
  travelled nowhere. Triaged and moved the keepers into [`reference/`](reference/); redundant/stale
  entries dropped (documented there). **Graduation candidates** (corpus facts that ideally fold into
  `docs/formlib/` / `CLAUDE.md`) are flagged in `reference/engine-and-tooling.md` — a future doc-health
  pass, on the owner's yes.
