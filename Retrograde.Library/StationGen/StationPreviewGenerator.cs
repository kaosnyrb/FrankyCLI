using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Passes.SpaceStation;
using Retrograde.StationDesigns;
using Retrograde.Utils;
using System;

namespace Retrograde.Generator;

public class StationPreviewGenerator
{
    private readonly StationDungeonGenerator _generator;

    public IStationDesign StationDesign { get; }
    public string Faction { get; }
    public string Size { get; }
    public string StationName { get; }
    public Cell InteriorCell { get; }
    public Location InteriorLocation { get; }
    public DungeonState State { get; private set; }

    public StationPreviewGenerator(IStationDesign stationDesign, string faction, string size)
    {
        StationDesign = stationDesign;
        Faction = faction;
        Size = size;

        StationName = stationDesign.GenerateStationName(faction);

        InteriorCell = CellTools.CloneCellById("duoutstationtest02interior");
        InteriorCell.EditorID = "Fieldwright_preview_int";
        InteriorCell.Name = StationName;

        var targetMod = RetrogradeContext.Current.TargetMod;
        InteriorLocation = new Location(targetMod)
        {
            EditorID = StationName + "_interior_loc",
            Name = StationName,
            LocationCellUniqueReferences = new ExtendedList<LocationCellUniqueReference>(),
        };

        InteriorCell.Location = InteriorLocation.ToNullableLink<ILocationGetter>();

        _generator = new StationDungeonGenerator(stationDesign);
    }

    public DungeonState GenerateTopology(Action<IGenPass> onPassStarted = null)
    {
        State = _generator.GenerateTopology(InteriorCell, InteriorLocation, Faction, Size,
            onPassStarted: onPassStarted);
        return State;
    }

    public void RunContentPasses(Action<IGenPass> onPassStarted = null)
    {
        _generator.RunContentPasses(State, onPassStarted: onPassStarted);
    }
}
