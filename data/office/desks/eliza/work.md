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

- **2026-07-22 — ⚠ WING REOPENED — `atsd_wing01` does NOT render. Bug, not done.** Visible in the ship-builder
  BUY MENU (GBFM/COBJ/name/category resolve → the record layer is fine) but no MODEL in the 3D editor → a
  **NIF/mesh/deploy** failure, not a records one. He's in the CK looking now. **The "start→in-game" in Done
  below was never verified ON THE GLASS for the render** — deploy + records-load got read as "in-game"; the
  model drawing was assumed (verify-from-the-consumer's-position). Suspects, likeliest first, all in my lane
  (the NIF is mine): (1) **deploy drift** — `.nif`/`.mesh` authored but not in Steam `Data\` (the ESM ships →
  menu works; the model can't load → no draw); `check_starfield_drift.py` settles it from a keyboard. (2)
  **BSGeometry not in the root NiNode's children list** — I flagged this exact render-risk on the Sherpa NIF;
  `nif_from_template.py` keeps one geo and repoints it, and unwired-from-root = no draw (readable in NifSkope).
  (3) **MaterialID not recomputed** — the one field the template tool can't derive; needs NifSkope's clean/
  update pass. (4) MSTT `Model` path. Ask-then-verify: nothing asserted about the cause till the CK reports.
  - **2026-07-22 (CK narrowing #1, his finding):** the textured wing **DRAWS in the PackIn view** → NIF / mesh /
    material / **deploy all CLEARED** (the asset half is healthy); suspects 1–3 above are out. **LEADING
    CANDIDATE:** the MoveableStatic's **MaterialSwaps were set to source materials the MODEL DOESN'T HAVE** —
    and the **CK UI cannot author that** (it only offers materials the mesh uses), so it came from the
    **generator**. His hypothesis it's the render cause; he's testing a hand-fix. **MY read (to confirm, not
    asserted):** a `gen_shipstruct --swaps` seam — a MaterialSwap remaps by matching the model's actual material
    as its SOURCE key, and `nif_from_template.py` sets the wing's real `.mat`; if the generator wrote swaps whose
    source is a *template* part's material (Sherpa/placeholder), the swap points at nothing on the wing, and the
    builder's material-application can fail while the raw PackIn view (base materials) still renders. **IF his
    fix confirms → the ROOT fix is in `gen_shipstruct`, NOT this one part** — hand-fixing here leaves
    `bottompanel01` + every future part to inherit it. Offered to `gen_inspect` the swaps to pin the exact
    source-vs-actual mismatch + prep the generator fix; waiting on his go / test result. Symptom earned; cause
    under test.
  - **2026-07-22 CONFIRMED (ESM-grounded) + his call:** cause = `gen_shipstruct`'s default = 3 VANILLA Starfield
    swaps (`099196`/`0B6B1F`/`2AF78A`) source-mismatched to the wing's custom `wing01.mat` → render blocked (no
    swaps → black). Shipyards' `ats_corewing_01` uses the same three and works (vanilla materials) = the default
    is a Shipyards-ism. Sherpa (working) references its own `atsd_matswap_sherpa_P`(`000813`)/`_S`(`000822`),
    base→tinted. **LayeredMaterialSwap is REFL-opaque → CK-authored, not generator-authorable** — his standing
    rule now: deep-copy + wire + flag when I hit REFL-opaque (→ bethesda.md standing rulings). **HE'S REBUILDING
    THE WING PROPER (P/S/T recolour), hand-authoring the swaps in the CK** — wing is his hands; I stood down on
    scaffolding a swap for a part he's replacing. **MY DURABLE TODO (proposed, his go): kill `gen_shipstruct`'s
    silent vanilla-swap default — require `--swaps` for a custom-material part so `bottompanel01` doesn't inherit
    the same black/invisible trap.**
  - **2026-07-22 WING REBUILT (3-part P/S/T) — NIF + 6 mats DONE (mine):** he re-exported the port wing as
    `_pri`/`_sec`/`_tri` (rotation fixed in Blender — a mesh bake, the NIF was always identity; I dumped it).
    Extended `nif_from_template.py` → N geometries (4 + 3×N blocks, root n-children, transforms forced identity);
    built `atsd_wing01_port.nif` (13 blocks, round-trip + transform-dump verified). Authored 6 mats
    (`wing01_pri/sec/tri` + `_P/_S/_T` variants) as verified transforms of `wing01.mat`/`_P` — shared `wing01_*`
    textures, res: E74/E75/E76 blocks, Primary/Secondary/Tertiary channels. **Modding project NOT committed by
    me** (his live workspace — Blender/meshes/collision mid-flight; his to commit). **REMAINING (his/gated):
    NifSkope clean pass (MaterialID), 3 REFL-opaque swaps (his CK or my deep-copy+wire+flag, ESM must be free),
    deploy+archlist, stb wing (needs his stb part meshes), in-game test.**
  - **2026-07-22 WIN — the 3-part wing LOADS + ATTACHES in the editor** (his screenshot). N-geometry NIF pipeline
    proven end to end. **CTD lesson: NifSkope clean pass is LOAD-CRITICAL (raw script NIF crashes on load till
    sanitized), not just MaterialID — he tested pre-clean → CTD, cleaned → attaches; tool warning strengthened.**
    Deployed all 9 files + NIF (robocopy, stb preserved). REMAINING: 3 swaps (recolour), pull NifSkope'd NIF back
    to source (deployed now newer), stb wing, archlist. Modding project still his to commit (live workspace).
  - **2026-07-22 SWAPS wired — built `gen_copyswap` (FrankyCLI, the REFL-opaque deep-copy+wire tool his rule
    wanted).** Cloned sherpa_P/_S → `atsd_matswap_wing01_P/_S/_T` (875/876/877), wired onto `atsd_ms_wing01_port`;
    verified in the ESM. `copyswap <mod> <mstt> <new>=<src>…` — `DuplicateInAsNewRecord` + set `Model.MaterialSwaps`,
    env-close-before-write; compiled + ran clean. **Owed by HIM: repoint the 3 REFL mappings in the CK (sherpa→wing
    materials) + re-bridge .esm→.esp, then test.** ESM written by me (his workspace, his to commit).

- **2026-07-22 — Dark Universe: Jaeger — mission-board legendary-creature hunts. DESIGN LOCKED, gen-1 mapped,
  build is the next session.** *(Engine-domain FACTS — QUST anatomy, mission board = SMQN pool, alias fills,
  vanilla bases, the Mutagen from-event→create-obj conversion — are in home-office `bethesda.md`. This is the
  FrankyCLI-corpus + project half.)*
  - **THE CREATURE HALF IS ALREADY BUILT** — `Retrograde.Library/Nouns/Hunt/PredatorHuntTarget.cs`.
    `GetHuntTarget(planet)` → picks the planet-correct vanilla **`PCM_<system>_<planet>_Predator<N>`** NPC (native
    is encoded in the vanilla EditorID — no lookup), clones it, retargets its template chain to a renderable
    `_Enc*_Template`, boss-treats it (level from `SystemLevels` table via `GetSystemForPlanet` = planet→
    `GalaxyData.StarId`→`Star`→level; `VeryAggressive`/`Foolhardy`; boss OMOD `0x32047B`; blank-CCT-name keyword
    `0x182D74`), names it (`The <Prefix>-<Suffix>`), wraps in a FormList. Filters out aquatic (Skin-OMOD tokens).
    `gen_hunttest` writes one per probe planet to `hunttest.esm` (overwritable) — proves the creatures; stops
    before any quest.
  - **THE QUEST HALF IS THE GAP.** The proven pattern is `RetrogradeBountyQuest` (clone base bounty QUST via
    `QuestNoun` → override target → register into an `SMQN` node via `FindOrCreateNode` which clones the node
    preserving `MaxConcurrentQuests`/`HoursUntilReset`/`Parent`). `QuestNoun`(clone + setters) is the primitive;
    `NPCDialogueNoun` already authors a `Quest` from scratch (the "author from nothing" precedent he wants
    long-term).
  - **gen-1 RECIPE (write `gen_jaeger`, model on `RetrogradeBountyQuest` + `gen_hunttest`'s env setup):**
    (1) `GetHuntTarget(planet)` → creature+FormList. (2) `QuestNoun(FormKeyLookup.GetFormKey("MB_Bounty01Far"))`
    — vanilla ground remote bounty, **no deps** (QuestNoun copies template records IN, so the output is
    standalone). (3) **Override `PrimaryRef`**: null `FindMatchingRefFromEvent`, set `CreateReferenceToObject
    {Object=FormList, AliasID=<create-at loc>, Create, Level}` — the vanilla base is from-event, so
    `SetQuestReferenceCreateAlias` alone NREs; needs the full reconstruction. (4) pin `TargetPlanetLocation`/
    `System` to the planet. (5) `MissionBoardDescription` + name. (6) new `SMQN` "Hunt" node, `Parent =
    Starfield.esm:0x015302`, add the quest. (7) write `hunttest.esm`.
  - **FOUR VALUES SETTLE ONLY AGAINST A BUILD + xEDIT/IN-GAME LOOP (his eye, not bytes):** the create-at
    `AliasID`, the `CreateEnum` value, the planet-Location pin, the board-node parent. So gen-1 is a real build
    session, not a five-minute wire — stopped clean here with the map complete rather than cram a rushed push.
  - **DESIGN DECISIONS (his):** promoted vanilla fauna (not new creatures) · procedural/tiered roster ·
    **~300 pre-pinned missions** (one planet + one create each; native baked at gen-time because the board picks
    the planet at random and a runtime biome condition would lose the roll) · vanilla dungeon locations first,
    custom lairs later · clone vanilla, no deps · gen 1 first then scale.
  - **CROSS-MOD MULTIPLIER — RESOLVED to the LOCATION axis (his, 2026-07-22).** The open-PCM multiplier can ride
    the creature axis OR the location axis; he put it on **location**. **CREATURE stays VANILLA** —
    `PredatorHuntTarget` as-is (Starfield.esm native predators, boss-treated); the load-order-wide creature scan
    is DROPPED as uncertain (how a modded creature slots into the boss/template chain is unknown, don't chase).
    **LOCATION = nature + cave POI types from the PCM tree** — the hunt sends the player to any nature/cave POI,
    including other mods', so Jaeger still inherits the ecosystem multiplier, on the ground axis. Thematically
    right (hunt a beast in the wild / its den, not a factory) AND the free multiplier, one filter. **WIRE-TIME
    GROUNDING (don't guess — the ask/verify lesson): confirm the exact nature/cave POI-type keyword/category the
    location alias reads against the PCM tree when building the location alias.** Set the Hunt `SMQN` node to
    RANDOM mode (his: each playthrough different order). Engine facts → [[bethesda.md]] § *The QUEST +
    mission-board system*.
  - **LONG-TERM (his aspiration, banked):** author quests from *nothing* rather than clone — his origination
    dial pointed at quests, "quests basically don't have a complexity ceiling." Clone-vanilla-now is the
    stepping stone: every subrecord we inherit is one we study in place, then graduate to authoring. Precedent
    exists (`NPCDialogueNoun`).

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

- **2026-07-21 — Authored `atsd_wing01` (both sides) end-to-end + built the tools that were missing.** First
  Stardust part taken start→in-game — **⚠ REOPENED 2026-07-22: does NOT render in the 3D editor (see Open, top).
  The TOOLS below are done and real; the wing is not.** Commits: FrankyCLI `fa1b902` (`gen_shipstruct` snap/swaps/bounds flags +
  `FixNextFormId` unit fix), `3958157` (FixNextFormId → **derive the counter, never read the header**; surveyed all
  54 Data plugins — 40 store nextObjectID namespaced, 14 local, and it sits *under* live records in Bethesda's own,
  so the field is not an allocation cursor to anyone — floor 0x800, write local form), `7bdadd5` (COBJ recipe filter
  = `FNAM` = `RecipeFilters`, was declared and dropped; `--category` override), `987a6ff` (new **`setrecipefilter`**
  command — patch the FNAM onto existing COBJs FormID-stable; the `GameEnvironment`-holds-the-file-open write trap
  is documented in it). NIF authoring lives in the modding project (`nif_from_template.py`). **All the durable
  Bethesda facts → home-office `bethesda.md`; the portable code-health lessons → `craft.md`.** The tools now exist
  to finish `bottompanel01` (same mesh-only-gap) whenever he wants it. His hands + the rig gate the rest.
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
