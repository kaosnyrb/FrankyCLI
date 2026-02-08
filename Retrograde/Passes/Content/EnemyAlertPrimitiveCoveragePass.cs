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
    /// Each grid cell gets a PlacedObject with an inline Primitive defining the alert zone,
    /// allowing custom area dimensions without requiring a matching prefab for every size.
    /// Boss rooms use Defend alert behavior; other areas get a random alert type.
    /// </summary>
    public class EnemyAlertPrimitiveCoveragePass : IGenPass
    {
        // Alert activator editor IDs for different combat behaviors
        private const string DefendActivatorId = "rg_enemy_alert_SandboxEngagedPreferredDefendHold";
        private static readonly string[] AlertActivatorIds = new[]
        {
            DefendActivatorId,
            "rg_enemy_alert_EngagedPreferred",
            "rg_enemy_alert_Engaged",
            "rg_enemy_alert_Chase"
        };

        private const float GridSpacing = 25f;
        private static readonly P3Float BoxExtents = new P3Float(GridSpacing, GridSpacing, GridSpacing);

        public void RunPass(DungeonState state)
        {
            if (state?.placedRooms == null || state.placedRooms.Count == 0)
                return;

            // Resolve activator base records by editor ID
            var availableActivators = new List<Activator>();
            Activator? defendActivator = null;

            foreach (var actId in AlertActivatorIds)
            {
                try
                {
                    var activator = FindActivator(actId);
                    if (activator != null)
                    {
                        availableActivators.Add(activator);
                        if (actId == DefendActivatorId)
                            defendActivator = activator;
                    }
                }
                catch
                {
                    // Activator not found - continue with others
                }
            }

            if (availableActivators.Count == 0)
                return;

            if (defendActivator == null)
                defendActivator = availableActivators[0];

            var bounds = ComputeDungeonBounds(state.placedRooms);
            var bossRoomBounds = ComputeBossRoomBounds(state.placedRooms);

            int boxesPlaced = 0;
            for (float x = bounds.Min.X; x <= bounds.Max.X; x += GridSpacing)
            {
                for (float y = bounds.Min.Y; y <= bounds.Max.Y; y += GridSpacing)
                {
                    for (float z = bounds.Min.Z; z <= bounds.Max.Z; z += GridSpacing)
                    {
                        Activator activator;
                        if (IsInsideBossRoom(x, y, z, bossRoomBounds))
                            activator = defendActivator;
                        else
                            activator = availableActivators[RandomUtils.random.Next(availableActivators.Count)];

                        PlacePrimitiveBox(state, activator, x, y, z, BoxExtents);
                        boxesPlaced++;
                    }
                }
            }

            if (!state.IsHarnessRun)
                Console.WriteLine($"[EnemyAlertPrimitive] Placed {boxesPlaced} primitive alert boxes in grid ({availableActivators.Count} types)");
        }

        private static Activator? FindActivator(string editorId)
        {
            foreach (var acti in gen_quest_main.myMod.Activators)
            {
                if (acti.EditorID == editorId)
                    return acti;
            }
            return null;
        }

        private static void PlacePrimitiveBox(DungeonState state, Activator activator, float x, float y, float z, P3Float extents)
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
    }
}
