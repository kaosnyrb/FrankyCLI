using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Generator;
using System;
using System.Collections.Generic;

namespace Retrograde.Nouns.SpaceCells;

/// <summary>
/// Creates a procedurally-generated space cell (CELL record) and a companion
/// LeveledSpaceCell (LVSC) containing it.
///
/// Uses vanilla scGenRocky04 (CELL:00138C3E) as a structural template:
///   - Preserves Persistent items (Origin Point, patrol markers, ship markers)
///   - Extracts asteroid Static palette from Temporary items
///   - Starts Temporary empty — content passes populate it
/// </summary>
public class SpaceCellNoun
{
    // scGenRocky04 "Rocky Asteroids" — CELL:00138C3E in Starfield.esm
    private const uint SourceCellFormId = 0x00138C3E;

    // SE_AreaTrigger — vanilla area trigger tied to the source cell's encounter zone.
    // We never want this in a generated cell (it would reference the wrong trigger volume).
    private const uint AreaTriggerFormId = 0x001DEE0C;

    public Cell Cell { get; }
    public Location Location { get; }
    public LeveledSpaceCell LeveledSpaceCell { get; }
    public SpaceCellState State { get; }

    public SpaceCellNoun(string name, SpaceCellPalette palette = SpaceCellPalette.Rocky)
    {
        var targetMod    = RetrogradeContext.Current.TargetMod;
        var starfieldMod = RetrogradeContext.Current.StarfieldMod;

        // ── 1. Find vanilla source cell ──────────────────────────────────────────
        ICellGetter? sourceGetter = null;
        foreach (var block in starfieldMod.Cells)
            foreach (var sub in block.SubBlocks)
                foreach (var c in sub.Cells)
                    if (c.FormKey.ID == SourceCellFormId) { sourceGetter = c; goto Found; }
        Found:

        if (sourceGetter == null)
            throw new InvalidOperationException(
                $"SpaceCellNoun: scGenRocky04 (0x{SourceCellFormId:X8}) not found in Starfield.esm.");

        Console.WriteLine($"[SpaceCellNoun] Source cell: {sourceGetter.EditorID} ({sourceGetter.FormKey})");

        // DeepCopy() → fully mutable Cell; avoids IXxxGetter type mismatches throughout.
        Cell srcCell = sourceGetter.DeepCopy();

        // ── 2. Scan Temporary for marker templates and vanilla radius ─────────────
        // Asteroids  → update vanillaRadius only (palette is hardcoded below).
        // Everything else → marker templates (SpaceMarkersPass clones these verbatim).
        // The AreaTrigger is dropped entirely — it's tied to the vanilla encounter zone.
        var markerTemplates = new List<PlacedObject>();
        float vanillaRadius = 1000f;

        foreach (var item in srcCell.Temporary)
        {
            if (item is not PlacedObject po) continue;
            if (po.Base.IsNull) continue;
            if (po.Base.FormKey.ID == AreaTriggerFormId) continue;

            if (starfieldMod.MoveableStatics.ContainsKey(po.Base.FormKey))
            {
                var p = po.Position;
                float d = MathF.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
                if (d > vanillaRadius) vanillaRadius = d;
            }
            else
            {
                markerTemplates.Add(po);
            }
        }

        // ── Build asteroid palette from hardcoded FormIDs ─────────────────────────
        var asteroidPalette = SpaceCellPaletteData.GetFormKeys(
            palette, RetrogradeContext.Current.StarfieldModKey);

        Console.WriteLine($"[SpaceCellNoun] Palette: {palette} ({asteroidPalette.Count} types), " +
                          $"vanilla radius: {vanillaRadius:F0}");

        // ── 3. Create Location ───────────────────────────────────────────────────
        string safe = System.Text.RegularExpressions.Regex
            .Replace(name.ToLower(), "[^a-z0-9]", "");

        Location = new Location(targetMod)
        {
            EditorID = $"rg_sc_{safe}_loc",
            Name     = name,
            LocationCellUniqueReferences = new ExtendedList<LocationCellUniqueReference>(),
        };
        targetMod.Locations.Add(Location);

        // ── 4. Clone Persistent items with fresh FormKeys ────────────────────────
        // Copies the Origin Point, ship markers, patrol markers from the vanilla cell.
        var persistent = new ExtendedList<IPlaced>();
        foreach (var item in srcCell.Persistent)
        {
            if (item is not PlacedObject srcPO) continue;

            // New PlacedObject gets a fresh FormKey; copy all fields from mutable srcPO.
            var po = new PlacedObject(targetMod)
            {
                Action                    = srcPO.Action,
                AttachRef                 = srcPO.AttachRef,
                Base                      = srcPO.Base,
                BlueprintPartOrigin       = srcPO.BlueprintPartOrigin,
                BOLV                      = srcPO.BOLV,
                Collision                 = srcPO.Collision,
                Comments                  = srcPO.Comments,
                Components                = srcPO.Components,
                ConstrainedDecal          = srcPO.ConstrainedDecal,
                Count                     = srcPO.Count,
                CurrentZoneCell           = srcPO.CurrentZoneCell,
                DebugText                 = srcPO.DebugText,
                EditorID                  = srcPO.EditorID != null
                                               ? srcPO.EditorID + RandomProvider.Random.Next(10000)
                                               : null,
                EnableParent              = srcPO.EnableParent,
                ExternalEmittance         = srcPO.ExternalEmittance,
                FactionRank               = srcPO.FactionRank,
                GeometryDirtinessScale    = srcPO.GeometryDirtinessScale,
                GroupedPackIn             = srcPO.GroupedPackIn,
                HeadTrackingWeight        = srcPO.HeadTrackingWeight,
                HealthPercent             = srcPO.HealthPercent,
                IsActivationPoint         = srcPO.IsActivationPoint,
                IsIgnoredBySandbox        = srcPO.IsIgnoredBySandbox,
                IsLinkedRefTransient      = srcPO.IsLinkedRefTransient,
                LayeredMaterialSwaps      = srcPO.LayeredMaterialSwaps,
                LevelModifier             = srcPO.LevelModifier,
                LightArea                 = srcPO.LightArea,
                LightBarndoorData         = srcPO.LightBarndoorData,
                LightColors               = srcPO.LightColors,
                LightFlicker              = srcPO.LightFlicker,
                GoboAnimatedProperties    = srcPO.GoboAnimatedProperties,
                Lighting                  = srcPO.Lighting,
                LightLayerData            = srcPO.LightLayerData,
                LightRoundedness          = srcPO.LightRoundedness,
                LightStaticShadowMap      = srcPO.LightStaticShadowMap,
                LightVolumetricData       = srcPO.LightVolumetricData,
                LinkedReferences          = srcPO.LinkedReferences,
                LocationRefTypes          = srcPO.LocationRefTypes,
                Lock                      = srcPO.Lock,
                MapMarker                 = srcPO.MapMarker,
                NavigationDoorLink        = srcPO.NavigationDoorLink,
                NumTraversalFluffBytes    = srcPO.NumTraversalFluffBytes,
                OpenByDefault             = srcPO.OpenByDefault,
                Ownership                 = srcPO.Ownership,
                Patrol                    = srcPO.Patrol,
                Position                  = srcPO.Position,
                PowerLinks                = srcPO.PowerLinks,
                Primitive                 = srcPO.Primitive,
                ProjectedDecal            = srcPO.ProjectedDecal,
                ProjectedDecalReferences  = srcPO.ProjectedDecalReferences,
                Radius                    = srcPO.Radius,
                RagdollBipedRotation      = srcPO.RagdollBipedRotation,
                Properties                = srcPO.Properties,
                RagdollData               = srcPO.RagdollData,
                StarfieldMajorRecordFlags = srcPO.StarfieldMajorRecordFlags,
                Rotation                  = srcPO.Rotation,
                Scale                     = srcPO.Scale,
                ShipArrival               = srcPO.ShipArrival,
                SnapLinks                 = srcPO.SnapLinks,
                TeleportDestination       = srcPO.TeleportDestination,
                Spline                    = srcPO.Spline,
                TimeOfDay                 = srcPO.TimeOfDay,
                Traversals                = srcPO.Traversals,
                VolumeData                = srcPO.VolumeData,
                VirtualMachineAdapter     = srcPO.VirtualMachineAdapter,
                XALG                      = srcPO.XALG,
                PlacedObjectXCZRXCZA      = srcPO.PlacedObjectXCZRXCZA,
                XFLG                      = srcPO.XFLG,
                XNSE                      = srcPO.XNSE,
                XPCK                      = srcPO.XPCK,
            };
            // FormLink fields assigned after construction (never in initializer).
            if (!srcPO.Emittance.IsNull)
                po.Emittance = srcPO.Emittance.FormKey.ToNullableLink<IEmittanceGetter>();
            if (!srcPO.Layer.IsNull)
                po.Layer = srcPO.Layer.FormKey.ToNullableLink<ILayerGetter>();
            if (!srcPO.PersistentLocation.IsNull)
                po.PersistentLocation = srcPO.PersistentLocation.FormKey.ToNullableLink<ILocationGetter>();
            if (!srcPO.ReferenceGroup.IsNull)
                po.ReferenceGroup = srcPO.ReferenceGroup.FormKey.ToNullableLink<IReferenceGroupGetter>();
            if (!srcPO.SourcePackIn.IsNull)
                po.SourcePackIn = srcPO.SourcePackIn.FormKey.ToNullableLink<IPackInGetter>();
            if (!srcPO.EncounterZone.IsNull)
                po.EncounterZone = srcPO.EncounterZone;

            persistent.Add(po);
        }

        // ── 5. Construct Cell with fresh FormKey ─────────────────────────────────
        // srcCell is a fully mutable Cell (from DeepCopy), so direct struct copies in
        // the initializer are safe — we only avoid calling .ToNullableLink<>()/SetTo()
        // inside the initializer (those calls crash). Location is set post-construction
        // since it points to OUR new location record, not the vanilla one.
        Cell = new Cell(targetMod)
        {
            EditorID                   = $"rg_sc_{safe}",
            Name                       = name,
            AcousticSpace              = srcCell.AcousticSpace,
            CellSkyRegion              = srcCell.CellSkyRegion,
            Components                 = srcCell.Components,
            EnvironmentMap             = srcCell.EnvironmentMap,
            Flags                      = srcCell.Flags,
            GlobalDirtLayerMaterial    = srcCell.GlobalDirtLayerMaterial,
            ImageSpace                 = srcCell.ImageSpace,
            IsLinkedRefTransient       = srcCell.IsLinkedRefTransient,
            Lighting                   = srcCell.Lighting?.DeepCopy(),
            LightingTemplate           = srcCell.LightingTemplate,
            LinkedReferences           = srcCell.LinkedReferences,
            MajorFlags                 = srcCell.MajorFlags,
            Music                      = srcCell.Music,
            Ownership                  = srcCell.Ownership?.DeepCopy(),
            Persistent                 = persistent,
            Temporary                  = new ExtendedList<IPlaced>(),
            TimeOfDay                  = srcCell.TimeOfDay,
            PersistentTimestamp        = srcCell.PersistentTimestamp,
            PersistentUnknownGroupData = srcCell.PersistentUnknownGroupData,
            Timestamp                  = srcCell.Timestamp,
            Water                      = srcCell.Water,
            WaterEnvironmentMap        = srcCell.WaterEnvironmentMap,
            WaterHeight                = srcCell.WaterHeight,
            WaterType                  = srcCell.WaterType,
            WaterVelocity              = srcCell.WaterVelocity,
            XCLAs                      = srcCell.XCLAs,
            XILS                       = srcCell.XILS,
        };
        // Location points to our new record — must use ToNullableLink, so set post-construction.
        Cell.Location = Location.ToNullableLink<ILocationGetter>();

        Console.WriteLine($"[SpaceCellNoun] Cell {Cell.EditorID}: " +
                          $"{persistent.Count} persistent items");

        // ── 6. Register cell in mod ──────────────────────────────────────────────
        AddCellToMod(targetMod, Cell);

        // ── 7. Create LeveledSpaceCell ───────────────────────────────────────────
        LeveledSpaceCell = new LeveledSpaceCell(targetMod)
        {
            EditorID   = $"rg_lvsc_{safe}",
            ChanceNone = 0,
        };
        LeveledSpaceCell.Entries = new ExtendedList<LeveledNpcEntry>
        {
            new LeveledNpcEntry { Level = 1, Count = 1 }
        };
        LeveledSpaceCell.Entries[0].Reference.SetTo(Cell.FormKey);
        targetMod.LeveledSpaceCells.Add(LeveledSpaceCell);

        Console.WriteLine($"[SpaceCellNoun] LVSC: {LeveledSpaceCell.EditorID}");

        // ── 8. Run content passes ────────────────────────────────────────────────
        var generator = new SpaceCellGenerator();
        State = generator.Generate(Cell, Location, asteroidPalette, markerTemplates, vanillaRadius,
            SpaceCellPaletteData.GetScale(palette));

        Console.WriteLine($"[SpaceCellNoun] Done — {Cell.Temporary.Count} asteroids.");
    }

    private static void AddCellToMod(StarfieldMod targetMod, Cell cell)
    {
        var keyStr   = cell.FormKey.ID.ToString();
        int blockNum = int.Parse(keyStr.Substring(keyStr.Length - 1));
        int subNum   = int.Parse(keyStr.Substring(keyStr.Length - 2, 1));

        CellBlock block = null;
        foreach (var b in targetMod.Cells)
            if (b.BlockNumber == blockNum) { block = b; break; }
        if (block == null)
        {
            block = new CellBlock
            {
                BlockNumber = blockNum,
                GroupType   = GroupTypeEnum.InteriorCellBlock,
                SubBlocks   = new ExtendedList<CellSubBlock>(),
            };
            targetMod.Cells.Add(block);
        }

        CellSubBlock sub = null;
        foreach (var s in block.SubBlocks)
            if (s.BlockNumber == subNum) { sub = s; break; }
        if (sub == null)
        {
            sub = new CellSubBlock
            {
                BlockNumber = subNum,
                GroupType   = GroupTypeEnum.InteriorCellSubBlock,
                Cells       = new ExtendedList<Cell>(),
            };
            block.SubBlocks.Add(sub);
        }

        sub.Cells.Add(cell);
    }
}
