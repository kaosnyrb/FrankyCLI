using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Retrograde.Passes
{
    public class WindowSealingPass : IGenPass
    {
        public void RunPass(DungeonState state)
        {
            const float maxSightDistance = 100f;

            // Iterate backwards so we can safely remove connectors that get sealed
            for (int i = state.openConnectors.Count - 1; i >= 0; i--)
            {
                var open = state.openConnectors[i];

                // Seal if any room is visible within the first 100 units ahead
                if (!HasRoomInLineOfSight(open, state.placedRooms, maxSightDistance))
                    continue;

                // Pick blocker prefab based on door size / tileset
                var blockerId = ConnectorUtils.GetWindowBlocker(open.Parsed.DoorSize, open.Parsed.Tileset);
                var blockerPrefab = new RoomPrefab(blockerId);

                // Blocker should have a connector that will attach to the OPEN connector.
                // If the open connector faces North, the blocker needs a South-facing connector to mate.
                var requiredDir = ConnectorUtils.Opposite(open.Parsed.Direction);

                // Try yaw steps 0..3 to orient blocker correctly (same approach as rooms)
                bool placed = false;

                for (int yawSteps = 0; yawSteps < 4; yawSteps++)
                {
                    var blockerConns = ConnectorUtils.GetConnectors(blockerPrefab, yawSteps);

                    // Find a connector on the blocker that matches required direction and same door size/tileset.
                    // If your blocker is generic and doesn’t encode tileset, drop that constraint.
                    var attach = blockerConns.FirstOrDefault(c =>
                        c.Parsed.Direction == requiredDir &&
                        string.Equals(c.Parsed.DoorSize, open.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Parsed.Tileset, open.Parsed.Tileset, StringComparison.OrdinalIgnoreCase));

                    if (!attach.Parsed.IsValid)
                        continue;

                    // Align blocker so its attach connector lands exactly at the open connector position
                    var blockerPos = open.WorldPos - attach.LocalPos;

                    state.instance.Temporary.Add(new PlacedObject(gen_quest_main.myMod)
                    {
                        Count = 1,
                        Rotation = RgRotation.RotationToP3Float(yawSteps),
                        Position = blockerPos,
                        Base = blockerPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                    });

                    placed = true;
                    break;
                }

                // Optional: log missing blocker connector rather than hard fail
                if (!placed)
                {
                    // You may want to add logging here, e.g. Debug.WriteLine(...)
                }
                else
                {
                    // Remove the connector now that it has been filled
                    state.openConnectors.RemoveAt(i);
                }
            }
        }

        private static bool HasRoomInLineOfSight(OpenConnector open, List<PlacedRoom> placedRooms, float maxDistance, float tolerance = 0.01f)
        {
            foreach (var room in placedRooms)
            {
                var bounds = ConnectorUtils.ToWorldAabbRotated(room.Prefab.packin_instance.ObjectBounds, room.WorldPos, room.YawSteps);

                // Skip the room that owns the connector (connector position lies inside its bounds)
                if (PointInside(bounds, open.WorldPos, tolerance))
                    continue;

                if (IsForwardOfConnector(open, bounds, maxDistance, tolerance))
                    return true;
            }

            return false;
        }

        private static bool IsForwardOfConnector(OpenConnector open, RgAabb bounds, float maxDistance, float tolerance)
        {
            var pos = open.WorldPos;
            switch (open.Parsed.Direction)
            {
                case ConnectorDirection.North:
                    return bounds.Max.Y >= pos.Y + tolerance &&
                           bounds.Min.Y <= pos.Y + maxDistance &&
                           AxisOverlap(pos.X, bounds.Min.X, bounds.Max.X, tolerance) &&
                           AxisOverlap(pos.Z, bounds.Min.Z, bounds.Max.Z, tolerance);
                case ConnectorDirection.South:
                    return bounds.Min.Y <= pos.Y - tolerance &&
                           bounds.Max.Y >= pos.Y - maxDistance &&
                           AxisOverlap(pos.X, bounds.Min.X, bounds.Max.X, tolerance) &&
                           AxisOverlap(pos.Z, bounds.Min.Z, bounds.Max.Z, tolerance);
                case ConnectorDirection.East:
                    return bounds.Max.X >= pos.X + tolerance &&
                           bounds.Min.X <= pos.X + maxDistance &&
                           AxisOverlap(pos.Y, bounds.Min.Y, bounds.Max.Y, tolerance) &&
                           AxisOverlap(pos.Z, bounds.Min.Z, bounds.Max.Z, tolerance);
                case ConnectorDirection.West:
                    return bounds.Min.X <= pos.X - tolerance &&
                           bounds.Max.X >= pos.X - maxDistance &&
                           AxisOverlap(pos.Y, bounds.Min.Y, bounds.Max.Y, tolerance) &&
                           AxisOverlap(pos.Z, bounds.Min.Z, bounds.Max.Z, tolerance);
                default:
                    return false;
            }
        }

        private static bool PointInside(RgAabb bounds, P3Float point, float tolerance)
        {
            return AxisOverlap(point.X, bounds.Min.X, bounds.Max.X, tolerance) &&
                   AxisOverlap(point.Y, bounds.Min.Y, bounds.Max.Y, tolerance) &&
                   AxisOverlap(point.Z, bounds.Min.Z, bounds.Max.Z, tolerance);
        }

        private static bool AxisOverlap(float value, float min, float max, float tolerance)
        {
            return value >= min - tolerance && value <= max + tolerance;
        }
    }
}
