# Enchantment Chain (ObjectEffect / MagicEffect)

How equipment enchantments work in Starfield — the ObjectEffect → MagicEffect chain used by legendary armor OMODs, weapon enchantments, and other persistent item effects.

## Record chain

```
OMOD (Property = Enchantment) → ObjectEffect (ENCH) → Effect[0] → MagicEffect (MGEF)
```

| Record | Mutagen Type | Purpose |
|--------|-------------|---------|
| **ObjectEffect** | `ObjectEffect` (ENCH) | Wrapper — holds a list of `Effect` entries. Each Effect points to one MGEF + magnitude/duration/area. |
| **MagicEffect** | `MagicEffect` (MGEF) | The actual effect logic — archetype, target AV, cast type, flags, optional perk or script. |

An OMOD attaches the ENCH via `Armor.Property.Enchantment` (or `Weapon.Property.Enchantment`). The ENCH is applied to the player when the item is equipped.

## ObjectEffect (ENCH)

Minimal fields for a legendary armor enchantment:

```csharp
var ench = new ObjectEffect(targetMod)
{
    EditorID = "Ench_MyEffect",
    // Effects list added below
};
targetMod.ObjectEffects.Add(ench);

// Each Effect entry: BaseEffect (MGEF FormLink) + Magnitude + Duration + Area
// Magnitude is the "strength" — e.g. 0.25 for +25%
// Duration = 0 for constant effects
// Area = 0 for self-targeted effects
```

**Vanilla pattern:** All inspected legendary armor ENCHs have exactly 1 Effect entry. Bolstering is the exception — its OMOD has 2 Enchantment properties pointing to 2 separate ENCHs (one for energy resist, one for physical resist).

### Key fields

| Field | Value | Notes |
|-------|-------|-------|
| `Effects` | `ExtendedList<Effect>` | List of Effect entries |
| `DirtinessScale` | leave default | Seen as 100% inverse in vanilla |
| `ObjectBounds` | `new ObjectBounds()` | Zero bounds (0,0,0 → 0,0,0) |

## MagicEffect (MGEF)

Three patterns confirmed in vanilla legendary armor:

### Pattern 1: PeakValueModifier (simplest — no perk, no script)

Directly modifies an engine-native actor value. Used when the game engine reads the AV natively (e.g. O2 consumption, heal rate).

```
MGEF fields:
  Archetype.Type = PeakValueModifier
  ActorValue2   = target AVIF FormLink (e.g. PlayerOxygenUseMult)
  CastType      = ConstantEffect
  TargetType    = Self
  Flags         = Recover | Detrimental | NoDuration | NoArea | HideInUI
  PerkToApply   = Null
```

**Confirmed example:** `Legendary_Armor_OxygenFilter` (`2C43D9`) → modifies `PlayerOxygenUseMult` (`2D87CB`).

**When to use:** Stats targeting engine-native AVs like `PlayerOxygenUseMult`, `HealRateMult`, `SpeedMult`, `CarryWeight`, `OxygenRateMult`, env resist AVs. Needs verification per AV — not all AVs are read by the engine without a perk.

### Pattern 2: Script + PerkToApply (adds a perk to the player)

The MGEF applies a Perk record when the item is equipped. The perk has entry points that provide the gameplay logic. Used for conditional/triggered effects.

```
MGEF fields:
  Archetype.Type = Script
  CastType       = ConstantEffect
  TargetType     = Self
  Flags          = NoDuration | NoMagnitude | NoArea | HideInUI
  PerkToApply    = target Perk FormLink
```

**Confirmed examples:**

| MGEF | Perk Applied | Effect |
|------|-------------|--------|
| `Legendary_Armor_FallDamage_AddPerkEffect` (`0710FF`) | `ModFallDamage` (`0BA435`) | -50% fall damage |
| `abModLegendaryAddCommonPerk` (`1E684B`) | `ModLegendaryCommonPerk` (`1E6849`) | Shared perk — reads LGND_* AVs |
| `Legendary_Armor_Incendiary_AddPerkEffect` (`002986`) | Perk `00297F` | 10% ignite |
| `Legendary_Armor_CarryWeight_Resources_AddPerkEffect` (`0065B7`) | Perk `001301` | -25% resource weight |
| `Legendary_Armor_Disarm_AddPerkEffect` (`0612E7`) | Perk `0612F5` | Disarm chance |
| `Legendary_Armor_Stagger_AddPerkEffect` (`0612EC`) | Perk `0612F7` | Stagger reduction |

**Shared perk pattern:** Anti-Ballistic and Armor-Plated both use `Ench_ModLegendaryAddCommonPerk` → `ModLegendaryCommonPerk`. Their different effects come from their OMOD `ActorValue` properties setting different custom `LGND_*` AVs that the shared perk reads.

### Pattern 3: Script + Papyrus (custom script logic)

MGEF runs a Papyrus script via VirtualMachineAdapter. No perk — the script IS the logic.

```
MGEF fields:
  Archetype.Type       = Script
  CastType             = ConstantEffect
  TargetType           = Self
  Flags                = NoDuration | NoMagnitude | NoArea | HideInUI
  PerkToApply          = Null
  VirtualMachineAdapter = { Scripts = [...] }
```

**Confirmed example:** `Legendary_Armor_AutoHeal_ScriptEffect` (`0C9A3F`) — Auto-Medic, heals at <25% HP every 60s.

## MGEF Flags reference

Observed combinations on vanilla legendary armor MGEFs:

| Flag | Hex | Purpose |
|------|-----|---------|
| `Recover` | | Effect recovers when item is unequipped |
| `Detrimental` | | Marks as negative (for UI / resist calculations) |
| `NoDuration` | | Infinite duration (constant effect) |
| `NoMagnitude` | | Magnitude field ignored (Script archetype) |
| `NoArea` | | No area of effect |
| `HideInUI` | | Don't show in active effects list |
| `Painless` | | No hit reaction |

**PeakValueModifier pattern:** `Recover | Detrimental | NoDuration | NoArea | HideInUI`
**Script pattern:** `NoDuration | NoMagnitude | NoArea | HideInUI`

## Actor Values used by vanilla legendary armor

### Engine-native AVs (work with PeakValueModifier, no perk needed)

| AV | FormKey | DefaultValue | Notes |
|----|---------|-------------|-------|
| `PlayerOxygenUseMult` | `2D87CB` | 1.0 | O2 consumption multiplier. Flags: DefaultToOne, ValueLessThanOne, MultiplyByOneHundred. Min 0.1, Max 2.0. |
| `OxygenUseMult` | `147E2D` | 1.0 | Alternate O2 mult. ContextNote: "scales a perk". Min 0.1, Max 2.0. |

### Custom LGND_* AVs (inert without associated perk)

| AV | FormKey | ContextNotes |
|----|---------|-------------|
| `LGND_LessDmgBallistic` | `13369D` | Parameterizes Anti-Ballistic perk |
| `LGND_LessDmgBallisticParticle` | `20D255` | Secondary Anti-Ballistic parameter |
| `LGND_ArmorPlated` | `2CD667` | Parameterizes Armor-Plated perk |
| `FallingDamageMod` | `0BA43D` | *"for this actor value to actually do anything, the actor needs to have the associated perk"* |

## Minimum records for a new legendary armor stat

### Phase 1 stat (PeakValueModifier — engine-native AV)

4 records total:

1. **KYWD** — display keyword (e.g. "Regenerator")
2. **MGEF** — PeakValueModifier, targets the AV, ConstantEffect, Self
3. **ENCH** — ObjectEffect with 1 Effect entry pointing to the MGEF
4. **OMOD** — ArmorModification with properties: Value (MultAndAdd), Enchantment (Add → ENCH), Keyword (Add → KYWD)

### Phase 2 stat (Script + Perk — custom AV)

6 records total:

1. **KYWD** — display keyword
2. **AVIF** — custom actor value (Type=Variable, DefaultToZero)
3. **PERK** — perk with entry points reading the custom AV
4. **MGEF** — Script archetype, PerkToApply → the perk
5. **ENCH** — ObjectEffect with 1 Effect entry pointing to the MGEF
6. **OMOD** — ArmorModification with properties: Value, Enchantment → ENCH, Keyword → KYWD, ActorValue → custom AVIF

## OMOD attachment to armor

The OMOD's `AttachPoint` field is a FormLink to a Keyword that identifies the legendary tier slot:

| Tier | Keyword | FormKey |
|------|---------|---------|
| 1 | `ap_Legendary_rank_1` | `1E32C8:Starfield.esm` |
| 2 | `ap_Legendary_rank_2` | `329ABC:Starfield.esm` |
| 3 | `ap_Legendary_rank_3` | `329ABD:Starfield.esm` |
| 4 | `ap_Legendary_rank_4` | `3197E8:Starfield.esm` |

The LGDI (LegendaryItem) record maps OMODs to slots (First/Second/Third) which correspond 1:1 to these attachment points. Vanilla's `DefaultLegendaryArmor` (`1336C3:Starfield.esm`) has 31 OMODs across 3 slots.

## Open questions

- Which engine-native AVs actually work with PeakValueModifier without a perk? `PlayerOxygenUseMult` is confirmed. `HealRateMult`, `SpeedMult`, `CarryWeight` need in-game testing.
- LGDI IncludeFilter keywords (`23C7C1`, `23C7BF`, `23C7C0`) — gate body/helmet/pack slots. Need xEdit lookup (Mutagen keyword enumeration crashes before reaching them).
- Does the ENCH `Effect` entry's `Magnitude` field interact with PeakValueModifier, or is the magnitude set purely on the MGEF/AV side?
- Can we create a custom LGDI that extends vanilla's `DefaultLegendaryArmor` without overriding it?
