# gen_promptlab — prompt-engineering findings

*Salvaged 2026-07-07 from the stranded harness store (`feedback_promptlab_dataslate.md` +
`feedback_promptlab_stage_leakage.md`). Mutation-tested rules for the mission-content prompts — each
was validated by running variants through `gen_promptlab` and comparing outputs. **→ graduate**: these
ideally live beside the prompt code (`QuestPrompts.cs`, `DialoguePrompts.cs`, `Discovery_Dataslate.cs`)
or in a `docs/` prompt-authoring note.*

## Mission-briefing dataslate (from a 5-mutation test, 2026-03-12)

- **Use fixer-shorthand style, not "plain declarative sentences."** Plain declarative produces uniform
  flatness *and* the model adds forbidden label headers (`TARGET:`, `ACTION:`) to impose its own
  structure. "Write as if a fixer dropped the hunter a terse field note mid-route — short bursts,
  functional shorthand" avoids headers and gives better rhythm. (M4 best; M1/M3 both added labels.)
- **"No headers or labels of any kind" must be a standalone hard rule**, not buried in a bullet — models
  override the soft form. Dedicated sentence in the Style line.
- **Remove `LogMessage` from dataslate prompt addons** — it pre-answers the prompt and the model parrots
  it verbatim, killing uniqueness. Derive from `LoreContext` + `StageBridge` only. (`Discovery_Dataslate.cs`
  passes only `missionTemplate.Addons`.) `LogMessage` is still fine for quest-name prompts.
- **Mandate an urgency hook** — "Close with a concrete urgency hook: a named rival, a contact who moves
  on quickly, or a window that closes soon" — produces the strongest forward-pull closers.
- **Allow one hedged construction per field note** ("believed to be", "last reported moving through") —
  improves naturalness without breaking the intel-note register.

## Stage leakage — knowledge horizons (from mutations 7–11, 2026-03-13)

- **`GetLogMessage` needs a stage-locked knowledge constraint.** At QuestProgress 0–10% the log draws on
  the full `LoreContext` (incl. the Faction section), leaking DeepInvestigation content at
  Discovery/InitialInvestigation. Add: *"At QuestProgress 0–10%, use only the target's name, their basic
  crime, and the current objective. Do NOT reference faction investigations or security assessments."*
  (Mutation 8 cleanly eliminated the leak; a StageBridge-only reframe only half-fixed it — the root draw
  is `LoreContext` Faction.)
- **Expand the banned-prefix list** — the existing ban missed `Target:`. Ban *"'Objective:', 'Log:',
  'Target:', 'Name:', 'Subject:', or any similar word followed by a colon."*
- **`GetDialogueScript` NPC knowledge horizon** — early-stage NPCs leak the classified angle because the
  full `LoreContext` Faction section is available. Add: *"This NPC knows only what someone in their job
  and location would personally witness or overhear. They do NOT have access to investigation reports,
  security assessments, or classified faction files."* (Mutation 11 eliminated all leakage.)
- **Beat-3 NPC scope — direction only** — *"name only a direction, location, or person to approach next.
  Do NOT explain why it matters or reveal what the player will discover there."*
