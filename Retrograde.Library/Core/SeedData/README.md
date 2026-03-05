# SeedData

Static data lists and random-selection helpers used during procedural generation.
No Mutagen record operations — everything here is pure data or string output.

| File | What it seeds |
|------|---------------|
| `ActivatorSeedData` | Space and ground activator types (name + model path). Used by Investigation quest steps to pick the objective object. |
| `ArmourSeedData` | Vanilla armour FormIDs (helmets, packs, spacesuits) for random loot and NPC equipment. |
| `FactionSeedData` | Starfield faction names for random selection. |
| `FlavourSeedData` | AI prompt wrappers that inject tone and style into generated book/log text. |
| `GangSeedData` | Street gang name parts (prefixes, suffixes, roles) for `StreetGang` and `NamedStreetGang`. |
| `NameSeedData` | First/last name lists split by gender; `GenerateName(female)` picks a full name. |
| `NarrativeSeedData` | Tones, log focus points, transmission types, and speaker types for AI prompt variation. |
| `NpcSeedData` | Character trait lists (upbringings, fears, goals, flaws, habits, nationalities) with convenience getters. |
| `ShipSeedData` | Ship FormIDs by class (A/B/Cargo), faction ship name resolvers, and codename generators per faction. |
| `StorySeedData` | High-level story seed lists (occupations, crimes, motives, personality traits) used by `LorePrompts.GenerateLoreFile`. |
| `VoiceSeedData` | ElevenLabs voice IDs and display names split by gender, used when assigning voices to NPCs. |
