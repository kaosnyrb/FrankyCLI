# Overtime Bounty Board — `duo_*` Quest Family

Reverse-engineered reference for the **Overtime** (`du_overtime.esm` / `.esp`) bounty board
mission template. This is a **distinct family** from the Crimson Fleet `duout_*` bounty
quests documented in [`quest_from_scratch.md`](quest_from_scratch.md):

| Family | Plugin | Script | Aliases | Bounty target |
|---|---|---|---|---|
| `duout_*` (Crimson Fleet) | `Starfield.esm` | `duout_ground_bounty_quest` | 5 (planet) / 10 (space) | Generic via PCM / leveled spawn |
| `duo_*` (Overtime) | `du_overtime.esm` | `duo_bountyscript_named` | 8 | **Named, pre-built NPC** with name list + artifact reward |

The Overtime family carries **richer state** (named target, name list, artifact drop,
mission parent, reward global) and is the closer analogue to a "hunt" mission than the
Crimson Fleet bounty.

---

## Worked example — `duo_MB31a_qst` ("Dismantle: Kill the Robot")

Inspected via `gen_inspect quest_vmad duo_MB31a_qst`.

### Top-level

| Field | Value |
|---|---|
| FormKey | `000ACB:du_overtime.esm` |
| Name | `Dismantle: Kill the Robot on <Alias=TargetPlanetLocation>` |
| Priority | 15 |
| Type | None |
| Flags | `0x00080000` |

### Stages

| Stage | Flags | Role |
|---|---|---|
| 5 | `0x40` | Init / start gate |
| 10 | `KeepInstanceDataFromHereOn` | Active — target placed, hunt begins |
| 100 | `0` | Target dead — set by `DefaultAliasOnDeath` on PrimaryRef |
| 200 | `0` | Turn-in / completion |

### Objectives

| Index | Text |
|---|---|
| 10 | `Collect the bounty at the <Alias=TargetLocation>` |

Note: only **one** objective. The "kill the robot" part is conveyed by the quest
**name**; the objective fires after the kill and points the player at the
hand-in location. This is the bounty-board convention — the kill itself is the
implicit task.

### Aliases (8)

Quest.Aliases IDs are assigned by CK and **must be preserved when cloning**.

| ID | Name | Type | Flags | Fill |
|---|---|---|---|---|
| 0 | `PlayerStarSystemLocation` | LocAlias | `0x20000008` | Implicit (player's current system) |
| 2 | `TargetPlanetLocation` | LocAlias | `0x40000100` | Specific planet |
| 3 | `PrimaryRef` | RefAlias | `0x00000100` | Spawned `targetNPC` instance — VMA[0] `DefaultAliasOnDeath`→SetStage 100 |
| 5 | `TargetLocation` | LocAlias | `0x00800100` | Conditions: `LocationHasKeyword` + `LocationHasRefType` — the hand-in spot |
| 8 | `TargetSystemLocation` | LocAlias | `0x20000100` | Star system containing the bounty |
| 9 | `PlayerShip` | RefAlias | `0x00100002` | Player's ship |
| 11 | `MapMarker` | RefAlias | `0x00000080` | VMA[1] `DefaultAliasMapMarkerScript` |
| 13 | `EnemyType` | RefAlias | `0x00000102` | Categorical (faction/template) |

Compare to the planet quest family in `quest_from_scratch.md` (IDs 0/1/2/3/5):
**different ID scheme entirely**.

### VMA.Aliases (2)

Same rule as the Crimson Fleet family — only aliases with their own scripts get a
VMA entry:

```
[0] (on PrimaryRef): DefaultAliasOnDeath  StageToSet=100
[1] (on MapMarker):  DefaultAliasMapMarkerScript  MapMarkerCategory=0  UndiscoveredVisibility=0
```

### Script: `duo_bountyscript_named` — 17 properties

| Property | Type | Value / role |
|---|---|---|
| `targetNPC` | Npc | `000A6D` → `duo_boss_LvlRobotModelA_Ecliptic` (the bounty target — see below) |
| `NameList` | FormList | `000A7F` → `duo_namelist_robot` (name pool) |
| `artifactlist` | FormList | `0009CB` → `duo_ArtifactList_Local` (drop pool) |
| `GangMembers` | FormList | `000A0F` (Ecliptic mercs that spawn with the boss) |
| `MinGangMembers` / `MaxGangMembers` | Int | 8 / 12 |
| `RewardAmountGlobal` | Global | `000831` → `duo_reward_creds_easy` = **1250 credits** |
| `RewardAmountGlobalActual` | Global | same — runtime actual |
| `MissionParent` | Quest | `015300:Starfield.esm` (vanilla mission parent) |
| `ShutdownOnFailure` | Bool | True |
| `PrimaryRef` | RefAlias | self-quest link (resolves alias ID=3 at runtime) |
| `EnemyType` | RefAlias | self-quest link (alias ID=13) |
| `PlayerShip` | RefAlias | self-quest link (alias ID=9) |
| `TargetLocation` | LocAlias | self-quest link (alias ID=5) |
| `TargetPlanetLocation` | LocAlias | self-quest link (alias ID=2) |
| `TargetSystemLocation` | LocAlias | self-quest link (alias ID=8) |
| `PlayerStarSystemLocation` | LocAlias | self-quest link (alias ID=0) |

**Key insight**: alias-typed properties (`PrimaryRef`, `EnemyType`, location aliases)
all carry the **quest's own FormKey** as `Object`. Papyrus resolves the alias by
**property name match** — same pattern as `duout_*`, confirmed in
[`quest_from_scratch.md`](quest_from_scratch.md#alias-property-object--key-insight).

### Fragment script

| Field | Value |
|---|---|
| `vma.Script.Name` | `duo_bounty_fragments` |
| Fragment | Stage=10  `Fragment_Stage_0010_Item_00` |
| Fragment | Stage=100 `Fragment_Stage_0100_Item_00` |
| Fragment | Stage=100 `Fragment_Stage_0100_Item_01` |

Unlike the Crimson Fleet family — which uses a vestigial Treasure-Map fragment script —
the Overtime family has its **own dedicated fragment script** with three actual
fragments. The stage-10 fragment likely does target spawning / name selection;
stage-100 (twice) likely handles death rewards + artifact drop.

### VMA version values

Same as `duout_*` family (confirmed):

| Field | Value |
|---|---|
| `VMA.Version` | 6 |
| `VMA.ObjectFormat` | 2 |
| `VMA.ExtraBindDataVersion` | 3 |

---

## The bounty target — `duo_boss_LvlRobotModelA_Ecliptic`

| Field | Value |
|---|---|
| FormKey | `000A6D:du_overtime.esm` |
| EditorID | `duo_boss_LvlRobotModelA_Ecliptic` |
| `DefaultTemplate` | `2EC54F:Starfield.esm` (vanilla leveled robot) |
| `Class` | `010B2F:Starfield.esm` |
| `CombatStyle` | `34CC6F:Starfield.esm` |
| `CombatOverridePackageList` | `0102ED:Starfield.esm` |
| `DefaultPackageList` | `26F689:Starfield.esm` |
| `DeathItem` | `1312B5:Starfield.esm` |
| Aggression | Frenzied |
| Confidence | Foolhardy |
| Assistance | HelpsFriendsAndAllies |
| `CalculatedHealth` | 270 |
| `DispositionBase` | 35 |
| Flags | 280 |

**Pattern**: Overtime defines its own boss-flavoured NPC that **templates onto a vanilla
leveled robot** (`DefaultTemplate=2EC54F`). The Overtime NPC supplies the boss tweaks
(Frenzied + Foolhardy + boss combat style); the vanilla template supplies the model
chain. This is the same dual-template pattern our [`PredatorHuntTarget`](../../Retrograde.Library/Nouns/Hunt/PredatorHuntTarget.cs)
uses (PCM clone for OMOD recipe + concrete `_Enc*_Template` for the render chain).

---

## Cross-reference to "hunt" design

The Overtime bounty is structurally the **closest vanilla cousin** to our hunt mission:

| Concern | Overtime `duo_*` | Our hunt (planned) |
|---|---|---|
| Bounty target | One named NPC, pre-built | Procedurally renamed predator NPC |
| Target spawn | Stage-10 fragment via `PrimaryRef` alias | TBD — same alias pattern is the natural fit |
| Name flavour | `NameList` FormList rolled per quest | `PredatorHuntTarget.GetHuntName()` prefix+suffix |
| Reward | `RewardAmountGlobal` (Global, easy=1250) | TBD — Global mirror is the natural fit |
| Death detection | `DefaultAliasOnDeath` on PrimaryRef → SetStage 100 | Same pattern transfers directly |
| Bounty hand-in | `TargetLocation` LocAlias with `LocationHasKeyword`+`LocationHasRefType` conditions | TBD — bounty board terminal lookup |
| Map marker | `MapMarker` RefAlias + `DefaultAliasMapMarkerScript` | Same pattern transfers directly |
| Mission parent | `015300:Starfield.esm` | Likely same vanilla parent |

The **8-alias scheme** (vs Crimson Fleet's 5) is what to copy if we want a robust hunt:
the extra `PlayerStarSystemLocation` / `TargetSystemLocation` / `EnemyType` slots are
what let the bounty board describe "robot on <planet> in <system>" filling without a
PCM keyword filter.

---

## Outstanding questions

1. **Fragment script contents** — what do `Fragment_Stage_0010_Item_00` and the two
   stage-100 fragments actually do? Need to dump the `.pex` or read the source `.psc`.
2. **`EnemyType` (alias ID=13)** — flags `0x00000102` suggest CreateRefTemp + extra
   flag. Likely a faction-template ref used by the gang spawn logic to pick which
   faction's mercs accompany the boss. Needs confirmation.
3. **`TargetLocation` conditions** — `LocationHasKeyword` + `LocationHasRefType` —
   which keyword and which ref-type? These pin the bounty hand-in spot.
4. **`NameList` (`duo_namelist_robot`) contents** — what are the candidate names?
   Items list not enumerated by the inspector default. Re-run with FormList expansion.
5. **`duo_*` variants** — is `MB31a` one of many (e.g. `MB31b`, `MB32a`)? Worth
   enumerating all `duo_MB*_qst` to see the variant axes (enemy type? difficulty?).
6. **Sibling quest types** — are there `duo_*` activator / station / derelict variants
   matching the Crimson Fleet `duout_*` set, or is Overtime kill-only?

These are good follow-ups before designing a from-scratch Overtime-style hunt quest.
