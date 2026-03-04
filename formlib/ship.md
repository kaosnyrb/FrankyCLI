# GenericBaseForm (GBFM) — Encounter Ship

Encounter ships in Starfield are `GenericBaseForm` records. The GBFM holds the ship's
mesh template, components, and faction data. `SpaceShipNoun.cs` clones a vanilla ship
from `Starfield.esm` into the target mod and rewires faction links.

---

## Safe fields to copy when cloning

These fields are safe to deep-copy from the source ship to the new GBFM:

| Field | Notes |
|---|---|
| `ObjectBounds` | Direct copy |
| `DirtinessScale` | Direct copy |
| `ObjectPaletteDefaults` | Direct copy |
| `Components` | Deep-copy list — contains `ExternalDataSourceComponent`, `FullNameComponent`, etc. |
| `Filter` | Direct copy |
| `ObjectTemplates` | Direct copy |
| `VirtualMachineAdapter` | Direct copy |
| `NavmeshGeometry` | Safe to copy; cargo ships typically have none |
| `Template` | `IFormLinkNullable` — must be set AFTER construction with `instance.Template.SetTo(ship.Template.FormKey)` |

---

## ObjectTemplateInstanceData gotcha — copy ONLY index [0]

`GenericBaseForm.ObjectTemplateInstanceData` is an `ExtendedList<string>` that typically
has two entries when copied from vanilla:

| Index | Size | Content | Rule |
|---|---|---|---|
| `[0]` | 25 bytes | Core ship instance data | **Always copy** |
| `[1]` | 22 bytes | `Spaceship_InstanceData` — tied to the **original ship's FormKey** | **Never copy when cloning** |

Copying `[1]` to a new FormKey breaks docking/boarding. **Confirmed by gen_shipcompare**
— this was the root cause of ships not being boardable.

**Correct pattern:**
```csharp
ObjectTemplateInstanceData = ship.ObjectTemplateInstanceData?.Count > 0
    ? new ExtendedList<string> { ship.ObjectTemplateInstanceData[0] }
    : null,
```

**Wrong (old code in SpaceShipNoun.cs):**
```csharp
ObjectTemplateInstanceData = ship.ObjectTemplateInstanceData,  // BAD — copies [1] which breaks boarding
```

---

## ExternalDataSourceComponent — faction injection

Faction data lives inside `ExternalDataSourceComponent` (one of the entries in `instance.Components`).
Each `Source` entry has a `Name` string key and a `Source` FormLink to a `LeveledBaseForm`.

| Source name | Role |
|---|---|
| `"FACTIONS"` | Which faction the ship belongs to |
| `"AIDATA"` | Combat AI behaviour package |
| `"TRAITS"` | NPC trait/leveling data |

To replace factions when cloning:
```csharp
foreach (var component in instance.Components)
{
    if (component is ExternalDataSourceComponent extComp)
    {
        var shipTemplate = starfieldMod.LeveledBaseForms[new FormKey(starfieldModKey, factionLvlBaseFormId)];
        bool setFaction = false;
        foreach (var source in extComp.Sources)
        {
            if (source.Name == "FACTIONS") { source.Source = shipTemplate.ToLink<IExternalBaseTemplateGetter>(); setFaction = true; }
            if (source.Name == "AIDATA")   { source.Source = shipTemplate.ToLink<IExternalBaseTemplateGetter>(); }
            if (source.Name == "TRAITS")   { source.Source = shipTemplate.ToLink<IExternalBaseTemplateGetter>(); }
        }
        if (!setFaction)
            extComp.Sources.Add(new ExternalDataSource { Name = "FACTIONS", Source = shipTemplate.ToLink<IExternalBaseTemplateGetter>() });
    }
}
```

---

## LeveledBaseForm faction FormIDs (Starfield.esm)

These are `LeveledBaseForm` records used as faction sources in `ExternalDataSourceComponent`:

| Faction string | LvlBaseForm FormID | Notes |
|---|---|---|
| `"Crimson Fleet"` / `"CrimsonFleet"` | `0x000B1375` | |
| `"Spacer"` | `0x000B13A8` | |
| `"Ecliptic"` | `0x000AE4F3` | |
| `"Varuun"` | `0x000B19CF` | |
| `"UC Navy"` | `0x000D320E` | |
| `"UC Vanguard"` | `0x000D1859` | |
| `"Freestar Security"` | `0x000CA78D` | |
| `"UC SysDef"` | `0x000DBC51` | |
| `"Trade Authority"` | `0x000AE4D0` | |
| `"Galbank"` | `0x0034BB12` | |
| `"Trackers Alliance"` | `0x000AE4D3` | |

See `ShipTools.GetFactionID(string faction)` for the canonical lookup.

---

## Gang member FormLists — no template deps

`ShipTools.GetGangList(uint factionId)` returns a gang-member FormList that is safe
to link from the target mod. It copies the template's FormList into the target mod on first
call (keyed by `frmlist_ganglist_{editorId}`), so the output ESM doesn't declare a template
mod as a master.

---

## Ship class FormID pools (Starfield.esm)

`ShipTools` exposes three pools for selecting vanilla ship base forms:

- `GetCargoShip()` — trade/civilian cargo ships (UC Citizen, Trade Authority, Star Parcel)
- `GetAClassShip()` — A-class combat ships (Crimson Fleet, UC, Ecliptic, Freestar, Spacer, The First)
- `GetBClassShip()` — B-class combat ships (most major factions)

Each returns a random `uint` FormID from `Starfield.esm`. Use with:
```csharp
var ship = starfieldMod.GenericBaseForms[new FormKey(starfieldModKey, ShipTools.GetBClassShip())].DeepCopy();
```

---

## FullNameComponent — ship display name

To set the ship's in-game name, find `FullNameComponent` in `instance.Components`:
```csharp
if (component is FullNameComponent fullName)
    fullName.Name = shipName;
```

---

## Key files

- `Retrograde.Library/Nouns/SpaceShipNoun.cs` — clones GBFM, wires faction + name
- `Retrograde.Library/Utils/ShipTools.cs` — FormID pools, faction IDs, gang lists, name generators
