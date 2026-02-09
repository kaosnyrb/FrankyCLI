using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Passes
{
    /// <summary>
    /// Event pass that spawns an alien infection in the dungeon:
    /// - Several pests (Heatleech/Xenogrub) scattered across the map
    /// - Mature Gryllobas scattered across the station
    /// - A Grylloba hive room with a concentrated spawn
    /// </summary>
    public class InfectionEventPass : IGenPass
    {
        // FormKeys for the infection creatures
        private static readonly FormKey PestFormKey = new(RetrogradeContext.Current.StarfieldModKey, 0x0006AAD2); // LvlInvasivePest_HeatleechOrXenogrub
        private static readonly FormKey GryllobaFormKey = new(RetrogradeContext.Current.StarfieldModKey, 0x0032DB12); // LC030_EncGryllobaMelee06 "Mature Grylloba"

        // Configuration
        private const int MinPests = 3;
        private const int MaxPests = 6;
        private const int MinScatteredGryllobas = 2;
        private const int MaxScatteredGryllobas = 4;
        private const int MinHiveGryllobas = 3;
        private const int MaxHiveGryllobas = 5;

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

            // Step 2: Scatter Gryllobas across the station
            int scatteredCount = SpawnScatteredGryllobas(state);

            // Step 3: Spawn a Grylloba hive in one room
            int hiveCount = SpawnGryllobaHive(state);

            _hasPlaced = true;

            if (!state.IsHarnessRun)
            {
                Console.WriteLine($"[Infection] Spawned {pestCount} pests, {scatteredCount} scattered Gryllobas, hive with {hiveCount} Gryllobas");
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

            int pestCount = RandomProvider.Random.Next(MinPests, MaxPests + 1);
            pestCount = Math.Min(pestCount, spawnPoints.Count);

            // Shuffle and pick spawn points
            var shuffled = spawnPoints.OrderBy(_ => RandomProvider.Random.Next()).Take(pestCount).ToList();

            foreach (var spawn in shuffled)
            {
                var worldPos = CalculateWorldPosition(spawn.Room, spawn.Marker);

                state.PlacementUtil.NPCAddToTemporary(state.instance, new PlacedNpc(RetrogradeContext.Current.TargetMod)
                {
                    Rotation = spawn.Marker.Rotation,
                    Position = worldPos,
                    Base = new FormLink<INpcGetter>(PestFormKey)
                });
            }

            return pestCount;
        }

        /// <summary>
        /// Spawns Gryllobas scattered across the station, one per room.
        /// </summary>
        private int SpawnScatteredGryllobas(DungeonState state)
        {
            var spawnPoints = CollectSpawnPoints(state, includeBossRoom: false);
            if (spawnPoints.Count == 0)
                return 0;

            // Group by room so we pick at most one per room for scatter
            var byRoom = spawnPoints.GroupBy(s => s.Room).ToList();
            var shuffledRooms = byRoom.OrderBy(_ => RandomProvider.Random.Next()).ToList();

            int count = RandomProvider.Random.Next(MinScatteredGryllobas, MaxScatteredGryllobas + 1);
            count = Math.Min(count, shuffledRooms.Count);

            int placed = 0;
            foreach (var roomGroup in shuffledRooms.Take(count))
            {
                var markers = roomGroup.ToList();
                var spawn = markers[RandomProvider.Random.Next(markers.Count)];
                var worldPos = CalculateWorldPosition(spawn.Room, spawn.Marker);

                state.PlacementUtil.NPCAddToTemporary(state.instance, new PlacedNpc(RetrogradeContext.Current.TargetMod)
                {
                    Rotation = spawn.Marker.Rotation,
                    Position = worldPos,
                    Base = new FormLink<INpcGetter>(GryllobaFormKey)
                });
                placed++;
            }

            return placed;
        }

        /// <summary>
        /// Picks one room as a hive and spawns a concentrated group of Gryllobas.
        /// </summary>
        private int SpawnGryllobaHive(DungeonState state)
        {
            var spawnPoints = CollectSpawnPoints(state, includeBossRoom: false);
            if (spawnPoints.Count == 0)
                return 0;

            // Group by room, filter out rooms too close to the entrance, then prefer rooms with the most spawn points
            float minHiveDistance = 500f;
            var byRoom = spawnPoints.GroupBy(s => s.Room)
                .Select(g => new { Group = g, Distance = (float)Math.Sqrt(MathUtil.DistanceSquared(g.Key.WorldPos, state.StartingPosition)) })
                .Where(r => r.Distance >= minHiveDistance)
                .OrderByDescending(r => r.Group.Count())
                .ToList();

            // Fallback: if all rooms are near the entrance, just pick the farthest one
            if (byRoom.Count == 0)
            {
                byRoom = spawnPoints.GroupBy(s => s.Room)
                    .Select(g => new { Group = g, Distance = (float)Math.Sqrt(MathUtil.DistanceSquared(g.Key.WorldPos, state.StartingPosition)) })
                    .OrderByDescending(r => r.Distance)
                    .Take(1)
                    .ToList();
            }

            if (byRoom.Count == 0)
                return 0;

            var hiveRoom = byRoom[0].Group;
            var hiveMarkers = hiveRoom.OrderBy(_ => RandomProvider.Random.Next()).ToList();

            int hiveCount = RandomProvider.Random.Next(MinHiveGryllobas, MaxHiveGryllobas + 1);
            hiveCount = Math.Min(hiveCount, hiveMarkers.Count);

            int placed = 0;
            foreach (var spawn in hiveMarkers.Take(hiveCount))
            {
                var worldPos = CalculateWorldPosition(spawn.Room, spawn.Marker);

                state.PlacementUtil.NPCAddToTemporary(state.instance, new PlacedNpc(RetrogradeContext.Current.TargetMod)
                {
                    Rotation = spawn.Marker.Rotation,
                    Position = worldPos,
                    Base = new FormLink<INpcGetter>(GryllobaFormKey)
                });
                placed++;
            }

            return placed;
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
