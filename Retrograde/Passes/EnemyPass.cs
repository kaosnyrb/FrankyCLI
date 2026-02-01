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

            int enemyCap = Math.Max(1, (int)Math.Ceiling(state.placedRooms.Count * 1.5f));

            var candidates = BuildCandidates(state, bossVector, bossDistance, fallbackDistance);
            if (candidates.Count == 0)
                return;

            enemyCap = Math.Min(enemyCap, candidates.Count);
            var chosenSpawns = ChooseCandidates(candidates, enemyCap);
            if (chosenSpawns.Count == 0)
                return;

            // Avoid clustering too many enemies in the same small area.
            const int maxClusterSize = 5;
            const float clusterRadius = 32;
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
                default:
                    stationFactionCrew = new CrimsonFleetFactionCrew();
                    break;
            }

            bool bossplaced = false;
            foreach (var spawn in spacedSpawns)
            {
                var placed = spawn.Room;

                var worldPos = CalculateWorldPosition(spawn);
                var worldRot = spawn.Marker.Rotation;

                Npc selected = stationFactionCrew.GetCrewMember(spawn.Room.DistrictType);

                if (spawn.Room.DistrictType == "boss" && !bossplaced)
                {
                    selected = stationFactionCrew.GetBoss(spawn.Room.DistrictType);
                    bossplaced = true;                
                }

                state.PlacementUtil.NPCAddToTemporary(state.instance, new PlacedNpc(gen_quest_main.myMod)
                {
                    Rotation = worldRot,
                    Position = worldPos,
                    Base = selected.ToLink<INpcGetter>()
                });
            }
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

        private static float ComputeWeight(float progress)
        {
            return 0.2f + 0.8f * progress * progress;
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

        private static P3Float CalculateWorldPosition(SpawnCandidate spawn)
        {
            var rotatedLocal = RgRotation.RotateYaw90(spawn.Marker.Position, spawn.Room.YawSteps);
            return spawn.Room.WorldPos + rotatedLocal;
        }
    }
}
