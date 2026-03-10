# FormList (FLST)

A `FormList` is an ordered list of record links — a generic container used to group NPCs, placed objects, PackIn variants, or any other records that a script or game system needs to iterate. In FrankyCLI they are used for crew/gang member pools, waypoint marker collections, and slot-to-content mapping.

---

## Key fields

| Field | Type | Notes |
|---|---|---|
| `EditorID` | `string` | Required. Use a descriptive naming convention — see patterns below. |
| `Items` | `ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>` | The list of record links. **Initialize to `new ExtendedList<...>()` in the constructor block** — it's a reference type, not a FormLink struct, so in-block initialization is safe. Add entries via `.Add()` after construction. |

---

## Construction pattern

```csharp
var formList = new FormList(targetMod)
{
    EditorID = prefix + "_mylist",
    Items    = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
};

// Add entries after construction
formList.Items.Add(npcRecord.ToLink<IStarfieldMajorRecordGetter>());
formList.Items.Add(new FormKey(sfEsm, someFormId).ToLink<IStarfieldMajorRecordGetter>());

targetMod.FormLists.Add(formList);
```

All entries use `ToLink<IStarfieldMajorRecordGetter>()` — the list is typed to the base record interface, so any record type can be stored.

---

## Use cases in FrankyCLI

### Crew and gang member pools

NPC instances are created first, then added to a FormList. The FormList is then wired into a quest script property (`GangMembers`) so Papyrus can pick NPCs at runtime.

```csharp
var frmlst = new FormList(targetMod)
{
    EditorID = shipName + "_crewlist",
    Items    = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
};
// ... create NPCs ...
frmlst.Items.Add(npc);   // PlacedNpc or Npc instance
targetMod.FormLists.Add(frmlst);
```

EditorID conventions observed:
- `{ShipName}_crewlist` — named faction crews
- `frmlist_ganglist_{editorId}` — cached gang lists in target mod
- `frmlist_{8-char-guid}` — anonymous gang lists (StreetGang, NamedMercenaryGang)

### Waypoint / marker collections

Placed XMarker records are collected into a FormList so a script can iterate waypoints in order.

```csharp
// XMarker [Starfield.esm:0x3B]
var xMarkerKey = new FormKey(starfieldModKey, 0x3B);

var formList = new FormList(targetMod)
{
    EditorID = worldspace.EditorID + "_WaypointList",
    Items    = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
};
// Create and place each XMarker, then:
formList.Items.Add(marker.ToLink<IStarfieldMajorRecordGetter>());
targetMod.FormLists.Add(formList);
```

### Slot-to-content mapping (EnemyPass)

Room slot markers are looked up by EditorID, and each slot maps to a FormList of valid content. The `FindSlotList()` helper normalises the slot EditorID and searches `targetMod.FormLists` by exact EditorID match.

---

## Copying from a template mod (no master dependency)

Never reference a template mod's FormList directly — create a copy in the target mod to avoid a master file dependency.

```csharp
// Find in template mods
IFormListGetter? source = null;
foreach (var tm in RetrogradeContext.Current.TemplateMods)
{
    source = tm.FormLists.FirstOrDefault(r => r.FormKey == fk);
    if (source != null) break;
}

// Create new list in target mod (fresh FormKey — no master reference)
var newList = new FormList(targetMod)
{
    EditorID = source.EditorID,
    Items    = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
};
foreach (var item in source.Items)
    newList.Items.Add(item.FormKey.ToLink<IStarfieldMajorRecordGetter>());
targetMod.FormLists.Add(newList);
```

---

## Lookup patterns

**By FormKey:**
```csharp
var fl = targetMod.FormLists.FirstOrDefault(r => r.FormKey == fk);
```

**By EditorID (exact):**
```csharp
var fl = targetMod.FormLists.FirstOrDefault(fl =>
    string.Equals(fl.EditorID, editorId, StringComparison.OrdinalIgnoreCase));
```

**By EditorID (contains) — for partial template names:**
```csharp
foreach (var tm in RetrogradeContext.Current.TemplateMods)
{
    var fl = tm.FormLists.FirstOrDefault(f =>
        f.EditorID != null && f.EditorID.Contains(partialName));
    if (fl != null) { /* found */ break; }
}
```

Always search `targetMod` first, then `TemplateMods` in order.

---

## Known template gang list EditorIDs

These exist in template mods (not Starfield.esm):

| EditorID | Faction |
|---|---|
| `duout_GangMembersList_Space_Ecliptic` | Ecliptic |
| `duout_GangMembersList_Space_Crimsonfleet` | Crimson Fleet |
| `duout_GangMembersList_Space_Spacer` | Spacers |
| `duout_GangMembersList_Space_Varuun` | House Va'ruun |

---

## Inspecting with gi.sh

```bash
bash c:/Git/FrankyCLI/scripts/gi.sh FormList duout_GangMembersList_Space_Spacer
bash c:/Git/FrankyCLI/scripts/gi.sh FormList 0x00ABCD
```

**Note:** `gen_inspect` uses the generic `DumpRecord()` for FormList, which shows FormKey and EditorID but **does not list the items**. To inspect items, use `gi.sh Quest` or look at the record in xEdit/CK.

---

## Useful vanilla FormKey

| FormID (Starfield.esm) | EditorID | Use |
|---|---|---|
| `0x3B` | `XMarker` | Invisible in-world marker — used as waypoint anchors |

---

## Gotchas

- **`Items` is safe to initialize in-block** — it's an `ExtendedList` reference, not a FormLink struct. Unlike `ParentLocation` or `Base`, it won't crash in the constructor initializer.
- **All entries use `ToLink<IStarfieldMajorRecordGetter>()`** — the list holds the base interface; type specialization happens at runtime in Papyrus.
- **Never reference template mod FormLists directly** — copy items into a new `FormList(targetMod)` to avoid adding template mods as masters.
- **Search order matters** — always check `targetMod` before `TemplateMods`; cached copies in the target mod should take priority to avoid duplicate records.
