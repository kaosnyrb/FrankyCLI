using Mutagen.Bethesda.Starfield;
using Noggog;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde;

public enum ConnectorDirection
{
    North,
    South,
    East,
    West,
    Unknown
}

public struct RgAabb
{
    public P3Float Min;
    public P3Float Max;
}

public struct RgConnector
{
    public string RawEditorId;
    public ConnectorDirection Direction;
    public string DoorSize;
    public string Tileset;
    public bool IsValid;
}

public struct RgConnectorInstance
{
    public string EditorId;          // rg_conn_n_D1_station
    public RgConnector Parsed;       // parsed metadata
    public P3Float LocalPos;         // marker.Position (local space)
}

public struct OpenConnector
{
    public RgConnector Parsed;       // direction/door/tileset
    public int YawSteps;             // 0..3
    public P3Float WorldPos;         // world-space position of this connector
    public string DistrictType;      // owning room's district (e.g., trunk, hab)
}

public struct PlacedRoom
{
    public RoomPrefab Prefab;
    public P3Float WorldPos;
    public int YawSteps; // 0..3
    public string DistrictType;
    public List<RgConnectorInstance> Connectors;
}

public class PrefabMarker
{
    public PrefabMarker() { }

    public string MarkerEditorId;
    public P3Float Position;
    public P3Float Rotation;
}

public static class RgConnectorParser
{
    public static RgConnector Parse(string editorId)
    {
        var result = new RgConnector
        {
            RawEditorId = editorId,
            IsValid = false
        };

        if (string.IsNullOrWhiteSpace(editorId))
            return result;

        if (editorId.Contains("DUPLICATE"))
        {
            editorId = editorId.Substring(0, editorId.IndexOf("DUPLICATE"));
        }

        var parts = editorId.Split('_');

        // Expected: rg conn n D1 station  => 5 parts
        if (parts.Length < 5)
            return result;

        if (parts[0] != "rg" || parts[1] != "conn")
            return result;

        result.Direction = ParseDirection(parts[2]);
        result.DoorSize = parts[3];
        result.Tileset = StripDigits(parts[4]);
        result.IsValid = result.Direction != ConnectorDirection.Unknown;

        return result;
    }

    private static string StripDigits(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return new string(value.Where(c => !char.IsDigit(c)).ToArray());
    }

    private static ConnectorDirection ParseDirection(string dir)
    {
        switch (dir.ToLowerInvariant())
        {
            case "n": return ConnectorDirection.North;
            case "s": return ConnectorDirection.South;
            case "e": return ConnectorDirection.East;
            case "w": return ConnectorDirection.West;
            default: return ConnectorDirection.Unknown;
        }
    }
}

public static class RgRotation
{
    // steps: 0=0°, 1=90°, 2=180°, 3=270° (clockwise or CCW depends on your coord system)
    public static P3Float RotateYaw90(P3Float v, int steps)
    {
        steps = ((steps % 4) + 4) % 4;

        // NOTE: This assumes a typical X/Y plane with +Z up.
        return steps switch
        {
            0 => v,
            1 => new P3Float(v.Y, -v.X, v.Z),
            2 => new P3Float(-v.X, -v.Y, v.Z),
            3 => new P3Float(-v.Y, v.X, v.Z),
            _ => v
        };
    }

    public static ConnectorDirection RotateDir(ConnectorDirection d, int steps)
    {
        steps = ((steps % 4) + 4) % 4;

        if (d == ConnectorDirection.Unknown)
            return d;

        for (int i = 0; i < steps; i++)
        {
            d = d switch
            {
                ConnectorDirection.North => ConnectorDirection.East,
                ConnectorDirection.East => ConnectorDirection.South,
                ConnectorDirection.South => ConnectorDirection.West,
                ConnectorDirection.West => ConnectorDirection.North,
                _ => ConnectorDirection.Unknown
            };
        }
        return d;
    }

    public static float EulerToRadCardinals(int euler)
    {
        switch (euler)
        {
            case 0:
                return 0;
            case 90:
                return 1.57079632f;
            case 180:
                return 3.14159f;
            case 270:
                return 4.71239f;
        }
        return 0;
    }

    public static P3Float RotationToP3Float(int yawSteps)
    {
        // Put yaw on Z assuming X/Y plane.
        return new P3Float(0, 0, EulerToRadCardinals(yawSteps * 90));
    }
}

/// <summary>
/// Represents a room prefab with its PackIn data and markers.
/// Must be initialized via the Initialize() method after the mod context is set up.
/// </summary>
public class RoomPrefab
{
    public string PrefabEditorId { get; private set; }
    public PackIn? packin_instance { get; set; }
    public List<PrefabMarker> Markers { get; private set; } = new();
    public P3Float StartingMarkerPosition;

    public RoomPrefab(string editorId)
    {
        PrefabEditorId = editorId;
    }

    /// <summary>
    /// Initializes the prefab by looking up the PackIn and extracting markers.
    /// This must be called after RetrogradeContext.Current is set.
    /// </summary>
    public void Initialize()
    {
        var mod = RetrogradeContext.Current.TargetMod;

        // Find the prefab
        foreach (var packin in mod.PackIns)
        {
            if (packin?.EditorID == PrefabEditorId)
            {
                packin_instance = packin;
                break;
            }
        }

        if (packin_instance == null)
            return;

        // Fetch the cell
        var cellFormKey = packin_instance.Cell.FormKey;
        Cell? prefabCell = FindCellByFormKey(mod, cellFormKey);

        if (prefabCell == null)
            return;

        // Extract markers from Temporary
        foreach (var entry in prefabCell.Temporary)
        {
            if (entry == null) continue;

            switch (entry)
            {
                case PlacedObject po:
                    TryAddMarker(po.EditorID, po.Position, po.Rotation);
                    break;
                case PlacedNpc pn:
                    TryAddMarker(pn.EditorID, pn.Position, pn.Rotation);
                    break;
            }
        }

        // Extract markers from Persistent
        foreach (var placed in prefabCell.Persistent)
        {
            if (placed is PlacedObject po && po.EditorID != null)
            {
                Markers.Add(new PrefabMarker
                {
                    MarkerEditorId = po.EditorID,
                    Position = po.Position,
                    Rotation = po.Rotation
                });
            }
        }
    }

    private static Cell? FindCellByFormKey(StarfieldMod mod, Mutagen.Bethesda.Plugins.FormKey formKey)
    {
        for (int i = 0; i < mod.Cells.Count; i++)
        {
            for (int j = 0; j < mod.Cells[i].SubBlocks.Count; j++)
            {
                for (int k = 0; k < mod.Cells[i].SubBlocks[j].Cells.Count; k++)
                {
                    if (mod.Cells[i].SubBlocks[j].Cells[k].FormKey == formKey)
                        return mod.Cells[i].SubBlocks[j].Cells[k];
                }
            }
        }
        return null;
    }

    private void TryAddMarker(string? editorId, P3Float position, P3Float rotation)
    {
        if (string.IsNullOrEmpty(editorId))
            return;

        Markers.Add(new PrefabMarker
        {
            MarkerEditorId = editorId,
            Position = position,
            Rotation = rotation
        });
    }
}
