using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FrankyCLI.questgen_tools;

namespace FrankyCLI.Retrograde.Passes
{
    public class ConectorSealingPass : IGenPass
    {
        public string GetDoorBlocker(string doorSize, string tileset)
        {
            // Prefer: tileset-specific blockers, fallback to generic
            var blockerId = doorSize switch
            {
                "D1" => $"rg_blocker_D1_{tileset}",
                "D2" => $"rg_blocker_D2_{tileset}",
                _ => $"rg_blocker_{tileset}"
            };

            // Gather all prefabs whose editor IDs contain the blocker ID, pick one at random.
            var candidates = new List<string>();

            foreach (var packin in gen_quest_main.myMod.PackIns)
            {
                if (packin?.EditorID == null)
                    continue;

                if (packin.EditorID.IndexOf(blockerId, StringComparison.OrdinalIgnoreCase) >= 0)
                    candidates.Add(packin.EditorID);
            }

            if (candidates.Count > 0)
                return candidates[RandomUtils.random.Next(candidates.Count)];

            return blockerId;
        }

        public void RunPass(DungeonState state)
        {
            // Close any connectors below the allowed Y plane first, then process the rest.
            var ordered = state.openConnectors
                .OrderBy(c => c.WorldPos.Y < state.YMin ? 0 : 1)
                .ToList();

            foreach (var open in ordered)
            {
                bool belowYMin = open.WorldPos.Y < state.YMin;

                // First try with strict matching; if we're below YMin, relax tileset matching to ensure we seal it.
                bool placed = TryPlaceBlocker(open, state, requireTilesetMatch: true);
                if (!placed && belowYMin)
                {
                    placed = TryPlaceBlocker(open, state, requireTilesetMatch: false);
                }

                // Optional: log missing blocker connector rather than hard fail
                if (!placed)
                {
                    // You may want to add logging here, e.g. Debug.WriteLine(...)
                }
            }
        }

        private bool TryPlaceBlocker(OpenConnector open, DungeonState state, bool requireTilesetMatch)
        {
            // Pick blocker prefab based on door size / tileset
            var blockerId = GetDoorBlocker(open.Parsed.DoorSize, open.Parsed.Tileset);
            var blockerPrefab = new RoomPrefab(blockerId);

            // Blocker should have a connector that will attach to the OPEN connector.
            // If the open connector faces North, the blocker needs a South-facing connector to mate.
            var requiredDir = ConnectorUtils.Opposite(open.Parsed.Direction);

            // Try yaw steps 0..3 to orient blocker correctly (same approach as rooms)
            for (int yawSteps = 0; yawSteps < 4; yawSteps++)
            {
                var blockerConns = ConnectorUtils.GetConnectors(blockerPrefab, yawSteps);

                // Find a connector on the blocker that matches required direction and same door size/tileset.
                var attach = blockerConns.FirstOrDefault(c =>
                    c.Parsed.Direction == requiredDir &&
                    string.Equals(c.Parsed.DoorSize, open.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                    (requireTilesetMatch
                        ? string.Equals(c.Parsed.Tileset, open.Parsed.Tileset, StringComparison.OrdinalIgnoreCase)
                        : true));

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

                return true;
            }

            return false;
        }
    }
}
