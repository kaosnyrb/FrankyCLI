using System;
using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;

namespace Retrograde;

/// <summary>
/// Provides a shared Random instance for the Retrograde library.
/// Can be replaced by the host application if needed.
/// </summary>
public static class RandomProvider
{
    private static Random _random = new Random();

    /// <summary>
    /// The shared Random instance used throughout the library.
    /// </summary>
    public static Random Random
    {
        get => _random;
        set => _random = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Resets to a new Random instance with a random seed.
    /// </summary>
    public static void Reset()
    {
        _random = new Random();
    }

    /// <summary>
    /// Resets to a new Random instance with the specified seed.
    /// </summary>
    public static void Reset(int seed)
    {
        _random = new Random(seed);
    }

    /// <summary>
    /// Gets a random marker record matching the specified name, searching TargetMod then TemplateMods.
    /// If the record is found only in a template mod it is cloned into TargetMod (as a cell override)
    /// before being returned.
    /// </summary>
    public static IMajorRecordGetter GetRandomMarker(string name)
    {
        var ctx = RetrogradeContext.Current;
        var rec = new List<IMajorRecordGetter>();

        foreach (var record in ctx.TargetMod.EnumerateMajorRecords())
            if (record.EditorID?.Contains(name) == true)
                rec.Add(record);

        if (rec.Count > 0)
            return rec[Random.Next(rec.Count)];

        var templateCandidates = new List<IMajorRecordGetter>();
        foreach (var mod in ctx.TemplateMods)
            foreach (var record in mod.EnumerateMajorRecords())
                if (record.EditorID?.Contains(name) == true)
                    templateCandidates.Add(record);

        if (templateCandidates.Count == 0)
            throw new InvalidOperationException($"GetRandomMarker: no records found matching '{name}'");

        var chosen = templateCandidates[Random.Next(templateCandidates.Count)];
        return CloneIntoMod(chosen, ctx.TargetMod);
    }

    private static IMajorRecordGetter CloneIntoMod(IMajorRecordGetter source, StarfieldMod targetMod)
    {
        switch (source)
        {
            case IStaticGetter s:
                var sClone = s.DeepCopy();
                targetMod.Statics.Add(sClone);
                return sClone;
            case IActivatorGetter a:
                var aClone = a.DeepCopy();
                targetMod.Activators.Add(aClone);
                return aClone;
            case IPlacedObjectGetter po:
                return ClonePlacedObjectAsNew(po, targetMod);
            default:
                throw new InvalidOperationException(
                    $"GetRandomMarker: cannot clone record type {source.GetType().Name} — add a case to CloneIntoMod");
        }
    }

    private static PlacedObject ClonePlacedObjectAsNew(IPlacedObjectGetter source, StarfieldMod targetMod)
    {
        var ctx = RetrogradeContext.Current;

        // The parent cell may be in a different mod from the marker — search all available mods.
        var allMods = new List<IStarfieldModGetter>(ctx.TemplateMods) { ctx.StarfieldMod };

        foreach (var mod in allMods)
        {
            // Search interior cells
            foreach (var block in mod.Cells)
                foreach (var subBlock in block.SubBlocks)
                    foreach (var cell in subBlock.Cells)
                    {
                        var po = TryCreateNewInInteriorCell(source, cell, targetMod);
                        if (po != null) return po;
                    }

            // Search worldspace top cells and exterior cells
            foreach (var ws in mod.Worldspaces)
            {
                // Top cell (persistent world-children group) — city markers live here
                if (ws.TopCell != null)
                {
                    var po = TryCreateNewInWorldspaceTopCell(source, ws.TopCell, ws, targetMod);
                    if (po != null) return po;
                }

                foreach (var wsBlock in ws.SubCells)
                    foreach (var wsSubBlock in wsBlock.Items)
                        foreach (var cell in wsSubBlock.Items)
                        {
                            var po = TryCreateNewInWorldspaceCell(source, cell, wsBlock, wsSubBlock, ws, targetMod);
                            if (po != null) return po;
                        }
            }
        }

        throw new InvalidOperationException(
            $"GetRandomMarker: cannot find parent cell for PlacedObject {source.EditorID} ({source.FormKey})");
    }

    /// <summary>
    /// Creates a brand-new PlacedObject in TargetMod with a fresh FormKey, copying all fields
    /// from the source getter. Never uses DeepCopy() so the result is independent of the template.
    /// FormLinkNullable fields are set after construction per Mutagen rules.
    /// </summary>
    private static PlacedObject CreateNewPlacedObject(IPlacedObjectGetter source, StarfieldMod targetMod)
    {
        var placed = new PlacedObject(targetMod)
        {
            Collision              = source.Collision?.DeepCopy(),
            Comments               = source.Comments,
            Components             = source.Components?.Select(x => x.DeepCopy()).ToExtendedList(),
            ConstrainedDecal       = source.ConstrainedDecal,                   // P3Float value type
            Count                  = source.Count,
            DebugText              = source.DebugText?.DeepCopy(),
            EditorID               = source.EditorID,
            EnableParent           = source.EnableParent?.DeepCopy(),
            // EncounterZone is FormLinkNullable — set after construction
            ExternalEmittance      = source.ExternalEmittance?.DeepCopy(),       // complex sub-record
            FactionRank            = source.FactionRank,
            GeometryDirtinessScale = source.GeometryDirtinessScale,
            HeadTrackingWeight     = source.HeadTrackingWeight,
            HealthPercent          = source.HealthPercent,
            IsActivationPoint      = source.IsActivationPoint,
            IsIgnoredBySandbox     = source.IsIgnoredBySandbox,
            IsLinkedRefTransient   = source.IsLinkedRefTransient,
            LayeredMaterialSwaps   = source.LayeredMaterialSwaps?.ToExtendedList(),
            LevelModifier          = source.LevelModifier,
            LightArea              = source.LightArea?.DeepCopy(),
            LightBarndoorData      = source.LightBarndoorData?.DeepCopy(),
            LightColors            = source.LightColors?.Select(x => x.DeepCopy()).ToExtendedList(),
            LightFlicker           = source.LightFlicker?.DeepCopy(),
            Lighting               = source.Lighting?.DeepCopy(),
            LightLayerData         = source.LightLayerData,                      // bool value type
            LightRoundedness       = source.LightRoundedness?.DeepCopy(),
            LightStaticShadowMap   = source.LightStaticShadowMap,                // bool value type
            LightVolumetricData    = source.LightVolumetricData,                 // float value type
            LinkedReferences       = source.LinkedReferences?.Select(x => x.DeepCopy()).ToExtendedList(),
            LocationRefTypes       = source.LocationRefTypes?.ToExtendedList(),
            Lock                   = source.Lock?.DeepCopy(),
            MapMarker              = source.MapMarker?.DeepCopy(),
            NavigationDoorLink     = source.NavigationDoorLink?.DeepCopy(),
            NumTraversalFluffBytes = source.NumTraversalFluffBytes,
            OpenByDefault          = source.OpenByDefault,
            Ownership              = source.Ownership?.DeepCopy(),
            Patrol                 = source.Patrol?.DeepCopy(),
            Position               = source.Position,
            PowerLinks             = source.PowerLinks?.Select(x => x.DeepCopy()).ToExtendedList()!,
            Primitive              = source.Primitive?.DeepCopy(),
            ProjectedDecal         = source.ProjectedDecal?.DeepCopy(),
            ProjectedDecalReferences = source.ProjectedDecalReferences?.ToExtendedList(),
            Properties             = source.Properties?.Select(x => x.DeepCopy()).ToExtendedList(),
            Radius                 = source.Radius,
            RagdollBipedRotation   = source.RagdollBipedRotation,                // P3Float value type
            RagdollData            = source.RagdollData?.Select(x => x.DeepCopy()).ToExtendedList(),
            Rotation               = source.Rotation,
            Scale                  = source.Scale,
            ShipArrival            = source.ShipArrival?.DeepCopy(),
            SnapLinks              = source.SnapLinks?.Select(x => x.DeepCopy()).ToExtendedList(),
            Spline                 = source.Spline?.DeepCopy(),
            StarfieldMajorRecordFlags = source.StarfieldMajorRecordFlags,
            TeleportDestination    = source.TeleportDestination?.DeepCopy(),
            Traversals             = source.Traversals?.Select(x => x.DeepCopy()).ToExtendedList(),
            VirtualMachineAdapter  = source.VirtualMachineAdapter?.DeepCopy(),
            VolumeData             = source.VolumeData?.DeepCopy(),
            XALG                   = source.XALG,
            XNSE                   = source.XNSE?.ToArray(),                     // ReadOnlyMemorySlice → byte[]
        };
        // FormLinkNullable fields must be set after construction (Mutagen rule)
        if (!source.Base.IsNull)               placed.Base               = source.Base.FormKey.ToNullableLink<IPlaceableObjectGetter>();
        if (!source.Emittance.IsNull)          placed.Emittance          = source.Emittance.FormKey.ToNullableLink<IEmittanceGetter>();
        if (!source.EncounterZone.IsNull)      placed.EncounterZone      = source.EncounterZone.FormKey.ToNullableLink<ILocationGetter>();
        if (!source.Layer.IsNull)              placed.Layer              = source.Layer.FormKey.ToNullableLink<ILayerGetter>();
        if (!source.PersistentLocation.IsNull) placed.PersistentLocation = source.PersistentLocation.FormKey.ToNullableLink<ILocationGetter>();
        if (!source.TeleportName.IsNull)       placed.TeleportName       = source.TeleportName.FormKey.ToNullableLink<IMessageGetter>();
        if (!source.TimeOfDay.IsNull)          placed.TimeOfDay          = source.TimeOfDay.FormKey.ToNullableLink<ITimeOfDayRecordGetter>();
        return placed;
    }

    /// <summary>
    /// Returns the worldspace from the mod that originally created it (FormKey.ModKey).
    /// Avoids using a template override as the DeepCopy base.
    /// </summary>
    private static IWorldspaceGetter FindOriginalWorldspace(FormKey fk)
    {
        var ctx = RetrogradeContext.Current;
        IStarfieldModGetter? owner = fk.ModKey == ctx.StarfieldModKey
            ? ctx.StarfieldMod
            : ctx.TemplateMods.FirstOrDefault(m => m.ModKey == fk.ModKey);
        return owner?.Worldspaces.FirstOrDefault(w => w.FormKey == fk)
            ?? throw new InvalidOperationException($"GetRandomMarker: worldspace {fk} not found in owning mod {fk.ModKey}");
    }

    /// <summary>
    /// Returns the cell from the mod that originally created it (FormKey.ModKey).
    /// Searches interior cell blocks then worldspace cells.
    /// </summary>
    private static ICellGetter FindOriginalCell(FormKey fk)
    {
        var ctx = RetrogradeContext.Current;
        IStarfieldModGetter? owner = fk.ModKey == ctx.StarfieldModKey
            ? ctx.StarfieldMod
            : ctx.TemplateMods.FirstOrDefault(m => m.ModKey == fk.ModKey);
        if (owner == null)
            throw new InvalidOperationException($"GetRandomMarker: cell {fk} owning mod {fk.ModKey} not loaded");

        foreach (var block in owner.Cells)
            foreach (var subBlock in block.SubBlocks)
                foreach (var cell in subBlock.Cells)
                    if (cell.FormKey == fk) return cell;

        foreach (var ws in owner.Worldspaces)
        {
            if (ws.TopCell?.FormKey == fk) return ws.TopCell;
            foreach (var wsBlock in ws.SubCells)
                foreach (var wsSubBlock in wsBlock.Items)
                    foreach (var cell in wsSubBlock.Items)
                        if (cell.FormKey == fk) return cell;
        }

        throw new InvalidOperationException($"GetRandomMarker: cell {fk} not found in owning mod {fk.ModKey}");
    }

    private static bool CellContains(ICellGetter cell, FormKey fk)
    {
        foreach (var p in cell.Persistent)
            if (p.FormKey == fk) return true;
        foreach (var p in cell.Temporary)
            if (p.FormKey == fk) return true;
        return false;
    }

    private static PlacedObject? TryCreateNewInInteriorCell(IPlacedObjectGetter source, ICellGetter cell, StarfieldMod targetMod)
    {
        if (!CellContains(cell, source.FormKey)) return null;

        var poNew = CreateNewPlacedObject(source, targetMod);

        // Add to existing cell override if present, otherwise create one
        foreach (var block in targetMod.Cells)
            foreach (var subBlock in block.SubBlocks)
                foreach (var existingCell in subBlock.Cells)
                    if (existingCell.FormKey == cell.FormKey)
                    {
                        existingCell.Persistent.Add(poNew);
                        return poNew;
                    }

        var cellOverride = FindOriginalCell(cell.FormKey).DeepCopy();
        cellOverride.Persistent.Clear();
        cellOverride.Persistent.Add(poNew);
        cellOverride.Temporary.Clear();

        int blockNum    = (int)(cell.FormKey.ID % 10);
        int subBlockNum = (int)(cell.FormKey.ID / 10 % 10);

        CellBlock? cellBlock = null;
        foreach (var b in targetMod.Cells)
            if (b.BlockNumber == blockNum) { cellBlock = b; break; }
        if (cellBlock == null)
        {
            cellBlock = new CellBlock { BlockNumber = blockNum, GroupType = GroupTypeEnum.InteriorCellBlock, SubBlocks = new ExtendedList<CellSubBlock>() };
            targetMod.Cells.Add(cellBlock);
        }

        CellSubBlock? targetSubBlock = null;
        foreach (var sb in cellBlock.SubBlocks)
            if (sb.BlockNumber == subBlockNum) { targetSubBlock = sb; break; }
        if (targetSubBlock == null)
        {
            targetSubBlock = new CellSubBlock { BlockNumber = subBlockNum, GroupType = GroupTypeEnum.InteriorCellSubBlock, Cells = new ExtendedList<Cell>() };
            cellBlock.SubBlocks.Add(targetSubBlock);
        }

        targetSubBlock.Cells.Add(cellOverride);
        return poNew;
    }

    private static PlacedObject? TryCreateNewInWorldspaceTopCell(
        IPlacedObjectGetter source, ICellGetter topCell,
        IWorldspaceGetter sourceWs, StarfieldMod targetMod)
    {
        if (!CellContains(topCell, source.FormKey)) return null;

        // Find or create worldspace override in targetMod
        Worldspace? wsOverride = null;
        foreach (var ws in targetMod.Worldspaces)
            if (ws.FormKey == sourceWs.FormKey) { wsOverride = ws; break; }
        if (wsOverride == null)
        {
            wsOverride = FindOriginalWorldspace(sourceWs.FormKey).DeepCopy();
            wsOverride.OffsetData = null;   // do NOT copy nav-mesh offset blob into override
            wsOverride.SubCells.Clear();
            if (wsOverride.TopCell != null)
            {
                wsOverride.TopCell.Persistent.Clear();
                wsOverride.TopCell.Temporary.Clear();
            }
            targetMod.Worldspaces.Add(wsOverride);
        }

        if (wsOverride.TopCell == null)
            throw new InvalidOperationException(
                $"GetRandomMarker: worldspace override {sourceWs.EditorID} has no TopCell after DeepCopy");

        var poNew = CreateNewPlacedObject(source, targetMod);
        wsOverride.TopCell.Persistent.Add(poNew);
        return poNew;
    }

    private static PlacedObject? TryCreateNewInWorldspaceCell(
        IPlacedObjectGetter source, ICellGetter cell,
        IWorldspaceBlockGetter wsBlock, IWorldspaceSubBlockGetter wsSubBlock,
        IWorldspaceGetter sourceWs, StarfieldMod targetMod)
    {
        if (!CellContains(cell, source.FormKey)) return null;

        // Find or create worldspace override in targetMod
        Worldspace? wsOverride = null;
        foreach (var ws in targetMod.Worldspaces)
            if (ws.FormKey == sourceWs.FormKey) { wsOverride = ws; break; }
        if (wsOverride == null)
        {
            wsOverride = FindOriginalWorldspace(sourceWs.FormKey).DeepCopy();
            wsOverride.OffsetData = null;   // do NOT copy nav-mesh offset blob into override
            wsOverride.SubCells.Clear();
            targetMod.Worldspaces.Add(wsOverride);
        }

        // Find or create the matching block
        WorldspaceBlock? targetBlock = null;
        foreach (var b in wsOverride.SubCells)
            if (b.BlockNumberX == wsBlock.BlockNumberX && b.BlockNumberY == wsBlock.BlockNumberY) { targetBlock = b; break; }
        if (targetBlock == null)
        {
            targetBlock = new WorldspaceBlock { BlockNumberX = wsBlock.BlockNumberX, BlockNumberY = wsBlock.BlockNumberY, GroupType = GroupTypeEnum.ExteriorCellBlock, Items = new ExtendedList<WorldspaceSubBlock>() };
            wsOverride.SubCells.Add(targetBlock);
        }

        // Find or create the matching subblock
        WorldspaceSubBlock? targetSubBlock = null;
        foreach (var sb in targetBlock.Items)
            if (sb.BlockNumberX == wsSubBlock.BlockNumberX && sb.BlockNumberY == wsSubBlock.BlockNumberY) { targetSubBlock = sb; break; }
        if (targetSubBlock == null)
        {
            targetSubBlock = new WorldspaceSubBlock { BlockNumberX = wsSubBlock.BlockNumberX, BlockNumberY = wsSubBlock.BlockNumberY, GroupType = GroupTypeEnum.ExteriorCellSubBlock, Items = new ExtendedList<Cell>() };
            targetBlock.Items.Add(targetSubBlock);
        }

        var poNew = CreateNewPlacedObject(source, targetMod);

        // Add to existing cell override if present, otherwise create one
        foreach (var existingCell in targetSubBlock.Items)
            if (existingCell.FormKey == cell.FormKey)
            {
                existingCell.Persistent.Add(poNew);
                return poNew;
            }

        var cellOverride = FindOriginalCell(cell.FormKey).DeepCopy();
        cellOverride.Persistent.Clear();
        cellOverride.Persistent.Add(poNew);
        cellOverride.Temporary.Clear();
        targetSubBlock.Items.Add(cellOverride);
        return poNew;
    }

    /// <summary>
    /// Gets a random synonym for log/journal entries.
    /// </summary>
    public static string GetLogSynonym()
    {
        var synonyms = new List<string>
        {
            "Ship Notes", "Crew Notes", "Duty Records", "Mission Notes",
            "Mission Records", "Daily Entries", "Service Entries", "Personal Records",
            "Crew Journals", "Field Notes", "Status Reports", "Shift Reports",
            "Voyage Notes", "Travel Records", "Operations Logs", "Observation Notes",
            "Shipboard Records", "Work Entries", "Duty Journals", "Activity Reports",
            "Notes", "Ledger", "Recordings", "Memoranda", "Transcript",
            "Summary", "Documentation", "Overview", "Statement", "Recount", "Diary"
        };
        return synonyms[Random.Next(synonyms.Count)];
    }
}
