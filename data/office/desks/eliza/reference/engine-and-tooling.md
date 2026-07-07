# FrankyCLI — engine & tooling reference (salvaged)

*Salvaged 2026-07-07 from the stranded harness auto-memory store
(`~/.claude/projects/c--Git-FrankyCLI/memory/MEMORY.md`), which was machine-local and travelled
nowhere. Triaged — keepers below, with what I dropped and why at the bottom. Items marked
**→ graduate** are corpus facts that ideally fold into `docs/formlib/` or `CLAUDE.md`; they sit here
until that doc-health pass. Verify any file/flag against the current tree before trusting it — some of
this is months old.*

## Build & run

- Build with an **absolute forward-slash path, no `cd`, no pipe**: `dotnet build
  "c:/Git/FrankyCLI/Retrograde.Library/Retrograde.csproj" --no-restore 2>&1`. Piping through
  `grep`/`tail` produces confusing exit codes that mask real build failures; `cd` first can fail to find
  the csproj on Windows bash. Project files: `Retrograde.Library/Retrograde.csproj`, `FrankyCLI.csproj`.
- **Parallel `dotnet run` locks `Retrograde.dll`** — build once, then run all with `--no-build`.
- Bulk mechanical edits across many `.cs`: a Python script via Bash (`python` is on git-bash;
  `pwsh`/`powershell` are **not**). **Encoding gotcha:** some older CS files are Windows-1252 (curly
  apostrophes, byte `\x92`) — try `('utf-8','utf-8-sig','cp1252')` in a fallback loop.

## Mutagen / record gotchas

- **`Dictionary<string,object>` unbox — int vs uint.** Hex literals (`0x002CC1EF`) box as `int`; a typed
  `uint` boxes as `uint`. `(uint)obj` throws `InvalidCastException` on a boxed `int`. Always
  `Convert.ToUInt32(...)` for FormId-style reads from a dict. **→ graduate** (`docs/formlib/mutagen_api.md`).
- **Overlay worldspace coordinate system** — two unit systems, never confuse: BTD internal cell = 4096
  units, Overlay PlacedObject X/Y cell = **100 units**. `overlayX = btdX * (100f/4096f)`;
  `SampleHeightAtWorld` takes BTD coords, divide result by 8 for PlacedObject Z; cell routing
  `floor(pos/100f)`; Starfield BTDs always `WorldCenterX = 0`. **→ graduate** (`docs/formlib/worldspace.md`
  — verify not already covered).
- **Ship clone — `ObjectTemplateInstanceData`: copy ONLY `[0]`.** `[1]` ("Spaceship_InstanceData", 22
  bytes) is tied to the *original* ship's FormKey and breaks docking/boarding when cloned to a new
  FormKey. Confirmed by `gen_shipcompare` (working du_outlaws_01 = 1 entry; failing outlaws02 = 2).
  **→ graduate** (`docs/formlib/ship.md` — verify).
- **PCM tree `Nodes` list is unused** — `PlanetContentManagerBranchNode.Nodes` (child links) is not
  written by any PCM pass; Starfield builds the parent→child tree at runtime from each child's
  `ParentNode` back-ref. Do NOT add `Nodes.Add(...)`. **→ graduate** (`docs/formlib/pcm.md` — verify).
- **`Scene.SCPI` = exactly 2 bytes (ushort), not 4** — `BitConverter.GetBytes((ushort)100)` mainline /
  `(ushort)0` side; set `Scene.Index` (uint?) in increments of 10 alongside. `Scene.Actions` is nullable
  — assign a fresh `ExtendedList`. See `docs/formlib/book_audio.md` (mostly covered there).
- **`NPCTools` lookups** — `GetTemplateNPC`/`GetTemplateDeadNPC` return vanilla Starfield.esm IDs; don't
  look them up with `mod.Npcs[new FormKey(mod.ModKey, id)]` (wrong ModKey). Use
  `NPCTools.FindTemplateNpc(isFemale)` / `FindNpcById` (searches TargetMod → TemplateMods → StarfieldMod).

## AI content tools

- **`AITools` — three call types:** `RunPrompt(p)` (adds user+assistant, for content generation);
  `RunStatelessPrompt(p)` (read-only history — for selections that must not pollute context, e.g.
  PlannedArc template pick); `InjectContextIntoHistory(text)` (no API call, adds a SystemChatMessage —
  for rules/artefacts/constraints needing no response). Stage summaries + "don't reveal showdown
  location" → inject; PlannedArc XML → inject after a stateless pick so all stage prompts see it.
- **`gen_promptlab`** — prompt iteration over `[system]`/`[user]`/`[assistant]` block files in
  `docs/prompts/` (last block must be `[user]`). Mutation-tested prompt-engineering rules live in
  [`promptlab-findings.md`](promptlab-findings.md).

## Tool commands (verify still current)

- `gen_deprefscan <modname>` — scans a built `.esm` for FormLinks into template mods; clean prints
  "Mod is clean." `gen_hunttest` — writes `hunttest.esm` with predator hunt NPCs. `migrate_params.py` —
  `MissionTemplate` field migration (idempotent). All were pre-approved / `python:*`.

## Dropped in triage (not moved — here's why)

- **Mutagen nullable-FormLink rule** (set after construction, `IsNull` guard) — **redundant**, already
  authored verbatim in `CLAUDE.md` § Critical Rules. Read it there.
- **`BGSAdaptiveTriggerData_Component` enumeration crash / `EnumerateSafe` / `SearchWithRecovery`** —
  **redundant**, documented in `gen_inspect.cs`'s own comment and `FormKeyLookup.cs`.
- **`gi.sh` wrapper usage** (`bash scripts/gi.sh …`) — **stale/unverified**: the live invocation is now
  `dotnet run -- gen_inspect`; whether `scripts/gi.sh` still exists needs checking before relying on it.
- **`reference_gi_unsupported_types.md`** ("supported types as of 2026-03-22") — **stale**: the supported
  set has since grown (Race, Biome, and more). The current truth is the `switch` in `gen_inspect.cs` —
  read the code, not a frozen list.
- **Audio data-slate / Wwise WAV→WEM pipeline** — covered in `docs/formlib/book_audio.md`; kept only the
  `Scene.SCPI` 2-byte gotcha above as a quick reminder.
