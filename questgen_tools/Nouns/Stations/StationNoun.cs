using FrankyCLI.questgen_tools.Nouns;
using FrankyCLI.Retrograde.StationDesigns;
using FrankyCLI.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Assets;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Noggog.StructuredStrings.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public class StationNoun
    {
        public GenericBaseForm instance;

        public IStationDesign stationDesign;

        public StationNoun(string stationName, string faction, string size, IStationDesign design)
        {
            string StationID = Guid.NewGuid().ToString().Substring(0, 8);

            stationDesign = design;


            //Create and attack the location to the int cell so we can find it in quests
            var location = new Location(gen_quest_main.myMod)
            {
                EditorID = stationName + "_loc",
                Name = stationName,
                LocationCellMarkerReference = new ExtendedList<IFormLinkGetter<IPlacedGetter>>(),
                LocationCellUniqueReferences = new ExtendedList<LocationCellUniqueReference>(),
                LocationCellUniques = new ExtendedList<LocationCellUnique>(),
                LocationCellPersistentReferences = new ExtendedList<LocationReference>(),
                LocationCellStaticReferences = new ExtendedList<LocationCellStaticReference>()
            };
            gen_quest_main.myMod.Locations.Add(location);

            // Ship Interior Cell

            //Clone the Ship Interior
            var shipintcell = RetrogradeUtils.CloneCellById("duout02stationtestintcell");
            shipintcell.EditorID = "Station_shipint_" + StationID;
            shipintcell.Location = location.ToNullableLink<ILocationGetter>();
            shipintcell.Name = stationName;

            //Set the Interior door to be linked
            PlacedObject shipint_doorreference = null;
            PlacedObject shipinttoint_doorreference = null;
            PlacedObject shipint_xmarker = null;

            //Find the markers
            foreach (var persistant in shipintcell.Persistent)
            {
                if (persistant.EditorID == "duoutstationtestdoor")
                {
                    shipint_doorreference = (PlacedObject)persistant;
                }
                if (persistant.EditorID == "intdoorxmarker")
                {
                    shipint_xmarker = (PlacedObject)persistant;
                }
                if (persistant.EditorID == "du_shipinttointodoor")
                {
                    shipinttoint_doorreference = (PlacedObject)persistant;
                }
                
            }

            //Create a Message and set the door to it
            //MessageNoun messageNoun = new MessageNoun(0x000844, stationName);
            //shipinttoint_doorreference.TeleportName = messageNoun.instance.ToNullableLink();


            shipint_doorreference.LinkedReferences[0].Reference = shipint_xmarker.ToLink<ILinkedReferenceGetter>();

            gen_quest_main.myMod.Cells[0].SubBlocks[0].Cells.Add(shipintcell);


            // Interior Cell
            var intcell = RetrogradeUtils.CloneCellById("duoutstationtest02interior");
            intcell.EditorID = "Station_int_" + StationID;
            intcell.Location = location.ToNullableLink<ILocationGetter>();
            //Set the Interior door to be linked
            PlacedObject int_doorreference = null;
            PlacedObject int_xmarker = null;

            //Find the markers
            foreach (var persistant in intcell.Persistent)
            {
                if (persistant.EditorID == "du_intcelldoor")
                {
                    int_doorreference = (PlacedObject)persistant;
                }
                if (persistant.EditorID == "intdoorxmarker003")
                {
                    int_xmarker = (PlacedObject)persistant;
                }
            }
            //int_doorreference.LinkedReferences[0].Reference = int_xmarker.ToLink<ILinkedReferenceGetter>();
            gen_quest_main.myMod.Cells[0].SubBlocks[0].Cells.Add(intcell);



            // Ship Exterior Cell
            //Clone the Exterior
            var extcell = RetrogradeUtils.CloneCellById("duout02stationtestextcell");
            extcell.EditorID = "Station_ext_" + StationID;

            //Set the Doors to be linked

            //Ship Exterior to Ship Int
            ((PlacedObject)extcell.Persistent[0]).LinkedReferences[0].Reference = shipint_doorreference.ToLink<ILinkedReferenceGetter>();
            
            //Ship Int to Int
            shipinttoint_doorreference.TeleportDestination.Door = int_doorreference.ToLink<IPlacedObjectGetter>();
            int_doorreference.TeleportDestination.Door = shipinttoint_doorreference.ToLink<IPlacedObjectGetter>();

            gen_quest_main.myMod.Cells[0].SubBlocks[0].Cells.Add(extcell);


            //Clone the Base Form
            var ship = gen_quest_main.myMod.GenericBaseForms[FormKeyLookup.GetFormKey("duout02_stationtest")].DeepCopy();
            instance = new GenericBaseForm(gen_quest_main.myMod)
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
                    formLinkDataComponent.Links[0].LinkedForm = shipintcell.ToNullableLink<IStarfieldMajorRecordGetter>();
                    formLinkDataComponent.Links[1].LinkedForm = extcell.ToNullableLink<IStarfieldMajorRecordGetter>();
                }
            }
            gen_quest_main.myMod.GenericBaseForms.Add(instance);



            //Now generate the dungeon....

            StationDungeonGenerator dungeonGenerator = new StationDungeonGenerator(stationDesign);
            dungeonGenerator.GenerateDungeon(intcell, location, faction, size);
        }        
    }
}
