You are a quest writer designing the best prompts to improve the writing quality in my prompts.

Using gen_promptlab and the complete generated prompt history to run 5 prompt mutations trying to improve the output.

We want to improve the following:

Tonal Consistency
Contextual Coherence
Uniqueness / Repetition Avoidance
Player Motivation Clarity
Dialogue Naturalness

The prompt we wish to improve is in:

docs\prompts\The Derelict Records.txt

The code that generates this prompt is in:

Retrograde.Library\Core\AI\Prompts\DialoguePrompts.cs

## Workflow

1. Read the conversation file to understand what the current prompt generates across multiple runs — look for repetition patterns, structural weaknesses, and tonal problems.
2. Design 5 focused mutations, each isolating one hypothesis about what causes the weakness.
3. Create self-contained mutation files in docs\prompts\mutations\ — each must include a [system] with lore context, a [user]/[assistant] pair establishing LoreContext, and the mutated [user] prompt at the end.
4. Run all 5 in parallel with gen_promptlab. Assess each output against the five quality dimensions above.
5. Identify the 2-3 winning constraints. Combine them into a single [user] block appended to The Derelict Records.txt and run against the full conversation history to verify they stack.
6. Apply the winning prompt changes to DialoguePrompts.cs.

## Lessons learned

### GREETING constraint
The model defaults to opening with the NPC announcing foreknowledge ("I know why you're here", "You're going to ask me about..."). This is the single biggest driver of repetition across runs.

Fix: Forbid the NPC from naming the target, predicting the player's question, or claiming foreknowledge in the GREETING. Force it to open with a specific action or mundane detail. Foreknowledge must emerge from what the NPC reveals, not be stated.

### PLAYER voice
Generic PLAYER lines ("What can you tell me?", "How did you know that?") produce NPC monologues, not exchanges. The player has no character.

Fix: Define the player as a bounty hunter on a paying contract. Questions must be operational and closed: "When did she leave?" / "What name was on the manifest?" / "Who processed her entry?" PLAYER3 should be the most direct question in the exchange.

### Information beat architecture
Without beat rules, the NPC bleeds information across all exchanges — destination leaks in NPC1, behavioral details repeat in NPC2, NPC3 is anticlimactic.

Fix: Assign exactly ONE type of information to each beat:
- Beat 1 (NPC1): personal observation — behavior or appearance only, no logistics
- Beat 2 (NPC2): logistics only — ship, route, alias — no behavioral repetition
- Beat 3 (NPC3): current location + one actionable next step, nothing else

### Intrigue detail as structural constraint
The Intrigue detail seed from FlavourSeedData is injected as a thematic hint. The model implements it as a broad GREETING announcement ("I already knew that"), which repeats identically across runs.

Fix: Add a structural rule to the prompt: implement the Intrigue detail in exactly ONE NPC line (not GREETING, not NPC3a) as a concrete unasked-for detail that lands without comment. The NPC must not announce or reference their foreknowledge.

### GetLogMessage — "Objective:" prefix and location hallucination
The word "objective" in "Write a 50-word **objective** log entry" causes the model to label its output with "Objective:". Separately, "use LoreContext for ... **location**" causes it to blend the explicit Location from Additional Information with planet names from LoreContext (e.g. "at a remote location on Montara Luna").

Fix (mutation_6_log_prefix.txt — applied to QuestPrompts.GetLogMessage):
- Change "objective log entry" to "log entry"
- Remove "location" from the LoreContext fact list
- Add: "Location: use ONLY the location provided in Additional Information exactly as written. Do not add planet names, system names, or any location detail from the LoreContext."
- Add: "Output only the log text. Do NOT prefix with 'Objective:', 'Log:', or any other label or header."

### GetLogMessage — "Target:" prefix and stage leakage
The banned-prefix list ("Objective:", "Log:") missed "Target:", which the model uses consistently. More critically, the log draws on the full LoreContext (including Faction section with Vanguard security details) regardless of QuestProgress — so Discovery and InitialInvestigation logs reveal DeepInvestigation-level faction intel. The StageBridge also bleeds Vanguard details when treated as current knowledge.

Fix (mutations 7+8 — applied to QuestPrompts.GetLogMessage):
- Expand banned prefix to: "Objective:", "Log:", "Target:", "Name:", "Subject:", or any similar word followed by a colon. Start directly with the action or fact.
- Add stage-locked knowledge rule: At QuestProgress 0–10%, use only target name, basic crime, and current objective. Do NOT reference faction investigations or security assessments. At 50–80%, faction involvement is allowed. At 90%+, full picture available.

### Dialogue NPC — stage leakage via full LoreContext and StageBridge
At InitialInvestigation (10%), NPCs reveal the Vanguard security-breach angle in NPC3b because they have access to the full LoreContext Faction section and the StageBridge text ("Vanguard knows far more than embezzlement"). A dock worker would not know the content of classified security briefs.

Fix (mutation 11 — applied to DialoguePrompts.GetDialogueScript):
- Add NPC knowledge constraint: the NPC knows only what someone in their job would personally witness or overhear. If they mention the Vanguard, it must be from something they personally saw — not a summary of the investigation.
- Add Beat 3 NPC3b scope rule: name only a direction, location, or person to approach — do NOT explain why it matters or reveal what the player will discover there.

Source conversation: 20260313_2135_Jisoo_Yamamoto.txt

## Mutation file format

```
[system]
<Starfield lore context>

[user]
Here is the established LoreContext for this quest. Treat it as canon.
<compressed LoreContext summary>

[assistant]
LoreContext confirmed. <one-line summary>. Ready.

[user]
<mutated prompt>
```

Last block must be [user]. Run with:
```
dotnet run --no-build --project "c:/Git/FrankyCLI/FrankyCLI.csproj" -- gen_promptlab "docs/prompts/mutations/mutation_N_name.txt"
```
