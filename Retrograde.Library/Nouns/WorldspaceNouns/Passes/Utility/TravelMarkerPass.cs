using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Utils;
using System;
using System.Linq;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Places the six RE travel marker statics (A1/A2/A3/B1/B2/B3) near the outer edges
/// of the worldspace and wires each one into the Location's MasterSpecialReferences
/// with the corresponding RETravelXXLocRef location reference type.
///
/// Positions are derived by scaling the du_takeover reference layout
/// (035wldblltprfprtnlbs, 2x2 cells) to the worldspace's actual half-extent
/// (cellGridSize / 2 * 100 overlay units).
/// Z is sampled from the BTD terrain file; falls back to WorldspaceState.TerrainHeight.
/// </summary>
public class TravelMarkerPass : IWorldspacePass
{
    // (normalizedX, normalizedY, staticFormId, locRefFormId)
    // Positions normalized to worldspace half-extent (1.0 = edge).
    // Layout verified against du_takeover.esm / 035wldblltprfprtnlbs.
    private static readonly (float nx, float ny, uint staticId, uint locRefId)[] MarkerDefs =
    {
        ( 0.476f,  0.935f, 0x0E3800u, 0x05F478u), // A1 – north edge, east half
        (-0.958f,  0.957f, 0x0E37FFu, 0x05F479u), // A2 – north edge, west half
        (-0.966f,  0.072f, 0x0E37FEu, 0x05F47Au), // A3 – west edge, mid-height
        (-0.967f, -0.937f, 0x0E37FDu, 0x05F47Bu), // B1 – south-west corner
        ( 0.479f, -0.985f, 0x0E37FCu, 0x05F47Cu), // B2 – south edge, east half
        ( 0.968f, -0.966f, 0x0E37FBu, 0x05F47Du), // B3 – south-east corner
    };

    public void RunPass(WorldspaceState state)
    {
        if (state.CellLookup.Count == 0) return;

        var targetMod = RetrogradeContext.Current.TargetMod;
        var starfieldEsm = RetrogradeContext.Current.StarfieldModKey;

        // Derive worldspace half-extent from the cell grid (e.g. 2x2 → halfExtent=100)
        int minCellX = state.CellLookup.Keys.Min(p => p.X);
        int maxCellX = state.CellLookup.Keys.Max(p => p.X);
        int gridSize = maxCellX - minCellX + 1;
        float halfExtent = gridSize * 50f;  // gridSize cells * 100 units/cell / 2

        state.Location.MasterSpecialReferences ??= new ExtendedList<LocationCellStaticReference>();

        foreach (var (nx, ny, staticId, locRefId) in MarkerDefs)
        {
            float worldX = nx * halfExtent;
            float worldY = ny * halfExtent;
            float worldZ = SampleZ(state, worldX, worldY);

            int cellX = (int)MathF.Floor(worldX / 100f);
            int cellY = (int)MathF.Floor(worldY / 100f);

            if (!state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var cell))
            {
                Console.WriteLine($"[TravelMarkerPass] WARNING: no cell at ({cellX},{cellY}) for marker 0x{staticId:X6} — skipping");
                continue;
            }

            var placed = new PlacedObject(targetMod)
            {
                Base = new FormKey(starfieldEsm, staticId).ToNullableLink<IPlaceableObject>(),
                Position = new P3Float(worldX, worldY, worldZ),
                Rotation = new P3Float(0, 0, 0),
            };

            state.PlacementUtil.AddToTemporary(cell, placed);

            state.Location.MasterSpecialReferences.Add(new LocationCellStaticReference
            {
                LocationRefType = new FormKey(starfieldEsm, locRefId).ToLink<ILocationReferenceTypeGetter>(),
                Marker         = placed.FormKey.ToLink<IPlacedGetter>(),
                Location       = cell.FormKey.ToLink<IComplexLocationGetter>(),
                Grid           = new P2Int16((short)cellX, (short)cellY),
            });
        }

        if (!RetrogradeContext.Quiet)
            Console.WriteLine($"[TravelMarkerPass] Placed {MarkerDefs.Length} travel markers (halfExtent={halfExtent})");
    }

    /// <summary>
    /// Samples terrain Z at overlay position (worldX, worldY).
    /// Converts overlay → BTD-internal coords (1 overlay unit = 40.96 BTD units)
    /// then divides the BTD result by 8 (Starfield scale factor).
    /// Falls back to WorldspaceState.TerrainHeight when no BTD is available.
    /// </summary>
    private static float SampleZ(WorldspaceState state, float worldX, float worldY)
    {
        if (state.BtdFile != null)
        {
            const float overlayToBtd = 4096f / 100f;   // BTD units per overlay unit
            float btdX = worldX * overlayToBtd;
            float btdY = worldY * overlayToBtd;
            return state.BtdFile.SampleHeightAtWorld(btdX, btdY) / 8f;
        }
        return state.TerrainHeight;
    }
}
