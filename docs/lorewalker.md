# Lorewalker: MVP Implementation Plan

A tightly contained MVP to test the grammar-driven quest generation concepts from `docs/narrative_design.md`. Same game output, same quest types, same templates — but with a structural grammar layer that enriches the AI context with arc shaping, stage roles, character webs, and entity manifests.

---

## 1. Hypothesis

**"Does a grammar layer + enriched context produce meaningfully better quest stories using the same underlying quest structures?"**

The existing bounty hunt is already the "Justice" motivation. Lorewalker enriches it with:

- **Arc shaping** — tension curves from Reagan's emotional arcs instead of linear 0%→90%
- **Stage roles** — Setback, Revelation, Escalation instead of flat "InitialInvestigation"/"DeepInvestigation"
- **Character webs** — 3-5 NPCs with relationships, secrets, agendas (not just "the outlaw")
- **Entity manifests** — explicit per-stage entity lists to prevent LLM hallucination
- **Moral framing** — the target isn't always simply guilty

If this produces measurably better quests using the exact same 21 `IOutlawQuest` implementations and 18 template libraries, the architecture is validated. Adding new motivations (Rescue, Betrayal, Redemption) becomes content expansion — no architectural risk.

### What Lorewalker is NOT

- Not a new motivation. Justice only.
- Not new quest types. No Rescue/Defend/Escape/Negotiate implementations.
- Not branching. Same linear chain structure.
- Not template-only mode. Still uses LLM for all text generation.
- Not a full evaluation pipeline. Entity validation + manual A/B comparison only.

---

## 2. Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────┐
│  gen_quest_main.cs                                      │
│  List<IQuestchain> { LoopingLayout, Lorewalker }        │
│  questchains[random.Next(count)].GenerateQuest()        │
└────────────┬────────────────────────────┬───────────────┘
             │                            │
     LoopingLayoutQuestChain      LorewalkerQuestChain
     (unchanged)                  (new)
                                         │
                              ┌──────────┴──────────┐
                              │   GrammarEngine      │
                              │   ┌────────────────┐ │
                              │   │ GrammarData    │ │ strategies, roles, framings
                              │   │ ArcCurve       │ │ 6 tension curves
                              │   │ CharacterWeb   │ │ cast generator
                              │   │ StageSpec      │ │ per-stage specs
                              │   │ EntityValidator│ │ post-gen check
                              │   └────────────────┘ │
                              └──────────┬──────────┘
                                         │
                              enriched Addons + parameters
                                         │
                              ┌──────────┴──────────┐
                              │  existing pipeline   │
                              │  AllTemplateManager  │
                              │  IOutlawQuest.Setup()│
                              │  WritingPolishPass   │
                              │  SpeechTools         │
                              └─────────────────────┘
```

### Key Design Principle: Addons Are the Entire Injection Surface

Every AI prompt in the system ends with `"Additional Information:\r\n"` followed by the `Addons` list concatenated as strings. The grammar layer produces richer Addons — it never touches the quest-building code in `IOutlawQuest.Setup()`.

This means:

- **Zero changes to any of the 21 `IOutlawQuest` implementations**
- **Zero changes to any of the 18 template library files**
- **Zero changes to `NarrativePrompts.cs`, `QuestPrompts.cs`**
- **Zero changes to the writing polish pass or audio pipeline**

The grammar makes structural decisions. Those decisions become Addons strings. The existing pipeline reads those strings and writes better content.

### Data Flow

```
1. Roll OutlawNpc (existing)
2. Generate LoreFile + LoreContext (existing)
3. GrammarEngine.SelectStrategy()          ← NEW
4. GrammarEngine.BuildStageSpecs()         ← NEW
5. CharacterWeb.Generate()                 ← NEW (one AI call)
6. GrammarEngine.AssembleEnrichedAddons()  ← NEW
7. Template selection (existing AllTemplateManager)
8. Backward generation loop (existing — Setup() calls)
   └─ EntityValidator.Check() after each stage  ← NEW
9. Writing polish pass (existing)
10. Audio staging (existing)
```

Steps 3-6 and the validator call in step 8 are the only new code. Everything else is the existing pipeline.

---

## 3. Grammar Data Definitions

All grammar data lives in static C# classes, following the pattern of `StorySeedData.cs` (static readonly lists, no config files, no YAML).

### Location

New directory: `Retrograde.Library/Core/Grammar/`

### Enums

```
Motivation       { Justice }                    — one for MVP; extensible enum
StageRole        { Discovery, Escalation, Setback, Revelation, Resolution }
MoralFraming     { ClearGuilt, GuiltyButJustified, Framed, SympatheticFugitive }
ArcShape         { RagsToRiches, Tragedy, ManInAHole, Icarus, Cinderella, Oedipus }
```

`StageRole` replaces the current `"InitialInvestigation"` / `"DeepInvestigation"` labels with semantically meaningful roles. A `Setback` stage means "things go wrong here." A `Revelation` stage means "the player learns something that reframes the situation." These roles feed directly into Addons and shape the AI's tone.

### Strategies

A strategy is a named pattern of stage roles paired with a target emotional arc. Three strategies for Justice:

**Steady Hunt** — The simplest form. A clean escalation to a satisfying conclusion.
```
Stages:   Discovery → Escalation → Escalation → Resolution
Arc:      Rags to Riches (steady rise)
Framings: ClearGuilt only
```

**Cold Trail** — The investigation hits a dead end. The player must recover. This is the "Man in a Hole" pattern — the most popular arc shape in fiction.
```
Stages:   Discovery → Escalation → Setback → Revelation → Resolution
Arc:      Man in a Hole (fall then rise)
Framings: ClearGuilt, GuiltyButJustified
```

**Double Bluff** — The target was framed. The real villain is someone the player already encountered. The Oedipus arc — fall, rise, fall.
```
Stages:   Discovery → Escalation → Revelation → Resolution
Arc:      Oedipus (fall-rise-fall)
Framings: Framed, SympatheticFugitive
```

Each strategy specifies:
- A `List<StageRole>` defining the slot pattern
- A target `ArcShape` for tension curve sampling
- A set of valid `MoralFraming` values (randomly selected at chain start)
- An optional minimum/maximum stage count range for variable-length investigation phases

### Arc Curves

Six named tension curves from Reagan et al., defined as float arrays. Each curve maps normalized position (0.0 = start, 1.0 = end) to tension (0.0 = low, 1.0 = maximum).

```
Rags to Riches:  [0.2, 0.35, 0.5, 0.65, 0.8, 0.9]           — steady rise
Tragedy:         [0.8, 0.7, 0.55, 0.4, 0.25, 0.15]           — steady fall
Man in a Hole:   [0.3, 0.5, 0.8, 0.85, 0.5, 0.2]             — fall then rise
Icarus:          [0.7, 0.5, 0.2, 0.15, 0.5, 0.85]            — rise then fall
Cinderella:      [0.2, 0.4, 0.7, 0.8, 0.5, 0.2]              — rise-fall-rise
Oedipus:         [0.7, 0.4, 0.2, 0.3, 0.7, 0.9]              — fall-rise-fall
```

`ArcCurve.SampleTension(ArcShape shape, float normalizedPosition)` interpolates between curve points. `ArcCurve.SampleMood(float tension)` maps tension ranges to mood descriptors:

| Tension Range | Mood |
|---|---|
| 0.0 – 0.2 | calm, reflective |
| 0.2 – 0.4 | curious, uneasy |
| 0.4 – 0.6 | focused, tense |
| 0.6 – 0.8 | urgent, desperate |
| 0.8 – 1.0 | climactic, resolved |

---

## 4. Character Web

### What It Is

A small cast of 3-5 characters with relationships to the target, secrets, agendas, and a composure score. Generated via one AI call after `LoreContext` is established.

### WebCharacter Fields

| Field | Type | Purpose |
|---|---|---|
| `Name` | string | Character's full name |
| `Role` | enum | Target, Fixer, Witness, Informant, Complicator |
| `Relationship` | string | Specific connection to target ("former partner", "estranged sibling", "debt holder") |
| `Secret` | string | What they know but won't share freely |
| `Agenda` | string | What they want from the interaction with the player |
| `Composure` | float | 0.0 (nervous wreck) to 1.0 (ice cold) — feeds into voice direction |

### Generation

One AI call using `AITools.RunPrompt()`, after LoreContext generation (step 5 in the data flow). The prompt:

1. Provides the LoreContext and OutlawNpc traits
2. Specifies the selected `MoralFraming` (e.g., "the target was framed — one of the supporting characters is the real villain")
3. Requests exactly N characters in structured XML format
4. Constrains: "Use only locations and factions from the LoreContext. Do not invent new ones."

The response is parsed into `WebCharacter` objects. The Target character is always the existing `OutlawNpc` — the AI generates the supporting cast around them.

### How It Flows Into Prompts

The character web is NOT injected as one block. The `GrammarEngine` slices it per-stage:

- A conversation-in-city investigation might get the Witness character
- A planet informant investigation might get the Informant character
- The showdown gets the Target
- The Fixer appears in the discovery briefing

Each stage sees only the characters relevant to that scene, serialized as `<CharacterInScene>` Addon blocks. This follows the selective grounding principle from Section 7.1 of `narrative_design.md` — provide only the relevant context, not the entire cast.

### The Target Character

The `OutlawNpc` is always the Target in the character web. Their `Relationship` is "self", their `Secret` depends on the moral framing:

| MoralFraming | Target's Secret |
|---|---|
| ClearGuilt | Knows they're guilty, has no defense |
| GuiltyButJustified | Had a reason the player hasn't learned yet |
| Framed | Didn't do it — someone else in the web is responsible |
| SympatheticFugitive | Running from something worse than justice |

This secret never appears directly in Addons. Instead, it shapes what other characters say *about* the target, creating a breadcrumb trail the player follows.

---

## 5. Enriched Addons

### Before (Current System)

A stage's Addons today contain only structural markers:

```xml
<QuestStage>DeepInvestigation</QuestStage>
<QuestProgress>70%</QuestProgress>
<QuestStageLocation stage="Discovery">A small outpost near Gagarin</QuestStageLocation>
<QuestStageLocation stage="InitialInvestigation">Neon starport</QuestStageLocation>
```

The AI receives this and writes generic investigation prose. It knows *where* the player has been and *how far along* they are, but not *what the story is about*, *who is in this scene*, *what mood to hit*, or *what this stage's role is in the arc*.

### After (Lorewalker)

The same stage's Addons now carry the full grammar context:

```xml
<Motivation>Justice</Motivation>
<Strategy>Cold Trail</Strategy>
<MoralFraming>GuiltyButJustified</MoralFraming>

<QuestStage>Investigation</QuestStage>
<StageRole>SETBACK — the lead was wrong or the informant lied.
  The player is further from the target than they thought.
  This stage should feel like a step backward.</StageRole>
<QuestProgress>50%</QuestProgress>

<EmotionalArc>Man in a Hole</EmotionalArc>
<Tension>0.8</Tension>
<Mood>desperate</Mood>

<CharacterInScene name="Dara Okonkwo" role="Informant">
  Relationship: the target's former cargo partner.
  Secret: knows the target diverted supplies to cover a family debt,
  but claims they skimmed for personal profit.
  Agenda: wants the player to find the target before UC Security does —
  owes the target a favor but won't say why.
  Composure: 0.3 (nervous, evasive).
</CharacterInScene>

<EntityManifest>
  Characters: Dara Okonkwo (informant, present), Kael Vasquez (target, absent)
  Locations: Cydonia docking bay, the fuel depot where the diversion happened
  Factions: UC Security, Trade Authority
  Do NOT reference any character, location, or faction not in this list.
</EntityManifest>

<QuestStageLocation stage="Discovery">Cydonia bounty board</QuestStageLocation>
<QuestStageLocation stage="Escalation">Trade Authority office, Neon</QuestStageLocation>
```

The AI now knows:
- This is a setback stage. The lead should go wrong or the NPC should mislead.
- The mood is desperate. The writing should reflect that.
- Dara is nervous and has her own agenda. She's lying about the target's motive.
- Only these specific characters, locations, and factions may be referenced.
- The target had a justified reason, but the player doesn't know that yet.

The existing prompts in `QuestPrompts.GetLogMessage()`, `NarrativePrompts.GetFirstPersonAccount()`, etc. all end with `"Additional Information:\r\n"` and then concatenate Addons. They will read these enriched tags without any code changes — the LLM interprets the XML naturally.

### Addon Assembly

The `GrammarEngine.AssembleEnrichedAddons()` method builds the Addon list for each stage by:

1. Adding the grammar context tags (`<Motivation>`, `<Strategy>`, `<MoralFraming>`)
2. Adding the stage role description (from the strategy's slot pattern)
3. Sampling tension and mood from the arc curve at this stage's normalized position
4. Slicing the character web to only characters present in this stage
5. Building the entity manifest from the character slice + template location
6. Appending the existing `<QuestStage>`, `<QuestProgress>`, and `<QuestStageLocation>` tags

The result is a `List<string>` — same type as `MissionTemplate.Addons` today. It's set on the template before `Setup()` is called.

---

## 6. Orchestrator Flow

`LorewalkerQuestChain : IQuestchain` parallels `LoopingLayoutQuestChain`. Here's what's the same and what's new:

### Same as LoopingLayoutQuestChain

| Step | What It Does | Where |
|---|---|---|
| Roll OutlawNpc | `new OutlawNpc(myMod, true)` | Same |
| Generate LoreFile | `LorePrompts.GenerateLoreFile(traits)` | Same |
| Generate LoreContext | `LorePrompts.GenerateLoreContext(...)` | Same call, but with `selectArc: false` — the grammar handles arc planning |
| Template selection | `templateManager.GetInvestigationMissionTemplate()` etc. | Same mechanism |
| Backward generation | Showdown first → investigations → discovery | Same loop, same `Setup()` calls |
| Inject log messages | `AITools.InjectContextIntoHistory(logMessage)` | Same |
| NPC generation | `outlawNpc.GenerateNPC()`, `outlawNpc.GenerateLog()` | Same |
| Writing polish | `WritingPolishPass.Run(polishables)` | Same |
| Audio staging | `StageAudio()` + `SpeechTools` | Same |

### New in LorewalkerQuestChain

| Step | What It Does | When |
|---|---|---|
| Select strategy | `GrammarEngine.SelectStrategy(Motivation.Justice)` — weighted random from strategy pool | After LoreContext, before template selection |
| Build stage specs | `GrammarEngine.BuildStageSpecs(strategy)` — produces `List<StageSpec>` with roles, tension, mood | After strategy selection |
| Generate character web | `CharacterWeb.Generate(outlawNpc, loreContext, moralFraming)` — one AI call | After stage specs, before template selection |
| Inject character web | `AITools.InjectContextIntoHistory(characterWebSummary)` | After web generation |
| Enrich Addons | `GrammarEngine.AssembleEnrichedAddons(stageSpec, characterWeb, template)` | After template selection, before `Setup()` |
| Validate entities | `EntityValidator.Check(entityManifest, generatedText)` | After each `Setup()` call |

### How Template Selection Changes

The grammar constrains template selection without changing the selection mechanism:

1. The strategy's stage pattern defines how many investigation stages and their roles
2. The stage count comes from the strategy (e.g., Cold Trail always has 3 investigations)
3. Template selection still uses `AllTemplateManager` with `AI_TemplateEngine` or `RandomTemplateEngine`
4. The grammar does NOT filter templates by action type in the MVP — all existing investigation templates are valid for any stage role. The stage role shapes the *prompts*, not the *template pool*.

This is a deliberate simplification. The full grammar system (narrative_design.md Section 10.6) envisions action-type-based template filtering. The MVP tests whether enriched Addons alone — without changing which templates are selected — produce better stories. If they do, action-type filtering is a follow-up optimization, not a prerequisite.

### The `selectArc: false` Flag

`LorePrompts.GenerateLoreContext()` already has a `selectArc` parameter (line 82 of `LorePrompts.cs`). When `false`, it skips the Phase 2 `PlannedArc` AI call. Lorewalker uses this because the grammar replaces the AI's arc selection with structural strategy selection. The AI still generates the LoreContext (Phase 1) — it just doesn't choose which templates to use.

---

## 7. Entity Validation

### What It Does

After each stage's `Setup()` call, extract proper nouns from the generated text and compare against the entity manifest. Log violations as warnings.

### Implementation

1. **Extract** — Regex-based: find all capitalized multi-word phrases (2+ words starting with uppercase) and single capitalized words that aren't sentence starters
2. **Filter** — Remove common false positives: "UC", "Starfield", "Trade Authority", faction names, planet names (maintained in a static allowlist)
3. **Compare** — Check each extracted entity against the stage's entity manifest (the `<EntityManifest>` content used to generate the Addons)
4. **Log** — Print violations to console: `"[EntityValidator] Stage 'Cold Trail - Investigation 2': found 'Detective Sarah Chen' — not in manifest"`

### What It Catches

- Hallucinated character names (the most common LLM failure)
- Fabricated location names
- Made-up faction names

### What It Doesn't Catch

- Correct entity names used with wrong facts
- Tone violations
- Structural issues

### MVP Scope

Entity validation is **observational only** — it logs warnings but does not block generation or trigger retries. The goal is to measure the hallucination rate and see whether the `<EntityManifest>` negative constraint reduces it compared to the current system (which has no entity manifest).

---

## 8. Implementation Phases

### Dependency Graph

```
Phase 1 (GrammarData, ArcCurve, StageSpec)    ← no dependencies
    │
Phase 2 (CharacterWeb)                        ← depends on Phase 1 for MoralFraming enum
    │
Phase 3 (GrammarEngine)                       ← depends on Phase 1 + Phase 2
    │
Phase 4 (LorewalkerQuestChain)                ← depends on Phase 3
    │
Phase 5 (EntityValidator)                     ← depends on Phase 3 for entity manifests
    │
Phase 6 (Integration)                         ← depends on Phase 4 + Phase 5
```

### Phase 1: Grammar Data Layer

**Files:** `Core/Grammar/GrammarData.cs`, `Core/Grammar/ArcCurve.cs`, `Core/Grammar/StageSpec.cs`

Pure data modeling with no side effects. Define enums, strategy records, arc curves, and the `StageSpec` data class. Can be built and verified by inspection — no runtime dependencies.

**Verification:** Build compiles. Enums and strategies are well-formed.

### Phase 2: Character Web Generator

**File:** `Core/Grammar/CharacterWeb.cs`

Define `WebCharacter` record and the generation method. This is the one new AI call in the pipeline — it takes `OutlawNpc`, `LoreContext`, and `MoralFraming` and returns a populated web.

**Verification:** Run the AI call standalone (via `gen_promptlab` or a test harness). Verify the output parses into `WebCharacter` objects. Verify it doesn't invent locations or factions not in the LoreContext.

### Phase 3: Grammar Engine

**File:** `Core/Grammar/GrammarEngine.cs`

The integration point. Selects strategy, computes tension curves, assigns characters to stages, builds entity manifests, assembles enriched Addons. This is the largest new file (~250 lines).

**Verification:** Given a strategy and character web, produces well-formed Addons lists for each stage. Tension values match the expected arc curve. Entity manifests include all and only the expected entities.

### Phase 4: Lorewalker Orchestrator

**File:** `Nouns/Quests/LorewalkerQuestChain.cs`

The new `IQuestchain` implementation. Structurally follows `LoopingLayoutQuestChain` with the grammar steps inserted at the right points.

**Verification:** Full end-to-end run: generates a complete quest chain with enriched Addons. Output .esm is loadable in xEdit. All stage quests are properly linked.

### Phase 5: Entity Validator

**File:** `Core/Grammar/EntityValidator.cs`

Post-generation entity extraction and manifest comparison. Logs warnings.

**Verification:** Run on generated output. Review false positive rate. Verify that entity violations are real hallucinations, not parser errors.

### Phase 6: Integration

**File modification:** `commands/quest/gen_quest_main.cs` (3 lines)

Add `LorewalkerQuestChain` to the `List<IQuestchain>` dispatch list. Both chain types are available; the random selector picks one per run.

**Verification:** Run `gen_quest` multiple times. Verify that both chain types fire and produce valid output. Compare output quality side-by-side.

---

## 9. File Inventory

### New Files (7)

| File | Purpose | Est. Lines |
|---|---|---|
| `Retrograde.Library/Core/Grammar/GrammarData.cs` | Motivation, Strategy, StageRole, MoralFraming definitions + Justice strategy pool | ~200 |
| `Retrograde.Library/Core/Grammar/ArcCurve.cs` | 6 Reagan arc shapes as float arrays + tension/mood sampling | ~80 |
| `Retrograde.Library/Core/Grammar/StageSpec.cs` | Per-stage data class: role, tension, mood, character slice, entity manifest | ~60 |
| `Retrograde.Library/Core/Grammar/CharacterWeb.cs` | WebCharacter record + AI-powered web generator | ~150 |
| `Retrograde.Library/Core/Grammar/GrammarEngine.cs` | Strategy selection, stage spec building, Addon assembly | ~250 |
| `Retrograde.Library/Nouns/Quests/LorewalkerQuestChain.cs` | New orchestrator implementing `IQuestchain` | ~280 |
| `Retrograde.Library/Core/Grammar/EntityValidator.cs` | Post-generation entity grounding check | ~80 |

**Total new code:** ~1,100 lines across 7 files.

### Modified Files (2)

| File | Change | Lines Changed |
|---|---|---|
| `commands/quest/gen_quest_main.cs` | Add `new LorewalkerQuestChain(myMod)` to `List<IQuestchain>` | ~3 |
| `Retrograde.Library/Core/AI/Prompts/LorePrompts.cs` | Potentially minor — Lorewalker calls `GenerateLoreContext(..., selectArc: false)` which already exists. May need to make `LoreContext` accessible if it isn't already (it's a public static field, so no change needed). | ~0 |

### Unchanged Files

- All 21 `IOutlawQuest` implementations
- All 18 `Templates_*.cs` template libraries
- `IOutlawQuest.cs` interface
- `MissionTemplate.cs`
- `TemplateLib.cs` and all template engine classes
- `NarrativePrompts.cs`, `QuestPrompts.cs`
- `WritingPolishPass.cs`
- `SpeechTools` and the entire audio pipeline
- `OutlawNpc.cs`, `OutlawTraits.cs`, `StorySeedData.cs`

---

## 10. What's Explicitly Out of Scope

| Feature | Why It's Out | When It Comes In |
|---|---|---|
| New motivations (Rescue, Betrayal, etc.) | Content expansion — validate architecture first | After MVP proves the grammar layer works |
| New `IOutlawQuest` implementations | Need Rescue/Defend/Escape/Negotiate quest types for new motivations | With new motivations |
| DAG / branching quest structures | Requires `Meta_Fork_Exclusive` integration with grammar | After linear grammar is proven |
| Template-only generation mode | Valuable for iteration speed but separate concern | Parallel workstream |
| Action-type template filtering | Can test enriched Addons without changing template selection | Follow-up optimization |
| LLM-as-Judge evaluation (Tier 2) | Too heavy for MVP; entity validation + manual A/B is sufficient | After grammar stabilizes |
| Dialogue act constraints | Requires per-NPC prompt modifications in `IOutlawQuest.Setup()` | After character web proves its value |
| Voice performance per-NPC (ElevenLabs stability/similarity) | Requires changes to SpeechTools | After dialogue quality improves |
| Multiple resolution types at showdown | Requires new showdown implementations | With new motivations |
| Unreliable narrator bias tags | Advanced character web feature | After basic character web works |
| Cross-chain continuity | Explicitly out of scope per `narrative_design.md` | Long-term |

---

## 11. A/B Comparison Protocol

### How to Test

1. Run `gen_quest` 5 times with only `LoopingLayoutQuestChain` in the dispatch list
2. Run `gen_quest` 5 times with only `LorewalkerQuestChain` in the dispatch list
3. For each run, save:
   - The console output (template selections, stage names, entity validation warnings)
   - The generated quest names and log messages (from the .esm via xEdit or `gen_deprefscan`)
   - The full AI conversation history (if exportable)

### What to Compare

| Dimension | How to Evaluate | What "Better" Looks Like |
|---|---|---|
| **Narrative coherence** | Read all stage log entries in sequence | Events build on each other; later stages reference earlier discoveries naturally |
| **Character consistency** | Check if NPCs behave according to their web descriptions | An NPC described as "nervous, composure 0.3" reads as nervous in their dialogue |
| **Entity grounding** | Count EntityValidator warnings per chain | Fewer hallucinated names/locations in Lorewalker chains |
| **Emotional arc** | Read the tone progression across stages | Lorewalker chains have a perceptible emotional shape (e.g., things get worse before they get better) |
| **Stage distinctiveness** | Compare investigation stages within a chain | Lorewalker stages feel different from each other; current system stages feel samey |
| **Moral depth** | Check if the target feels one-dimensional | Lorewalker chains with GuiltyButJustified/Framed framings produce more complex targets |

### Success Criteria

The MVP succeeds if:
1. Lorewalker chains produce at least **3 out of 5** noticeably better stories in blind reading
2. Entity validation warnings are **lower** (fewer hallucinated names) than the current system
3. The emotional arc is **perceptible** — a reader can identify the setback/revelation without being told where it is
4. No regressions in structural quality — all quests are still valid, linked, and loadable in xEdit
