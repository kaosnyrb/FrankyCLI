using FrankyCLI.questgen_tools;
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
    /// Event pass that spawns an alien infection in the dungeon:
    /// - Several pests (Heatleech/Xenogrub) scattered across the map
    /// - A Terrormorph miniboss far from both the boss room and entrance
    /// </summary>
    public class InfectionEventPass : IGenPass
    {
        // FormKeys for the infection creatures
        private static readonly FormKey PestFormKey = new(gen_quest_main.StarfieldModKey, 0x0006AAD2); // LvlInvasivePest_HeatleechOrXenogrub
        private static readonly FormKey TerrormorphFormKey = new(gen_quest_main.StarfieldModKey, 0x002B2293); // LvlTerrormorph

        // Configuration
        private const int MinPests = 3;
        private const int MaxPests = 6;

        // Ensures only one infection is placed per pass instance
        private bool _hasPlaced;

        public void RunPass(DungeonState state)
        {
            if (_hasPlaced)
                return;

            if (state?.placedRooms == null || state.placedRooms.Count == 0)
                return;

            // Step 1: Scatter pests across the dungeon
            int pestCount = SpawnPests(state);

            // Step 2: Spawn Terrormorph miniboss (far from boss room and entrance)
            bool terrormorphPlaced = SpawnTerrormorph(state);

            _hasPlaced = true;

            if (!state.IsHarnessRun)
            {
                Console.WriteLine($"[Infection] Spawned {pestCount} pests and Terrormorph: {terrormorphPlaced}");
            }
        }

        /// <summary>
        /// Spawns pests scattered across the dungeon.
        /// </summary>
        private int SpawnPests(DungeonState state)
        {
            var spawnPoints = CollectSpawnPoints(state, includeBossRoom: false);
            if (spawnPoints.Count == 0)
                return 0;

            int pestCount = RandomUtils.random.Next(MinPests, MaxPests + 1);
            pestCount = Math.Min(pestCount, spawnPoints.Count);

            // Shuffle and pick spawn points
            var shuffled = spawnPoints.OrderBy(_ => RandomUtils.random.Next()).Take(pestCount).ToList();

            foreach (var spawn in shuffled)
            {
                var worldPos = CalculateWorldPosition(spawn.Room, spawn.Marker);

                state.PlacementUtil.NPCAddToTemporary(state.instance, new PlacedNpc(gen_quest_main.myMod)
                {
                    Rotation = spawn.Marker.Rotation,
                    Position = worldPos,
                    Base = new FormLink<INpcGetter>(PestFormKey)
                });
            }

            return pestCount;
        }

        /// <summary>
        /// Spawns the Terrormorph miniboss far from boss room and entrance.
        /// </summary>
        private bool SpawnTerrormorph(DungeonState state)
        {
            var spawnPoints = CollectSpawnPoints(state, includeBossRoom: false);
            if (spawnPoints.Count == 0)
                return false;

            // Score spawn points: want far from start AND far from boss room
            var scored = new List<(SpawnInfo Spawn, float Score)>();

            foreach (var spawn in spawnPoints)
            {
                float distFromStart = (float)Math.Sqrt(MathUtil.DistanceSquared(spawn.Room.WorldPos, state.StartingPosition));

                // Find distance to nearest boss room
                float distFromBoss = float.MaxValue;
                foreach (var room in state.placedRooms)
                {
                    if (!string.IsNullOrEmpty(room.DistrictType) &&
                        room.DistrictType.Contains("boss", StringComparison.OrdinalIgnoreCase))
                    {
                        float dist = (float)Math.Sqrt(MathUtil.DistanceSquared(spawn.Room.WorldPos, room.WorldPos));
                        distFromBoss = Math.Min(distFromBoss, dist);
                    }
                }

                // If no boss room, just use distance from start
                if (distFromBoss == float.MaxValue)
                    distFromBoss = distFromStart;

                // Score: balance between far from start and far from boss
                float score = Math.Min(distFromStart, distFromBoss);
                scored.Add((spawn, score));
            }

            // Sort by score descending (highest = farthest from both)
            scored.Sort((a, b) => b.Score.CompareTo(a.Score));

            // Pick from top candidates
            int pickRange = Math.Min(3, scored.Count);
            var chosen = scored[RandomUtils.random.Next(pickRange)].Spawn;

            var worldPos = CalculateWorldPosition(chosen.Room, chosen.Marker);

            state.PlacementUtil.NPCAddToTemporary(state.instance, new PlacedNpc(gen_quest_main.myMod)
            {
                Rotation = chosen.Marker.Rotation,
                Position = worldPos,
                Base = new FormLink<INpcGetter>(TerrormorphFormKey)
            });

            return true;
        }

        /// <summary>
        /// Collects all valid spawn points from the dungeon.
        /// </summary>
        private static List<SpawnInfo> CollectSpawnPoints(DungeonState state, bool includeBossRoom)
        {
            var result = new List<SpawnInfo>();

            foreach (var room in state.placedRooms)
            {
                if (room.Prefab?.Markers == null || room.Prefab.Markers.Count == 0)
                    continue;

                // Skip boss rooms if requested
                if (!includeBossRoom &&
                    !string.IsNullOrEmpty(room.DistrictType) &&
                    room.DistrictType.Contains("boss", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var marker in room.Prefab.Markers)
                {
                    var id = marker.MarkerEditorId;
                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    if (!id.StartsWith("rg_enemy_spawn", StringComparison.OrdinalIgnoreCase))
                        continue;

                    result.Add(new SpawnInfo
                    {
                        Room = room,
                        Marker = marker
                    });
                }
            }

            return result;
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
        }
    }
}
