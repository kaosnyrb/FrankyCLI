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

- **2026-07-23 (late) — TAIL FIN `atsd_fin01`: flips + attaches VISUALLY in-game, but the shipbuilder's
  runtime validation reports it UNATTACHED.** Editor snap passes, runtime attach check fails — two different
  checks. Five surgical commits today, each fixing a real reason a flip LOOKS broken, all tracing to the fin
  being authored **top-up** (joint at Z=0, blade rising in Z) fighting the fore-facing generator:
  `ba7cc21` **setsnap** (+ fixed `CalculateNodes` silently dropping weapon mounts from every orientation) ·
  `836910b` **setcreated** (why the flip was invisible — the visible COBJ created one GBFM not the FormList,
  and the set COBJ had no recipe filter) · `273d269` **setrotation** (gen_shipflips' hardcoded map is only
  right for a fore-facing part; fin's four orientations are all about Y) · `2071899` **setbounds** (a rotated
  variant's OBND must describe the part AS PLACED, not the base's unrotated box). Result: it now snaps + flips
  on the glass — **but runtime still says unattached, so that check wants more than a corrected axis-aligned box.**
  - **DECISION (his, for tomorrow): stop using gen_shipflips PLACEMENT-ROTATION for the fin — do it the WING
    WAY.** Each orientation its own MSTT with its mesh already in that orientation (IDENTITY placement),
    materials/swaps shared, own snap template with the joint on the correct face. No placement rotation → the
    game validates each as an ordinary module. The wing (`_port`/`_stb`) passes clean exactly this way;
    placement-rotation is only proven on rotation-INVARIANT-box parts (the Shipyards dishes — fin01 is the
    first family whose rotations change the box, which is why the OBND-copy bug hid until now).
  - **CHEAPER FIX I flagged, unspent — get the log wording FIRST.** The exact "unattached" message names the
    failing check. IF it's the grown axis-aligned OBND, runtime may apply the placement rotation itself and
    want the **UN-rotated** box = a one-field flip, not N meshes. IF it's snap-node/connection-graph/keyword,
    N meshes won't fix it either. He chose separate meshes regardless; still worth reading the log before
    paying for the exports.
  - **Modding workspace: `C:\modding\avontech_stardust`** (`nif_from_template.py`, `check_part.py`). Fin is
    3-part P/S/T (`pri`/`sec`/`tri`), deployed in Steam `Data\`. **Tomorrow's flow:** he rotates + **applies**
    + exports each orientation's meshes in Blender (apply-before-export or the `.mesh` won't move) → I rebuild
    N NIFs over their filenames (materials unchanged → the CK-repointed swaps survive) → N MSTTs sharing
    mats/swaps → `check_part` green before the in-game retest.
  - **2026-07-24 — THE PIVOT RAN AND THE HUNT IS CLOSED: rotated meshes, everything identity (`0,0,0`)
    in engine. Top, Port, Starboard in — and Stbd (a failing variant) is 100% WORKING at runtime (his
    confirmation).** Cause earned at the honest resolution: the runtime attach validator can't handle a
    placement-rotated variant of a rotation-variant-box part; which field trips was never read and no
    longer matters for the fin. Full outcome → home-office `bethesda.md` § the fin. **REMAINING:**
    (a) Bottom — he's making it now (his hands); (b) record-layer follow-through once all four are in:
    FormList regrouped over the new identity MSTTs (flip key), COBJ→FormList, swaps shared per MSTT
    (`copyswap` wire-existing), OLD placement-rotated variants retired in the same change, `check_part`
    per orientation.
  - **2026-07-24 (cont) — BOTTOM BUILT END TO END, `check_part` 0 fail 0 warn, CLEAR TO TEST.** His bot
    export had ONE unapplied rotation (bot_tri: identical envelope + identical 262 tris to base — the
    eng01 tell, caught mechanically); he re-exported, verified flipped. Then: NIF built off the cleaned
    sbd template (13 blocks, shared `fin01_*` mats), bot hull injected (17 verts exact, survived his
    NifSkope clean — second proof injection/clean are order-independent), deployed + hash-verified,
    archlist + both .ba2 rebuilt, records via `struct avontechstardust atsd fin01_bot` (CLI is
    command-first: `dotnet run -- struct <mod> <prefix> <item> <nif> [flags]`) — 7-node SNTP mirrored
    from the base template by symmetry (Z negated, tip labels swapped, equipment tip-canonical), sibling
    bounds/mass/name/swaps (shared `atsd_matswap_fin01_*`, wire-existing). esm→esp bridged, pair 98,772.
    **OPEN, his:** (a) in-game test of bot incl. the two weapon-mount facings (my tip-canonical pick is
    a proposal, his eye rules); (b) naming drift — base GBFM is "Avontech Trimmer Fin", port/sbd/bot
    ship as "Avontech Fin"; `setname` fixes all four surgically on his pick; (c) **retirement pass owed
    on his nod:** 4 old `ShipModPosition*` MSTTs + their `atsd_sn_*` templates + `atsd_gbfm_atsd_ms_*`
    GBFMs + `atsd_co_atsd_ms_fin01` COBJ + the `_franky` FormList — the dead flip system comes out in
    one change, ESM free of CK when I write. NOTE: new family = separate COBJ per orientation (no
    FormList/flip-key grouping) — four builder entries; if he wants one entry + flip key that's a
    FormList regroup, his call.

- **2026-07-23 — ✅ SHIP ENGINES SHIPPED: `gen_shipstruct --engine` built, `atsd_eng01` green end to end.**
  *(Durable engine facts — per-power storage, the 12-power identity, the class ceilings, the 21-property
  PropertySheet, the flare, the Shipyards audit, his grandfather ruling — are in home-office
  `bethesda.md` § *Ship ENGINES*. This is the FrankyCLI half.)*
  - **DONE (commit `c9a440d`):** `--engine "class=A,force=…,thruster=…,power=…,health=…,speed=…"` +
    `--mass` (mass was hardcoded to 5 for every part). Writes the full 21-property engine PropertySheet
    and the `ShipModuleClass<A|B|C>` keyword; constants read off vanilla Ares DT30, not invented.
    **REFUSES to exceed the class ceiling** (A 7620/1610 · B 8860/1850 · C 9000/3900 per power, + speed
    by class) — all seven legs bite-tested, each with its own message, multi-axis breach naming every
    axis in one run. **Also killed the silent three-vanilla-swap default** (the black-render trap) and
    deleted the three orphaned paint links in the same commit. Header comment's "engines run 1 node"
    corrected to the counted population.
  - **PROVEN:** `atsd_eng01` — MSTT · 4-node SNTP · CELL · PKIN · GBFM(ClassA) · COBJ, plus `copyswap`
    for the 3 REFL-opaque swaps; `check_part` **0 fail 0 warn** across NIF, materials, archive, records.
    Class A, power 2, 5200/1000 per power, health 70, mass 90, speed 150.
  - **OPEN GAP 1 — no flare placement.** `gen_shipstruct` writes exactly 3 placed objects (2 dummies +
    the MSTT); the engine flare is a placed vanilla MoveableStatic (`SMOD_FX_EngineMain*`) in the PackIn
    cell, so he had to add it by hand in the CK on an otherwise fully-generated part. **Proposed:
    `--flare <EditorID|0xHEX>@x,y,z[;…]`**; offsets derive from the mesh's aft face, one per nozzle.
  - **OPEN GAP 2 — `nif_from_template.py --collision` hardcodes `obj Z → game −Y`.** That is one part's
    export convention, not a law (his wing maps straight through), and when the collision was re-exported
    the other way the tool silently produced **mirrored render bounds** and reported them as fine.
    **Fix: derive bounds from the MESH** (authored in game space) and treat the collision as a
    reconciliation reading, or at minimum detect the mismatch instead of assuming.
  - **OPEN GAP 3 — `check_part` should assert MESH-vs-COLLISION orientation.** An unapplied Blender
    rotation cost a rebuild cycle today; the tell (identical tri counts + identical bounding boxes across
    a supposed flip) is mechanical and belongs in the doctor, not in me noticing.
  - **Live tail: his in-game test of eng01** (looks/snaps/paints/feels), then the mirrored variant if it
    wants a pair. Nothing owed by me meanwhile.
  - **THE GAP: no generator can author an engine.** `gen_shipstruct` writes a **1**-property PropertySheet
    (`SpaceshipPartMass`); an engine GBFM needs **21** (`SpaceshipEnginePartForce`/`MaxPower`,
    `SpaceshipThrusterPartForce`/`MaxPower`/`StrafeForce`/`MaxStrafeSpeed`, `EnginePartMaxForward/Backward
    Speed`, `ShipSystemEngineHealth`+`EMHealth`, `DamageWeightEngine`, the three Boost props, the three
    zeroed Max*Velocity props, `CrewRating`, `Health`, `Mass`, `ShipModuleVariant`) plus a
    `ShipModuleClass<A|B|C>` keyword, a manufacturer keyword, an `s_<NNN>_ShipEngine_*` sort key and a
    `ShipUpgrade_Eng_*` link. **The rest of the chain (MSTT→CELL→PKIN→GBFM→COBJ) is unchanged** — so this
    is a PropertySheet + keyword extension, not a new chain.
  - **PROPOSED (his fork, unresolved): `gen_shipengine` as its own command vs extending `gen_shipstruct`
    with an `--engine` profile.** My lean: a separate command. The two parts share a chain but not a
    vocabulary, and stuffing 20 engine-only flags onto the struct generator coupled to a part type that
    doesn't use them is the grab-bag smell. **Either way it should REFUSE to exceed the class ceiling**
    (A 7620/1610 · B 8860/1850 · C 9000/3900 thrust/manoeuvre per power) — the Shipyards audit showed
    every over-ceiling record was hand-typed and every copied one was fine, so the defect is a *process*
    one and the generator is the right place to fix it. **Also: default nothing silently** — same lesson
    as `gen_shipstruct`'s vanilla-swap default below.
  - **METHOD NOTE for whoever builds it:** the record stores thrust/manoeuvre **per power**, so the
    generator takes the per-power design number directly (no ×power arithmetic); mass and health are
    absolute per module. Verified against two byte-exact vanilla copies in his own ESM.
  - **Live tail: his call on which shape, and whether we hand-author one engine first** (my lean — one
    part through the whole chain teaches the record shape, generator writes itself after).

- **2026-07-22 — ✅ WING RESOLVED — `atsd_wing01` port DONE (renders, attaches, colours, PAINTS) + now DOCTOR-VALIDATED.**
  The head of this thread once read "REOPENED — does not render, bug not done"; the sub-bullets below are the earned
  trail that ran every suspect to ground — deploy → NIF/root-children → NifSkope MaterialID → loose materials → the
  Model recolour flag. Head closed; the investigation kept in place as the record of *how*. **The part doctor
  (`check_part.py`, its own entry under Done) now proves the port wing mechanically across all four layers, and will
  catch every one of those footguns on the NEXT part before an in-game test.**
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
  - **2026-07-22 THE BLACK WAS LOOSE MATERIALS — Starfield doesn't load loose `.mat` (his); packaged the `.ba2`.**
    Ruled out my whole lane first (base graph, formats, colour content bright, variant, MaterialIDs, swaps — all
    identical to the working Sherpa), then he named it: loose materials aren't read at runtime (CK preview reads
    them, game doesn't). Built **`build_archive.py`** (wraps Archive2: `Main.ba2` General geo/mat/mesh +
    `Textures.ba2` DDS, → Steam Data). Reconciled NifSkope'd NIF to source, regenned archlist, built both —
    verified 29 + 12 files, extract byte-exact, Sherpa intact (source complete). **Verify-by-extraction not size:
    simple BC7 compresses heavy (10.6 MB → ~180 MB byte-exact).** Wing materials now load from the archive →
    should render on game restart. **His workspace has my uncommitted files (build_archive.py, reconciled NIF,
    archlist, 6 mats) — his to commit.**
  - **2026-07-22 RECOLOUR SOLVED — the Model `Support Model Only Swap` flag (Mutagen: HasFirstPersonModel).**
    Whole recolour hunt's answer: a MoveableStatic Model flag every repainting part sets + `gen_shipstruct` never
    did → parts render/attach/colour but offer no paint. **Fixed gen_shipstruct at the root** (`Flags =
    Model.Flag.HasFirstPersonModel`; flips/rotates copy the base Model so inherit it). Found by stepping a vanilla
    field-by-field (his directive) after keywords/swaps/materials all matched — I'd chased ShipModPosition (his:
    that's flips)/swap-channel/vanilla-vs-custom, all wrong. **THE LESSON: step a known-good reference field-by-
    field BEFORE hypothesising.** **WING (port) DONE: rendered, attached, coloured, PAINTABLE.** Remaining: stb
    (set the flag or rebuild via the fixed generator), the FormList (his 5-min CK job), bottompanel01 etc.
    `gen_inspect` gained MSTT + swap keyword expansion this hunt.

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

- **2026-07-22 — WING COMPLETE, both sides, in-game ("looking awesome", his).** Starboard finished: 3-part NIF
  (`nif_from_template` — stb meshes + stb collision bounds, NifSkope-cleaned), matte (shares the port's `wing01`
  mats), paintable — `check_part` green, confirmed on-glass. **The stb SURFACED-TODO in the doctor entry below
  (flag present, no swaps) is now CLOSED.** A mirrored two-sided part shares its sibling's materials AND swaps, so
  I extended **`gen_copyswap` with a wire-existing mode** (bare EditorID wires an existing swap as-is — no deep-copy,
  no dup, no CK repoint; commit `8fc5966`) and wired the port's `wing01` swaps onto `atsd_ms_wing01_stb`. Finish
  loop that worked: build NIF → deploy raw NIF+meshes to the GAME dir (NifSkope loads from there) → he cleans →
  reconcile cleaned NIF back to workspace → `build_archive` → `check_part` green. Remaining: his CK FormList+flip.
- **2026-07-22 — Built the PART DOCTOR: `check_part.py <part>` (modding project) + FrankyCLI `checkpart` (new command).**
  A read-only pre-flight that walks a finished ship-part field-by-field and reports every footgun this session cost
  us BEFORE an in-game test — the "pull a vanilla and step through" discipline frozen into code. **Two oracles, one
  judge:** asset checks live in Python (`check_part.py` owns the NIF parser + reads `.mat` JSON + the `.ba2` name
  table); record checks come from **`gen_checkpart`** (new, registered in `Program.cs` top-level case + `mode
  switch`) which emits a `CHECKPART_JSON {…}` raw-facts line — MSTT, `Model.File`, the recolour flag, swaps resolve,
  `MSTT→PKIN→GBFM→COBJ` links, master type — and **ALL pass/fail judgement lives in the Python**, so no split-brain
  about "healthy". Compiled clean (nullable `Model.Flags` → `.Value.HasFlag`). Parts are self-describing → no
  hand-spec needed to validate (a spec is a *pipeline* concern). **Proven both ends:** all-green + true on the
  working port wing (4 groups); **FAIL on starboard — the doctor found it carries the recolour flag (inherited from
  a Model copy) but has NO swaps wired → still can't paint** (sharper than my predicted "missing flag"; it read the
  real record state, not my guess). Durable Bethesda facts (BA2 name-table format, the doctor's checklist) →
  home-office `bethesda.md`. **`check_part.py` is in his modding workspace — his to commit; the FrankyCLI half is
  mine, committed this session.** **SURFACED TODO: the stb wing needs swaps wired (+ likely the 3-part rebuild)
  before it can paint — the doctor named it.**
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
