using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde.FactionMembers;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI.Retrograde.Passes
{
    /// <summary>
    /// Event pass that spawns a unique bounty target NPC - a mini-boss with
    /// a unique named armor piece as special loot. The bounty is placed far
    /// from the dungeon start for an end-game encounter.
    /// </summary>
    public class BountyTargetEventPass : IGenPass
    {
        // Faction-specific bounty name pools (25 per faction)
        private static readonly Dictionary<string, string[]> FactionBountyNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Spacer"] = new[]
            {
                "Vex the Ruthless", "Iron Maw", "The Collector", "Deadlock", "Hollow Point",
                "Scar", "The Butcher", "Rust Devil", "Void Rat", "Junkyard King",
                "Scrapper", "The Vagrant", "Gutter Snake", "Debris", "Hull Breach",
                "Rotten Luck", "The Scavenger", "Trash Heap", "Crater Face", "Derelict",
                "Freebooter", "Salvage King", "The Drifter", "Wreck", "Rust Lung"
            },
            ["Crimsonfleet"] = new[]
            {
                "Crimson Fang", "Black Sun", "The Red Wake", "Bloodtide", "Captain Vicious",
                "The Plunderer", "Corsair Queen", "Dread Mako", "Scourge of Kryx", "The Keelhauler",
                "Blackbeard's Ghost", "The Privateer", "Red Handed", "Cutthroat", "The Buccaneer",
                "Siren's Call", "Jolly Reckoning", "The Marauder", "Scarlet Tide", "Broadside",
                "The Raider", "Blood Bounty", "Crimson Debt", "Hull Splitter", "The Reaver"
            },
            ["Ecliptic"] = new[]
            {
                "Ghost of Akila", "Neon Viper", "The Contractor", "Coldshot", "Iron Sigil",
                "The Professional", "Nightfall", "Credit Hunter", "The Eraser", "Silent Ledger",
                "The Asset", "Clean Sweep", "Hostile Takeover", "The Closer", "Blackout",
                "Dead Drop", "The Operative", "No Witness", "The Liquidator", "Final Notice",
                "The Consultant", "Zero Trace", "The Specialist", "Cold Contract", "The Fixer"
            },
            ["Varuun"] = new[]
            {
                "The Void Walker", "Prophet of Ruin", "Serpent's Chosen", "The Zealot", "Starborn Heretic",
                "Voice of the Deep", "The Annihilator", "Grav Storm", "The Fanatic", "Coil Priest",
                "Serpent's Maw", "The Penitent", "Void Touched", "The Apostle", "Star Eater",
                "The Devoted", "Gravity's Wrath", "The Ascendant", "Cosmic Fury", "The Believer",
                "Dark Pilgrim", "The Ordained", "Void Prophet", "The Purifier", "Serpent's Voice"
            }
        };

        // Fallback names for unknown factions
        private static readonly string[] DefaultBountyNames =
        {
            "The Outlaw",
            "Wanted Dead",
            "The Fugitive",
            "Marked One",
            "The Hunted"
        };

        // Ensures only one bounty target is placed per pass instance
        private bool _hasPlaced;

        // The created bounty target NPC
        private Npc _bountyTarget;

        // The unique armor created for this bounty
        private Armor _bountyLoot;

        // The selected bounty name
        private string _bountyName;

        /// <summary>
        /// Gets a random bounty name appropriate for the given faction.
        /// </summary>
        private static string GetBountyName(string faction)
        {
            if (!string.IsNullOrWhiteSpace(faction) &&
                FactionBountyNames.TryGetValue(faction, out var names) &&
                names.Length > 0)
            {
                return names[RandomUtils.random.Next(names.Length)];
            }

            return DefaultBountyNames[RandomUtils.random.Next(DefaultBountyNames.Length)];
        }

        public void RunPass(DungeonState state)
        {
            if (_hasPlaced)
                return; // Only place one bounty target

            if (state?.placedRooms == null || state.placedRooms.Count == 0)
                return;

            // Step 1: Select a faction-appropriate bounty name
            _bountyName = GetBountyName(state.Faction);

            // Step 2: Create unique armor with bounty's name
            _bountyLoot = CreateBountyArmor(_bountyName);
            if (_bountyLoot == null)
                return;

            // Step 3: Find spawn location (far from start)
            var spawnInfo = SelectBountySpawnLocation(state);
            if (spawnInfo == null)
                return;

            // Step 4: Create bounty target NPC
            _bountyTarget = CreateBountyTarget(state.FactionCrew, _bountyName);
            if (_bountyTarget == null)
                return;

            // Step 5: Add legendary armor to NPC inventory
            AddBountyLoot(_bountyTarget, _bountyLoot);

            // Step 6: Place the NPC
            var worldPos = CalculateWorldPosition(spawnInfo.Value.Room, spawnInfo.Value.Marker);
            var worldRot = spawnInfo.Value.Marker.Rotation;

            state.PlacementUtil.NPCAddToTemporary(state.instance, new PlacedNpc(gen_quest_main.myMod)
            {
                Rotation = worldRot,
                Position = worldPos,
                Base = _bountyTarget.ToLink<INpcGetter>()
            });

            _hasPlaced = true;

            if (!state.IsHarnessRun)
            {
                var roomName = spawnInfo.Value.Room.Prefab?.PrefabEditorId ?? "unknown";
                Console.WriteLine($"[BountyTarget] Placed bounty '{_bountyName}' with unique armor in room '{roomName}'");
            }
        }

        /// <summary>
        /// Creates a unique armor piece for the bounty target.
        /// </summary>
        private Armor CreateBountyArmor(string bountyName)
        {
            // Get a random base armor from Starfield.esm (helmet, pack, or spacesuit)
            int armorType = RandomUtils.random.Next(100);
            uint armorId = armorType < 33 ? ArmourTools.GetRandomHelmet()
                         : armorType < 66 ? ArmourTools.GetRandomPack()
                         : ArmourTools.GetRandomSpacesuit();
            var baseArmor = gen_quest_main._StarfieldMod.Armors[new FormKey(gen_quest_main.StarfieldModKey, armorId)].DeepCopy();

            if (baseArmor == null)
                return null;

            var guid = Guid.NewGuid().ToString().Substring(0, 8);

            var armor = new Armor(gen_quest_main.myMod, $"rg_bounty_armor_{guid}")
            {
                ObjectBounds = baseArmor.ObjectBounds,
                Transforms = baseArmor.Transforms,
                Name = $"{bountyName}'s {baseArmor.Name}",
                WorldModel = baseArmor.WorldModel,
                PickupSound = baseArmor.PickupSound,
                FirstPersonFlags = baseArmor.FirstPersonFlags,
                ArmorRating = (ushort)(baseArmor.ArmorRating + 25),
                Armatures = baseArmor.Armatures,
                Components = baseArmor.Components,
                Description = $"Armor worn by the infamous {bountyName}. Claimed as a trophy.",
                Health = baseArmor.Health,
                ObjectTemplates = baseArmor.ObjectTemplates,
                AttachParentSlots = baseArmor.AttachParentSlots,
                Footstep = baseArmor.Footstep,
                DropdownSound = baseArmor.DropdownSound,
                Keywords = baseArmor.Keywords,
                Resistances = baseArmor.Resistances,
                ObjectEffect = baseArmor.ObjectEffect,
                Voice = baseArmor.Voice,
                Value = baseArmor.Value * 2,
                Weight = baseArmor.Weight,
                Race = baseArmor.Race,
            };

            gen_quest_main.myMod.Armors.Add(armor);
            return armor;
        }

        /// <summary>
        /// Creates the bounty target NPC using the faction's boss template.
        /// </summary>
        private Npc CreateBountyTarget(IFactionMembers factionCrew, string bountyName)
        {
            if (factionCrew == null)
                return null;

            // Get a boss-tier NPC for the bounty target
            var npc = factionCrew.GetBoss("district");
            if (npc == null)
                return null;

            // Override the name with the bounty name
            npc.Name = bountyName;

            // Update EditorID to match
            var sanitizedName = bountyName.Replace(" ", "").Replace("'", "").ToLower();
            npc.EditorID = $"rg_bounty_{sanitizedName}";

            return npc;
        }

        /// <summary>
        /// Adds the armor to the NPC's inventory.
        /// </summary>
        private static void AddBountyLoot(Npc npc, Armor armor)
        {
            if (npc.Items == null)
                npc.Items = new ExtendedList<ContainerEntry>();

            npc.Items.Add(new ContainerEntry
            {
                Item = new ContainerItem
                {
                    Item = armor.ToLink<IItemGetter>(),
                    Count = 1
                }
            });
        }

        /// <summary>
        /// Selects a spawn location for the bounty target.
        /// Prefers locations far from the start for an end-game encounter.
        /// </summary>
        private SpawnInfo? SelectBountySpawnLocation(DungeonState state)
        {
            var candidates = new List<SpawnInfo>();

            foreach (var room in state.placedRooms)
            {
                // Skip rooms without markers
                if (room.Prefab?.Markers == null || room.Prefab.Markers.Count == 0)
                    continue;

                // Skip boss rooms - bounty target shouldn't compete with the main boss
                if (!string.IsNullOrEmpty(room.DistrictType) &&
                    room.DistrictType.Contains("boss", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Find enemy spawn markers in this room
                foreach (var marker in room.Prefab.Markers)
                {
                    var id = marker.MarkerEditorId;
                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    if (!id.StartsWith("rg_enemy_spawn", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Calculate score: prefer rooms FAR from start
                    float distanceFromStart = (float)Math.Sqrt(MathUtil.DistanceSquared(room.WorldPos, state.StartingPosition));
                    float score = distanceFromStart; // Higher distance = higher score

                    candidates.Add(new SpawnInfo
                    {
                        Room = room,
                        Marker = marker,
                        Score = score
                    });
                }
            }

            if (candidates.Count == 0)
                return null;

            // Sort by score (highest first - farthest from start)
            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

            // Pick from top 3 candidates randomly for variety
            int pickRange = Math.Min(3, candidates.Count);
            return candidates[RandomUtils.random.Next(pickRange)];
        }

        private static P3Float CalculateWorldPosition(PlacedRoom room, PrefabMarker marker)
        {
            var rotatedLocal = RgRotation.RotateYaw90(marker.Position, room.YawSteps);
            return room.WorldPos + rotatedLocal;
        }

        private struct SpawnInfo
        {
            public PlacedRoom Room;
            public PrefabMarker Marker;
            public float Score;
        }
    }
}
