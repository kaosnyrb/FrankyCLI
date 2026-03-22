# Armor Stat Boosts — Research & Design Reference

## How Starfield Armor Stats Work

### Base Stats (on the ARMO record directly)
| Stat | Field | Type |
|------|-------|------|
| Physical DR | `ArmorRating` | `ushort` |
| EM Resist | `Resistances[0]` → DamageType `0x00023190` | `DamageTypeValue` |
| Energy Resist | `Resistances[1]` → DamageType `0x00060A81` | `DamageTypeValue` |

### Environmental Resistances (via OMODs or enchantments)
| Resist | Actor Value | FormID |
|--------|------------|--------|
| Thermal | `ENV_Resist_Thermal` | `0x00248D32` |
| Corrosive | `ENV_Resist_Corrosive` | `0x00248D30` |
| Airborne | `ENV_Resist_Airborne` | `0x00248D31` |
| Radiation | `ENV_Resist_Radiation` | `0x00248D2F` |

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

### Magic Effect Archetypes (for enchantment path)

- `ValueModifier` (0) — modifies one actor value by magnitude
- `DualValueModifier` (5) — modifies two actor values
- `PeakValueModifier` (34) — modifies one AV with keyword
- `Chameleon` (49), `Jetpack` (48), `Stimpack` (31) — Starfield-specific

---

## Actor Value Reference

### Combat

| Actor Value | FormID | Notes |
|-------------|--------|-------|
| `DamageResist` | `0x000002E3` | Physical DR |
| `EnergyResist` | `0x000002EB` | Energy DR |
| `ElectromagneticDamageResist` | `0x00000392` | EM DR |
| `AttackDamageMult` | `0x00000357` | Universal damage multiplier |
| `CriticalHitChance` | `0x000002DD` | Crit chance |
| `CriticalHitDamageMult` | `0x0000039C` | Crit damage multiplier |
| `CriticalHitIncMult` | `0x0000039B` | Crit meter fill rate |
| `AimStability` | `0x0000035B` | Weapon aim stability |
| `PlayerIncomingWeaponDamageMult` | `0x0030397A` | Incoming weapon damage mult |
| `PlayerMeleeDamageMult` | `0x003039AF` | Player melee damage mult |
| `Player_Explosive_IncomingDamageReduction` | `0x0022F93C` | Explosive DR |
| `ElectromagneticRecoverRate` | `0x000003A0` | EM recovery rate |
| `ElectromagneticRecoverRateMult` | `0x0000039F` | EM recovery rate mult |

### Legendary Damage Reduction AVs (vanilla uses these)

| Actor Value | FormID | Notes |
|-------------|--------|-------|
| `LGND_ArmorPlated` | `0x002CD667` | All damage reduction |
| `LGND_LessDmgBallistic` | `0x0013369D` | Less ballistic damage |
| `LGND_LessDmgCreatures` | `0x001F1DEC` | Less creature damage |
| `LGND_LessDmgHumans` | `0x001F1DF2` | Less human damage |
| `LGND_LessDmgRobots` | `0x001F81E6` | Less robot damage |
| `LGND_LessDmgStandStill` | `0x002C4121` | Less damage standing still |

### Movement & Speed

| Actor Value | FormID | Notes |
|-------------|--------|-------|
| `SpeedMult` | enum idx 30 | Movement speed multiplier |
| `JumpMult` | `0x00040CDC` | Jump height multiplier |
| `JumpSpeedMult` | `0x0020317B` | Jump speed |
| `JumpCostMult` | `0x00004213` | Jump O2 cost multiplier |
| `FallSpeedMult` | `0x00000336` | Fall speed |
| `AnimationMult` | enum idx? | Animation speed multiplier |
| `CarryWeight` | enum idx 32 | Carry weight |
| `PlayerEquippedArmorWeightMult` | `0x0024B8A5` | Equipped armor weight mult |
| `PlayerEquippedWeaponWeightMult` | `0x0024B8A6` | Equipped weapon weight mult |

### Boostpack

| Actor Value | FormID | Notes |
|-------------|--------|-------|
| `BoostpackFuel` | `0x00024021` | Current fuel |
| `BoostPackHoverFuelDrainAV` | `0x0013BB22` | Hover fuel drain |
| `BoostpackDrainInitial` | `0x00355249` | Initial fuel drain |
| `BoostpackDrainSustained` | `0x0035524A` | Sustained fuel drain |
| `BoostpackThrustInitial` | `0x00355247` | Initial thrust |
| `BoostpackThrustSustained` | `0x00355248` | Sustained thrust |
| `BoostpackMinFuelRequired` | `0x0035524C` | Min fuel to activate |
| `BoostpackLowGravityDampeningMult` | `0x00355251` | Low-G dampening |
| `BoostpackHighGravityDampeningMult` | `0x00355252` | High-G dampening |
| `BoostpackFallingThrustMult` | `0x00355253` | Falling thrust mult |

### Health & Survival

| Actor Value | FormID | Notes |
|-------------|--------|-------|
| `Health` | enum idx 24 | HP pool |
| `HealRate` | enum idx 27 | HP regen rate |
| `HealRateMult` | `0x00000358` | HP regen rate multiplier |
| `CombatHealthRegenMult` | `0x00000343` | In-combat HP regen mult |
| `OxygenUseMult` | `0x00147E2D` | O2 consumption multiplier |
| `PlayerOxygenUseMult` | `0x002D87CB` | Player-specific O2 mult |
| `Player_Sprint_O2_DrainRate` | `0x0022F93D` | Sprint O2 drain rate |
| `OxygenRate` | enum idx? | O2 recovery rate |
| `OxygenRateMult` | `0x00000359` | O2 recovery rate mult |
| `CarbonDioxideRate` | `0x00000351` | CO2 rate |
| `CarbonDioxideRateMult` | `0x00000352` | CO2 rate mult |
| `PlayerFirstAidMagnitudeMult` | `0x0023B923` | First aid effectiveness |
| `PlayerFoodMagnitudeMult` | `0x003039B3` | Food effectiveness |
| `ENV_Damage_Soak` | `0x00000313` | Environmental damage soak |
| `ENV_DamageRateMult` | `0x00000355` | Environmental damage rate mult |

### Affliction Prevention & Cure

| Actor Value | FormID | Notes |
|-------------|--------|-------|
| `ENV_AFFL_BonusCureChance_Infection` | `0x00205A14` | Bonus cure chance (infections) |
| `ENV_AFFL_BonusCureChance_Injury` | `0x00205A13` | Bonus cure chance (injuries) |
| `ENV_AFFL_SkillChance_PreventInfection` | `0x000C9AF4` | Chance to prevent infection |
| `ENV_AFFL_SkillChance_PreventInjury` | `0x000C9AF6` | Chance to prevent injury |

### Stealth & Detection

| Actor Value | FormID | Notes |
|-------------|--------|-------|
| `MovementNoiseMult` | `0x00000319` | Movement noise multiplier |
| `Player_Detection_Light` | `0x002EC4F5` | Player light-based detection |
| `Player_Detection_Movement` | `0x002EC4ED` | Player movement-based detection |
| `Invisibility` | enum idx 54 | Invisibility flag |
| `DetectLifeRange` | enum idx 56 | Detect life range |

### Social & Economy

| Actor Value | FormID | Notes |
|-------------|--------|-------|
| `PlayerPersuasionSkillSuccessChanceMult` | `0x002D873B` | Persuasion success chance |
| `BypassVendorStolenCheck` | `0x0000031A` | Sell stolen goods |
| `MapMarkerMaxCompassDistanceMult` | `0x0025E14A` | Compass range |
| `MissionRewardMultiplier` | `0x0006BAD3` | Mission reward mult |

### Crafting

| Actor Value | FormID | Notes |
|-------------|--------|-------|
| `ComponentUsageMultChemical` | `0x00000368` | Chemical component usage |
| `ComponentUsageMultMetallic` | `0x00000369` | Metallic component usage |
| `ComponentUsageMultOrganic` | `0x0000036A` | Organic component usage |
| `ComponentUsageMultTechnical` | `0x0000036B` | Technical component usage |

### Outpost & Scanning

| Actor Value | FormID | Notes |
|-------------|--------|-------|
| `OutpostScannerMultiplier` | `0x002CBE73` | Scanner multiplier |
| `OutpostScannerRadius` | `0x002CBE74` | Scanner radius |
| `OutpostBuildAreaRadius` | `0x00063FC5` | Build area radius |

---

## Perk Entry Points Reference (armor-relevant subset)

### Damage Modification
| Entry Point | Notes |
|-------------|-------|
| `ModIncomingWeaponDamage` | Modify incoming weapon damage (condition on damage type, attacker type) |
| `ModTypedIncomingWeaponDamage` | Modify incoming damage by type (physical/energy/EM) |
| `ModIncomingExplosionDamage` | Modify incoming explosion damage |
| `ModIncomingStagger` | Modify incoming stagger |
| `ModDamageResistance` | Modify damage resistance |
| `ModArmorRating` | Modify armor rating |
| `ModArmorResistance` | Modify armor resistance |
| `ModReflectDamageChance` | Modify damage reflection chance |
| `ModFallingDamage` | Modify falling damage |
| `ModIncomingLimbDamage` | Modify incoming limb damage |
| `ModWeaponAttackDamage` | Modify outgoing weapon damage |
| `ModSneakAttackMult` | Modify sneak attack multiplier |

### Spell/Effect Triggers
| Entry Point | Notes |
|-------------|-------|
| `ApplyCombatHitSpell` | Apply spell when hit in combat (on attacker) |
| `ApplyCombatHitSpellSelf` | Apply spell when hit in combat (on self) |
| `ApplyJetpackSpell` | Apply spell when using boostpack |
| `ApplyAimDownSightSpell` | Apply spell when aiming down sights |
| `ApplyStartMantleSpell` | Apply spell when mantling |
| `ApplyStartCombatSlideSpell` | Apply spell when combat sliding |
| `ApplySneakingSpell` | Apply spell while sneaking |

### Movement & Carry
| Entry Point | Notes |
|-------------|-------|
| `SetMaxCarryWeight` | Set max carry weight |
| `ModArmorWeight` | Modify armor weight |
| `ModPlayerGravity` | Modify player gravity! |
| `ModJumpingOxygen` | Modify O2 cost of jumping |
| `ModJetpackFuelDrainMult` | Modify boostpack fuel drain |
| `ModSprintOxygenDrainRate` | Modify sprint O2 drain |
| `ModOxygenUse` | Modify O2 usage |

### Detection & Stealth
| Entry Point | Notes |
|-------------|-------|
| `ModDetectionLight` | Modify light-based detection |
| `ModDetectionMovement` | Modify movement-based detection |
| `ModDetectionSneakSkill` | Modify sneak skill detection |

### Combat Stats
| Entry Point | Notes |
|-------------|-------|
| `ModMyCriticalHitChance` | Modify crit chance |
| `ModMyCriticalHitDamageMult` | Modify crit damage |
| `ModConeoffireMult` | Modify weapon cone of fire (accuracy) |
| `ModWeaponReloadSpeed` | Modify reload speed |
| `ModMagazineSize` | Modify magazine size |
| `ModRecoveredHealth` | Modify recovered health amount |
| `ModActorScopeStability` | Modify scope stability |

### Social & Economy
| Entry Point | Notes |
|-------------|-------|
| `ModBuyPrices` | Modify buy prices |
| `ModSellPrices` | Modify sell prices |
| `ModPersuasion_Skill_Player_Success_Chance` | Modify persuasion success |
| `ModPlayer_Bounty` | Modify player bounty |
| `ModExpLocation` | Modify location XP |
| `ModKillExperience` | Modify kill XP |
| `ModExp` | Modify general XP |

### Crafting & Workshop
| Entry Point | Notes |
|-------------|-------|
| `ModCraftingDupeChance` | Chance to duplicate crafted item |
| `ModCraftingFreebieChance` | Chance to craft for free |
| `ModCraftingCost` | Modify crafting cost |
| `ModCraftingModFreebieChance` | Chance for free mod |
| `ModCraftingReturnQuantity` | Modify return quantity on scrapping |
| `ModScrapRewardMult` | Modify scrap rewards |
| `ModResearchCost` | Modify research cost |
| `ModResearchCritChance` | Modify research crit chance |

### Bleedout System
| Entry Point | Notes |
|-------------|-------|
| `ModBleedoutChance` | Modify chance to enter bleedout instead of dying |
| `ModBleedoutRecoverChance` | Modify chance to recover from bleedout |
| `ModBleedoutHealthPercent` | Modify HP percent at bleedout |

---

## Condition Functions Reference (armor-relevant subset)

### Health & Resource Thresholds
| Function | Notes |
|----------|-------|
| `GetHealthPercentage` (430) | Returns 0.0-1.0 |
| `GetValuePercent` | Any actor value as percentage |
| `GetValue` (14) | Raw actor value |
| `GetUsedWeightCapacityConditionFunction` | Carry weight check |

### Combat State
| Function | Notes |
|----------|-------|
| `IsInCombat` (289) | In combat |
| `GetCombatState` (323) | 0=none, 1=combat, 2=searching |
| `IsAttacking` (672) | Currently attacking |
| `IsBlocking` (569) | Currently blocking |
| `IsSneaking` (286) | Sneaking |
| `IsSprinting` (568) | Sprinting |
| `IsBleedingOut` (580) | Downed |
| `IsWeaponOut` (263) | Weapon drawn |
| `GetInIronSights` | Aiming down sights |
| `EPGetLastCombatHitCritical` | Last hit was crit |
| `EPGetLastCombatHitKill` | Last hit was kill |
| `GetLastCombatHitActorConsecutiveHits` | Hit streak counter |
| `EPIsDamageType` | Damage type check |

### Movement & Activity
| Function | Notes |
|----------|-------|
| `IsMoving` (25) | Moving |
| `IsRunning` (287) | Running |
| `GetMovementSpeed` (623) | Current speed |
| `GetIsFloating` | Zero-G floating |
| `GetFlyingState` (633) | Flying/boostpack |

### Environment & Planet
| Function | Notes |
|----------|-------|
| `GetBodyGravity` (871) | Planet gravity value |
| `GetBodyTemperature` (869) | Planet temperature |
| `GetBodyPressure` (870) | Planet pressure |
| `IsInInterior` (300) | In interior cell |
| `ActorExposedToSky` | Under open sky |
| `BodyHasKeyword` (858) | Planet keyword check |
| `BiomeHasKeyword` (859) | Biome keyword check |
| `GetPlayerGravityScale` | Player gravity multiplier |

### Target Type
| Function | Notes |
|----------|-------|
| `GetIsCreature` (64) | Is creature |
| `GetIsCreatureType` (437) | Creature type |
| `GetIsRace` (69) | Race check |
| `HasKeyword` (560) | Keyword check |
| `IsHostileToActor` (719) | Hostility check |

### Equipment
| Function | Notes |
|----------|-------|
| `WornHasKeyword` (682) | Worn item has keyword |
| `WornApparelHasKeywordCount` (722) | Count of worn items with keyword |
| `GetEquipped` (182) | Has item equipped |
| `HasMagicEffect` (214) | Has active magic effect |

---

## Proposed New Stats

### Feasibility Tiers

**Tier A — OMOD ActorValue (simplest, most reliable):**
Flat stat modifications via `Armor.Property.ActorValue`. Just needs an AVIF FormID and a float value.

**Tier B — Enchantment chain (moderate complexity):**
OMOD attaches an ObjectEffect → Spell → MagicEffect.

**Tier C — Perk + conditions (most complex):**
Full Perk record with entry points and conditions. Most powerful but most records per stat.

---

### Combat Modifiers

| # | Name | Effect | Tier | Implementation |
|---|------|--------|------|----------------|
| 1 | **Kinetic Dampener** | -15% melee damage taken | A | ActorValue → melee damage resist AV |
| 2 | **Stagger Resist** | -30% stagger from hits | A | ActorValue → stagger resist mult AV |
| 3 | **Fortified** | +25 to all 3 base DR (phys/energy/EM) | A | 3x ActorValue entries on one OMOD |
| 4 | **Berserk Plating** | +25% damage dealt below 25% HP | C | Perk `ModWeaponAttackDamage` + `GetHealthPercentage < 0.25` |
| 5 | **Reactive Shielding** | +50 DR for 5s after taking a hit | C | Perk `ApplyCombatHitSpellSelf` → DamageResist buff spell |
| 6 | **Deflector** | -25% incoming crit damage | C | Perk `ModIncomingWeaponDamage` + `EPGetLastCombatHitCritical` |
| 7 | **Thorns** | 10% reflect damage chance | C | Perk `ModReflectDamageChance` |
| 8 | **Last Stand** | +100 all DR when below 10% HP | C | Perk `ModDamageResistance` + `GetHealthPercentage < 0.1` |
| 9 | **Blast Shield** | -25% incoming explosion damage | C | Perk `ModIncomingExplosionDamage` |
| 10 | **Crit Amplifier** | +15% crit chance | A | ActorValue → `CriticalHitChance` (`0x000002DD`), Add +15 |
| 11 | **Momentum** | +5% damage per consecutive hit (max 25%) | C | Perk `ModWeaponAttackDamage` + `GetLastCombatHitActorConsecutiveHits` |
| 12 | **EM Recovery** | +50% EM recovery rate | A | ActorValue → `ElectromagneticRecoverRateMult` (`0x0000039F`) |

### Movement & Exploration

| # | Name | Effect | Tier | Implementation |
|---|------|--------|------|----------------|
| 13 | **Sprint Efficiency** | -25% O2 cost while sprinting | C | Perk `ModSprintOxygenDrainRate`, MultAndAdd -0.25 |
| 14 | **Deep Pockets** | +50 carry capacity | A | ActorValue → `CarryWeight` (enum 32), Add +50 |
| 15 | **Jump Jets** | +25% jump height | A | ActorValue → `JumpMult` (`0x00040CDC`), Add +0.25 |
| 16 | **Lightweight Frame** | +8% movement speed | A | ActorValue → `SpeedMult` (enum 30), Add +8 |
| 17 | **Low-G Specialist** | -20% gravity effect on player | C | Perk `ModPlayerGravity`, MultAndAdd -0.2 |
| 18 | **Featherfall** | -75% fall damage | C | Perk `ModFallingDamage`, MultAndAdd -0.75 |
| 19 | **Marathon Runner** | -20% O2 consumption | A | ActorValue → `OxygenUseMult` (`0x00147E2D`), Add -0.2 |
| 20 | **Light Armor Weave** | -30% equipped armor weight | A | ActorValue → `PlayerEquippedArmorWeightMult` (`0x0024B8A5`), Add -0.3 |
| 21 | **Boostpack Efficiency** | -25% boostpack fuel drain | C | Perk `ModJetpackFuelDrainMult`, MultAndAdd -0.25 |
| 22 | **Boostpack Thrust** | +20% boostpack thrust | A | ActorValue → `BoostpackThrustInitial` (`0x00355247`), MultAndAdd +0.2 |
| 23 | **Low Jump Cost** | -30% O2 cost of jumping | A | ActorValue → `JumpCostMult` (`0x00004213`), Add -0.3 |
| 24 | **Compass Range** | +50% compass marker distance | A | ActorValue → `MapMarkerMaxCompassDistanceMult` (`0x0025E14A`), Add +0.5 |

### Stealth & Detection

| # | Name | Effect | Tier | Implementation |
|---|------|--------|------|----------------|
| 25 | **Signal Dampener** | -20% movement detection | A | ActorValue → `Player_Detection_Movement` (`0x002EC4ED`), Add -0.2 |
| 26 | **Shadow Weave** | -20% light-based detection | A | ActorValue → `Player_Detection_Light` (`0x002EC4F5`), Add -0.2 |
| 27 | **Muffled Steps** | -50% movement noise | A | ActorValue → `MovementNoiseMult` (`0x00000319`), MultAndAdd -0.5 |
| 28 | **Ghost Protocol** | +30% sneak attack damage | C | Perk `ModSneakAttackMult`, AddValue +0.3 |
| 29 | **Sneak Spell** | Cast spell while entering sneak | C | Perk `ApplySneakingSpell` → buff spell (e.g. temporary detection reduction) |
| 30 | **Predator Sense** | +50% detect life range | A | ActorValue → `DetectLifeRange` (enum 56), MultAndAdd +0.5 |

### Survival & Sustain

| # | Name | Effect | Tier | Implementation |
|---|------|--------|------|----------------|
| 31 | **Regenerator** | +50% HP regen rate | A | ActorValue → `HealRateMult` (`0x00000358`), Add +0.5 |
| 32 | **Combat Medic** | +25% in-combat HP regen | A | ActorValue → `CombatHealthRegenMult` (`0x00000343`), Add +0.25 |
| 33 | **Med Efficiency** | +25% first aid effectiveness | A | ActorValue → `PlayerFirstAidMagnitudeMult` (`0x0023B923`), Add +0.25 |
| 34 | **Well Fed** | +25% food healing effectiveness | A | ActorValue → `PlayerFoodMagnitudeMult` (`0x003039B3`), Add +0.25 |
| 35 | **Life Support** | -20% O2 consumption | A | ActorValue → `PlayerOxygenUseMult` (`0x002D87CB`), Add -0.2 |
| 36 | **O2 Recovery** | +25% O2 recovery rate | A | ActorValue → `OxygenRateMult` (`0x00000359`), Add +0.25 |
| 37 | **Hazmat Lining** | +25 to all 4 env resistances | A | 4x ActorValue (Thermal/Corrosive/Airborne/Radiation) |
| 38 | **Env Damage Soak** | +20 environmental damage soak | A | ActorValue → `ENV_Damage_Soak` (`0x00000313`), Add +20 |
| 39 | **Infection Resist** | +25% chance to prevent infections | A | ActorValue → `ENV_AFFL_SkillChance_PreventInfection` (`0x000C9AF4`), Add +0.25 |
| 40 | **Injury Resist** | +25% chance to prevent injuries | A | ActorValue → `ENV_AFFL_SkillChance_PreventInjury` (`0x000C9AF6`), Add +0.25 |
| 41 | **Rapid Cure** | +25% cure chance for afflictions | A | ActorValue → `ENV_AFFL_BonusCureChance_Infection` (`0x00205A14`) + Injury (`0x00205A13`) |
| 42 | **Vitality** | +15% max HP | A | ActorValue → `Health` (enum 24), MultAndAdd +0.15 |
| 43 | **Second Wind** | +25% recovered health | C | Perk `ModRecoveredHealth`, AddValue +0.25 |
| 44 | **Bleedout Resist** | +20% chance to enter bleedout instead of dying | C | Perk `ModBleedoutChance`, AddValue +0.2 |
| 45 | **Quick Recovery** | +25% bleedout recovery chance | C | Perk `ModBleedoutRecoverChance`, AddValue +0.25 |

### Weapon Synergy (armor boosting weapon performance)

| # | Name | Effect | Tier | Implementation |
|---|------|--------|------|----------------|
| 46 | **Targeting Matrix** | -15% weapon cone of fire | C | Perk `ModConeoffireMult`, MultAndAdd -0.15 |
| 47 | **Stabilizer Weave** | +20% scope stability | C | Perk `ModActorScopeStability`, AddValue +0.2 |
| 48 | **Quick Reload** | +15% reload speed | C | Perk `ModWeaponReloadSpeed`, AddValue +0.15 |
| 49 | **Extended Magazine** | +20% magazine size | C | Perk `ModMagazineSize`, MultAndAdd +0.2 |
| 50 | **ADS Focus** | Cast buff spell on aim down sights | C | Perk `ApplyAimDownSightSpell` → accuracy/stability buff |

### Social & Economy

| # | Name | Effect | Tier | Implementation |
|---|------|--------|------|----------------|
| 51 | **Silver Tongue** | +10% persuasion success | A | ActorValue → `PlayerPersuasionSkillSuccessChanceMult` (`0x002D873B`), Add +0.1 |
| 52 | **Bargain Hunter** | -10% buy prices | C | Perk `ModBuyPrices`, MultAndAdd -0.1 |
| 53 | **Merchant's Mark** | +10% sell prices | C | Perk `ModSellPrices`, MultAndAdd +0.1 |
| 54 | **Bounty Reducer** | -25% bounty accrued | C | Perk `ModPlayer_Bounty`, MultAndAdd -0.25 |
| 55 | **Mission Pay** | +10% mission rewards | A | ActorValue → `MissionRewardMultiplier` (`0x0006BAD3`), Add +0.1 |
| 56 | **XP Boost** | +10% kill experience | C | Perk `ModKillExperience`, AddValue +0.1 |

### Crafting & Outpost

| # | Name | Effect | Tier | Implementation |
|---|------|--------|------|----------------|
| 57 | **Resource Efficiency** | -15% crafting component usage (all types) | A | 4x ActorValue → ComponentUsageMult (Chemical/Metallic/Organic/Technical) |
| 58 | **Lucky Craft** | +10% chance to duplicate crafted item | C | Perk `ModCraftingDupeChance`, AddValue +0.1 |
| 59 | **Scrap Master** | +25% scrap rewards | C | Perk `ModScrapRewardMult`, AddValue +0.25 |
| 60 | **Research Discount** | -15% research cost | C | Perk `ModResearchCost`, MultAndAdd -0.15 |
| 61 | **Scanner Boost** | +25% scanner radius | A | ActorValue → `OutpostScannerRadius` (`0x002CBE74`), MultAndAdd +0.25 |
| 62 | **Build Area** | +20% outpost build area | A | ActorValue → `OutpostBuildAreaRadius` (`0x00063FC5`), MultAndAdd +0.2 |

---

## Summary by Feasibility

| Tier | Count | Notes |
|------|-------|-------|
| **A — OMOD ActorValue** | 35 | Flat stat boosts. Simplest — just needs AVIF FormID + float. Reuses weapon OMOD generator pattern. |
| **C — Perk Entry Point** | 27 | Conditional/triggered effects. Most powerful but requires Perk + conditions per stat. |

Note: Many stats previously classified as Tier B (Enchantment chain) were reclassified. Regenerator moved to Tier A since `HealRateMult` is a direct AV. Muffled Steps moved to Tier A via `MovementNoiseMult`. True Tier B (needing full spell chains) is rare — most effects map to either a direct AV or a perk entry point.

**Recommended phases:**
1. Start with the 35 Tier A stats — trivial OMOD generation
2. Add Tier C stats that use simple perk entry points (no spell triggers) — `ModFallingDamage`, `ModBuyPrices`, etc.
3. Add Tier C stats with spell triggers (`ApplyCombatHitSpellSelf`, `ApplyAimDownSightSpell`) last

---

## Implementation Prerequisites

1. **Verify AVIF FormIDs** — Several AVs above use enum indices rather than confirmed FormIDs. Look up via xEdit or a Mutagen enumeration script.
2. **Create armor OMOD generator** — parallel to weapon upgrade generator, using `Armor.Property` enum and YAML stat definitions
3. **Build custom LGDI** — replace vanilla `DefaultLegendaryArmor` with a custom legendary item definition that includes our new OMODs
4. **Test in-game** — verify stats show on item card, apply correctly, and don't conflict with vanilla legendary system
