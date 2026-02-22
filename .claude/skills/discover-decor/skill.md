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

### Step 1 — Find the PackIn and its cell contents

```bash
cd /c/Git/FrankyCLI && dotnet run -- gen_inspect PackIn $ARGUMENTS
```

From the output, record:
- The **PackIn FormKey** (e.g. `01F329:Starfield.esm`)
- The **Cell FormKey** from the `Cell` property
- Any **variant PackIns** with the same prefix (e.g. `SC_CounterScience01a`)

Then dump the PackIn's cell to confirm its internal contents:

```bash
dotnet run -- gen_inspect Cell 0x<CellFormKeyId>
```

Note the Z height of visible objects inside the cell — this is the counter surface height
(usually ~0.8 above the cell floor for furniture PackIns).

Repeat for all close variants (e.g. `01a`).

### Step 2 — Find all worldspace placements

Search for placed objects whose `Base` matches the PackIn FormKey:

```bash
dotnet run -- gen_inspect placed 0x<PackInFormKeyId>
```

Identify the worldspace with the **most instances** — that's the best source of decoration data.
Record all instance positions (X, Y, Z) from that worldspace.

### Step 3 — Dump the worldspace and filter by proximity

Dump all placed objects in the chosen worldspace:

```bash
dotnet run -- gen_inspect worldspace_objects <WorldspaceEditorId> 2>&1 > /tmp/ws_objects.txt
```

Then use Python to find objects near each PackIn instance position within the given radius:

```python
import re, sys

ws_file = '/tmp/ws_objects.txt'
radius = 3.0   # replace with $1 if provided

# Positions of the PackIn instances in this worldspace (from Step 2)
packin_positions = [
    # (x, y, z),  # fill from Step 2 output
]

with open(ws_file) as f:
    lines = f.readlines()

pat = re.compile(r'PlacedObject (\S+) Base=(\S+) EdID=(\S*) Pos=([\-\d.Ee+]+), ([\-\d.Ee+]+), ([\-\d.Ee+]+)')
nearby = {}

for line in lines:
    m = pat.search(line)
    if not m:
        continue
    fk, base, edid, x, y, z = m.groups()
    x, y, z = float(x), float(y), float(z)

    for (cx, cy, cz) in packin_positions:
        dist = ((x-cx)**2 + (y-cy)**2)**0.5   # XY distance only
        if dist < radius:
            dz = z - cz   # height above the PackIn base
            if base not in nearby:
                nearby[base] = {'edid': edid, 'dz': dz, 'count': 0}
            nearby[base]['count'] += 1
            break

# Sort by count desc then dz
for base, info in sorted(nearby.items(), key=lambda kv: -kv[1]['count']):
    print(f"  count={info['count']}  dz={info['dz']:+.2f}  Base={base}  EdID={info['edid']}")
```

### Step 4 — Identify unknown FormKeys

For each `Base` FormKey found in Step 3 that lacks an EditorID, look it up:

```bash
dotnet run -- gen_inspect Static 0x<FormId>
dotnet run -- gen_inspect PackIn 0x<FormId>
dotnet run -- gen_inspect Activator 0x<FormId>
```

Run these in parallel. Note the `EditorID` for each.

Skip pivot/dummy markers: `StaticCollectionPivotDummy` (035812) and `PrefabPackinPivotDummy` (03F808).

### Step 5 — Categorise by Z offset

Group items by their dz (height above PackIn base Z):

| Z range | Category | Meaning |
|---|---|---|
| dz < 0.1 | **Floor** | Freestanding at ground level |
| 0.1 ≤ dz < 1.5 | **Counter-top** | Sitting on bench/table surface |
| dz ≥ 1.5 | **Overhead/wall** | Ceiling lights, mounted screens, etc. |

Items at the PackIn base itself (dz ≈ 0) that are *not* the PackIn are floor items.
Items at dz ≈ 0.8–1.0 are typically the counter surface props.

### Step 6 — Look up any relevant DesktopClutter or named PackIns

Search for dedicated clutter PackIns designed for this surface type:

```bash
dotnet run -- gen_inspect PackIn DesktopClutter_
dotnet run -- gen_inspect PackIn ClutterPI_
```

These pre-built clutter kits (e.g. `DesktopClutter_Science_A01`) are the highest-quality
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

- `gen_inspect worldspace_objects` outputs large results — use a temp file and Python filter
- The `placed` search checks ALL loaded mods (Starfield.esm + ShatteredSpace.esm etc.)
  so filter to `Starfield.esm` sources only for vanilla patterns
- Non-Static/Activator items (vials, misc items, loot) won't resolve via gen_inspect —
  document their FormKeys with a `// MiscItem — pickup-able` comment
- Prefer the worldspace with the most PackIn instances for richer co-placement data
- The `gen_inspect` binary format: `dotnet run -- gen_inspect <RecordType> <search>`
  (NOT the legacy `dummy gen_inspect dummy` format)
