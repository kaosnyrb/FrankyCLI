# Weapon Upgrade (WeaponModification / OMOD)

Procedural weapon upgrade generation system — creates tiered weapon mods (OMODs), blueprint Books, ConstructibleObjects, and LeveledItem distribution lists.

## Record chain per upgrade

The core records are the OMOD and COBJ. The Global/Book/LeveledItem blueprint system is specific to the **Avontech Blacksite** mod — it gates recipes behind finding lootable data-slates in the world. A standalone weapon upgrade generator would only need the OMOD + COBJ.

| Record | Type | Purpose | Blacksite-specific? |
|--------|------|---------|:-------------------:|
| **WeaponModification** | `OMOD` | The actual stat-bearing weapon mod | No |
| **ConstructibleObject** | `COBJ` | Workbench recipe — optional perk + research gates | No |
| **Global** | `GLOB` | Boolean flag — `0` = locked, `>0` = recipe known | Yes |
| **Book** | `BOOK` | Blueprint data-slate — on pickup runs `atbb_recipepickup` script to set the Global | Yes |
| **LeveledItemEntry** | (entry) | Added to a split leveled list, gated by "not already known" condition | Yes |

### EditorID conventions

All prefixed `atbb_`:

```
atbb_omod_{originalOmodEditorID}_{statName}_{level}
atbb_g_{originalOmodEditorID}_{statName}_{level}
atbb_book_{originalOmodEditorID}_{statName}_{level}
atbb_co_{originalOmodEditorID}_{statName}_{level}
```

## WeaponModification (OMOD)

Deep-copied from a vanilla source OMOD, then modified:

```csharp
var omod = new WeaponModification(myMod)
{
    EditorID = omodeditorid,
    Name = originalmod.Name,            // overwritten later with stat-decorated name
    Description = stats.Description,
    Model = originalmod.Model,
    TargetOmodKeywords = originalmod.TargetOmodKeywords,
    FilterKeywords = originalmod.FilterKeywords,
    AttachPoint = originalmod.AttachPoint,
    AttachParentSlots = originalmod.AttachParentSlots,
    Includes = originalmod.Includes,
    Properties = originalmod.Properties, // stat properties added below
};
```

**DontShowInUI removal** — the keyword `0x00374EFA` is stripped from Properties so the upgrade appears on the item card:

```csharp
for (int i = 0; i < omod.Properties.Count; i++)
{
    if (type.Name == "ObjectModFormLinkIntProperty`1")
    {
        if (((ObjectModFormLinkIntProperty<Weapon.Property>)omod.Properties[i]).Record.FormKey.ID == 0x00374EFA)
        {
            omod.Properties.RemoveAt(i);
            break;
        }
    }
}
```

## OMOD Property types (AddStat)

Stats are added via `gen_upgradegenerator_utils.AddStat()`. Each stat type maps to a different `ObjectMod*Property`:

| Stat Type | Mutagen Property | Example use |
|-----------|-----------------|-------------|
| `Int` | `ObjectModIntProperty<Weapon.Property>` | Projectile count, ammo |
| `Float` | `ObjectModFloatProperty<Weapon.Property>` | Damage multiplier, fire rate |
| `BothFloat` | `ObjectModFloatProperty` with `Value = Value2` | Range (min+max) |
| `Enum` | `ObjectModEnumProperty<Weapon.Property>` | Silenced flag |
| `KeywordFloat` | `ObjectModFormLinkFloatProperty<Weapon.Property>` | Damage resistance (keyword + float) |
| `AddFormInt` | `ObjectModFormLinkIntProperty<Weapon.Property>` (Add) | Enchantment effect |
| `ModEnchant` | `ObjectModFormLinkIntProperty<Weapon.Property>` (Add) | Custom enchant (BlackSite mod) |
| `ProjectileOverride` | `ObjectModFormLinkIntProperty<Weapon.Property>` (Set) | Override projectile form |
| `Include` | `ObjectModInclude` added to `omod.Includes` | Attach another OMOD |
| `Group` | Recursive — calls AddStat for each child | Range (min+max grouped) |

Stats are defined in YAML files via `BonusStats`:

```csharp
public class BonusStats
{
    public string Type;                              // One of the types above
    public bool Percentage;                          // Display as percentage
    public Weapon.Property property;                 // Which weapon property to modify
    public ObjectModProperty.FloatFunctionType floatFunctionType; // Add/Multiply/Set
    public UInt32 Keyword;                           // FormID for keyword-based stats
    public string StatName;                          // Display name on item card
    public string ShortName;                         // Compact tag for OMOD name
    public decimal Default;                          // Base value at step 0
    public decimal Step;                             // Value added per step
    public bool Lootable = true;                     // If false, OMOD excluded from loot modgroups
    public List<string> OtherStats;                  // For Group type — child stat keys
}
```

## Blueprint Book

Data-slate that unlocks a recipe on pickup:

```csharp
var book = new Book(myMod)
{
    EditorID = editorBookid,
    ObjectBounds = new ObjectBounds(),
    Transforms = new Transforms() { Inventory = Inv_DefaultTransform_UP_X90_Y160_Z270_DataSlates },
    Name = ingameName,
    Model = new Model()
    {
        File = new AssetLink<StarfieldModelAssetType>("avontechblacksite\\dataslate.nif"),
    },
    Description = "Blueprint for a Avontech Blacksite ...",
    Value = 500,
    Weight = 0,
    VirtualMachineAdapter = new VirtualMachineAdapter()
    {
        Scripts = new ExtendedList<ScriptEntry>()
        {
            new ScriptEntry()
            {
                Name = "atbb_recipepickup",
                Properties = new ExtendedList<ScriptProperty>()
                {
                    new ScriptObjectProperty()
                    {
                        Name = "recipeglobal",
                        Object = global.ToLink<IStarfieldMajorRecordGetter>(),
                    }
                }
            }
        }
    },
};
```

### Key FormIDs

| FormID | Record | Use |
|--------|--------|-----|
| `0x000162A7` | `Inv_DefaultTransform_UP_X90_Y160_Z270_DataSlates` [TRNS] | Inventory transform for data-slate Books |
| `0x000796D5` | `Inv_Guns_Workbench3D_01` [TRNS] | Workbench transform for weapon-level Books |

## ConstructibleObject (COBJ)

Workbench recipe — creates the OMOD at a weapon workbench:

```csharp
var co = new ConstructibleObject(myMod)
{
    EditorID = coeditorid,
    Description = Description,
    CreatedObject = omod.FormKey.ToNullableLink<IConstructibleObjectTargetGetter>(),
    WorkbenchKeyword = WorkbenchWeaponKeyword,  // 0x002CE1C0
    AmountProduced = 1,
    LearnMethod = ConstructibleObject.LearnMethodEnum.DefaultOrConditions,
    Conditions = new ExtendedList<Condition>(),
    RequiredPerks = new ExtendedList<ConstructibleRequiredPerk>()
};
```

### Conditions

1. **Research requirement** — cloned from a vanilla COBJ's `IsResearchCompleteConditionData`, tiered by level and attach point
2. **Global unlock** (Blacksite-specific) — `GetGlobalValue > 0` on the blueprint's Global (must pick up Book first)

### Crafting cost

Scaled by level via `GetUpgradeCost()`:
- Level 0–50: 1 random resource (1–5 count)
- Level 50–100: 2 random resources
- Level 100+: 3 random resources

Resources drawn from `Data/basicresources.yaml` (list of `uint` FormIDs). Duplicates are avoided.

### Key FormIDs

| FormID | Record | Use |
|--------|--------|-----|
| `0x002CE1C0` | `WorkbenchWeaponKeyword` [KYWD] | Routes recipe to weapon workbench |
| `0x0000080A` | `WorkbenchBlacksiteKeyword` [KYWD] | Routes to custom Blacksite workbench (BlackSite mod) |
| `0x0000080C` | `WorkbenchBlacksiteFilterKeyword` [KYWD] | Filter for Blacksite workbench |
| `0x00000809` | `atbb_upgradeitem` [MISC] | Currency item for Blacksite workbench recipes |

## LeveledItem distribution

### Split structure (256-entry limit workaround)

Each weapon+OMOD pairing gets a **parent → N children** LeveledItem tree because Starfield caps entries at 256 per list:

```
atbb_{omodEditorID}              (parent — contains N child refs)
  ├─ atbb_{omodEditorID}_split_0 (child — holds actual Book entries)
  ├─ atbb_{omodEditorID}_split_1
  └─ ...split_4
```

Default `splitcount = 5`. Stats are randomly distributed across buckets.

### Per-weapon list

```
atbb_{weaponName}   (per-weapon list — refs to all OMOD parent lists for that weapon)
```

All per-weapon lists feed into `atbb_mainlist` (global distribution).

### Book entry gating

Books are added to leveled lists with a condition: `GetGlobalValue == 0` — once the player has the recipe, the book stops dropping.

```csharp
bookentry.Conditions.Add(new ConditionFloat()
{
    Data = con,
    CompareOperator = CompareOperator.EqualTo,
    ComparisonValue = 0
});
```

All lists use `Flag.CalculateFromAllLevelsLessThanOrEqualPlayer`.

## Modgroups (loot distribution)

Lootable OMODs are injected into vanilla weapon **modgroup** OMODs (names containing "modgroup"). This makes them appear on randomly generated weapons in the world.

```csharp
myMod.ObjectModifications[modkey].Includes.Add(new ObjectModInclude()
{
    DoNotUseAll = true,
    MinimumLevel = safelevel,
    Mod = includemod,
    Optional = true
});
```

**Capacity management** — hard limit of 9300 entries per modgroup. High-level mods are excluded from lower-tier groups:
- Level > 75 → skip group "01"
- Level > 120 → skip group "02"
- 10% random exclusion from group "03"
- Above 9000 entries → 50% random exclusion

`IsLootable` is false if **any** stat in the set has `Lootable = false` (stats that don't show on the item card, e.g. +HP).

## Attach points

Vanilla weapon part slots mapped to simplified categories:

| FormKey | Simplified | Original label |
|---------|-----------|----------------|
| `02249C:Starfield.esm` | Muzzle | Muzzle |
| `02249D:Starfield.esm` | Barrel | Barrel |
| `02EE28:Starfield.esm` | Laser | Laser |
| `14D08A:Starfield.esm` | Foregrip | Laser |
| `0191EE:Starfield.esm` | Laser | Laser |
| `149CA8:Starfield.esm` | Receiver | Casing |
| `01BC46:Starfield.esm` | Receiver | Cover |
| `024004:Starfield.esm` | Receiver | Receiver |
| `02249F:Starfield.esm` | Grip | Grip and Stock |
| `0849A6:Starfield.esm` | Stock | Grip and Stock |
| `147AFE:Starfield.esm` | Receiver | Internal |
| `05D4D7:Starfield.esm` | Magazine | Magazine and Battery |
| `022499:Starfield.esm` | Optic | Optic |
| `2FB3C2:Starfield.esm` | Handle | Handle (melee) |
| `2FB3C0:Starfield.esm` | Blade | Blade (melee) |

Some vanilla attach points are merged (e.g. Casing/Cover/Internal → Receiver) so stat sets can target broader categories via `AllowedAttachPoints`.

## Level styles

Controls how many tiers an upgrade has and their level spacing:

| Style | Start level | Steps | Level/step |
|-------|-------------|-------|------------|
| `Standard_Common` | 0 | 7 | 20 |
| `Standard_Rare` | 80 | 7 | 20 |
| `Standard_Epic` | 150 | 7 | 20 |
| `Standard_Legendary` | 220 | 7 | 20 |
| `Unique_Legendary` | -1 (random 50–200) | 1 | 0 |
| `Starter_Common` | 0 | 5 | 5 |
| `Starter_Rare` | 30 | 5 | 5 |
| `Enchant_Common` | 20 | 5 | 10 |
| `Enchant_Rare` | 60 | 5 | 10 |
| `Enchant_Epic` | 100 | 5 | 10 |
| `Enchant_Legendary` | 130 | 5 | 10 |

## Data-driven YAML files

| File | Type | Purpose |
|------|------|---------|
| `Data/comap.yaml` | `Dict<string,string>` | Maps COBJ EditorIDs that don't follow `co_gun_{omodEditorID}` convention |
| `Data/weaponmodel.yaml` | `Dict<string,string>` | Weapon name → NIF model path for workbench Books |
| `Data/basicresources.yaml` | `List<uint>` | Pool of inorganic resource FormIDs for crafting costs |
| `Data/perks.yaml` | `Dict<string,uint>` | Perk name → FormID lookup |
| `Data/replacemap.yaml` | `Dict<string,string>` | Word replacements to clean up OMOD display names (e.g. "Standard Barrel" → "") |
| `Data/WeaponNameMap.yaml` | `Dict<string,string>` | Internal weapon ID → clean display name |
| `Data/bannedomods.yaml` | `List<string>` | Substring patterns for OMODs to skip (modgroups, special entries) |

## UpdateSetRequest (YAML input)

Top-level config passed to `Generate()`:

```csharp
public class UpdateSetRequest
{
    public int DamageMode;                // 0=Energy, 1=EM, 2=Phys — filters percentage damage stats
    public List<string> StatLibFile;      // Directories containing StatSet YAML files
    public string ScalingStats;           // Directory containing BonusStats YAML files
    public string ThemeFile;              // Theme file for roman numeral naming
    public List<string> Weapons;          // Weapon IDs to process (matched against OMOD EditorIDs)
    public string WeaponESM = "Starfield.esm"; // Source ESM containing the weapons
}
```

## StatSet (YAML)

Defines a named upgrade archetype (e.g. "Marksman", "Berserker"):

```csharp
public class StatSet
{
    public string Name;                   // Display name prefix
    public string Description;            // Tooltip prefix
    public string LevelStyle;             // Key into levelStyles dictionary
    public int DamageMode = -1;           // -1=all, 0=Energy, 1=EM, 2=Phys
    public string Theme = "Miltec";       // Theme key for level naming
    public string RequiredPerk = "";      // Perk gate (empty = none)
    public uint RequiredPerkLevel = 0;    // Minimum perk rank
    public List<string> stats;            // Keys into StatBank
    public List<string> AllowedAttachPoints; // Which attach categories this set applies to
}
```

## Papyrus scripts (Blacksite-specific)

These scripts are part of the Avontech Blacksite blueprint discovery system, not the core OMOD/COBJ upgrade pattern.

| Script | Attached to | Properties | Behaviour |
|--------|-------------|------------|-----------|
| `atbb_recipepickup` | Blueprint Book (per-upgrade) | `recipeglobal` → Global | Sets Global to 1 on pickup, unlocking the recipe |
| `atbb_additem` | Weapon-level Book | `LevelledItem` → LeveledItem | Rolls a random blueprint from the weapon's leveled list on pickup |

## Research requirements

Cloned from vanilla `IsResearchCompleteConditionData` on existing ConstructibleObjects, tiered by level bracket and attach point:
- **Level < 50**: no research required
- **Level 50–99**: basic weapon research per part type
- **Level 100–199**: intermediate research
- **Level 200+**: advanced research

Each bracket has a hardcoded FormID per attach point (see `GetPartResearchReq()`).

## Compression gotcha

All records must have `IsCompressed = false` — Mutagen does not support writing compressed records:

```csharp
foreach (var rec in myMod.EnumerateMajorRecords())
{
    rec.IsCompressed = false;
}
```
