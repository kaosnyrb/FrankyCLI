using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Aspects;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Passes.SpaceStation
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
                    var worldRot = RgRotation.RotateYaw90(shipMarker.Rotation, placed.YawSteps) + RgRotation.RotationToP3Float(placed.YawSteps);

                    var newplaced = new PlacedObject(RetrogradeContext.Current.TargetMod)
                    {
                        Count = 1,
                        Rotation = worldRot,
                        Position = worldPos,
                        Base = shipMarker.Base.FormKey.ToNullableLink<IPlaceableObjectGetter>()
                    };
                    state.PlacementUtil.AddToTemporary(state.instance, newplaced);

                    //Find the marker from the base
                    var baseform = RetrogradeContext.Current.StarfieldMod.Statics[shipMarker.Base.FormKey];
                    var reftype = baseform.ForcedLocations[0];
                    var locreftype = RetrogradeContext.Current.StarfieldMod.LocationReferenceTypes[reftype.FormKey];

                    if (state.location.AddedSpecialReferences == null)
                        state.location.AddedSpecialReferences = new ExtendedList<LocationCellStaticReference>();
                    state.location.AddedSpecialReferences.Add(new LocationCellStaticReference()
                    {
                        Location = state.instance.ToNullableLink<IComplexLocationGetter>(),
                        Marker = newplaced.ToLink(),
                        LocationRefType = locreftype.ToLink()
                    });

                    markersPlaced++;
                }
            }

            if (!state.IsHarnessRun)
                Console.WriteLine($"[ShipMarker] Placed {markersPlaced} ship markers");
        }

        private static IEnumerable<IPlacedObjectGetter> EnumerateShipMarkers(ICellGetter prefabCell)
        {
            foreach (var entry in prefabCell.Temporary)
            {
                if (entry is IPlacedObjectGetter po && IsShipMarker(po))
                {
                    yield return po;
                }
            }

            foreach (var entry in prefabCell.Persistent)
            {
                if (entry is IPlacedObjectGetter po && IsShipMarker(po))
                {
                    yield return po;
                }
            }
        }

        private static bool IsShipMarker(IPlacedObjectGetter po)
        {
            if (po.Base.FormKey.ModKey.Name == "Starfield")
            {
                if (RetrogradeContext.Current.StarfieldMod.Statics.ContainsKey(po.Base.FormKey))
                {
                    var stat = RetrogradeContext.Current.StarfieldMod.Statics[po.Base.FormKey].EditorID;
                    bool result = stat.StartsWith("ShipMarker_");
                    return result;
                }
            }
            return false;
        }

        private static ICellGetter? ResolvePrefabCell(RoomPrefab prefab)
        {
            var cellFormKey = prefab.packin_instance?.Cell?.FormKey;
            if (cellFormKey == null)
                return null;

            foreach (var mod in RetrogradeContext.Current.TemplateMods)
                foreach (var block in mod.Cells)
                    foreach (var sub in block.SubBlocks)
                        foreach (var cell in sub.Cells)
                            if (cell.FormKey == cellFormKey)
                                return cell;

            return null;
        }
    }
}
