# Armor Stat Boosts — Research & Design Reference

## How Starfield Armor Stats Work

### Base Stats (on the ARMO record directly)
| Stat | Field | Type |
|------|-------|------|
| Physical DR | `ArmorRating` | `ushort` |
| EM Resist | `Resistances[0]` → DamageType `0x00023190` | `DamageTypeValue` |
| Energy Resist | `Resistances[1]` → DamageType `0x00060A81` | `DamageTypeValue` |

### Environmental Resistances (via OMODs or enchantments)
| Resist | Actor Value | Notes |
|--------|------------|-------|
| Thermal | `ENV_Resist_Thermal` | Heat/cold |
| Corrosive | `ENV_Resist_Corrosive` | Acid |
| Airborne | `ENV_Resist_Airborne` | Toxins |
| Radiation | `ENV_Resist_Radiation` (`0x248D2F`) | Radiation |

All capped at 85 in vanilla.

### Vanilla Legendary Perks (3 tiers, rolled on loot)

**Tier 1 (~13 perks):**
Ablative (-15% energy), Anti-Ballistic (-15% phys), Beast Hunter (-15% alien), Combat Veteran (-15% human), 4x env resists (+25 each), Analyzer (+10% dmg scanned), Hacker (+2 hack), Incendiary (10% ignite), O2 Filter (-25% O2), Sensor Chip (+20% accuracy moving)

**Tier 2 (~12 perks):**
Armor-Plated (-10% all), Acrobat (-50% fall), Assisted Carry (75% less O2 encumbered), Auto-Medic (heal <25%), Bolstering (+resist low HP), Chameleon (invis sneak), Fastened (+20 carry), Headhunter (+25% post-headshot), Mirrored (4% reflect), O2 Boosted (+20% O2), Repulsing (5% disarm), Resource Hauler (-25% resource weight)

**Tier 3 (~2 perks):**
Mechanized (+40 carry), Sentinel (75% chance -50% dmg standing still)

---

## Technical: How Perks Attach to Armor

### Armor.Property Enum (16 values — confirmed via Mutagen decompilation)

- `ActorValue` — the key one: points at an AVIF record via `ObjectModFormLinkFloatProperty<Armor.Property>`, can modify any of 1,223+ actor values
- `DamageResistance`, `Rating`, `Health`, `Weight`, `Value`
- `Enchantment` — attach a spell/magic effect chain
- `Keyword` — add/remove keywords
- `BodyPart`, `BlockMaterial`, `BashImpactDataSet`, `LayeredMaterialSwap`, `ColorRemappingIndex`, `AddonIndex`, `ModCount`

### Three Implementation Paths

| Path | Mechanism | Best for |
|------|-----------|----------|
| **OMOD + ActorValue** | `ObjectModFormLinkFloatProperty<Armor.Property>` with `Property = ActorValue`, `Record = AVIF FormLink` | Flat stat boosts (carry, speed, resist) |
| **OMOD + Enchantment** | `Armor.Property.Enchantment` pointing to ObjectEffect → Spell → MagicEffect chain | Persistent effects (HP regen, detection reduction) |
| **Perk Entry Point** | Perk record with entry points like `ModIncomingWeaponDamage`, `ApplyCombatHitSpell` | Conditional/triggered effects (proc on hit, low HP triggers) |

### Relevant Perk Entry Points (confirmed via decompilation, ~170 total)

- `ModDamageResistance`, `ModArmorRating`, `ModArmorWeight`
- `SetMaxCarryWeight`, `ModIncomingWeaponDamage`, `ModTypedIncomingWeaponDamage`
- `ModFallingDamage`, `ModReflectDamageChance`, `ModIncomingStagger`
- `ApplyCombatHitSpell` (proc-on-hit effects)

### Magic Effect Archetypes (for enchantment path)

- `ValueModifier` (0) — modifies one actor value by magnitude
- `DualValueModifier` (5) — modifies two actor values
- `PeakValueModifier` (34) — modifies one AV with keyword
- `Chameleon` (49), `Jetpack` (48), `Stimpack` (31) — Starfield-specific

### Key Actor Value FormIDs

| Actor Value | FormID | Notes |
|-------------|--------|-------|
| `DamageResist` | `0x000002E3` | Physical damage resistance |
| `EnergyResist` | `0x000002EB` | Energy damage resistance |
| `ElectromagneticDamageResist` | `0x00000392` | EM resistance |
| `ENV_Resist_Radiation` | `0x00248D2F` | Radiation |
| `Health` | needs lookup | HP pool |
| `CarryWeight` | needs lookup | Carry capacity |
| `SpeedMult` | needs lookup | Movement speed multiplier |
| `Oxygen` | needs lookup | O2 pool |

Full list: starfieldwiki.net Actor Value Indices (1,223+ values)

---

## Proposed New Stats

### Feasibility Tiers

**Tier A — OMOD ActorValue (simplest, most reliable):**
Flat stat modifications via `Armor.Property.ActorValue`. Just needs an AVIF FormID and a float value. No spells, no perks, no conditions.

**Tier B — Enchantment chain (moderate complexity):**
OMOD attaches an ObjectEffect → Spell → MagicEffect. For effects that don't map to a single actor value, or need continuous effects (like HP regen).

**Tier C — Perk + conditions (most complex):**
Full Perk record with entry points and conditions. For triggered/conditional effects. Most powerful but most records to create per stat.

---

### Combat Modifiers

| # | Name | Effect | Tier | Implementation |
|---|------|--------|------|----------------|
| 1 | **Kinetic Dampener** | -15% melee damage taken | A | ActorValue → melee damage resist AV, Add -0.15 |
| 2 | **Stagger Resist** | -30% stagger from hits | A | ActorValue → `StaggerResistMult` AV |
| 3 | **Fortified** | +25 to all 3 base resistances (phys/energy/EM) | A | 3x ActorValue entries on one OMOD |
| 4 | **Berserk Plating** | +25% damage dealt below 25% HP | C | Perk entry `ModDamageDealt` with `GetValuePercent Health < 0.25` condition |
| 5 | **Reactive Shielding** | +50 DR for 5s after taking damage | C | Perk entry `ApplyCombatHitSpell` → spell that buffs DamageResist |
| 6 | **Deflector** | -25% incoming crit damage | C | Perk entry `ModIncomingWeaponDamage` with IsCritical condition |
| 7 | **Thorns** | 10% of incoming damage reflected back | B | Enchantment with reflect damage effect, or Perk `ModReflectDamageChance` |
| 8 | **Last Stand** | +100 all DR when below 10% HP (once per fight) | C | Perk with HP condition + cooldown spell |

### Movement & Exploration

| # | Name | Effect | Tier | Implementation |
|---|------|--------|------|----------------|
| 9 | **Sprint Efficiency** | -25% O2 cost while sprinting | A | ActorValue → sprint O2 drain AV |
| 10 | **Deep Pockets** | +50 carry capacity | A | ActorValue → `CarryWeight`, Add +50 |
| 11 | **Jump Jets** | +25% jump height | A | ActorValue → `JumpMult`, MultAndAdd +0.25 |
| 12 | **Lightweight Frame** | +8% movement speed | A | ActorValue → `SpeedMult`, Add +8 |
| 13 | **Low-G Specialist** | +15% movement speed in low gravity | C | Perk with gravity condition → SpeedMult buff |
| 14 | **Featherfall** | -75% fall damage | A | ActorValue → fall damage mult AV |
| 15 | **Marathon Runner** | +30% max O2 | A | ActorValue → `Oxygen`, Add +30% |

### Stealth & Detection

| # | Name | Effect | Tier | Implementation |
|---|------|--------|------|----------------|
| 16 | **Signal Dampener** | +20% harder to detect while sneaking | A | ActorValue → detection AV (sneak modifier) |
| 17 | **Muffled Steps** | Eliminates footstep detection noise | B | Enchantment with muffle archetype, or ActorValue → `MuffledMovement` |
| 18 | **Thermal Masking** | -50% detection range by creatures | C | Perk `ModDetection` with `GetIsCreature` condition |
| 19 | **Scrambler Field** | Turrets/robots 30% less accurate against wearer | C | Perk `ModIncomingWeaponDamage` with robot condition |
| 20 | **Ghost Protocol** | +30% sneak attack damage | A | ActorValue → sneak attack mult AV |

### Survival & Sustain

| # | Name | Effect | Tier | Implementation |
|---|------|--------|------|----------------|
| 21 | **Regenerator** | Slow HP regen (2 HP/s out of combat) | B | Enchantment → Spell (Constant Effect) → MGEF ValueModifier on Health, RestoreAV |
| 22 | **Adrenaline Surge** | +15% speed for 10s when HP drops below 50% | C | Perk entry point with HP condition → buff spell |
| 23 | **Med Efficiency** | +25% healing from all sources | A | ActorValue → healing rate/mult AV |
| 24 | **Life Support** | +25% max O2 | A | ActorValue → `Oxygen` max |
| 25 | **Hazmat Lining** | +25 to all 4 environmental resistances | A | 4x ActorValue entries (Thermal/Corrosive/Airborne/Radiation) |
| 26 | **Rad Scrubber** | Slowly removes accumulated radiation | B | Enchantment → periodic RestoreAV on radiation |
| 27 | **Second Wind** | Auto-restore 25% O2 when depleted (60s cooldown) | C | Perk with O2 condition → restore spell + cooldown |
| 28 | **Vitality** | +15% max HP | A | ActorValue → `Health`, MultAndAdd +0.15 |

---

## Summary by Feasibility

| Tier | Count | Stats |
|------|-------|-------|
| **A — OMOD ActorValue** | 15 | Kinetic Dampener, Stagger Resist, Fortified, Sprint Efficiency, Deep Pockets, Jump Jets, Lightweight Frame, Featherfall, Marathon Runner, Signal Dampener, Ghost Protocol, Med Efficiency, Life Support, Hazmat Lining, Vitality |
| **B — Enchantment chain** | 4 | Thorns, Muffled Steps, Regenerator, Rad Scrubber |
| **C — Perk + conditions** | 9 | Berserk Plating, Reactive Shielding, Deflector, Last Stand, Low-G Specialist, Thermal Masking, Scrambler Field, Adrenaline Surge, Second Wind |

**Recommended starting set:** The 15 Tier A stats reuse the existing weapon OMOD generator pattern almost directly. The 4 Tier B stats need ObjectEffect + Spell + MGEF records. The 9 Tier C stats are most complex and could be a later phase.

---

## Implementation Prerequisites

1. **Look up remaining AVIF FormIDs** — Health, CarryWeight, SpeedMult, Oxygen, JumpMult, sneak-related AVs via xEdit or `help` console command
2. **Create armor OMOD generator** — parallel to weapon upgrade generator, using `Armor.Property` enum and YAML stat definitions
3. **Build custom LGDI** — replace vanilla `DefaultLegendaryArmor` with a custom legendary item definition that includes our new OMODs alongside vanilla ones
4. **Test in-game** — verify stats show on item card, apply correctly, and don't conflict with vanilla legendary system
