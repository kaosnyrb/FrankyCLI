# Spec: Dark Universe — Outlaws 02 (authored quest-chain generation)

> **Status:** DESIGN SEED (2026-07-08). Not built yet — this is the north star we're building toward,
> plus the grounded evidence for why. Decisions marked **[DECIDED]** / **[OPEN]**.

## The one-line goal

Outlaws 01 generated every quest's story **on the fly, once, unreviewed** — Claude wrote the text
at generation time and it was never judged or revised. Outlaws 02 makes quest generation **authored
and reviewable, like HyperBrain / Hanabi**: an intermediate **data format the writer (Jessica)
iterates on**, which is *then* passed to the generator to bake the quest chain into CK records.

The shift: **truth stops coming out of the model.** The format *is* the authored truth; the code
implements it into Mutagen records. AI moves from "invent the quest" to two smaller, bounded jobs
(optionally draft the format; lens authored truth into final prose) — never trusted to author truth.

## Why — grounded in the 01 audience (Nexus mod 15316 comments, pulled 2026-07-08)

Read of 16 player comments on Outlaws 01 (raw posts **not** committed — held session-local; this is
the distilled read). The complaints cluster into three, and the top two map directly onto the 02 design:

1. **Structural incoherence — the loudest by far (≈9 of 16 touched it).** Players describe an
   unconnected firehose: quests pile up in the log, picked up in bulk from a single POI ("half a
   dozen outlaw slates, two sitting next to each other"; "12 quests already"), with no chain relating
   them — *"the way they're presented feels wrong."* This **is** the disjointedness. It's the direct
   symptom of 01 generating each quest in isolation with no authored spine.
   → **Fixed by:** the authored chain format. A chain with a spine (acts that relate, quests that know
   they belong to one arc) is exactly what the audience is asking for.

2. **AI-writing sameness + length.** *"Every character has the same voice"*; slates *"borderline
   unreadable"*; one player literally asks for *"a prompt specifying length and writing style"*;
   another: *"plays better the less you read the lore slates."*
   → **Fixed by:** the judge criteria (voice variety + length discipline). `IPolishable.MaxChars`
   already exists as the length lever; voice-variety is a criterion to add. **The criteria are
   creative direction — Jessica's lane**, enforced by the review/judge gate.

3. **Pacing / encounter rate** — *"should be spice, not the main thing."* A tuning knob, plus real
   per-quest logic bugs (e.g. a stuck Ironkeel / Dust Haze quest). Tracked separately from the
   authoring architecture.

Balance note: there is genuine love for the concept (*"whispers in the void… tales of old
long-haulers… feel as old as travel itself"*) and explicit demand for **"Version 02?"** — the
premise works; the delivery is what 01 got wrong.

## What already exists in the code (the seam is half-built)

- `Retrograde.Library/Nouns/Quests/IOutlawQuest.cs` — `GetPolishables()` + `StageAudio()`: text is
  generated, exposed for improvement, audio staged **after** so voice uses the improved text.
- `Retrograde.Library/Core/Writing/IPolishable.cs` — a clean **node abstraction**: every text piece
  (quest name, log, book, dialogue) is a node with `Label`, `MaxChars`, `ContentType`, `StoryStage`,
  `GetText/SetText`. This *is* the "quest author tool" data model, already in code.
- `Retrograde.Library/Core/Writing/WritingPolishPass.cs` + `Core/AI/Prompts/PolishPrompts.cs` — an
  iterative pass (default `Iterations = 0`) with real style criteria in the prompt.

**Gap vs HyperBrain/Hanabi:** the current pass **fuses judge and author in one call** (it identifies
the weak pieces *and* rewrites them, with no separate verdict, no gate, and no re-judge of the
rewrite), and has **no ground-truth anchor** — its criteria are all *style*, nothing checks prose
against the *facts* of the quest. So it's a polish pass, not a review loop, and the "AI can't be
trusted with the truth" problem is untouched.

## Design direction

- **[DECIDED] Thick format — Jessica writes, the code implements.** The format carries the actual
  authored text (the words), and the generator is mostly a **baker** (format → CK records); AI is an
  optional first-draft/lens, not the author of truth. (His call, 2026-07-08: *"her writing and the
  code implementing."*)
- **[DECIDED] An authored chain, not independent quests.** The format describes a *chain with a
  spine*, directly answering complaint #1. The C# quest nouns (Discovery / Investigation / Showdown /
  bounty types) are the fixed **vocabulary** (they know how to bake to records); the format is the
  **specific chain** composed from them. Writer authors sentences; the grammar stays in C#.
- **[OPEN] Judge/author separation.** To be HyperBrain-shaped: split the fused pass into judge
  (score vs criteria) → author (improve failures) → re-judge. Criteria = Jessica's (voice variety,
  length, coherence-to-chain).
- **[OPEN] Ground-truth anchor ("AI Lensing," Trello).** Consider a per-node dry-truth field the
  prose must not contradict, so the judge checks surface-against-truth, not just style.
- **[OPEN] Schema discipline (Eliza's flag).** The format is now a **contract** between writer and
  generator — it wants a real *validated* schema that fails loudly on a malformed chain, built the
  same change as the format (not after). A loose format = silently-broken quests.

## Locations & interiors — the "new places" problem (3rd 01 gap)

**The gap (the author's finding, distinct from the Nexus read):** Outlaws 01 added **no new
locations** — quests sent the player to *existing* POIs. For anyone who has explored the game as much
as the author, that means wandering places they've already cleared. Fresh *place* is what makes
exploration feel like exploration; 01 added none. This caps the ceiling harder than the writing does
— a quality problem annoys, a novelty problem bores.

**The governing principle (the whole arc in one line):** *author the atoms, automate the composition
— and borrow the expensive atoms wherever Bethesda already authored them.* Procedural generation is a
**multiplier on authored content, never a substitute.** Ten hand-made buildings placed randomly is
still ten buildings; a veteran burns through them as fast as through the base game's.

### Exteriors already solve novelty — use them (the cheap 02 unlock)

The exterior POI generator (`Nouns/WorldspaceNouns`, e.g. `ScienceOutpostDesign`) is a **working
new-place engine**, and it escapes the authoring tax by borrowing the expensive atom: it **clones a
vanilla terrain template** (`DR*`/`OE*` worldspaces) and repopulates it with passes; nature-scatter
(rock/veg) is procedural and *forgiving*; navmesh is **seeded and the CK generates it** (the CK's
navmesh tool is smart — seed markers suffice; `Passes/Utility/NavmeshSeedPass.cs`). The author's only
real burden outdoors is the building. **→ New surface POIs *are* new places. Outlaws 02's
"new locations" need is NOT blocked on interiors — lean on the exterior generator that already ships.**

### Interiors — the wall is authoring, not tech

Navmesh is **not** the wall (CK handles it). Walls/doors are **not** the wall (just a grid). The wall
is **interior decor** — believably arranging furniture + clutter. And the engine for it **already
exists and is correct:** `Nouns/Stations/Passes/Content/ContentPass.cs` reads hand-placed `rg_slot_*`
markers, looks up a same-named FormList of **PackIn vignettes**, picks one, and places it at the
marker's baked transform — with **district-tuned density** (`GetCullChance`: living cluttered, utility
sparse, boss packed) and dedup spacing. Crucially it is **fully deterministic — no LLM, and the model
is never handed a coordinate.** The author's manual Retrograde process (copy-paste table+clutter
chunks out of vanilla interiors) was **hand-harvesting vignettes into this exact system.**

**So the remaining gap is the two authored inputs ContentPass eats** — the vignette library (PackIns +
`rg_slot_*` FormLists) and the slot markers — and both are **harvest problems, not compose problems:**

- **Harvest pass (the real unbuilt work):** mine vanilla interior cells, spatially cluster
  placed-objects into candidate chunks, emit each as a PackIn, auto-file into the right `rg_slot_*`
  FormList. Geometry clustering — deterministic.
- **AI's only honest role:** *semantic tagging* of a harvested cluster ("mess-hall table group →
  `slot_dining`") — a label, never a transform. (Trying to teach Claude to *pair/arrange* furniture
  aimed AI at the one spot in the pipeline where it doesn't belong — which is exactly why it was "meh.")
- **Slot markers:** stamp them on the grid deterministically (wall-adjacent cell → wall slot, open
  cell → floor slot), or harvest them with the shell.

### The readable-seams finding (production reality — the honest limit)

**In practice the seams between placed PackIns read badly:** discrete vignette islands in a bare room
— *"an empty room with a bunch of PackIns in it."* Why: the vignettes capture the **hero clusters**
but not the **connective tissue** — the continuous low-level dressing (floor debris, cable runs, wall
panels/screens, trim, small bridging clutter) that fills the negative space and dresses the edges.
And interiors demand **continuous, intentional fullness**, where exteriors tolerate **sparse, random**
scatter — so the same island-scatter that reads "natural" outdoors reads "unfinished" indoors.
*That is the deep reason the exterior playbook does not port cleanly.*

**Fix — the missing layers (dissolve the seams; don't chase perfect vignettes):**

- **Continuous interior scatter pass** — the interior twin of `RockScatterPass`/`VegetationScatterPass`:
  low-level floor clutter/debris/cables placed *across* the room, **ignoring vignette boundaries**, so
  the continuous layer has no seams to see.
- **Wall/edge dressing pass** — wall props (screens, panels, pipes, signage) along the grid's wall
  segments. Bare walls are the loudest "empty" tell.
- Denser slot layouts help a little (turn `GetCullChance` down) but only pack the *same islands* — the
  **continuous** layers are what actually knit.

**The honest resolution the author already shipped: a hybrid** — harvested vignettes for the bulk +
**handcrafted parts per room** to knit the seams. This is legitimate and probably correct: fully
procedural interiors that pass the eye are genuinely hard. **The goal is not 100% automation — it is
shifting the ratio** so the handcraft becomes *knitting* (cheap, fun) rather than *building*
(expensive). The scatter + wall-dressing passes are precisely what shrink the per-room hand-finish.

### Card cross-refs

`Roomshape dynamic` (library of decorated sections — the right instinct; the vignette-PackIn is its
concrete form) · `Room learner` (**retired framing** — it aimed AI at composition; harvest instead) ·
`Lighting swapper` / `Warehouse rooms` (clone-and-redress vanilla interiors) · `spaceship Ikea`
(the connective-dressing / wall-fit layer).

## Related

- Trello (Starfield Ideas): `Outlaws 002`, `quest author tool` ("load all quest text into nodes and
  allow editing"), `AI Lensing`, `Outlaws: Lore file reviewing` — these are **one architecture**, not
  four features: node model (have) + dry-truth/prose split (open) + judge-against-truth gate with a
  review surface (open).
- `docs/specs/npc_dialogue.md` — the dialogue scene model the dialogue nodes bake into.
