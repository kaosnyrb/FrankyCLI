using FrankyCLI;
using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde.FactionMembers;
using FrankyCLI.Retrograde.Passes;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Retrograde
{


    public class EnemyPass : IGenPass
    {
        private readonly Dictionary<string, FormList> _slotListsCache = new(StringComparer.OrdinalIgnoreCase);

        public string enemyType = "";

        private struct SpawnCandidate
        {
            public PrefabMarker Marker;
            public PlacedRoom Room;
            public float Weight;
        }

        private static string StripNumericSuffix(string id)
        {
            int lastUnderscore = id.LastIndexOf('_');
            if (lastUnderscore <= 0)
                return id;

            return id.Substring(0, lastUnderscore);
        }

        private FormList FindSlotList(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
                return null;

            // Normalize marker ID:
            // rg_slot_crate_large_02 -> rg_slot_crate_large
            var normalizedId = StripNumericSuffix(slotId);

            if (_slotListsCache.TryGetValue(normalizedId, out var cached))
                return cached;

            Mutagen.Bethesda.Starfield.FormList found = null;

            foreach (var fl in gen_quest_main.myMod.FormLists)
            {
                if (string.Equals(fl.EditorID, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    found = fl;
                    break;
                }
            }

            _slotListsCache[normalizedId] = found;
            return found;
        }

        public void RunPass(DungeonState state)
        {
            if (state?.placedRooms == null || state.placedRooms.Count == 0)
                return;

            enemyType = string.IsNullOrWhiteSpace(enemyType) ? state.Faction : enemyType;
            var enemySlotId = $"rg_enemy_spawn_{enemyType}_001";

            var slotList = FindSlotList(enemySlotId);
            if (slotList == null)
                return;

            var bossAnchor = DetermineBossAnchor(state);
            var bossVector = MathUtil.Subtract(bossAnchor, state.StartingPosition);
            var bossDistance = MathUtil.Length(bossVector);
            var fallbackDistance = Math.Max(1f, CalculateFarthestDistance(state));

            // Scale enemy count on dungeon area so large sprawling stations
            // get more enemies than compact ones, then apply a random density
            // multiplier (0.8x–1.4x) for per-run variety.
            float dungeonArea = CalculateDungeonArea(state);
            float areaPerEnemy = Math.Max(1f, state.AreaPerEnemy);
            int areaBasedCap = Math.Max(1, (int)Math.Ceiling(dungeonArea / areaPerEnemy));
            float densityMultiplier = 0.8f + (float)RandomUtils.random.NextDouble() * 0.6f;
            int enemyCap = Math.Max(1, (int)Math.Ceiling(areaBasedCap * densityMultiplier));

            var candidates = BuildCandidates(state, bossVector, bossDistance, fallbackDistance);
            if (candidates.Count == 0)
                return;

            // Reserve a boss spawn before general selection so it's guaranteed.
            SpawnCandidate? reservedBoss = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Room.DistrictType == "boss")
                {
                    reservedBoss = candidates[i];
                    candidates.RemoveAt(i);
                    break;
                }
            }

            enemyCap = Math.Min(enemyCap, candidates.Count);
            var chosenSpawns = ChooseCandidates(candidates, enemyCap);
            if (chosenSpawns.Count == 0 && reservedBoss == null)
                return;

            // Avoid clustering too many enemies in the same small area.
            const int maxClusterSize = 5;
            const float clusterRadius = 20;
            var spacedSpawns = EnforceClusterLimit(chosenSpawns, maxClusterSize, clusterRadius);
            if (spacedSpawns.Count == 0)
                return;


            //Generate the crew
            IFactionMembers stationFactionCrew = null;

            switch(state.Faction)
            {
                case "Crimsonfleet":
                    stationFactionCrew = new CrimsonFleetFactionCrew();
                    break;
                case "Ecliptic":
                    stationFactionCrew = new EclipticFactionCrew();
                    break;
                case "Varuun":
                    stationFactionCrew = new VaruunFactionCrew();
                    break;
                case "Spacer":
                    stationFactionCrew = new SpacerFactionCrew();
                    break;
                default:
                    stationFactionCrew = new CrimsonFleetFactionCrew();
                    break;
            }

            // Place guaranteed boss encounter first.
            if (reservedBoss.HasValue)
            {
                var bossSpawn = reservedBoss.Value;
                var bossPos = CalculateWorldPosition(bossSpawn);
                var bossRot = bossSpawn.Marker.Rotation;
                int bossGroupSize = DetermineGroupSize(bossSpawn.Room.DistrictType);

                for (int memberIdx = 0; memberIdx < bossGroupSize; memberIdx++)
                {
                    Npc selected = memberIdx == 0
                        ? stationFactionCrew.GetBoss(bossSpawn.Room.DistrictType)
                        : stationFactionCrew.GetCrewMember(bossSpawn.Room.DistrictType);

                    var memberPos = memberIdx == 0
                        ? bossPos
                        : OffsetPosition(bossPos, memberIdx);

                    state.PlacementUtil.NPCAddToTemporary(state.instance, new PlacedNpc(gen_quest_main.myMod)
                    {
                        Rotation = bossRot,
                        Position = memberPos,
                        Base = selected.ToLink<INpcGetter>()
                    });
                }
            }

            // Pick a miniboss candidate: 30% chance per non-boss spawn in
            // high-progress rooms (hab, ore, district). At most one miniboss.
            int minibossIndex = -1;
            if (spacedSpawns.Count > 0)
            {
                var eligibleIndices = new List<int>();
                for (int i = 0; i < spacedSpawns.Count; i++)
                {
                    var dt = spacedSpawns[i].Room.DistrictType;
                    if (dt != "boss" && dt != "util" && dt != "trunk")
                        eligibleIndices.Add(i);
                }
                if (eligibleIndices.Count > 0 && RandomUtils.random.Next(100) < 30)
                {
                    minibossIndex = eligibleIndices[RandomUtils.random.Next(eligibleIndices.Count)];
                }
            }

            // Track which rooms receive enemies so we can add patrols to empty ones.
            var roomsWithEnemies = new HashSet<PlacedRoom>();

            if (reservedBoss.HasValue)
                roomsWithEnemies.Add(reservedBoss.Value.Room);

            // Place remaining enemies.
            for (int spawnIdx = 0; spawnIdx < spacedSpawns.Count; spawnIdx++)
            {
                var spawn = spacedSpawns[spawnIdx];
                var worldPos = CalculateWorldPosition(spawn);
                var worldRot = spawn.Marker.Rotation;
                bool isMiniboss = spawnIdx == minibossIndex;

                roomsWithEnemies.Add(spawn.Room);

                int groupSize = isMiniboss
                    ? DetermineGroupSize("boss")
                    : DetermineGroupSize(spawn.Room.DistrictType);

                for (int memberIdx = 0; memberIdx < groupSize; memberIdx++)
                {
                    Npc selected;
                    if (isMiniboss && memberIdx == 0)
                        selected = stationFactionCrew.GetBoss(spawn.Room.DistrictType);
                    else
                        selected = stationFactionCrew.GetCrewMember(spawn.Room.DistrictType);

                    var memberPos = memberIdx == 0
                        ? worldPos
                        : OffsetPosition(worldPos, memberIdx);

                    state.PlacementUtil.NPCAddToTemporary(state.instance, new PlacedNpc(gen_quest_main.myMod)
                    {
                        Rotation = worldRot,
                        Position = memberPos,
                        Base = selected.ToLink<INpcGetter>()
                    });
                }
            }

            // Add lone patrol enemies to sections with no enemies.
            // This ensures there are no completely dead areas while maintaining
            // the peaks and lulls of the main encounter pacing.
            int patrolCount = PlaceLonePatrols(state, stationFactionCrew, roomsWithEnemies, candidates);

            int totalNpcs = (reservedBoss.HasValue ? 1 : 0) + spacedSpawns.Count + patrolCount;
            if (!state.IsHarnessRun)
                Console.WriteLine($"[Enemy] Placed {totalNpcs} NPCs ({spacedSpawns.Count} spawns, {patrolCount} patrols), faction: {state.Faction}");
        }

        /// <summary>
        /// Determines how many NPCs to spawn at a single marker based on room purpose.
        /// Combat-heavy districts get squads; quieter areas get lone sentries.
        /// </summary>
        private static int DetermineGroupSize(string districtType)
        {
            int min, max;
            switch (districtType)
            {
                case "boss":
                    min = 2; max = 4;
                    break;
                case "hab":
                case "ore":
                case "district":
                    min = 2; max = 3;
                    break;
                case "util":
                    min = 1; max = 1;
                    break;
                case "trunk":
                case "bridge":
                    min = 1; max = 2;
                    break;
                default:
                    min = 1; max = 2;
                    break;
            }

            return RandomUtils.random.Next(min, max + 1);
        }

        /// <summary>
        /// Places lone patrol enemies in rooms that have no enemies after main placement.
        /// This fills in the "lulls" with sparse encounters to prevent completely dead zones
        /// while still maintaining the contrast with high-intensity peak areas.
        /// Also covers safe zone rooms that were excluded from main placement.
        /// </summary>
        /// <returns>Number of patrol enemies placed.</returns>
        private int PlaceLonePatrols(
            DungeonState state,
            IFactionMembers factionCrew,
            HashSet<PlacedRoom> roomsWithEnemies,
            List<SpawnCandidate> allCandidates)
        {
            // Build a list of all rooms with spawn markers (including safe zone rooms
            // that weren't in the candidate list due to zero weight).
            var allSpawnableRooms = new Dictionary<PlacedRoom, PrefabMarker>();

            // First, add all rooms from candidates.
            foreach (var candidate in allCandidates)
            {
                if (!allSpawnableRooms.ContainsKey(candidate.Room))
                    allSpawnableRooms[candidate.Room] = candidate.Marker;
            }

            // Also scan for rooms that might have been excluded from candidates
            // (e.g., safe zone rooms) but still have spawn markers.
            foreach (var placed in state.placedRooms)
            {
                if (allSpawnableRooms.ContainsKey(placed) || roomsWithEnemies.Contains(placed))
                    continue;

                if (placed.Prefab?.Markers == null)
                    continue;

                foreach (var marker in placed.Prefab.Markers)
                {
                    var id = marker.MarkerEditorId;
                    if (!string.IsNullOrWhiteSpace(id) &&
                        id.StartsWith("rg_enemy_spawn", StringComparison.OrdinalIgnoreCase))
                    {
                        allSpawnableRooms[placed] = marker;
                        break;
                    }
                }
            }

            // Find rooms that have spawn markers but didn't get any enemies.
            var emptyRooms = new List<(PlacedRoom Room, PrefabMarker Marker)>();
            foreach (var kvp in allSpawnableRooms)
            {
                if (!roomsWithEnemies.Contains(kvp.Key))
                {
                    emptyRooms.Add((kvp.Key, kvp.Value));
                }
            }

            if (emptyRooms.Count == 0)
                return 0;

            // Place a lone patrol in each empty room.
            // These are single enemies that create atmosphere without overwhelming the player.
            foreach (var (room, marker) in emptyRooms)
            {
                var spawn = new SpawnCandidate { Room = room, Marker = marker, Weight = 1f };
                var worldPos = CalculateWorldPosition(spawn);
                var worldRot = marker.Rotation;

                // Single patrol enemy - not a group, just one sentry.
                Npc patrolNpc = factionCrew.GetCrewMember(room.DistrictType);

                state.PlacementUtil.NPCAddToTemporary(state.instance, new PlacedNpc(gen_quest_main.myMod)
                {
                    Rotation = worldRot,
                    Position = worldPos,
                    Base = patrolNpc.ToLink<INpcGetter>()
                });
            }

            return emptyRooms.Count;
        }

        /// <summary>
        /// Offsets additional group members slightly from the spawn marker so they
        /// don't stack on top of each other. Uses a small circle around the origin.
        /// </summary>
        private static P3Float OffsetPosition(P3Float origin, int index)
        {
            const float offsetRadius = 1.5f;
            double angle = index * (2.0 * Math.PI / 3.0); // evenly space up to 3 extras
            float dx = (float)(Math.Cos(angle) * offsetRadius);
            float dy = (float)(Math.Sin(angle) * offsetRadius);
            return new P3Float(origin.X + dx, origin.Y + dy, origin.Z);
        }

        private List<SpawnCandidate> BuildCandidates(
            DungeonState state,
            P3Float bossVector,
            float bossDistance,
            float fallbackDistance)
        {
            var result = new List<SpawnCandidate>();

            foreach (var placed in state.placedRooms)
            {
                if (placed.Prefab?.Markers == null || placed.Prefab.Markers.Count == 0)
                    continue;

                var progress = CalculateProgress(state.StartingPosition, bossVector, bossDistance, fallbackDistance, placed.WorldPos);
                var weight = ComputeWeight(progress);
                if (weight <= 0f)
                    continue;

                foreach (var marker in placed.Prefab.Markers)
                {
                    var id = marker.MarkerEditorId;

                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    if (!id.StartsWith("rg_enemy_spawn", StringComparison.OrdinalIgnoreCase))
                        continue;

                    result.Add(new SpawnCandidate
                    {
                        Marker = marker,
                        Room = placed,
                        Weight = weight
                    });
                }
            }

            return result;
        }

        private static List<SpawnCandidate> ChooseCandidates(List<SpawnCandidate> candidates, int targetCount)
        {
            var pool = new List<SpawnCandidate>(candidates);
            var chosen = new List<SpawnCandidate>();

            for (int i = 0; i < targetCount && pool.Count > 0; i++)
            {
                float totalWeight = pool.Sum(c => c.Weight);
                if (totalWeight <= 0f)
                    break;

                float roll = (float)(RandomUtils.random.NextDouble() * totalWeight);
                float cumulative = 0f;
                int chosenIndex = 0;

                for (int idx = 0; idx < pool.Count; idx++)
                {
                    cumulative += pool[idx].Weight;
                    if (roll <= cumulative)
                    {
                        chosenIndex = idx;
                        break;
                    }
                }

                chosen.Add(pool[chosenIndex]);
                pool.RemoveAt(chosenIndex);
            }

            return chosen;
        }

        private static List<SpawnCandidate> EnforceClusterLimit(
            List<SpawnCandidate> chosenSpawns,
            int maxClusterSize,
            float clusterRadius)
        {
            if (maxClusterSize <= 0 || clusterRadius <= 0f)
                return chosenSpawns;

            float radiusSq = clusterRadius * clusterRadius;
            var spaced = new List<SpawnCandidate>();

            foreach (var spawn in chosenSpawns)
            {
                var worldPos = CalculateWorldPosition(spawn);
                int nearby = 0;

                for (int i = 0; i < spaced.Count; i++)
                {
                    var otherPos = CalculateWorldPosition(spaced[i]);
                    if (MathUtil.DistanceSquared(worldPos, otherPos) <= radiusSq)
                    {
                        nearby++;
                        if (nearby >= maxClusterSize)
                            break;
                    }
                }

                if (nearby >= maxClusterSize)
                    continue;

                spaced.Add(spawn);
            }

            return spaced;
        }

        private static float CalculateProgress(
            P3Float start,
            P3Float bossVector,
            float bossDistance,
            float fallbackDistance,
            P3Float roomPos)
        {
            var roomVector = MathUtil.Subtract(roomPos, start);

            if (bossDistance > 0.01f)
            {
                var along = MathUtil.Dot(roomVector, bossVector) / bossDistance;
                var normalized = MathUtil.Clamp01(along / bossDistance);

                if (!float.IsNaN(normalized) && !float.IsInfinity(normalized))
                    return normalized;
            }

            var roomDistance = MathUtil.Length(roomVector);
            if (fallbackDistance <= 0f)
                return 0f;

            return MathUtil.Clamp01(roomDistance / fallbackDistance);
        }

        // Randomized once per pass so every run has a different quiet intro.
        private readonly float _safeZone = 0.05f + (float)RandomUtils.random.NextDouble() * 0.15f;

        // Pacing wave parameters for peaks and lulls of combat.
        // WaveFrequency controls how many intensity cycles occur across the dungeon.
        // WaveAmplitude controls how much the intensity varies (0 = flat, 1 = full swing).
        private readonly float _waveFrequency = 2.5f + (float)RandomUtils.random.NextDouble() * 1.5f; // 2.5-4 cycles
        private readonly float _waveAmplitude = 0.4f + (float)RandomUtils.random.NextDouble() * 0.2f; // 40-60% variation
        private readonly float _wavePhase = (float)RandomUtils.random.NextDouble() * (float)Math.PI; // Random start phase

        private float ComputeWeight(float progress)
        {
            // No enemies in the safe zone so the player has breathing
            // room to orient themselves on entry (varies 5%–20% per run).
            if (progress < _safeZone)
                return 0f;

            float adjusted = (progress - _safeZone) / (1f - _safeZone);

            // Base weight increases with progress (original behavior).
            float baseWeight = 0.2f + 0.8f * adjusted * adjusted;

            // Apply a sine wave to create peaks and lulls of intensity.
            // The wave oscillates between (1 - amplitude) and (1 + amplitude).
            // This creates exciting high-density areas and quieter exploration zones.
            float wave = (float)Math.Sin(adjusted * _waveFrequency * Math.PI * 2 + _wavePhase);
            float intensityModifier = 1f + wave * _waveAmplitude;

            // Ensure we never go below a minimum threshold to prevent completely dead zones.
            float finalWeight = baseWeight * intensityModifier;
            return Math.Max(0.1f, finalWeight);
        }

        private static bool IsBossRoom(PlacedRoom room)
        {
            var id = room.Prefab?.PrefabEditorId;
            return !string.IsNullOrEmpty(id) && id.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static P3Float DetermineBossAnchor(DungeonState state)
        {
            var bossRoom = FindBossRoom(state);
            if (bossRoom.HasValue)
                return bossRoom.Value.WorldPos;

            return FindFarthestRoomFrom(state.StartingPosition, state.placedRooms);
        }

        private static PlacedRoom? FindBossRoom(DungeonState state)
        {
            for (int i = 0; i < state.placedRooms.Count; i++)
            {
                var candidate = state.placedRooms[i];
                if (IsBossRoom(candidate))
                    return candidate;
            }

            return null;
        }

        private static P3Float FindFarthestRoomFrom(P3Float origin, List<PlacedRoom> rooms)
        {
            var best = origin;
            float bestDistance = float.MinValue;

            foreach (var room in rooms)
            {
                float dist = MathUtil.DistanceSquared(room.WorldPos, origin);
                if (dist > bestDistance)
                {
                    bestDistance = dist;
                    best = room.WorldPos;
                }
            }

            return best;
        }

        private static float CalculateFarthestDistance(DungeonState state)
        {
            float maxDistSq = 0f;
            foreach (var room in state.placedRooms)
            {
                var distSq = MathUtil.DistanceSquared(room.WorldPos, state.StartingPosition);
                if (distSq > maxDistSq)
                    maxDistSq = distSq;
            }

            return (float)Math.Sqrt(maxDistSq);
        }

        private static float CalculateDungeonArea(DungeonState state)
        {
            if (state.placedRooms.Count <= 1)
                return 1f;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (var room in state.placedRooms)
            {
                if (room.WorldPos.X < minX) minX = room.WorldPos.X;
                if (room.WorldPos.X > maxX) maxX = room.WorldPos.X;
                if (room.WorldPos.Y < minY) minY = room.WorldPos.Y;
                if (room.WorldPos.Y > maxY) maxY = room.WorldPos.Y;
            }

            float width = Math.Max(1f, maxX - minX);
            float height = Math.Max(1f, maxY - minY);
            return width * height;
        }

        private static P3Float CalculateWorldPosition(SpawnCandidate spawn)
        {
            var rotatedLocal = RgRotation.RotateYaw90(spawn.Marker.Position, spawn.Room.YawSteps);
            return spawn.Room.WorldPos + rotatedLocal;
        }
    }
}
