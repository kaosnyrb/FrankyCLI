using FrankyCLI.questgen_tools;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;

namespace FrankyCLI.Retrograde.Passes
{
    /// <summary>
    /// Places enemy alert boxes in a grid across the dungeon for localized combat zones.
    /// Randomly picks from available alert types at each grid position for varied behavior.
    /// Boss rooms always get Defend prefabs for aggressive enemy response.
    /// </summary>
    public class EnemyAlertCoveragePass : IGenPass
    {
        // Alert prefabs - all same size (25), different behaviors
        private const string DefendPrefabId = "rg_enemy_alert_25_SandboxEngagedPreferredDefendHold"; // Hold the area
        private static readonly string[] AlertPrefabIds = new[]
        {
            DefendPrefabId,
            "rg_enemy_alert_25_EngagedPreferred", //Defend unless a fight is near
            "rg_enemy_alert_25_Engaged", // Hold Until Engaged
            "rg_enemy_alert_25_Chase" //Chase the player
        };

        private const float GridSpacing = 25f;

        public void RunPass(DungeonState state)
        {
            if (state?.placedRooms == null || state.placedRooms.Count == 0)
                return;

            // Load all available alert prefabs
            var availablePrefabs = new List<RoomPrefab>();
            RoomPrefab? defendPrefab = null;

            foreach (var prefabId in AlertPrefabIds)
            {
                try
                {
                    var prefab = PrefabCache.GetPrefab(prefabId);
                    if (prefab?.packin_instance != null)
                    {
                        availablePrefabs.Add(prefab);
                        if (prefabId == DefendPrefabId)
                            defendPrefab = prefab;
                    }
                }
                catch
                {
                    // Prefab not found - continue with others
                }
            }

            if (availablePrefabs.Count == 0)
                return;

            // Fallback if defend prefab not found
            if (defendPrefab == null)
                defendPrefab = availablePrefabs[0];

            // Compute dungeon bounds from all placed rooms
            var bounds = ComputeDungeonBounds(state.placedRooms);

            // Pre-compute boss room bounds for checking
            var bossRoomBounds = ComputeBossRoomBounds(state.placedRooms);

            // Place alert boxes in a grid across the dungeon
            int boxesPlaced = 0;
            for (float x = bounds.Min.X; x <= bounds.Max.X; x += GridSpacing)
            {
                for (float y = bounds.Min.Y; y <= bounds.Max.Y; y += GridSpacing)
                {
                    for (float z = bounds.Min.Z; z <= bounds.Max.Z; z += GridSpacing)
                    {
                        // Use Defend prefab in boss rooms, random elsewhere
                        RoomPrefab prefab;
                        if (IsInsideBossRoom(x, y, z, bossRoomBounds))
                            prefab = defendPrefab;
                        else
                            prefab = availablePrefabs[RandomUtils.random.Next(availablePrefabs.Count)];

                        PlaceBox(state, prefab, x, y, z);
                        boxesPlaced++;
                    }
                }
            }

            if (!state.IsHarnessRun)
                Console.WriteLine($"[EnemyAlert] Placed {boxesPlaced} alert boxes in grid ({availablePrefabs.Count} types)");
        }

        private static List<RgAabb> ComputeBossRoomBounds(List<PlacedRoom> placedRooms)
        {
            var bossRoomBounds = new List<RgAabb>();
            foreach (var room in placedRooms)
            {
                if (room.DistrictType != "boss")
                    continue;

                if (room.Prefab?.packin_instance?.ObjectBounds == null)
                    continue;

                var bounds = ConnectorUtils.ToWorldAabbRotated(
                    room.Prefab.packin_instance.ObjectBounds,
                    room.WorldPos,
                    room.YawSteps);
                bossRoomBounds.Add(bounds);
            }
            return bossRoomBounds;
        }

        private static bool IsInsideBossRoom(float x, float y, float z, List<RgAabb> bossRoomBounds)
        {
            foreach (var bounds in bossRoomBounds)
            {
                if (x >= bounds.Min.X && x <= bounds.Max.X &&
                    y >= bounds.Min.Y && y <= bounds.Max.Y &&
                    z >= bounds.Min.Z && z <= bounds.Max.Z)
                {
                    return true;
                }
            }
            return false;
        }

        private static RgAabb ComputeDungeonBounds(List<PlacedRoom> placedRooms)
        {
            var first = placedRooms[0];
            var b0 = ConnectorUtils.ToWorldAabbRotated(first.Prefab.packin_instance.ObjectBounds, first.WorldPos, first.YawSteps);

            float minX = b0.Min.X, minY = b0.Min.Y, minZ = b0.Min.Z;
            float maxX = b0.Max.X, maxY = b0.Max.Y, maxZ = b0.Max.Z;

            for (int i = 1; i < placedRooms.Count; i++)
            {
                var room = placedRooms[i];
                if (room.Prefab?.packin_instance?.ObjectBounds == null)
                    continue;

                var b = ConnectorUtils.ToWorldAabbRotated(room.Prefab.packin_instance.ObjectBounds, room.WorldPos, room.YawSteps);

                if (b.Min.X < minX) minX = b.Min.X;
                if (b.Min.Y < minY) minY = b.Min.Y;
                if (b.Min.Z < minZ) minZ = b.Min.Z;
                if (b.Max.X > maxX) maxX = b.Max.X;
                if (b.Max.Y > maxY) maxY = b.Max.Y;
                if (b.Max.Z > maxZ) maxZ = b.Max.Z;
            }

            return new RgAabb
            {
                Min = new P3Float(minX, minY, minZ),
                Max = new P3Float(maxX, maxY, maxZ)
            };
        }

        private static void PlaceBox(DungeonState state, RoomPrefab prefab, float x, float y, float z)
        {
            state.PlacementUtil.AddToTemporary(state.instance, new PlacedObject(gen_quest_main.myMod)
            {
                Count = 1,
                Rotation = new P3Float(0, 0, 0),
                Position = new P3Float(x, y, z),
                Base = prefab.packin_instance.ToLink<IPlaceableObjectGetter>()
            });
        }
    }
}
