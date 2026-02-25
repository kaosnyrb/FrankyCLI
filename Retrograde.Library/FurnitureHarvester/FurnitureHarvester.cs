using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.FurnitureHarvester;

/// <summary>
/// Scans all interior cells in Starfield.esm for instances of a target base form
/// (e.g. a specific furniture or static), then creates PackIn prefabs of each instance
/// together with every nearby object within the configured radius.
///
/// The target form is placed at the origin (0,0,0) with no rotation. All other objects
/// in the group are translated and yaw-rotated to match, preserving their relative layout.
///
/// Only objects whose Base links to Starfield.esm are included to avoid dependencies on
/// DLC or other mods.
///
/// Usage:
///   var h = new FurnitureHarvester(outputMod, sfModKey);
///   int count = h.Harvest(starfieldGetter, targetFormKey, "sanitisedLabel", radius: 150f, maxVariants: 50);
/// </summary>
public class FurnitureHarvester
{
    // PrefabPackinPivotDummy — always first Temporary entry in every PackIn cell.
    private const uint IdPivot = 0x03F808;

    private readonly StarfieldMod _targetMod;
    private readonly ModKey _sfModKey;

    // Running counter for unique EditorID generation.
    private int _variantSeq = 1;

    public FurnitureHarvester(StarfieldMod targetMod, ModKey sfModKey)
    {
        _targetMod = targetMod;
        _sfModKey  = sfModKey;
    }

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Scans all interior cells in <paramref name="starfield"/> for placed instances of
    /// <paramref name="targetFormKey"/> and harvests up to <paramref name="maxVariants"/>
    /// PackIn prefabs into the target mod.
    /// </summary>
    /// <param name="starfield">The mod to scan (typically Starfield.esm).</param>
    /// <param name="targetFormKey">FormKey of the furniture/static to use as the anchor.</param>
    /// <param name="baseLabel">Short label used in generated EditorIDs (already sanitised).</param>
    /// <param name="radius">Capture radius in game units around each found instance.</param>
    /// <param name="maxVariants">Upper bound on the number of prefabs to create.</param>
    /// <returns>Number of prefabs created.</returns>
    public int Harvest(
        IStarfieldModGetter starfield,
        FormKey              targetFormKey,
        string               baseLabel,
        float                radius,
        int                  maxVariants)
    {
        int count = 0;

        // ── Interior cells ────────────────────────────────────────────────────
        foreach (var block in starfield.Cells)
        {
            foreach (var subBlock in block.SubBlocks)
            {
                foreach (var cell in subBlock.Cells)
                {
                    if (count >= maxVariants) goto Done;
                    count += HarvestCell(cell, targetFormKey, baseLabel, radius, maxVariants - count);
                }
            }
        }

        Done:
        return count;
    }

    // ── Per-cell scan ─────────────────────────────────────────────────────────

    private int HarvestCell(
        ICellGetter cell,
        FormKey     targetFormKey,
        string      baseLabel,
        float       radius,
        int         remaining)
    {
        // Merge Persistent + Temporary into one pool of placed objects.
        var allObjects = cell.Persistent
            .Concat(cell.Temporary)
            .OfType<IPlacedObjectGetter>()
            .ToList();

        // Find every instance of the target form in this cell.
        var anchors = allObjects
            .Where(o => o.Base.FormKey == targetFormKey)
            .ToList();

        if (anchors.Count == 0) return 0;

        int created = 0;

        foreach (var anchor in anchors)
        {
            if (created >= remaining) break;

            // Collect all Starfield.esm objects within the radius.
            var group = allObjects
                .Where(o => o.Base.FormKey.ModKey.Name == "Starfield")
                .Where(o => Distance(o.Position, anchor.Position) <= radius)
                .ToList();

            // Skip trivial groups — anchor alone is not interesting.
            if (group.Count <= 1)
            {
                Console.WriteLine($"  [skip] isolated anchor in {cell.EditorID ?? cell.FormKey.ToString()} ({group.Count} object)");
                continue;
            }

            string variantId = $"rg_harv_{baseLabel}_{_variantSeq:D3}";
            _variantSeq++;

            CreatePrefab(variantId, anchor, group);
            created++;

            string cellLabel = cell.EditorID ?? cell.FormKey.ToString();
            Console.WriteLine($"  [{_variantSeq - 1:D3}] {variantId} — {group.Count} objects from {cellLabel}");
        }

        return created;
    }

    // ── Prefab creation ───────────────────────────────────────────────────────

    private void CreatePrefab(
        string                  editorId,
        IPlacedObjectGetter     anchor,
        List<IPlacedObjectGetter> group)
    {
        var cell = CreateCell(editorId);

        // Pivot dummy — always the first Temporary entry.
        AddTemp(cell, IdPivot, 0f, 0f, 0f);

        // Compute the inverse transform: translate by -anchorPos, rotate by -anchorYaw.
        var   anchorPos = anchor.Position ?? new P3Float(0, 0, 0);
        float anchorYaw = anchor.Rotation?.Z ?? 0f;
        float cos       = MathF.Cos(-anchorYaw);
        float sin       = MathF.Sin(-anchorYaw);

        // Track extents for ObjectBounds.
        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

        foreach (var obj in group)
        {
            var pos = obj.Position ?? new P3Float(0, 0, 0);

            // Translate relative to anchor.
            float dx = pos.X - anchorPos.X;
            float dy = pos.Y - anchorPos.Y;
            float dz = pos.Z - anchorPos.Z;

            // Rotate around Z by -anchorYaw.
            float rx = cos * dx - sin * dy;
            float ry = sin * dx + cos * dy;

            // Adjust rotation: subtract anchor yaw from the object's yaw.
            float relYaw = (obj.Rotation?.Z ?? 0f) - anchorYaw;
            var   relRot = new P3Float(
                obj.Rotation?.X ?? 0f,
                obj.Rotation?.Y ?? 0f,
                relYaw);

            // For the anchor itself, force to exact origin with zero rotation.
            if (obj.FormKey == anchor.FormKey)
            {
                rx     = 0f;
                ry     = 0f;
                dz     = 0f;
                relRot = new P3Float(0f, 0f, 0f);
            }

            AddTempFromGetter(cell, obj, rx, ry, dz, relRot);

            minX = MathF.Min(minX, rx);
            minY = MathF.Min(minY, ry);
            minZ = MathF.Min(minZ, dz);
            maxX = MathF.Max(maxX, rx);
            maxY = MathF.Max(maxY, ry);
            maxZ = MathF.Max(maxZ, dz);
        }

        // Add a small margin to the bounds.
        const float Pad = 20f;

        var packin = new PackIn(_targetMod)
        {
            EditorID            = editorId,
            MajorRecordFlagsRaw = 512, // Prefab flag
            ObjectBounds        = new ObjectBounds
            {
                First  = new P3Int16(
                    (short)MathF.Floor(minX - Pad),
                    (short)MathF.Floor(minY - Pad),
                    (short)MathF.Floor(minZ - Pad)),
                Second = new P3Int16(
                    (short)MathF.Ceiling(maxX + Pad),
                    (short)MathF.Ceiling(maxY + Pad),
                    (short)MathF.Ceiling(maxZ + Pad + 50f)),
            },
        };
        packin.Cell = cell.ToNullableLink<ICellGetter>();
        _targetMod.PackIns.Add(packin);
    }

    // ── Cell infrastructure ───────────────────────────────────────────────────

    /// <summary>
    /// Creates a new interior Cell and inserts it into the correct CellBlock / CellSubBlock
    /// hierarchy in the target mod. Follows the same block/subblock routing as
    /// SciHallwayGenerator and SciRoomGenerator.
    /// </summary>
    private Cell CreateCell(string editorId)
    {
        var cell = new Cell(_targetMod)
        {
            EditorID   = editorId + "_cell",
            Flags      = Cell.Flag.IsInteriorCell,
            Temporary  = new ExtendedList<IPlaced>(),
            Persistent = new ExtendedList<IPlaced>(),
        };

        int blockNum    = (int)(cell.FormKey.ID % 10);
        int subBlockNum = (int)((cell.FormKey.ID / 10) % 10);

        // Find or create CellBlock.
        CellBlock? cellBlock = null;
        foreach (var b in _targetMod.Cells)
            if (b.BlockNumber == blockNum) { cellBlock = b; break; }

        if (cellBlock == null)
        {
            cellBlock = new CellBlock
            {
                BlockNumber = blockNum,
                GroupType   = GroupTypeEnum.InteriorCellBlock,
                SubBlocks   = new ExtendedList<CellSubBlock>(),
            };
            _targetMod.Cells.Add(cellBlock);
        }

        // Find or create CellSubBlock.
        CellSubBlock? subBlock = null;
        foreach (var sb in cellBlock.SubBlocks)
            if (sb.BlockNumber == subBlockNum) { subBlock = sb; break; }

        if (subBlock == null)
        {
            subBlock = new CellSubBlock
            {
                BlockNumber = subBlockNum,
                GroupType   = GroupTypeEnum.InteriorCellSubBlock,
                Cells       = new ExtendedList<Cell>(),
            };
            cellBlock.SubBlocks.Add(subBlock);
        }

        subBlock.Cells.Add(cell);
        return cell;
    }

    // ── Placed object helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Clones a getter-typed PlacedObject into the target mod's cell at the given
    /// transformed position/rotation. Copies the most important visual properties
    /// (scale, lighting, lock) while assigning a fresh FormKey.
    /// </summary>
    private void AddTempFromGetter(
        Cell                cell,
        IPlacedObjectGetter source,
        float x, float y, float z,
        P3Float             rotation)
    {
        var placed = new PlacedObject(_targetMod)
        {
            Count    = source.Count,
            Base     = source.Base.FormKey.ToLink<IPlaceableObjectGetter>(),
            Position = new P3Float(x, y, z),
            Rotation = rotation,
            Scale    = source.Scale,
        };

        // Preserve lighting and lock state.
        if (source.Lighting != null)
            placed.Lighting = source.Lighting.DeepCopy();
        if (source.Lock != null)
            placed.Lock = source.Lock.DeepCopy();

        // Preserve layered material swaps (e.g. blood/damage decals).
        if (source.LayeredMaterialSwaps != null)
            placed.LayeredMaterialSwaps = source.LayeredMaterialSwaps
                .Select(s => s.DeepCopy())
                .ToExtendedList();

        // Preserve enable-parent link so toggled props stay wired up.
        if (source.EnableParent != null)
            placed.EnableParent = source.EnableParent.DeepCopy();

        cell.Temporary.Add(placed);
    }

    /// <summary>Adds a simple Starfield.esm static to a cell's Temporary list.</summary>
    private void AddTemp(Cell cell, uint formId, float x, float y, float z, P3Float? rotation = null)
    {
        var po = new PlacedObject(_targetMod)
        {
            Base     = new FormKey(_sfModKey, formId).ToLink<IPlaceableObjectGetter>(),
            Position = new P3Float(x, y, z),
            Rotation = rotation ?? new P3Float(0f, 0f, 0f),
        };
        cell.Temporary.Add(po);
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static float Distance(P3Float? a, P3Float? b)
    {
        if (a == null || b == null) return float.MaxValue;
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
