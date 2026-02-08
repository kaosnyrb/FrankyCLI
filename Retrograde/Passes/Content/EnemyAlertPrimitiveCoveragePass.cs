using FrankyCLI.questgen_tools;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace FrankyCLI.Retrograde.Passes
{
    /// <summary>
    /// Places enemy alert volumes using PlacedPrimitive area boxes instead of prefabs.
    /// Recursively subdivides the dungeon bounds via BSP into varied-size rectangular
    /// sections, then places a primitive alert box for each section that overlaps a room.
    /// Boss rooms get Defend alert behavior; other areas get a random alert type.
    /// </summary>
    public class EnemyAlertPrimitiveCoveragePass : IGenPass
    {
        // Alert activator editor IDs for different combat behaviors
        private const string DefendActivatorId = "DMP_Room_SandboxEngagedPreferredDefend";

        // Alert type options for random selection per section
        private static readonly string[] AlertOptions = new[]
        {
            DefendActivatorId,
            "DMP_Room_PreferredDefend",
            "DMP_Room_EngagedPreferred",
            "DMP_Room_Engaged"
        };

        // Chance (0.0 to 1.0) that a non-boss section gets no alert box
        private const float NoneChance = 0.2f;

        // BSP subdivision limits
        private const float MinSectionSize = 10f;
        private const float MaxSectionSize = 40f;
        private const int MaxDepth = 6;

        public void RunPass(DungeonState state)
        {
            if (state?.placedRooms == null || state.placedRooms.Count == 0)
                return;

            // Get world-space AABB for every placed room
            var roomBounds = ComputeAllRoomBounds(state.placedRooms);
            if (roomBounds.Count == 0)
                return;

            var bossRoomBounds = ComputeBossRoomBounds(state.placedRooms);
            var dungeonBounds = ComputeDungeonBounds(roomBounds);

            // BSP subdivide then collect leaf sections that overlap rooms
            var sections = new List<RgAabb>();
            Subdivide(dungeonBounds, roomBounds, sections, 0);

            // Place a primitive for each section (one type per section, or none)
            int boxesPlaced = 0;
            int sectionsSkipped = 0;
            foreach (var section in sections)
            {
                float cx = (section.Min.X + section.Max.X) * 0.5f;
                float cy = (section.Min.Y + section.Max.Y) * 0.5f;
                float cz = (section.Min.Z + section.Max.Z) * 0.5f;

                float sx = section.Max.X - section.Min.X;
                float sy = section.Max.Y - section.Min.Y;
                float sz = section.Max.Z - section.Min.Z;

                bool isBossRoom = IsInsideBossRoom(cx, cy, cz, bossRoomBounds);

                // Non-boss sections have a chance to be skipped entirely
                if (!isBossRoom && RandomUtils.random.NextDouble() < NoneChance)
                {
                    sectionsSkipped++;
                    continue;
                }

                string selectedOption = isBossRoom
                    ? DefendActivatorId
                    : AlertOptions[RandomUtils.random.Next(AlertOptions.Length)];

                var activator = FindActivator(selectedOption);
                if (activator == null)
                    continue;

                PlacePrimitiveBox(state, activator, cx, cy, cz, new P3Float(sx, sy, sz));
                boxesPlaced++;
            }

            if (!state.IsHarnessRun)
                Console.WriteLine($"[EnemyAlertPrimitive] Placed {boxesPlaced} primitive alert boxes across {roomBounds.Count} rooms ({sectionsSkipped} sections skipped)");
        }

        /// <summary>
        /// Recursively splits an area via BSP. Leaf nodes that overlap at least one room
        /// are added to the output list. Sections that don't touch any room are discarded.
        /// </summary>
        private static void Subdivide(RgAabb area, List<RgAabb> roomBounds, List<RgAabb> output, int depth)
        {
            // Discard sections that don't overlap any room
            if (!OverlapsAnyRoom(area, roomBounds))
                return;

            float sizeX = area.Max.X - area.Min.X;
            float sizeY = area.Max.Y - area.Min.Y;
            float sizeZ = area.Max.Z - area.Min.Z;

            // Pick the longest axis to split along
            int axis; // 0=X, 1=Y, 2=Z
            float axisSize;
            if (sizeX >= sizeY && sizeX >= sizeZ)
            {
                axis = 0;
                axisSize = sizeX;
            }
            else if (sizeY >= sizeX && sizeY >= sizeZ)
            {
                axis = 1;
                axisSize = sizeY;
            }
            else
            {
                axis = 2;
                axisSize = sizeZ;
            }

            // Stop subdividing if section is small enough or we've hit max depth
            if (axisSize <= MaxSectionSize || depth >= MaxDepth)
            {
                output.Add(area);
                return;
            }

            // Randomised split between 40%-60% of the axis to get varied sizes
            float splitFraction = 0.4f + (float)RandomUtils.random.NextDouble() * 0.2f;
            float splitPoint;
            RgAabb childA, childB;

            switch (axis)
            {
                case 0: // Split on X
                    splitPoint = area.Min.X + sizeX * splitFraction;
                    // Ensure both children meet minimum size
                    if (splitPoint - area.Min.X < MinSectionSize || area.Max.X - splitPoint < MinSectionSize)
                    {
                        output.Add(area);
                        return;
                    }
                    childA = new RgAabb { Min = area.Min, Max = new P3Float(splitPoint, area.Max.Y, area.Max.Z) };
                    childB = new RgAabb { Min = new P3Float(splitPoint, area.Min.Y, area.Min.Z), Max = area.Max };
                    break;

                case 1: // Split on Y
                    splitPoint = area.Min.Y + sizeY * splitFraction;
                    if (splitPoint - area.Min.Y < MinSectionSize || area.Max.Y - splitPoint < MinSectionSize)
                    {
                        output.Add(area);
                        return;
                    }
                    childA = new RgAabb { Min = area.Min, Max = new P3Float(area.Max.X, splitPoint, area.Max.Z) };
                    childB = new RgAabb { Min = new P3Float(area.Min.X, splitPoint, area.Min.Z), Max = area.Max };
                    break;

                default: // Split on Z
                    splitPoint = area.Min.Z + sizeZ * splitFraction;
                    if (splitPoint - area.Min.Z < MinSectionSize || area.Max.Z - splitPoint < MinSectionSize)
                    {
                        output.Add(area);
                        return;
                    }
                    childA = new RgAabb { Min = area.Min, Max = new P3Float(area.Max.X, area.Max.Y, splitPoint) };
                    childB = new RgAabb { Min = new P3Float(area.Min.X, area.Min.Y, splitPoint), Max = area.Max };
                    break;
            }

            Subdivide(childA, roomBounds, output, depth + 1);
            Subdivide(childB, roomBounds, output, depth + 1);
        }

        private static bool OverlapsAnyRoom(RgAabb area, List<RgAabb> roomBounds)
        {
            foreach (var room in roomBounds)
            {
                if (ConnectorUtils.Intersects(area, room))
                    return true;
            }
            return false;
        }

        private static List<RgAabb> ComputeAllRoomBounds(List<PlacedRoom> placedRooms)
        {
            var result = new List<RgAabb>(placedRooms.Count);
            foreach (var room in placedRooms)
            {
                if (room.Prefab?.packin_instance?.ObjectBounds == null)
                    continue;

                result.Add(ConnectorUtils.ToWorldAabbRotated(
                    room.Prefab.packin_instance.ObjectBounds,
                    room.WorldPos,
                    room.YawSteps));
            }
            return result;
        }

        private static IActivatorGetter? FindActivator(string editorId)
        {
            foreach (var acti in gen_quest_main._StarfieldMod.Activators)
            {
                if (acti.EditorID == editorId)
                    return acti;
            }
            return null;
        }

        private static void PlacePrimitiveBox(DungeonState state, IActivatorGetter activator, float x, float y, float z, P3Float extents)
        {
            var placed = new PlacedObject(gen_quest_main.myMod)
            {
                Count = 1,
                Position = new P3Float(x, y, z),
                Rotation = new P3Float(0, 0, 0),
                Base = activator.ToLink<IPlaceableObjectGetter>(),
                Primitive = new PlacedPrimitive()
                {
                    Bounds = extents,
                    Color = Color.FromArgb(255, 100, 100),
                    Type = PlacedPrimitive.TypeEnum.Box
                }
            };

            state.PlacementUtil.AddToTemporary(state.instance, placed);
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

        private static RgAabb ComputeDungeonBounds(List<RgAabb> roomBounds)
        {
            var b0 = roomBounds[0];
            float minX = b0.Min.X, minY = b0.Min.Y, minZ = b0.Min.Z;
            float maxX = b0.Max.X, maxY = b0.Max.Y, maxZ = b0.Max.Z;

            for (int i = 1; i < roomBounds.Count; i++)
            {
                var b = roomBounds[i];
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
    }
}
