# PCM_* creature NPCs — biome-swap targets, not spawnable

*Salvaged 2026-07-07 from the stranded harness store (`pcm_creature_npcs.md`). High-value
confirmed-in-game findings — kept intact. **→ graduate**: the spawnable-creature pattern belongs in
`docs/formlib/placed_npc.md`. The `[[mutagen-alpha-upgrade]]` link at the end pointed at another harness
memory that did not travel; treat it as historical.*

**TL;DR:** `PCM_*_Predator/Prey/Critter` NPCs are biome-marker swap targets, **NOT spawnable**. To spawn
a creature, clone a concrete `_Enc<Species>*_Template` instead.

## PCM_* Npc convention (946 records in Starfield.esm)

```
PCM_<StarSystem>_<Planet|Moon>_<Role><N>[_<Modifier>]
```

- **Roles**: `Predator`, `Prey`, `Critter`
- **Modifiers**: `_Swimmer`, `_Flyer`, `_Floater` (others likely)
- Examples: `PCM_Nemeria_Nemeria-II_Predator01`, `PCM_Alpha-Centauri_Gagarin_Prey03_Swimmer`

## PCM_*_Predator NPCs are NOT spawnable — confirmed in-game

`placeatme` on a PCM_*_Predator NPC fails silently — no model renders. They sit at template-chain level 2 with `DefaultTemplate → CCT_Creature [0x2AD4E3]`, and CCT_Creature is abstract (no mesh, no animations). The Bethesda wiki (`C:\modding\starfielddocs\starfield\7__ World Building_Biome Markers__WebHome.md` and `PCM Block Creation Request`) confirms PCM_ NPCs are **biome-marker swap targets** resolved at landing time, not direct spawns. The runtime chain is:

```
Cell contains BiomeMarker_Predator_NoLair_Solitary (primitive 3D placeholder)
  → at landing, biome applied
  → biome's Marker Objects tab matches MarkerType + BiomeTypeX keywords
  → engine swaps marker for an NPC with matching keywords
```

PCM_*_Predator NPCs only have meaning inside this swap pipeline.

## To make a spawnable creature, clone a concrete `_Enc<Species>*_Template`

These sit at template-chain level 2 with real mesh/AI/animations. Confirmed-working list (Starfield.esm only, no DLC):

```
LC116_EncCataxiRanged00_Template
LC116_EncCataxiMelee00_Template
LC030_EncGryllobaRanged00_Template
LC030_EncGryllobaMelee00_Template
LC030_EncGryllobaQueen00_Template
LC030_EncCotylite00_Template
SEDerelict_EncHexapodAGlider00_Template
```

SFBGS001_* templates exist but are in ShatteredSpace.esm — using them forces a DLC master.

## Skin/WNAM is NOT the appearance switch

`015B19:Starfield.esm` (Skin_OctopedeA_ScorpionNoTailA) is shared by ~99% of all PCM_* NPCs AND every concrete `_Enc*_Template`. The Skin armor has only 1 armature (one Octopede mesh) yet Grylloba/Cataxi/Hexapod look completely different in-game. So the visual is generated procedurally at runtime via the template chain (CCT) and likely the `Properties` field, not the Skin record. Copying Skin between NPCs is a no-op.

## Pattern: PredatorHuntTarget

`Retrograde.Library/Nouns/Hunt/PredatorHuntTarget.cs` uses the PCM_*_<planet>_Predator regex only as a **planet-validation gate** (does this planet have predators in the PCM tree?), then clones a concrete species template via `NPCTools.CloneNPC` and overrides Name/EditorID. Confirmed in-game with `player.placeatme` on `npc_huntpredator_nemeriaii_thefrostscourge`.

Test command: `dotnet run --project c:/Git/FrankyCLI/FrankyCLI.csproj -- gen_hunttest` → writes `hunttest.esm` to the Starfield data folder with 5 predator hunt NPCs.

**Open follow-ups for issue #42:**
- Boss treatment (Level bump, MajorFlags Unique, LocDungeonBoss keywords at *placement* time per `formlib/placed_npc.md`)
- Biome-correct species pick (currently random from the 7-template pool — losing planet/biome correctness)
- PlacedNpc on the target planet (placement scaffolding still needed)
- Mission board wiring

Related: [[mutagen-alpha-upgrade]] (gen_inspect now reads Race + Biome on 0.54.0-alpha.89).
