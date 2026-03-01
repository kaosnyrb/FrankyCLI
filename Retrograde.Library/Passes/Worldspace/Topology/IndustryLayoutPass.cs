using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Map layout pass for SmallIndustryBase POIs.
/// Places 3–6 GPPIPCMManMade_ PackIn buildings in a compact cluster
/// around the map centre (24, 24) using a fixed slot grid.
///
/// Inner ring: up to 5 buildings on 4 diagonal slots at step ±10 from centre.
/// Outer ring: 1–2 solar/misc prefabs on 4 cardinal slots at step ±14 from centre.
/// No walls, gates, connectors, or scatter tiles.
///
/// Slot spacing verified against actual PackIn ObjectBounds (TileWorldSize=4):
///   - Inner step 10 → 40 overlay units. Worst pair (FluidStorageXLarge+XLarge): gap 36.
///   - Diagonal-only inner ring avoids same-axis conflicts with the cardinal outer ring.
///   - Inner diagonal (34,34) to outer cardinal (38,24): ≈43 overlay units, gap 5.1.
/// All gaps ≥ 5 overlay units for the largest variants in each category.
/// Everything sits ≥ 10 tiles from the map edge (was 3–4 tiles for the outer ring).
/// </summary>
/// <param name="scale">Controls cluster density (0.1 = 3 buildings, 1.0 = 6 buildings).</param>
public class IndustryLayoutPass(float scale = 0.5f) : IWorldspacePass
{
    private readonly float _scale = Math.Clamp(scale, 0.1f, 1.0f);

    // Inner ring: 4 diagonal slots at step ±10 from centre (24, 24).
    // Diagonal-only so there is no same-axis conflict with the cardinal outer ring slots.
    // Step 10 × TileWorldSize 4 = 40 overlay units from centre.
    // Worst-case inner pair (same axis, 20 tiles apart): gap = 80 - 22 - 22 = 36 units ✓
    private static readonly (int x, int y)[] InnerSlots =
    {
        (34, 34), (34, 14),
        (14, 34), (14, 14),
    };

    // Outer ring: 4 cardinal slots at step ±14 from centre.
    // Step 14 × 4 = 56 overlay units from centre.
    // Nearest inner building is the diagonal at ≈43 overlay units from (38,24) → gap 5.1 ✓
    // All positions ≥ 10 tiles from the map edge (was 3–4 tiles at step ±20).
    private static readonly (int x, int y)[] OuterSlots =
    {
        (38, 24), (10, 24), (24, 38), (24, 10),
    };

    // Additional inner categories after the centre (AbandonedIndustrial)
    private static readonly string[] AdditionalKeys =
    {
        "industry_large",
        "industry_comms",
        "industry_mech_large",
        "industry_fluid_xl",
    };

    public void RunPass(WorldspaceState state)
    {
        var rand = state.Rng;
        var map  = state.Map;

        // --- How many additional inner buildings beyond the centre ---
        // scale < 0.35 → 2 additional (3 total)
        // scale 0.35–0.70 → 2 or 3 additional (3–4 total)
        // scale > 0.70 → 4 additional (5 total, all categories)
        int additionalCount = _scale < 0.35f ? 2
                            : _scale > 0.70f ? 4
                            : (rand.Next(2) + 2);   // 2 or 3

        // --- Centre building: always AbandonedIndustrial ---
        map.placesmalltile(24, 24, "industry_centre", rand.Next(4) * 90, "floor");

        // --- Shuffle category list and take additionalCount keys ---
        var keys = AdditionalKeys.ToList();
        Shuffle(keys, rand);
        keys = keys.Take(additionalCount).ToList();

        // --- Shuffle inner slots and assign one per category ---
        var slots = InnerSlots.ToList();
        Shuffle(slots, rand);

        for (int i = 0; i < keys.Count; i++)
        {
            map.placesmalltile(slots[i].x, slots[i].y, keys[i], rand.Next(4) * 90, "floor");
        }

        // --- Outer ring ---
        // scale < 0.5 → 1 outer slot (solar only)
        // scale >= 0.5 → 2 outer slots (solar + misc)
        int outerCount = _scale < 0.5f ? 1 : 2;

        var outerSlots = OuterSlots.ToList();
        Shuffle(outerSlots, rand);

        // First outer slot: solar panel at 0° rotation
        map.placesmalltile(outerSlots[0].x, outerSlots[0].y, "industry_solar", 0, "floor");

        // Second outer slot (when scale >= 0.5): misc from combined outer pool, random rotation
        if (outerCount == 2)
        {
            map.placesmalltile(outerSlots[1].x, outerSlots[1].y, "industry_outer", rand.Next(4) * 90, "floor");
        }
    }

    private static void Shuffle<T>(List<T> list, Random rand)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
