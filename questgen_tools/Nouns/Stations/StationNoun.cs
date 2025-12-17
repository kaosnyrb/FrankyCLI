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

namespace FrankyCLI.questgen_tools
{
    public class StationNoun
    {
        public GenericBaseForm instance;
        public StationNoun()
        {
            string StationID = Guid.NewGuid().ToString().Substring(0, 8);
            
            //Clone the Interior
            var intcell = StationUtils.CloneCellById("duout02stationtestintcell");
            intcell.EditorID = "Station_int_" + StationID;
            //Set the Interior door to be linked
            PlacedObject doorreference = null;            
            PlacedObject xmarker = null;

            //Find the markers
            foreach (var persistant in intcell.Persistent)
            {
                if (persistant.EditorID == "duoutstationtestdoor")
                {
                    doorreference = (PlacedObject)persistant;
                    //((PlacedObject)persistant).LinkedReferences[0].Reference =
                }
                if (persistant.EditorID == "intdoorxmarker")
                {
                    xmarker = (PlacedObject)persistant;
                }
            }
            doorreference.LinkedReferences[0].Reference = xmarker.ToLink<ILinkedReferenceGetter>();

            gen_quest_main.myMod.Cells[0].SubBlocks[0].Cells.Add(intcell);

            //Clone the Exterior
            var extcell = StationUtils.CloneCellById("duout02stationtestextcell");
            extcell.EditorID = "Station_ext_" + StationID;


            //Set the Door to be linked
            ((PlacedObject)extcell.Persistent[0]).LinkedReferences[0].Reference = doorreference.ToLink<ILinkedReferenceGetter>();

            gen_quest_main.myMod.Cells[0].SubBlocks[0].Cells.Add(extcell);


            //Clone the Base Form
            var ship = gen_quest_main.myMod.GenericBaseForms[new FormKey(gen_quest_main.myMod.ModKey, 0x016CC2)].DeepCopy();
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
                    fullName.Name = "Station " + StationID;
                }
                if (typestring == "Mutagen.Bethesda.Starfield.FormLinkDataComponent")
                {
                    FormLinkDataComponent formLinkDataComponent = (FormLinkDataComponent)component;
                    formLinkDataComponent.Links[0].LinkedForm = intcell.ToNullableLink<IStarfieldMajorRecordGetter>();
                    formLinkDataComponent.Links[1].LinkedForm = extcell.ToNullableLink<IStarfieldMajorRecordGetter>();
                }
            }
            gen_quest_main.myMod.GenericBaseForms.Add(instance);

            //Now generate the dungeon....
            GenerateDungeon(intcell);
        }

        public void GenerateDungeon(Cell cell)
        {
            //Build the list of prefabs we can use.
            RoomPrefab roomPrefab = new RoomPrefab("du_template_prefab_room_01");

            //Find starting point
            PlacedObject StartingMarker = null;
            foreach(var marker in cell.Persistent)
            {
                if (marker.EditorID != null)
                {
                    if ( marker.EditorID == "dumarker_start")
                    {
                        StartingMarker = (PlacedObject)marker;
                    }
                }
            }

            //Place a prefab
            var nextmarker = roomPrefab.Markers.Single(e => e.MarkerEditorId == "dumarker_south");            
            P3Float pos = StartingMarker.Position + nextmarker.Position;
            

            cell.Temporary.Add(new PlacedObject(gen_quest_main.myMod)
            {
                Count = 1,
                Rotation = new P3Float(),
                Position = pos,
                Base = roomPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
            });

            //
        }
        
    }
}
