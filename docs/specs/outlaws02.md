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

## Related

- Trello (Starfield Ideas): `Outlaws 002`, `quest author tool` ("load all quest text into nodes and
  allow editing"), `AI Lensing`, `Outlaws: Lore file reviewing` — these are **one architecture**, not
  four features: node model (have) + dry-truth/prose split (open) + judge-against-truth gate with a
  review surface (open).
- `docs/specs/npc_dialogue.md` — the dialogue scene model the dialogue nodes bake into.
