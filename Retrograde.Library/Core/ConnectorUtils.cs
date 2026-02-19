using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde;

public struct MarkerSlot
{
    public string SlotId;     // MarkerEditorId (non-connector)
    public P3Float LocalPos;  // marker.Position (local)
    public P3Float LocalRot;  // marker.Rotation (local) - optional if available
}

public class ConnectorUtils
{
    private static readonly ConcurrentDictionary<string, (P3Float Min, P3Float Max)> RotatedBoundsCache = new();

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
        return PrefabCache.GetConnectors(prefab, yawSteps);
    }

    internal static List<RgConnectorInstance> BuildConnectors(RoomPrefab prefab, int yawSteps = 0)
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
            .Select(x => x!.Value)
            .ToList();
    }

    public static RgAabb ToWorldAabb(IObjectBoundsGetter boundsLocal, P3Float worldPos)
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
            if (r.Prefab?.packin_instance?.ObjectBounds == null)
                continue;
            var placedAabb = ToWorldAabbRotated(r.Prefab.packin_instance.ObjectBounds, r.WorldPos, r.YawSteps);
            if (Intersects(candidate, placedAabb, padding))
                return true;
        }
        return false;
    }

    public static RgAabb ToWorldAabbRotated(IObjectBoundsGetter boundsLocal, P3Float worldPos, int yawSteps)
    {
        var key = $"{boundsLocal.First.X},{boundsLocal.First.Y},{boundsLocal.First.Z}|{boundsLocal.Second.X},{boundsLocal.Second.Y},{boundsLocal.Second.Z}|{yawSteps}";
        var rotated = RotatedBoundsCache.GetOrAdd(key, _ =>
        {
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

            var first = RgRotation.RotateYaw90(corners[0], yawSteps);
            float minX = first.X, minY = first.Y, minZ = first.Z;
            float maxX = first.X, maxY = first.Y, maxZ = first.Z;

            for (int i = 1; i < corners.Length; i++)
            {
                var w = RgRotation.RotateYaw90(corners[i], yawSteps);
                if (w.X < minX) minX = w.X;
                if (w.Y < minY) minY = w.Y;
                if (w.Z < minZ) minZ = w.Z;
                if (w.X > maxX) maxX = w.X;
                if (w.Y > maxY) maxY = w.Y;
                if (w.Z > maxZ) maxZ = w.Z;
            }

            return (new P3Float(minX, minY, minZ), new P3Float(maxX, maxY, maxZ));
        });

        return new RgAabb
        {
            Min = worldPos + rotated.Min,
            Max = worldPos + rotated.Max
        };
    }

    public static bool IsBelowYMin(RgAabb aabb, float yMin)
    {
        return aabb.Min.Y < yMin;
    }

    public static string GetWindowBlocker(string doorSize, string tileset)
    {
        var baseId = doorSize switch
        {
            "D1" => $"rg_windowblocker_D1_{tileset}",
            "D2" => $"rg_windowblocker_D2_{tileset}",
            _ => $"rg_blocker_{tileset}"
        };

        var candidates = new List<string>();
        foreach (var mod in RetrogradeContext.Current.TemplateMods)
            foreach (var packIn in mod.PackIns)
            {
                var editorId = packIn?.EditorID;
                if (!string.IsNullOrWhiteSpace(editorId) && editorId.StartsWith(baseId, StringComparison.OrdinalIgnoreCase))
                    candidates.Add(editorId);
            }

        if (candidates.Count == 0)
            return baseId;

        return candidates[RandomProvider.Random.Next(candidates.Count)];
    }

    public static string GetDoor(string doorSize, string tileset)
    {
        var baseId = doorSize switch
        {
            "D1" => $"rg_doorblocker_D1_{tileset}",
            "D2" => $"rg_doorblocker_D2_{tileset}",
            _ => $"rg_blocker_{tileset}"
        };

        var candidates = new List<string>();
        foreach (var mod in RetrogradeContext.Current.TemplateMods)
            foreach (var packIn in mod.PackIns)
            {
                var editorId = packIn?.EditorID;
                if (!string.IsNullOrWhiteSpace(editorId) && editorId.StartsWith(baseId, StringComparison.OrdinalIgnoreCase))
                    candidates.Add(editorId);
            }

        if (candidates.Count == 0)
            return baseId;

        return candidates[RandomProvider.Random.Next(candidates.Count)];
    }

    public static string GetPlug(string doorSize, string tileset)
    {
        var baseId = $"rg_plug_{doorSize}_{tileset}";
        var candidates = new List<string>();
        foreach (var mod in RetrogradeContext.Current.TemplateMods)
            foreach (var packIn in mod.PackIns)
            {
                var editorId = packIn?.EditorID;
                if (!string.IsNullOrWhiteSpace(editorId) && editorId.StartsWith(baseId, StringComparison.OrdinalIgnoreCase))
                    candidates.Add(editorId);
            }

        if (candidates.Count == 0)
            return baseId;

        return candidates[RandomProvider.Random.Next(candidates.Count)];
    }

    public static int ChooseFarthestNorthFromStart(List<OpenConnector> openConnectors, P3Float startingPosition)
    {
        float maxDist = float.MinValue;
        int bestIndex = -1;
        for (int i = 0; i < openConnectors.Count; i++)
        {
            if (openConnectors[i].Parsed.Direction != ConnectorDirection.North)
                continue;

            var dist = MathUtil.DistanceSquared(openConnectors[i].WorldPos, startingPosition);
            if (dist > maxDist || bestIndex == -1)
            {
                maxDist = dist;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    /// <summary>
    /// Clears the rotated bounds cache.
    /// </summary>
    public static void ClearCache()
    {
        RotatedBoundsCache.Clear();
    }
}
