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
dotnet run -- gen_delve build <id> --dry --emit <dir>   the would-be plugin and the emitted model, to <dir> only
```

**Then a MANUAL step for any recipe with a talk beat:** `python scripts/delves/talk_check.py <id>` (or no id for all,
`--flagged` for only the menus it flags). It prints what the player is sure to have heard before each menu (the
greeting, an earlier talk's closing reply, what he is carrying) and marks prompts whose words are not in it. **A
person reads it**; the lint cannot, because this is about meaning. His plays, 2026-10-22/23: 19a *"he never tells
you the sattlie is bust"*, 11a *"the growing line is unprompted"*, 11b the drill. Read the `*` rows first.

**True construction (his ruling, 2026-10-15).** A template with a `shell` builds its quest with `new` from
`data/delves/shells/<template>.json` (the quest the template starts from, as a `gen_questmodel` model) and never
reads the base quest. Every build then **emits the quest's model** to `data/delves/models/<id>.json` beside the
recipe, and the quest written into the plugin is built from that file's text. The model is the build's output:
read it to see exactly what a Delve's quest holds. `gen_delve shell <template>` writes a shell once from the base;
the file is ours after that. `--from-base` starts from the old clone instead, for the A/B proof (`--dry --emit` both
ways, compare the two plugins by bytes).

**The parts (2026-10-15, his "close the 2 places").** Beside each shell, `data/delves/shells/<template>.parts.json`
holds every alias the build takes off ANOTHER quest, as models: `remotePlaceFrom`'s PCM request (a remote place),
`borrowFrom`'s aliases (the board's player, object slots and second place) and vanilla's empty wave collection
(`UC08_AB_AnomalyBattle`'s `ActiveHostiles`, which `hold` and `clear` copy). `gen_delve shell <template>` writes the
shell and its parts together, reading everything before writing either. **Both templates have both, so a build
reads no quest at all.** A shell without the parts its template takes is refused, never filled from the quest, and
a parts file read off a quest the row no longer names is refused too. **Open:** the lint still reads the base quest
(for two warnings, never for the build). `scripts/delves/ab.py` is the byte-for-byte A/B every step is proven by.

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
`SharesEvent`, `NumQuestsToRun` 1. `testnode` does not apply to it. **A build dealt into it EMPTIES it first** (his
2026-10-26: *"It should be cleared out before each test so only the tested mission is there"*: a board refreshes only on
a sleep, about 2 minutes, so five in the node is a 1-in-5 roll, and a quest that failed to fill never shows, which a roll
cannot tell from bad luck). Every board recipe deals here, so the quests it held leave the board until each is rebuilt;
the build names them, and the verify asserts the node holds the built quest alone. ⚠ The `.esp` twin carries its own copy of every
node; `testnode` edits the `.esm` only.


**The legacy mission system: `gen_delve legacy <pool node> [--dry]`** (2026-10-20, his ask; his shape 2026-10-17:
the old quests stay in the plugin, a setting OFF by default, ON mixes them in, never old-only). It moves every member
of a pool that is NOT a Delve (a Delve's EditorID names a recipe here, and must agree with the `duo_delve` prefix) into
a new sibling, `<pool>_Legacy`, placed FIRST under the same branch and gated on the pool's own conditions plus
`GetGlobalValue(duo_ClassicMissions) == 1` and `GetRandomPercent < duo_ClassicMissionsShare` (50). So with the option
OFF the legacy node is skipped and the pool deals Delves only; ON, about half the events deal an original quest.
**Why a node:** a Story Manager quest entry is `Quest` + `HoursUntilReset` and a QUST has no event conditions, so a
pool cannot gate one member. The first run also creates the setting (all five or refused): the option
`duo_GPO_ClassicMissions` (Float 0..1, Off/On, default Off) in group `duo_GPO_Group`, the two globals, and
`duo_GameplayOptionsQuest` (start game enabled) running `papyrus/duo_gameplayoptions.psc`, which copies the option into
`duo_ClassicMissions` (vanilla PEO's shape, and his Suit Power Cores'). Compile that script once: `python
papyrus/compile.py C:/modding/DU_Overtime/Data duo_gameplayoptions`, then copy the `.pex` into Steam `Data/scripts`.
Refuses a pool already split. No quest is touched; a Delve built later still deals into the pool its recipe names.
⚠ **New Delves for a split pool go to the POOL, never to `_Legacy`.** Snapshot first, as for a build.
⚠ **A board pool is different (2026-10-20, Bounty):** a `duo_MB_MissionNode_*` node carries `SharesEvent`, so a refresh
walks on after it deals and the legacy node and the pool BOTH deal; there the legacy node gets NO share gate (On =
today's originals plus the Delves, Off = the Delves only) and keeps the pool's caps (Bounty: 4 / 4 each, so a
Bounty-heavy board can show more cards once the pool holds several Delves). **A pool needs at least one Delve before it
is split,** or Off deals nothing from it: board Delves are moved off `_duo_MB_MissionNode_test` first with
**`gen_delve move <delveId> <fromNode> <toNode> [--dry]`** (a list edit, never a rebuild; then the recipe's `deal`).

**The rare tier: `gen_delve rare <pool node> <delveId>[,<delveId>...] [--dry]`** (2026-10-20, his: *"the picnic
quest should happen quite rarely cus its unique"*, *"we'll make other rare delves too"*). Moves built Delves from a
pool into `<pool>_Rare`, created on first use directly BEFORE the pool (after its `_Legacy`), gated on the pool's own
conditions plus `GetRandomPercent < duo_RareDelveChance` (3, shared by every rare node, not Constant). Then set each
moved recipe's `"deal"` to `<pool>_Rare`, JSON only. A new rare Delve is simply built with that `deal`.
⚠ **The chance is per EVENT, not per quest:** it is how often the pool's slates try the rare node, so with one member it
is that member's rate. Raise it as rare Delves are added. Not gated on Classic Missions. **Why before the pool:** a node
after it is reached only when the pool starts nothing, which is where the original `_Local_LowChance_Node` sits (his
ship-landing missions, meant to be rare, likely never dealt; its order is his call). The lint's base-quest warning
reads the whole BRANCH, so a Delve in a tier node does not warn for its base living in `_Legacy`.

**The walk-up header, in place: `gen_delve infogroup <delveId>[,...] [--clear] [--dry]`** (2026-10-23, his: *"try the
InfoGroup fix on a different quest, a simpler one that we know works"*). Sets (or clears) the record-header flag
`InfoGroup` on a built Delve's walk-up groups (`<id>_talk<n>_hellos`) with no rebuild, so no id, lip file or shared-code
change rides along. It passes only if the written plugin differs from the live one by exactly one byte per group, that
group's header flags byte, in bit `0x40` alone; `--dry` writes the same bytes to a temp folder and diffs them without
touching live. Snapshot first (DU_Overtime `sync_plugin.py --snapshot`). After a real write it re-emits each named
Delve's model from the edited quest, so the model agrees with the plugin. First use: `duo_delve14a`, played; then 08a,
11a, 11b, 15a, 18a, missionboard02a and 32a the same morning. A build writes the header itself since the same day, so
this is for a Delve built before.

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

`dotnet run -- gen_delve templates` lists them. Each is one row in `data/delves/templates.json`, and the registry
refuses a key a row cannot hold exactly as a recipe does (a `_` key is a note), so a retired field cannot sit there
read by nothing.

| template | kind | beats | the shape |
|---|---|---|---|
| `beats` | beats | 1 to 9 | **no driver: the stages are the state machine.** Each beat has a `type`; any order behind a counter with `group`; a thief to `recover` from; a `returnTo` an earlier object; an `approach`; endings to `choose` between; waves to `hold` off (`duo_delve03a`-`09a`). See *Beats* below |
| `board` | board | 1 to 9 | **a mission-board mission on the beats machinery** (his 2026-10-14). The base is `duo_MB28a_qst`; its driver comes off and vanilla's `MissionQuestScript` goes on, so the BOARD starts it, takes it and pays it. fights, and since missionboard02a `use`, `pickup`, `deliver` and `talk`, with a requested second place on another planet. See *Board* below |
| `space` | space | 1 | **a Delve whose ONE place is an orbit** (2026-10-22, his "A": the alias-set test ladder's harness). The shell is `duo_artifact_remote_qst03a` WHOLE, the space set his 62 remote-space quests share; one `use` beat on the cell's own marker, no speech. Its own build (`delve_space.cs`), not the beats machinery. See *Space* below |
| `boardspace` | boardspace | 1 | **a mission-board mission whose ONE place is an orbit** (2026-10-26, his *"I want a missionboard that is only space"*). The base is his MB01a, which plays: its own activator (`ccs_activaterepair`, the repair, the attackers, `MissionComplete()`) is kept and reskinned, the board's `MissionBountySpaceScript` stays. The broadcast plays within `arrivalDistance` (1500, his) of the object, measured to THE SHIP HE IS FLYING, never a Player alias (it never fired); the closing line plays on stage 100 and its fragment waits for it, and never calls `MissionComplete()` again (the deadlock in his log). The tier goes onto the board's reward globals. *(Documented 2026-10-26 pm: built that morning and left out of this table.)* |
| `slatespace` | slatespace | 1 | **a remote-space SLATE Delve: one orbit, dealt by a slate node** (2026-10-26, his *"yup go"*, `duo_delve34a`). The `space` shell and alias set exactly; no start event (a slate Delve needs none, remote01a's model carries none). The call starts with the quest (the scene's `BeginOnQuestStart`, never stage 0's fragment), the closing line plays on stage 100 and the fragment waits for it before `Stop()`, and a `reward` tier is written onto stage 100's one reward entry, as a ground slate pays. `<Planet>` and `<Place>` both name the orbit. One `use` beat. Tested on the parked `_duo_MS_ArtifactMapRemoteSpace_TEST` with `testnode on --keep`. |

*Retired 2026-10-09: `carry-absence-recover-return` (kind `delve4`, `duo_delve_driver.psc`). 03 to 05 moved
onto `beats` and played the same in his hands the same morning; the old shape is now `pickup` / `use` /
`recover` / `deliver` with `returnTo: 2` and, on 03, an `approach`.*
*Retired 2026-10-15 (his "yup" to deleting them first in the gen_delve split): `two-beat-one-place` (kind
`dualactivator`) and `find-owner-or-buyer` (kind `choice`, `papyrus/duo_delve_choice.psc`), with no recipe on
either since 10-09. The choice became `choose` endings on `beats` (`duo_delve06a`, `06b`). A row of a retired
kind is refused when the registry loads, and a recipe still carrying `items`, `reward` or `people` is refused
as an unknown key.*

**Tokens** an author writes in prose, mapped by the template onto its real aliases:

| template | tokens |
|---|---|
| beats | `<Place>` (main), `<SecondPlace>`, `<ThirdPlace>`, `<Planet>`, `<SecondPlanet>` (only with `place.second.remote`: a found second place is on the main place's planet) |
| board | `<Place>`, `<Planet>`, `<System>`, `<SecondPlace>`, `<SecondPlanet>` (the last two only with a beat at the second place) |

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
| `schema` | int | yes | all | always `2` (since 2026-10-19; a schema-1 recipe is refused at read, by name). ⭐ **Schema 2: every SPOKEN field is a LIST of responses**, said back to back, each its own subtitle and audio file, 1 to 4 per line (vanilla, 1,027 lines measured: 909 one, 101 two, 17 three): `broadcast`, a beat's `say`, `offer.say`, `recapSay`, `greeting`, each exchange's `reply`, and each of `person.hellos`. One response is `["…"]`. Menu text (`prompt`), journals, objectives and box text stay plain strings. His ask: *"every line of dialouge is one string. Need to add multiple line support"*. Every recipe was migrated to one-response lists the same day, words unchanged, for Jessica to split |
| `id` | string | yes | all | the quest's EditorID and every clone's prefix. **Must equal the file name** (`duo_MB50a_qst.json` has id `duo_MB50a_qst`). A built Delve carries the classic grammar (`duo_MB<NN><x>_qst`, `duo_artifact_<local\|localcargo\|remote\|remote_planet>_qst<NN><x>`); a draft not yet built may still be `duo_delve…`. See *Naming* below |
| `template` | string | yes | all | a template id from the table above |
| `author`, `source` | string | no | none | provenance; not written into the game |
| `place` | object | yes | all | where the mission happens (below) |
| `prose` | object | yes | all | `name` (the quest title) and `briefing` (the journal's opening line); on a `board` mission also **`card`, required there and refused elsewhere**: the board's description, what he reads before taking the job |
| `beats` | list | yes | all | one entry per beat (below) |
| `offer` | object | no; refused unless a step is a `choose` | beats ending on a `choose` | the surprise between the find and the endings: `journal` lands on the stage that opens the choice, then `message` (a box, after the find's) OR `say` (a radio line), never both |
| `recap` | string | beats ending on a `group`: yes; otherwise refused | beats | stage 100's journal line, the last thing the player reads (see *Beats*) |
| `recapSay` | list of strings (schema 2) | no; refused unless the Delve ends on a `group` | beats | the speaker's closing line, `recap`'s spoken twin, on the completing stage. A group's members fire in any order, so no member's `say` can be the last line heard (Jessica, remote03a); a single-beat ending's own `say` already is. His play of remote03a, 2026-10-14: *"first radio message played but nothing at the end?"* Needs a `speaker` |
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
| here, then another planet (type 8, 10a, built 2026-10-14) | `Supply Run` | Supply Run: Dig Site on `<SecondPlanet>` |
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
| `third` | the third POI's own theme (a beat with `"place": "third"`); the place is CREATED at build, a clone of the template's `thirdPlace.cloneOf` |
| `third.orbit` | optional, **`"planet"` = the third place is an ORBIT over the main place's planet, not a POI; `"system"` = an orbit anywhere in its star system** (the same condition with `LocTypeStarSystem`; his *"yeah whole system"*, 2026-10-22, after `"planet"` would not start 19a in any map) (2026-10-22, his *"go on 19a, try over"*; `duo_delve19a` first). The build copies the template's `orbitFrom` chain off the parts file (beats: his MB01 satellite repair, `duo_MB01a_qst`): a `LocTypeOrbit` location, its altitude kept (20.589, his 8-32 band), tested `IsInSameCurrentLocationAsAlias(the main place, LocTypeMajorOrbital) == 1` and `LocTypeSENotAllowed == 0`; `LVLSC_AllSpacePOIs` created at it; each orbit beat's marker filled FROM that cell by `HasRefType` (a created cell's markers are on no location, so an in-location fill finds nothing). Its beats are `use`, `pickup`, `deliver` only (a thing used from the ship, as MB01's satellite), each `at` `se_TravelMarkerLocRefB02`; no theme, no `who`, no leash. ⚠ **No vanilla orbit is tied to a planet this way** (0 of 1,060 measured), and the same function with the same keyword crashed as a planet guard on a REQUESTED site (10a, 12a): this shape matches his MB01's working `TargetSystemLocation` (a by-type alias tested against a location alias) instead. Proven by his play or not at all. ⭐ **And its map marker** (his catch, 2026-10-22: 896 of 903 space-cell quests carry one): MB01's `SpaceMapMarker`, filled from the cell by `MapMarkerRefType`, linked to the orbit beat's object by `MapMarkerLinkedRefNameOverride` so the star map names it, wearing `DefaultAliasMapMarkerScript` (Discovered, type 47) ⭐ **`"near"`** (2026-10-22, after `"planet"` and `"system"` never started 19a, even with the collection id fixed): no shared location at all; the orbit keeps **MB01's own conditions verbatim** (`BodyHasKeyword LocTypeMajorOrbital`, not `LocTypeSENotAllowed`, `GetDistanceGalacticParsec` from the player) and its parsec limit becomes **`place.third.parsecs`**, which `"near"` requires (his 1-parsec idea for the dungeon, pointed at the orbit). Somewhere in his system, not forced over the main place's planet. ⛔ **And the copy carries the cell's collection ENTRY id with its alias** (it did not until 2026-10-22: the entry stayed MB01's 10 beside `FinalLocation` 10), asserted by the verify's unique-id leg. Unplayed when written |
| `remote` | optional, main place only, **written**: `true` = the place is REQUESTED on another planet rather than found near the player (his 2026-10-14: *"I got sick of travelling"*). The build copies the template's `remotePlaceFrom` quest's place alias over the base's (beats: `duo_artifact_remote_planet_qst01a`'s `DungeonLocation`, a `PCM_QuestGeneral` request with its planet condition and its `GetDistanceGalacticParsec < duo_distanceformission` limit); markers and theme are then written as for any place, and the local leash to the player goes. The lint measures the pool against what a request can place (`gen_delve pcmpool PCM_LocationRequest_General`, 214 readable places on Defiant), not the POI census. **Every beat is at that one place**, unless `second` is requested too: a third place, `reach`, or `remote` on `third` is refused. **`second.remote: true`** (2026-10-14, for missionboard02a and 10a) copies the same request over the second place, keeping its name, with no drive leash to the first; no shipped quest requests two sites (556 vanilla requesting quests, Overtime's 140, none with two), and his play of the probe `duo_delve_twosite01a` PROVED it the same day: *"two different POIs on two different Planets"*. The planets differing is one play; nothing in the request forces it. **Behind a LOCAL main place too** (2026-10-14, 10a, his *"build 10a"*): the main keeps its leash to the player and the second has no leash and **NO planet guard** (the guard behind a local first site CRASHED the game on `startquest`, twice; out, it started and played end to end, his 22:3x), so nothing forces another planet, and the beats template makes alias 60 `SecondPlanetLocation` (a copy of `targetPlanet`, parent the second place) for `<SecondPlanet>`. A third place beside a second request is refused. Not yet played. Deal it from `duo_MS_ArtifactMap_RemotePlanetNode` (the lint warns on a pool that never dealt the remote base) |
| `parsecs`, `habitability` | optional, a **requested** place only (`remote: true`; refused on a found one), **written**, his 2026-10-22 for 19a's dungeon: *"remove the PCM_CND_BlockCreation_Habitability02_Medium check and make the Parsec distance like 1. That should be same system at least"*. `parsecs`: the request's `GetDistanceGalacticParsec` limit from the player compares to this number instead of the copied `duo_distanceformission` Global (above 0). `habitability: false`: the request's own planet condition (its `ALPS` conditions) is cleared; `true` is refused. Each refuses if the copied request has nothing to change. Not a planet guard, so nothing forces the main place's planet. **An ORBIT third place may now sit beside a second request** (it is held to the main place, not a drive from the second); any other third beside one is still refused. Unplayed when written |
| `system` | optional, a **requested** place only, **written**, `"same"` the one value (2026-10-22, his *"build r5"*): the request may only use a planet in the player's current star system, said the way vanilla's ONE same-system request says it (`MB_Bounty02Local`): `IsInSameCurrentLocationAsRef(player, LocTypeStarSystem) == 1` added to the request's own (ALPS) conditions, which test the PLANET, after `habitability: false` clears them. ⚠ Not `parsecs` 1: a parsec NUMBER on a request appears nowhere in his load order but ours (674 requests read), and two 1-parsec rungs did not start where he tested (R2, R4). The verify reads the condition back off disk. Vanilla's far shape (`== 0`) is not built. **On a `space` template's place (the orbit)** it writes the orbit's conditions as the ONE shipped same-system orbit has them (`MB_BountySpace06Local`): a major orbital body, not a settled planet, and the test; the shell's far filters (a parsec limit, `MB_IsPlayerLevelAppropriate`, `LocTypeSENotAllowed == 0`) go, and a shell whose planet tests differ is refused. Refused beside `parsecs`. ⛔ Rung S7 (2026-10-22 13:14) ADDED the test and kept the far filters, on my claim that 238 vanilla orbits say "same system": 236 of them test `== 0`, NOT the player's system. S7 did not start; S9 is the precedent's shape. R5 PLAYED: *"R5 works, same system"* |
| `over` | **space only** (2026-10-22, rung S10, his pointer *"Read RQ_Settlement_KillShips_01"*): the alias, by name, the orbit hangs OVER. Its conditions are cleared and `ParentSystemLocationAliasID` points at that alias, with the flag bit `0x40000000` every vanilla parent-linked location alias carries: the orbit of whatever planet that thing is at. 125 of the 1,051 vanilla orbit aliases say "near" this way (95 with no conditions); `SE_Player_*` parent to `PlayerShip`, `RQ_Settlement_KillShips_01` to its trigger. Refused beside `system` or `parsecs`. The alias order puts the parent above the orbit (`AliasGraph.ParentDepsOf`: 1,283 of 1,289 vanilla parent links point above) |
| `altitude` | optional, an **orbit** only (a `space` template's place, or `place.third` with `orbit`), **written** over the copied `ALFG`. His settled band 8 to 32 (manual 12: past a ceiling between 32 and 256 the engine discards it and reverts), anything outside refused. His 2026-10-22 *"32 distance"*, after S1 sat at 03a's 10.5 (*"that too close to the planet bug"*). The verify reads it back |
| `settlement` | optional, **`place.second` only, written**: the second place is a **named city, pinned, not drawn** (2026-10-23, city01a and missionboard05a; his *"lets just go for autofill 01 and see"*). Vanilla's own shape for carrying a thing to a person in a city, `RQ_OE_Deliver_01`'s: the place alias pinned to the city's location (`SpecificLocation`), no conditions, flags `AllowCleared | StoresText` (never `ReservesLocationOrReference`, which would reserve the city), and every marker inside it `AllowReserved`, because a generic city spot is shared with vanilla. The name comes from a CLOSED table, `"Akila City"` and `"New Atlantis"`; anything else is refused. A beat there needs an `at` the city lists: **`AutofillGeneric01`** is one outdoor spot in each city (Akila `2D1D2A`, New Atlantis `133415`); the lint counts the city's spots for every marker and refuses a zero (`RECenterLocRef` is a POI marker and no city carries it). Refused with a theme, `who`, `leash`, `reach`, any request knob or a third place. **On a board too since 2026-10-23** (missionboard05a, after city01a played): the board's borrowed second place is pinned instead of requested; see *Board*. Manual 12, *A named city as a place*; spike `2026-10-23-a-named-city-as-a-delve-place` |
| ~~`second.planet`~~ | ⛔ **GONE 2026-10-19, refused as an unknown key on any place.** A planet guard (`IsInSameCurrentLocationAsAlias` on the first site's map marker) crashed the game on start twice: 10a's behind a local site (2026-10-14) and 12a's `"same"` on a board (2026-10-19, at `startquest`). His ruling: *"Stop doing it"*, *"it's fine if the mission is on the same planet"*. A second request lands on whichever planet the planet content manager picks. |
| `reach` | optional, **written**: the Global every second and third place is held within, of the main place's map marker and of each other. Absent = `duo_longdrivedistance` (7,500). `duo_shortdrivedistance` (4,000) when the places must be close (08a, his 2026-10-13: the long drive spread a trail over *"most of the worldspace"*). Tighter = more slates find no fit and fall through to remote. Refused if it is not a Global, or the recipe has no second or third place |
| `leash` | **advisory, not written.** ⭐ **What IS written (2026-10-13, his "make all points within 7500"): every second and third place is held within `duo_longdrivedistance` (7,500) of the main place's map marker, and a third place also of a marker in the second, so all a Delve's POIs are within one drive of each other.** The main place keeps the base's own limit, `duo_samemapdistance` (20,000) from the player. The base's leashes take an ALIAS number (`UseAliases`), and before this a beat at the second place moved that anchor into the second place itself, so the leash measured from its own marker (six Delves) or from a removed alias (11a). `gen_aliaslint` R5 now names that shape. ⭐ **A leash anchor is a FILL DEPENDENCY (his ruling, 2026-10-15): the place is filled, and its leash read, in list order, so the alias ordering puts a place below the alias it measures from exactly as it puts a marker below its place, and a build where no order exists (an anchor filling inside the place it leashes) is REFUSED, never written.** Found on 18a, the first Delve whose second-place beat is a `recover`: its new marker is appended below the third place that must be held near it. |
| `who` (on `place`, `second` or `third`) | **who is at that place**, placed round one object there the first time the player comes within 250 m of it. `{ "kind", "list", "min", "max", "beat" }`. Absent = nobody (non-hostile POIs mostly spawn no one). Kinds: **`infested`**, ONE species from a creature list (`duo_GangMembersList_PreditorMegalist_A`..`G`), set to attack, within 20 m · **`workers`**, a friendly MIX from a `Civ_*` list (`duo_GangMembersList_Civ_Construx`, `_LIST`, `_Medics`, `_ArcMight`, `_DeimosMiner`, `_ArgosMiner`, `_UnitedTransport`, `_StrongArm`), within 12 m · **`spacers`**, a hostile MIX from a gang list (`duo_GangMembersList_Spacer`, `_Crimson`, `_Ecliptic`, `_Varuun`, `_Syndicate`...), within 20 m. `min` at least 1. `beat` (1-based, optional) names which pickup / use / deliver at that place they gather round; absent = the first one. **On a `board` mission too** (2026-10-15, duo_delve12a, his *"teach board who"*): there the hook is armed only after the accept (`entryStage` 10) and the stage is numbered after the arrival's (16 to 19, below the first beat's 20), because a board quest runs from the refresh and its site exists before anyone takes the job. ⛔ Refused: any other kind (`the dead` is designed, not built), a `beat` that is not an object at that place, and an object whose ending person already hooks its distance. **The words must agree:** a briefing that names a crew needs one here. The pay tier is still the recipe's (`reward`), never moved by this field |
| `residents` (main `place` only, `board` only) | **what happens to the site's OWN people.** `"dead"`: on the arrival stage, before its radio call, `duo_delve_lib.KillResidents` kills every human (`ActorTypeHuman`) within 200 m of step one's marker, silently (no kill event: no bounty, not the player's kill), never the player, his crew, the dead or an essential actor. Creatures carry no `ActorTypeHuman`, so a clear's predators are untouched. Absent = they stand. His 2026-10-26, missionboard31a: every site in its pool carries a resident tag, *"what about we kill the inhabitants when the first radio message plays"*. ⛔ Refused: any other value, a template without a board arrival, a `broadcast`-less recipe, and the field on `second`/`third`. ⚠ **Unproven:** whether the site's people are loaded at 250 m (`FindAllReferencesWithKeyword` sees only the loaded area). Distinct from `who`'s designed *the dead* (corpses PLACED round an object). **The words must agree:** a briefing about people waiting to go home is wrong over their bodies |
| `civilians` | **retired with delve4, and refused.** On a beats Delve write `approach.civilians` |
| `drop` | **space only, the test ladder's knob** (2026-10-22): base alias NAMES the build removes. Refused on any other template, refused for the four the beat stands on (the orbit, its cell, the marker, the object), and refused, writing nothing, if anything left still names a removed alias (a fill, a condition, a system or parent alias, a link, an objective, a script property, an `<Alias=X>` in the text). The one exception, printed as `-link`: the object's own link to a dropped alias, because the shipped object is a ship linked to `PatrolMarker01` and the build replaces the ship. Retires with the ladder |

**Where a place can be** (2026-10-13): the lint measures each place's pool across the POI pool PLUS every location carrying `LocDungeonBossLocRef`, because the marker conditions do the selecting and POIs carry the boss marker too (his, correcting bethesda part 32: "loads of pois have mapmarkers and boss markers"). A beat on `LocDungeonBossLocRef` (`duo_delve11a`) draws from both; vanilla's `MB_Bounty01Far` asks a place for that one marker and nothing else. ⚠ The boss marker is indoors only about 29% of the time.

**Useful keywords**, from the census (`gen_delve keywords [filter…]` tallies themes inside a narrowed
pool): `LocTypeOE_NonHostile` (nobody hostile, 62 POIs with a centre and map marker; ⚠ NOT people: his, 2026-10-11, *"LocTypeOE_NonHostile mostly dont spawn NPCs, only if there is a quest present at it"*, so a Delve that needs people places them) ·
`LocTypeOE_ThemeInfrastructureKeyword` (36 of those 62) · `LocTypeOE_ThemeNaturalKeyword` ·
`LocTypeOE_ThemeCaveKeyword`.

**`--request`** reads the places a request can put down (a board's or a remote place's pool) instead of the POI census,
and **`--list`** names each place left with its keywords and ref types (2026-10-23, Jessica's ask for the dungeon
split). The control: `gen_delve keywords --request LocDungeonOutsideEntranceMarkerLocRef LocDungeonCenterMarkerLocRef
LocDungeonItemMarker03LocRef MapMarkerRefType LocTypeDungeon` gives 37, missionboard04a's lint pool. The dungeon split
itself is in the Bethesda manual, part 32.

### `beats[]`

| field | what it is |
|---|---|
| `at` | the marker the beat sits on, a `LocationReferenceType`. The workhorses: `RECenterLocRef`, `RETravelA1LocRef`, `RETravelB1LocRef`, `REMarkerMediumLocRef`, `REContainerLocRef`. ⚠ Two beats on the same travel ring (A1/A2/A3 or B1/B2/B3) land 7 to 14 m apart |
| `ring` | **required on an object beat (`use`, `pickup`, `deliver`) whose `at` is a travel ring**, refused anywhere else. His rule, 2026-10-14: *"Items that are important to someone shouldn't be on travel ring. Infrastructure, Large cargo and hostiles can be on the travel ring"*. So it is `"infrastructure"` (sensors, reclaimers, beacons) or `"large-cargo"` (supply and wreck crates); fights and `recover` (a thief holds it) are the hostiles and need none. Anything that matters to someone goes on `REMarkerMediumLocRef`, and a second small one at the same place on `REMarkerSmallImportantLocRef` (his: for things on tables). The ring is outside and a median 70 m from the centre, so an object there reads as the POI's edge |
| `place` | `"main"` (default), `"second"`, or `"third"` |
| `objective` | the on-screen objective. Imperative, short (vanilla median 4 words, never past 12), no end punctuation |
| `journal` | the journal line this beat sets. ⚠ **The log shows only the newest stage's line:** on a Delve with an `offer`, the beat before the endings must carry none, because the offer's journal lands on the same stage in the same instant and overwrites it (refused); put what the player must read in `offer.journal` |
| `message` | optional pausing box: `{ "title": "...", "text": "..." }`, from the player's point of view. Plain ASCII |

**A model must sit on the ground.** The lint grades each against the vanilla Static that owns the mesh
and warns if it sinks, hangs or floats. A brand new kitbash has no Static to read, so check it by eye.
Shop furniture reads wrong standing alone in a POI (his: *"Prob be a chest"*). **Names must be new:** an
item or NPC name already in the game, or used by another recipe, is refused, so **copying a recipe means
renaming its things.**

### `offer`

```json
"offer": { "journal": "…the line the player reads after the find…",
           "message": { "title": "Incoming Message", "text": "…" } }
```

Only on a Delve whose last step is a `choose`. `message` or `say` (a radio line in the recipe's `speaker`'s
voice), never both: his "either or". The endings' pay and people ride each ending beat (`reward`,
`person`): see *Beats*.

---

## Two endings (`choose`)

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

**Every recipe has a letter**, even when it has no sibling yet.

**Naming (his rulings, 2026-10-26; supersedes "new numbers fill 01 and 02 first", 10-11).** A Delve is a new
FAMILY in the original mod's own grammar, so it sorts beside the classics in xEdit with no marker word:

| dealt from | name | Delve families start at | classic families |
|---|---|---|---|
| a mission board | `duo_MB<NN><x>_qst` | **40** | MB01 to MB31 |
| a slate, local | `duo_artifact_local_qst<NN><x>` | **20** | 01 to 17 |
| a slate, local cargo | `duo_artifact_localcargo_qst<NN><x>` | **20** | 01 to 09 |
| a slate, remote (SPACE) | `duo_artifact_remote_qst<NN><x>` | **20** | 03 to 14 |
| a slate, remote planet | `duo_artifact_remote_planet_qst<NN><x>` | **20** | 01 to 02 |

- **The gap is the point:** *"a clear numbering gap I can read and leans into the V2"*. Everything at or above
  40 (board) or 20 (slate) is new.
- **A family lives in ONE place** (his *"hard no"* to a family's letters being dealt from different nodes), so
  the scope in the name can never split a family.
- **The number is assigned when the forms are made, not when the recipe is written** (his: *"When I built
  before the numbers got assigned when I built so there was no gaps"*). A draft keeps its `duo_delve…` name
  until it is built. ⏳ The build step that hands out the next free number is not built yet; until it is,
  the number is chosen by hand at first build and the recipe file renamed to match.
- **Letters are the phase-two axis:** his plan is about ten lettered versions per family.
- Fragment scripts are `duo_qf_` plus the id **without** its own `duo_`, so the longest id fits the
  compiler's 38. Quests built before 2026-10-26 keep the fragment they were built with.
- The 2026-10-26 rename table (old `duo_delve…` to new) is in the office's Bethesda manual, part 04.

**Full recipes** (his ruling, 2026-10-08). `duo_delve07a`, `duo_delve07b`: same structure, new names
and words. `lint --all` compares siblings that share a number and names any drift in template, beats
(each beat's place, type, group, choose, `returnTo`, `replace`, a hold's numbers, and WHETHER it
carries a person, box, line or item, never their words), themes or reward tier (per ending), so a fix
made in `a` and forgotten in `b` shows up. Until 2026-10-11 the
beats and reward legs read only the choice kind's fields and compared nothing on a beats recipe
(Jessica's find). Siblings **may** share a
delivery model, and so may different missions (his, 2026-10-11: *"two missions sharing a model isnt really an issue"*). A variant that needs different structure is a
new number.

---

## What the lint checks

**Refused:** not JSON · an unknown key · an id that is not its file name · a template that does not
exist, or a template row of a retired kind · a field the template never reads · the wrong beat count or
shape for the template · a marker, keyword, outfit, company list,
NPC template, model or reward tier that does not exist where `du_overtime` can reach it (the mod and its
masters, never the CK bridge `du_overtime.esp`) · a place whose pool is zero · two `choose` endings that
can be the same POI · a token in a name · an item or NPC name already in the game, or used by two
recipes (each beat's `item` / `person.name`) · an empty message box · a journal the offer would overwrite · a `deal` that is not a Story Manager quest node in the template's own mod (the refusal lists the mod's nodes).

**Warned:** a first objective that names no place (his, 2026-10-11: *"the first objective name should
have a POI name in it. It helps to know what POI your looking for"*) · an object beat (a use, pickup or deliver that is not a `returnTo`) with no `model` or no
`name`, because absent means the base's boss crate and generic prompt (his, 2026-10-11: *"we dont really
want defaults"*) · a pool under 40 · a model that sinks, hangs or floats, or that no Static owns · an
objective over 12 words or ending in punctuation · any em dash or
non-ASCII character · two beats on one marker or one travel ring · a base exclusion the recipe drops ·
letter siblings that differ in structure · a `deal` pool that never dealt the template's base quest.

**An object beat on a container (2026-10-23, missionboard04a):** an object is created AT its marker, and some
location ref types tag a CONTAINER rather than a spot, so the object is created inside it (his play: *"the sealed box
was placed at the same position as the boss crate so you couldn't actually reach it"*). The lint counts what the
placed refs carrying the beat's `at` are, over every location record: all containers is **refused**
(`LocDungeonBossContainerLocRef`, 306 of 306), some is **warned**. In a dungeon use an item marker,
`LocDungeonItemMarker01LocRef` to `03LocRef`, his: *"used to put quest items in dungeons"*. ⚠ On a request the item
markers stand on about 39 of 214 places, which he reads as the real dungeons, so the narrow-pool warning there is
expected.

**The build adds, off disk:** every alias fills below what it fills from (the order the engine needs),
every place demands exactly its markers, the plugin's masters are unchanged, and every script property,
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
| `pickup` | activates it; gets `item`, the object vanishes | `DefaultAliasOnActivateGiveItem` | the object is disabled FIRST (the stock flag waited out the fragment, 2026-10-27), objective done, next shown |
| `deliver` | activates it after the earlier steps | `DefaultAliasOnActivate` | takes every item picked up or recovered since the last deliver |
| `talk` | talks to a `person` created at `at` on quest start: a `greeting`, then `exchanges` (`{ "prompt", "reply" }`, prompt 60 characters, reply 200, words only). `exchanges[0]` closes the talk and shows FIRST in the menu; the rest show under it in recipe order (scene `SCPI` 100, then 50, 49, 48..., proven at his glass 2026-10-14) and loop back to the menu. **An exchange may carry `"perk"`** (since 2026-10-14, his *"its a really cool RP feature"*): a vanilla perk EditorID (`BackgroundDiplomat`, `Skill_Medicine`, `TRAIT_Empath`, `FactionCrimsonFleetPerk`...), set on the player's line as vanilla's own `INFO.Perk`, which tags the choice [Diplomat] and offers it only to a player with the perk (1,027 vanilla lines do this, mostly with no condition; whether a perk-less player sees it hidden or greyed is unwatched). The lint refuses a perk on `exchanges[0]` (a perk-less player could not finish) and one that does not resolve, and warns on a perk outside the background / trait / skill / faction families | none: the closing reply's own `SetParentQuestStage` (on END) sets the beat's stage | objective done, next shown. **A talk that is the whole LAST step is a HAND-BACK** (since 2026-10-13, 11a): its stage is 100 and takes every item still carried, as a deliver does. The person's lines are voiced (`person.voice`, `person.elevenlabs`) with lipsync built in (bethesda 39). **Since 2026-10-19 (his):** every topic loops back to the menu, so the side questions stay askable after `exchanges[0]`; a talk in the LAST step keeps the quest running after it completes, until the person unloads (stage 1000, `DefaultAliasOnUnload`). **`person.hellos`** (optional, 1 to 4 lines, each a list of responses like every spoken field, 200 characters a response, no tokens, Jessica's: `[["…"], ["…", "…"]]`): what he says as the player WALKS UP, one picked each time, in his voice, built as vanilla's Hello shape (MQ101's Lin: one Hello topic, a group gated `GetIsID(him)`, the lines under it). ⭐ **The group carries the record-header flag `InfoGroup`, and that is what makes the line play** (2026-10-23, his play of 14a: his own line, subtitle and audio): without it the engine falls back to a silent "Captain". The verify reads the header back. ⚠ Vanilla's created-speaker shape (gated `GetIsAliasRef(his alias)`, group flags 768, lines 2) was built 2026-10-22 (`ca77155`) and played no better WITHOUT the header; with it, it is unplayed, so a build does not write it. 19a still carries it, header-less, until its next rebuild. Refused on an ending's person. A talk Delve's quest runs at priority 50 (above vanilla's ambient greeting quests, 30 and 45), and a cloned person carries no faction, so vanilla's walk-up lines no longer match him: research in home-office `office/spikes/2026-10-19-delve-greetings.md` |
| `recover` | takes `item` off whoever has it (loot, pick up, be handed it: all count) | `DefaultAliasOnItemAddedScript` on the PLAYER (A-D duplicates for a second to fifth) | on ENTERING the step: the holder and a gang (0 to the base driver's max, from its own list) spawn at `at`, the holder into an empty alias the objective follows |
| `craft` | MAKES `item` at a bench, wherever he is (no `at`, no `place`): `craft: { "bench": "industrial", "ingredients": [["Aluminum", 4], ...] }`, each ingredient a MiscItem EditorID or unique in-game name in the plugin or a master | `DefaultAliasOnItemAddedScript` on the PLAYER, filtered to the item (recover's hook; the five-copy limit counts both) | none at entry; the build adds a COBJ cloned off his `duo_co_MB11a`, live from the step's start until its own stage (two `GetStageDone` gates, never `GetStage`, because a board's arrival stage moves the current stage while the step still shows). The objective has no target. A board's arrival waits on the first beat that HAS a place. His 2026-10-26 Manufacturing ask, missionboard34a. ⚠ Unproven when built: that OnItemAdded fires on a craft (it does on a pickup); his MB11 to MB13 instead finish on the item put into a container at the site |
| `hold` | fights off `waves` waves at `at`, each arriving when the last is down | `DefaultCollectionAliasOnDeath` on each wave's own empty collection alias (a copy of vanilla's UC08 `ActiveHostiles`), `TurnOffStageDone` its own stage | on ENTERING the step and on each wave's stage: the next wave (`size` [min, max], from the base driver's gang list) spawns 30 to 100 m from the player on any bearing outside the 45-degree cone he faces (else at `at` when he is more than 150 m from it); a `horde` wave is split between his left and his right (see `style`) INTO its collection, which wears a Travel-to-player package, the counter ticks, and the spawning fragment waits out the reveal and the stuck guard (`duo_delve_lib.SpawnWave`, `Utility.Wait`) |
| `defeat` | kills the `target` (an Npc or LeveledNpc), who appears at `at` when the player comes within 250 of it, with an optional `gang` (`list`, `min`, `max`, `species` `one`/`mix`) who need not die (the Bounty driver's gameplay, `dou_artifact_ground_bounty_quest`) | `DefaultAliasOnDeath` on an EMPTY Optional alias the target is placed into | **NEAR, as a clear (below, since 2026-10-22):** the SPAWNED stage's fragment calls `duo_delve_lib.SpawnTarget`; the objective is on the marker until then, on the target after. Before that day the target spawned when the step began; a defeat built earlier does so until it is rebuilt |
| `clear` | kills every one of `enemies` (`list`, `min`, `max`, `species`: `one` picks a single entry for all, the Hunt driver's way; `mix` draws per enemy), who appear at `at` when the player comes within 250 of it and hold the site (no package) | `DefaultCollectionAliasOnDeath` on an EMPTY collection (a copy of vanilla's UC08 `ActiveHostiles`), `TurnOffStageDone` its own stage | **NEAR:** `duo_delve_near` (our copy of the stock `DefaultAliasOnDistanceLessThan`, since an alias carries a script once and a board's step-one marker already wears the stock one for its arrival) on the spawn marker, armed by the step's stage, sets the beat's SPAWNED stage (900 + the beat's index) when he is within 250, and THAT stage's fragment calls `duo_delve_lib.SpawnGroup` (or `DropPods`). The objective is on the group AND on the spawn marker, the marker's target lit only while the spawned stage is not done: the site is on his map before they exist and gone once they do (his *"marker stays as a visable objective"* when it was ungated). WHY NEAR, 2026-10-21, his *"The spacers are spawned from a distance, which reads bad on the map and puts them in mid air. Spawn them when the player gets close"*. WHY THE ENGINE'S EVENT, 2026-10-22: it replaced a script that checked once a second (`duo_delve_groupnear`, retired), his *"that won't scale very well"*. ⚠ The event measures in 3D where that script measured flat. ⚠ Unmeasured: whether it fires at once if he is already inside 250 when the step begins. A clear built before 10-22 carries the retired script until it is rebuilt |

**The fight beats, `defeat` and `clear`** (2026-10-14, his scope: *"I want our delve system to be able to make the GAMEPLAY of these quests"*, Overtime's remote Bounty and Hunt; spike `office/spikes/2026-10-14-defeat-and-clear-beats.md` in the home office). Every list is NAMED IN THE RECIPE (his ruling), resolved own-or-master and refused if missing or empty. Both take an optional `loot` (`list`, `chance` 1-100): each enemy spawned, the target too, carries one entry with that chance (the drivers: `duo_ArtifactList_Local`, 25). Suspicious 2 and Aggression 2, the shipped drivers' values. A fight has no object of its own, so `model`, `name`, `replace`, `item`, `returnTo`, `group` and `choose` are refused on it, and an `approach` or a `defend` cannot point at one. ⚠ **No stuck guard** (a `hold` has one): a fight can begin at quest start, before he has even flown there, so a timer would be meaningless; an enemy spawned out of reach would hold the step.

**A clear's enemies by DROP POD, `enemies.pods`** (2026-10-21, his *"clear sounds good, let's do 3 per pod and 3 pods in
different points to start with"*; first on `duo_delve33a`, not yet played). `{ "count", "per" }` on a clear's `enemies`:
`count` pods (1 to 4) land round the spawn marker at even bearings, 15 to 25 out, one after another 2 to 4 s apart at
random (his *"randomly staggered by 2-4 seconds so they don't all land at once"*), and `per` (1 to 6) step out of each, so
the pods set the number and `min` / `max` are refused beside them; refused on a defeat's gang. When he comes near, the
spawned stage's fragment calls `duo_delve_lib.DropPods` in place of `SpawnGroup` (since 2026-10-22; before it,
`duo_delve_groupnear` carried the pod count), which runs the arrival `duo_podprobe` proved at his glass: the pod hung by
`AttachTo` from an invisible `StarbornRingSpin00` (a placed static moves its position and not its mesh), the UC04
distant explosion as it appears (his *"a clear winner"*), 400 up at 200 (2 s), a frag at touchdown, the crew 0.3 s
later. **The pod is Kim's hero model since 2026-10-21** (was vanilla `Nasa_GeminiCapsule01`): `duo_DropPod01`
[STAT `003445`], `Meshes\ccs\droppod01.nif`, built from Overtime `kitbash/droppod01.json`, 7.6 x 6.4 x 11.8 m with its
origin 5.196 m above its lowest vertices (the two blade tips); pod and helper turned so its door (-Y) faces the spawn
marker, and the crew placed within 1.5 of a navmesh point 4.5 in front of the door, never round the pod's middle
(inside her). **His first play of her hovered at shoulder height and leaned forward** while the helper read 0 off and
the crew stood on the ground, so pod and helper are now placed DISABLED, posed, enabled, and attached after
`WaitFor3DLoad`, and **at touchdown the falling pod is swapped for a fresh one placed at the rest pose** (the ground
marker, 3.2 up, turned to its bearing); the falling pod and helper are deleted under the frag. **His second play, it
stood on its blade tips: *"another like metre down so the heat shield is just near the ground"*, so 3.2 puts the
shield (2.95 below the origin) 0.25 off the ground and the blades 2.0 into it** (was 5.1). The Gemini was solid
where it landed and kept on the ground by a save and reload, his checks; ⚠ **solidity unmeasured on Kim's pod**, whose
solid parts are ship modules. Home-office manual part 32, *how a DROP POD comes down*.

**A team who lands by ship, `support`** (2026-10-21, his *"build a new delve mission with the landings"*; first on
`duo_delve33a`, not yet played). Optional on a `defeat` or a `clear`: `{ "list", "min", "max", "ship", "land", "near" }`.
`list` is the team (a FormList, own-or-master, e.g. `duo_GangMembersList_UC`), `ship` a leveled ship (a
`GenericBaseForm`, e.g. `LvlShip_UCSecurity_Transport`, the record type his landed bounties bind), `land` the marker at
the beat's place it comes down on (demanded of the place like a beat's own; a travel ring is open ground, and the lint
warns when it is the fight's own marker), `near` the flat call distance (100 to 1000, absent 300). The step's fragment
arms `papyrus/duo_delve_landing.psc` on a new landing-marker alias; when the player comes within `near`, a pad is
chosen (below) and `PlaceShipAtMe` puts the ship on it, which comes down from orbit by itself (his fact: his landed
bounties, qst12a to qst16d). On `IsLanded` its ramps open; on `IsRampDown` (or 20 s) the team is placed INTO an empty
collection (a copy of `ActiveHostiles`) **at the ship's exterior load door, so they come down the ramp**. A team at that
door leaves only when the ramp meets the ground (his play: *"unless the ramp is perfectly aligned with the floor the
people can[not] pathfind off the ship"*), which on open ground it did not, so from 11:21 to 16:4x they stood in a ring
round the hull instead; a hand-placed pad is flat, so with open ground gone the door is back (his *"we can bring back
the landing ramp"*, 2026-10-21, unplayed on a pad). A ship that never lands, or has no exterior load door, puts its team
on the pad. (The ship's deck markers were tried first and `GetExteriorRefs(Ship_LandDeck_Marker_RefType)` found 0 on
the UC A-class transport, one play.)
⭐ **The collection wears the
team's OWN package, `<id>_supportpkg_b<n>`**: a copy of the travel package aimed near the beat's marker alias (where the
enemies or the target spawn) by vanilla's own shape for a landed crew (`OE_KT_LandedShip_TravelPKG`: `LocationFallback`
`AliasForReference`), at Run, with no condition, **and each Marine LEAVES the collection as he comes within 20 of that
marker** (`duo_delve_landing`, everyone left after 180 s), so the Travel stops and his own AI takes over (the Marines are
`Aggressive`, `Brave`, on `DefaultMasterPackageList_Human`). His plays, 2026-10-21: placed with no package they *"hung out
in the landing bay"*; on the waves' run-to-player package they came off but *"didn't really attack the Spacers"*, and his
ruling: *"For enemy landings this is prob right but friendlys should attack the objectives"*; on this Travel *"they got out
and fought"* but then *"huddle there doing nothing"* at the marker; a copy of vanilla's `OE_Sandbox_20m_Run_Alert_Package`
in its place left them *"very chill, don't seem to want to engage in comabt"* (one play), so the Travel with the release,
his *"Maybe even just stopping the travel package"*. ⚠ A package's location `Radius` holds a FLOAT's bits in Mutagen's
field (1101004800 is 20.0; vanilla's 3.0, 10.0, 60.0 read the same). The ship takes off once its
landing deck is clear (`IsLandingDeckClear`, 5 to 60 s) and both refs are deleted 30 s later. Nothing sets aggression.
Refused on a board (not built for it yet). ⭐ **Where it comes down, PAD OR SQUAD, his ruling 2026-10-21 16:4x:** *"let's just do Pad
or Squad. Safe Position isn't really helping us."* (1) **Pad:** the place's OWN pad, the closest hand-placed landing marker
within 250 of `land` (five furniture bases carry every one in his load order, `gen_delve bases`), skipping one his ship
sits on; it is never deleted. (2) **Squad:** else no ship, and the team is placed on `land` and runs in on its package.
Drop pods (`enemies.pods`, above) are the middle ground now. Vanilla never searches for a landing spot (every NPC
landing is on a hand-placed marker). ⛔ **The open-ground rung is retired, measured first:** a clear spot from
`GetSafePosition(100, r)` at r 40, 25 and 15 cost 3.1 to 3.6, 1.7 to 1.9 and 0.7 to 0.9 s on a world with none (about
6 s of stutter, his *"a lot of slow down"*), and r 40 and 25 found nothing in 4 of 4 (home-office manual 09). Probe-only
notifications name each path and come out when it graduates. **`duo_delve_landing.pex` must be packed with the mod.**

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
"broadcast": ["This is Relay Control.", "Second response, said straight after. ..."],
```

and `"say": ["..."]` on any beat (schema 2: a list of responses, 1 to 4). **Which voice: [voices.md](voices.md)**, the described list. `broadcast` plays when the quest starts; a beat's `say` plays when that beat's
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
- **The call waits for him at the site.** A board mission is a posting (his 2026-10-14: *"You shouldn't rechieve radio
  messages just after picking up a missionboard mission"*), so `broadcast` plays on `arrivalStage` 15, set by a stock
  distance hook on step one's marker within 250 m of the player, armed only after the accept. The base has no player
  alias, so the slate base's (32) is copied in.
- **Every link to the base is remapped**, conditions included: MB28a's stage 100 chooses its completing entry on
  `GetStageDone(<itself>, 10)`, and a clone still testing MB28a never finished (his first play).
- **It pays through the board.** The last stage waits for its radio line and calls `MissionComplete()`
  (vanilla's own stage-100 fragment, autocast and all), never `CompleteQuest()` and `Stop()`. A beat's
  `reward` tier sets the script's `RewardAmountGlobal` and its `Actual`, which the card shows; never the
  stage's QRCR, or it pays twice. No tier keeps the base's (`duo_reward_creds_easy`).
- **The place is always a request.** MB28a's `TargetLocation` is one already, so `place.remote` is refused;
  the lint measures the pool against what a request can place, as for a remote Delve, with no parsec limit
  (the board picks the system).
- **`PrimaryRef` is beat 1's marker.** The board's system and planet aliases resolve FROM it, so it stays,
  its ref type rewritten to the first fight's `at`; its base death hook goes. The driver's `EnemyType` goes.
- **Borrowed from the slate base** (`borrowFrom`, `borrowAliases`; missionboard02a, 2026-10-14): MB28a has no player,
  no object slot and no second place, so the slate base's are copied in renumbered (player 32; slots 1/2 and 11/12 as
  20/21 and 23/24; second place 10 as 22), every link inside remapped and anything unmapped refused. A pickup clones
  `itemSource` (the board driver carries no cargo). So `use`, `pickup`, `deliver` and `talk` build beside the fights;
  `recover` and `choose` are still refused. A talk opens on the entry stage (10), never stage 0, which a board never sets.
- **`PrimaryRef` never demands a marker no beat named.** It is a fight's marker when step one is a fight, and otherwise
  fills from the place's map marker (every requested site has one); verify reads which.
- **A second place is a request or a named city** (`place.second.remote: true`, or `place.second.settlement`; a found
  one is refused, nothing would hold it near the first). A request is a copy of the board's own with NO planet guard,
  so it lands on either planet, and gets its own planet alias (`secondPlanet`, parent the second place) for
  `<SecondPlanet>`. Two requests are proven (duo_delve_twosite01a, two POIs on two planets). ⛔ The guard it used to
  carry (vanilla MB_Cargo's shape) is gone: two planet guards crashed the game on start (10a, 12a), his 2026-10-19
  ruling, and the verify now refuses one. A city (2026-10-23, missionboard05a) is the borrowed second place pinned,
  exactly as on a slate, and gets no `<SecondPlanet>`. ⚠ **Its person stands there from the board's refresh:** a
  talk's person is created on quest start, and a board quest starts when the board refreshes, before anyone takes the
  job (her conversation still waits for stage 10). Vanilla's twin, `RQ_OE_Deliver_01`, creates its recipient
  `InitiallyDisabled`; not built, parked on his ruling (2026-10-23, after 05a played): *"super edgecase, we'll look
  into it if it ever happens"*.
- **A `who` builds here too** (2026-10-15, duo_delve12a): numbered after the arrival (16 to 19) and armed only after the
  accept, so nothing stands at a posting's site until the job is taken. An `approach` and an ending's person are still
  refused (`cannot` in the row).
- **Verify, every Delve:** every alias link (fills from, created at, parent) resolves to an alias the quest has.
- **`deal` names a board node** (`_duo_MB_MissionNode_test` for the first). The proof recipe is
  `duo_delve_missionboard01a`, remote05a's gameplay; ids take the form `duo_delve_missionboardNNa` (his).

## Space: a Delve whose one place is an orbit (the test ladder's harness)

His 2026-10-22, after 19a's local + remote + orbit would not start: *"we faffed about trying to get a local, remote
and space all working at the same time. Instead lets take a backup then make a series of tests for remote and space
alias sets. Idea being we learn in isolation how to build these sets"*, then *"A"*: teach this tool an orbit-only
Delve. The `space` template's shell is `duo_artifact_remote_qst03a` whole. All 62 of his remote-space quests (and his
MB01) carry one set: `PlayerStarSystemLocation` 0, `TargetPlanetLocation` 2 (the orbit, by type `LocTypeOrbit`), the
`SpaceCellRefs` collection 10 created at it, `SpawnMarker01` 12 and `PatrolMarker01` 17 from the cell, `PrimaryRef` 3
created at 12, `SpaceMapMarker` 16 from the cell, `TargetSystemLocation` 8, the `StartingQuestTargetOrbital`
collection 14 created at 8 and `StartingQuestTarget` 15 from it, `PlayerShip` 9. `place.third.orbit` (19a) copies
FOUR of them (2, 10, 12, 16).

The build (`delve_space.cs`) changes only what a test needs: the driver comes off, `PrimaryRef` creates a clone of
`activatorFrom` reskinned to the beat's model (the shipped target is a ship), it wears `DefaultAliasOnActivate`
setting 100, objective 10 is the beat's, stage 0 runs on start and a fragment shows the objective and completes on
100. The shell carries no start event (`ShellOf` keeps none), so it starts with `startquest` like every Delve. Every
later rung takes pieces away with `place.drop`; `place.parsecs` sets the orbit's limit to a number.

The rungs, `duo_delvetest_s1` to `s6`: **S1** the set whole · **S2** minus the radiant pair (14, 15) · **S3** minus
`TargetSystemLocation` too · **S4** minus `PatrolMarker01` too · **S5** minus `PlayerStarSystemLocation` and
`PlayerShip` too, which leaves 19a's four and the object · **S6** S1 at 1 parsec. The remote ladder is `r1` to `r4`
on the `beats` template (one request; `parsecs` 1; `habitability` off; both). Results and the exit condition (this
template graduates into a real one or comes out) are in home-office `office/spikes/2026-10-22-alias-set-ladders.md`.

## Not yet

- ~~**An entry item.**~~ Every Delve is dealt from a dataslate pool by its `deal` (2026-10-12); `startquest` still works for a test.
- ~~**A third place outside the choice template**~~ (the beats template creates one), and `choose` with
  more than two endings.
- ~~**Dialogue.**~~ The `talk` beat (2026-10-12). Choose endings' people still stand and do not talk.
