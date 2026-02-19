using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Generator;
using Retrograde.Passes.Worldspace;
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

        // Resolve the template worldspace's SurfaceBlock — it provides the source BTD path,
        // FNAM (required binary field), and NAM5 (parent standalone SurfaceBlock link).
        var templateWorldspace = FindInTemplateMods(
            RetrogradeContext.Current.TemplateMods,
            m => m.Worldspaces,
            design.TemplateWorldspaceEditorId);

        ISurfaceBlockGetter? templateSurfaceBlock = null;
        if (templateWorldspace != null)
        {
            var overlayComp = templateWorldspace.Components
                ?.OfType<IWorldSpaceOverlayComponentGetter>()
                .FirstOrDefault();
            if (overlayComp?.SurfaceBlock?.FormKey is FormKey sbFormKey && !sbFormKey.IsNull)
            {
                templateSurfaceBlock = FindInTemplateMods(
                    RetrogradeContext.Current.TemplateMods,
                    m => m.SurfaceBlocks,
                    sbFormKey);
            }
        }

        if (templateSurfaceBlock == null && !RetrogradeContext.Quiet)
            Console.WriteLine($"Warning: could not resolve SurfaceBlock for template worldspace '{design.TemplateWorldspaceEditorId}'");

        // Derive cell grid size from the template SurfaceBlock's DNAM (cols x rows).
        // Falls back to 4 if the template couldn't be resolved.
        int cellGridSize = (int)(templateSurfaceBlock?.DNAM?.First ?? 4);
        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"Cell grid size from template: {cellGridSize}x{cellGridSize}");

        // Create SurfaceBlock from scratch
        var newBlock = new SurfaceBlock(targetMod)
        {
            NAM1 = "OverlayBlock",
            // NAM5 comes from the template worldspace's own SurfaceBlock so both overlay
            // blocks share the same standalone parent (size / terrain type must match).
            NAM5 = templateSurfaceBlock?.NAM5?.FormKey is FormKey nam5Key && !nam5Key.IsNull
                ? nam5Key.ToNullableLink<ISurfaceBlockGetter>()
                : new FormKey(starfieldEsm, 0x002C586B).ToNullableLink<ISurfaceBlockGetter>(),
            DNAM = new SurfaceBlockIntItem() { First = (uint)cellGridSize, Second = (uint)cellGridSize },
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

        // Copy FNAM from the template SurfaceBlock — all vanilla overlay SurfaceBlocks
        // carry this binary field; without it the record may not load correctly in the engine.
        if (templateSurfaceBlock?.FNAM != null)
            newBlock.FNAM = templateSurfaceBlock.FNAM.Value.ToArray();

        // Copy terrain file if data folder path is provided
        string newTerrainFile = "Data\\Terrain\\" + editorId + ".btd";
        if (dataFolderPath != null)
        {
            // Derive source filename from the template SurfaceBlock's ANAM; fall back to
            // the worldspace EditorID convention if ANAM is unavailable.
            string sourceBtdFile = templateSurfaceBlock?.ANAM is string anam
                ? Path.GetFileName(anam).ToLowerInvariant()
                : design.TemplateWorldspaceEditorId.ToLowerInvariant() + ".btd";
            string sourceTerrainPath = Path.Combine(dataFolderPath, "Terrain", sourceBtdFile);
            if (!File.Exists(sourceTerrainPath))
                throw new FileNotFoundException(
                    $"Source terrain file not found: {sourceTerrainPath}\n" +
                    $"You need to unpack '{sourceBtdFile}' from the Starfield terrain BSA (Starfield - Terrain*.ba2) into your Data\\Terrain\\ folder.");

            string destTerrainPath = Path.Combine(dataFolderPath, "Terrain", editorId + ".btd");
            if (File.Exists(destTerrainPath))
                File.Delete(destTerrainPath);
            File.Copy(sourceTerrainPath, destTerrainPath);
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

        int halfGrid = cellGridSize / 2;

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

        // Open BTD to set ENAM from actual header values and sample terrain height
        // for object placement.
        if (dataFolderPath == null)
            throw new InvalidOperationException("dataFolderPath is required to read terrain data.");

        string btdPath = Path.Combine(dataFolderPath, "Terrain", editorId + ".btd");
        if (!File.Exists(btdPath))
            throw new FileNotFoundException($"Terrain file not found: {btdPath}");

        var btd = new BtdFile(btdPath);

        // Override the fallback ENAM with actual values from the BTD header.
        newBlock.ENAM = new SurfaceBlockFloatItem()
        {
            First  = BitConverter.SingleToUInt32Bits(btd.WorldHeightMin / 8f),
            Second = BitConverter.SingleToUInt32Bits(btd.WorldHeightMax / 8f),
        };

        // Sample at the BTD's own grid centre — (0,0) in BTD-absolute coordinates
        // is outside the cell range for template BTDs with large cell offsets.
        float terrainHeight = btd.SampleHeightAtWorld(btd.WorldCenterX, btd.WorldCenterY) / 8f;
        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"Terrain height at center: {terrainHeight}");

        // Run generation (pass btd so terrain passes can modify it)
        var generator = new WorldspaceDungeonGenerator(design);
        State = generator.Generate(Worldspace, Location, seed, terrainHeight, btd, btdPath);

        // Save terrain edits made by any terrain passes
        if (State.BtdFile != null && State.BtdPath != null)
            State.BtdFile.Save(State.BtdPath, updateMinMax: false);
    }

    /// <summary>
    /// Safely searches across template mods for a record by EditorID.
    /// Catches Mutagen SubrecordExceptions from malformed records in mods
    /// that don't have valid entries for the given collection.
    /// </summary>
    private static T? FindInTemplateMods<T>(
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

    /// <summary>
    /// Safely searches across template mods for a record by FormKey.
    /// </summary>
    private static T? FindInTemplateMods<T>(
        System.Collections.Generic.IReadOnlyList<IStarfieldModGetter> templateMods,
        Func<IStarfieldModGetter, IEnumerable<T>> collectionSelector,
        FormKey formKey) where T : class, IMajorRecordGetter
    {
        foreach (var mod in templateMods)
        {
            try
            {
                foreach (var record in collectionSelector(mod))
                {
                    try
                    {
                        if (record.FormKey == formKey)
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
