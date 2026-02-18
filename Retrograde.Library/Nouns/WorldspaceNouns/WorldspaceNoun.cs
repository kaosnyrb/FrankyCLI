using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Generator;
using Retrograde.Passes;
using Retrograde.WorldspaceDesigns;
using System;
using System.IO;
using System.Linq;

namespace Retrograde.Nouns.WorldspaceNouns;

/// <summary>
/// Sets up a Starfield worldspace for procedural generation.
/// Clones the template worldspace and terrain, creates the location,
/// and runs the worldspace dungeon generator.
/// Ported from StarTiller POIBuilder.Generate().
/// </summary>
public class WorldspaceNoun
{
    public Worldspace Worldspace;
    public Location Location;
    public WorldspaceState State;

    public WorldspaceNoun(IWorldspaceDesign design, string faction, int seed, string dataFolderPath = null)
    {
        var targetMod = RetrogradeContext.Current.TargetMod;
        var starfieldEsm = RetrogradeContext.Current.StarfieldModKey;

        // Generate POI name and sanitized EditorID
        string poiName = design.GeneratePOIName(seed);
        string vowels = "aeiouy ";
        string shortname = poiName.ToLower();
        shortname = new string(shortname.Where(c => !vowels.Contains(c)).ToArray());
        string prefix = targetMod.Worldspaces.Count().ToString("000");
        string editorId = prefix + "wld" + shortname;

        if (!RetrogradeContext.Quiet)
        {
            Console.WriteLine("Building new POI: " + poiName);
            Console.WriteLine(editorId);
        }

        // Location Keywords
        IFormLinkNullable<IKeywordGetter> LocTypeDungeon = new FormKey(starfieldEsm, 0x000254BC).ToNullableLink<IKeywordGetter>();
        IFormLinkNullable<IKeywordGetter> LocTypeClearable = new FormKey(starfieldEsm, 0x00064EDE).ToNullableLink<IKeywordGetter>();
        IFormLinkNullable<IKeywordGetter> LocTypeOE_Keyword = new FormKey(starfieldEsm, 0x001A5468).ToNullableLink<IKeywordGetter>();
        IFormLinkNullable<IKeywordGetter> LocEncSpacers_Exclusive = new FormKey(starfieldEsm, 0x00283585).ToNullableLink<IKeywordGetter>();
        IFormLinkNullable<IKeywordGetter> LocTypeOverlay = new FormKey(starfieldEsm, 0x002CA99D).ToNullableLink<IKeywordGetter>();

        Location = new Location(targetMod)
        {
            EditorID = prefix + "loc" + shortname,
            Name = poiName,
            Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>(),
            WorldLocationRadius = 0,
            ActorFadeMult = 1,
            TNAM = 0,
        };

        Location.Keywords.Add(LocTypeDungeon);
        Location.Keywords.Add(LocTypeClearable);
        Location.Keywords.Add(LocTypeOE_Keyword);
        Location.Keywords.Add(LocEncSpacers_Exclusive);
        Location.Keywords.Add(LocTypeOverlay);
        targetMod.Locations.Add(Location);

        // Clone template worldspace and terrain
        var baseWorld = targetMod.Worldspaces.Where(x => x.EditorID == design.TemplateWorldspaceEditorId).First();
        Worldspace = targetMod.Worldspaces.DuplicateInAsNewRecord(baseWorld);

        var stbBlock = targetMod.SurfaceBlocks.Where(x => x.EditorID == design.TemplateSurfaceBlockEditorId).First();
        var newBlock = targetMod.SurfaceBlocks.DuplicateInAsNewRecord(stbBlock);

        // Copy terrain file if data folder path is provided
        string newTerrainFile = "Data\\Terrain\\" + editorId + ".btd";
        if (dataFolderPath != null)
        {
            string sourceTerrainPath = Path.Combine(dataFolderPath, "Terrain", design.TemplateWorldspaceEditorId + ".btd");
            string destTerrainPath = Path.Combine(dataFolderPath, "Terrain", editorId + ".btd");
            try
            {
                if (!File.Exists(destTerrainPath))
                {
                    File.Copy(sourceTerrainPath, destTerrainPath);
                }
            }
            catch
            {
                if (!RetrogradeContext.Quiet)
                    Console.WriteLine("Terrain file probably already exists");
            }
        }

        newBlock.ANAM = newTerrainFile;
        newBlock.EditorID = "OverlayBlock" + editorId;
        ((WorldSpaceOverlayComponent)Worldspace.Components[0]).SurfaceBlock = newBlock.ToNullableLink<ISurfaceBlockGetter>();
        Worldspace.EditorID = editorId;
        Worldspace.Location = Location.ToNullableLink<ILocationGetter>();
        Worldspace.Name = poiName;

        // Create fresh TopCell
        Worldspace.TopCell = new Cell(targetMod)
        {
            Flags = Cell.Flag.HasWater,
            Grid = new CellGrid(),
            WaterHeight = -200,
            XILS = 1,
            MajorFlags = Cell.MajorFlag.Persistent,
            Persistent = new ExtendedList<IPlaced>()
        };

        // Create fresh subcells
        int cellid = 0;
        foreach (var sbc in Worldspace.SubCells)
        {
            var point = sbc.Items[0].Items[0].Grid.Point;
            sbc.Items[0].Items[0] = new Cell(targetMod)
            {
                EditorID = editorId + "cell" + cellid++,
                Grid = new CellGrid() { Point = point },
                Flags = Cell.Flag.HasWater,
                XILS = 1,
                Temporary = new ExtendedList<IPlaced>(),
                WaterHeight = -200,
            };
        }

        // Run generation
        var generator = new WorldspaceDungeonGenerator(design);
        State = generator.Generate(Worldspace, Location, seed);
    }
}
