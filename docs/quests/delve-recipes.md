# Delve recipes: the reference

**A Delve is a JSON file. `gen_delve` lints it, builds it into `du_overtime.esm`, and verifies the
result off disk.** Content (who, what, which words) lives in the recipe; mechanism (which base quest,
which driver script, which stages) lives in the template. A hundred Delves are a hundred recipes and no
code.

This page is the format. The rules each field encodes were learned at his glass; where a rule has a
story, the story is in the office's
[`delves/README.md`](https://github.com/kaosnyrb/home-office/blob/main/office/projects/delves/README.md)
and the Bethesda manual, and this page links rather than repeats.

*Written 2026-10-08 off the code (`commands/quest/gen_delve.cs`, `data/delves/templates.json`,
`papyrus/duo_delve_*.psc`). If this page and the code disagree, the code is right and this page is the
bug.*

---

## The loop

```
data/delves/recipes/<id>.json          write or copy a recipe
dotnet run -- gen_delve lint --all     grade every recipe (about a minute, one line each)
dotnet run -- gen_delve lint <id>      one recipe's full report
dotnet run -- gen_delve build <id> --dry   lint, then build in memory, write nothing
dotnet run -- gen_delve build <id>     write it into the live du_overtime.esm, then verify off disk
```

**A clean lint means the build will take it.** The lint writes nothing and is safe to run any time.

**Before a real build, on the machine with the plugin (Defiant):**
1. Starfield and the Creation Kit are closed (the build cannot write a plugin the game holds, and
   nothing is rebuilt under his hands).
2. Snapshot: `python sync_plugin.py --snapshot --label pre-<id>` in `C:\modding\DU_Overtime`.
3. Build. Read the verification block: every line `[OK  ]`, and `the plugin's masters are unchanged`.
4. If the template's driver changed, compile it: `python papyrus/compile.py C:/modding/DU_Overtime/Data <driver>`,
   then copy the `.pex` into Steam `Data/scripts`.
5. `python sync_plugin.py`, commit the Overtime repo, snapshot again with a label.
**Testing a Delve from a real slate: `gen_delve testnode on|off <testNode> <realNode> [--keep q,q] [--dry]`**
(2026-10-12, his workflow: *"I'd put the ammo condition onto the real node then take it off the test one. Then I'd
have only the test missions on that node"*). Each slate branch has a PARKED test node, conditioned on the event's
treasure carrying `ObjectTypeAmmo`, which no slate's does (all four carry `LL_LegendaryItem_WeaponList`). Deal the
Delve there (`"deal": "_duo_MS_ArtifactMapLocalCargo_TEST"`), build, then `testnode on ... --keep <id>`: the condition
moves to the real node (parked) and the test node deals only what you kept, so every slate of that kind starts the
Delve. `off` moves it back. It refuses unless exactly one of the pair carries the condition, so the pair can never
end with both live or both parked. **The migration is `testnode off <test> <real> --promote`**: the test node's quests
move into the real pool (skipping any already there), the test node is emptied, and the real pool goes live. Then
edit each recipe's `deal` to the real node, JSON only, so the next rebuild agrees with the plugin. ⛔ **Never rebuild a
Delve to move it between pools** (his ruling, 2026-10-12: *"think it's a bit wasteful to rebuilt all the quests just to
put them in a different list. Everytime you build the formid's increment"*): a pool is a node's list; a rebuild
re-mints every record the build owns.

**The pairs, measured 2026-10-12 (`gen_inspect smtree`; his: *"I believe theres other test nodes too"*).** One parked
test node per slate branch, each beside its real pool:

| branch | test node (parked by `ObjectTypeAmmo`) | real node(s) |
|---|---|---|
| `duo_SQ_ArtifactLocalCargoBranch` | `_duo_MS_ArtifactMapLocalCargo_TEST` | `duo_MS_ArtifactMap_LocalCargoNode` |
| `duo_SQ_ArtifactLocalEventBranch` | `_duo_MS_ArtifactMapLocal_TEST` | `duo_MS_ArtifactMap_LocalNode` **and** `_Local_LowChance_Node` (gated by `GetRandomPercent`) |
| `duo_SQ_ArtifactRemotePlanetEventBranch` | `_duo_MS_ArtifactMapRemotePlanet_TEST` | `duo_MS_ArtifactMap_RemotePlanetNode` |
| `duo_SQ_ArtifactRemoteSpaceEventBranch` | `_duo_MS_ArtifactMapRemoteSpace_TEST` | `duo_MS_ArtifactMap_RemoteSpaceNode` |

⚠ **The local branch has TWO real nodes**, and `testnode` parks one: testing there leaves the low-chance pool live
unless it is parked too (a second call cannot do it, since the test node has given its condition away). ⚠ **The
board's `_duo_MB_MissionNode_test`** (under vanilla's `MissionBranch`) is a different shape: no conditions at all,
`SharesEvent`, `NumQuestsToRun` 1. `testnode` does not apply to it. ⚠ The `.esp` twin carries its own copy of every
node; `testnode` edits the `.esm` only.


**Taking a build back out: `dotnet run -- gen_delve unbuild <builtId> [--dry]`** (2026-10-12). Removes every
record whose EditorID is the id or starts with the id plus `_` (the build's own naming; nested topics and scenes
go with the quest), and the id's Story Manager pool entries, behind `removerecord`'s inbound guard; re-reads the
plugin off disk after. It refuses an id that is not a quest in the plugin, so a stray prefix matches nothing.
Snapshot first (step 2). It LISTS the loose files a build deployed (`duo_qf_<id>.pex`, the wems in
`voicecache/deployed_<id>.txt`) and does not delete them. Written for the 10-11 lettering: the old unlettered
builds (`duo_delve03` to `08`) are still in the plugin on `Defiant`, where the name-clash lint refuses their
lettered recipes until they come out. `duo_delve09` was the first, the same day.

6. In game: `startquest <id>`. A rebuild re-mints FormIDs, so start it fresh. **No reload is needed
   between runs:** the Delve base is repeatable, so `startquest <id>` again resets its stages and
   re-draws its aliases (his fact, 2026-10-08). `setstage <id> <stage>` jumps straight to a beat once a
   Delve is built on its stage graph.

---

## The templates

`dotnet run -- gen_delve templates` lists them. Each is one row in `data/delves/templates.json`.

| template | kind | beats | the shape |
|---|---|---|---|
| `two-beat-one-place` | dualactivator | 2 to 8 | pick a thing up, carry it, hand it over; extra beats in between are journal-only |
| `find-owner-or-buyer` | choice | exactly 3 | find a thing with a name on it, then return it to its owner OR sell it to a buyer, at two different places; walking to one ends it (Type 2; `duo_delve06a`, `06b`) |
| `beats` | beats | 1 to 9 | **no driver: the stages are the state machine.** Each beat has a `type`; any order behind a counter with `group`; a thief to `recover` from; a `returnTo` an earlier object; an `approach`; endings to `choose` between; waves to `hold` off (`duo_delve03a`-`09a`). See *Beats* below |
| `board` | board | 1 to 9, fights only | **a mission-board mission on the beats machinery** (his 2026-10-14). The base is `duo_MB28a_qst`; its driver comes off and vanilla's `MissionQuestScript` goes on, so the BOARD starts it, takes it and pays it. `defeat`, `clear` and `hold` only. See *Board* below |

*Retired 2026-10-09: `carry-absence-recover-return` (kind `delve4`, `duo_delve_driver.psc`). 03 to 05 moved
onto `beats` and played the same in his hands the same morning; the old shape is now `pickup` / `use` /
`recover` / `deliver` with `returnTo: 2` and, on 03, an `approach`.*

**Tokens** an author writes in prose, mapped by the template onto its real aliases:

| template | tokens |
|---|---|
| two-beat-one-place | `<Place>`, `<Planet>` |
| find-owner-or-buyer | `<FindPlace>`, `<OwnerPlace>`, `<BuyerPlace>`, `<Planet>` |
| beats | `<Place>` (main), `<SecondPlace>`, `<ThirdPlace>`, `<Planet>` |
| board | `<Place>`, `<Planet>`, `<System>` |

An unknown token is refused (it would print to the player literally). Names (items, delivery points,
NPCs) take **no** tokens.

---

## The fields

**Every key must be one of these.** An unknown key is refused with a did-you-mean. A key starting with
`_` is a note and is always allowed (`"_theme": "why I picked this"`). A field a template never reads is
refused rather than silently ignored.

### Top level

| field | type | required | read by | what it is |
|---|---|---|---|---|
| `schema` | int | yes | all | always `1` |
| `id` | string | yes | all | the quest's EditorID and every clone's prefix. **Must equal the file name** (`duo_delve07a.json` has id `duo_delve07a`) and look like `duo_delve…` |
| `template` | string | yes | all | a template id from the table above |
| `author`, `source` | string | no | none | provenance; not written into the game |
| `place` | object | yes | all | where the mission happens (below) |
| `prose` | object | yes | all | `name` (the quest title) and `briefing` (the journal's opening line); on a `board` mission also **`card`, required there and refused elsewhere**: the board's description, what he reads before taking the job |
| `items` | object | choice | choice | what is carried and what the objects look like (below) |
| `beats` | list | yes | all | one entry per beat (below) |
| `offer` | object | choice: yes | choice, beats ending on a `choose` | the surprise between the find and the endings: `journal` lands on the stage that opens the choice, `message` shows after the find's box |
| `reward` | object | choice: yes | choice | the pay tier per ending |
| `people` | object | no | choice | named NPCs at each ending, with outfits and company |
| `recap` | string | beats ending on a `group`: yes; otherwise refused | beats | stage 100's journal line, the last thing the player reads (see *Beats*) |
| `approach` | object | no | beats | what happens the first time the player comes near one beat's object (see *Beats*) |
| `deal` | string | no | all | **which slate pool hands this Delve out**: the EditorID of a Story Manager quest node in the template's own mod. The build appends the quest to it and removes the previous build's entry from every node. **Absent = in no pool**: started by hand with `startquest`. ⚠ It is live content: a dealt Delve reaches players from slates on the next release, once per `DoAllBeforeRepeating` rotation. **The beats template's base is a local-CARGO slate quest, so a beats Delve's pool is `duo_MS_ArtifactMap_LocalCargoNode`**; the lint warns when the named pool never dealt the template's base quest (his go, 2026-10-11) |

### `prose.name`: `Type: Thing on <Planet>`

His, 2026-10-11: *"I'm thinking of going back to the [Quest type]: [quest desc] format. I found the
missionboard hard to read with just the narrative quest names."* So a Delve's name is **one fixed type
word per Delve type**, a colon, then the specific thing. The type word tells the player what they will be
doing; it is fixed so the board can be scanned. Overtime's own words are reused where one fits, so old and
new missions read as one board. A new Delve type gets its word on the day it is written.

| Delve type | type word | example |
|---|---|---|
| return a lost thing (03, 04, 05) | `Recovery` | Recovery: Relay Dish on `<Planet>` |
| keep or give back (06) | `Salvage` | Salvage: Service Medal on `<Planet>` |
| fix up (07) | `Repairs` | Repairs: Water Reclaimers on `<Planet>` |
| the trail (08) | `Search` | Search: Soldier's Letters on `<Planet>` |
| hold the point (09) | `Defense` | Defense: Relay Alignment on `<Planet>` (US spelling, as the game's UI) |
| talk (type 7, 14a) | `Inquiry` | Inquiry: Survey Crate on `<Planet>` |
| the dungeon return (11a) | `Recovery` | Recovery: Stolen Belongings on `<Planet>` |
| here, then another planet (type 8, not yet built; now 10a) | `Supply Run` | Supply Run: Dig Site on `<SecondPlanet>` |
| survey (01) | `Survey` | Survey: Seismic Sensors on `<Planet>` |
| retrieve under fire (02) | `Extraction` | Extraction: Core Samples on `<Planet>` |

### `place`

```json
"place": {
  "theme": { "require": ["LocTypeOE_NonHostile"], "exclude": [] },
  "second": { "theme": { "require": [], "exclude": [] } },
  "third":  { "theme": { "require": ["LocTypeOE_ThemeInfrastructureKeyword"], "exclude": [] } }
}
```

| field | what it is |
|---|---|
| `theme.require` / `theme.exclude` | location keywords the MAIN place must / must not carry. The place must also carry every marker its beats sit on, plus a map marker; the build derives those, you never write them |
| `second` | the second POI's own theme (a beat with `"place": "second"`). **On a beats Delve with no beat at `second`, the build REMOVES the base's second place alias** (FinalLocation), which otherwise draws a second POI the quest never visits and only lets it start where two qualify (his catch on `duo_delve09`). It refuses if anything the player reads still names it |
| `third` | choice only: the third POI's own theme (a beat with `"place": "third"`) |
| `remote` | optional, main place only, **written**: `true` = the place is REQUESTED on another planet rather than found near the player (his 2026-10-14: *"I got sick of travelling"*). The build copies the template's `remotePlaceFrom` quest's place alias over the base's (beats: `duo_artifact_remote_planet_qst01a`'s `DungeonLocation`, a `PCM_QuestGeneral` request with its planet condition and its `GetDistanceGalacticParsec < duo_distanceformission` limit); markers and theme are then written as for any place, and the local leash to the player goes. The lint measures the pool against what a request can place (`gen_delve pcmpool PCM_LocationRequest_General`, 214 readable places on Defiant), not the POI census. **Every beat is at that one place:** a second or third place, `reach`, or `remote` on `second`/`third` is refused. Deal it from `duo_MS_ArtifactMap_RemotePlanetNode` (the lint warns on a pool that never dealt the remote base) |
| `reach` | optional, **written**: the Global every second and third place is held within, of the main place's map marker and of each other. Absent = `duo_longdrivedistance` (7,500). `duo_shortdrivedistance` (4,000) when the places must be close (08a, his 2026-10-13: the long drive spread a trail over *"most of the worldspace"*). Tighter = more slates find no fit and fall through to remote. Refused if it is not a Global, or the recipe has no second or third place |
| `leash` | **advisory, not written.** ⭐ **What IS written (2026-10-13, his "make all points within 7500"): every second and third place is held within `duo_longdrivedistance` (7,500) of the main place's map marker, and a third place also of a marker in the second, so all a Delve's POIs are within one drive of each other.** The main place keeps the base's own limit, `duo_samemapdistance` (20,000) from the player. The base's leashes take an ALIAS number (`UseAliases`), and before this a beat at the second place moved that anchor into the second place itself, so the leash measured from its own marker (six Delves) or from a removed alias (11a). `gen_aliaslint` R5 now names that shape |
| `who` (on `place`, `second` or `third`) | **who is at that place**, placed round one object there the first time the player comes within 250 m of it. `{ "kind", "list", "min", "max", "beat" }`. Absent = nobody (non-hostile POIs mostly spawn no one). Kinds: **`infested`**, ONE species from a creature list (`duo_GangMembersList_PreditorMegalist_A`..`G`), set to attack, within 20 m · **`workers`**, a friendly MIX from a `Civ_*` list (`duo_GangMembersList_Civ_Construx`, `_LIST`, `_Medics`, `_ArcMight`, `_DeimosMiner`, `_ArgosMiner`, `_UnitedTransport`, `_StrongArm`), within 12 m · **`spacers`**, a hostile MIX from a gang list (`duo_GangMembersList_Spacer`, `_Crimson`, `_Ecliptic`, `_Varuun`, `_Syndicate`...), within 20 m. `min` at least 1. `beat` (1-based, optional) names which pickup / use / deliver at that place they gather round; absent = the first one. ⛔ Refused: any other kind (`the dead` is designed, not built), a `beat` that is not an object at that place, and an object whose ending person already hooks its distance. **The words must agree:** a briefing that names a crew needs one here. The pay tier is still the recipe's (`reward`), never moved by this field |
| `civilians` | **retired with delve4, and refused.** On a beats Delve write `approach.civilians` |

**Where a place can be** (2026-10-13): the lint measures each place's pool across the POI pool PLUS every location carrying `LocDungeonBossLocRef`, because the marker conditions do the selecting and POIs carry the boss marker too (his, correcting bethesda part 32: "loads of pois have mapmarkers and boss markers"). A beat on `LocDungeonBossLocRef` (`duo_delve11a`) draws from both; vanilla's `MB_Bounty01Far` asks a place for that one marker and nothing else. ⚠ The boss marker is indoors only about 29% of the time.

**Useful keywords**, from the census (`gen_delve keywords [filter…]` tallies themes inside a narrowed
pool): `LocTypeOE_NonHostile` (nobody hostile, 62 POIs with a centre and map marker; ⚠ NOT people: his, 2026-10-11, *"LocTypeOE_NonHostile mostly dont spawn NPCs, only if there is a quest present at it"*, so a Delve that needs people places them) ·
`LocTypeOE_ThemeInfrastructureKeyword` (36 of those 62) · `LocTypeOE_ThemeNaturalKeyword` ·
`LocTypeOE_ThemeCaveKeyword`.

### `beats[]`

| field | what it is |
|---|---|
| `at` | the marker the beat sits on, a `LocationReferenceType`. The workhorses: `RECenterLocRef`, `RETravelA1LocRef`, `RETravelB1LocRef`, `REMarkerMediumLocRef`, `REContainerLocRef`. ⚠ Two beats on the same travel ring (A1/A2/A3 or B1/B2/B3) land 7 to 14 m apart |
| `place` | `"main"` (default), `"second"`, or `"third"` (choice) |
| `objective` | the on-screen objective. Imperative, short (vanilla median 4 words, never past 12), no end punctuation |
| `journal` | the journal line this beat sets. ⚠ **The log shows only the newest stage's line:** on a choice, beat 1's line is overwritten by the offer in the same instant, so put what the player must read in `offer.journal` |
| `message` | optional pausing box: `{ "title": "...", "text": "..." }`, from the player's point of view. Plain ASCII |

**Per template:**
- **two-beat-one-place:** beats 1 and last are the driver's (objective required); beats in between are
  created, carry a `journal` and **no** `objective`.
- **find-owner-or-buyer:** exactly 3, in this order: the find at `second`, the owner at `main`, the
  buyer at `third`.

### `items`

| field | template | what it is |
|---|---|---|
| `load` | choice | the carried thing's inventory name (a clone; never the base's item) |
| `missing` | refused | (was delve4's other half; on beats it is a `recover` beat's `item`) |
| `crateModel` | choice | the NIF the found thing wears, `Meshes\…` |
| `centreModel`, `centreName` | choice | the OWNER's delivery point's NIF and its activate prompt |
| `buyerModel`, `buyerName` | choice only, required | the buyer's delivery point |

**A model must sit on the ground.** The lint grades each against the vanilla Static that owns the mesh
and warns if it sinks, hangs or floats. A brand new kitbash has no Static to read, so check it by eye.
Shop furniture reads wrong standing alone in a POI (his: *"Prob be a chest"*). **Names must be new:** an
item or NPC name already in the game, or used by another recipe, is refused, so **copying a recipe means
renaming its things.**

### `offer` (choice)

```json
"offer": { "journal": "…the line the player reads after the find…",
           "message": { "title": "Incoming Message", "text": "…" } }
```

### `reward` (choice)

```json
"reward": { "owner": "easy", "buyer": "med" }
```

A **tier**, not a number: Overtime's ladder, written onto each ending's completing stage. `easy` 1,250
credits / 250 XP · `med` 2,800 / 500 · `hard` 7,500 / 1,500. The design has the buyer paying more; the
lint warns if not. The driver pays nothing itself.

### `people` (choice)

```json
"people": {
  "owner": { "name": "Maren Vale", "template": "UC_NA_MASTWorkerFemale01",
             "outfit": "Outfit_Clothes_Colonist_QuarterPaddedVest_01_NoHat",
             "company": { "list": "duo_GangMembersList_Civ_LIST", "min": 2, "max": 4 } },
  "buyer": { … }
}
```

| field | what it is |
|---|---|
| `name` | the NPC's name, no tokens. **A message box that describes a person needs this person.** |
| `template` | a vanilla NPC to clone, by EditorID. The friendly, talkable set: `UC_NA_MASTWorkerFemale01`…`05`, `UC_NA_MASTWorkerMale01`…`05` |
| `outfit` | optional, an Outfit EditorID (596 in Starfield.esm, e.g. `Outfit_Clothes_Formal_ShirtSlacks_Vest`, `Outfit_Clothes_Colonist_…`, `Outfit_Scientist_…`). Absent = the template's own |
| `company` | optional: nameless friendly NPCs placed within 8 m of the delivery point. `list` is a FormList of actors (Overtime's friendly ones are `duo_GangMembersList_Civ_*`: `Civ_LIST` colonists, `Civ_Medics`, `Civ_Construx`, `Civ_ArcMight`, `Civ_DeimosMiner`, `Civ_ArgosMiner`, `Civ_UnitedTransport`, `Civ_StrongArm`); `min`/`max` the count (more than 8 warns) |

The person and their company appear the first time the player comes within 250 m of the point.

---

## The choice's endings

The two delivery places **must never be able to draw the same POI.** The lint measures the two pools
and refuses any overlap; the shipped way to separate them is the theme: one requires a keyword the other
excludes. ⚠ **Asking for people at both ends is expensive:** the non-hostile pool is 62 POIs galaxy-wide, so
`duo_delve06a` starts only on maps that have one of each in range (his play: *"Quest starts, sometimes.
Map dependant"*).

---

## Letter variants

**Why they exist: a quest runs ONE instance at a time** (his, 2026-10-08), so the same mission cannot
sit in the player's log twice. Each letter is its own quest record, which is what lets `07a` and `07b`
run in parallel. A variant is not only new lore; it is another copy that can be live at once.

**Every recipe has a letter**, even when it has no sibling yet (`duo_delve03a`), and **new numbers fill 01 and 02 first** (his, 2026-10-11: *"its bugging me that our delves dont start at 1"*; the old 01 and 02 test quests are out of the plugin).

**Full recipes** (his ruling, 2026-10-08). `duo_delve07a`, `duo_delve07b`: same structure, new names
and words. `lint --all` compares siblings that share a number and names any drift in template, beats
(each beat's place, type, group, choose, `returnTo`, `replace`, a hold's numbers, and WHETHER it
carries a person, box, line or item, never their words), themes or reward tier (top-level on the choice
kind, per ending on beats), so a fix made in `a` and forgotten in `b` shows up. Until 2026-10-11 the
beats and reward legs read only the choice kind's fields and compared nothing on a beats recipe
(Jessica's find). Siblings **may** share a
delivery model, and so may different missions (his, 2026-10-11: *"two missions sharing a model isnt really an issue"*). A variant that needs different structure is a
new number.

---

## What the lint checks

**Refused:** not JSON · an unknown key · an id that is not its file name · a template that does not
exist · a field the template never reads · the wrong beat count or shape for the template · a missing
objective on a driven beat, or an objective on an extra beat · a marker, keyword, outfit, company list,
NPC template, model or reward tier that does not exist where `du_overtime` can reach it (the mod and its
masters, never the CK bridge `du_overtime.esp`) · a place whose pool is zero · a choice whose two endings
can be the same POI · a token in a name · an item or NPC name already in the game, or used by two
recipes (the choice kind's `items`/`people` and each beat's `item`/`person.name`) · an empty message box · a `deal` that is not a Story Manager quest node in the template's own mod (the refusal lists the mod's nodes).

**Warned:** a first objective that names no place (his, 2026-10-11: *"the first objective name should
have a POI name in it. It helps to know what POI your looking for"*) · an object beat (a use, pickup or deliver that is not a `returnTo`) with no `model` or no
`name`, because absent means the base's boss crate and generic prompt (his, 2026-10-11: *"we dont really
want defaults"*) · a pool under 40 · a model that sinks, hangs or floats, or that no Static owns · a journal
line the log will never show · an objective over 12 words or ending in punctuation · any em dash or
non-ASCII character · two beats on one marker or one travel ring · a base exclusion the recipe drops ·
letter siblings that differ in structure · a `deal` pool that never dealt the template's base quest · the buyer not
paying more than the owner.

**The build adds, off disk:** every alias fills below what it fills from (the order the engine needs),
every place demands exactly its markers, the plugin's masters are unchanged, and every driver property,
objective target, journal, stage reward and NPC field reads back as written, the quest sits in exactly the node its `deal` names (or none), and no node points at a quest the plugin no longer has.

---

## Beats: a Delve with no driver

**His ruling, 2026-10-08: "The stages of the quest are the state machine. All of vanilla Starfield was
built this way."** A `beats` recipe lists its beats by **type**. Each type is a stock `Default*` alias
script that sets the beat's stage once the previous step's stage is done, plus a few lines in ONE
fragment script the build generates (`papyrus/gen/duo_qf_<id>.psc`, gitignored, never hand-edited) for
the half that is Papyrus-only: objectives, the counter, taking delivered items, a message box.

**One quest script, and only for a TURNED-DOWN mission** (2026-10-14, his catch: a rejected Delve could
not be restarted). The removed driver was the only `OnQuestRejected` handler, and a fragment script is
not attached to the quest, so the event cannot land there. Every beats Delve carries
`papyrus/duo_delve_quest.psc`, which hides every objective the build wrote (`Objectives`, filled by the
build) and calls `Stop()`, vanilla's shape (`SQ_TreasureMapQuestScript`). Its `.pex` is deployed once,
shared by every Delve; the build's read-back checks it is the only quest script and carries every objective.

| `type` | the player | stock hook | fragment |
|---|---|---|---|
| `use` | activates it; the prompt goes | `DefaultAliasOnActivate` | objective done, next shown |
| `pickup` | activates it; gets `item`, the object vanishes | `DefaultAliasOnActivateGiveItem` | objective done, next shown |
| `deliver` | activates it after the earlier steps | `DefaultAliasOnActivate` | takes every item picked up or recovered since the last deliver |
| `talk` | talks to a `person` created at `at` on quest start: a `greeting`, then `exchanges` (`{ "prompt", "reply" }`, prompt 60 characters, reply 200, words only). `exchanges[0]` closes the talk; the rest loop back to the menu in any order | none: the closing reply's own `SetParentQuestStage` (on END) sets the beat's stage | objective done, next shown. **A talk that is the whole LAST step is a HAND-BACK** (since 2026-10-13, 11a): its stage is 100 and takes every item still carried, as a deliver does. The person's lines are voiced (`person.voice`, `person.elevenlabs`) with lipsync built in (bethesda 39) |
| `recover` | takes `item` off whoever has it (loot, pick up, be handed it: all count) | `DefaultAliasOnItemAddedScript` on the PLAYER (A-D duplicates for a second to fifth) | on ENTERING the step: the holder and a gang (0 to the base driver's max, from its own list) spawn at `at`, the holder into an empty alias the objective follows |
| `hold` | fights off `waves` waves at `at`, each arriving when the last is down | `DefaultCollectionAliasOnDeath` on each wave's own empty collection alias (a copy of vanilla's UC08 `ActiveHostiles`), `TurnOffStageDone` its own stage | on ENTERING the step and on each wave's stage: the next wave (`size` [min, max], from the base driver's gang list) spawns 30 to 100 m from the player on any bearing outside the 45-degree cone he faces (else at `at` when he is more than 150 m from it); a `horde` wave is split between his left and his right (see `style`) INTO its collection, which wears a Travel-to-player package, the counter ticks, and the spawning fragment waits out the reveal and the stuck guard (`duo_delve_lib.SpawnWave`, `Utility.Wait`) |
| `defeat` | kills the `target` (an Npc or LeveledNpc), who appears at `at` when the step BEGINS with an optional `gang` (`list`, `min`, `max`, `species` `one`/`mix`) who need not die (the Bounty driver's gameplay, `dou_artifact_ground_bounty_quest`) | `DefaultAliasOnDeath` on an EMPTY Optional alias the target is placed into | on ENTERING the step: `duo_delve_lib.SpawnTarget`; the objective follows the target |
| `clear` | kills every one of `enemies` (`list`, `min`, `max`, `species`: `one` picks a single entry for all, the Hunt driver's way; `mix` draws per enemy), who appear at `at` when the step BEGINS and hold the site (no package) | `DefaultCollectionAliasOnDeath` on an EMPTY collection (a copy of vanilla's UC08 `ActiveHostiles`), `TurnOffStageDone` its own stage | on ENTERING the step: `duo_delve_lib.SpawnGroup`; the objective is on the group |

**The fight beats, `defeat` and `clear`** (2026-10-14, his scope: *"I want our delve system to be able to make the GAMEPLAY of these quests"*, Overtime's remote Bounty and Hunt; spike `office/spikes/2026-10-14-defeat-and-clear-beats.md` in the home office). Every list is NAMED IN THE RECIPE (his ruling), resolved own-or-master and refused if missing or empty. Both take an optional `loot` (`list`, `chance` 1-100): each enemy spawned, the target too, carries one entry with that chance (the drivers: `duo_ArtifactList_Local`, 25). Suspicious 2 and Aggression 2, the shipped drivers' values. A fight has no object of its own, so `model`, `name`, `replace`, `item`, `returnTo`, `group` and `choose` are refused on it, and an `approach` or a `defend` cannot point at one. ⚠ **No stuck guard** (a `hold` has one): a fight can begin at quest start, before he has even flown there, so a timer would be meaningless; an enemy spawned out of reach would hold the step.

**Beat fields on this template:** `type` (required), `at`, `place` (`main` / `second` / `third`),
`objective`, `journal`, `message`, and optionally `item` (pickup and recover: the inventory name),
`model` and `name` (what the object looks like and its prompt; not on a recover, which has no object),
`group`, `replace`, `returnTo`, and on an ending `choose`, `reward`, `person`, on a talk `person`, `greeting`, `exchanges`, and on a hold `waves`,
`size`, `defend`, `stuck`, `style`, `reveal`, `revealObjective`.
`returnTo: N` makes this beat an EARLIER beat's object visited again (03 to 05 come back to the centre to
finish). The target must be a `use` beat, returned to once (the stock hook has one duplicate,
`DefaultAliasOnActivateA`); this beat repeats its `at` and `place` and sets no model or name. The first
visit keeps its prompt so the return can fire.
`replace: true` is for a marker that IS a placed object (`REContainerLocRef` is the box itself): the
box is disabled when the quest starts and the beat's object stands in its place (his way: `Disable(False)`,
never re-enabled). Without it the beat's object spawns inside the box (`duo_delve08`, 2026-10-09).

**Any order, with a counter:** consecutive `use` beats with the same `group` are one step. Only the first
carries the `objective`; the build appends the vanilla counter, `(<Global=…>/N)`, and the step completes
when all N are done. The counter resets when the quest starts, because these quests are repeatable.
Each member's marker goes out as it is done: its target is lit only while its stage is not done (`GetStageDone == 0`, vanilla's way). Clearing the alias was tried and does not drop a marker.

**A hold (Jessica's Type 6, `duo_delve09a`):** `waves` (1 to 5) waves of `size` [min, max] enemies (min at
least 1: an empty wave never clears) spawn one after another. **Where (2026-10-12, his "spawning the enemies
closer to the player", then "the distance between 30-100m and the arc choosen randoming outside of a 45 angle where
the player is facing"):** a random 30 to 100 m from him, on any bearing outside a 45-degree cone centred on his
facing (more than 22.5 degrees off it), re-read every wave from his heading the way vanilla does (`(sin, cos)` of
`GetAngleZ()`), each enemy snapped to navmesh within 10 m of the point. When he is more than 150 m from `at` (a
pity timer while he has wandered off) the wave forms at `at`. *History: the site's travel rings to 10-12 (60 to
130 m out, where he saw them stutter); then 25 to 35 m behind him, close enough that he could aim where they
landed; the first behind-him build used `MoveTo`'s `abRotateOffset` without `abMatchRotation` and every wave came
from "the same place".* **`style`**, optional, one per wave: `squad` (one group, anywhere on that arc; the default)
or `horde` (one group on his left half of the arc and one on his right, so it closes from two sides). **`reveal`**,
optional (his ask 2026-10-12: "after like 20 seconds of the wave we set the enemies as objectives? That way if the
player hasn't seen them it points them out"): seconds after a wave spawns before its enemies carry objective markers.
Absent = 20, `0` = never, and it must be under `stuck` (lint). Each wave gets its own objective, the hold's index + 1
+ wave (31, 32, 33 on `duo_delve09a`), targeting that wave's collection; the spawning fragment shows it after a `Utility.Wait` and the wave's own stage
hides it, so a cleared wave's bodies never keep a marker. **`revealObjective`** is its text, absent = "Eliminate the
attackers" (Eliza's placeholder; words are Jessica's). Both are refused on a beat that is not a hold. A horde is `size` PER SIDE (his "third wave seems small"). The
lint refuses a count that is not one per wave, an unknown word (case counts), and `style` on a beat that is not a
hold. Every
wave collection wears `<id>_wavepkg`, a copy of vanilla's `Trait_Wanted_TravelToPlayer` (jog, weapon drawn,
to PlayerRef, only while he is 9 to 1000 m away) at **Run** (the source jogs; his "They are slowish"), so they come
for him. No `StartCombat` at spawn: tried and dropped, because from 40 m+ out of sight combat lapsed and they stopped
short. The objective gets the counter `(n/waves)` appended and is redisplayed as each
wave falls, which is the player's feedback (no message boxes in this type, his ruling). `defend: N` points
the objective at beat N's object (a use or a deliver; absent, the spawn marker). Each wave is one stage, the
last landing on the step's done stage. **The stuck-enemy guard:** a wave still alive `stuck` seconds after it
spawned (default 180, his infestation driver's pity timer) is let go and its stage set anyway, by the
fragment that spawned it waiting (`Utility.Wait`) at its end; a later kill cannot set it twice. ⛔ **Not a timer:**
`StartTimer` is `selfonly` and a fragment script is not attached to the quest, so an `OnTimer` in one never fires
(his play of `duo_delve09a`, 2026-10-12: the reveal never showed, so the guard had never worked either; vanilla's one
fragment timer, `QF_RI06`, calls a method on the quest's ATTACHED script). The build verifies there is no `OnTimer`. A hold has no object of its own,
so no `model`, `name` or `replace`, and an approach cannot target it.

**`recap` when the Delve ENDS on a group:** the last member sets its stage and stage 100 in the same
instant, and the quest log shows only the newest line, so its own `journal` is never read and stage 100
would show the base's line. `recap` is stage 100's line, and the lint requires it there. Ending on a
single beat, that beat's `journal` already is stage 100's line, so a `recap` is refused.

**Endings, `choose`:** consecutive `deliver` beats with the same `choose` name are the ENDINGS, and they
must be the last step. Each is its own place (two endings' places that can draw one POI are refused), its
own objective (all shown together once the choice opens), and its own completing stage: the first ending
100, the next a COPY of 100 at 110 (so it completes and pays the same way), and so on. Each needs a
`reward` tier (`easy` / `med` / `hard`, Overtime's `duo_reward_*` globals) written onto its stage.
Walking to one takes the carried things, blocks every ending's prompt, HIDES the others' objectives
(never failed: neither side is the villain) and ends the quest. An optional `person`
`{ "name", "template", "outfit", "company": { "list", "min", "max" } }` is a friendly NPC cloned from a
vanilla template, placed beside the ending (with nameless company around it) the first time the player
comes within 250 m of it after the choice opens. ⚠ The beat before the endings must have no `journal`:
`offer.journal` lands on that stage in the same instant and is the line the player reads.

**`approach: { "to": N, "civilians": true, "lose": M }`:** the first time the player comes within
250 m of beat N's object, a stock `DefaultAliasOnDistanceLessThan` on the player sets stage 5, whose
fragment places 1 to 5 people from the base driver's own civilian list around it (`civilians`) and, if
pickup beat M's crate is still there, moves it out past the site's near edge so it reads as lost on the
way (`lose`; M must be at the same place). The work is `papyrus/duo_delve_lib.psc`, Global functions
lifted from the retired driver; **`duo_delve_lib.pex` must be packed with the mod.**

**Stages are numbered by the build:** 0 runs on start, then 10, 20, … per beat (a group takes one more),
and the last step always lands on 100, the base's completing stage, which carries the reward.
**`reward` on a single ending** (since 2026-10-13): the beat that is the whole last step (not a group, not a
choose) may carry a tier, which replaces the base's own pay on stage 100 and is read back off disk. Anywhere
else `reward` is refused, because the stage it would pay on completes nothing. Absent, stage 100 pays the base's
(`easy`).

**The base's object slots no beat takes are REMOVED** (since 2026-10-13): beats with an object of their own
(use, pickup, deliver) take the base's two marker+activator slots first. A recipe with fewer (11a: a recover and a
talk) leaves slots whose marker alias would still demand its marker at the place, which a dungeon does not carry,
so the quest would silently never start. The build removes each such pair after the objectives are rebuilt,
refuses if anything still names it, and the verification reads back that it is gone and named by nothing.
`setstage <id> <stage>` jumps straight to a beat.

⛔ **Deliver is gated by STAGE ORDER, not by the stock `DefaultAliasOnActivateRemoveItems`:** that script
sets its stage first and checks the item afterwards, so as a gate it would complete empty-handed.

**Build:** as above, then compile the generated script by name, which `compile.py` finds in `gen/`:
`python papyrus/compile.py C:/modding/DU_Overtime/Data duo_qf_<id>` and copy the `.pex` into Steam
`Data/scripts`. **Two places drawing one POI is warned**, so keep a trail's places disjoint by theme
(`duo_delve08a` splits natural and military).

## Speech: a speaker, a broadcast, and lines on stages (opt in)

```json
"speaker": { "name": "Relay Control", "voice": "GenericMale01", "elevenlabs": "CwhRBWXzGAHq8TQ4Fs17" },
"broadcast": "This is Relay Control. ...",
```

and `"say": "..."` on any beat. **Which voice: [voices.md](voices.md)**, the described list. `broadcast` plays when the quest starts; a beat's `say` plays when that beat's
stage is set. ⛔ **The broadcast is started by the ENGINE (the scene's `BeginOnQuestStart` flag), never by
stage 0's fragment:** stage 0 runs while the quest is still starting and a `Start()` there plays nothing (his
probe, 2026-10-09: stage 0 `playing = FALSE`, the same scene started at stage 10 played with voice and subtitle). Each line is a RADIO scene with no actor in the world (`AliasID -4`, the vanilla
audio-log shape, `docs/formlib/book_audio.md`): one topic, one response the speaker says, started by the
stage's fragment. The speaker is an NPC that is never placed; it carries the subtitle name and the voice type
whose folder the audio lives in. **Words only:** a spoken line cannot carry a `<Token>` (the voice cannot say
one), and holds 250 characters. The lint refuses lines with no speaker and a speaker with no lines.

**The audio** is generated at build time (ElevenLabs, then Wwise) into
`Data/Sound/Voice/<plugin>.esm/<voice>/<topic id, 8 hex>.wem`. A rebuild re-mints every topic id, so
**`C:/modding/DU_Overtime/voicecache/`** keeps each line by a hash of voice, voice type and exact words (with a
`.txt` saying which), and a rebuild COPIES the cached file to its new name; ElevenLabs is called only for new
or changed words (his ask). The previous build's files are deleted first by `deployed_<id>.txt`. A line that
fell back to the Windows voice plays but is not cached. ⚠ The deployed files are LOOSE: pack them before a
release.

## Board: a mission from the mission board

His 2026-10-14: *"full sep missionboard template"*. The board talks to ONE script, vanilla's `MissionQuestScript`
(`Data/Scripts/Source`, read whole): it sets **stage 5** when the mission is ready to show, the board's accept
sets **stage 10**, `MissionComplete()` pays through `MissionParent` and sets **stage 100**, and a turn-down sets
**200**. Vanilla runs it with no driver at all on three ground bounties (`MB_Bounty03_FCR02`, `MB_Bounty04_FCR04`,
`MB_Bounty05_UC`), and Overtime's `duo_MB28a_qst` is that skeleton plus `duo_bountyscript`. So the `board`
template clones MB28a, takes `duo_bountyscript` off, and puts `MissionQuestScript` on carrying the ten board
properties the driver set (`questScriptKeeps` in the row). What changes from a slate Delve:

- **It begins on stage 10, never on start.** A board quest STARTS when the board refreshes, long before
  anyone takes it, so step one's spawn, its objective, its counters and its `broadcast` all go on the accept
  stage (`entryStage`), and the first step's hooks wait for it. Beat stages number from 20. Verify refuses a
  stage that runs on start and any fragment below 10.
- **It pays through the board.** The last stage waits for its radio line and calls `MissionComplete()`
  (vanilla's own stage-100 fragment, autocast and all), never `CompleteQuest()` and `Stop()`. A beat's
  `reward` tier sets the script's `RewardAmountGlobal` and its `Actual`, which the card shows; never the
  stage's QRCR, or it pays twice. No tier keeps the base's (`duo_reward_creds_easy`).
- **The place is always a request.** MB28a's `TargetLocation` is one already, so `place.remote` is refused;
  the lint measures the pool against what a request can place, as for a remote Delve, with no parsec limit
  (the board picks the system).
- **`PrimaryRef` is beat 1's marker.** The board's system and planet aliases resolve FROM it, so it stays,
  its ref type rewritten to the first fight's `at`; its base death hook goes. The driver's `EnemyType` goes.
- **Fights only**: `defeat`, `clear`, `hold` (`allows` in the row). The base has no object slot, no player
  alias and no approach stage, so anything needing one is refused by name.
- **`deal` names a board node** (`_duo_MB_MissionNode_test` for the first). The proof recipe is
  `duo_delve_missionboard01a`, remote05a's gameplay; ids take the form `duo_delve_missionboardNNa` (his).

## Not yet

- ~~**An entry item.**~~ Every Delve is dealt from a dataslate pool by its `deal` (2026-10-12); `startquest` still works for a test.
- **A third place outside the choice template**, and a choice with more than two endings.
- ~~**Dialogue.**~~ The `talk` beat (2026-10-12). Choose endings' people still stand and do not talk.
