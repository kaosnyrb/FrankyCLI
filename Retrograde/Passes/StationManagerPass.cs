using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde.Passes;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Retrograde
{
    public class ShipMarkerPass : IGenPass
    {
        
        public void RunPass(DungeonState state)
        {
            // Copy any ShipMarker_* objects from prefabs into the live cell so the game can find them
            foreach (var placed in state.placedRooms)
            {
                var prefabCell = ResolvePrefabCell(placed.Prefab);
                if (prefabCell == null)
                    continue;

                foreach (var shipMarker in EnumerateShipMarkers(prefabCell))
                {
                    var rotatedLocal = RgRotation.RotateYaw90(shipMarker.Position, placed.YawSteps);
                    var worldPos = placed.WorldPos + rotatedLocal;
                    var worldRot = shipMarker.Rotation + RgRotation.RotationToP3Float(placed.YawSteps);

                    state.instance.Temporary.Add(new PlacedObject(gen_quest_main.myMod)
                    {
                        Count = 1,
                        Rotation = worldRot,
                        Position = worldPos,
                        Base = shipMarker.Base
                    });
                }
            }
        }

        private static IEnumerable<PlacedObject> EnumerateShipMarkers(Cell prefabCell)
        {
            foreach (var entry in prefabCell.Temporary)
            {
                if (entry is PlacedObject po && !string.IsNullOrWhiteSpace(po.EditorID) &&
                    po.EditorID.StartsWith("ShipMarker_", StringComparison.OrdinalIgnoreCase))
                {
                    yield return po;
                }
            }

            foreach (var entry in prefabCell.Persistent)
            {
                if (entry is PlacedObject po && !string.IsNullOrWhiteSpace(po.EditorID) &&
                    po.EditorID.StartsWith("ShipMarker_", StringComparison.OrdinalIgnoreCase))
                {
                    yield return po;
                }
            }
        }

        private static Cell ResolvePrefabCell(RoomPrefab prefab)
        {
            var cellFormKey = prefab.packin_instance?.Cell?.FormKey;
            if (cellFormKey == null)
                return null;

            for (int i = 0; i < gen_quest_main.myMod.Cells.Count; i++)
            {
                for (int j = 0; j < gen_quest_main.myMod.Cells[i].SubBlocks.Count; j++)
                {
                    for (int k = 0; k < gen_quest_main.myMod.Cells[i].SubBlocks[j].Cells.Count; k++)
                    {
                        if (gen_quest_main.myMod.Cells[i].SubBlocks[j].Cells[k].FormKey == cellFormKey)
                        {
                            return gen_quest_main.myMod.Cells[i].SubBlocks[j].Cells[k];
                        }
                    }
                }
            }

            return null;
        }
    }
}
