using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Noggog;

namespace Retrograde.Passes
{
    public class PlugPass : IGenPass
    {
        public void RunPass(DungeonState state)
        {
            var connectors = CollectConnectors(state.placedRooms);
            if (connectors.Count == 0)
                return;

            const float sealedTolerance = 0.01f;
            var sealedKeys = state.SealedConnectorPositionKeys ?? new HashSet<string>();
            var occupiedKeys = new HashSet<string>(sealedKeys);

            int plugsPlaced = 0;
            int plugsFailed = 0;

            foreach (var open in connectors)
            {
                var key = PositionKey(open.WorldPos, sealedTolerance);
                if (occupiedKeys.Contains(key))
                    continue;

                // Pick plug prefab based on connector size / tileset
                var plugId = ConnectorUtils.GetPlug(open.Parsed.DoorSize, open.Parsed.Tileset);
                var plugPrefab = PrefabCache.GetPrefab(plugId);

                // Plug should have a connector that will attach to the OPEN connector.
                // If the open connector faces North, the plug needs a South-facing connector to mate.
                var requiredDir = ConnectorUtils.Opposite(open.Parsed.Direction);

                // Try yaw steps 0..3 to orient plug correctly (same approach as rooms)
                bool placed = false;

                for (int yawSteps = 0; yawSteps < 4; yawSteps++)
                {
                    var plugConns = ConnectorUtils.GetConnectors(plugPrefab, yawSteps);

                    // Find a connector on the plug that matches required direction and same door size/tileset.
                    var attach = plugConns.FirstOrDefault(c =>
                        c.Parsed.Direction == requiredDir &&
                        string.Equals(c.Parsed.DoorSize, open.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Parsed.Tileset, open.Parsed.Tileset, StringComparison.OrdinalIgnoreCase));

                    if (!attach.Parsed.IsValid)
                        continue;

                    // Align plug so its attach connector lands exactly at the open connector position
                    var plugPos = open.WorldPos - attach.LocalPos;

                    state.PlacementUtil.AddToTemporary(state.instance, new PlacedObject(RetrogradeContext.Current.TargetMod)
                    {
                        Count = 1,
                        Rotation = RgRotation.RotationToP3Float(yawSteps),
                        Position = plugPos,
                        Base = plugPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                    });

                    placed = true;
                    occupiedKeys.Add(key);
                    break;
                }

                if (placed)
                    plugsPlaced++;
                else
                    plugsFailed++;
            }

            if (!state.IsHarnessRun)
                Console.WriteLine($"[Plug] Placed {plugsPlaced} plugs, {plugsFailed} failed");
        }

        private static List<OpenConnector> CollectConnectors(List<PlacedRoom> placedRooms)
        {
            var connectors = new List<OpenConnector>();
            for (int roomIndex = 0; roomIndex < placedRooms.Count; roomIndex++)
            {
                var room = placedRooms[roomIndex];
                foreach (var c in room.Connectors)
                {
                    connectors.Add(new OpenConnector
                    {
                        Parsed = c.Parsed,
                        YawSteps = room.YawSteps,
                        WorldPos = room.WorldPos + c.LocalPos,
                        DistrictType = room.DistrictType
                    });
                }
            }

            return connectors;
        }

        private static string PositionKey(P3Float pos, float tolerance)
        {
            float scale = 1f / tolerance;
            return $"{MathF.Round(pos.X * scale)}|{MathF.Round(pos.Y * scale)}|{MathF.Round(pos.Z * scale)}";
        }
    }
}
