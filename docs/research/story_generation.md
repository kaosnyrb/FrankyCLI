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

## 6. How AI Fits at Each Layer

| Layer | When | Call Type | Why This Type |
|-------|------|-----------|---------------|
| **Schema selection** | Before generation | `RunStatelessPrompt` | Don't pollute history with meta-decisions |
| **Cast & lore generation** | After schema selected | `RunPrompt` | Builds conversation history — all subsequent beats see lore context |
| **Beat type selection** | After lore generation | `RunStatelessPrompt` | Structural decision — shouldn't enter narrative history |
| **Beat content** (logs, books, dialogue) | During `IStoryBeat.Setup()` | `RunPrompt` | Each beat's content becomes context for the next |
| **Stage bridges** | Between beats | `RunPrompt` | Connective tissue needs to see accumulated narrative |
| **Cast sheet injection** | After cast built | `InjectContextIntoHistory` | Silent context — no response needed, all beats should see it |
| **Polish** | After all beats | `RunStatelessPrompt` × N | Read-only — must not pollute the narrative history |
| **Schema-specific constraints** | After lore | `InjectContextIntoHistory` | E.g., "don't reveal rescue location in early beats" |

### Prompt parameterization

The existing prompt classes (`QuestPrompts`, `NarrativePrompts`, `DialoguePrompts`) hardcode bounty-specific language. A systemic approach parameterizes them:

| Hardcoded Today | Schema Parameter |
|---|---|
| "bounty hunter" | `schema.stakes.player_role` |
| "outlaw target" | `cast.GetRole("target").Name + " (" + role_description + ")"` |
| "track and eliminate" | `schema.description` verb |
| "Kill the Outlaw target" | Schema's climax action description |
| "where the trail leads" | Schema's bridge prompt (varies by function) |

This keeps prompt engineering in C# (testable, tunable) while letting schemas customize the framing.

---

## 7. Practical Path Forward

### Phase 1: Prove the architecture (zero new content)

**Goal:** Refactor the bounty hunt to use the new architecture with identical output. This proves everything works before extending.

1. **Extract `CharacterTraits` from `OutlawTraits`.** Create a parent class or rename. Add `DefiningEvent` alongside `Crime`. Keep backward compatibility.

2. **Create `StoryCast`.** Wrap existing `OutlawNpc` creation into a cast with one role: `target → OutlawNpc`.

3. **Write `bounty_hunt.yaml`.** Encode the current hardcoded structure as a schema file. Add a minimal YAML parser.

4. **Create `SchemaRunner`.** Replaces `LoopingLayoutQuestChain` / `StaticLayoutQuestChain` with a single data-driven orchestrator. For Phase 1, produces identical output.

5. **Create `OutlawQuestAdapter : IStoryBeat`.** The existing 16 `IOutlawQuest` implementations continue working through the adapter. No rewrites.

6. **Parameterize `QuestPrompts.GetLogMessage()`.** Replace "bounty hunter" with `schema.stakes.player_role`. Verify output quality.

**Estimated scope:** ~5 new files, ~3 modified files. No spatial generation changes. No new content.

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

## 8. Trade-offs and Open Questions

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

## 9. Summary

The existing system's components — Nouns, IPolishable, WritingPolishPass, AITools, dungeon generators — are well-designed. The problem is that they're wired together by a single hardcoded story structure.

The fix is a **data-driven schema layer** that declares story shapes in YAML, a **casting layer** that generalizes OutlawNpc into role-agnostic characters, and a **scene layer** that adapts existing IOutlawQuest implementations to work with any schema.

The path starts by encoding the existing bounty hunt as the first schema (Phase 1 — zero risk, identical output), then proving the architecture with one new story type (Phase 2), then extending from there.

Most of the existing C# code continues to work through adapter patterns. The investment is in:
1. The SchemaRunner orchestrator (~1 file)
2. Schema YAML parsing (~1 file)
3. StoryCast + CharacterTraits (~2 files)
4. Prompt parameterization (modifications to existing prompt classes)
5. Lore templates for each new schema (creative writing, not code)
