# Lorewalker — Executive Summary

Lorewalker is a grammar-driven quest generation layer that sits on top of the existing bounty hunt pipeline. It produces the same playable .esm quest chains using the same 21 quest types and 18 template libraries — but injects richer narrative context through the existing Addons mechanism, giving the LLM dramatically better material to work with.

## What It Does

The existing system generates quest stages with minimal context: a stage name (`DeepInvestigation`) and a linear progress percentage (`70%`). The LLM fills in the blanks, often producing generic investigation prose.

Lorewalker adds a **grammar layer** that makes structural story decisions before any LLM call fires:

- **Strategy selection** — Instead of "generate 2-5 random investigation stages," the grammar picks a named strategy (Steady Hunt, Cold Trail, Double Bluff) that defines the stage pattern, emotional arc, and moral framing.
- **Emotional arc shaping** — Tension values sampled from Reagan et al.'s six story shapes (Man in a Hole, Icarus, Oedipus, etc.) replace the flat linear 0%→90% ramp. Each stage gets a tension value and mood descriptor.
- **Stage roles** — Stages are labeled as Escalation, Setback, or Revelation instead of generic "investigation." A Setback stage tells the LLM "things go wrong here." A Revelation tells it "the player learns something that changes everything."
- **Character webs** — A cast of 3-5 named NPCs with relationships, secrets, agendas, and composure scores. Each stage sees only the characters present in that scene.
- **Entity manifests** — Per-stage lists of allowed characters, locations, and factions with an explicit "do NOT reference anything not in this list" constraint.
- **Moral framing** — The target isn't always simply guilty. Four framings (Clear Guilt, Guilty But Justified, Framed, Sympathetic Fugitive) shape what characters say and how the story resolves.

All of this flows into the existing prompts as enriched Addons XML tags. No quest implementation or template library was modified.

## Architecture

```
gen_quest_main.cs
├── LoopingLayoutQuestChain  (existing, unchanged)
└── LorewalkerQuestChain     (new, grammar-driven)
         │
    GrammarEngine
    ├── GrammarData      strategies, roles, framings
    ├── ArcCurve         6 tension curves
    ├── CharacterWeb     AI-generated cast
    ├── StageSpec        per-stage specifications
    └── EntityValidator  post-generation grounding check
         │
    enriched Addons → existing IOutlawQuest.Setup() pipeline
```

Both chain types are in the dispatch list. The random selector picks one per run, enabling direct A/B comparison.

## File Inventory

| File | Lines | Purpose |
|------|-------|---------|
| `Core/Grammar/GrammarData.cs` | 160 | Enums, Strategy class, 3 Justice strategies |
| `Core/Grammar/ArcCurve.cs` | 50 | 6 arc shapes, tension/mood sampling |
| `Core/Grammar/StageSpec.cs` | 28 | Per-stage data class |
| `Core/Grammar/CharacterWeb.cs` | 190 | Cast generation, XML parsing, addon formatting |
| `Core/Grammar/GrammarEngine.cs` | 195 | Strategy selection, addon assembly |
| `Core/Grammar/EntityValidator.cs` | 120 | Post-gen entity grounding check |
| `Nouns/Quests/LorewalkerQuestChain.cs` | 240 | Grammar-driven orchestrator |

**~1,000 lines of new code. Zero lines of existing code modified** (beyond adding one line to the dispatch list).

## What's Next

Lorewalker is an MVP testing one motivation (Justice) with three strategies. If it produces better stories:

- **New motivations** (Rescue, Betrayal, Redemption) — each adds strategies and moral framings to the grammar
- **New quest types** (Negotiate, Expose, Defend) — new `IOutlawQuest` implementations for non-combat resolutions
- **Branching** — DAG-shaped quest structures using the existing `Meta_Fork_Exclusive` pattern
- **Action-type template filtering** — grammar constrains which templates are valid for each stage slot
- **Automated evaluation** — LLM-as-Judge scoring against quality rubrics

The full vision is documented in `docs/narrative_design.md`. The implementation plan is in `docs/lorewalker.md`.
