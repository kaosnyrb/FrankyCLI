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
                return ClonePlacedObjectAsOverride(po, targetMod);
            default:
                throw new InvalidOperationException(
                    $"GetRandomMarker: cannot clone record type {source.GetType().Name} — add a case to CloneIntoMod");
        }
    }

    private static PlacedObject ClonePlacedObjectAsOverride(IPlacedObjectGetter source, StarfieldMod targetMod)
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
                        var po = TryCloneIntoInteriorCell(source, cell, targetMod);
                        if (po != null) return po;
                    }

            // Search worldspace top cells and exterior cells
            foreach (var ws in mod.Worldspaces)
            {
                // Top cell (persistent world-children group) — city markers live here
                if (ws.TopCell != null)
                {
                    var po = TryCloneIntoWorldspaceTopCell(source, ws.TopCell, ws, targetMod);
                    if (po != null) return po;
                }

                foreach (var wsBlock in ws.SubCells)
                    foreach (var wsSubBlock in wsBlock.Items)
                        foreach (var cell in wsSubBlock.Items)
                        {
                            var po = TryCloneIntoWorldspaceCell(source, cell, wsBlock, wsSubBlock, ws, targetMod);
                            if (po != null) return po;
                        }
            }
        }

        throw new InvalidOperationException(
            $"GetRandomMarker: cannot find parent cell for PlacedObject {source.EditorID} ({source.FormKey})");
    }

    private static bool CellContains(ICellGetter cell, FormKey fk)
    {
        foreach (var p in cell.Persistent)
            if (p.FormKey == fk) return true;
        foreach (var p in cell.Temporary)
            if (p.FormKey == fk) return true;
        return false;
    }

    private static PlacedObject? TryCloneIntoInteriorCell(IPlacedObjectGetter source, ICellGetter cell, StarfieldMod targetMod)
    {
        if (!CellContains(cell, source.FormKey)) return null;

        // Check for existing cell override
        foreach (var block in targetMod.Cells)
            foreach (var subBlock in block.SubBlocks)
                foreach (var existingCell in subBlock.Cells)
                    if (existingCell.FormKey == cell.FormKey)
                    {
                        foreach (var placed in existingCell.Persistent)
                            if (placed.FormKey == source.FormKey) return (PlacedObject)placed;
                        var dup = source.DeepCopy();
                        existingCell.Persistent.Add(dup);
                        return dup;
                    }

        // Create new interior cell override (DeepCopy preserves FormKey)
        var poOverride = source.DeepCopy();
        var cellOverride = cell.DeepCopy();
        cellOverride.Persistent.Clear();
        cellOverride.Persistent.Add(poOverride);
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
        return poOverride;
    }

    private static PlacedObject? TryCloneIntoWorldspaceTopCell(
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
            wsOverride = sourceWs.DeepCopy();
            wsOverride.SubCells.Clear();
            // Clear the top cell's contents so only our placed object ends up there
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

        // Check if already added
        foreach (var placed in wsOverride.TopCell.Persistent)
            if (placed.FormKey == source.FormKey) return (PlacedObject)placed;

        var poOverride = source.DeepCopy();
        wsOverride.TopCell.Persistent.Add(poOverride);
        return poOverride;
    }

    private static PlacedObject? TryCloneIntoWorldspaceCell(
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
            wsOverride = sourceWs.DeepCopy();
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

        // Check for existing cell override
        foreach (var existingCell in targetSubBlock.Items)
            if (existingCell.FormKey == cell.FormKey)
            {
                foreach (var placed in existingCell.Persistent)
                    if (placed.FormKey == source.FormKey) return (PlacedObject)placed;
                var dup = source.DeepCopy();
                existingCell.Persistent.Add(dup);
                return dup;
            }

        // Create new exterior cell override (DeepCopy preserves FormKey)
        var poOverride = source.DeepCopy();
        var cellOverride = cell.DeepCopy();
        cellOverride.Persistent.Clear();
        cellOverride.Persistent.Add(poOverride);
        cellOverride.Temporary.Clear();
        targetSubBlock.Items.Add(cellOverride);
        return poOverride;
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
