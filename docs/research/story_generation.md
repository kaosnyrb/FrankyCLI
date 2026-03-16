# Systemic Story Generation

A design exploration for Retrograde — how to generate stories of any shape, not just bounty hunts.

---

## 1. Why Procedural Stories Are Hard

Three tensions kill procedurally generated narratives:

**Causality vs. coincidence.** Authored stories have events that *cause* other events. A character betrays someone because of a grudge established two acts earlier. Procedural systems produce coincidence — things happen next to each other without causing each other. The current Retrograde system partially solves this through AI conversation history accumulating lore context, and through StageBridge generation. But the causal chain is always the same shape: Discovery → clue → Investigation → clue → Showdown.

**Specificity vs. templates.** The best Bethesda moments feel hand-placed — a data-slate on a desk that explains why the locked room is locked. Procedural systems fight specificity because they must work across many configurations. Retrograde handles this well at the text level (AI grounds everything in LoreContext) but the *mechanical* specificity is locked to bounty hunting — you always find clues, talk to informants, kill the target.

**Stakes vs. arbitrariness.** Players care when they understand what's at risk and for whom. OutlawTraits and the epilogue log create personal stakes for the target. But the *player's* stake is always identical: complete the bounty. There's no variation in what the player risks or gains.

### What makes stories interesting

Four properties separate an interesting procedural story from a boring one:

1. **Tension that escalates differently.** A rescue escalates toward a deadline. A mystery escalates toward a revelation. A trade dispute escalates toward a choice. The current system only escalates toward combat.

2. **Characters with agency.** OutlawNpc has rich traits but zero agency within the quest chain — always running, always gets caught. Interesting stories have characters who *do things* that change the situation. A contact who lies. An ally who gets captured. A target who moves.

3. **Surprise that makes sense.** The current StageBridge system connects stages but never surprises. The informant is dead when you arrive. The data-slate contains a warning instead of a clue. The showdown location changes because the target found out you were coming.

4. **Conclusions that recontextualize.** The OutlawLogfile does this already — the dead target's final words reframe the hunt. A systemic system should produce different flavors: the rescue target who doesn't want to be rescued. The heist where the vault was empty. The investigation that proves the informant was the real criminal.

---

## 2. What the Current System Does Well

Before redesigning anything, it's worth naming what already works — these are load-bearing walls.

**Clean separation between spatial and narrative generation.** Dungeon generators know nothing about story. Quest chains know nothing about room layouts. This boundary is correct and must be preserved.

**AI as creative partner, not replacement.** Structure lives in C#. AI generates text content and selects from curated menus. The three-call-type system (`RunPrompt` / `RunStatelessPrompt` / `InjectContextIntoHistory`) is a well-designed approach to maintaining narrative coherence across many AI calls. The polish pass (stateless, iterative, reads full history) is particularly good.

**The Noun pattern.** Self-contained record builders that handle their own Mutagen wiring. `INoun` / `INoun<T>` provides type-safe access. NounRegistry tracks everything. This pattern should be extended, not replaced.

**IPolishable and WritingPolishPass.** Collecting all generated text and iteratively improving it across the entire quest chain is a major quality lever. A systemic story system should make this even more powerful by giving the polish pass structural knowledge about the story.

**Seed data driven randomization.** Trait generation, name generation, voice selection happen in C# from curated pools rather than asking the AI. Faster, more controllable, more predictable.

---

## 3. Core Concepts

### 3.1 Story Schemas

A **Story Schema** is a reusable narrative structure defined as a sequence of beat slots. The current system has exactly one schema hardcoded in `LoopingLayoutQuestChain`: the Bounty Hunt (`Discovery[1] → Investigation[2-5] → Showdown[1]`).

A systemic system needs a library of schemas. Each schema defines:

- A sequence of **beat slots** with narrative roles (opener, escalation, twist, climax, resolution)
- Min/max count per slot
- Ordering constraints
- Required character roles
- The emotional arc (what kind of tension is escalating)

Example schemas beyond bounty hunting:

| Schema | Beats | Climax Type | Escalation |
|--------|-------|-------------|------------|
| **Bounty Hunt** | Discovery → Investigation[2-5] → Showdown | Kill target | Pursuit → Confrontation |
| **Rescue** | Alert → Search[1-3] → Locate → Extraction | Reach & free | Urgency → Deadline |
| **Mystery** | Crime Scene → Evidence[2-3] → Suspects → Revelation | Learn truth | Confusion → Clarity |
| **Heist/Recovery** | Report → Trail[1-3] → Acquisition | Retrieve item | Tracking → Opportunity |
| **Faction Conflict** | Introduction → Both Sides[2] → Escalation → Resolution | Choice | Complexity → Decision |
| **Salvage** | Commission → Approach → Exploration[2-3] → Discovery | Find prize | Risk → Reward |

**The critical insight:** ~80% of existing `IOutlawQuest` implementations are mechanically generic. `Investigation_ConversationCity` creates an NPC, places them at a marker, generates dialogue, wires up a quest. Whether the dialogue is "where did the outlaw go?" or "when did you last see the missing person?" is determined entirely by AI prompts. The quest *structure* is reusable; only the *narrative framing* changes.

### 3.2 Story Beats

A **Story Beat** is the atomic unit of narrative — one thing that happens that advances the story. In Starfield terms, a beat is one Quest record with its associated Nouns (dialogue, books, activators).

Each beat has:

- **Narrative function**: what role it plays (introduce problem, gather evidence, escalate tension, reverse expectations, climax, resolve)
- **Mechanical type**: what the player does (read data-slate, talk to NPC, destroy objects, kill target, reach location, activate something)
- **Inputs**: what must exist before this beat runs (target name, previous location, a clue)
- **Outputs**: what this beat produces for subsequent beats (new location, character name, plot revelation)

How existing implementations map to mechanical types:

| Mechanical Type | Existing Class | Schema-Agnostic? |
|---|---|---|
| Read data-slate | `Discovery_Dataslate` | Yes — text is AI-generated |
| Talk to NPC (city) | `Investigation_ConversationCity` | Yes — dialogue is AI-generated |
| Talk to NPC (planet) | `Investigation_ConversationPlanet` | Yes |
| Activate clue (any) | `Investigation_Activator*` (4 variants) | Yes |
| Explore derelict | `Investigation_Derelict_Space` | Yes |
| Talk to informant | `Investigation_Informant_*` | Yes |
| Destroy objects | `Investigation_Destroy*` | Partially — "contraband" is bounty-flavored |
| Kill target | `Showdown_Bounty*` | Bounty-specific — other schemas need different climaxes |

### 3.3 Character Roles

The current system has one character concept: the Outlaw (`OutlawNpc`). A systemic system needs abstract roles that get filled by concrete Nouns.

**Abstract roles:**

| Role | Current Implementation | Generalizable? |
|------|----------------------|----------------|
| Target | `OutlawNpc` | Yes — rename DefiningEvent instead of Crime |
| Informant | Created inline by `Investigation_Conversation*` | Should be first-class |
| Employer/Client | Implicit (bounty board) | Could be a named NPC |
| Ally | Nonexistent | New |
| Witness | Subset of informant | New |
| Antagonist | Same as target | Should be separate for non-bounty schemas |

`OutlawTraits` is already mostly role-agnostic:

```
Upbringing       → works for any character
Fear             → works for any character
Goal             → works for any character
Flaw             → works for any character
Quirk            → works for any character
Occupation       → works for any character
Crime            → needs to generalize to "DefiningEvent"
HuntingFaction   → needs to generalize to "AssociatedFaction"
CurrentPreoccupation → works for any character
```

Only two of nine fields are bounty-specific. The trait generation infrastructure (`NpcSeedData`, `StorySeedData`, `NarrativeSeedData`) can be extended with role-appropriate seed pools.

### 3.4 Motivation & Stakes

Every story needs a "why." Currently hardcoded: player is a bounty hunter, reward is credits + legendary item, stakes are that the outlaw stays free.

A systemic system separates three kinds of motivation:

| Layer | Bounty Hunt | Rescue | Mystery |
|-------|-------------|--------|---------|
| **Player motivation** | Payment | Obligation/compassion | Curiosity |
| **Story stakes** | Criminal stays free | Person dies/suffers | Truth stays hidden |
| **Character motivations** | OutlawTraits | Missing person's situation + captor's motive | Victim's secrets + suspects' alibis |

These map to Starfield content:
- Player motivation → quest objectives and log entries
- Story stakes → books, dialogue, environmental details
- Character motivations → LoreContext and personal logs

---

## 4. Proposed Architecture

Five layers, each talking only to its neighbors:

```
┌─────────────────────────────────────────────────┐
│  SCHEMA LAYER                                    │
│  Data-driven story structures (YAML)             │
│  questgen_quests/schemas/*.yaml                  │
├─────────────────────────────────────────────────┤
│  CASTING LAYER                                   │
│  StoryCast fills abstract roles with Nouns       │
│  CharacterTraits generalizes OutlawTraits        │
│  Lore template selection + AI generation         │
├─────────────────────────────────────────────────┤
│  SCENE LAYER                                     │
│  SchemaRunner orchestrates beat sequence         │
│  IStoryBeat implementations (wrapping existing   │
│  IOutlawQuest classes via adapter pattern)       │
│  AI prompt generation (parameterized by schema)  │
├─────────────────────────────────────────────────┤
│  POLISH LAYER                                    │
│  WritingPolishPass (extended)                    │
│  Schema-aware evaluation criteria                │
│  Cast-aware consistency checking                 │
├─────────────────────────────────────────────────┤
│  WORLD LAYER                                     │
│  Existing spatial generation (unchanged)         │
│  StationDungeonGenerator                         │
│  WorldspaceDungeonGenerator                      │
│  SpaceCellGenerator                              │
└─────────────────────────────────────────────────┘

Cross-cutting:
  NounRegistry + NounGraph (relationships)
  AITools (RunPrompt / RunStateless / InjectContext)
  SeedData pools (randomization)
```

### 4.1 Schema Layer — Story Structures in Data

A schema is a YAML file that declares the shape of a story. The runtime reads it, the orchestrator executes it. No C# needed for new story types (as long as they use existing mechanical beat types).

```yaml
# questgen_quests/schemas/bounty_hunt.yaml
schema: bounty_hunt
display_name: "Bounty Hunt"
description: "Track and eliminate a wanted target"

roles:
  target:
    type: hostile_npc
    required: true
    trait_pool: outlaw         # which seed data pools to use
  employer:
    type: implicit             # bounty board, not a named NPC
    required: false

stakes:
  player_motivation: "Payment for completing the contract"
  failure_consequence: "Target escapes to cause more harm"
  player_role: "bounty hunter" # injected into AI prompts

beats:
  - id: discovery
    function: introduce_problem
    mechanical_types: [read_dataslate, find_poster]
    count: [1, 1]
    progress_range: [0, 10]
    produces: [target_name, first_lead]

  - id: investigation
    function: gather_evidence
    mechanical_types:
      - talk_npc_city
      - talk_npc_planet
      - activate_clue_space
      - activate_clue_planet
      - activate_clue_city
      - activate_clue_station
      - explore_derelict
      - informant_space
      - informant_planet
    count: [2, 5]
    progress_range: [15, 75]
    requires: [target_name]
    produces: [location_lead, evidence]
    variety: different_environments

  - id: showdown
    function: climax
    mechanical_types: [kill_target_planet, kill_target_city]
    count: [1, 1]
    progress_range: [85, 100]
    requires: [location_lead]

arc:
  order: [discovery, investigation, showdown]
  bridges: true

lore_template: outlaw_bounty
```

A rescue schema using the same format:

```yaml
# questgen_quests/schemas/rescue.yaml
schema: rescue
display_name: "Search and Rescue"
description: "Find and extract a missing person"

roles:
  missing_person:
    type: friendly_npc
    required: true
    trait_pool: civilian
  captor:
    type: hostile_npc
    required: false            # might be environmental danger
    trait_pool: outlaw

stakes:
  player_motivation: "Someone needs help — you're the closest"
  failure_consequence: "Missing person's fate remains unknown"
  player_role: "rescuer"

beats:
  - id: alert
    function: introduce_problem
    mechanical_types: [read_dataslate, talk_npc_city]
    count: [1, 1]
    progress_range: [0, 10]
    produces: [missing_person_name, last_known_location]

  - id: search
    function: gather_evidence
    mechanical_types:
      - talk_npc_city
      - talk_npc_planet
      - activate_clue_space
      - explore_derelict
      - informant_space
    count: [1, 3]
    progress_range: [15, 65]
    requires: [missing_person_name]
    produces: [location_lead, situation_details]

  - id: locate
    function: escalation
    mechanical_types: [activate_clue_space, talk_npc_planet]
    count: [1, 1]
    progress_range: [70, 80]
    requires: [location_lead]
    produces: [extraction_location]

  - id: extraction
    function: climax
    mechanical_types: [rescue_station, rescue_planet]
    count: [1, 1]
    progress_range: [85, 100]
    requires: [extraction_location]

arc:
  order: [alert, search, locate, extraction]
  bridges: true

lore_template: missing_person
```

**Why YAML and not C#?** Adding a rescue schema should be a YAML file + a lore template. The mechanical beat types (`talk_npc_city`, `activate_clue_space`) already exist. The only time you write C# is when you need a genuinely new *mechanical* type (like `rescue_station`).

**What stays in C#:** The mechanical beat implementations *must* be C# because they create Mutagen records, wire scripts, set aliases, and handle Starfield-specific concerns. Data files define *what happens in what order*. C# defines *how each thing becomes Starfield records*.

### 4.2 Casting Layer — Filling Roles with Nouns

Takes a schema's abstract roles and produces concrete game characters.

```csharp
/// <summary>
/// Generalized character traits — works for any story role, not just outlaws.
/// </summary>
public class CharacterTraits
{
    public string Upbringing           = string.Empty;
    public string Fear                 = string.Empty;
    public string Goal                 = string.Empty;
    public string Flaw                 = string.Empty;
    public string Quirk                = string.Empty;
    public string Occupation           = string.Empty;
    public string DefiningEvent        = string.Empty;  // generalized Crime
    public string AssociatedFaction    = string.Empty;  // generalized HuntingFaction
    public string CurrentPreoccupation = string.Empty;

    /// <summary>
    /// Generate traits appropriate for the given role and trait pool.
    /// </summary>
    public static CharacterTraits Generate(string traitPool)
    {
        var traits = new CharacterTraits
        {
            Upbringing           = NpcSeedData.GetUpbringing(),
            Fear                 = NpcSeedData.GetFears(),
            Goal                 = NpcSeedData.GetGoals(),
            Flaw                 = NpcSeedData.GetFlaws(),
            Quirk                = NpcSeedData.GetQuirk(),
            Occupation           = StorySeedData.Occupations.Pick(),
            AssociatedFaction    = FactionSeedData.GetCombatFaction(),
            CurrentPreoccupation = NarrativeSeedData.LogFocusPoints.Pick(),
        };

        // Pool-specific: what the "defining event" means for this role
        traits.DefiningEvent = traitPool switch
        {
            "outlaw"   => StorySeedData.Crimes.Pick(),
            "civilian" => StorySeedData.Disappearances.Pick(),   // new pool
            "faction"  => StorySeedData.Grievances.Pick(),       // new pool
            _          => StorySeedData.Events.Pick(),           // generic fallback
        };

        return traits;
    }

    public void AppendToPrompt(StringBuilder sb)
    {
        sb.AppendLine($"- Background: {Upbringing}");
        sb.AppendLine($"- Core fear: {Fear}");
        sb.AppendLine($"- Goal: {Goal}");
        sb.AppendLine($"- Personality flaw: {Flaw}");
        sb.AppendLine($"- Behavioural quirk: {Quirk}");
        sb.AppendLine($"- Former occupation: {Occupation}");
        sb.AppendLine($"- Defining event: {DefiningEvent}");
        sb.AppendLine($"- Associated with: {AssociatedFaction}");
        sb.AppendLine($"- Currently preoccupied with: {CurrentPreoccupation}");
    }
}
```

```csharp
/// <summary>
/// A character in the story, identified by their narrative role.
/// </summary>
public class StoryRole
{
    public string RoleId;            // "target", "informant_1", "employer"
    public string RoleType;          // from schema: "hostile_npc", "friendly_npc", "implicit"
    public CharacterTraits Traits;
    public INoun? NounInstance;       // the generated Noun (OutlawNpc, etc.)
    public string Name;              // display name
    public bool IsFemale;
}

/// <summary>
/// The full cast of a story — all characters, accessible by role.
/// Built before any quest content is generated so every beat can
/// reference any cast member by role.
/// </summary>
public class StoryCast
{
    public Dictionary<string, StoryRole> Roles { get; } = new();

    public StoryRole GetRole(string roleId) => Roles[roleId];

    /// <summary>
    /// Inject all cast members into an AI prompt for context.
    /// </summary>
    public void AppendToPrompt(StringBuilder sb)
    {
        sb.AppendLine("## Story Characters");
        foreach (var (id, role) in Roles)
        {
            if (role.RoleType == "implicit") continue;
            sb.AppendLine($"\n### {role.Name} ({id})");
            role.Traits.AppendToPrompt(sb);
        }
    }
}
```

**Backward compatibility:** `OutlawTraits` and `OutlawNpc` continue to exist. An adapter maps `OutlawNpc` to a `StoryRole` with `RoleId = "target"`, copying traits field-by-field. No existing code breaks.

### 4.3 Scene Layer — SchemaRunner + IStoryBeat

The SchemaRunner replaces `LoopingLayoutQuestChain` as the orchestrator. It reads a schema, builds a cast, and executes beats in order.

```csharp
/// <summary>
/// Generalized beat interface. The adapter pattern bridges existing
/// IOutlawQuest implementations to this interface.
/// </summary>
public interface IStoryBeat
{
    Quest Setup(StarfieldMod mod, StoryCast cast, BeatContext context, IStoryBeat? nextBeat);
    string LogMessage { get; set; }
    string QuestLocation { get; set; }
    Quest questform { get; set; }

    IEnumerable<IPolishable> GetPolishables()
    {
        if (questform != null)
            yield return new QuestLogPolishable(questform);
    }

    void StageAudio() { }
}

/// <summary>
/// Everything a beat needs to know about its narrative context.
/// </summary>
public class BeatContext
{
    public MissionTemplate Template;
    public string SchemaId;            // "bounty_hunt", "rescue"
    public string NarrativeFunction;   // "introduce_problem", "gather_evidence", "climax"
    public int ProgressPercent;        // 0-100
    public string PlayerRole;          // "bounty hunter", "rescuer"
    public List<string> Addons;
    public StoryCast Cast;
}
```

```csharp
/// <summary>
/// Wraps any existing IOutlawQuest as an IStoryBeat.
/// The cast's "target" role maps to the OutlawNpc parameter.
/// </summary>
public class OutlawQuestAdapter : IStoryBeat
{
    private readonly IOutlawQuest _inner;

    public OutlawQuestAdapter(IOutlawQuest inner) => _inner = inner;

    public Quest Setup(StarfieldMod mod, StoryCast cast, BeatContext ctx, IStoryBeat? nextBeat)
    {
        var outlawNpc = (OutlawNpc)cast.GetRole("target").NounInstance!;
        var nextInner = (nextBeat as OutlawQuestAdapter)?._inner;
        return _inner.Setup(mod, outlawNpc, ctx.Template, nextInner!);
    }

    public string LogMessage { get => _inner.LogMessage; set => _inner.LogMessage = value; }
    public string QuestLocation { get => _inner.QuestLocation; set => _inner.QuestLocation = value; }
    public Quest questform { get => _inner.questform; set => _inner.questform = value; }

    public IEnumerable<IPolishable> GetPolishables() => _inner.GetPolishables();
    public void StageAudio() => _inner.StageAudio();
}
```

The SchemaRunner flow (pseudocode — same structure as `LoopingLayoutQuestChain` but driven by schema data):

```
1. Load schema YAML
2. Build StoryCast from schema.roles
3. Load lore template for schema.lore_template
4. AI: GenerateLoreContext (RunPrompt) — accumulates history
5. AI: SelectBeatTypes for each slot (RunStatelessPrompt) — picks mechanical types
6. For each beat slot (reverse order, same as current):
   a. Look up IStoryBeat from BeatRegistry by mechanical type
   b. Build BeatContext with schema metadata
   c. Call beat.Setup(mod, cast, context, nextBeat)
   d. Generate StageBridge to next beat (RunPrompt)
7. WritingPolishPass.Run(allPolishables) — with schema-aware criteria
8. For each beat: StageAudio()
9. SpeechTools.ConvertAndDeploy()
```

### 4.4 Polish Layer — Schema-Aware Refinement

`WritingPolishPass` already works well. Extensions for a systemic system:

**Schema-specific evaluation criteria.** The `PolishPrompts.Build()` method currently describes the arc as "Discovery → Investigation → Showdown." With schemas, it describes the arc using the schema's beat names and narrative functions. A rescue schema would instruct the AI to enforce urgency. A mystery would instruct the AI to plant and pay off clues.

**Cast consistency checking.** The polish prompt could include the full cast sheet so the AI can verify character names, traits, and relationships are consistent across all polishable text.

**Emotional arc enforcement.** Each schema could define a tension curve. The polish prompt would include guidance like "tension should build through the search beats and peak at extraction" rather than the generic "narrative flow" criterion.

```csharp
// Extension to PolishPrompts — accept schema context
public static string Build(List<IPolishable> polishables, StorySchema schema, StoryCast cast)
{
    var sb = new StringBuilder();

    // Schema-specific arc description
    sb.AppendLine($"This is a {schema.DisplayName} story.");
    sb.AppendLine($"The player is a {schema.Stakes.PlayerRole}.");
    sb.AppendLine($"Stakes: {schema.Stakes.FailureConsequence}");

    // Cast sheet for consistency
    cast.AppendToPrompt(sb);

    // Schema-specific evaluation criteria
    sb.AppendLine(schema.GetPolishGuidance());

    // ... existing polishable listing code ...
}
```

### 4.5 World Layer — Unchanged

The existing spatial generation system (56 passes across station, worldspace, and space cell generators) remains untouched. The schema system's output — quest records, NPC records, book records — gets placed into the world using the same `PlacementUtil` and PCM routing as today.

One potential extension: the schema could influence location distribution constraints. "No two beats in the same star system" or "the climax must be on a different planet." But this is an optimization, not a requirement.

---

## 5. The Noun Graph

### Why Nouns need relationships

Currently Nouns are isolated. An `OutlawNpc` doesn't know about the `QuestNoun` that hunts it. A `BookNoun` doesn't know which `NPCDialogueNoun` references it. The `NounRegistry` is a flat list.

For a systemic story system, relationships matter because:

1. **Narrative coherence** — if NPC-A tells the player about NPC-B, the dialogue generation for NPC-A needs NPC-B's name and traits. Currently this flows through AI history, which is effective but fragile.
2. **Cross-story connections** — an NPC who appears as an informant in one story could be referenced in another, creating persistent-world illusion.
3. **Polish consistency** — the polish pass could check that all references to a character use the same name and describe the same traits.

### Lightweight relationship model

Not a graph database — just a list of triples on the `NounRegistry`.

```csharp
public class NounRelationship
{
    public INoun Source;
    public string RelationType;  // "knows", "employs", "opposes", "evidence_of", "located_at"
    public INoun Target;
    public string Context;       // optional: "informant told player about target"
}

public class NounGraph
{
    private readonly List<NounRelationship> _relationships = new();

    public void Add(INoun source, string type, INoun target, string context = "")
        => _relationships.Add(new NounRelationship { Source = source, RelationType = type, Target = target, Context = context });

    public IEnumerable<INoun> GetRelated(INoun source, string type)
        => _relationships.Where(r => r.Source == source && r.RelationType == type).Select(r => r.Target);

    /// <summary>
    /// Build a cast sheet for AI prompts from the graph.
    /// </summary>
    public string GetCastSheet()
    {
        var sb = new StringBuilder();
        foreach (var group in _relationships.GroupBy(r => r.Source))
        {
            sb.AppendLine($"- {group.Key.EditorID}:");
            foreach (var r in group)
                sb.AppendLine($"    {r.RelationType} → {r.Target.EditorID} ({r.Context})");
        }
        return sb.ToString();
    }
}
```

**Usage during generation:**

```csharp
// When a beat creates an informant NPC who knows about the target:
graph.Add(informantNoun, "knows", targetNoun, "dock worker who processed their cargo");
graph.Add(bookNoun, "evidence_of", targetNoun, "shipping manifest with target's alias");
```

The graph feeds into AI prompts as structured context, complementing the conversation history.

### What the graph should NOT do

- Drive quest generation on its own (that's the schema's job)
- Model every possible relationship (keep it to story-relevant connections)
- Replace the AI's role in generating narrative connections

---

## 6. Unsolved Problems

Phase 1 proved the schema architecture works structurally, but a test run and codebase audit revealed six fundamental quality problems that must be solved before the system can produce shippable content.

### 6.1 Problem: Output Length and Style Violations

**Observed:** Lore entries ran to full paragraphs despite "2-3 sentences" rules. Quest names contained special characters (`—`, `:`, `'`) despite "no punctuation" rules. Log messages exceeded 50-word targets.

**Root cause:** All format constraints live in prompt text. The AI treats them as suggestions, not hard limits. The only enforcement is crude length-based retry loops (`results.Length > 200`), which don't catch format violations — a 190-character quest name with colons passes the length check but breaks Starfield's UI.

**Why prompts alone can't fix this:** LLMs are probabilistic. A rule like "2-4 words, no punctuation" is competing with the model's training distribution where titles commonly use punctuation. Increasing prompt emphasis helps but never guarantees compliance. The only guarantee is post-generation validation.

### 6.2 Problem: Information Leakage Between Stages

**Observed:** Discovery mission log entries referenced the showdown location. Investigation dialogue mentioned details from other investigations the player hadn't visited yet.

**Root cause:** All beats share a single AI conversation history. The generation timeline shows exactly where leakage enters:

```
Timeline of what the AI knows during generation:

1. LORE GENERATION (RunPrompt → enters history)
   AI knows: seed traits, quest theme
   After: full LoreContext in history

2. ARC SELECTION (RunPrompt → enters history)
   AI knows: all template names + locations (from menu)
   After: PlannedArc with ALL stage locations injected as system context

3. SHOWDOWN GENERATION (RunPrompt → enters history)
   AI knows: showdown location, template name
   After: showdown log message in history, then "don't reveal location" warning

4. INVESTIGATION[0] — closest to showdown (RunPrompt → enters history)
   Bridge prompt: "...points toward the Showdown stage at {SHOWDOWN_LOCATION}"
   ← EXPLICIT LEAK: showdown location sent directly in prompt text
   AI knows: everything from steps 1-3 + showdown location in bridge
   After: investigation log, dialogue, bridge text all in history

5. INVESTIGATION[N] — each earlier investigation
   Bridge prompt: "...points toward {NEXT_INVESTIGATION_LOCATION}"
   ← EXPLICIT LEAK: next stage location in prompt
   AI knows: everything from all previous steps
   After: more location-containing content in history

6. DISCOVERY (RunPrompt → enters history)
   AI knows: EVERYTHING — all locations, all log messages, all dialogue
   Bridge prompt: "...points toward {FIRST_INVESTIGATION_LOCATION}"
   ← The discovery stage sees the entire quest's content
```

The "don't reveal showdown location" warning (injected after step 3) is a prompt-engineering band-aid. The AI has already seen the location in the history. It's asking the model to voluntarily forget information it has access to.

**Three leakage vectors:**

1. **Bridge prompts contain explicit destination locations** — `GenerateStageBridge()` sends `toTemplate.Location` directly in the prompt text
2. **RunPrompt accumulates history** — every log message, dialogue script, and bridge enters history visible to all subsequent calls
3. **PlannedArc injection** — the full arc with template names and narrative themes is injected as system context, visible to every beat

### 6.3 Problem: Uncontrolled Invention

**Observed:** Investigation dialogue invented NPC names, faction names, and location details not present in the lore context or beat template. A conversation at Neon referenced "Captain Vasquez at the Meridian Import desk" — neither the character nor the business exists in the lore or any Starfield content.

**Root cause:** The AI receives broad context (LoreContext + template info) and is free to generate any detail that seems plausible. The only constraint is "Use the LoreContext established earlier" — which means the AI can't contradict it, but CAN freely extend it with invented specifics.

**Why this matters for Starfield:** Invented names create dead references. If dialogue says "ask Captain Vasquez," the player will look for an NPC that doesn't exist. If a data-slate mentions "the Meridian Import warehouse," the player expects to find it. Every invented detail is a promise the game world can't keep.

The existing `NpcBackground` field on MissionTemplate ("dock manifest clerk, seen too much, says little") is the right idea — it constrains the NPC to a role rather than inventing a name. But it's optional and only covers one axis. Location details, objects, faction references, and supporting characters are all unconstrained.

### 6.4 Problem: Flowery Language Over Factual Correctness

**Observed:** Log entries read like novel excerpts instead of field notes. Dialogue NPCs delivered atmospheric monologues instead of answering the question. Quest names used evocative metaphors instead of plain descriptions. Lore sections expanded 2-3 sentence instructions into full paragraphs.

**Examples of the failure mode:**

```
BAD (flowery, atmospheric, vague):
"The neon-drenched corridors of the station whispered secrets of a
desperate man's flight, his shadow lingering like a ghost in the
recycled air of the lower decks."

GOOD (factual, specific, actionable):
"Docking records show Harlan boarded a cargo shuttle to Neon three
days ago. Manifest lists forged transit papers under a false name."
```

```
BAD quest name:  "Whispers in the Void"
GOOD quest name: "Neon Cargo Raid"
```

**Root cause:** LLMs are trained on published fiction, journalism, and marketing copy — all genres that reward vivid description. When asked to "write a log entry," the model's training distribution pulls toward atmospheric prose. Prompt instructions like "plain declarative sentences, no metaphor" compete against millions of training examples that do the opposite. The model follows the instruction *most of the time* but drifts back toward its training distribution, especially on longer outputs.

**Why this matters more than it seems:** Flowery text isn't just a style preference — it directly undermines the other three problems:
- **Invention** — atmospheric filler requires inventing details to fill the atmosphere ("neon-drenched corridors," "recycled air of the lower decks" are invented specifics)
- **Leakage** — verbose text has more surface area for accidentally including information from the wrong stage
- **Length violations** — prose naturally runs longer than factual statements

Fixing the style problem reinforces all three other fixes. A system that produces terse, factual text is also a system that invents less, leaks less, and stays within length limits.

### 6.5 Problem: Systemic Seed Data Bias

An audit of every randomized string pool in the codebase reveals a one-directional bias: every pool pushes toward literary, emotional, or dramatic output. There are zero mundane, factual, or terse counterweights anywhere.

**Severity: HIGH — affects every generated NPC, quest, and text artifact.**

| Pool | File | Count | Bias |
|------|------|-------|------|
| Weapon lore descriptors | `AISeedData.cs` | 38 | Poetic combat language — *"brutal impact"*, *"channeling focused devastation"*, *"resolving disputes explosively"* |
| Resource lore descriptors | `AISeedData.cs` | 55 | Anthropomorphized materials — *"sustaining alien life"*, *"shaping industrial foundations"* |
| Medical/chem descriptors | `AISeedData.cs` | 40 | Romanticized pharmacology — *"euphoric"*, *"enhancing perception"*, *"controlled hypothermic calm"* |
| LogFocusPoints | `NarrativeSeedData.cs` | 28 | All emotional crises — *"the moment they knew they were finished"*, *"fear of destroying that"*. Zero mundane options like "supply status" or "job debrief" |
| TransmissionTypes | `NarrativeSeedData.cs` | 31 | All gothic "final hours" scenarios — *"recorded in their final hours"*, *"someone who had stopped expecting rescue"*. No routine logs or status reports |
| Upbringings | `NpcSeedData.cs` | 52 | ~90% hardship/trauma — *"scraping by"*, *"shuffled through transitional housing"*, *"failed colony"*. Only 2-3 neutral options |
| Fears/Phobias | `NpcSeedData.cs` | 49 | Elaborately introspective — *"the sensation of unseen eyes follows them into sleep"*, *"triggers something deeper than anxiety"* |
| Quirks | `NpcSeedData.cs` | 53 | ~80% trauma/paranoia responses — *"keeps a specific exit route planned"*, *"avoids mirrors"*. Only ~10% neutral behavioral tics |
| Crimes | `StorySeedData.cs` | 36 | Psychologically motivated — every crime includes sympathetic framing (*"fled"*, *"to hide a costly mistake"*, *"coerced"*) |
| Motives | `StorySeedData.cs` | 31 | All desperate/sympathetic — *"medical costs"*, *"abusive situation"*, *"grief"*. No rational motives like "wanted more credits" or "thrill-seeking" |
| PersonalityTraits | `StorySeedData.cs` | 30 | Every trait internally conflicted — *"panics when cornered"*, *"deeply ashamed"*, *"quietly desperate"*. No straightforward personalities |
| Gang names | `GangSeedData.cs` | 100+ | All gothic noir — *"Neon Fangs"*, *"Void Cutters"*, *"Chrome Reapers"*. No mundane names |

**Cumulative effect:** Before the AI writes a single word, the seed data has already framed every NPC as a traumatized victim, every crime as a sympathetic act of desperation, every found log as a gothic tragedy, and every piece of equipment in poetic terms. The AI mirrors what it's given — when every input is literary, every output will be literary.

**The fix is not to replace these pools but to balance them.** The emotional entries are valuable for the outlaw's personal log and specific atmospheric beats. But quest log entries, NPC informant dialogue, and investigation clues need terse/factual seed data options that currently don't exist.

### 6.6 Problem: Prompt Instruction Conflicts

Multiple prompt classes issue contradictory instructions within the same AI conversation. Because `RunPrompt()` accumulates a shared history, later instructions overwrite earlier constraints. The AI resolves conflicts by favoring the most recent or most emphatic instruction.

**Five critical contradictions identified:**

**1. PolishPrompts rewrites QuestPrompts constraints.**
- `QuestPrompts.cs` says: *"Style: field intel note — plain declarative sentences, no metaphor, no atmospheric writing."*
- `PolishPrompts.cs` evaluates for: *"Narrative flow"*, *"tension"*, *"each piece earns its place in the arc"*
- Polish runs up to 15 iterations via `WritingPolishPass`. Each pass drifts the text further from factual toward literary. A terse log entry accumulates atmospheric language across iterations.

**2. DialoguePrompts refinement pass contradicts its own generation pass.**
- Generation (line 56): *"Tone: grounded, Starfield-style — terse, believable, not dramatic."*
- Refinement (lines 98-116): *"Writing sharpness"*, *"NPC voice distinctiveness"*
- Refinement uses `RunPromptHighQuality()` (more creative model), amplifying the contradiction. Direct dialogue gains interpretive layers: *"North sector"* becomes *"North sector — she had contacts there."*

**3. FlavourSeedData prepended to outlaw log overrides NarrativePrompts.**
- `NarrativePrompts.GetOutlawLogfile()` builds a prompt with emotional core from traits
- `FlavourSeedData.AddFlavourToTargetBook()` prepends 1-5 random paranoia/decay directives before the AI sees the base prompt
- Prepended instructions are seen first and treated as primary. The system knows this causes problems — there's a fallback that retries without flavour if the first attempt triggers a refusal.

**4. AISeedData "enrich" instruction undermines all downstream constraints.**
- System prompt (`AISeedData.cs`): *"use them to enrich each scene"*
- Every subsequent prompt that says "plain declarative" or "no metaphor" is fighting the foundation instruction
- System-level instructions prime all downstream outputs. The AI defaults to embellishment because the first instruction it received told it to enrich.

**5. Intrigue injection contradicts NPC knowledge constraint.**
- `DialoguePrompts.cs` line 57: *"in exactly ONE NPC line let a concrete unasked-for detail land without comment"*
- `DialoguePrompts.cs` line 58-59: *"NPC knows only what someone in their job and location would personally witness"*
- Logical paradox: the NPC must volunteer information they have no reason to mention. Creates artificial, evasive-feeling dialogue.

**The pattern:** Every conflict follows the same shape — a specific prompt constrains toward terse/factual, then a later or broader prompt undoes it with vague creative directives ("enrich", "narrative flow", "distinctiveness"). The creative instruction always wins because LLMs default to creative writing when given ambiguous instructions.

---

## 7. Proposed Solutions

### 7.1 Solution: Output Validators

Every AI-generated text passes through a programmatic validator before acceptance. Validators are per-content-type: quest names, log messages, dialogue lines, book text.

```
AI generates text → Validator checks → Pass → accept
                                      → Fail → retry with error feedback (max 3)
                                              → still fails → clean programmatically
```

**Validator rules — format:**

| Content Type | Rule | Check | On Failure |
|---|---|---|---|
| Quest name | Length | 2-40 chars | Retry |
| Quest name | No punctuation | Regex `[^\w\s]` | Strip chars |
| Quest name | Word count | 2-4 words | Retry |
| Log message | Length | 40-60 words | Retry with "exactly 50 words" |
| Log message | No label prefix | Doesn't start with `\w+:` | Strip prefix |
| Dialogue line | Length per segment | < 250 chars (Starfield limit) | Split |
| Lore section | Sentence count | 2-3 per section | Retry |
| Any text | No markdown | No `**`, `##`, `-` lists | Strip formatting |
| Any text | No invented names | Cross-ref against BeatFacts vocabulary | Retry with explicit list |

**Validator rules — style (anti-flowery):**

| Content Type | Rule | Check | On Failure |
|---|---|---|---|
| Any text | Adjective density | > 1 adjective per sentence avg | Retry: "Too many adjectives. State facts, not descriptions." |
| Any text | No similes/metaphors | Regex for "like a", "as if", "as though", "-drenched", "-soaked" | Retry: "Remove metaphor: '{match}'. Use plain statement instead." |
| Any text | No atmosphere filler | Blocklist: "whispered", "echoed", "loomed", "shadows", "silence hung" | Retry: "Remove atmospheric filler: '{match}'. State what happened." |
| Any text | Fact density | Every sentence must contain a BeatFact reference (name, location, object, or action) | Retry: "Sentence {n} contains no fact. Every sentence must state something concrete." |
| Log message | Starts with action | First word must be a verb or proper noun | Retry: "Start with the action or subject, not atmosphere." |
| Dialogue | Answers the question | Response must address the player's implied question | Retry: "NPC must answer what the player asked, not deliver a monologue." |

The style validators are **the enforcement layer** for "simple but factual." Prompt instructions say "plain declarative sentences" — the validator ensures compliance. The key difference from prompt-only rules: the validator gives the AI *specific, targeted feedback* on exactly which phrase failed and why, rather than restating the general rule.

Example retry feedback:
```
Your previous output was rejected:
- Sentence 2: "The recycled air carried whispers of his desperate flight"
  → contains atmospheric filler ("whispers", "desperate") and no BeatFact.
  Replace with a factual statement using these available facts:
  Location: Neon docking bay. Object: forged transit papers. Clue: boarded
  cargo shuttle three days ago.
```

This is far more actionable than "please write in a plainer style."

```csharp
public interface IOutputValidator
{
    /// <summary>
    /// Returns null if valid, or an error message describing the violation.
    /// The error message is appended to the retry prompt so the AI
    /// knows exactly what to fix.
    /// </summary>
    string? Validate(string output);
}

public class ValidatedPrompt
{
    public static string Run(string prompt, IOutputValidator validator, int maxRetries = 3)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            string result = AITools.RunIsolatedPrompt(context, prompt);
            string? error = validator.Validate(result);
            if (error == null) return result;

            if (attempt < maxRetries)
                prompt += $"\n\nYour previous output was rejected: {error}\nPlease fix and try again.";
        }
        // Final fallback: programmatic cleanup
        return validator.CleanupBestEffort(result);
    }
}
```

**Key insight:** The retry prompt includes the *specific validation error*, not just "try again." This gives the AI targeted feedback: "Output was 73 words, must be under 60" is far more actionable than re-sending the same prompt.

### 7.2 Solution: Air-Gapped AI Contexts (Context Envelopes)

**Principle:** No beat should see any AI output from any other beat. Each beat gets a sealed context envelope containing only the information it's authorized to use.

#### 7.2.1 The Back-to-Front Constraint

Quests are generated in **reverse narrative order** (Showdown → Investigations → Discovery). This is a hard technical requirement, not a narrative choice.

**Why:** Each quest's Papyrus scripts need a FormLink to the *next* quest in the player's story order. Mutagen allocates FormKeys at construction time — you can't reference a FormKey that doesn't exist yet. So the showdown (which has no successor) must be created first, then each earlier quest receives the successor's FormKey via the `nextQuest` parameter.

**How each beat type uses `nextQuest`:**

| Beat Type | From `nextQuest` | Mutagen Property Set |
|---|---|---|
| Showdown | Nothing (`null`) | — |
| Investigation (conversation) | `.questform` | `duout_onstagenext_quest.nextquest` |
| Investigation (activator) | `.questform` + `.QuestLocation` | `duout_activator_completenstart.nextquest` |
| Investigation (book-delivery) | `.questform` + `.QuestLocation` | `duout_queststart.QuestToStart` |
| Discovery (dataslate) | `.questform` + `.LogMessage` | `duout_queststart.QuestToStart` + LeveledItem condition |
| Discovery (wanted poster) | `.questform` + `.QuestLocation` | `duout_activator_completenstart.nextquest` |

**Two kinds of "next stage knowledge":**

Beats use `nextQuest` for two fundamentally different purposes:

1. **Record wiring** (FormKeys, script properties) — purely mechanical, no AI involved, MUST flow back-to-front. Zero leakage concern.

2. **Clue grounding** — several beats feed `nextQuest.QuestLocation` into AI prompts for pickup messages ("a lead pointing toward Neon Strip"). This is *legitimate* player knowledge. The whole point of a clue is telling the player where to go next.

The problem was never that beats know the next location for clue-writing. The problem is the **shared conversation history** causing that knowledge to leak into content where it doesn't belong (log messages, NPC dialogue, lore text).

**Implication for air-gapped design:** `nextQuest.QuestLocation` becomes an explicit BeatFact: `"nextDestination": "Neon Strip"`. It's available to the clue/pickup prompt but NOT to the log message or dialogue prompts — those get separate, more restrictive envelopes.

#### 7.2.2 Splitting Setup into Wiring and Content

Currently `IOutlawQuest.Setup()` mixes both concerns in a single method:

```
Current Setup():
  1. Generate quest name          ← AI content (needs isolation)
  2. Generate log message         ← AI content (needs isolation)
  3. Create Quest record          ← Mutagen wiring (needs nextQuest.questform)
  4. Set script properties        ← Mutagen wiring (needs nextQuest.questform)
  5. Generate dialogue/book       ← AI content (needs isolation)
  6. Generate pickup message      ← AI content (needs nextQuest.QuestLocation)
  7. Wire pickup script property  ← Mutagen wiring (needs nextQuest.questform)
```

The air-gapped design needs to separate these. Two approaches:

**Option A: Envelope-aware prompt classes.** Pass a `ContextEnvelope` through `BeatContext`. Modify `QuestPrompts`, `NarrativePrompts`, `DialoguePrompts` to accept an optional envelope — when present, they call `RunIsolatedPrompt` instead of `RunPrompt`. Existing `IOutlawQuest.Setup()` keeps its structure but the AI calls inside it become air-gapped. Backward compatible: without an envelope, behavior is unchanged.

**Option B: Two-pass generation.** First pass: generate all text content via isolated prompts, producing a `BeatContent` bundle (quest name, log message, dialogue script, book text, pickup message). Second pass: `Setup()` receives pre-generated text and only does Mutagen wiring — no AI calls at all. Cleaner separation but requires modifying every `IOutlawQuest` implementation to accept pre-generated text.

**Recommendation: Option A for Phase 1.5, migrate toward Option B over time.** Option A requires fewer changes to existing code and can be done incrementally per prompt class.

#### 7.2.3 New AI Primitive

```csharp
/// <summary>
/// Runs a prompt in a completely fresh AI context — no shared history.
/// The envelope is set as the system prompt. The user prompt is the
/// generation request. Nothing is added to the main conversation history.
/// </summary>
public static string RunIsolatedPrompt(ContextEnvelope envelope, string prompt)
```

This is different from all three existing call types:
- `RunPrompt` → shared history, reads AND writes
- `RunStatelessPrompt` → shared history, reads only (still sees everything)
- `InjectContextIntoHistory` → no API call, adds to shared history
- **`RunIsolatedPrompt`** → fresh context, reads nothing from shared history, writes nothing to it

#### 7.2.4 Context Envelope

```csharp
public class ContextEnvelope
{
    /// <summary>
    /// Frozen lore summary — generated once during setup.
    /// NOT the full LoreContext (which may contain too much detail).
    /// A curated subset: target name, crime, personality, factions involved.
    /// </summary>
    public string LoreSummary;

    /// <summary>
    /// Cast sheet — names, roles, traits. Read-only reference.
    /// </summary>
    public string CastSheet;

    /// <summary>
    /// This beat's explicit facts — the ONLY specific details the AI
    /// may reference. See section 7.3.
    /// </summary>
    public BeatFacts Facts;

    /// <summary>
    /// What the player knows at this point in the story.
    /// Built incrementally: each completed beat contributes
    /// a SHORT summary (1-2 sentences) of what the player learned.
    /// Contains NO information about future stages.
    /// </summary>
    public List<string> PlayerKnowledge;

    /// <summary>
    /// Schema-level rules for this content type.
    /// E.g., "You are writing for a bounty hunter. Style: terse field notes."
    /// </summary>
    public string ContentRules;
}
```

**Multiple envelopes per beat.** A single beat may need different envelopes for different content types:

| Content | Envelope contains |
|---|---|
| Log message | LoreSummary, CastSheet, BeatFacts (this beat), PlayerKnowledge (story order) |
| Dialogue | LoreSummary, CastSheet, BeatFacts (this beat), PlayerKnowledge (story order) |
| Pickup message | BeatFacts.NextDestination only — minimal context to write a directional clue |
| Bridge | Source location + destination location only |

The pickup message envelope is deliberately narrow. It needs to say "go to Neon Strip" but should NOT know why the player is going there or what they'll find.

#### 7.2.5 How PlayerKnowledge Works

PlayerKnowledge is a curated list built from BeatFacts, NOT from raw AI output. The Fact Planning Pass (section 7.3) generates a `ClueLearned` per beat. After each beat is generated, its `ClueLearned` is added to PlayerKnowledge in **story order**.

Since generation runs back-to-front but PlayerKnowledge flows front-to-back, we pre-compute the full PlayerKnowledge timeline before generating any beats:

```
Pre-computed from BeatFacts (story order):

  After Discovery:   ["Target {name} wanted for {crime}, last seen near {location}"]
  After Initial Inv: + ["Investigated {location}, learned {clue}"]
  After Deep Inv:    + ["Found {object} at {location}, confirming {detail}"]
  After Showdown:    + ["Confronted target at {location}"]
```

When generating a beat, it receives the PlayerKnowledge slice UP TO that beat's position in story order:

```
Generation order          Story position        PlayerKnowledge received
─────────────────         ──────────────        ────────────────────────
1. Showdown               Last (4/4)            FULL — all 3 prior entries
2. DeepInvestigation      Third (3/4)           2 entries (Discovery + Initial)
3. InitialInvestigation   Second (2/4)          1 entry (Discovery only)
4. Discovery              First (1/4)           EMPTY — player knows nothing
```

This is the **exact opposite** of the current system's leakage pattern. Today, the discovery (generated last) sees everything because of accumulated conversation history. In the air-gapped system, the discovery sees nothing because it's first in story order.

The showdown (generated first) sees the most PlayerKnowledge, which is correct — the showdown log entry should reflect the full journey. And since it's generated in isolation, its content can't leak backward into earlier beats.

#### 7.2.6 Bridge Generation

Bridges move from inline (generated between beats, leaking destinations) to a **separate post-beat pass** with minimal context.

```
Current (leaks destination details):
"Describe the clue at {source.Location} that points toward
the '{destination.Name}' stage at {destination.Location}."

Fixed (minimal information):
"The player is at {source.Location}. They need a reason to travel
to {destination.Location}. Describe a clue or lead they find at
{source.Location} — a data file, overheard conversation, or contact
by role only. Do not describe what the player will find at the
destination. 1-2 sentences only."
```

The bridge AI only knows: where the player IS and where they're GOING. Not why, not what template, not what stage type. Just two location names.

**Bridge timing and pickup messages.** Currently, some `IOutlawQuest` implementations use `nextQuest.QuestLocation` to generate pickup messages like "A manifest references cargo bound for {nextLocation}." Under the air-gapped system, this needs to come from the bridge — the pickup message IS the bridge, delivered through an in-game object.

This means bridges can't be fully deferred to a post-beat pass. Instead:

1. **Bridge text** is generated in a minimal-context isolated prompt (location pair only)
2. **Bridge text is available as a BeatFact** for the source beat's pickup message prompt
3. The source beat's `Setup()` receives the bridge via `BeatFacts.BridgeText` and wires it into the pickup object

The generation order becomes:

```
For each beat (back-to-front):
  1. Generate bridge TO this beat's successor (minimal context: location pair)
  2. Add bridge to this beat's BeatFacts
  3. Generate beat content (log, dialogue, book, pickup) in isolated context
  4. Wire Mutagen records (Setup)
```

This preserves back-to-front order while keeping bridges air-gapped. The bridge is generated before the beat content, so it's available as a BeatFact for the pickup message — but it was generated with only location info, not the full narrative context.

### 7.3 Solution: Beat Facts (Correctness Through Scarcity)

Every beat carries an explicit set of facts — the ONLY specific details the AI may reference. Anything not in the facts list is off-limits.

**The scarcity principle:** BeatFacts solve both invention AND flowery writing through the same mechanism — limiting the AI's input. Atmospheric prose requires raw material: you can't write "neon-drenched corridors whispered secrets" if your envelope doesn't contain corridors, neon, or secrets. When the AI only has 5 concrete facts, the output *must* be factual because there's nothing else to draw from.

This is more reliable than prompt instructions ("write plainly") because it's structural, not advisory. The AI literally cannot embellish what it doesn't have.

```csharp
public class BeatFacts
{
    /// <summary>
    /// The location name exactly as it should appear in output.
    /// </summary>
    public string Location;

    /// <summary>
    /// NPCs the AI may reference, by ROLE only (no names).
    /// E.g., "nervous food vendor", "UC security officer", "docking bay foreman"
    /// </summary>
    public List<string> AllowedNpcRoles;

    /// <summary>
    /// Objects the AI may reference.
    /// E.g., "encrypted data-slate", "cargo manifest", "docking permit"
    /// </summary>
    public List<string> AllowedObjects;

    /// <summary>
    /// Setting details the AI may use for atmosphere.
    /// E.g., "crowded market stalls", "dim service corridor", "noisy cantina"
    /// </summary>
    public List<string> SettingDetails;

    /// <summary>
    /// What the player learns at this beat — the clue or revelation.
    /// Stated as a fact, not a mystery.
    /// E.g., "target was seen buying forged transit papers three days ago"
    /// </summary>
    public string ClueLearned;

    /// <summary>
    /// Emotional tone for this beat.
    /// E.g., "tense, guarded", "frantic, urgent", "resigned, melancholy"
    /// </summary>
    public string Tone;
}
```

**Where do BeatFacts come from?**

A dedicated **Fact Planning Pass** runs after lore/arc selection but before any beat content is generated. This pass has full context (lore, cast, all template info) and produces structured facts for every beat. It runs in the shared phase (step 4 above), then the shared conversation is discarded.

```
Fact Planning Prompt:
"Given this lore context and the following quest arc, generate
specific facts for each beat. Rules:
- NPCs are described by ROLE only (no invented names)
- Objects must be plausible for the location
- Each beat's ClueLearned must logically lead to the next beat
- No beat should reference details from a later beat
- Use concrete, specific details — not vague atmosphere

Output format:
<BeatFacts beat="discovery">
  <Location>Neon Strip</Location>
  <NpcRoles>wanted poster vendor; dock security guard</NpcRoles>
  <Objects>bounty board terminal; transit record</Objects>
  <Setting>crowded commercial strip; neon signage; vendor stalls</Setting>
  <ClueLearned>target was last seen near the cargo docks</ClueLearned>
  <Tone>routine, businesslike</Tone>
</BeatFacts>
..."
```

The prompt constrains for each beat comes from the context envelope:

```
"You are writing a 50-word log entry for a bounty hunter.

ALLOWED VOCABULARY — use ONLY these specifics:
- Location: Neon Strip Market
- NPCs (by role only, no names): nervous food vendor
- Objects: encrypted data-slate, cargo manifest
- Setting: crowded market stalls, dim back corridors
- Clue: target was seen buying forged transit papers three days ago

Do not reference any person by name except the target ({targetName}).
Do not invent locations, businesses, NPCs, factions, or organizations
not listed above."
```

**Why role-only NPCs?** If the AI invents "Captain Vasquez," the player expects to find that NPC. If the dialogue says "a nervous food vendor," it's atmospheric flavor that doesn't create a false expectation. The game world already has unnamed ambient NPCs everywhere — role descriptions blend in naturally.

**Cross-referencing validation:** The output validator (7.1) can cross-check generated text against BeatFacts. Any word that looks like a proper noun (capitalized, not at sentence start, not the target's name) triggers a warning.

### 7.4 Putting It Together: The Full Pipeline

```
┌─────────────────────────────────────────────────────────────────┐
│  SHARED PHASE (one AI conversation, output frozen, then discarded)│
│                                                                   │
│  1. Generate LoreFile from seed traits               [RunPrompt] │
│  2. Expand LoreContext against NPC                   [RunPrompt] │
│  3. Select PlannedArc templates                      [RunPrompt] │
│  4. Plan BeatFacts for all beats                     [RunPrompt] │
│                                                                   │
│  → Outputs: LoreSummary, CastSheet, BeatFacts[], PlannedArc     │
│  → Pre-compute PlayerKnowledge timeline from BeatFacts           │
│  → Conversation history DISCARDED after this phase               │
├─────────────────────────────────────────────────────────────────┤
│  BEAT PHASE (back-to-front, each beat isolated)                  │
│                                                                   │
│  For each beat (Showdown → DeepInv → ... → InitialInv → Disc):  │
│                                                                   │
│    A. Generate bridge TO this beat's successor          [Isolated]│
│       envelope = { thisLocation, successorLocation }             │
│       → Stored as thisBeat.Facts.BridgeText                      │
│                                                                   │
│    B. Generate beat content                             [Isolated]│
│       envelope = { LoreSummary, CastSheet, thisBeat.Facts,       │
│                    PlayerKnowledge[story order ≤ this beat] }     │
│                                                                   │
│       log      = RunIsolatedPrompt(envelope, logPrompt)          │
│       dialogue = RunIsolatedPrompt(envelope, dialoguePrompt)     │
│       book     = RunIsolatedPrompt(envelope, bookPrompt)         │
│       pickup   = RunIsolatedPrompt(pickupEnvelope, pickupPrompt) │
│                                                                   │
│       Each output → Validator → accept / retry with feedback     │
│                                                                   │
│    C. Wire Mutagen records (Setup)                               │
│       Create Quest, set script properties,                       │
│       link nextQuest.questform (available — successor exists)    │
│                                                                   │
│  Note: Showdown has no successor → skip step A, nextQuest=null   │
├─────────────────────────────────────────────────────────────────┤
│  POLISH PHASE (sees all text, validates consistency)             │
│                                                                   │
│  Collect all generated text                                      │
│  RunIsolatedPrompt(fullContext, polishPrompt) × N iterations     │
│  Validators enforce final compliance                             │
├─────────────────────────────────────────────────────────────────┤
│  AUDIO PHASE (no AI, just TTS/Wwise)                             │
│                                                                   │
│  SpeechTools processes finalized text                             │
└─────────────────────────────────────────────────────────────────┘
```

**Why the bridge is generated first within each beat's loop:**

The bridge text ("a cargo manifest referencing shipments to Neon") needs to be available when generating the pickup message for this beat. But the bridge must be generated with minimal context (just two locations) to prevent leakage. Generating it as step A, before the beat's main content, means:
- The bridge is air-gapped (only knows source → destination locations)
- The bridge is available as a BeatFact for step B's pickup prompt
- The beat's content prompts can reference the bridge naturally

**Information flow during beat generation:**

```
                    Shared Phase outputs (frozen)
                              │
                    ┌─────────┴─────────┐
                    │                   │
              LoreSummary          BeatFacts[]
              CastSheet            PlayerKnowledge[]
                    │                   │
        ┌───────────┴───────┐    ┌──────┴──────┐
        │  Per-beat facts   │    │ Story-order  │
        │  (this beat only) │    │ knowledge    │
        │  + bridge text    │    │ (≤ this beat)│
        └───────────┬───────┘    └──────┬──────┘
                    │                   │
                    └────────┬──────────┘
                             │
                    ┌────────┴────────┐
                    │ ContextEnvelope │
                    │ (sealed, fresh) │
                    └────────┬────────┘
                             │
                    ┌────────┴────────┐
                    │ RunIsolatedPrompt│
                    │ (no shared state)│
                    └────────┬────────┘
                             │
                    ┌────────┴────────┐
                    │   Validator     │
                    │ (accept/retry)  │
                    └─────────────────┘
```

**API call count comparison:**

| Phase | Current System | New System |
|---|---|---|
| Lore + Arc | ~3-5 RunPrompt | ~4-5 RunPrompt (adds fact planning) |
| Per beat | ~2-6 RunPrompt (accumulating) | ~2-6 RunIsolatedPrompt (air-gapped) |
| Bridges | ~N RunPrompt (inline, leaks) | ~N RunIsolatedPrompt (pre-beat, minimal context) |
| Polish | ~15 RunStatelessPrompt | ~15 RunIsolatedPrompt |
| **Total** | Similar count | Similar count, but each call is smaller (no huge accumulated history) |

Air-gapping may actually *reduce* API cost because isolated contexts are smaller (no accumulated history means fewer input tokens per call).

### 7.5 Prompt Parameterization

The existing prompt classes (`QuestPrompts`, `NarrativePrompts`, `DialoguePrompts`) hardcode bounty-specific language. A systemic approach parameterizes them:

| Hardcoded Today | Schema Parameter |
|---|---|
| "bounty hunter" | `schema.stakes.player_role` |
| "outlaw target" | `cast.GetRole("target").Name + " (" + role_description + ")"` |
| "track and eliminate" | `schema.description` verb |
| "Kill the Outlaw target" | Schema's climax action description |
| "where the trail leads" | Schema's bridge prompt (varies by function) |

This keeps prompt engineering in C# (testable, tunable) while letting schemas customize the framing.

### 7.6 Solution: Structural Templates (Fill-in-the-Blank)

The most aggressive approach to factual correctness: don't ask the AI to "write" anything. Give it a rigid skeleton with slots. The AI fills slots — 5-15 words each — but the structure is fixed in C#.

**Why this works:** Open-ended prompts ("write a 50-word log entry") invite prose. The AI decides structure, tone, pacing, and content. Slot-filling prompts ("fill in {verb}, {location}, {reason}") constrain the AI to individual facts. There's no room for "the neon-drenched corridors whispered" because no slot asks for corridor atmosphere.

**Log message template:**

```
Template (C#):
"{action} {target_name} at {location}. {reason}. {lead}."

Slot prompts (each a separate RunIsolatedPrompt, 5-15 words max):
  action  → "One verb phrase for the player's objective at this stage"
  reason  → "One clause: why this matters, using only these facts: {BeatFacts}"
  lead    → "One clause: what clue or contact to pursue, from: {BeatFacts.ClueLearned}"

Assembled result:
"Track Harlan Voss at Neon docking bay. Transit records show forged
papers under a false name. A cargo handler may confirm the shuttle
destination."
```

**Dialogue template:**

```
Template (C#):
PLAYER: "{question}"
NPC: "{direct_answer}. {supporting_detail}. {deflection_or_warning}."

Slot prompts:
  question          → "What the player would ask this NPC, given: {BeatFacts}"
  direct_answer     → "NPC answers the question in one sentence, using: {BeatFacts}"
  supporting_detail → "One concrete detail the NPC adds, from: {BeatFacts.AllowedObjects}"
  deflection        → "NPC ends the conversation, personality: {BeatFacts.Tone}"
```

**Quest name template:**

```
Template: "{noun} {preposition} {location_word}"
   or:    "{verb} {target_word}"

Slot prompts:
  noun          → "One concrete noun from: {BeatFacts}" (e.g., "Cargo", "Transit", "Ledger")
  location_word → "One word for the location: {BeatFacts.Location}" (e.g., "Neon", "Cydonia")
  verb          → "One action verb for this stage" (e.g., "Track", "Seize", "Raid")
  target_word   → "One word for what's targeted" (e.g., "Manifest", "Witness", "Shipment")
```

**Trade-off:** Templates sacrifice creative range for guaranteed correctness. Every output matches the structure exactly. The AI fills blanks rather than composing — this is a feature, not a limitation.

**When to use templates vs. open prompts:**

| Content Type | Template? | Why |
|---|---|---|
| Quest name | Yes | Must be 2-4 words, factual, no punctuation — perfect for slots |
| Log message | Yes | Must be terse, factual, stage-locked — template prevents drift |
| Pickup message | Yes | Short, directional — just needs location and reason |
| Dialogue (functional) | Yes | Informant conversations are Q&A — template keeps NPC on-topic |
| Dialogue (character) | Hybrid | Target's personal log needs voice — use open prompt with tight BeatFacts envelope |
| Book text | Hybrid | Data-slates are factual (template), personal logs need voice (open) |
| Bridge text | Yes | Just two facts: source location + destination location |

**Hybrid approach:** The template defines structure, but one slot can be "open" (up to 20 words, AI-composed). This allows a touch of personality without losing structural control:

```
Template: "{factual_statement}. {open_color}."

Example output:
"Docking records confirm Harlan boarded a Neon-bound shuttle three
days ago. The cargo handler wouldn't say more."
```

The first sentence is fact-filled (template). The second is a brief, AI-composed touch of texture — but it's capped at one short sentence and validated for style.

### 7.7 How the Three Approaches Reinforce Each Other

The three correctness mechanisms work at different layers and compound:

```
┌──────────────────────────────────────────────────────┐
│  INPUT LAYER: BeatFacts (section 7.3)                │
│  Limits what the AI has to work with.                │
│  5 facts in → no room for invented atmosphere.       │
│  "Correctness through scarcity"                      │
├──────────────────────────────────────────────────────┤
│  STRUCTURE LAYER: Templates (section 7.6)            │
│  Controls the shape of the output.                   │
│  AI fills slots, not composes prose.                 │
│  "Correctness through constraint"                    │
├──────────────────────────────────────────────────────┤
│  OUTPUT LAYER: Validators (section 7.1)              │
│  Rejects anything that slips through.                │
│  Specific feedback on exactly what failed.           │
│  "Correctness through rejection"                     │
└──────────────────────────────────────────────────────┘
```

Any one layer alone is leaky. Together they're airtight:
- BeatFacts limits INPUT → fewer opportunities for embellishment
- Templates limit STRUCTURE → no room for prose even if the AI tries
- Validators catch OUTPUT → specific feedback drives targeted fixes

A flowery sentence would have to survive all three: be grounded in BeatFacts (layer 1), fit a template slot of 5-15 words (layer 2), and pass adjective density + atmosphere blocklist checks (layer 3). That's not impossible, but it's structurally unlikely.

---

## 8. Practical Path Forward

### Phase 1: Prove the architecture (zero new content) — DONE

**Goal:** Refactor the bounty hunt to use the new architecture with identical output.

**Completed:**
1. `CharacterTraits` extracted from `OutlawTraits` with backward-compatible aliases
2. `StoryCast` wraps `OutlawNpc` into a role-based cast
3. `bounty_hunt.json` encodes the current structure as schema data
4. `SchemaRunner` implements `IQuestchain`, driven by schema, produces identical output to `LoopingLayoutQuestChain`
5. `OutlawQuestAdapter : IStoryBeat` bridges all existing `IOutlawQuest` implementations
6. `QuestPrompts.GetLogMessage()` accepts `playerRole` parameter

**Files created:** `Core/Story/CharacterTraits.cs`, `StoryCast.cs`, `StorySchema.cs`, `IStoryBeat.cs`, `SchemaRunner.cs`, `questgen_quests/schemas/bounty_hunt.json`

### Phase 1.25: Remove codebase anti-patterns that sabotage text quality

**Goal:** Remove existing designs that actively produce flowery, incorrect, or leaked text. These are prerequisites — building air-gapped contexts on top of a system that injects psychological monologue directives into prompts is pointless.

**Priority order (most damage first):**

1. **Remove `FlavourSeedData.AddFlavourToTargetBook()`.** This method appends 1-5 random psychological directives (from a pool of 176) to the outlaw log prompt. Every option invites introspective literary prose: *"Include hints of romantic tension"*, *"Have them describe hearing whispers they can't explain"*, *"Include a moment where they confess they've been hiding their struggles."* There are zero factual options in the pool. The method is called from `NarrativePrompts.GetOutlawLogfile()` and is the single largest source of flowery output. **Action:** Delete the method body, return `BookPrompt` unmodified. If variation is needed later, it should come from BeatFacts, not random emotional injection.

2. **Remove `FlavourSeedData.GetQuestTheme()`.** Returns random genre labels (*"Cult infiltration psychological drama"*, *"Deep-space horror encounter"*, *"Interstellar noir detective case"*) injected into `LorePrompts.GenerateLoreContext()` at line 57. The AI reframes a simple crime story through the lens of whichever genre it draws — a cargo thief becomes a noir case, a minor embezzler becomes a horror encounter. **Action:** Remove the call from `LorePrompts.cs` line 57. If tonal direction is needed, replace with concrete constraints from the schema (`schema.stakes` fields) rather than genre templates.

3. **Rewrite `PolishPrompts` evaluation criteria.** Current criteria ask for *"narrative flow"*, *"plants hooks"*, *"builds tension"*, *"pays it off"*, *"earns its place in the arc"* — dramaturgical concepts that cause the AI to introduce literary framing during the improvement pass. **Action:** Replace evaluation bullets with factual-correctness metrics:
   - *"Every sentence states a concrete fact (name, location, action, object)"*
   - *"No adjectives that aren't physical descriptions"*
   - *"No metaphors, similes, or atmospheric filler"*
   - *"Matches established vocabulary (names, locations, factions from lore context)"*
   - *"Terse and direct — field intel, not fiction"*

4. **Add `RunIsolatedPrompt` to `AITools`/`IAITools`.** The static `AITools` class with its single `ClaudeAITools` provider is the root cause of information leakage. Every `RunPrompt` adds to one global conversation visible to all subsequent calls. **Action:** Add `RunIsolatedPrompt(string systemContext, string prompt)` to the `IAITools` interface and implement in `ClaudeAITools`. Creates a fresh message list, makes one API call, returns the result, writes nothing to the shared history. This is the prerequisite for all air-gapped work in Phase 1.5.

5. **Add factual tones to `NarrativeSeedData.LogTones`.** All 26 current tones demand psychologically rich writing (*"confessional and raw"*, *"manic and frenetic"*, *"hollow optimism"*). There is no neutral or factual option. **Action:** Add grounded tones to the pool: *"matter-of-fact — listing what happened, what they did, and what comes next"*, *"terse and practical — focused on logistics, not feelings"*, *"blunt — no self-pity, no justification, just the situation as they see it"*. The emotional tones can stay for the outlaw's personal log (which should have voice), but the pool needs balance.

6. **Remove `FlavourSeedData.GetConversationIntrigueDetail()` injection in `DialoguePrompts`.** At `DialoguePrompts.cs` line 57, a random "intrigue" directive is injected into every NPC conversation prompt. These are cryptic behavioral instructions (*"They answer a question you didn't ask"*, *"They seem to be testing you"*) that force the AI to write evasive, atmospheric dialogue instead of direct intel delivery. An NPC who should say *"The target was seen at Cydonia docks"* instead speaks in riddles. **Action:** Remove the `GetConversationIntrigueDetail()` call from `DialoguePrompts`. If NPC personality variation is needed, derive it from the character's role and traits, not random cryptic behavior injection.

7. **Remove `DialoguePrompts.RunRefinementPass()`.** At `DialoguePrompts.cs` lines 76-82 and 98-149, a second AI pass rewrites already-constrained dialogue using `RunPromptHighQuality()`. The refinement prompt asks for *"literary sophistication"* and *"natural conversation flow"*, which directly undoes the terse, factual constraints of the first pass. A dialogue line that correctly says *"Check the warehouse on level 3"* gets rewritten to *"You might want to take a look at what's been going on down in the warehouse — third level, if you catch my drift."* **Action:** Delete the refinement pass entirely. If quality improvement is needed, it should happen through the WritingPolishPass with factual-correctness criteria, not a separate literary sophistication pass.

8. **Rewrite `AISeedData` system prompt "enrich" instruction.** At `AISeedData.cs` line ~40, the master system prompt tells the AI to *"use them to enrich each scene"*. The word "enrich" invites embellishment — the AI interprets it as adding atmospheric detail, emotional subtext, and descriptive prose. Every downstream prompt inherits this instruction. **Action:** Replace with a directive that prioritizes factual grounding: *"Use them to ground each scene in concrete, specific details. Prefer facts over atmosphere. State what happened, who was involved, and where — not how it felt."*

9. **Fix `ClaudeAITools` history compression lossy summarization.** At `ClaudeAITools.cs` lines 117-159, when conversation history exceeds 4000 characters, it gets compressed to a 400-word prose summary. This loses exact dialogue wording, character name spelling, location names, word-count constraints, and technical formatting rules. User prompts are replaced with `[omitted]`. The AI then works from a fuzzy summary rather than exact prior outputs, causing name inconsistencies and constraint drift across stages. **Action:** Replace prose compression with structured extraction — preserve a vocabulary list (all proper nouns, locations, faction names), all explicit constraints, and exact prior outputs as bullet points. Cap the structural summary, not the factual content.

10. **Rewrite `DialoguePrompts` side options — remove "pure color" and JOKE framing.** At `DialoguePrompts.cs` lines 151-201, side dialogue choices (the non-mainline options) are explicitly labeled as *"pure color — they exist for atmosphere, not information"*. One option type is JOKE, with the instruction to *"add levity or personality"*. This produces side dialogue that's atmospheric filler (*"This station gives me the creeps"*) instead of world-building that reinforces the story. **Action:** Reframe side options as supplementary intel — each should reference a concrete fact from the lore context (a faction, a location, a recent event). Remove the JOKE category entirely; replace with a DETAIL category that surfaces minor but factual world detail.

11. **Balance seed data pools with factual/mundane options (see §6.5).** The following pools need terse, grounded entries added alongside existing literary ones:
    - `NarrativeSeedData.LogFocusPoints` — add *"current supply and resource status"*, *"the job itself — what went right, what went wrong"*, *"next steps and immediate priorities"*
    - `NarrativeSeedData.TransmissionTypes` — add *"a routine shift log recorded by a crew member"*, *"a maintenance report flagging equipment issues"*, *"a cargo manifest annotation with a personal aside"*
    - `NpcSeedData.Upbringings` — add neutral options: *"Grew up in New Atlantis, unremarkable middle-class upbringing"*, *"Grew up on a working freighter, learned the trade from family"*
    - `NpcSeedData.FearsAndPhobias` — add simple fears: *"Doesn't like tight spaces"*, *"Uncomfortable around dogs"*, *"Hates zero-g"*
    - `NpcSeedData.Quirks` — add neutral tics: *"Drums fingers when thinking"*, *"Always early to meetings"*, *"Keeps a tidy workspace"*
    - `StorySeedData.Motives` — add rational motives: *"wanted more credits"*, *"bored and looking for excitement"*, *"saw an opportunity and took it"*
    - `StorySeedData.PersonalityTraits` — add straightforward personalities: *"direct and reliable"*, *"quiet but competent"*, *"easygoing, doesn't overthink things"*

12. **Rewrite `AISeedData` weapon/resource/medical lore descriptors.** All 133 entries in `GetStarfieldBasicWeaponsLoreList()`, `GetStarfieldResourceLoreList()`, and `GetStarfieldMedicalLoreList()` use poetic 5-word descriptions (*"Magnetic revolver delivering brutal impact"*, *"Euphoric narcotic enhancing perception"*). These are injected as worldbuilding lore anchors and train the AI to use flowery language for everything. **Action:** Rewrite descriptors as dry technical specs: *"Magshot — magnetic revolver, .50 cal equivalent"*, *"Aurora — synthetic narcotic, Neon-exclusive, controlled substance"*. Facts over flavor.

13. **Make `PolishPrompts` content-type-aware.** Currently one set of evaluation criteria is applied to all polishable text (logs, dialogue, books, quest names). Quest logs need *"terse, factual, field-intel style"* criteria; outlaw personal logs can tolerate *"emotional voice"* criteria; dialogue needs *"direct, in-character"* criteria. **Action:** `PolishPrompts.Build()` should accept a content-type parameter and apply different evaluation criteria per type. `StageAnnotatedPolishable` already carries a stage label — extend it with a content-type tag.

14. **Cap `WritingPolishPass` iterations for short-form content.** The polish pass runs up to 15 iterations across all polishables. Each iteration drifts short-form text (quest names, log entries, one-line dialogue) further from its original constraints. Long-form content (outlaw log, books) benefits from iteration; short-form does not. **Action:** Add a max-iterations field to `IPolishable` or `StageAnnotatedPolishable`. Set quest logs and dialogue to 1-2 iterations max. Keep books at current limit.

15. **Resolve the five prompt instruction conflicts (see §6.6).** Specific fixes:
    - Replace `AISeedData` *"enrich"* with *"ground in concrete facts"* (conflict #4 — fixes the foundation instruction)
    - Make `PolishPrompts` criteria factual-correctness only, not dramaturgical (conflict #1 — already item #3 above, but now motivated by the conflict analysis)
    - Delete `DialoguePrompts.RunRefinementPass()` (conflict #2 — already item #7 above)
    - Delete `FlavourSeedData.AddFlavourToTargetBook()` prepend (conflict #3 — already item #1 above)
    - Remove intrigue injection or replace with role-derived behavior (conflict #5 — already item #6 above)
    - Add a final system-level constraint after all seed data: *"OVERRIDE: all generated text must be terse, factual, and concrete. No metaphor, no atmospheric filler, no poetic language. When in doubt, state the fact."*

### Phase 1.5: Air-gapped contexts + validation + beat facts

**Goal:** Solve the three quality problems (leakage, compliance, invention) before attempting new content. Test against the bounty hunt schema — output quality should improve measurably.

1. **Implement `RunIsolatedPrompt` on AITools/IAITools.** New call type: fresh conversation context, no shared history. The Claude provider creates a temporary message list, makes one API call, returns the result.

2. **Implement `ContextEnvelope`.** Data class carrying LoreSummary, CastSheet, BeatFacts, PlayerKnowledge, ContentRules. `BuildSystemPrompt()` method assembles these into a system prompt string for the isolated call.

3. **Implement `BeatFacts` and the Fact Planning Pass.** After lore/arc selection, a single prompt generates structured facts for all beats. Parsed from XML. One set of facts per beat, frozen before content generation begins.

4. **Implement `IOutputValidator` and validators** for quest names, log messages, dialogue lines, and book text. Wire into a `ValidatedPrompt.Run()` helper that retries with error feedback.

5. **Refactor `SchemaRunner` to use air-gapped flow.** Replace `RunPrompt` calls with `RunIsolatedPrompt` for all beat content. Build PlayerKnowledge incrementally. Generate bridges in a separate pass with minimal context.

6. **Refactor bridge generation.** Bridges only receive source + destination location. No template names, no stage types, no narrative details. Generated after all beats complete.

7. **Test.** Run bounty hunt schema with air-gapped contexts. Verify: no location leakage in discovery/early investigation logs. Verify: quest names pass validation. Verify: no invented proper nouns in dialogue.

**Estimated scope:** ~3 new files (ContextEnvelope, BeatFacts, validators), ~4 modified files (AITools, SchemaRunner, prompt classes). The `IOutlawQuest` implementations may need modification to accept isolated contexts rather than calling `AITools.RunPrompt` directly — this is the riskiest part.

**Risk: IOutlawQuest direct AI calls.** Currently each `IOutlawQuest.Setup()` calls `QuestPrompts`, `NarrativePrompts`, `DialoguePrompts` directly, which call `AITools.RunPrompt()` internally. To air-gap these, either:
- **Option A:** Pass the ContextEnvelope through BeatContext and modify the prompt classes to accept an optional envelope (falling back to RunPrompt if none provided). Backward compatible. `Setup()` keeps its current structure but each AI call inside it becomes isolated.
- **Option B:** Have the adapter intercept before Setup(), pre-generate all text via isolated calls, then pass pre-generated text to Setup(). Avoids modifying IOutlawQuest implementations but requires a new "text injection" mechanism.

**Recommendation:** Option A — it's more work upfront but cleaner long-term.

**Constraint: back-to-front generation order is permanent.** The Mutagen FormKey linkage (each quest's script properties reference the next quest's FormKey) requires generating the showdown first and the discovery last. This cannot change. The air-gapped system works WITH this constraint: generation order stays back-to-front, but PlayerKnowledge flows front-to-back (pre-computed from BeatFacts). See section 7.2.1 for the full analysis.

**Per-beat loop structure (within back-to-front order):**

```
For each beat (Showdown → ... → Discovery):
  1. Generate bridge to successor (isolated, location-pair only)
     — skip for Showdown (no successor)
  2. Store bridge in thisBeat.Facts.BridgeText
  3. Generate content (log, dialogue, book, pickup) via isolated prompts
  4. Validate all outputs
  5. Wire Mutagen records via Setup(mod, npc, template, nextQuest)
     — nextQuest.questform is available (successor was created earlier)
```

### Phase 2: First new schema

**Goal:** Prove the system works for a non-bounty story.

**Recommended:** Mystery/Investigation. Closest to bounty hunt (player follows clues to uncover truth) but different climax (revelation, not combat) and different emotional arc (confusion → clarity, not pursuit → confrontation).

1. Write `mystery.yaml` schema.
2. Write mystery lore templates (crime scene, suspects, red herrings).
3. Create one new mechanical beat type: `Revelation` — player learns the truth via data-slate or conversation. Mechanically similar to existing beats.
4. Add mystery-specific prompt parameters.
5. Test with SchemaRunner — same runner produces mysteries or bounty hunts depending on schema.

### Phase 3: Extended cast

**Goal:** Support multiple named NPCs per story.

1. `StoryCast` supports multiple roles with relationships.
2. Build basic `NounGraph` — track `knows` / `opposes` / `evidence_of` between cast members.
3. Extend lore generation for multi-character stories.
4. Extend polish pass with cast sheet consistency checking.

### Phase 4: New mechanical types

**Goal:** Beats that don't exist in the current system.

Candidates:
- **Defend** — protect a location or NPC from waves
- **Escort** — accompany NPC to destination
- **Negotiate** — dialogue where choices matter (extends `Meta_Fork_Exclusive` pattern)
- **Revelation** — non-combat climax (learn truth via evidence)

Each requires new Papyrus scripting and template quest records — higher effort than reusing existing beats.

### Phase 5: Cross-story connections

**Goal:** Generated stories reference each other.

1. Persist `NounGraph` across generation runs within a single mod.
2. Add "previously generated" context to lore generation.
3. Shared informant NPCs across quest chains — recurring faces.

---

## 9. Trade-offs and Open Questions

### YAML vs. C# schemas

| | YAML | C# |
|---|---|---|
| **Adding new story types** | Just a file | New class |
| **Complex logic** (conditional beats, weighted forks) | Awkward | Natural |
| **Debugging** | Parse errors are opaque | Compiler catches mistakes |
| **Iteration speed** | Faster (no rebuild) | Slower |

**Recommendation:** YAML for simple schemas, C# escape hatch for complex ones. `SchemaRunner` should accept both.

### How much AI vs. how much structure?

**More AI** — let AI design the schema dynamically. "Given this lore, what story should this be?" Tempting but risky: incoherent structures, hard to debug.

**More structure** — pre-define every beat sequence, AI only generates text. Guarantees mechanical correctness, limits creativity.

**Recommendation:** Keep the current balance. Schemas define structure. AI selects which schema and which beats. AI generates all text. The innovation is data-driven structure, not AI-driven structure.

### IOutlawQuest migration strategy

**Option A: Adapter pattern.** `OutlawQuestAdapter` wraps any `IOutlawQuest`. Existing code untouched. New beats implement `IStoryBeat` directly. *Recommended for Phase 1.*

**Option B: Gradual migration.** Keep both interfaces. Migrate one at a time. *Natural evolution from Option A.*

**Option C: Big bang rewrite.** Rewrite all 16 implementations. Highest risk, cleanest result. *Only if adapter proves too constraining.*

### New climax types — what's feasible?

The bounty system has two climax types (kill target on planet/city). New schemas need different endings:

| Climax Type | Starfield Feasibility | Required New Work |
|---|---|---|
| **Revelation** (learn truth) | High — book or dialogue | Minimal — reuse existing beat patterns |
| **Rescue** (reach & free NPC) | Medium — stage advance on area entry | New Papyrus script + quest template |
| **Negotiation** (dialogue resolution) | Medium — branching dialogue exists | New dialogue structure + outcomes |
| **Escape** (leave under pressure) | Low — needs timed pressure system | Significant Papyrus + scripting |
| **Defense** (hold position) | Low — needs wave spawning system | Significant scripting + AI packages |

**Recommendation:** Start with climax types buildable from existing Mutagen patterns. Defer Papyrus-heavy ones.

### The naming problem

Existing code uses bounty-specific names: `OutlawNpc`, `IOutlawQuest`, `RetrogradeBountyQuest`. Renaming breaks muscle memory and existing code.

**Recommendation:** Don't rename. Create new classes with neutral names (`IStoryBeat`, `StoryCast`, `CharacterTraits`). Let old names coexist. The adapter pattern enables this naturally.

### Where do lore templates come from?

Each schema needs its own lore template files (like the existing `questgen_quests/Lorefiles/*.md`). These are the real bottleneck for new schemas — the code architecture can support any schema, but the AI needs a well-written lore template to produce good content.

Writing good lore templates is a creative task that requires understanding both Starfield's setting and what makes each story type work. This is where the system's quality ceiling lives — not in the code, but in the templates.

---

## 10. Summary

The existing system's components — Nouns, IPolishable, WritingPolishPass, AITools, dungeon generators — are well-designed. The problem is that they're wired together by a single hardcoded story structure.

The fix is a **data-driven schema layer** that declares story shapes in YAML, a **casting layer** that generalizes OutlawNpc into role-agnostic characters, and a **scene layer** that adapts existing IOutlawQuest implementations to work with any schema.

The path starts by encoding the existing bounty hunt as the first schema (Phase 1 — zero risk, identical output), then proving the architecture with one new story type (Phase 2), then extending from there.

Most of the existing C# code continues to work through adapter patterns. The investment is in:
1. The SchemaRunner orchestrator (~1 file)
2. Schema YAML parsing (~1 file)
3. StoryCast + CharacterTraits (~2 files)
4. Prompt parameterization (modifications to existing prompt classes)
5. Lore templates for each new schema (creative writing, not code)
