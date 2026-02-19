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
using Retrograde.Passes.SpaceStation;

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

            string stationsafename = System.Text.RegularExpressions.Regex.Replace(stationName.ToLower(), "[^a-z0-9]", "");

            stationDesign = design;


            var ShipExteriorlocation = new Location(targetMod)
            {
                EditorID = stationName + "_shipext_loc",
                Name = stationName,
                LocationCellUniqueReferences = new ExtendedList<LocationCellUniqueReference>(),
            };
            targetMod.Locations.Add(ShipExteriorlocation);


            var ShipIntlocation = new Location(targetMod)
            {
                EditorID = stationName + "_shipint_loc",
                Name = stationName,
                LocationCellUniqueReferences = new ExtendedList<LocationCellUniqueReference>(),

                ParentLocation = ShipExteriorlocation.ToNullableLink()
            };
            targetMod.Locations.Add(ShipIntlocation);

            // Ship Interior Cell

            //Clone the Ship Interior

            ShipInteriorCell = CellTools.CloneCellById("duout02stationtestintcell");
            ShipInteriorCell.EditorID = "rg_shipint_" + stationsafename;
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

                    if (persistantid.Contains("duoutstationtestdoor"))
                    {
                        shipint_doorreference = (PlacedObject)persistant;
                    }
                    if (persistantid.Contains("intdoorxmarker"))
                    {
                        shipint_xmarker = (PlacedObject)persistant;
                    }
                    if (persistantid.Contains("du_shipinttointodoor"))
                    {
                        shipinttoint_doorreference = (PlacedObject)persistant;
                    }
                }

            }

            shipint_doorreference.LinkedReferences[0].Reference = shipint_xmarker.ToLink<IPlacedGetter>();

            AddCellToMod(targetMod, ShipInteriorCell);


            // Interior Cell
            //Create and attack the location to the int cell so we can find it in quests

            var InteriorCellLocation = new Location(targetMod)
            {
                EditorID = stationName + "_interior_loc",
                Name = stationName,
                LocationCellUniqueReferences = new ExtendedList<LocationCellUniqueReference>(),
                ParentLocation = ShipIntlocation.ToNullableLink(),
            };
            targetMod.Locations.Add(InteriorCellLocation);


            InteriorCell = CellTools.CloneCellById("duoutstationtest02interior");

            InteriorCell.EditorID = "rg_intcell_" + stationsafename;
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

                    if (persistantid.Contains("du_intcelldoor"))
                    {
                        int_doorreference = (PlacedObject)persistant;
                    }
                    if (persistantid.Contains("intdoorxmarker003") )
                    {
                        int_xmarker = (PlacedObject)persistant;
                    }
                }
            }
            AddCellToMod(targetMod, InteriorCell);



            // Ship Exterior Cell
            //Clone the Exterior
            ExteriorCell = CellTools.CloneCellById("duout02stationtestextcell");

            ExteriorCell.Name = stationName;
            ExteriorCell.EditorID = "rg_extcell_" + stationsafename;
            ExteriorCell.Location = ShipExteriorlocation.ToNullableLink<ILocationGetter>();
            //Set the Doors to be linked

            //Ship Exterior to Ship Int
            ((PlacedObject)ExteriorCell.Persistent[0]).LinkedReferences[0].Reference = shipint_doorreference.ToLink<IPlacedGetter>();

            //Ship Int to Int
            shipinttoint_doorreference.TeleportDestination.Door = int_doorreference.ToLink<IPlacedObjectGetter>();
            int_doorreference.TeleportDestination.Door = shipinttoint_doorreference.ToLink<IPlacedObjectGetter>();

            AddCellToMod(targetMod, ExteriorCell);


            //Clone the Base Form
            var formKey = FormKeyLookup.GetFormKey("duout02_stationtest");
            IGenericBaseFormGetter? shipSource = targetMod.GenericBaseForms.FirstOrDefault(r => r.FormKey == formKey);
            if (shipSource == null)
                foreach (var tm in RetrogradeContext.Current.TemplateMods)
                {
                    shipSource = tm.GenericBaseForms.FirstOrDefault(r => r.FormKey == new FormKey(tm.ModKey, formKey.ID));
                    if (shipSource != null) break;
                }
            if (shipSource == null)
                throw new KeyNotFoundException($"StationNoun: no GenericBaseForm with EditorID 'duout02_stationtest' found in target mod or any template mod.");
            var ship = shipSource.DeepCopy();
            instance = new GenericBaseForm(targetMod)
            {
                EditorID = "rg_" + stationsafename,
                ObjectBounds = ship.ObjectBounds,
                Components = ship.Components,
                ObjectTemplates = ship.ObjectTemplates,
                Template = ship.Template,
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

            dungeonState = dungeonGenerator.GenerateDungeon(InteriorCell, InteriorCellLocation, faction, size, shipinttoint_doorreference);
        }

        private static void AddCellToMod(StarfieldMod targetMod, Cell cell)
        {
            var keyStr = cell.FormKey.ID.ToString();
            int blockNumber    = int.Parse(keyStr.Substring(keyStr.Length - 1));
            int subBlockNumber = int.Parse(keyStr.Substring(keyStr.Length - 2, 1));

            CellBlock cellBlock = null;
            foreach (var b in targetMod.Cells)
                if (b.BlockNumber == blockNumber) { cellBlock = b; break; }
            if (cellBlock == null)
            {
                cellBlock = new CellBlock
                {
                    BlockNumber = blockNumber,
                    GroupType = GroupTypeEnum.InteriorCellBlock,
                    SubBlocks = new ExtendedList<CellSubBlock>()
                };
                targetMod.Cells.Add(cellBlock);
            }

            CellSubBlock subBlock = null;
            foreach (var s in cellBlock.SubBlocks)
                if (s.BlockNumber == subBlockNumber) { subBlock = s; break; }
            if (subBlock == null)
            {
                subBlock = new CellSubBlock
                {
                    BlockNumber = subBlockNumber,
                    GroupType = GroupTypeEnum.InteriorCellSubBlock,
                    Cells = new ExtendedList<Cell>()
                };
                cellBlock.SubBlocks.Add(subBlock);
            }

            subBlock.Cells.Add(cell);
        }
    }
}
