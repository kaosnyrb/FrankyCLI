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
    /// </summary>
    public class EnemyAlertCoveragePass : IGenPass
    {
        // Alert prefabs ordered by size (largest first for efficient coverage)
        private static readonly (string prefabId, float size)[] AlertPrefabs = new[]
        {
            ("rg_enemy_alert_25_Defend", 25f),
            ("rg_enemy_alert_20_Defend", 20f),
            //("rg_enemy_alert_10_Defend", 10f),
        };

        public void RunPass(DungeonState state)
        {
            if (state?.placedRooms == null || state.placedRooms.Count == 0)
                return;

            // Load all available alert prefabs
            var availablePrefabs = new List<(RoomPrefab prefab, float size)>();
            foreach (var (prefabId, size) in AlertPrefabs)
            {
                try
                {
                    var prefab = PrefabCache.GetPrefab(prefabId);
                    if (prefab?.packin_instance != null)
                    {
                        availablePrefabs.Add((prefab, size));
                    }
                }
                catch
                {
                    // Prefab not found - continue with others
                }
            }

            if (availablePrefabs.Count == 0)
                return;

            // Use the largest available prefab for grid spacing
            var (alertPrefab, gridSpacing) = availablePrefabs[0];

            // Compute dungeon bounds from all placed rooms
            var bounds = ComputeDungeonBounds(state.placedRooms);

            // Place alert boxes in a grid across the dungeon
            int boxesPlaced = 0;
            for (float x = bounds.Min.X; x <= bounds.Max.X; x += gridSpacing)
            {
                for (float y = bounds.Min.Y; y <= bounds.Max.Y; y += gridSpacing)
                {
                    for (float z = bounds.Min.Z; z <= bounds.Max.Z; z += gridSpacing)
                    {
                        PlaceBox(state, alertPrefab, x, y, z);
                        boxesPlaced++;
                    }
                }
            }

            if (!state.IsHarnessRun)
                Console.WriteLine($"[EnemyAlert] Placed {boxesPlaced} alert boxes in grid");
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
