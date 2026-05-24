# Spec: Outlaw Mission Plan — Decouple Story from Build

> Status: draft. Subject to iteration before any code is written.

## Problem

`LoopingQuestChain.GenerateQuest()` and `StaticLayoutQuestChain.GenerateQuest()`
generate the **story** (lore, quest names, log messages, books, dialogue) and
build the **ESM records** (Quest, Book, Activator, NPC, FormList, Message,
SpaceCell, Audio) in a single pass. Every AI call lands directly on a record.

Concrete consequences:

- We can't see the finished narrative until the mod is open in-game.
- We can't reroll a single weak log message; we have to discard the whole
  chain and pay for every other AI call again.
- We can't hand-edit a quest name or a piece of dialogue without learning the
  ESM toolchain.
- `WritingPolishPass` exists precisely because text quality is the bottleneck,
  but it works on already-built records — too late to change template choices
  or restructure a stage.

## Goal

Split the current pipeline into two phases with a **Mission Plan** document
as the hand-off:

```
Phase 1 (Plan)                 Phase 2 (Build)
─────────────────              ──────────────────
LoreFile           ─┐
LoreContext         │
PlannedArc          ├──►  plan.md  ──►  ESM records (Quests, Books,
Template picks      │                   Activators, NPCs, FormLists,
Quest names         │                   Messages, SpaceCells, WAVs)
Log messages        │
Book text           │
Dialogue scripts    │
NPC names           │
Activator names    ─┘
```

The plan captures **every piece of AI-generated text and every narrative
decision**. The build step is a mechanical translation: it reads the plan,
allocates FormKeys, and stamps records. No AI calls fire during build.

This means:

- The user can read the entire mission as prose before any record is created.
- A bad log entry can be rewritten in a text editor or regenerated with a
  single CLI call — no other AI cost.
- The polish pass moves to the planning phase, where it can still affect
  template choices if needed.
- The build phase becomes deterministic and fast — useful for CI, for
  re-running after an ESM schema bump, and for shipping the same plan to
  multiple mod targets.

---

## Plan Document

### Format

GitHub-flavoured Markdown. One file per mission chain.

Default location: `questgen_quests/plans/<outlaw-slug>.md`
(slug = lowercase outlaw name with non-alphanumerics replaced by `-`).

Each section has a stable HTML anchor comment so the regeneration tool can
target it without fuzzy matching:

```markdown
<!-- plan:stage discovery -->
### Discovery — Dataslate
…
<!-- /plan:stage -->
```

The whole file is also fronted with a YAML metadata block for machine-read
fields (FormKey seeds, template engine pick, plan schema version). Anything
narrative lives in the Markdown body.

### Top-level structure

```markdown
---
plan_version: 1
chain_type: Looping            # or Static
template_engine: Random        # or AI
seed: 0x4F2A91B7               # RNG seed — re-runs of build are deterministic
generated_at: 2026-05-24T14:02:18Z
generated_by: claude-opus-4-7
---

# Mission Plan — Marek Vance

<!-- plan:outlaw -->
## Outlaw Target
**Name:** Marek Vance
**Gender:** male
**Traits:**
- Occupation: dock manifest clerk
- Crime: skimmed off cargo and burned a colleague
- Goal: vanish with the credits before the syndicate finds him
- Flaw: cocky under pressure, talks too much

**Voice:** male / ElevenLabs `voice-xyz123`
**Spacesuit required:** false
<!-- /plan:outlaw -->

<!-- plan:lorefile -->
## LoreFile
…2–3 paragraphs of canonical lore…
<!-- /plan:lorefile -->

<!-- plan:lorecontext -->
## LoreContext
…structured lore the AI fills in…
<!-- /plan:lorecontext -->

<!-- plan:arc -->
## Planned Arc
- **Discovery:** Wanted Poster — _hook: dockworker rumour_
- **Investigation 1 (10%):** Planet Conversation — _Akila bartender_
- **Investigation 2 (40%):** Space Destroy guarded by Spacer A-class — _intercept_
- **Investigation 3 (70%):** Planet Smallbase Informant — _broker_
- **Showdown (90%):** Planet Bounty — _abandoned refinery_
<!-- /plan:arc -->

<!-- plan:stage discovery -->
## Stage: Discovery — Wanted Poster
…stage block, see below…
<!-- /plan:stage -->

<!-- plan:stage investigation-1 -->
## Stage: Investigation 1 — Planet Conversation
…
<!-- /plan:stage -->

…repeat for each stage…

<!-- plan:stage showdown -->
## Stage: Showdown — Planet Bounty
…
<!-- /plan:stage -->

<!-- plan:outlaw-log -->
## Outlaw Personal Log (found document)
…log text…
<!-- /plan:outlaw-log -->
```

### Stage block

The most important block; one per quest in the chain.

```markdown
## Stage: Investigation 2 — Space Destroy (Spacer A-class)

**Template:** `Space Destroy - Guarded by Spacer A Class`
**Progress:** 40%
**Location text:** A clue hidden in orbit around Cydonia
**Faction (Label):** Spacer
**Ship FormId:** 0x00045B12
**SpaceCell design:** _none_

### Quest fields
- **Quest name:** Cydonia Cargo Strike
- **Log message:** Intercept the Spacer freighter in Cydonia orbit and recover the manifest tablet. Vance shipped through this lane two weeks ago.
- **Objective:** Recover the manifest tablet from the Spacer cargo hold

### Found item
- **Type:** Data tablet
- **Item name:** Cydonia Cargo Manifest
- **Author (speaker):** Selene Korr (female, voice female-04, ElevenLabs voice-abc987)
- **Voiced text:**
  > Two weeks back, a clerk paid me cash to vanish a crate off the books. Said his name was Vance. We dropped it at the Cydonia loop. If you're reading this, the syndicate is already looking.

### Stage history (for context only — not built)
- Discovery: a dockyard wanted poster in New Atlantis
```

The fields above are exactly what the existing `Investigation_Informant_Space.Setup()`
asks the AI for, lifted out of the build path. Different template types have
different field shapes — see *Per-template schemas* below.

### Per-template schemas

The plan format is open: each template declares which fields it needs and the
build step validates against that schema. A first cut maps the existing quest
classes 1-to-1:

| Quest class                              | Plan-relevant fields                                                                                |
|------------------------------------------|------------------------------------------------------------------------------------------------------|
| `Discovery_Dataslate`                    | quest name, briefing book name, briefing text, speaker NPC, voiced text                              |
| `Discovery_WantedPoster`                 | poster pickup message, marker location pick                                                          |
| `Investigation_Informant_Space`          | quest name, log, objective, item name, ship name, speaker NPC, voiced log, SpaceCell pick (optional) |
| `Investigation_Informant_Planet*`        | quest name, log, objective, item name, speaker NPC, voiced log                                       |
| `Investigation_ConversationCity/Planet`  | quest name, log, NPC name, NPC background, dialogue script (greeting + N exchanges)                  |
| `Investigation_ActivatorSpace*`          | quest name, log, objective, activator name, SpaceCell pick                                           |
| `Investigation_ActivatorPlanet/City`     | quest name, log, objective, activator name                                                           |
| `Investigation_ActivatorSetDungeon`      | quest name, log, objective, dungeon pick                                                             |
| `Investigation_DestroySpace*`            | quest name, log, objective, ship name, SpaceCell pick                                                |
| `Investigation_DestroySmallBase`         | quest name, log, objective, base pick                                                                |
| `Investigation_DestroySetDungeon`        | quest name, log, objective, dungeon pick                                                             |
| `Investigation_Derelict_Space`           | quest name, log, objective, derelict ship pick, found-document text                                  |
| `Showdown_BountyCity/Planet`             | quest name, log                                                                                      |

Each quest class implements two new methods alongside `Setup`:

```csharp
public interface IOutlawQuest
{
    // existing
    Quest Setup(StarfieldMod mod, OutlawNpc outlaw, MissionTemplate t, IOutlawQuest next);

    // new — Phase 1
    StagePlan Plan(OutlawPlanContext ctx, MissionTemplate t, StagePlan? nextPlan);

    // new — Phase 2 (replaces Setup eventually)
    Quest Build(StarfieldMod mod, OutlawNpc outlaw, MissionTemplate t,
                StagePlan plan, IOutlawQuest nextQuest);
}
```

`StagePlan` is a tagged record (one variant per template) that carries the
fields above. The existing `Setup` stays during migration; new code uses
`Plan` + `Build`. A stage is finished migrating when `Setup` is gone and
`Build` is purely mechanical.

---

## CLI Workflow

Three new sub-commands under `commands/quest/`.

### `gen plan`

```
FrankyCLI plan --outlaw random --out plans/marek-vance.md
FrankyCLI plan --pin-discovery "Wanted Poster" --pin-showdown "Planet Bounty" \
               --investigations 3 --out plans/...
```

What it does (mirrors the current `LoopingLayoutQuestChain.GenerateQuest`
up to the polish pass, but writes plan blocks instead of records):

1. Generate or load lore file
2. Generate lore context
3. Generate PlannedArc (existing `LorePrompts.GenerateLoreContext`)
4. Resolve outlaw NPC name + traits + voice (no Mutagen records yet — just
   data in memory)
5. For each stage, call `outlawQuest.Plan(...)` instead of `Setup(...)`
6. Run the polish pass over the plan text (the polish pass already operates
   on `IPolishable`; in plan mode it operates on plan fields rather than
   record fields)
7. Write the final markdown file

No ESM records, no audio. The only side effects are: AI calls, the plan
file, the seed in the front-matter.

### `gen plan regen`

```
FrankyCLI plan regen plans/marek-vance.md investigation-2
FrankyCLI plan regen plans/marek-vance.md investigation-2.log
FrankyCLI plan regen plans/marek-vance.md showdown.quest-name
```

Re-runs a single AI prompt for one slot and rewrites only that section.
Reads the plan file, locates the named anchor, calls the matching prompt
helper, replaces the field in place, saves. Other sections are untouched
and so are their AI costs.

Selector grammar:

- `<stage>` — regenerate the whole stage (still cheap: one stage's worth of
  prompts, not the whole chain)
- `<stage>.<field>` — regenerate one field (`log`, `quest-name`,
  `objective`, `book-text`, `dialogue`, `npc-name`, `item-name`,
  `briefing`, `voiced-log`, `pickup-message`)
- `lorefile`, `lorecontext`, `arc`, `outlaw-log` — top-level slots

Pre-existing history matters for some prompts (`GetLogMessage` uses
`AITools.RunPrompt`, which feeds the conversation). For deterministic
regen, the tool replays `LoreContext` and any earlier stage logs as
injected history before the targeted call. The seed in front-matter pins
the RNG.

### `gen build`

```
FrankyCLI build plans/marek-vance.md --output OutlawsChain.esm
```

Reads the plan, calls each stage's `Build()` to allocate records, wires up
audio, deploys. No AI calls. Fails loudly if the plan is missing required
fields for any template.

---

## Code Organisation

```
Retrograde.Library/Nouns/Quests/
  Plan/                                 ← new
    StagePlan.cs                        ← discriminated union / base class
    StagePlan_Discovery_Dataslate.cs    ← per-template plan record
    StagePlan_Investigation_*.cs
    StagePlan_Showdown_*.cs
    MissionPlan.cs                      ← whole-chain container
    PlanMarkdown.cs                     ← serialise / parse markdown ↔ MissionPlan
    PlanRegen.cs                        ← regen one field
  Discovery/   …existing files gain Plan() and Build() …
  Investigation/
  Showdown/
  LoopingQuestChain.cs                  ← orchestrates plan-only run
  StaticLayoutQuestChain.cs             ← same
  BuildFromPlan.cs                      ← new chain-equivalent for Phase 2
```

`OutlawNpc` already separates name/voice generation (`GenerateNPC`) from
record creation. The split there is the model for the other classes:
gather the data in Phase 1, write records in Phase 2.

`MissionTemplate` stays as the parameter bag, but `outlawQuest` becomes
optional during Phase 1 — Phase 1 only needs the template's parameters and
addons, not its quest implementation.

---

## Migration Plan

Don't migrate every quest class at once. Two thin slices first, then
expand.

1. **Slice 1: Discovery_Dataslate + Showdown_BountyPlanet + one Investigation
   (Investigation_Informant_Space).** Enough to round-trip a 3-stage chain
   end-to-end: plan → edit → regen one field → build.
2. **Slice 2: All Conversation-style stages.** Hardest because of dialogue
   scripts; getting the markdown format right here de-risks the rest.
3. **Slice 3: Remaining Activator / Destroy / Derelict / Discovery variants.**
4. **Slice 4: Delete the old `Setup`-only path; `Build` is canonical.**

Until slice 4 ships, the existing chains keep working unchanged.

---

## Polish Pass Relocation

`WritingPolishPass` runs against `IPolishable` items today. In the new
flow, `IPolishable` is implemented by the plan-side fields instead of by
records:

- `QuestLogPolishable` → reads/writes `StagePlan.LogMessage`
- `BookPolishable` → reads/writes `StagePlan.BookText`
- `DialogueScriptPolishable` → reads/writes the dialogue block

The pass moves to right before plan write-out. Audio still happens at
build time, on the post-polish text.

---

## Open Questions

1. **Where does NPC FormID allocation happen?** Plan stage can carry the NPC
   identity (name, gender, voice), but the FormKey can't be known until
   Build. The plan should refer to NPCs by stable slug (e.g. `npc-disc-01`)
   and Build resolves them. Same goes for FormLists, Messages, etc.

2. **Marker selection (Wanted Poster)** currently uses an AI call to pick a
   marker from a list of existing records in the mod. That requires the
   mod to be loaded. Two options: (a) move the marker pick to Phase 2 and
   accept it isn't user-editable, or (b) snapshot the candidate list into
   the plan and let the user pick. (b) is preferred but adds a load step
   to Phase 1.

3. **AI history during regen.** Replaying lore + earlier log messages
   before a targeted regen call is correct in principle but expensive
   (long context). Cheaper: only replay LoreContext + the immediately
   preceding stage's log entry, which is what the prompt actually
   references.

4. **Multi-target builds.** Same plan, different output ESM names / FormKey
   seeds — useful for variants. Out of scope for the first cut; the
   front-matter shape supports it (`seed` becomes a CLI override).

5. **Plan diffing.** A `plan diff old.md new.md` would help reviewers see
   what regen actually changed. Cheap to add once the parser exists.

6. **Schema versioning.** `plan_version: 1`. Bump when stage schemas change
   incompatibly; build refuses to consume future versions.
