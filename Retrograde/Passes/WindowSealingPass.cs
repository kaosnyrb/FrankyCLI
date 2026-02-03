using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using FrankyCLI.Retrograde;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Retrograde.Passes
{

    //We used to build the dungeon in the ship interior cell, but the lighting was all over the show.
    //This used to place windows on connectors but we don't want that anymore as there's no star field to look at.
    public class WindowSealingPass : IGenPass
    {
        public void RunPass(DungeonState state)
        {
            const float edgeTolerance = 0.1f;

            if (state.placedRooms == null || state.placedRooms.Count == 0)
                return;

            int windowsPlaced = 0;
            int windowsFailed = 0;

            // Iterate backwards so we can safely remove connectors that get sealed
            for (int i = state.openConnectors.Count - 1; i >= 0; i--)
            {
                var open = state.openConnectors[i];

                // Only consider connectors on the extreme outer edge and facing outward
                if (!IsOnOuterEdgeFacingOut(open, state.placedRooms, edgeTolerance))
                    continue;

                // Pick blocker prefab based on door size / tileset
                var blockerId = ConnectorUtils.GetWindowBlocker(open.Parsed.DoorSize, open.Parsed.Tileset);
                var blockerPrefab = PrefabCache.GetPrefab(blockerId);

                // Blocker should have a connector that will attach to the OPEN connector.
                // If the open connector faces North, the blocker needs a South-facing connector to mate.
                var requiredDir = ConnectorUtils.Opposite(open.Parsed.Direction);

                // Try yaw steps 0..3 to orient blocker correctly (same approach as rooms)
                bool placed = false;

                for (int yawSteps = 0; yawSteps < 4; yawSteps++)
                {
                    var blockerConns = ConnectorUtils.GetConnectors(blockerPrefab, yawSteps);

                    // Find a connector on the blocker that matches required direction and same door size/tileset.
                    // If your blocker is generic and doesn�t encode tileset, drop that constraint.
                    var attach = blockerConns.FirstOrDefault(c =>
                        c.Parsed.Direction == requiredDir &&
                        string.Equals(c.Parsed.DoorSize, open.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Parsed.Tileset, open.Parsed.Tileset, StringComparison.OrdinalIgnoreCase));

                    if (!attach.Parsed.IsValid)
                        continue;

                    // Align blocker so its attach connector lands exactly at the open connector position
                    var blockerPos = open.WorldPos - attach.LocalPos;

                    state.PlacementUtil.AddToTemporary(state.instance, new PlacedObject(gen_quest_main.myMod)
                    {
                        Count = 1,
                        Rotation = RgRotation.RotationToP3Float(yawSteps),
                        Position = blockerPos,
                        Base = blockerPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                    });

                    placed = true;
                    break;
                }

                if (!placed)
                {
                    windowsFailed++;
                }
                else
                {
                    windowsPlaced++;
                    state.windowConnectors.Add(open.WorldPos);
                    // Remove the connector now that it has been filled
                    state.openConnectors.RemoveAt(i);
                }
            }

            Console.WriteLine($"[WindowSealing] Placed {windowsPlaced} window blockers, {windowsFailed} failed");
        }

        private static bool IsOnOuterEdgeFacingOut(OpenConnector open, List<PlacedRoom> placedRooms, float tolerance)
        {
            if (!TryGetPlacedBounds(placedRooms, out var bounds))
                return false;

            switch (open.Parsed.Direction)
            {
                case ConnectorDirection.North:
                    return open.WorldPos.Y >= bounds.Max.Y - tolerance;
                case ConnectorDirection.South:
                    return false; // never place south-facing windows
                case ConnectorDirection.East:
                    return open.WorldPos.X >= bounds.Max.X - tolerance;
                case ConnectorDirection.West:
                    return open.WorldPos.X <= bounds.Min.X + tolerance;
                default:
                    return false;
            }
        }

        private static bool TryGetPlacedBounds(List<PlacedRoom> placedRooms, out RgAabb bounds)
        {
            bounds = default;
            if (placedRooms == null || placedRooms.Count == 0)
                return false;

            var first = ConnectorUtils.ToWorldAabbRotated(placedRooms[0].Prefab.packin_instance.ObjectBounds, placedRooms[0].WorldPos, placedRooms[0].YawSteps);
            float minX = first.Min.X, minY = first.Min.Y, minZ = first.Min.Z;
            float maxX = first.Max.X, maxY = first.Max.Y, maxZ = first.Max.Z;

            for (int i = 1; i < placedRooms.Count; i++)
            {
                var aabb = ConnectorUtils.ToWorldAabbRotated(placedRooms[i].Prefab.packin_instance.ObjectBounds, placedRooms[i].WorldPos, placedRooms[i].YawSteps);
                if (aabb.Min.X < minX) minX = aabb.Min.X;
                if (aabb.Min.Y < minY) minY = aabb.Min.Y;
                if (aabb.Min.Z < minZ) minZ = aabb.Min.Z;
                if (aabb.Max.X > maxX) maxX = aabb.Max.X;
                if (aabb.Max.Y > maxY) maxY = aabb.Max.Y;
                if (aabb.Max.Z > maxZ) maxZ = aabb.Max.Z;
            }

            bounds = new RgAabb
            {
                Min = new P3Float(minX, minY, minZ),
                Max = new P3Float(maxX, maxY, maxZ)
            };

            return true;
        }

    }
}
