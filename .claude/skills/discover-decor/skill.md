---
name: discover-decor
description: Discover what decoration items Starfield places around a given PackIn or Static in existing worldspaces, then generate C# array entries ready to paste into a BuildingDecoratorPass. Use when the user asks to find decorations for a science counter, workbench, or any other Starfield furniture PackIn.
user_invocable: true
---

# Decoration Discovery Skill

Automates the investigation workflow used to discover what decoration/clutter items
Starfield places near a given PackIn (e.g. `SC_CounterScience01`) and produces a
formatted summary + C# array stubs for use in `BuildingDecoratorPass`.

## Arguments

```
/discover-decor <PackInEditorId>
/discover-decor <PackInEditorId> [radius]
```

Examples:
- `/discover-decor SC_CounterScience01`
- `/discover-decor SC_CounterScience03 4.0`

Default radius: `3.0` overlay units.

## Workflow

All commands use scripts from `c:/Git/FrankyCLI/scripts/` — already pre-approved in
`settings.local.json`. Temp files go to `C:/tmp/`.

### Step 1 — Find the PackIn and its cell contents

```bash
bash scripts/gi.sh PackIn $ARGUMENTS
```

From the output, record:
- The **PackIn FormKey** (e.g. `01F329:Starfield.esm`)
- The **Cell FormKey** from the `Cell` property
- Any **variant PackIns** with the same prefix (e.g. `SC_CounterScience01a`)

Then dump the PackIn's cell to confirm its internal contents:

```bash
bash scripts/gi.sh Cell 0x<CellFormKeyId>
```

Note the Z height of visible objects inside the cell — this is the counter surface height
(usually ~0.8 above the cell floor for furniture PackIns).

Repeat for all close variants (e.g. `01a`).

### Step 2 — Find all worldspace placements

Search for placed objects whose `Base` matches the PackIn FormKey:

```bash
bash scripts/gi.sh placed 0x<PackInFormKeyId>
```

Identify the worldspace with the **most instances** — that's the best source of decoration data.
Record all instance positions (X, Y, Z) from that worldspace.

### Step 3 — Dump the worldspace and filter by proximity

```bash
bash scripts/dump_ws.sh <WorldspaceEditorId>
# Output: C:/tmp/ws_<WorldspaceEditorId>.txt
```

Then filter by proximity — pass each PackIn instance position as a `x,y,z` argument:

```bash
python3 scripts/proximity_filter.py C:/tmp/ws_<WorldspaceEditorId>.txt <radius> <x1,y1,z1> [<x2,y2,z2> ...]
```

Example:
```bash
python3 scripts/proximity_filter.py C:/tmp/ws_OESF003World.txt 3.0 16.1,113.7,26.4 23.7,143.1,32.8
```

Output is sorted by hit count descending. `dz` = item Z minus anchor Z (positive = above anchor).

### Step 4 — Identify unknown FormKeys

For any `Base` FormKey with an empty `EdID=` field:

```bash
bash scripts/lookup_fk.sh <formId>
# Example: bash scripts/lookup_fk.sh 075B8D
```

This tries Static → PackIn → Activator in sequence and prints the first match's EditorID and
mesh path. If nothing matches, the record is likely a MiscItem, Container, or NPC form (not
placeable as a Static) — document it with a `// MiscItem` comment.

Skip pivot/dummy markers: `StaticCollectionPivotDummy` (035812) and `PrefabPackinPivotDummy` (03F808).

### Step 5 — Categorise by Z offset

Group items by their `dz` (height above PackIn base Z):

| Z range | Category | Meaning |
|---|---|---|
| dz < 0.1 | **Floor** | Freestanding at ground level |
| 0.1 ≤ dz < 1.5 | **Counter-top** | Sitting on bench/table surface |
| dz ≥ 1.5 | **Overhead/wall** | Ceiling lights, mounted screens, etc. |

Items at the PackIn base itself (dz ≈ 0) that are *not* the PackIn are floor items.
Items at dz ≈ 0.8–1.0 are typically counter surface props.
Items with very large |dz| (> 5) are usually architectural room pieces — skip them.

### Step 6 — Look up any related PackIn families or DesktopClutter kits

Find dedicated clutter PackIns designed for this surface type:

```bash
bash scripts/find_family.sh DesktopClutter_
bash scripts/find_family.sh ClutterPI_
```

For SC_LD_ family items (loading dock crates/barrels etc.), find all variants:

```bash
bash scripts/find_family.sh SC_LD_CratesMedium
bash scripts/find_family.sh SC_LD_BarrelsOnPallets
```

Pre-built clutter kits (e.g. `DesktopClutter_Science_A01`) are the highest-quality
option when available — they contain multiple internally-positioned props in a single drop.

### Step 7 — Output summary and C# stubs

Present results in three sections:

---

**Counter-top items** (place at `pod.Z + ~0.8f`):

```csharp
private static readonly (uint formId, string editorId)[] CounterTopItems =
[
    // format: (0xFORMKEY, "EditorID"),
];
```

**Floor-standing items** (place at `pod.Z`):

```csharp
private static readonly (uint formId, string editorId)[] FloorItems =
[
    // format: (0xFORMKEY, "EditorID"),
];
```

**Pre-built clutter PackIns** (highest quality — use these first):

```csharp
private static readonly (uint formId, string editorId)[] ClutterPackIns =
[
    // format: (0xFORMKEY, "EditorID"),
];
```

---

Include the source worldspace EditorID and the search radius used so results can be
re-run or extended later.

## Notes

- `dump_ws.sh` outputs large results — the Python filter handles files of any size
- The `placed` search checks ALL loaded mods (Starfield.esm + ShatteredSpace.esm etc.)
  so filter to `Starfield.esm` sources only for vanilla patterns
- Non-Static/Activator items (vials, misc items, loot) won't resolve via `lookup_fk.sh` —
  document their FormKeys with a `// MiscItem — pickup-able` comment
- Prefer the worldspace with the most PackIn instances for richer co-placement data
- Most "nearby" results in interior worldspaces will be architectural room pieces (catwalks,
  walls, railings) — focus on items at dz ≈ 0 to +1.5 for genuine decoration candidates
