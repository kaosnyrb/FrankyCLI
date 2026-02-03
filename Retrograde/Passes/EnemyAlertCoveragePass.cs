using FrankyCLI.questgen_tools;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;

namespace FrankyCLI.Retrograde.Passes
{
    /// <summary>
    /// Scatters enemy alert area boxes (rg_enemy_alert) over the dungeon to provide
    /// coverage with minimal overlap. Each alert box is 10x10x10 units.
    /// </summary>
    public class EnemyAlertCoveragePass : IGenPass
    {
        private const float AlertBoxSize = 10f;
        private const string AlertPrefabId = "rg_enemy_alert";

        public void RunPass(DungeonState state)
        {
            if (state?.placedRooms == null || state.placedRooms.Count == 0)
                return;

            // Get the alert prefab
            RoomPrefab alertPrefab;
            try
            {
                alertPrefab = PrefabCache.GetPrefab(AlertPrefabId);
            }
            catch
            {
                // Prefab not found - skip pass
                return;
            }

            if (alertPrefab?.packin_instance == null)
                return;

            // Calculate the 3D bounding box of the entire dungeon
            var dungeonBounds = CalculateDungeonBounds(state);

            // Generate grid positions with AlertBoxSize spacing
            var gridPositions = GenerateGridPositions(dungeonBounds, AlertBoxSize);

            // Place alert boxes at each grid position
            foreach (var position in gridPositions)
            {
                state.PlacementUtil.AddToTemporary(state.instance, new PlacedObject(gen_quest_main.myMod)
                {
                    Count = 1,
                    Rotation = new P3Float(0, 0, 0),
                    Position = position,
                    Base = alertPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                });
            }
        }

        /// <summary>
        /// Calculates the 3D bounding box encompassing all placed rooms.
        /// Uses the rotated bounds of each room to get accurate coverage.
        /// </summary>
        private static RgAabb CalculateDungeonBounds(DungeonState state)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var room in state.placedRooms)
            {
                if (room.Prefab?.packin_instance?.ObjectBounds == null)
                    continue;

                var roomBounds = ConnectorUtils.ToWorldAabbRotated(
                    room.Prefab.packin_instance.ObjectBounds,
                    room.WorldPos,
                    room.YawSteps);

                if (roomBounds.Min.X < minX) minX = roomBounds.Min.X;
                if (roomBounds.Max.X > maxX) maxX = roomBounds.Max.X;
                if (roomBounds.Min.Y < minY) minY = roomBounds.Min.Y;
                if (roomBounds.Max.Y > maxY) maxY = roomBounds.Max.Y;
                if (roomBounds.Min.Z < minZ) minZ = roomBounds.Min.Z;
                if (roomBounds.Max.Z > maxZ) maxZ = roomBounds.Max.Z;
            }

            // Handle edge case where no valid bounds were found
            if (minX == float.MaxValue)
            {
                return new RgAabb
                {
                    Min = new P3Float(0, 0, 0),
                    Max = new P3Float(0, 0, 0)
                };
            }

            return new RgAabb
            {
                Min = new P3Float(minX, minY, minZ),
                Max = new P3Float(maxX, maxY, maxZ)
            };
        }

        /// <summary>
        /// Generates a grid of positions covering the bounding box with the specified spacing.
        /// Positions are offset by half the spacing to center the grid cells.
        /// </summary>
        private static List<P3Float> GenerateGridPositions(RgAabb bounds, float spacing)
        {
            var positions = new List<P3Float>();

            // Offset by half spacing so boxes are centered on the grid
            float halfSpacing = spacing / 2f;
            float startX = bounds.Min.X + halfSpacing;
            float startY = bounds.Min.Y + halfSpacing;
            float startZ = bounds.Min.Z + halfSpacing;

            for (float x = startX; x < bounds.Max.X; x += spacing)
            {
                for (float y = startY; y < bounds.Max.Y; y += spacing)
                {
                    for (float z = startZ; z < bounds.Max.Z; z += spacing)
                    {
                        positions.Add(new P3Float(x, y, z));
                    }
                }
            }

            return positions;
        }
    }
}
