using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde.Passes;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Aspects;
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
            int markersPlaced = 0;

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

                    var newplaced = new PlacedObject(gen_quest_main.myMod)
                    {
                        Count = 1,
                        Rotation = worldRot,
                        Position = worldPos,
                        Base = shipMarker.Base
                    };
                    state.PlacementUtil.AddToTemporary(state.instance, newplaced);

                    //Find the marker from the base
                    var baseform = gen_quest_main._StarfieldMod.Statics[shipMarker.Base.FormKey];
                    var reftype = baseform.ForcedLocations[0];
                    var locreftype = gen_quest_main._StarfieldMod.LocationReferenceTypes[reftype.FormKey];

                    state.location.LocationCellStaticReferences.Add(new LocationCellStaticReference()
                    {
                        Location = state.instance.ToNullableLink<IComplexLocationGetter>(),
                        Marker = newplaced.ToLink(),
                        LocationRefType = locreftype.ToLink()
                    });

                    markersPlaced++;
                }
            }

            Console.WriteLine($"[ShipMarker] Placed {markersPlaced} ship markers");
        }

        private static IEnumerable<PlacedObject> EnumerateShipMarkers(Cell prefabCell)
        {
            foreach (var entry in prefabCell.Temporary)
            {
                if (entry is PlacedObject po && IsShipMarker(po))
                {
                    yield return po;
                }
            }

            foreach (var entry in prefabCell.Persistent)
            {
                if (entry is PlacedObject po && IsShipMarker(po))
                {
                    yield return po;
                }
            }
        }

        private static bool IsShipMarker(PlacedObject po)
        {
            if ( po.Base.FormKey.ModKey.Name == "Starfield")
            {
                if (gen_quest_main._StarfieldMod.Statics.ContainsKey(po.Base.FormKey))
                {
                    var stat = gen_quest_main._StarfieldMod.Statics[po.Base.FormKey].EditorID;
                    bool result = stat.StartsWith("ShipMarker_");
                    return result;
                }
            }
            return false;
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
