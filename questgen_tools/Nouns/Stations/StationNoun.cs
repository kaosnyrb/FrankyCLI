using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Assets;
using Mutagen.Bethesda.Starfield;
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
            var intcell = CloneCellById("duout02stationtestintcell");
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
            var extcell = CloneCellById("duout02stationtestextcell");
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

            Console.WriteLine("Completed");
        }

        public Cell CloneCellById(string id)
        {
            for(int i = 0; i < gen_quest_main.myMod.Cells.Count; i++)
            {
                for (int j = 0; j < gen_quest_main.myMod.Cells[i].SubBlocks.Count; j++)
                {
                    for(int k = 0; k < gen_quest_main.myMod.Cells[i].SubBlocks[j].Cells.Count; k++)
                    {
                        if (gen_quest_main.myMod.Cells[i].SubBlocks[j].Cells[k].EditorID == id)
                        {
                            var refcell = gen_quest_main.myMod.Cells[i].SubBlocks[j].Cells[k].DeepCopy();

                            var persistantItems = new Noggog.ExtendedList<IPlaced>();
                            foreach(var item in refcell.Persistent)
                            {
                                PlacedObject poref = (PlacedObject)item;
                                PlacedObject placedObject = new PlacedObject(gen_quest_main.myMod)
                                {
                                    Action = poref.Action,
                                    AttachRef = poref.AttachRef,
                                    Base = poref.Base,
                                    BlueprintPartOrigin = poref.BlueprintPartOrigin,
                                    BOLV = poref.BOLV,
                                    Collision = poref.Collision,
                                    Comments = poref.Comments,
                                    Components = poref.Components,
                                    ConstrainedDecal = poref.ConstrainedDecal,
                                    Count = poref.Count,
                                    CurrentZoneCell = poref.CurrentZoneCell,
                                    DebugText = poref.DebugText,
                                    EditorID = poref.EditorID,
                                    Emittance = poref.Emittance,
                                    EnableParent = poref.EnableParent,
                                    EncounterZone = poref.EncounterZone,
                                    ExternalEmittance = poref.ExternalEmittance,
                                    FactionRank = poref.FactionRank,
                                    GeometryDirtinessScale = poref.GeometryDirtinessScale,
                                    GroupedPackIn = poref.GroupedPackIn,
                                    HeadTrackingWeight = poref.HeadTrackingWeight,
                                    HealthPercent = poref.HealthPercent,
                                    IsActivationPoint = poref.IsActivationPoint,
                                    IsIgnoredBySandbox = poref.IsIgnoredBySandbox,
                                    IsLinkedRefTransient = poref.IsLinkedRefTransient,
                                    Layer = poref.Layer,
                                    LayeredMaterialSwaps = poref.LayeredMaterialSwaps,
                                    LevelModifier = poref.LevelModifier,
                                    LightArea = poref.LightArea,
                                    LightBarndoorData = poref.LightBarndoorData,
                                    LightColors = poref.LightColors,
                                    LightFlicker = poref.LightFlicker,
                                    LightGobo = poref.LightGobo,
                                    Lighting = poref.Lighting,
                                    LightLayerData = poref.LightLayerData,
                                    LightRoundedness = poref.LightRoundedness,
                                    LightStaticShadowMap = poref.LightStaticShadowMap,
                                    LightVolumetricData = poref.LightVolumetricData,
                                    LinkedReferences = poref.LinkedReferences,
                                    LocationRefTypes = poref.LocationRefTypes,
                                    Lock = poref.Lock,
                                    MapMarker = poref.MapMarker,
                                    NavigationDoorLink = poref.NavigationDoorLink,
                                    NumTraversalFluffBytes = poref.NumTraversalFluffBytes,
                                    OpenByDefault = poref.OpenByDefault,
                                    Ownership = poref.Ownership,
                                    Patrol = poref.Patrol,
                                    Position = poref.Position,
                                    PersistentLocation = poref.PersistentLocation,
                                    PowerLinks = poref.PowerLinks,
                                    Primitive = poref.Primitive,
                                    ProjectedDecal = poref.ProjectedDecal,
                                    ProjectedDecalReferences = poref.ProjectedDecalReferences,
                                    Radius = poref.Radius,
                                    RagdollBipedRotation = poref.RagdollBipedRotation,
                                    Properties = poref.Properties,
                                    RagdollData = poref.RagdollData,
                                    ReferenceGroup = poref.ReferenceGroup,
                                    StarfieldMajorRecordFlags = poref.StarfieldMajorRecordFlags,
                                    Rotation = poref.Rotation,
                                    Scale = poref.Scale,
                                    ShipArrival = poref.ShipArrival,
                                    SnapLinks = poref.SnapLinks,
                                    SourcePackIn = poref.SourcePackIn,
                                    TeleportDestination = poref.TeleportDestination,
                                    TeleportName = poref.TeleportName,
                                    Spline = poref.Spline,
                                    TimeOfDay = poref.TimeOfDay,
                                    Traversals = poref.Traversals,
                                    VolumeData = poref.VolumeData,
                                    VirtualMachineAdapter = poref.VirtualMachineAdapter,
                                    XALG = poref.XALG,
                                    XCZA = poref.XCZA,
                                    XFLG = poref.XFLG,
                                    XNSE = poref.XNSE,
                                    XPCK = poref.XPCK
                                };
                                persistantItems.Add(placedObject);
                            }

                            var tempItems = new Noggog.ExtendedList<IPlaced>();
                            foreach (var item in refcell.Temporary)
                            {
                                PlacedObject poref = (PlacedObject)item;
                                PlacedObject placedObject = new PlacedObject(gen_quest_main.myMod)
                                {
                                    Action = poref.Action,
                                    AttachRef = poref.AttachRef,
                                    Base = poref.Base,
                                    BlueprintPartOrigin = poref.BlueprintPartOrigin,
                                    BOLV = poref.BOLV,
                                    Collision = poref.Collision,
                                    Comments = poref.Comments,
                                    Components = poref.Components,
                                    ConstrainedDecal = poref.ConstrainedDecal,
                                    Count = poref.Count,
                                    CurrentZoneCell = poref.CurrentZoneCell,
                                    DebugText = poref.DebugText,
                                    EditorID = poref.EditorID,
                                    Emittance = poref.Emittance,
                                    EnableParent = poref.EnableParent,
                                    EncounterZone = poref.EncounterZone,
                                    ExternalEmittance = poref.ExternalEmittance,
                                    FactionRank = poref.FactionRank,
                                    GeometryDirtinessScale = poref.GeometryDirtinessScale,
                                    GroupedPackIn = poref.GroupedPackIn,
                                    HeadTrackingWeight = poref.HeadTrackingWeight,
                                    HealthPercent = poref.HealthPercent,
                                    IsActivationPoint = poref.IsActivationPoint,
                                    IsIgnoredBySandbox = poref.IsIgnoredBySandbox,
                                    IsLinkedRefTransient = poref.IsLinkedRefTransient,
                                    Layer = poref.Layer,
                                    LayeredMaterialSwaps = poref.LayeredMaterialSwaps,
                                    LevelModifier = poref.LevelModifier,
                                    LightArea = poref.LightArea,
                                    LightBarndoorData = poref.LightBarndoorData,
                                    LightColors = poref.LightColors,
                                    LightFlicker = poref.LightFlicker,
                                    LightGobo = poref.LightGobo,
                                    Lighting = poref.Lighting,
                                    LightLayerData = poref.LightLayerData,
                                    LightRoundedness = poref.LightRoundedness,
                                    LightStaticShadowMap = poref.LightStaticShadowMap,
                                    LightVolumetricData = poref.LightVolumetricData,
                                    LinkedReferences = poref.LinkedReferences,
                                    LocationRefTypes = poref.LocationRefTypes,
                                    Lock = poref.Lock,
                                    MapMarker = poref.MapMarker,
                                    NavigationDoorLink = poref.NavigationDoorLink,
                                    NumTraversalFluffBytes = poref.NumTraversalFluffBytes,
                                    OpenByDefault = poref.OpenByDefault,
                                    Ownership = poref.Ownership,
                                    Patrol = poref.Patrol,
                                    Position = poref.Position,
                                    PersistentLocation = poref.PersistentLocation,
                                    PowerLinks = poref.PowerLinks,
                                    Primitive = poref.Primitive,
                                    ProjectedDecal = poref.ProjectedDecal,
                                    ProjectedDecalReferences = poref.ProjectedDecalReferences,
                                    Radius = poref.Radius,
                                    RagdollBipedRotation = poref.RagdollBipedRotation,
                                    Properties = poref.Properties,
                                    RagdollData = poref.RagdollData,
                                    ReferenceGroup = poref.ReferenceGroup,
                                    Rotation = poref.Rotation,
                                    Scale = poref.Scale,
                                    ShipArrival = poref.ShipArrival,
                                    SnapLinks = poref.SnapLinks,
                                    SourcePackIn = poref.SourcePackIn,
                                    TeleportDestination = poref.TeleportDestination,
                                    TeleportName = poref.TeleportName,
                                    Spline = poref.Spline,
                                    TimeOfDay = poref.TimeOfDay,
                                    Traversals = poref.Traversals,
                                    VolumeData = poref.VolumeData,
                                    VirtualMachineAdapter = poref.VirtualMachineAdapter,
                                    XALG = poref.XALG,
                                    XCZA = poref.XCZA,
                                    XFLG = poref.XFLG,
                                    XNSE = poref.XNSE,
                                    XPCK = poref.XPCK
                                };
                                tempItems.Add(placedObject);
                            }

                            Cell cell = new Cell(gen_quest_main.myMod)
                            {
                                AcousticSpace = refcell.AcousticSpace,
                                CellSkyRegion = refcell.CellSkyRegion,
                                Components = refcell.Components,
                                EditorID = id,
                                EncounterLocation = refcell.EncounterLocation,
                                EnvironmentMap = refcell.EnvironmentMap,
                                Flags = refcell.Flags,
                                GlobalDirtLayerMaterial = refcell.GlobalDirtLayerMaterial,
                                Grid = refcell.Grid,
                                ImageSpace = refcell.ImageSpace,
                                Lighting = refcell.Lighting,
                                IsLinkedRefTransient = refcell.IsLinkedRefTransient,
                                LightingTemplate = refcell.LightingTemplate,
                                LinkedReferences = refcell.LinkedReferences,
                                Location = refcell.Location,
                                MajorFlags = refcell.MajorFlags,
                                MHDT = refcell.MHDT,
                                Music = refcell.Music,
                                Name = refcell.Name,
                                Ownership = refcell.Ownership,
                                Persistent = persistantItems,
                                Temporary = tempItems,
                                TimeOfDay = refcell.TimeOfDay,
                                PersistentTimestamp = refcell.PersistentTimestamp,
                                PersistentUnknownGroupData = refcell.PersistentUnknownGroupData,
                                Timestamp = refcell.Timestamp,
                                Water = refcell.Water,
                                XCIB = refcell.XCIB,
                                WaterEnvironmentMap = refcell.WaterEnvironmentMap,
                                WaterHeight = refcell.WaterHeight,
                                WaterType = refcell.WaterType,
                                WaterVelocity = refcell.WaterVelocity,
                                XCLAs = refcell.XCLAs,
                                XILS = refcell.XILS,
                                XWCN = refcell.XWCN
                            };

                            return cell;

                        }
                    }
                }
            }
            return null;
        }
    }
}
