using FrankyCLI.questgen_tools;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;

namespace FrankyCLI.Retrograde.Passes
{
    /// <summary>
    /// Places enemy alert boxes to create localized combat zones.
    /// Divides rooms into even blocks and places alert boxes at block boundaries/edges
    /// so enemies engage locally but can traverse nearby areas.
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

            // Use the largest available prefab size for block divisions
            var (alertPrefab, blockSize) = availablePrefabs[0];

            // Process each room - divide into blocks and place at edges
            int totalBoxes = 0;
            var coveredRooms = new List<string>();
            var uncoveredRooms = new List<string>();

            foreach (var room in state.placedRooms)
            {
                if (room.Prefab?.packin_instance?.ObjectBounds == null)
                    continue;

                var roomBounds = ConnectorUtils.ToWorldAabbRotated(
                    room.Prefab.packin_instance.ObjectBounds,
                    room.WorldPos,
                    room.YawSteps);

                string roomName = room.Prefab.packin_instance.EditorID ?? "Unknown";
                int boxesPlaced = PlaceAlertBoxesAtEdges(state, roomBounds, alertPrefab, blockSize);

                if (boxesPlaced > 0)
                {
                    coveredRooms.Add(roomName);
                    totalBoxes += boxesPlaced;
                }
                else
                {
                    uncoveredRooms.Add(roomName);
                }
            }

            // Log coverage results
            Console.WriteLine($"[EnemyAlert] Placed {totalBoxes} boxes across {coveredRooms.Count} rooms, {uncoveredRooms.Count} uncovered");
            if (uncoveredRooms.Count > 0)
            {
                Console.WriteLine($"[EnemyAlert] Uncovered rooms: {string.Join(", ", uncoveredRooms)}");
            }
        }

        /// <summary>
        /// Divides a room into even blocks and places alert boxes at the block edges/boundaries.
        /// This creates overlapping coverage at room subdivisions for smooth enemy response.
        /// </summary>
        /// <returns>Number of boxes placed.</returns>
        private static int PlaceAlertBoxesAtEdges(
            DungeonState state,
            RgAabb roomBounds,
            RoomPrefab alertPrefab,
            float blockSize)
        {
            // Calculate room dimensions
            float roomWidth = roomBounds.Max.X - roomBounds.Min.X;
            float roomDepth = roomBounds.Max.Y - roomBounds.Min.Y;
            float roomHeight = roomBounds.Max.Z - roomBounds.Min.Z;

            // Calculate how many blocks to divide the room into (minimum 1)
            int blocksX = Math.Max(1, (int)Math.Round(roomWidth / blockSize));
            int blocksY = Math.Max(1, (int)Math.Round(roomDepth / blockSize));
            int blocksZ = Math.Max(1, (int)Math.Round(roomHeight / blockSize));

            // Calculate the actual block size to evenly divide the room
            float actualBlockX = roomWidth / blocksX;
            float actualBlockY = roomDepth / blocksY;
            float actualBlockZ = roomHeight / blocksZ;

            int boxesPlaced = 0;

            // Place boxes at block boundaries (edges)
            // We place at each grid line intersection, which means blocksX+1 positions in X, etc.
            // But we place at the CENTER of each block edge, not at corners
            // So we iterate through each block and place at its edges
            for (int bx = 0; bx < blocksX; bx++)
            {
                for (int by = 0; by < blocksY; by++)
                {
                    for (int bz = 0; bz < blocksZ; bz++)
                    {
                        // Calculate the center of this block
                        float blockCenterX = roomBounds.Min.X + (bx + 0.5f) * actualBlockX;
                        float blockCenterY = roomBounds.Min.Y + (by + 0.5f) * actualBlockY;
                        float blockCenterZ = roomBounds.Min.Z + (bz + 0.5f) * actualBlockZ;

                        // Place a box at each face of this block that's on the room edge
                        // or at internal block boundaries

                        // X-axis edges (left and right faces of block)
                        if (bx == 0) // Left room edge
                        {
                            PlaceBox(state, alertPrefab, roomBounds.Min.X, blockCenterY, blockCenterZ);
                            boxesPlaced++;
                        }
                        if (bx == blocksX - 1) // Right room edge
                        {
                            PlaceBox(state, alertPrefab, roomBounds.Max.X, blockCenterY, blockCenterZ);
                            boxesPlaced++;
                        }
                        else // Internal boundary - place at right edge of this block
                        {
                            float edgeX = roomBounds.Min.X + (bx + 1) * actualBlockX;
                            PlaceBox(state, alertPrefab, edgeX, blockCenterY, blockCenterZ);
                            boxesPlaced++;
                        }

                        // Y-axis edges (front and back faces of block)
                        if (by == 0) // Front room edge
                        {
                            PlaceBox(state, alertPrefab, blockCenterX, roomBounds.Min.Y, blockCenterZ);
                            boxesPlaced++;
                        }
                        if (by == blocksY - 1) // Back room edge
                        {
                            PlaceBox(state, alertPrefab, blockCenterX, roomBounds.Max.Y, blockCenterZ);
                            boxesPlaced++;
                        }
                        else // Internal boundary
                        {
                            float edgeY = roomBounds.Min.Y + (by + 1) * actualBlockY;
                            PlaceBox(state, alertPrefab, blockCenterX, edgeY, blockCenterZ);
                            boxesPlaced++;
                        }

                        // Z-axis edges (top and bottom faces of block)
                        if (bz == 0) // Bottom room edge
                        {
                            PlaceBox(state, alertPrefab, blockCenterX, blockCenterY, roomBounds.Min.Z);
                            boxesPlaced++;
                        }
                        if (bz == blocksZ - 1) // Top room edge
                        {
                            PlaceBox(state, alertPrefab, blockCenterX, blockCenterY, roomBounds.Max.Z);
                            boxesPlaced++;
                        }
                        else // Internal boundary
                        {
                            float edgeZ = roomBounds.Min.Z + (bz + 1) * actualBlockZ;
                            PlaceBox(state, alertPrefab, blockCenterX, blockCenterY, edgeZ);
                            boxesPlaced++;
                        }
                    }
                }
            }

            return boxesPlaced;
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
