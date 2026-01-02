using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Retrograde
{
    public struct MarkerSlot
    {
        public string SlotId;     // MarkerEditorId (non-connector)
        public P3Float LocalPos;  // marker.Position (local)
        public P3Float LocalRot;  // marker.Rotation (local) - optional if available
    }

    public class ConnectorUtils
    {
        public static ConnectorDirection Opposite(ConnectorDirection d)
        {
            return d switch
            {
                ConnectorDirection.North => ConnectorDirection.South,
                ConnectorDirection.South => ConnectorDirection.North,
                ConnectorDirection.East => ConnectorDirection.West,
                ConnectorDirection.West => ConnectorDirection.East,
                _ => ConnectorDirection.Unknown
            };
        }

        public static List<RgConnectorInstance> GetConnectors(RoomPrefab prefab, int yawSteps = 0)
        {
            return prefab.Markers
                .Select(m =>
                {
                    var parsed = RgConnectorParser.Parse(m.MarkerEditorId);
                    if (!parsed.IsValid) return (RgConnectorInstance?)null;

                    return new RgConnectorInstance
                    {
                        EditorId = m.MarkerEditorId,
                        Parsed = new RgConnector
                        {
                            RawEditorId = parsed.RawEditorId,
                            Direction = RgRotation.RotateDir(parsed.Direction, yawSteps),
                            DoorSize = parsed.DoorSize,
                            Tileset = parsed.Tileset,
                            IsValid = parsed.IsValid
                        },
                        LocalPos = RgRotation.RotateYaw90(m.Position, yawSteps)
                    };
                })
                .Where(x => x.HasValue)
                .Select(x => x.Value)
                .ToList();
        }

        public static RgAabb ToWorldAabb(ObjectBounds boundsLocal, P3Float worldPos)
        {
            // Local AABB translated into world space (no rotation assumed)
            return new RgAabb
            {
                Min = worldPos + boundsLocal.First,
                Max = worldPos + boundsLocal.Second
            };
        }

        public static bool Intersects(RgAabb a, RgAabb b, float padding = 0f)
        {
            // Optional padding expands A slightly to keep a clearance gap.
            return
                a.Min.X - padding <= b.Max.X && a.Max.X + padding >= b.Min.X &&
                a.Min.Y - padding <= b.Max.Y && a.Max.Y + padding >= b.Min.Y &&
                a.Min.Z - padding <= b.Max.Z && a.Max.Z + padding >= b.Min.Z;
        }

        public static bool CollidesWithAny(RgAabb candidate, List<PlacedRoom> placedRooms, float padding = 0f)
        {
            foreach (var r in placedRooms)
            {
                var placedAabb = ToWorldAabb(r.Prefab.packin_instance.ObjectBounds, r.WorldPos);
                if (Intersects(candidate, placedAabb, padding))
                    return true;
            }
            return false;
        }

        public static RgAabb ToWorldAabbRotated(ObjectBounds boundsLocal, P3Float worldPos, int yawSteps)
        {
            // Rotate 8 corners of the local AABB; then take min/max in world space.
            var min = boundsLocal.First;
            var max = boundsLocal.Second;

            P3Float[] corners =
            {
                new P3Float(min.X, min.Y, min.Z),
                new P3Float(min.X, min.Y, max.Z),
                new P3Float(min.X, max.Y, min.Z),
                new P3Float(min.X, max.Y, max.Z),
                new P3Float(max.X, min.Y, min.Z),
                new P3Float(max.X, min.Y, max.Z),
                new P3Float(max.X, max.Y, min.Z),
                new P3Float(max.X, max.Y, max.Z),
            };

            var first = worldPos + RgRotation.RotateYaw90(corners[0], yawSteps);
            float minX = first.X, minY = first.Y, minZ = first.Z;
            float maxX = first.X, maxY = first.Y, maxZ = first.Z;

            for (int i = 1; i < corners.Length; i++)
            {
                var w = worldPos + RgRotation.RotateYaw90(corners[i], yawSteps);
                if (w.X < minX) minX = w.X;
                if (w.Y < minY) minY = w.Y;
                if (w.Z < minZ) minZ = w.Z;
                if (w.X > maxX) maxX = w.X;
                if (w.Y > maxY) maxY = w.Y;
                if (w.Z > maxZ) maxZ = w.Z;
            }

            return new RgAabb
            {
                Min = new P3Float(minX, minY, minZ),
                Max = new P3Float(maxX, maxY, maxZ),
            };
        }

        public static string GetWindowBlocker(string doorSize, string tileset)
        {
            // Prefer: tileset-specific blockers, fallback to generic
            return doorSize switch
            {
                "D1" => $"rg_windowblocker_D1_{tileset}",
                "D2" => $"rg_windowblocker_D2_{tileset}",
                _ => $"rg_blocker_{tileset}"
            };
        }

        public static string GetDoor(string doorSize, string tileset)
        {
            // Prefer: tileset-specific blockers, fallback to generic
            return doorSize switch
            {
                "D1" => $"rg_doorblocker_D1_{tileset}",
                "D2" => $"rg_doorblocker_D2_{tileset}",
                _ => $"rg_blocker_{tileset}"
            };
        }
    }
}
