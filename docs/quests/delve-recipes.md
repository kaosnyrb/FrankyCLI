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
| `carry-absence-recover-return` | delve4 | exactly 4 | find the load, find the other half gone, take it back off the carrier, finish the job (testcase 01; `duo_delve03`-`05`) |
| `find-owner-or-buyer` | choice | exactly 3 | find a thing with a name on it, then return it to its owner OR sell it to a buyer, at two different places; walking to one ends it (Type 2; `duo_delve06`) |
| `beats` | beats | 1 to 9 | **no driver: the stages are the state machine.** Each beat has a `type`; any order behind a counter with `group` (types 1 and 3; `duo_delve07`, `duo_delve08`). See *Beats* below |

**Tokens** an author writes in prose, mapped by the template onto its real aliases:

| template | tokens |
|---|---|
| two-beat-one-place, carry-absence-recover-return | `<Place>`, `<Planet>` |
| find-owner-or-buyer | `<FindPlace>`, `<OwnerPlace>`, `<BuyerPlace>`, `<Planet>` |
| beats | `<Place>` (main), `<SecondPlace>`, `<ThirdPlace>`, `<Planet>` |

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
| `prose` | object | yes | all | `name` (the quest title) and `briefing` (the journal's opening line) |
| `items` | object | delve4, choice | delve4, choice | what is carried and what the objects look like (below) |
| `beats` | list | yes | all | one entry per beat (below) |
| `offer` | object | choice: yes | choice | the surprise between the find and the endings |
| `reward` | object | choice: yes | choice | the pay tier per ending |
| `people` | object | no | choice | named NPCs at each ending, with outfits and company |

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
| `second` | the second POI's own theme (a beat with `"place": "second"`) |
| `third` | choice only: the third POI's own theme (a beat with `"place": "third"`) |
| `leash` | **advisory, not written.** The base's own distance limit is kept as is |
| `civilians` | delve4 only, default `true`: put civilians at the centre. Set `false` when the theme already guarantees people. ⚠ The lint cannot tell an explicit `true` from the default, so it does not refuse this on other templates |

**Useful keywords**, from the census (`gen_delve keywords [filter…]` tallies themes inside a narrowed
pool): `LocTypeOE_NonHostile` (people are there, 62 POIs with a centre and map marker) ·
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
- **carry-absence-recover-return:** exactly 4. Beat 4 is a return and must name beat 2's marker. Only
  beat 1 may be at the second place.
- **find-owner-or-buyer:** exactly 3, in this order: the find at `second`, the owner at `main`, the
  buyer at `third`.

### `items`

| field | template | what it is |
|---|---|---|
| `load` | delve4, choice | the carried thing's inventory name (a clone; never the base's item) |
| `missing` | delve4 only | the other half's name |
| `crateModel` | delve4, choice | the NIF the found thing wears, `Meshes\…` |
| `centreModel`, `centreName` | delve4, choice | the delivery point's NIF and its activate prompt (on a choice, the OWNER's) |
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
excludes. ⚠ **Asking for people at both ends is expensive:** the peopled pool is 62 POIs galaxy-wide, so
`duo_delve06` starts only on maps that have one of each in range (his play: *"Quest starts, sometimes.
Map dependant"*).

---

## Letter variants

**Why they exist: a quest runs ONE instance at a time** (his, 2026-10-08), so the same mission cannot
sit in the player's log twice. Each letter is its own quest record, which is what lets `07a` and `07b`
run in parallel. A variant is not only new lore; it is another copy that can be live at once.

**Full recipes** (his ruling, 2026-10-08). `duo_delve07a`, `duo_delve07b`: same structure, new names
and words. `lint --all` compares siblings that share a number and names any drift in template, beats,
themes or reward tier, so a fix made in `a` and forgotten in `b` shows up. Siblings **may** share a
delivery model; two different numbers sharing one warns. A variant that needs different structure is a
new number.

---

## What the lint checks

**Refused:** not JSON · an unknown key · an id that is not its file name · a template that does not
exist · a field the template never reads · the wrong beat count or shape for the template · a missing
objective on a driven beat, or an objective on an extra beat · a marker, keyword, outfit, company list,
NPC template, model or reward tier that does not exist where `du_overtime` can reach it (the mod and its
masters, never the CK bridge `du_overtime.esp`) · a place whose pool is zero · a choice whose two endings
can be the same POI · a token in a name · an item or NPC name already in the game, or used by two
recipes · an empty message box.

**Warned:** a pool under 40 · a model that sinks, hangs or floats, or that no Static owns · a journal
line the log will never show · an objective over 12 words or ending in punctuation · any em dash or
non-ASCII character · two beats on one marker or one travel ring · a base exclusion the recipe drops ·
two mission numbers sharing a delivery model · letter siblings that differ in structure · the buyer not
paying more than the owner.

**The build adds, off disk:** every alias fills below what it fills from (the order the engine needs),
every place demands exactly its markers, the plugin's masters are unchanged, and every driver property,
objective target, journal, stage reward and NPC field reads back as written.

---

## Beats: a Delve with no driver

**His ruling, 2026-10-08: "The stages of the quest are the state machine. All of vanilla Starfield was
built this way."** A `beats` recipe lists its beats by **type**. Each type is a stock `Default*` alias
script that sets the beat's stage once the previous step's stage is done, plus a few lines in ONE
fragment script the build generates (`papyrus/gen/duo_qf_<id>.psc`, gitignored, never hand-edited) for
the half that is Papyrus-only: objectives, the counter, taking delivered items, a message box.

| `type` | the player | stock hook | fragment |
|---|---|---|---|
| `use` | activates it; the prompt goes | `DefaultAliasOnActivate` | objective done, next shown |
| `pickup` | activates it; gets `item`, the object vanishes | `DefaultAliasOnActivateGiveItem` | objective done, next shown |
| `deliver` | activates it after the earlier steps | `DefaultAliasOnActivate` | takes every item picked up since the last deliver |

**Beat fields on this template:** `type` (required), `at`, `place` (`main` / `second` / `third`),
`objective`, `journal`, `message`, and optionally `item` (pickup only, the inventory name), `model` and
`name` (what the object looks like and its prompt), `group`.

**Any order, with a counter:** consecutive `use` beats with the same `group` are one step. Only the first
carries the `objective`; the build appends the vanilla counter, `(<Global=…>/N)`, and the step completes
when all N are done. The counter resets when the quest starts, because these quests are repeatable.

**Stages are numbered by the build:** 0 runs on start, then 10, 20, … per beat (a group takes one more),
and the last step always lands on 100, the base's completing stage, which carries the reward.
`setstage <id> <stage>` jumps straight to a beat.

⛔ **Deliver is gated by STAGE ORDER, not by the stock `DefaultAliasOnActivateRemoveItems`:** that script
sets its stage first and checks the item afterwards, so as a gate it would complete empty-handed.

**Build:** as above, then compile the generated script by name, which `compile.py` finds in `gen/`:
`python papyrus/compile.py C:/modding/DU_Overtime/Data duo_qf_<id>` and copy the `.pex` into Steam
`Data/scripts`. **Two places drawing one POI is warned**, so keep a trail's places disjoint by theme
(`duo_delve08` splits natural and military).

## Not yet

- **An entry item.** No Delve has one; they start from the console. Jessica's design starts them from a
  dataslate (an enemy's drop, or his buildable mission terminal).
- **A third place outside the choice template**, and a choice with more than two endings.
- **Dialogue.** The people stand and are present; they do not talk.
