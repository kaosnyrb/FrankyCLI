using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Generator;
using Retrograde.Passes;
using Retrograde.Utils;
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
        };

        Location.Keywords.Add(LocTypeDungeon);
        Location.Keywords.Add(LocTypeClearable);
        Location.Keywords.Add(LocTypeOE_Keyword);
        Location.Keywords.Add(LocEncSpacers_Exclusive);
        Location.Keywords.Add(LocTypeOverlay);
        targetMod.Locations.Add(Location);

        // Create SurfaceBlock from scratch
        var newBlock = new SurfaceBlock(targetMod)
        {
            NAM1 = "OverlayBlock",
            // NAM5 must point to a standalone overlay source block with the same DNAM size.
            // OverlayFieldsDunesMedium01 [2C586B] is a 4x4 source; matches CellGridSize=4.
            NAM5 = new FormKey(starfieldEsm, 0x002C586B).ToNullableLink<ISurfaceBlockGetter>(),
            DNAM = new SurfaceBlockIntItem() { First = (uint)design.CellGridSize, Second = (uint)design.CellGridSize },
            WHGT = float.MinValue,
            GNAM = 0,
            HNAM = 0,
            INAM = 0,
            JNAM = 0,
            KNAM = 0,
            NAM2 = 0,
            Version2 = 5,
        };
        targetMod.SurfaceBlocks.Add(newBlock);

        // Copy FNAM from the parent overlay source block — all vanilla overlay SurfaceBlocks
        // carry this binary field; without it the record may not load correctly in the engine.
        var parentBlock = FindInTemplateMods(
            RetrogradeContext.Current.TemplateMods,
            m => m.SurfaceBlocks,
            "OverlayFieldsDunesMedium01");
        if (parentBlock?.FNAM != null)
            newBlock.FNAM = parentBlock.FNAM.Value.ToArray();

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

        // Template BTDs all use (-500, 1000) unscaled — set as fallback immediately.
        // SurfaceBlockFloatItem stores raw IEEE 754 bits as uint.
        newBlock.ENAM = new SurfaceBlockFloatItem()
        {
            First  = BitConverter.SingleToUInt32Bits(-500f),
            Second = BitConverter.SingleToUInt32Bits(1000f),
        };

        int halfGrid = design.CellGridSize / 2;

        // Create worldspace from scratch (matching OEBB029World reference values)
        Worldspace = new Worldspace(targetMod)
        {
            EditorID = editorId,
            Name = poiName,
            Flags = Worldspace.Flag.SmallWorld,
            Location = Location.ToNullableLink<ILocationGetter>(),
            LandDefaults = new WorldspaceLandDefaults()
            {
                DefaultLandHeight = -2048,
                DefaultWaterHeight = -200,
            },
            Climate = new FormKey(starfieldEsm, 0x00015F).ToNullableLink<IClimateGetter>(),
            Water = new FormKey(starfieldEsm, 0x000018).ToNullableLink<IWaterGetter>(),
            LodWater = new FormKey(starfieldEsm, 0x000018).ToNullableLink<IWaterGetter>(),
            LodWaterHeight = 0,
            Components = new ExtendedList<AComponent>
            {
                new WorldSpaceOverlayComponent()
                {
                    SurfaceBlock = newBlock.ToNullableLink<ISurfaceBlockGetter>(),
                },
                new PlanetContentManagerContentPropertiesComponent(),
            },
            MapData = new WorldspaceMap()
            {
                UsableDimensions = new P2Int(0, 0),
                NorthwestCellCoords = new P2Int16((short)-halfGrid, (short)(halfGrid - 1)),
                SoutheastCellCoords = new P2Int16((short)(halfGrid - 1), (short)-halfGrid),
            },
            GNAM = 1f,
            DistantLodMultiplier = 1f,
            Version2 = 10,
            WorldMapOffsetScale = 1f,
        };
        targetMod.Worldspaces.Add(Worldspace);

        // Create fresh TopCell
        Worldspace.TopCell = new Cell(targetMod)
        {
            Flags = Cell.Flag.HasWater,
            Grid = new CellGrid(),
            WaterHeight = float.MaxValue,
            XILS = 1,
            Version2 = 2,
            MajorFlags = Cell.MajorFlag.Persistent,
            Persistent = new ExtendedList<IPlaced>()
        };

        // Create subcell grid derived from CellGridSize
        // Cell coords range from -(gridSize/2) to (gridSize/2 - 1)
        int cellid = 0;
        for (int cy = -halfGrid; cy < halfGrid; cy++)
        {
            for (int cx = -halfGrid; cx < halfGrid; cx++)
            {
                var point = new P2Int(cx, cy);
                var cell = new Cell(targetMod)
                {
                    EditorID = editorId + "cell" + cellid++,
                    Grid = new CellGrid() { Point = point },
                    Flags = Cell.Flag.HasWater,
                    XILS = 1,
                    Temporary = new ExtendedList<IPlaced>(),
                    WaterHeight = -200,
                };
                var subBlock = new WorldspaceSubBlock()
                {
                    BlockNumberX = (short)cx,
                    BlockNumberY = (short)cy,
                    GroupType = GroupTypeEnum.ExteriorCellSubBlock,
                    Items = new ExtendedList<Cell> { cell },
                };
                var block = new WorldspaceBlock()
                {
                    BlockNumberX = (short)cx,
                    BlockNumberY = (short)cy,
                    GroupType = GroupTypeEnum.ExteriorCellBlock,
                    Items = new ExtendedList<WorldspaceSubBlock> { subBlock },
                };
                Worldspace.SubCells.Add(block);
            }
        }

        // Open BTD to set ENAM from actual header values, flatten terrain to height 0
        // (template BTD carries non-zero heights from oejm001world which would appear as
        // a mountain), and sample the final terrain height for object placement.
        float terrainHeight = 0;
        if (dataFolderPath != null)
        {
            string btdPath = Path.Combine(dataFolderPath, "Terrain", editorId + ".btd");
            if (File.Exists(btdPath))
            {
                var btd = new BtdFile(btdPath);

                // Override the fallback ENAM with actual values from the BTD header.
                newBlock.ENAM = new SurfaceBlockFloatItem()
                {
                    First  = BitConverter.SingleToUInt32Bits(btd.WorldHeightMin / 8f),
                    Second = BitConverter.SingleToUInt32Bits(btd.WorldHeightMax / 8f),
                };

                // Flatten all non-edge cells to height 0 so the overlay sits flush with
                // the surrounding planet surface instead of appearing as a mountain.
                ushort flatRaw = btd.HeightToRaw(0f);
                var flatBuf = new ushort[128 * 128];
                Array.Fill(flatBuf, flatRaw);
                for (int cy = btd.CellMinY + 1; cy <= btd.CellMaxY - 1; cy++)
                    for (int cx = btd.CellMinX + 1; cx <= btd.CellMaxX - 1; cx++)
                        btd.SetCellHeightMap(flatBuf, cx, cy);
                btd.SmoothDirtyCellEdges(32);
                btd.Save(btdPath, updateMinMax: false);

                terrainHeight = btd.SampleHeightAtWorld(0, 0) / 8f;
                if (!RetrogradeContext.Quiet)
                    Console.WriteLine($"Terrain height at center: {terrainHeight}");
            }
        }

        // Run generation
        var generator = new WorldspaceDungeonGenerator(design);
        State = generator.Generate(Worldspace, Location, seed, terrainHeight);
    }

    /// <summary>
    /// Safely searches across template mods for a record by EditorID.
    /// Catches Mutagen SubrecordExceptions from malformed records in mods
    /// that don't have valid entries for the given collection.
    /// </summary>
    private static T FindInTemplateMods<T>(
        System.Collections.Generic.IReadOnlyList<IStarfieldModGetter> templateMods,
        Func<IStarfieldModGetter, IEnumerable<T>> collectionSelector,
        string editorId) where T : class, IMajorRecordGetter
    {
        foreach (var mod in templateMods)
        {
            try
            {
                foreach (var record in collectionSelector(mod))
                {
                    try
                    {
                        if (record.EditorID == editorId)
                            return record;
                    }
                    catch { }
                }
            }
            catch { }
        }
        return null;
    }
}
