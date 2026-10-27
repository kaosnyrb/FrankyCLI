# Narrative Design: From Bounty Hunts to Story Grammars

A research-backed design vision for restructuring Retrograde's quest generation from a single archetype into a composable grammar of story structures.

---

## 1. Current System Analysis

### The Pipeline

Retrograde generates quest chains through a six-stage pipeline:

1. **Character seeding** — An outlaw NPC is rolled from nine trait dimensions (occupation, crime, motive, personality, fear, quirk, hunted-by faction, goal, preoccupation). These traits are authored seed pools, not AI-generated — the randomization happens in C#, keeping the AI grounded.

2. **Lore generation** — A two-phase AI pipeline builds the narrative foundation. Phase 1 generates a LoreFile from the seed traits. Phase 2 expands it into a full LoreContext with summary, target profile, motives, faction ties, geographic context, and story summary. This context is injected into every subsequent AI call.

3. **Arc planning** — The AI selects quest templates from a menu: one Discovery, two to five Investigations, one Showdown. Each gets a `<Theme>` explaining its narrative role. The full planned arc is injected as silent context so all stage prompts understand the shape of the story.

4. **Backward generation** — Quests are generated from showdown to discovery (reverse narrative order). Each stage gets breadcrumb links to all previous locations via `<QuestStageLocation>` addons. The showdown location is explicitly hidden from earlier stages.

5. **Writing polish** — An iterative refinement pass reviews all generated text as a unified narrative document, identifying and rewriting the weakest pieces across multiple iterations.

6. **Voice staging** — All text content is voiced through ElevenLabs, with audio converted and deployed to the game.

### What Works

The system already solves several problems that the research literature identifies as hard:

- **AI grounding.** Authored seed pools prevent the LLM from drifting into generic fantasy. Every detail traces back to a concrete trait or lore element.
- **Context coherence.** The LoreContext injection pattern ensures all stages reference the same facts. The stateless/stateful prompt distinction prevents template selection from polluting the narrative generation history.
- **Stage-locked knowledge.** The `<QuestProgress>` tag prevents anachronistic reveals — early stages don't reference facts the player hasn't discovered yet.
- **Environmental variety.** 17 template libraries span planets, space, cities, dungeons, derelicts, and spacestations.
- **Emotional payoff.** The outlaw's personal log — found on their body after the showdown — creates a genuine moment of understanding.

### Structural Constraints

Despite the strong foundation, the system is locked into a single story shape:

- **One archetype.** Every quest is a bounty hunt. Every chain follows the same Discovery → Investigation → Showdown arc. The interface signatures, template engine methods, and chain orchestrator all assume this structure.
- **Linear progression.** Stages form a strict chain. The fork system exists as an optional template but isn't a first-class orchestration concept. The player never faces a real decision point.
- **Combat-only resolution.** Both showdown types end with killing the outlaw. There is no dialogue confrontation, no negotiation, no moral choice at the climax.
- **Flat emotional arc.** Quest progress maps linearly from 0% to 90%. There is no concept of setbacks, reversals, or dramatic tension curves.
- **Functional characters.** NPCs serve as information sources. They have a role ("informant at Neon starport") but no agenda, no secret, no reason to lie or withhold.
- **No moral ambiguity.** The outlaw is always guilty. The briefing always tells the truth. The player never has to weigh conflicting accounts or make a judgement call.

These aren't bugs — they're the natural result of building outward from one well-executed archetype. The question is how to open the system up without losing what makes it work.

---

## 2. Research Foundations

Seven bodies of research inform the redesign. Each is presented with its core insight, why it matters, and what it suggests for Retrograde specifically.

### 2.1 Doran-Parberry Quest Grammar

**Paper:** "Towards Procedural Quest Generation: A Structural Analysis of RPG Quests" (Doran & Parberry, 2011)

**Core insight.** After analyzing 750+ quests from four MMORPGs, Doran and Parberry decomposed quest structure into a three-level grammar:

```
Quest = Motivation → Strategy → [Action]
```

**Nine motivations** drive all quests: Knowledge, Comfort, Reputation, Serenity, Protection, Conquest, Wealth, Ability, Equipment. Each motivation decomposes into 2–7 strategies (e.g., Protection → Escort to Safety, Eliminate Threat, Fortify Location). Strategies decompose into sequences of atomic actions: Goto, Kill, Gather, Escort, Deliver, Use, Talk, Defend, Explore.

**Why it matters.** This is a formal grammar for quest structure. It explains *why* radiant quests feel repetitive — they operate at the Action level without motivation or strategy providing meaning. A random "go kill X at Y" is an action without a motivation. "Protect the settlement by eliminating the raider camp" is the same action with a motivation that makes it feel purposeful.

**What it suggests.** Retrograde's existing quest stage types (conversation, activator, destroy, informant, derelict) are already atomic actions in Doran-Parberry terms. What's missing is the grammar layer above them — the motivations and strategies that compose these actions into different story shapes. The bounty hunt is one strategy under the "Conquest" motivation. The same atomic actions could serve Rescue, Protection, Knowledge, or Serenity motivations with different compositions.

### 2.2 Reagan et al. — Emotional Arcs

**Paper:** "The Emotional Arcs of Stories are Dominated by Six Basic Shapes" (Reagan et al., EPJ Data Science, 2016)

**Core insight.** Sentiment analysis of 1,700+ stories from Project Gutenberg revealed that virtually all fiction follows one of six emotional arc shapes:

1. **Rags to Riches** — steady rise (Alice in Wonderland)
2. **Tragedy** — steady fall (Romeo and Juliet)
3. **Man in a Hole** — fall then rise (most common in popular fiction)
4. **Icarus** — rise then fall
5. **Cinderella** — rise-fall-rise
6. **Oedipus** — fall-rise-fall

This computationally confirmed Kurt Vonnegut's 1995 intuition that "there is no reason why the simple shapes of stories can't be fed into computers."

**Why it matters.** The current system maps quest progress linearly: 0% → 20% → 40% → 70% → 90%. This produces a flat ramp. Real stories have curves — setbacks, reversals, false victories, darkest-before-dawn moments. A "Man in a Hole" arc (the most popular shape) would have the player succeeding early, hitting a serious complication mid-chain, then recovering toward a satisfying climax.

**What it suggests.** Each quest chain should target an arc shape. The arc doesn't just label stages — it drives concrete generation decisions: what the player discovers at each stage, whether an investigation succeeds cleanly or reveals a complication, whether informants are helpful or hostile, and how much the player knows going into the showdown. The `<QuestProgress>` tag could carry tension values derived from the arc curve rather than linear percentages.

### 2.3 Quality-Based Narrative (QBN)

**System:** Failbetter Games / StoryNexus (Fallen London, 2009–present)

**Core insight.** In QBN, narrative content exists as free-floating "storylets" — self-contained scenes gated by accumulated "qualities" (stats, flags, relationship values). There is no fixed ordering. Players access whichever storylets they qualify for based on their current state. Content grows "like coral" — new storylets can be added without restructuring existing ones.

As designer Alexis Kennedy described it: "If a team agrees some basic rules — we won't give out more than X of a quality at a time, we won't advance this storyline quality without checking — then it's easy to add storylets into an existing narrative."

**Why it matters.** QBN solves the rigidity problem. The current system's three-act structure (Discovery → Investigation → Showdown) is a fixed pipeline. QBN offers a model where quest stages are selected based on accumulated story state rather than a predetermined sequence. This maps naturally to the Creation Engine: qualities are global variables and keywords, storylets are quest stages with condition checks.

**What it suggests.** Even without cross-chain continuity (out of scope for now), QBN thinking can inform *within-chain* stage selection. Instead of a fixed investigation count, the chain could accumulate narrative qualities (e.g., "evidence gathered", "trust earned", "danger level") and select the next stage based on what the story needs. A chain where the player has gathered strong evidence might skip to the showdown early. A chain where leads keep going cold might extend with additional investigation stages.

### 2.4 PANGeA — Structured LLM Validation

**Paper:** "PANGeA: Procedural Artificial Narrative using Generative AI for Turn-Based Video Games" (Buongiorno et al., AAAI AIIDE, 2024)

**Core insight.** PANGeA wraps LLM narrative generation in a validation pipeline: the AI generates content, a schema validator checks structural correctness, and a repair system fixes violations before the content reaches the game engine. This boosted a small open-source model (Llama-3 8B) from 28% to 98% structural accuracy — rivaling GPT-4 output. NPCs are parameterized with Big Five personality traits, producing consistent characterization.

**Why it matters.** Raw LLM output is structurally unreliable for game engines. Retrograde already handles this with retry loops (regenerate if output is too short) and template-governed prompts. But the validation is primitive — checking character count, not narrative structure. PANGeA shows that structured validation can make even small models produce engine-ready content.

**What it suggests.** As the grammar system grows more complex — with multiple motivations, branching paths, and variable resolutions — the validation layer needs to grow with it. The AI will be making higher-stakes decisions (choosing arc shapes, placing dramatic reversals, selecting resolution types). Each of these decisions should be validated against the grammar's structural constraints before generation proceeds.

### 2.5 Story Sifting

**System:** Felt (Kreminski, UC Santa Cruz, 2019)

**Core insight.** Story sifting is the process of mining narratively interesting patterns from simulation output. Kreminski's Felt system uses composable query patterns to detect narrative structures in event logs — "character who was betrayed later gets revenge," "two friends end up on opposite sides of a conflict." Patterns compose: complex narrative structures are detected by combining simpler building blocks.

Two modes exist: **retrospective sifting** (mine completed event logs after the fact) and **prospective sifting** (detect emerging patterns during generation and nudge the system toward completing them — a drama manager).

**Why it matters.** As quest generation becomes more flexible, the system will produce a combinatorial explosion of possible story shapes. Not all of them will be narratively satisfying. Story sifting provides the quality filter — a way to detect when a generated chain is accidentally producing a compelling pattern ("the informant who lied in stage 2 turns out to be the real antagonist") and lean into it, or detect when a chain is producing narrative noise and steer it back on course.

**What it suggests.** The writing polish pass already does a form of retrospective sifting — it reviews all generated text and identifies the weakest piece. The concept could extend to structural sifting: after generating the stage sequence, evaluate whether the pattern of revelations and reversals matches a known satisfying arc shape. If not, regenerate the weakest structural choice.

### 2.6 Nesmith on Radiant Quests

**Source:** Bruce Nesmith, "Bethesda's Nesmith Reflects on the Difficult Birth of Skyrim's Radiant Story System" (Game Design Expo, 2012)

**Core insight.** The designer of Skyrim's Radiant Story system offered a blunt assessment: "Players only want so many of the 'job quests' that are not advancing the main story." The team "ended up doing many more custom, hand-crafted pieces" than originally planned. Using Radiant Story for the main quest "sounds great, would play horribly."

**Why it matters.** This is the cautionary tale. Pure procedural quests — even well-constructed ones — feel like busywork when they lack narrative progression, consequences, and character development. Players detect the template. The system works for supplementary content but cannot carry a narrative.

**What it suggests.** Retrograde already surpasses basic radiant quests by using AI-generated lore and stage-locked knowledge to create the illusion of authored content. The grammar system should push further: each chain should feel like it's "about something" — not just a target to track, but a story with a thesis. The motivation layer is what provides this. A Revenge quest is about whether vengeance is justified. A Rescue quest is about what you'll sacrifice. A Betrayal quest is about who you can trust. The grammar gives structure; the motivation gives meaning.

### 2.7 Storytron — The Texture Lesson

**System:** Storytron (Chris Crawford, 1992–2018)

**Core insight.** After decades of work on interactive storytelling, Crawford's own assessment of Storytron was that it produced "a repetitive, mechanical feel." The procedural interaction engine generated structurally valid stories, but they lacked the specific, surprising, human details that make authored fiction compelling.

**Why it matters.** This validates Retrograde's existing approach of using authored seed data pools (StorySeedData, NarrativeSeedData, FlavourSeedData) to inject texture at the leaf nodes of procedural structures. Pure grammar produces skeletons. Authored flavor produces flesh.

**What it suggests.** As the grammar system expands to new motivations and strategies, each new story shape needs its own seed pools and prompt tuning. A Rescue motivation needs different speaker types, transmission tones, and log styles than a Betrayal motivation. The grammar layer provides the bones; the seed data provides the voice. Don't expand one without the other.

---

## 3. Story Grammar Architecture

### The Grammar Model

The core proposal is a three-level decomposition inspired by Doran-Parberry but adapted for the constraints and capabilities of the Creation Engine.

**Level 1: Motivation** — Why is the player engaged in this quest chain?

This is the thematic frame. It determines what the story is *about*, what kind of NPCs appear, what moral questions arise, and what resolutions are available. Candidate motivations:

| Motivation | Thesis | Player Role |
|---|---|---|
| **Justice** | Someone committed a crime. Track them down. | Bounty hunter (current system) |
| **Revenge** | Someone wronged you or your client. Find them. | Avenger |
| **Rescue** | Someone is in danger. Find and extract them. | Protector |
| **Betrayal** | Someone you trusted is not who they seem. | Investigator |
| **Redemption** | Someone wants to make amends. Help or hinder them. | Judge |
| **Survival** | The player or an ally is threatened. Eliminate the threat. | Survivor |
| **Discovery** | Something hidden must be uncovered. Follow the trail. | Explorer |
| **Protection** | Something or someone must be defended from a coming threat. | Guardian |
| **Escape** | Someone (possibly the player) needs to get out. | Fugitive/Helper |

Each motivation carries implicit constraints: Rescue motivations can't end with "kill the target" because the target is the person being rescued. Betrayal motivations require at least one NPC who appears trustworthy early and is revealed as treacherous later. These constraints are part of the grammar.

**Level 2: Strategy** — What is the overall shape of this quest chain?

Each motivation decomposes into 2–4 strategies. A strategy is a pattern of stages — not the specific content, but the structural rhythm.

Example strategies for **Rescue**:
- **Race Against Time** — Linear escalation: discover the kidnapping, track the captor, breach the holding site. Tension rises monotonically.
- **Wrong Trail** — False lead midway: the first investigation points to the wrong location. The player must backtrack and find the real trail. Man-in-a-Hole arc.
- **Inside Job** — The "rescuer" learns the victim is complicit. Midway twist reframes everything. Icarus or Oedipus arc.

Example strategies for **Betrayal**:
- **Slow Reveal** — Each investigation stage peels back one layer of deception. The traitor is identified at the showdown. Steady-fall arc.
- **Double Cross** — The quest giver is the betrayer. Midway reversal when the player realizes their fixer is playing them. Oedipus arc.
- **Loyalty Test** — The player must choose between two NPCs, each accusing the other. Fork structure.

The strategy determines: how many stages, where branches occur, what the emotional arc shape is, and what resolution types are available at the showdown.

**Level 3: Action** — What does the player do at each stage?

Actions are the atomic quest stages — the existing `IOutlawQuest` implementations. The grammar composes them into sequences:

| Action | What the Player Does | Existing Implementations |
|---|---|---|
| **Goto** | Travel to a location | All location-based templates |
| **Talk** | Converse with an NPC | Investigation_Conversation*, Investigation_Informant* |
| **Search** | Find an object or clue | Investigation_Activator*, Discovery_Dataslate |
| **Destroy** | Eliminate hostiles | Investigation_Destroy*, Investigation_DestroySetDungeon |
| **Explore** | Investigate an environment | Investigation_Derelict_Space |
| **Confront** | Face the antagonist (dialogue or combat) | Showdown_Bounty* (combat only — needs expansion) |
| **Choose** | Make a decision between paths | Meta_Fork_Exclusive |
| **Rescue** | Extract a friendly NPC | *new — requires implementation* |
| **Defend** | Protect a location or person | *new — requires implementation* |
| **Escape** | Leave under pressure | *new — requires implementation* |

Most of the action vocabulary already exists. The grammar layer composes these existing pieces into new shapes.

### Composition Example

Here's how the grammar composes a complete quest chain:

```
Motivation: Rescue
  Strategy: Wrong Trail (Man-in-a-Hole arc)
    Stage 1: Search    → Discovery_Dataslate (find the distress signal)
    Stage 2: Talk      → Investigation_ConversationCity (witness saw the abduction)
    Stage 3: Goto+Search → Investigation_ActivatorPlanet (follow the lead — WRONG LOCATION)
    Stage 4: Talk      → Investigation_Informant_Space (real intel from a reluctant source)
    Stage 5: Confront  → Showdown_Rescue (breach and extract — new implementation)
```

The same atomic stages, composed differently, produce a fundamentally different player experience than a bounty hunt.

### How the Grammar Talks to the AI

The grammar doesn't replace the AI — it constrains it. Today, the AI selects templates from a flat menu. Under the grammar system:

1. The **motivation** is selected (randomly, or by AI based on seed traits)
2. The motivation constrains the **strategy** pool
3. The strategy defines the **stage sequence pattern** (including where branches and reversals go)
4. Each stage slot specifies an **action type** and environmental constraints
5. The AI fills each slot by selecting from templates that match the action type
6. The AI generates narrative content within the constraints established by the motivation and strategy

The key insight: the grammar handles structure, the AI handles texture. Neither does both.

---

## 4. Emotional Arc Shaping

### Arcs as Generation Parameters

Each strategy in the grammar is paired with a target emotional arc from Reagan et al.'s six shapes. The arc is not decorative — it drives concrete generation decisions at every stage.

**What the arc controls:**

| Generation Aspect | How the Arc Shapes It |
|---|---|
| **Revelation pacing** | Man-in-a-Hole: player learns key facts early, then a discovery undermines them mid-chain. Icarus: player accumulates advantages, then loses them all at once. |
| **NPC cooperation** | Rising arc: informants are helpful early, hostile late. Falling arc: early allies abandon the player. |
| **Stage outcomes** | In a setback point on the arc, the investigation stage "fails" — the lead was wrong, the informant lied, the evidence was planted. The player must recover. |
| **Tension descriptors** | The `<QuestProgress>` addon tag carries not just completion percentage but a tension value derived from the arc curve. Writing prompts use this to calibrate tone. |
| **Showdown framing** | Man-in-a-Hole rises to a triumphant resolution. Tragedy falls to a pyrrhic one. Oedipus ends with an ironic reversal. |

### Arc-Shaped Stage Sequencing

Instead of linear 0% → 90% progress, map the arc curve to stage tension:

**Man in a Hole (fall-rise):**
```
Stage:     Discovery → Inv 1 → Inv 2 (SETBACK) → Inv 3 → Showdown
Tension:   0.3        0.5      0.8 (peak danger)   0.6     0.9 (triumph)
Mood:      hopeful    focused  desperate             recovering  resolved
```

**Icarus (rise-fall):**
```
Stage:     Discovery → Inv 1 → Inv 2 (PEAK) → Inv 3 (FALL) → Showdown
Tension:   0.2        0.4      0.3 (false ease)  0.7 (reversal)  0.9 (pyrrhic)
Mood:      curious    confident  triumphant       panicked         hollow victory
```

The tension value at each stage feeds into prompt generation as a `<Tension>` addon, guiding the AI's tone, NPC behavior descriptions, and narrative content.

### Setback Mechanics

The most important arc concept is the **setback** — a stage where things go wrong. Currently, every investigation stage is a step forward. Arcs require some stages to be steps backward:

- **Wrong lead** — the investigation points somewhere, but it's a trap or dead end
- **Betrayal** — an informant feeds the player false information
- **Escalation** — the antagonist becomes aware of the player and acts first
- **Loss** — an ally NPC is captured, killed, or turns hostile
- **Revelation** — the player discovers the situation is worse than they thought

These aren't failures in the game-mechanical sense (the quest still advances). They're narrative setbacks that change the emotional register of subsequent stages. The player knows more, but the situation is worse.

---

## 5. Character & Moral Complexity

### Character Webs

The current system generates one character: the outlaw target. Every other NPC is functional — "informant," "guard," "contact." They exist to deliver information and disappear.

The grammar system should generate a **character web** for each quest chain: a small cast of 3–5 characters with relationships, secrets, and agendas.

**Core cast roles:**

| Role | Current System | Proposed |
|---|---|---|
| **Target** | OutlawNpc (always guilty, always killed) | The central figure — may be guilty, framed, sympathetic, or complex |
| **Fixer** | Implicit (mission briefing dataslate) | A named character with their own angle on the situation |
| **Witness** | Generic speaker type | Someone who saw something — but filtered through their own bias |
| **Complicator** | Does not exist | An NPC whose presence makes the situation harder (rival hunter, the target's family, a corrupt official) |
| **Informant** | Generic NPC background | Someone with specific knowledge and a specific reason to share or withhold it |

Each character gets:
- **A relationship to the target** — not just "knows them" but a specific connection (former partner, betrayed colleague, estranged family, debt holder)
- **A secret** — something they know but won't share without pressure or trust (the target's real location, the truth about what happened, their own involvement)
- **An agenda** — what they want from the interaction (get the player to do their dirty work, protect the target, extract a favor, redirect blame)

These character properties feed into the existing prompt infrastructure. The `NpcBackground` field on `MissionTemplate` already accepts character descriptions — it just needs richer input.

### Moral Ambiguity

Not every target should be guilty. Not every briefing should tell the truth. The grammar system should support multiple moral framings:

| Framing | What Happened | Player Experience |
|---|---|---|
| **Clear guilt** | Target committed the crime as described | Current system — satisfying hunt, clean resolution |
| **Guilty but justified** | Target committed the crime, but for sympathetic reasons | Player learns the target's motives and must decide how they feel |
| **Framed** | Target didn't do it — someone else is responsible | Midway twist; the real antagonist is someone the player already met |
| **Complex past** | Everyone involved made bad choices; no clear villain | Morally gray — resolution depends on who the player believes |
| **Sympathetic fugitive** | Target is genuinely trying to escape a bad situation | Player can choose to help, capture, or abandon them |

The moral framing is a property of the quest chain, selected alongside the motivation and strategy. It constrains what information the LoreFile generates, what NPCs say, and — critically — what resolution types are available at the showdown.

### Multiple Resolution Types

The showdown is where moral complexity pays off. Currently: kill the outlaw. The grammar system should support:

| Resolution | Player Action | When It Fits |
|---|---|---|
| **Kill** | Combat encounter | Clear guilt, Protection, Conquest motivations |
| **Capture** | Non-lethal takedown | Justice motivation, player wants due process |
| **Negotiate** | Dialogue confrontation | Redemption, sympathetic fugitive |
| **Expose** | Present evidence to a third party | Betrayal, framed target (the real villain is someone else) |
| **Release** | Let the target go | Guilty-but-justified, sympathetic fugitive |
| **Rescue** | Extract the target from danger | Rescue motivation |
| **Escape** | Help the target (or yourself) flee | Escape motivation |

Not every quest chain needs every resolution. The grammar constrains which resolutions are available based on the motivation, moral framing, and what the player has learned. A chain where the player discovers the target was framed shouldn't end with "kill the target" as the only option — the "expose the real villain" resolution should unlock.

### Unreliable Narrators

When characters have agendas and secrets, they lie. This is a feature, not a bug.

The fixer's briefing might omit key context. An informant might exaggerate. A witness might genuinely misremember. A dataslate might contain the target's own self-serving account.

The system can generate these contradictions by giving each narrative prompt a **bias tag** alongside the existing speaker type. The bias doesn't make the content false — it makes it perspectival. The player pieces together the truth from multiple biased accounts.

This turns the investigation stages from "collect the next breadcrumb" into "figure out what actually happened" — a fundamentally more engaging player experience.

---

## 6. Multi-Path Structures

### From Chains to Graphs

The current system generates a linear chain: Discovery → Investigation₁ → Investigation₂ → ... → Showdown. Each stage has exactly one predecessor and one successor.

The grammar system should support quest structures as **directed acyclic graphs (DAGs)** — stages can branch and (optionally) reconverge.

### Branch Types

**Fork (Exclusive)** — The player chooses one of two paths. The other path's content still exists in the world but is not required. This is already implemented in `Meta_Fork_Exclusive`.

```
Discovery → Investigation₁ → Fork → Investigation₂a (path A)
                                   → Investigation₂b (path B) → Showdown
```

**Fork (Inclusive)** — Both paths are available. The player can do one or both. More evidence gathered = different showdown context.

```
Discovery → Investigation₁ → Investigation₂a (optional)
                            → Investigation₂b (optional) → Showdown
```

**Convergence** — Two paths that were separated earlier come back together. The showdown plays differently depending on which path(s) the player completed.

```
Fork → Path A → Showdown (variant A)
     → Path B → Showdown (variant B)
     → Both   → Showdown (variant C — fullest picture)
```

### When Branches Occur

The grammar's strategy layer determines where branches appear. Not every chain needs them:

- **Linear strategies** (Race Against Time, Slow Reveal): no branches. Tension comes from pacing, not choice.
- **Fork strategies** (Loyalty Test, Wrong Trail): one branch point, typically at the midway setback.
- **Web strategies** (Complex Past): multiple optional investigation paths, all feeding into a convergent showdown.

### Conflicting Information at Branch Points

When a chain branches, the two paths should present **contradictory accounts** of the same events. This is the narrative payoff of branching — the player isn't just choosing where to go, they're choosing whose version of the story to believe.

The AI prompt for each branch path receives the same LoreContext but a different `<Perspective>` addon — one NPC's account versus another's. The writing polish pass can then verify that the two accounts diverge on the right points (the contested facts) while agreeing on the right points (established lore).

### Mapping DAGs to Creation Engine Quests

Each node in the DAG is a Creation Engine quest. Edges are quest-to-quest links (the existing `nextQuest` script property pattern). Branches are implemented via the message-box choice pattern already used by `Meta_Fork_Exclusive`. Convergence requires the showdown quest to check which predecessor quests were completed and adjust its script behavior accordingly — this is straightforward with Papyrus `GetStageDone` conditions.

The backward-generation strategy (showdown first, then predecessors) still works for DAGs — generate the showdown first, then generate each path that leads to it, then generate the branch point that splits into those paths.

---

## 7. LLM Grounding: Keeping the AI on the Rails

The grammar system pre-defines the story elements — characters, locations, facts, relationships, arc shape. The LLM's job is to *connect the dots*: write the prose, dialogue, and transitions that weave those elements together. It should never invent new characters, locations, or facts.

This is a well-studied problem. The research points to a layered defense: good prompts reduce hallucination, structured output catches structural drift, and post-generation validation catches everything else.

### 7.1 The Core Principle: Selective Grounding

The single most important finding across the literature: **provide only the relevant context, not the entire world bible.**

RAG-based game dialogue systems (tested in 2D RPGs and NPC dialogue engines) found that quality scores of 4.0–4.5 out of 5 on coherence and persona consistency are achievable — but only when the grounding context is focused. Dumping the entire lore database into every prompt *decreases* quality. The model gets confused by irrelevant details and starts cross-referencing facts that don't belong together.

The practical rule: for each generation call, assemble only the story elements relevant to that specific scene. A dialogue prompt for an informant NPC should include that NPC's bio, their relationship to the target, the facts they know, and the current stage's tension level — not the full LoreContext, not the showdown details, not other NPCs' secrets.

Retrograde already practices a form of this with the `Addons` list and `NpcBackground` field on `MissionTemplate`. The grammar system should formalize it: each stage's generation context is assembled from the character web and story graph, filtered to only what that stage's NPCs would know.

### 7.2 Prompt Architecture for Faithful Generation

Research identifies four concrete prompt patterns that reduce hallucination in creative content:

**Explicit entity lists with negative constraints.** Name every entity the LLM is allowed to reference, then explicitly forbid inventing new ones. Retrograde already does this ("Use the LoreContext for concrete names. Do not invent names."). The grammar system should extend this to a full entity manifest per generation call:

```
Characters in this scene: Riko Reyes (target), Marcus Venn (informant)
Locations the speaker knows about: Gagarin industrial sector, Neon starport
Facts the speaker knows: Riko falsified safety reports, was last seen near Gagarin
Facts the speaker does NOT know: Riko's current location, the fixer's identity

Use ONLY the characters, locations, and facts listed above.
Do NOT introduce any character, location, or fact not in this list.
```

**Chain-of-thought for factual planning.** Before writing prose, ask the model to list which pre-defined elements it will reference. This forces a planning step that catches hallucinations *before* they enter the prose. The pattern:

```
Step 1: List which characters and facts from the context above you will reference.
Step 2: Write the dialogue.
```

Research on structured output (EMNLP 2025) found that a "scratchpad" area for reasoning plus a validated "final answer" field produces the best balance of quality and faithfulness.

**Few-shot examples.** Show the model what correct output looks like — a dialogue that uses only provided entities, a prose transition that references only established facts. This is more effective than rules alone because it demonstrates the *pattern* of constraint adherence.

**Output templates.** Provide a structural skeleton the LLM fills in, rather than asking for free-form generation. The existing `<QuestStage>` / `<QuestProgress>` / `<QuestStageLocation>` XML addon pattern is already a form of this. The grammar system should extend it to narrative content: provide the structure (who speaks, in what order, referencing what), let the LLM provide the words.

### 7.3 Sampling Parameters

Temperature and sampling strategy directly affect the faithfulness-creativity tradeoff:

| Parameter | Setting | Effect |
|---|---|---|
| **Temperature** | 0.5–0.7 | Best balance for narrative content. Lower = more faithful but robotic. Higher = more creative but more hallucination. |
| **Min-p** | 0.05–0.1 | Outperforms top-p (ICLR 2025). Dynamically truncates low-probability tokens based on model confidence. Allows higher temperature (more stylistic variety) while still cutting off hallucination tokens. |
| **Top-p** | 0.9 | Standard fallback if min-p is unavailable. |

The key insight from min-p research: it lets you decouple *style* from *faithfulness*. You can have creative, varied prose without the model inventing facts, because min-p cuts off the low-confidence tokens that typically produce hallucinations while preserving the high-confidence stylistic variations.

For factual decisions (which character to reference, which location to name), use lower temperature. For prose quality (how to phrase a sentence), allow higher temperature with min-p truncation.

### 7.4 Post-Generation Validation

The highest-value technique for the "don't invent new things" requirement. After each LLM generation:

1. **Entity extraction** — Parse all proper nouns, character names, location names, and faction references from the output
2. **Database check** — Compare each extracted entity against the predefined entity set for this generation context
3. **Flag or reject** — Any entity not in the allowed set is a hallucination

This is deterministic, fast, and catches the most common failure mode. It can be implemented as simple string matching against the entity list — no second LLM call needed.

Retrograde's current validation is primitive: retry if the output is too short. The grammar system should add entity validation as a standard pipeline step. The character web and story graph provide the entity database; extraction is regex or simple NER; validation is set membership.

**What entity validation catches:**
- Invented character names ("Detective Sarah Chen" when no such character exists)
- Fabricated locations ("the abandoned refinery on Volii Alpha" when the scene is set elsewhere)
- Made-up faction names or organizational details
- Facts contradicting the provided context

**What entity validation doesn't catch:**
- Tone violations (too dramatic, too casual)
- Subtle factual contradictions (correct entities but wrong relationships)
- Structural issues (dialogue too long, wrong number of exchanges)

For these subtler issues, a second-pass LLM verification call is warranted — but only after deterministic validation passes.

### 7.5 The Self-Critique Trap

A tempting approach: ask the LLM to check its own output. "Review the dialogue above. Does it introduce any characters not in the provided list?"

Research shows this **does not work reliably without external grounding.** Huang et al. (2024) found that without external feedback, LLM self-critique consistently *decreased* accuracy. The model amplifies its own biases — if it hallucinated a character name, it will rationalize why that name was appropriate.

Self-critique only works when combined with external tool verification (the CRITIC framework). In practice: the LLM can be used as a *second pass verifier* with a *different prompt* that explicitly lists the allowed entities and asks "does this text only reference these entities?" — but it should never be the only line of defense. Deterministic entity validation (Section 7.4) catches what self-critique misses.

### 7.6 The Generation Pipeline

Combining all techniques into a practical pipeline for the grammar system:

```
1. ASSEMBLE CONTEXT
   - From character web: select only NPCs present in this scene
   - From story graph: select only facts this stage's NPCs would know
   - From arc shape: derive tension level and mood for this stage
   - Build explicit entity manifest (names, locations, facts allowed)

2. CONSTRUCT PROMPT
   - System: role + task definition + entity manifest + negative constraints
   - Context: assembled story elements (focused, not full world bible)
   - Planning: "First list which elements you will reference, then write"
   - Schema: output template with structural skeleton

3. GENERATE
   - Temperature: 0.5–0.7
   - Min-p: 0.05–0.1 (if available)
   - Constrained decoding for entity names (if supported by provider)

4. VALIDATE (deterministic)
   - Extract all entity references from output
   - Check against entity manifest — reject any hallucinated entities
   - Check structural constraints (length, format, required elements)

5. VERIFY (optional LLM second pass)
   - Different prompt: "Given these elements [list], does this text
     introduce anything not in the list? Contradict any stated facts?"
   - Only runs if deterministic validation passes

6. ACCEPT or RE-PROMPT
   - If validation fails: re-prompt with specific corrections
     ("You referenced 'Detective Chen' but this character doesn't exist.
      The informant's name is 'Marcus Venn'. Regenerate using only
      the provided characters.")
   - Do NOT just retry the same prompt — targeted correction > blind retry
```

This pipeline is an evolution of what Retrograde already does. The LoreContext injection is step 1. The Addons system is step 2. The retry loops are a primitive version of step 6. The grammar system formalizes and strengthens each step.

### 7.7 What This Means for the Grammar System

The grammar's role in grounding becomes clear: **the grammar pre-computes the story, the LLM writes it.**

Before any LLM call fires:
- The motivation, strategy, and arc shape are already chosen
- The character web is already generated (names, relationships, secrets, agendas)
- The stage sequence is already determined (which actions, in what order, with what tension)
- Each stage knows which characters are present and what facts are in play

The LLM receives all of this as structured context and writes the human-readable output — dialogue, prose, log entries, dataslate text. It connects the dots that were already placed. It doesn't decide *what happens*; it decides *how to say what happens*.

This is the opposite of the current system's approach, where the LLM helps select templates and plan the arc (via `PlannedArc` selection). In the grammar system, those decisions are made by the grammar engine. The LLM's creative freedom is channeled entirely into prose quality — word choice, emotional register, conversational rhythm, environmental description. The things humans are bad at automating and LLMs are good at.

---

## 8. Dialogue Systems: Making NPCs Sound Like People

The grammar system gives NPCs agendas, secrets, and biases. But those traits are only as good as the dialogue that expresses them. An NPC who is "protecting a secret" needs to actually *sound* like someone protecting a secret — evasive without being obviously scripted, helpful on safe topics, subtly wrong on dangerous ones.

### 8.1 Persona Grounding

Research on persona-consistent dialogue (PersonaChat, the LIGHT dataset for fantasy RPGs, MCPDial for Minecraft) converges on a clear finding: **3–5 persona sentences is the sweet spot.** Enough to constrain behavior, not so much that the model gets overwhelmed and starts contradicting itself.

For agenda-driven NPCs, the persona block should include:
- Who they are (role, relationship to the target)
- What they want from this conversation (their agenda)
- What they know but won't share (their secret, with unlock conditions)
- Their current emotional state

This maps directly to the character web. Each NPC's generation context is assembled from their web entry, not from a generic role description.

### 8.2 Dialogue Acts as Behavioral Constraints

Speech act theory provides a vocabulary for what NPCs *do* with their words, beyond what they *say*:

| Dialogue Act | When an NPC Uses It |
|---|---|
| **Inform** | Reveal facts they want the player to know |
| **Deflect** | Redirect away from dangerous topics |
| **Withhold** | Omit information without actively lying |
| **Deceive** | Provide false information |
| **Persuade** | Push the player toward the NPC's desired outcome |
| **Request** | Ask the player for something |

The key insight: instead of telling the LLM "this NPC is lying," specify which dialogue acts are permitted per exchange. "In this exchange: inform about the market freely. If asked about the shipment, deflect to complaints about docking fees. Withhold all knowledge of the theft." This produces more controllable, more natural-sounding deception than vague personality instructions.

The KNUDGE dataset (built from 159 dialogues across 45 side quests in *The Outer Worlds*) confirmed that LLMs struggle with *precision of information delivery* — they either over-reveal (spoiling quest details) or under-deliver (being vague when specifics matter). The solution is **explicit per-turn fact gating**: lists of what may and must not be revealed in each exchange. "Keep the secret" is empirically insufficient.

### 8.3 Grice's Maxims and the Art of Lying Well

Philosopher Paul Grice identified four conversational maxims that cooperative speakers follow: Quality (be truthful), Quantity (give the right amount of information), Relevance (stay on topic), and Manner (be clear). When someone violates these maxims, listeners sense "something is off" — even before they can articulate what.

This is the theoretical backbone for NPC deception:

| NPC Behavior | Maxim Violated | Player Experience |
|---|---|---|
| NPC lies outright | Quality | May not notice immediately |
| NPC gives excessive detail on an irrelevant topic | Quantity + Relevance | Senses misdirection |
| NPC answers a different question than asked | Relevance | Senses evasion |
| NPC is vague when precision would be natural | Manner | Senses withholding |
| NPC overshares to appear trustworthy | Quantity | May be lulled or suspicious |

The prompt engineering implication: instead of "this NPC is lying about the murder," instruct the LLM to "violate the maxim of Relevance by redirecting to complaints about the weather when asked about the murder." This produces dialogue that feels naturally evasive rather than robotically deceptive.

**Critical distinction — flouting vs. violating:** When a speaker openly breaks a maxim (sarcasm, obvious evasion), both parties know it's happening. That's *flouting*. When a speaker secretly breaks a maxim, intending the listener not to notice, that's *violating*. NPCs protecting secrets should *violate* maxims. NPCs with attitude should *flout* them. The grammar should specify which.

### 8.4 Consistency Over Cleverness

A CHI 2024 study ("Lies, Deceit, and Hallucinations") tested player perception of NPC deception in a game where NPCs made false statements. The findings are counterintuitive:

- Players detected deception primarily through **contradiction and inconsistency**, not through "reading" tone
- If an NPC consistently lied, players attributed it to narrative design (a character trait)
- If a trustworthy NPC lied once, players assumed it was a **software bug**
- Perceived-intentional false statements were attributed to the story; unintentional-seeming ones broke immersion

The implication: NPCs who lie must lie **consistently and with pattern**. Every deception should serve the NPC's agenda. When the player finally discovers the truth, every lie should click into place. Random, meaningless falsehoods read as bugs, not storytelling.

### 8.5 Emotional State as a Modifier

An NPC's emotional state affects how well they execute their dialogue acts. A composed NPC deflects smoothly. A stressed NPC deflects clumsily — they hesitate, over-explain, contradict themselves.

This creates a gameplay loop: the player can probe an NPC's emotional state to find cracks in their facade. Model this as a simple state variable that modifies the prompt:

- **High composure:** "Deflect smoothly. Give a plausible alternative explanation without hesitation."
- **Low composure:** "Deflect with visible difficulty. The NPC starts to answer honestly, catches themselves, and redirects — but the hesitation is noticeable."

The emotional state can be derived from the arc's tension value at the current stage. NPCs in high-tension stages are harder to read (everyone is stressed). NPCs in low-tension stages who are stressed stand out — something is wrong.

### 8.6 The Two-Layer Dialogue Model

Pulling it all together, each NPC dialogue generation should work in two layers:

**Surface layer** — what the NPC says. Controlled by:
- Permitted dialogue acts per exchange
- Explicit fact gates (may reveal X, must not reveal Y)
- Gricean violation instructions (which maxim to break, how)
- Emotional state modifier (composure level)

**Subtext layer** — what a perceptive player might notice. This could be:
- Narrator-voice observations ("She pauses a beat too long before answering")
- Skill-check gated hints (if the player character is perceptive, they notice the evasion)
- Contradictions with earlier testimony that the player can recall

The surface layer is what the NPC voice-acts. The subtext layer is environmental — log entries, internal monologue, or companion reactions that the system can generate separately.

---

## 9. Voice Performance: Making NPCs Sound Like They Mean It

The system already voices everything through ElevenLabs. But the current approach is flat: generate text, voice it. The text carries all the emotional weight; the voice just reads it aloud.

Research into emotional speech synthesis, vocal deception cues, and ElevenLabs' v3 capabilities suggests a much richer pipeline where the voice itself communicates character state.

### 9.1 ElevenLabs v3 Audio Tags

The most directly actionable finding. ElevenLabs' Eleven v3 model introduced **audio tags** — bracketed text cues interpreted as performance direction rather than spoken text. They function like stage directions for a voice actor.

| Category | Example Tags | Use Case |
|---|---|---|
| Emotional states | `[nervous]`, `[frustrated]`, `[calm]`, `[excited]` | Base NPC emotional state |
| Reactions | `[sigh]`, `[laughs]`, `[gulps]`, `[gasps]`, `[whispers]` | Non-verbal vocalizations |
| Cognitive beats | `[pauses]`, `[hesitates]`, `[stammers]`, `[resigned tone]` | Deception cues, uncertainty |
| Pacing | `[rushed]`, `[slows down]`, `[deliberate]` | Urgency, tension |
| Volume | `[WHISPERING]`, `[SHOUTING]`, `[QUIETLY]` | Situational context |
| Emphasis | `[emphasized]`, `[understated]` | Drawing attention to key info |

Tags can be combined within a single line. A lying informant's line could be generated as:

```
[nervous] [hesitates] I... [stammers] I don't know anything about that.
[gulps] Why would you even ask me?
```

Versus the truthful version of the same information:

```
[calm] [deliberate] I don't know anything about that. Why would you even ask me?
```

Same words. Completely different player experience.

**Caveat:** Results are non-deterministic. Generating 2–3 takes per critical line and selecting the best is recommended. For background dialogue, single takes suffice.

### 9.2 Voice Settings as Character Parameters

Beyond audio tags, ElevenLabs exposes three API parameters that should be set per-NPC, not globally:

| Setting | Low Value | High Value | Character Mapping |
|---|---|---|---|
| **Stability** (0–1) | Broader emotional range, more variation | Monotonous, controlled | Panicked fugitive: 0.30. Cold liar: 0.65. |
| **Similarity** (0–1) | More creative interpretation | Closer to original voice | Keep consistent per character |
| **Style Exaggeration** (0–1) | Neutral delivery | Amplified performance | Increase for dramatic showdown moments |

A desperate NPC at low stability sounds genuinely unstable — their voice wavers, cracks, shifts. A controlled deceiver at high stability sounds measured and calm, which is its own kind of unsettling.

### 9.3 How Liars Sound Different

Research into vocal deception cues (Paul Ekman's work and subsequent speech analysis studies) identifies consistent markers:

| Vocal Cue | What Happens | Why |
|---|---|---|
| Increased pauses | Longer, more frequent, especially at turn starts | Cognitive load of constructing a lie |
| Speech errors | More "uh," "um," repetitions, partial words | Divided attention |
| Elevated pitch | Average pitch rises | Physiological arousal (anxiety) |
| Narrower pitch range | Less melodic variation | Attempting to control voice suppresses natural variation |
| Variable pace | Simple lies faster (rehearsed); complex lies slower | Thinking while talking |

The critical nuance: these cues indicate **stress**, not lying per se. A truthful person under interrogation shows the same patterns. What makes it deception-specific is the **mismatch** between stated emotion and vocal delivery — claiming to be calm while the voice shows stress markers.

For the grammar system, this means the text generation and voice performance layers should work together:

- **Good liar (high composure):** Text is smooth and confident. Voice uses moderate stability (0.50–0.60). One subtle `[pauses]` where there shouldn't be one. The player feels "something is off" without being able to pinpoint it.
- **Bad liar (low composure):** Text includes self-corrections and fragments. Voice uses low stability (0.30–0.40) with `[stammers]`, `[hesitates]`, `[nervous]` tags. The deception is more visible.
- **Truthful but stressed:** Text is direct but terse. Voice uses low stability with `[tense]` but no hesitation tags. Stressed about the situation, not about being caught.

### 9.4 Text as Performance Notation

The dialogue generation LLM should treat punctuation as prosody control:

| Technique | Example | Voice Effect |
|---|---|---|
| Ellipses | `I thought we were... safe.` | Trailing pause, uncertainty |
| Em dashes | `The shipment was — look, you can't tell anyone.` | Abrupt break, topic shift |
| Comma clusters | `Well, I suppose, if you must know...` | Hesitant, reluctant |
| Short fragments | `No. Not there. Somewhere else.` | Clipped, tense |
| CAPS (sparring) | `You have NO idea what they'll do.` | Emphasis on key word |

Combined with audio tags, this gives two layers of vocal control: punctuation handles prosodic contour (the rhythm and melody of speech), audio tags handle emotional coloring and non-verbal sounds.

The dialogue generation prompt should instruct the LLM to write *for voice performance*, not for reading. This is already partially implemented — the outlaw log prompt specifies "write it for voice performance, not for reading" and instructs use of `[sighs]`, `[whispers]`, ellipses, and em dashes. The grammar system should extend this to all NPC dialogue, calibrated by the character's emotional state and dialogue act constraints.

### 9.5 The Voice Pipeline

Putting it together for the grammar system:

```
1. DIALOGUE GENERATION (LLM)
   - Input: NPC persona, dialogue acts, fact gates, emotional state, tension level
   - Output: Text with embedded audio tags and prosodic punctuation
   - A nervous liar gets [hesitates], [stammers], ellipses, self-corrections
   - A cold professional gets [deliberate], [calm], short declarative sentences

2. VOICE CONFIGURATION (per-NPC)
   - Map NPC archetype + emotional state → Stability/Similarity/Style values
   - Store as part of the character web, not as global settings

3. AUDIO GENERATION (ElevenLabs v3)
   - Use Instant Voice Clones (IVC), not Professional Voice Clones (PVC)
     (v3 audio tags work better with IVC)
   - Generate 2–3 takes for critical lines (showdown dialogue, key reveals)
   - Single take for ambient/background lines

4. POST-PROCESSING
   - Convert to WEM via existing SpeechTools pipeline
   - Deploy keyed by DialogTopic FormKey (existing pattern)
```

The key shift: voice performance is no longer an afterthought that happens after all text is generated. It's a first-class consideration that shapes how the text is written in the first place.

---

## 10. From Grammar to Game Records: How It Actually Builds

The grammar (Section 3) describes the design vision. The LLM grounding (Section 7) describes how to keep the AI honest. This section describes the bridge between them: how the grammar's decisions flow through the existing MissionTemplate pipeline to produce real Creation Engine quest records.

### 10.1 The Current Pipeline (What Exists)

Today, a quest chain is built by five collaborating systems:

**MissionTemplate** — A data container that describes one quest variant. It holds:
- `Name` — e.g., "Planetside Smallbase Informant - Trade Authority Broker"
- `Location` — where the objective takes place
- `outlawQuest` — the `IOutlawQuest` implementation that knows how to build this quest type
- `parameters` — a `Dictionary<string, object>` carrying type-specific configuration (NeedSpacesuit, FormId, Label, Outfit, ExtraLore, etc.)
- `Addons` — a `List<string>` of context lines that get appended to every AI prompt for this stage
- `NpcBackground` — character description fed into dialogue generation
- `MissionTags` — descriptive tags like "follow_clue", "kill_target"

**TemplateLib** — A two-tier registry. Templates are organized into weighted groups (e.g., all city investigation variants at weight 1.0). Selection is two-stage: pick a group probabilistically by weight, then pick a random template within that group. `PickAndRemove()` prevents the same template appearing twice in one chain.

**TemplateEngine** — The selection strategy. `RandomTemplateEngine` does blind weighted picks. `AI_TemplateEngine` additionally filters out all templates sharing a mission type prefix after each pick (so you can't get two "Planetside Smallbase Informant" variants). Both support named lookup as a fallback (used when `PlannedArc` pins a specific template).

**IOutlawQuest.Setup()** — The implementation method that does the actual work. It receives `(StarfieldMod, OutlawNpc, MissionTemplate, IOutlawQuest nextQuest)` and:
1. Creates in-game records (Quest, PlacedObjects, Activators, NPCs) via Mutagen
2. Generates text via AI prompts, passing `missionTemplate.Addons` as context
3. Wires script properties to link this quest to `nextQuest`
4. Stores `LogMessage` and `QuestLocation` for use by subsequent stages

**LoopingLayoutQuestChain** — The orchestrator. Builds the stage list, assigns `<QuestStage>` and `<QuestProgress>` addons, injects `<QuestStageLocation>` history, then calls `Setup()` on each stage in reverse narrative order (showdown → investigations → discovery). After each `Setup()`, it injects the stage's log message into the AI conversation history so subsequent stages have narrative context.

### 10.2 How the Addons System Carries Context

Addons are the primary channel for passing grammar-level decisions into AI prompts. Every prompt in the system ends with `"Additional Information:\r\n"` followed by the Addons list concatenated as strings. This is how structural decisions become narrative context.

Currently, Addons carry:

```xml
<QuestStage>DeepInvestigation</QuestStage>
<QuestProgress>70%</QuestProgress>
<QuestStageLocation stage="Discovery">A small outpost near Gagarin</QuestStageLocation>
<QuestStageLocation stage="InitialInvestigation">Neon starport</QuestStageLocation>
```

Under the grammar system, Addons would additionally carry:

```xml
<Motivation>Rescue</Motivation>
<Strategy>Wrong Trail</Strategy>
<EmotionalArc>Man in a Hole</EmotionalArc>
<Tension>0.8</Tension>
<Mood>desperate</Mood>
<StageRole>SETBACK — this lead was wrong, the player must recover</StageRole>
<MoralFraming>Sympathetic fugitive</MoralFraming>

<CharacterInScene name="Marcus Venn" role="Informant">
  Relationship: former colleague of the target. Secret: knows the target's
  real location but won't share it without pressure. Agenda: wants the player
  to deal with the people who kidnapped the target, not the target themselves.
  Emotional state: nervous, composure 0.3.
  Dialogue acts: Inform freely about the kidnapping. Deflect if asked about
  the target's current location. Withhold knowledge of the real captors.
</CharacterInScene>

<EntityManifest>
  Characters: Marcus Venn, Riko Reyes (target, absent)
  Locations: Deimos Transfer Station, Gagarin industrial sector
  Factions: Trade Authority, UC Security
  Do NOT reference any character, location, or faction not in this list.
</EntityManifest>
```

The Addons system is already the right architecture — it's a flexible context injection mechanism that doesn't require changing prompt code. The grammar system makes the context richer, not the mechanism different.

### 10.3 How the Parameters Dict Evolves

The `parameters` dictionary on MissionTemplate currently carries environment-specific configuration: `NeedSpacesuit`, `FormId` (PCM keyword), `Label` (marker lookup), `Outfit`, `ExtraLore`, `IsTargetDead`, `NpcNameHint`.

Under the grammar system, parameters would additionally carry grammar-level configuration:

| Key | Type | Purpose |
|---|---|---|
| `Motivation` | `string` | "Rescue", "Betrayal", etc. — the grammar's top-level selection |
| `Strategy` | `string` | "Wrong Trail", "Double Cross", etc. |
| `ArcShape` | `string` | "ManInAHole", "Icarus", etc. |
| `StageRole` | `string` | "SETBACK", "REVELATION", "ESCALATION", "RESOLUTION" |
| `MoralFraming` | `string` | "ClearGuilt", "Framed", "SympatheticFugitive" |
| `ResolutionType` | `string` | "Kill", "Negotiate", "Expose", "Rescue" |
| `CharacterWeb` | `object` | Reference to the chain's character web for this stage |

These parameters don't change how `Setup()` creates game records — they change the context that flows into AI prompts. A `Showdown_BountyPlanet` with `ResolutionType = "Kill"` creates the same quest structure as one with `ResolutionType = "Negotiate"`, but the generated dialogue, log entries, and NPC behavior descriptions are completely different because the prompts receive different context.

### 10.4 What Changes vs. What Stays

The grammar system is designed to layer on top of the existing infrastructure, not replace it:

**Stays the same:**
- `MissionTemplate` as the data container
- `IOutlawQuest.Setup()` as the method that creates game records
- All existing `IOutlawQuest` implementations (Investigation_ConversationCity, Investigation_ActivatorPlanet, Showdown_BountyPlanet, etc.)
- The `Addons` mechanism for injecting context into prompts
- The `parameters` dictionary for environment-specific config
- The backward generation pattern (showdown → discovery)
- The writing polish pass and audio staging

**Changes:**
- **Template selection** moves from flat weighted picks to grammar-constrained picks. The grammar's strategy defines which *action types* are needed at each slot; the template engine selects templates matching that action type, filtered by environment constraints.
- **The orchestrator** (currently `LoopingLayoutQuestChain`) is replaced by a grammar-driven orchestrator that reads the strategy's stage pattern — including where branches and setbacks go — and builds the stage list accordingly.
- **Addons become richer.** The grammar pre-computes motivation, tension, mood, stage role, moral framing, character web entries, and entity manifests. These flow through Addons into prompts.
- **New `IOutlawQuest` implementations** are needed for new action types (Rescue, Defend, Escape) and new showdown types (Negotiate, Expose). These follow the same `Setup()` pattern as existing implementations.
- **New `Templates_*.cs` files** are needed for new motivations and action types. These follow the same pattern as existing template libraries.

### 10.5 The Grammar-Driven Orchestrator

The grammar orchestrator replaces `LoopingLayoutQuestChain` with a more general process:

```
1. SELECT MOTIVATION (random or AI-guided from seed traits)
   → e.g., Rescue

2. SELECT STRATEGY (from motivation's strategy pool)
   → e.g., Wrong Trail (Man-in-a-Hole arc)

3. BUILD STAGE PATTERN (from strategy definition)
   → [Search, Talk, Search(SETBACK), Talk, Confront(Rescue)]

4. FOR EACH SLOT in the pattern:
   a. Determine the action type and constraints
   b. Query TemplateLib for templates matching that action type
   c. Select template (weighted pick from matching set)
   d. Compute tension from arc shape at this position
   e. Assemble Addons: stage role, tension, mood, character web slice, entity manifest
   f. Attach Addons and grammar parameters to the MissionTemplate

5. GENERATE (backward, same as today)
   → For each stage: call template.outlawQuest.Setup()
   → Inject log messages into AI history for context

6. POLISH + VOICE (same as today)
```

Steps 1–4 are new. Step 5 is the existing generation loop. Step 6 is unchanged.

The critical insight: the grammar makes all structural decisions *before* any LLM call fires. By the time `Setup()` runs, the grammar has already determined what happens, who's involved, and what emotional register to hit. The LLM's job is to write it well.

### 10.6 Template Tagging for Grammar Matching

For the grammar to select templates by action type, each template needs to declare what actions it supports. The existing `MissionTags` field on `MissionTemplate` is the right place for this:

| Template | Current Tags | Grammar Action Tags |
|---|---|---|
| Investigation_ConversationCity | "conversation", "city" | `Talk`, `City` |
| Investigation_ActivatorPlanet | "activator", "planet" | `Search`, `Planet` |
| Investigation_DestroySmallBase | "destroy", "combat" | `Destroy`, `Planet` |
| Investigation_Derelict_Space | "derelict", "explore" | `Explore`, `Space` |
| Showdown_BountyPlanet | "showdown", "combat" | `Confront.Kill`, `Planet` |
| Meta_Fork_Exclusive | "fork", "choice" | `Choose` |
| *New: Showdown_NegotiatePlanet* | "showdown", "dialogue" | `Confront.Negotiate`, `Planet` |
| *New: Investigation_RescuePlanet* | "rescue", "planet" | `Rescue`, `Planet` |

The grammar's strategy specifies required action types per slot. The template engine filters TemplateLib to templates matching those tags, then does weighted selection within the filtered set. This preserves the existing two-tier weighted selection while adding grammar awareness.

### 10.7 A Concrete Example End-to-End

**Input:** Seed traits roll a character who was a "field medic" who "diverted emergency supplies" driven by "desperation to cover a family member's medical costs."

**Grammar selects:** Motivation = Redemption (the character wants to make amends). Strategy = Slow Reveal (each stage peels back a layer; Tragedy arc). Moral framing = Guilty but justified.

**Stage pattern:**
```
Slot 1: Search    → Discovery_Dataslate (find the bounty notice)
Slot 2: Talk      → Investigation_ConversationCity [Neon] (witness to the supply diversion)
Slot 3: Search    → Investigation_ActivatorSpace (find the medical records — REVELATION)
Slot 4: Talk      → Investigation_Informant_Planet (the family member who was sick)
Slot 5: Confront  → Showdown_NegotiatePlanet (target offers to turn themselves in)
```

**Tension curve (Tragedy — steady fall):**
```
Slot:      1     2     3     4     5
Tension:   0.2   0.4   0.6   0.8   0.9
Mood:      curious  uneasy  troubled  heavy  resigned
```

**Stage 4 Addons (assembled by grammar):**
```xml
<Motivation>Redemption</Motivation>
<QuestStage>DeepInvestigation</QuestStage>
<QuestProgress>70%</QuestProgress>
<Tension>0.8</Tension>
<Mood>heavy</Mood>
<StageRole>REVELATION — the player learns the full truth about why the target
  diverted the supplies. The informant is the target's family member.</StageRole>
<MoralFraming>Guilty but justified — the target saved a life at the cost of others.</MoralFraming>

<CharacterInScene name="Yuki Tanaka" role="Informant">
  Relationship: the target's sister, the one who was sick.
  Secret: knows exactly where the target is hiding.
  Agenda: wants the player to understand, not just hunt.
  Emotional state: grief-stricken but resolute. Composure 0.5.
  Dialogue acts: Inform about the illness and the impossible choice.
  Reveal the target's location only after the player understands the context.
  Withhold nothing — she wants the truth out.
</CharacterInScene>

<EntityManifest>
  Characters: Yuki Tanaka (informant), Hiro Tanaka (target, absent)
  Locations: settlement clinic on Gagarin, the safehouse (next stage)
  Facts: Hiro diverted 3 crates of emergency antibiotics. Yuki nearly died.
  Do NOT reference any entity not in this list.
</EntityManifest>

<QuestStageLocation stage="Discovery">Cydonia bounty board</QuestStageLocation>
<QuestStageLocation stage="InitialInvestigation">Neon Trade Authority office</QuestStageLocation>
<QuestStageLocation stage="Investigation">Derelict medical transport, deep orbit</QuestStageLocation>
```

This Addons block flows into `Investigation_Informant_Planet.Setup()`, which calls `QuestPrompts.GetQuestName(addons)`, `QuestPrompts.GetLogMessage(addons)`, and generates the informant dataslate text. The existing `Setup()` code doesn't change — it creates the same game records. But the AI, reading these Addons, writes completely different content than it would for a Justice/bounty-hunt chain.

The grammar pre-computed the story. The LLM wrote it.

---

## 11. Generation Without the LLM

The LLM is a tool, not a dependency. Every part of the generation pipeline should be able to produce output without it — rougher, more mechanical, but structurally complete. This matters for three reasons:

1. **Iteration speed.** Testing grammar changes, arc shapes, and template compositions shouldn't require API calls. A template-only mode generates a full chain in milliseconds.
2. **Baseline quality.** If the template-only output is garbage, the grammar is broken — no amount of LLM polish will fix structural problems. If the template-only output is coherent but flat, the grammar is sound and the LLM adds texture on top.
3. **Understanding the value boundary.** Knowing exactly where templates stop being sufficient and LLM generation becomes essential clarifies what the LLM's actual job is.

### 11.1 What the Grammar Produces on Its Own

The grammar engine (Section 3) and template pipeline (Section 10) make every structural decision without the LLM:

- **Motivation, strategy, arc shape** — selected by weighted random or trait-based rules
- **Stage sequence** — determined by the strategy's pattern, including branch points and setback placement
- **Template selection** — weighted picks from TemplateLib filtered by action type tags
- **Character web** — names, relationships, secrets, agendas assembled from seed pools
- **Tension curve** — computed from the arc shape at each stage position
- **Entity manifest** — the complete list of characters, locations, factions, and facts for each stage

All of this is deterministic (given a seed). The grammar produces a complete story *specification* — who does what, where, why, in what emotional register, with what moral framing.

### 11.2 Template-Based Text Generation

For each content type the system produces, a template layer can generate acceptable placeholder text without any LLM call.

**Quest names** — Tracery-style grammar expansion. 50+ templates per motivation, with slots filled from the character web and location data:

```
[Motivation: Justice]    "The #location# Bounty" / "#target.lastName# Warrant" / "Warrant for #target.firstName#"
[Motivation: Rescue]     "#target.firstName#'s Trail" / "The #location# Extraction" / "Missing from #location#"
[Motivation: Betrayal]   "The #faction# Double Cross" / "#npc.lastName#'s Game" / "Trust in #location#"
```

50 templates × 9 motivations = 450 quest name patterns. With slot variation, this produces thousands of unique names. The LLM adds nothing here that a well-curated grammar can't match.

**Log entries** — Improv-style tag-filtered templates. Each template is tagged with motivation, stage role, and tension level. The system selects from the matching pool:

```
[tags: Justice, Discovery, tension:low]
"Bounty posted for {target.name}, wanted for {target.crime}. Last known
location: {location}. Heading there to pick up the trail."

[tags: Rescue, SETBACK, tension:high]
"Trail went cold at {location}. {npc.name} either lied or was fed bad intel.
{target.name} wasn't here — but someone knew I was coming."

[tags: Betrayal, REVELATION, tension:high]
"It was {npc.name}. The evidence at {location} doesn't lie — {npc.pronoun}
set this whole thing up. {target.name} was just the fall {target.pronoun_obj}."
```

20–30 templates per (motivation × stage role) combination. With 9 motivations × 5 stage roles, that's ~1,000 templates total — a significant authoring investment, but one-time. The LLM adds smoother phrasing and contextual adaptation, but the templates carry the essential information.

**NPC names** — Markov chains trained on culture-specific corpora. Order 2–3 character-level chains produce pronounceable, plausible names. This is a solved problem — no LLM needed. The existing system already uses AI for name generation, but a Markov generator would produce names in microseconds with zero cost.

**Dialogue** — This is where templates hit their ceiling. Short exchanges (2–3 turns) can use the mad-libs pattern with synonym pools and sentence structure variation:

```
[tags: Informant, deflecting, composure:low]
PLAYER: "What do you know about {target.name}?"
NPC: "{deflect_opener}, I don't want any trouble."
     / "{deflect_opener}, you didn't hear this from me."
     / "Look, I {hedging_verb} something, but {deflect_reason}."
```

Where `deflect_opener` = ["Listen", "Hey", "Look, friend"] and `hedging_verb` = ["heard", "saw", "might know"] and `deflect_reason` = ["it's not safe to talk here", "I've got my own problems", "people are watching"].

This produces functional dialogue. It does not produce *good* dialogue. The template structure is audible after a few encounters. Extended conversations (4+ turns) and emotionally complex scenes (the showdown confrontation, the informant's confession) need either a massive authored corpus or an LLM.

**Dataslate prose** — The hardest content type for templates. Short headers work fine ("Field Report — {location}" / "{target.name}: Final Assessment"). Body text requires either:
- A Caves of Qud-style approach: 40K+ words of authored fragments recombined by replacement grammars, producing text that reads as mythic rather than precise
- Or acceptance that template prose will be mechanical: functional intel reports rather than evocative prose

### 11.3 The Caves of Qud Model

Caves of Qud is the gold standard for non-LLM procedural text. Its sultan biography system generates multi-paragraph histories with thematic coherence and narrative arc — entirely without AI.

**How it works:**
1. Generate 5 sultans with procedural names and 3 "domains" each (abstract themes like Ice, Scholarship, Fungus)
2. For each sultan, generate 10–22 life events via a state machine (origin, core events, death)
3. Events are rationalized *ex post facto* — generate what happened first, then construct the narrative explaining it
4. Text is produced by replacement grammars where domain symbols weave through gospel patterns
5. A 40,000+ word hand-authored corpus provides the "voice"

The critical insight: **subvert cause and effect.** Don't simulate a logical story and describe it. Generate interesting events first, then rationalize them. This produces more mythic, surprising text than logical construction would.

For quest chains, this suggests: generate the stage sequence and setbacks first (the grammar does this), then use replacement grammars to produce narrative explanations for why each event happened. "The informant lied" is a grammar decision. "She lied because she owed the target a debt from before the war" is a replacement grammar filling in motivation from themed pools.

The tradeoff is authoring cost. 40K words is months of writing. The LLM amortizes that cost — instead of writing 40K words of fragments, you write prompts. But the fragments produce more consistent, more controllable, and infinitely faster output.

### 11.4 The Hybrid Architecture

The grammar system should support three generation modes for each content type:

| Mode | Speed | Cost | Quality | Use Case |
|---|---|---|---|---|
| **Template-only** | Microseconds | Zero | Functional but mechanical | Grammar testing, regression baselines, structural validation |
| **Template + LLM polish** | Seconds | Low | Good — template structure with LLM texture | Standard generation with cost control |
| **Full LLM** | Seconds | Higher | Best — novel, contextual, emotionally nuanced | Flagship content, showdown scenes, key dialogue |

The mode selector could operate per-content-type within a single chain:

```
Quest names:        Template-only (LLM adds negligible value)
NPC names:          Markov chain (solved problem, no LLM needed)
Log entries:        Template + LLM polish (structure from template, smoothed by LLM)
NPC barks:          Template-only (short, frequent, need speed)
Short dialogue:     Template + LLM polish
Extended dialogue:  Full LLM (templates can't carry 4+ turn conversations)
Dataslate body:     Full LLM (requires novel prose)
Outlaw final log:   Full LLM (emotional peak, worth the cost)
```

This means the grammar system produces a *complete, playable quest chain* in template-only mode — every piece of text is filled, every quest record has content. Then the LLM selectively rewrites the pieces where it adds the most value. The template output serves as both a fallback and a baseline for measuring what the LLM improves.

### 11.5 What the LLM Actually Adds

With the template layer handling structure, the LLM's unique contribution narrows to three things:

1. **Contextual adaptation.** A template says "I heard something about {target.name} but it's not safe to talk here." An LLM says "Riko used to buy parts from me — fuel cells, mostly, nothing unusual. Then three months ago she needed a whole cooling unit, rush delivery, no questions. That's when I stopped asking." The LLM connects dots between specific world-state facts in ways that would require combinatorial template explosion.

2. **Emotional texture.** Templates produce functional text. The LLM produces text that *feels* like something — a desperate person's halting confession, a cold liar's measured deflection, a grieving sister's quiet anger. This is the "authored flavor" that Storytron lacked and Caves of Qud invested 40K words to approximate.

3. **Novel combinations.** When the grammar produces an unusual combination — a Rescue motivation with an Icarus arc and a "Framed" moral framing — templates for that specific intersection may not exist. The LLM handles the long tail of combinations that templates can't practically cover.

Everything else — structure, entity grounding, arc shape, stage sequencing, character relationships — is handled by the grammar and templates. The LLM is the texture layer on top of a structurally sound system, not the foundation holding it together.

---

## 12. Evaluation: Knowing If It's Working

The grammar system will generate a combinatorial explosion of quest chains. We can't manually play every one. We need automated and semi-automated evaluation so that changes to prompts, parameters, and grammar rules can be validated against quality baselines without human playtesting every output.

### 10.1 What to Measure

Research identifies eight dimensions that matter for procedural narrative quality. Each maps to a specific failure mode in quest chain generation:

| Dimension | What It Measures | Failure Mode It Catches |
|---|---|---|
| **Structural completeness** | All required stages present, proper act structure | Missing stages, broken chains, generation crashes |
| **Narrative coherence** | Events follow logically, no contradictions | Stage 3 references a character who dies in stage 2 |
| **Character consistency** | NPCs behave according to their persona | An NPC who is "cautious and paranoid" speaks breezily |
| **Entity grounding** | Only references pre-defined world elements | Hallucinated names, locations, factions |
| **Dialogue quality** | Natural, in-character, appropriate tone | Robotic phrasing, modern slang in a formal setting |
| **Emotional arc adherence** | Matches target tension/resolution pattern | A "Man in a Hole" arc that never has the setback |
| **Information pacing** | Clues revealed at the right rate | Too much revealed too early, or nothing revealed at all |
| **Moral clarity** | The moral framing is consistent and lands | A "framed target" quest where the framing is never revealed |

Not all dimensions can be measured the same way. Some are automatable (entity grounding, structural completeness). Some require LLM-as-judge (dialogue quality, coherence). Some need human evaluation (emotional impact, memorability).

### 10.2 The Three-Tier Evaluation Pipeline

**Tier 1: Deterministic Checks (fast, cheap, run on every generation)**

These are hard pass/fail gates that catch structural failures before any quality evaluation happens.

- **Structural validation** — Does the output contain the expected number of stages? Does each stage have a quest name, log entry, and location? Are quest-to-quest links wired correctly? This is pure code: parse the generation output and check required fields.
- **Entity grounding** — Extract all proper nouns from generated text. Check each against the entity manifest (characters, locations, factions defined for this chain). Flag any entity not in the manifest. This catches the most common LLM failure mode.
- **Length constraints** — Log entries within word limits? Dialogue exchanges within turn count? Dataslate text within character limits for voice synthesis?
- **Stage-locked knowledge** — Does a stage at 20% progress reference facts that should only appear at 70%+? Check each stage's text against a per-stage knowledge whitelist derived from the grammar.

Tier 1 runs in seconds and catches ~60% of quality issues. Every generation must pass Tier 1 before proceeding.

**Tier 2: LLM-as-Judge (moderate cost, run on every generation that passes Tier 1)**

Use a separate LLM call to evaluate the generated content against rubrics. The G-Eval framework (chain-of-thought scoring with custom criteria) significantly outperforms traditional metrics like BLEU/ROUGE on human alignment for creative content.

Key technique: **rubric decomposition**. Don't ask "is this good?" Ask specific questions:

```
Evaluate the following quest chain on NARRATIVE COHERENCE (1-5):
1 = Major contradictions between stages
2 = Minor contradictions that a player would notice
3 = No contradictions, but connections feel forced
4 = Logical flow with natural transitions
5 = Each stage builds naturally on the previous, with clear cause-and-effect

Quest chain:
[generated content]

First, identify any contradictions or logical gaps between stages.
Then assign a score with justification.
```

Run separate rubric evaluations for each dimension. This produces a score vector per quest chain, not a single number.

**Known biases in LLM-as-judge** (from the 2024 survey):
- **Position bias** — Swapping presentation order shifts accuracy by >10%. Mitigation: when comparing two chains, present in both orders and average.
- **Verbosity bias** — Judges prefer longer, more formal outputs regardless of quality. Mitigation: include "brevity is acceptable" in the rubric.
- **Self-preference** — LLMs score their own generations higher. Mitigation: use a different model for judging than for generating, or use multi-judge panels.

**Tier 3: Human Evaluation (expensive, run on sample batches for calibration)**

Human evaluation provides ground truth for calibrating Tier 2. Run periodically, not on every generation.

- **Comparative ranking** is more reliable than absolute scoring. Present two quest chains side by side and ask "which tells a better story?" This eliminates rater scale drift.
- **5-point Likert scales** on specific dimensions (not holistic "how good is this?"). Use the same dimensions as Tier 2 for calibration.
- **3 raters per item** minimum for reliable inter-annotator agreement (the HANNA benchmark standard).

The output of Tier 3 calibration: correlation scores between Tier 2 LLM-as-judge ratings and human ratings. If a specific rubric dimension shows low correlation (<0.6), that dimension needs a better rubric or should be moved to Tier 1 deterministic checking.

### 10.3 Emotional Arc Measurement

The arc is a first-class generation parameter, so measuring whether the output actually follows it is critical.

**Approach:** Run a sentiment/emotion classifier on each stage's generated text independently. Plot the sentiment trajectory across stages. Compute correlation with the target arc shape.

```
Target arc (Man in a Hole):
  Stage:     1     2     3     4     5
  Tension:   0.3   0.5   0.8   0.6   0.9

Measured sentiment (inverted valence = tension):
  Stage:     1     2     3     4     5
  Tension:   0.35  0.48  0.72  0.55  0.85

Correlation: 0.98 → PASS (threshold: 0.85)
```

If the measured arc diverges from the target — the setback isn't dark enough, the triumph isn't triumphant enough — the system can flag that specific stage for regeneration. This is cheaper than regenerating the whole chain.

Research confirms this works: the Reagan et al. methodology (sentiment analysis via labMT word lists) is robust and computationally cheap. A 2025 paper on emotional arc-guided game level generation showed that arc-integrated generation significantly enhances player engagement and emotional impact.

### 10.4 Consistency Checking Across Stages

The ConStory-Bench taxonomy identifies 19 error types in 5 categories for long-form narrative. The ones most relevant to quest chains:

| Category | Error Type | Quest Chain Example |
|---|---|---|
| **Timeline/Plot** | Temporal contradiction | Stage 3 says "yesterday" for an event that happened in stage 1, weeks ago |
| **Characterization** | Personality drift | An NPC described as "meek" suddenly speaks with authority |
| **Characterization** | Knowledge violation | An NPC references facts they shouldn't know |
| **World-building** | Rule violation | A quest set on an airless moon where NPCs breathe freely |
| **Factual** | Entity contradiction | A character's name spelled differently across stages |

The ConStory-Checker approach: extract entity-fact pairs from each stage, then check for contradictions between stages. This can be automated as a Tier 2 evaluation step — feed all stages to an LLM with the prompt "identify any contradictions between these stages."

### 10.5 Regression Testing

The system will evolve: prompts will change, grammar rules will be added, seed pools will grow. Each change could improve one dimension while degrading another. Regression testing catches this.

**The test suite:**

1. **Test case bank** — A curated set of 20–50 quest chain specifications (fixed traits, fixed motivation/strategy, fixed arc shape). These are the "golden inputs" that produce reproducible-ish outputs.
2. **Baseline scores** — Run the full evaluation pipeline (Tier 1 + Tier 2) on all test cases. Save the score vectors as the baseline.
3. **On change** — Re-run the suite. Compare each dimension's scores against the baseline. Flag any dimension that drops more than 0.5 points (on 1–5 scale) or any Tier 1 check that newly fails.
4. **Score dashboard** — Track dimension scores over time. This reveals trends: "coherence has been improving but dialogue quality has been drifting down."

**Tooling:** The DeepEval framework (open-source, pytest-like) and Promptfoo (CLI, YAML-based) both support this pattern with built-in regression comparison and CI/CD integration.

**Practical consideration:** LLM generation is non-deterministic. The same prompt can produce different quality outputs on different runs. Mitigate by running each test case 3 times and scoring the median, or by using low temperature for regression runs (sacrificing creativity for reproducibility).

### 10.6 The Evaluation Loop

Putting it all together into the development workflow:

```
1. CHANGE (modify prompt, grammar rule, or seed pool)

2. REGRESSION RUN (automated, ~30 minutes for 50 test cases)
   ├── Tier 1: Deterministic checks on all outputs
   │   → Any new failures? Stop and fix.
   ├── Tier 2: LLM-as-judge scoring on all passing outputs
   │   → Any dimension dropped >0.5 from baseline? Investigate.
   └── Arc measurement on all outputs
       → Any arc correlation dropped below 0.85? Investigate.

3. ACCEPT or ITERATE
   ├── All green → Update baseline, ship the change
   └── Regression detected → Analyze which dimension degraded, fix, re-run

4. PERIODIC CALIBRATION (monthly or after major changes)
   └── Tier 3: Human evaluation on 20 sampled outputs
       → Recalibrate Tier 2 rubrics if LLM-human correlation drifts
```

The goal is a fast inner loop (Tier 1+2, automated, minutes) with a slower outer loop (Tier 3, human, periodic). Most iteration happens in the inner loop. Human evaluation prevents the automated metrics from drifting away from what actually matters to players.

### 10.7 What "Good" Looks Like

Concrete quality targets for the grammar system:

| Dimension | Target Score (1–5) | Tier 1 Gate |
|---|---|---|
| Structural completeness | N/A | 100% pass rate (hard gate) |
| Entity grounding | N/A | 100% pass rate (hard gate) |
| Narrative coherence | ≥ 3.5 | No cross-stage contradictions |
| Character consistency | ≥ 3.5 | NPC names/roles match across stages |
| Dialogue quality | ≥ 3.0 | Within word/turn limits |
| Emotional arc adherence | N/A | Arc correlation ≥ 0.85 |
| Information pacing | ≥ 3.0 | Stage-locked knowledge respected |
| Moral clarity | ≥ 3.0 | Resolution type matches moral framing |

These targets are starting points. Tier 3 human evaluation calibrates whether "3.5 on coherence" actually means "players don't notice contradictions" or whether the rubric needs adjustment.

---

## 13. Sources

### Academic Papers

- Doran, J. & Parberry, I. (2011). "Towards Procedural Quest Generation: A Structural Analysis of RPG Quests." *PCG Workshop at FDG 2011*. [[Semantic Scholar]](https://www.semanticscholar.org/paper/Towards-Procedural-Quest-Generation-:-A-Structural-Doran-Parberry/aa4c9154fccdc647ca0f59b544620fda978d3217)
- Doran, J. & Parberry, I. (2011). "A Prototype Quest Generator Based on a Structural Analysis of Quests from Four MMORPGs." *LARC Technical Report*. [[PDF]](https://ianparberry.com/techreports/LARC-2011-02.pdf)
- Reagan, A.J. et al. (2016). "The Emotional Arcs of Stories are Dominated by Six Basic Shapes." *EPJ Data Science*, 5(1). [[Springer]](https://link.springer.com/article/10.1140/epjds/s13688-016-0093-1) [[MIT Tech Review summary]](https://www.technologyreview.com/2016/07/06/158961/data-mining-reveals-the-six-basic-emotional-arcs-of-storytelling/)
- Buongiorno, D. et al. (2024). "PANGeA: Procedural Artificial Narrative using Generative AI for Turn-Based Video Games." *AAAI AIIDE 2024*. [[arXiv]](https://arxiv.org/abs/2404.19721)
- Riedl, M.O. (2014). "Narrative Planning: Balancing Plot and Character." *Journal of Artificial Intelligence Research*. [[arXiv]](https://arxiv.org/abs/1401.3841)
- Kreminski, M. & Wardrip-Fruin, N. (2019). "Felt: A Simple Story Sifter." *Interactive Storytelling (ICIDS)*. [[Springer]](https://link.springer.com/chapter/10.1007/978-3-030-33894-7_27) [[GitHub]](https://github.com/mkremins/felt)
- de Lima, E.S. et al. (2022). "Procedural Generation of Branching Quests for Games." *Entertainment Computing*. [[ScienceDirect]](https://www.sciencedirect.com/science/article/pii/S1875952122000155)
- Kumaran, V. et al. (2023). "SceneCraft: An LLM Agent for Synthesizing 3D Scenes from Natural Language." *AAAI AIIDE*. [[AAAI]](https://ojs.aaai.org/index.php/AIIDE/article/view/27504)
- "PCG in Games: A Survey with Insights on Emerging LLM Integration." *AIIDE 2024*. [[arXiv]](https://arxiv.org/html/2410.15644v1)
- "Game Knowledge Management System (G-KMS)." *Systems*, 2025. [[MDPI]](https://www.mdpi.com/2079-8954/14/2/175)
- Kreminski, M. et al. (2025). "Composable Story Sifting Patterns." *FDG 2025*. [[ACM]](https://dl.acm.org/doi/10.1145/3723498.3723809)
- "Emotional Arc Guided Procedural Game Level Generation." 2025. [[arXiv]](https://arxiv.org/pdf/2508.02132)
- "LIGS: LLM-infused Game System for Emergent Narrative." *CHI 2025*. [[ACM]](https://dl.acm.org/doi/10.1145/3706599.3720212)
- "Personalized Quest Generation via Knowledge Graph + LLM." *CHI 2023*. [[ACM]](https://dl.acm.org/doi/10.1145/3544548.3581441)
- "CONAN: Let CONAN Tell You a Story." *Entertainment Computing*. [[ScienceDirect]](https://www.sciencedirect.com/science/article/abs/pii/S1875952121000197)
- "Questgram: Mixed-Initiative Quest Generation Tool." *FDG 2021*. [[ACM]](https://dl.acm.org/doi/fullHtml/10.1145/3472538.3472544)
- "Characterization and Emergent Narrative in Dwarf Fortress." *ResearchGate*. [[PDF]](https://www.researchgate.net/publication/356686095_Characterization_and_Emergent_Narrative_in_Dwarf_Fortress)
- "Reinforcement Learning for Procedural Narrative." *GRAPP 2025*. [[arXiv]](https://arxiv.org/html/2501.08552v1)
- "Knudge: Ontologically Faithful Generation of Non-Player Character Dialogues." 2022. [[arXiv]](https://ar5iv.labs.arxiv.org/html/2212.10618)
- "Generative Subgraph Retrieval for Knowledge-Grounded Dialog." 2024. [[arXiv]](https://arxiv.org/html/2410.09350v1)
- "Knowledge-Consistent Dialogue Generation with Knowledge Graphs." *OpenReview*. [[PDF]](https://openreview.net/pdf?id=kuJQ_NwJO8_)
- "SLOT: Structuring the Output of LLMs." *EMNLP 2025*. [[PDF]](https://aclanthology.org/2025.emnlp-industry.32.pdf)
- "Min-p Sampling for Creative and Coherent LLM Outputs." *ICLR 2025*. [[OpenReview]](https://openreview.net/forum?id=FBkpCyujtS)
- Huang, J. et al. (2024). "When Can LLMs Actually Correct Their Own Mistakes?" *TACL*. [[MIT Press]](https://direct.mit.edu/tacl/article/doi/10.1162/tacl_a_00713/125177/When-Can-LLMs-Actually-Correct-Their-Own-Mistakes)
- "CRITIC: LLMs Can Self-Correct with Tool-Interactive Critiquing." *OpenReview*. [[Paper]](https://openreview.net/forum?id=Sx038qxjek)
- "HaluGate: Token-Level Hallucination Detection." *vLLM Blog*, 2025. [[Blog]](https://blog.vllm.ai/2025/12/14/halugate.html)
- "Survey and Analysis of Hallucinations in LLMs." *Frontiers in AI*, 2025. [[Paper]](https://www.frontiersin.org/journals/artificial-intelligence/articles/10.3389/frai.2025.1622292/full)
- "Prompt Engineering Patterns that Reduce Hallucinations." *ResearchGate*. [[Paper]](https://www.researchgate.net/publication/394431721_Prompt_Engineering_Patterns_that_Reduce_Hallucinations_in_Large_Language_Models)
- "High-Quality Generation of Dynamic Game Content via Small Language Models." 2025. [[arXiv]](https://arxiv.org/html/2601.23206)
- "LLM-Driven NPCs: Cross-Platform Dialogue System." 2025. [[arXiv]](https://arxiv.org/html/2504.13928v1)
- "Guiding Generative Storytelling with Knowledge Graphs." 2025. [[arXiv]](https://arxiv.org/html/2505.24803v2)
- "Survey on LLMs for Story Generation." *EMNLP 2025 Findings*. [[PDF]](https://aclanthology.org/2025.findings-emnlp.750.pdf)
- "Mitigating LLM Hallucinations Using a Multi-Agent Framework." *Information*, 2025. [[MDPI]](https://www.mdpi.com/2078-2489/16/7/517)
- "MCPDial: A Minecraft Persona-driven Dialogue Dataset." 2024. [[arXiv]](https://arxiv.org/html/2410.21627v1)
- "Recent Trends in Personalized Dialogue Generation." 2024. [[arXiv]](https://arxiv.org/html/2405.17974v1)
- "Dialogue Act-based Partner Persona Extraction." *Expert Systems with Applications*, 2024. [[ScienceDirect]](https://www.sciencedirect.com/science/article/abs/pii/S0957417424012466)
- Weir, N. et al. (2024). "KNUDGE: Ontologically Faithful Generation of NPC Dialogues." *EMNLP 2024*. [[ACL Anthology]](https://aclanthology.org/2024.emnlp-main.520/)
- "Lies, Deceit, and Hallucinations: NPC Deception Perception." *CHI 2024*. [[ACM]](https://dl.acm.org/doi/10.1145/3613904.3642253)
- "Tricking LLM-Based NPCs into Spilling Secrets." *ProvSec 2025*. [[arXiv]](https://arxiv.org/html/2508.19288)
- "Symbolically Scaffolded Play: Role-Sensitive Prompts for NPC Dialogue." 2025. [[arXiv]](https://arxiv.org/html/2510.25820)
- "Conversational Interactions with NPCs in LLM-Driven Gaming." *ICIDS 2024*. [[Springer]](https://link.springer.com/chapter/10.1007/978-3-031-54975-5_10)
- "EmoCtrl-TTS: Zero-Shot Emotional Speech Synthesis." *Microsoft Research*, 2024. [[Project]](https://www.microsoft.com/en-us/research/project/emoctrl-tts/)
- "EmoKnob: Fine-Grained Emotion Control for TTS." *Columbia University*. [[Project]](https://emoknob.cs.columbia.edu/)
- "EmoSphere++: Emotion-Adaptive Spherical Vectors for TTS." 2024. [[arXiv]](https://arxiv.org/html/2411.02625v1)
- "Prosodic Characteristics of Deceptive Speech." *Speech Communication*, 2025. [[ScienceDirect]](https://www.sciencedirect.com/science/article/pii/S0167639325001141)
- "Vocal Pitch Production during Lying." *ResearchGate*. [[Paper]](https://www.researchgate.net/publication/239795119_Vocal_Pitch_Production_during_Lying_Beliefs_about_Deception_Matter)
- "Cues to Lying." *PMC*. [[Paper]](https://pmc.ncbi.nlm.nih.gov/articles/PMC6634475/)
- "Spoken Conversational AI in Video Games — Emotional Dialogue Management." *ResearchGate*. [[Paper]](https://www.researchgate.net/publication/328215976_Spoken_Conversational_AI_in_Video_Games-Emotional_Dialogue_Management_Increases_User_Engagement)
- "Fine-tuning GPT-2 on Annotated RPG Quests for NPC Dialogue." *FDG 2021*. [[ACM]](https://dl.acm.org/doi/fullHtml/10.1145/3472538.3472595)
- "What Makes a Good Story and How Can We Measure It? Comprehensive Survey." 2024. [[arXiv]](https://arxiv.org/html/2408.14622v1)
- "SCORE: Story Coherence and Retrieval Enhancement for AI Narratives." 2025. [[arXiv]](https://arxiv.org/html/2503.23512v1)
- "HANNA: Of Human Criteria and Automatic Metrics for Story Generation." *COLING 2022*. [[ACL Anthology]](https://aclanthology.org/2022.coling-1.509/) [[GitHub]](https://github.com/dig-team/hanna-benchmark-asg)
- "NarraBench: A Comprehensive Framework for Narrative Understanding Benchmarks." 2025. [[arXiv]](https://arxiv.org/abs/2510.09869)
- "Lost in Stories: ConStory-Bench Consistency Evaluation." 2025. [[arXiv]](https://arxiv.org/html/2603.05890)
- "A Survey on LLM-as-a-Judge." 2024. [[arXiv]](https://arxiv.org/abs/2411.15594)
- "Position Bias in LLM-as-a-Judge." 2024. [[arXiv]](https://arxiv.org/abs/2406.07791)
- "G-Eval: NLG Evaluation using GPT-4 with Better Human Alignment." 2023. [[arXiv]](https://arxiv.org/pdf/2303.16634)
- "LLM-RUBRIC: Multidimensional Calibrated Evaluation." *ACL 2024*. [[PDF]](https://aclanthology.org/2024.acl-long.745.pdf)
- "Player-Driven Emergence in LLM-Driven Game Narrative." 2024. [[arXiv]](https://arxiv.org/pdf/2404.17027)
- "Verifiable Emotion Reward for Character-Coherent Role-Playing." *Information*, 2025. [[MDPI]](https://www.mdpi.com/2078-2489/16/9/738)
- "PersonaLens: Personalization Evaluation in Dialogue." *ACL 2025 Findings*. [[PDF]](https://aclanthology.org/2025.findings-acl.927.pdf)
- "Agents' Room: Narrative Generation through Multi-Agent Collaboration." 2025. [[OpenReview]](https://openreview.net/pdf?id=HfWcFs7XLR)
- "MultiSentimentArcs for Long-Form Narratives." *Frontiers in Computer Science*, 2024. [[Paper]](https://www.frontiersin.org/journals/computer-science/articles/10.3389/fcomp.2024.1444549/full)

### Systems and Tools

- **Versu** (Emily Short & Richard Evans) — Simulationist interactive storytelling. [[Site]](https://versu.com/) [[IEEE]](https://ieeexplore.ieee.org/document/6648395/)
- **Storytron** (Chris Crawford) — Interactive storyworld engine, 1992–2018.
- **Tracery** (Kate Compton) — JSON-defined generative grammars. [[GitHub]](https://github.com/galaxykate/tracery)
- **Spirit AI / Character Engine** — Dual interaction mode NPC system. [[Emily Short's blog]](https://emshort.blog/tag/spiritai/)
- **AI Dungeon** (Latitude) — LLM-powered interactive fiction. [[Wikipedia]](https://en.wikipedia.org/wiki/AI_Dungeon)
- **Dwarf Fortress** (Bay 12 Games) — Agent simulation with emergent narrative. [[Taylor & Francis chapter]](https://www.taylorfrancis.com/chapters/edit/10.1201/9780429488337-15/emergent-narrative-dwarf-fortress-tarn-adams)
- **Fallen London / StoryNexus** (Failbetter Games) — Quality-Based Narrative at scale. [[Failbetter]](https://www.failbettergames.com/news/storynexus-is-live)

### Design Writing

- Nesmith, B. (2012). "Bethesda's Nesmith Reflects on the Difficult Birth of Skyrim's Radiant Story System." [[VentureBeat]](https://venturebeat.com/games/bethesdas-nesmith-reflects-on-the-difficult-birth-of-skyrims-radiant-story-system/)
- Dias, B. (2017). "An Ideal QBN System." [[Blog]](https://brunodias.dev/2017/05/30/an-ideal-qbn-system.html)
- Riedl, M.O. "An Introduction to AI Story Generation." [[Medium]](https://mark-riedl.medium.com/an-introduction-to-ai-story-generation-7f99a450f615)
- Riedl, M.O. "Computational Narrative Intelligence: Past, Present, and Future." [[Medium]](https://mark-riedl.medium.com/computational-narrative-intelligence-past-present-and-future-99e58cf25ffa)
- Creation Kit Wiki. "Bethesda Tutorial: Radiant Quests." [[UESP]](https://ck.uesp.net/wiki/Bethesda_Tutorial_Radiant_Quests)
- "Awesome Story Generation" — Curated paper collection. [[GitHub]](https://github.com/yingpengma/Awesome-Story-Generation)
- "Grice's Maxims and How You Can Use Them in Fiction." *SFWA*. [[Article]](https://www.sfwa.org/2019/06/26/grices-maxims-and-how-you-can-use-them-in-your-fiction/)
- "Adding Life to Worlds with Dialogue Barks." *Game Developer*. [[Article]](https://www.gamedeveloper.com/design/adding-life-to-worlds-with-dialogue-barks)
- "Caramel Dialogue: Real Procedural Personalities." *GearHead RPG*. [[Blog]](https://www.gearheadrpg.com/2019/06/12/caramel-dialogue-real-procedural-personalities/)
- "How the Voice Can Betray Lies." *Paul Ekman Group*. [[Article]](https://www.paulekman.com/blog/how-the-voice-can-betray-lies/)
- ElevenLabs v3 Audio Tags documentation. [[Blog series]](https://elevenlabs.io/blog/v3-audiotags)
- **EmotiVoice** (NetEase) — Open-source emotion-controlled TTS. [[GitHub]](https://github.com/netease-youdao/EmotiVoice)
- **KNUDGE** dataset (The Outer Worlds). [[GitHub]](https://github.com/nweir127/KNUDGE)
- **DeepEval** — Open-source LLM evaluation framework (pytest-like). [[GitHub]](https://github.com/confident-ai/deepeval)
- **Promptfoo** — CLI tool for prompt testing with regression baselines. [[GitHub]](https://github.com/promptfoo/promptfoo)
- **HANNA** benchmark — 1,056 stories with human annotations on 6 quality dimensions. [[GitHub]](https://github.com/dig-team/hanna-benchmark-asg)
- **Improv** (Bruno Dias) — Model-backed generative text with tag-based filtering. [[GitHub]](https://github.com/sequitur/improv) [[Blog]](https://brunodias.dev/2016/01/27/improv.html)
- **Bronco** — Turing-complete authoring language for procedural text. [[GitHub]](https://github.com/qed-lab/Bronco-Text-Generator) [[Paper]](https://link.springer.com/chapter/10.1007/978-3-031-22298-6_35)
- **Bracery** — Extended Tracery with variable manipulation. [[GitHub]](https://github.com/ihh/bracery)
- **Lume** — Procedural story generation via parameterized scene nodes. [[PDF]](https://eis.ucsc.edu/papers/Mason_Lume.pdf)
- Grinblat, J. & Bucklew, B. (2017). "Subverting Historical Cause & Effect: Generation of Mythic Biographies in Caves of Qud." [[PDF]](https://www.freeholdgames.com/papers/Generation_of_Mythic_Biographies_in_CavesofQud.pdf) [[GDC Talk]](https://gdcvault.com/play/1024990/Procedurally-Generating-History-in-Caves)
- Short, E. (2014). "Procedural Text Generation in IF." [[Blog]](https://emshort.blog/2014/11/18/procedural-text-generation-in-if/)
- "Textual Procedural Generation and Narration." *Game Developer*. [[Article]](https://www.gamedeveloper.com/design/textual-procedural-generation-and-narration-generalities-opinions-tips-and-perspectives-)
- Compton, K. (2015). "Tracery: An Author-Focused Generative Text Tool." *FDG 2015*. [[PDF]](http://www.fdg2015.org/papers/fdg2015_extended_abstract_18.pdf) [[GitHub]](https://github.com/galaxykate/tracery)
