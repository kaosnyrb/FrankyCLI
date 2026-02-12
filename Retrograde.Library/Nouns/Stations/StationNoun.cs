using Retrograde;
using Retrograde.Generator;
using Retrograde.StationDesigns;
using Retrograde.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Assets;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Retrograde.Passes;

namespace Retrograde.Nouns.Stations
{
    public class StationNoun
    {
        public GenericBaseForm instance;

        public Cell ExteriorCell;
        public Cell ShipInteriorCell;
        public Cell InteriorCell;

        public DungeonState dungeonState;


        public IStationDesign stationDesign;

        public StationNoun(string stationName, string faction, string size, IStationDesign design)
        {
            var targetMod = RetrogradeContext.Current.TargetMod;

            string StationID = Guid.NewGuid().ToString().Substring(0, 8);

            stationDesign = design;


            var ShipExteriorlocation = new Location(targetMod)
            {
                EditorID = stationName + "_shipext_loc",
                Name = stationName,
                LocationCellMarkerReference = new ExtendedList<IFormLinkGetter<IPlacedGetter>>(),
                LocationCellUniqueReferences = new ExtendedList<LocationCellUniqueReference>(),
                LocationCellUniques = new ExtendedList<LocationCellUnique>(),
                LocationCellPersistentReferences = new ExtendedList<LocationReference>(),
                LocationCellStaticReferences = new ExtendedList<LocationCellStaticReference>(),
            };
            targetMod.Locations.Add(ShipExteriorlocation);


            var ShipIntlocation = new Location(targetMod)
            {
                EditorID = stationName + "_shipint_loc",
                Name = stationName,
                LocationCellMarkerReference = new ExtendedList<IFormLinkGetter<IPlacedGetter>>(),
                LocationCellUniqueReferences = new ExtendedList<LocationCellUniqueReference>(),
                LocationCellUniques = new ExtendedList<LocationCellUnique>(),
                LocationCellPersistentReferences = new ExtendedList<LocationReference>(),
                LocationCellStaticReferences = new ExtendedList<LocationCellStaticReference>(),

                ParentLocation = ShipExteriorlocation.ToNullableLink()
            };
            targetMod.Locations.Add(ShipIntlocation);

            // Ship Interior Cell

            //Clone the Ship Interior

            ShipInteriorCell = CellTools.CloneCellById("duout02stationtestintcell");
            ShipInteriorCell.EditorID = "Station_shipint_" + StationID;
            ShipInteriorCell.Location = ShipIntlocation.ToNullableLink<ILocationGetter>();
            ShipInteriorCell.Name = stationName;

            //Set the Interior door to be linked
            PlacedObject shipint_doorreference = null;
            PlacedObject shipinttoint_doorreference = null;
            PlacedObject shipint_xmarker = null;

            //Find the markers
            foreach (var persistant in ShipInteriorCell.Persistent)
            {
                if (persistant.EditorID != null)
                {
                    string persistantid = persistant.EditorID.Replace("DUPLICATE000", "");

                    if (persistantid == "duoutstationtestdoor")
                    {
                        shipint_doorreference = (PlacedObject)persistant;
                    }
                    if (persistantid == "intdoorxmarker")
                    {
                        shipint_xmarker = (PlacedObject)persistant;
                    }
                    if (persistantid == "du_shipinttointodoor")
                    {
                        shipinttoint_doorreference = (PlacedObject)persistant;
                    }
                }

            }

            shipint_doorreference.LinkedReferences[0].Reference = shipint_xmarker.ToLink<ILinkedReferenceGetter>();

            targetMod.Cells[0].SubBlocks[0].Cells.Add(ShipInteriorCell);


            // Interior Cell
            //Create and attack the location to the int cell so we can find it in quests

            var InteriorCellLocation = new Location(targetMod)
            {
                EditorID = stationName + "_interior_loc",
                Name = stationName,
                LocationCellMarkerReference = new ExtendedList<IFormLinkGetter<IPlacedGetter>>(),
                LocationCellUniqueReferences = new ExtendedList<LocationCellUniqueReference>(),
                LocationCellUniques = new ExtendedList<LocationCellUnique>(),
                LocationCellPersistentReferences = new ExtendedList<LocationReference>(),
                LocationCellStaticReferences = new ExtendedList<LocationCellStaticReference>(),
                ParentLocation = ShipIntlocation.ToNullableLink(),
            };
            targetMod.Locations.Add(InteriorCellLocation);


            InteriorCell = CellTools.CloneCellById("duoutstationtest02interior");

            InteriorCell.EditorID = "Station_int_" + StationID;
            InteriorCell.Name = stationName;
            InteriorCell.Location = InteriorCellLocation.ToNullableLink<ILocationGetter>();

            //Set the Interior door to be linked
            PlacedObject int_doorreference = null;
            PlacedObject int_xmarker = null;

            //Find the markers
            foreach (var persistant in InteriorCell.Persistent)
            {
                if (persistant.EditorID != null)
                {
                    string persistantid = persistant.EditorID.Replace("DUPLICATE000", "");

                    if (persistantid == "du_intcelldoor")
                    {
                        int_doorreference = (PlacedObject)persistant;
                    }
                    if (persistantid == "intdoorxmarker003" )
                    {
                        int_xmarker = (PlacedObject)persistant;
                    }
                }
            }
            targetMod.Cells[0].SubBlocks[0].Cells.Add(InteriorCell);



            // Ship Exterior Cell
            //Clone the Exterior
            ExteriorCell = CellTools.CloneCellById("duout02stationtestextcell");

            ExteriorCell.Name = stationName;
            ExteriorCell.EditorID = "Station_ext_" + StationID;
            ExteriorCell.Location = ShipExteriorlocation.ToNullableLink<ILocationGetter>();
            //Set the Doors to be linked

            //Ship Exterior to Ship Int
            ((PlacedObject)ExteriorCell.Persistent[0]).LinkedReferences[0].Reference = shipint_doorreference.ToLink<ILinkedReferenceGetter>();

            //Ship Int to Int
            shipinttoint_doorreference.TeleportDestination.Door = int_doorreference.ToLink<IPlacedObjectGetter>();
            int_doorreference.TeleportDestination.Door = shipinttoint_doorreference.ToLink<IPlacedObjectGetter>();

            targetMod.Cells[0].SubBlocks[0].Cells.Add(ExteriorCell);


            //Clone the Base Form
            var formKey = FormKeyLookup.GetFormKey("duout02_stationtest");
            var ship = targetMod.GenericBaseForms[formKey].DeepCopy();
            instance = new GenericBaseForm(targetMod)
            {
                EditorID = "station_form_" + StationID,
                ObjectBounds = ship.ObjectBounds,
                Components = ship.Components,
                ObjectTemplates = ship.ObjectTemplates,
                Template = ship.Template,
                ObjectPlacementDefaults = ship.ObjectPlacementDefaults,
                ODTY = ship.ODTY,
                STRVs = ship.STRVs,
            };



            //Set the Exterior and Interior of the base form
            foreach (var component in instance.Components)
            {
                var typestring = component.GetType().ToString();
                if (typestring == "Mutagen.Bethesda.Starfield.FullNameComponent")
                {
                    FullNameComponent fullName = (FullNameComponent)component;
                    fullName.Name = stationName;
                }
                if (typestring == "Mutagen.Bethesda.Starfield.FormLinkDataComponent")
                {
                    FormLinkDataComponent formLinkDataComponent = (FormLinkDataComponent)component;
                    formLinkDataComponent.Links[0].LinkedForm = ShipInteriorCell.ToNullableLink<IStarfieldMajorRecordGetter>();
                    formLinkDataComponent.Links[1].LinkedForm = ExteriorCell.ToNullableLink<IStarfieldMajorRecordGetter>();
                }
            }
            targetMod.GenericBaseForms.Add(instance);



            //Now generate the dungeon....

            StationDungeonGenerator dungeonGenerator = new StationDungeonGenerator(stationDesign);

            dungeonState = dungeonGenerator.GenerateDungeon(InteriorCell, InteriorCellLocation, faction, size);
        }
    }
}
