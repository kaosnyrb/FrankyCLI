using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// Map layout pass for SmallIndustryBase POIs.
/// Places 3–6 GPPIPCMManMade_ PackIn buildings in a compact cluster
/// around the map centre (24, 24) using a fixed slot grid.
///
/// Inner ring: up to 5 buildings on 4 diagonal slots at step ±13 from centre.
/// Outer ring: 1–2 solar/misc prefabs on 4 cardinal slots at step ±20 from centre.
/// No walls, gates, connectors, or scatter tiles.
///
/// Slot spacing verified against actual PackIn ObjectBounds (TileWorldSize=4):
///   - Inner step 13 → 52 overlay units. Worst pair (FluidStorageXLarge+XLarge): gap 7.8.
///   - Diagonal-only inner ring avoids same-axis conflicts with the cardinal outer ring.
///   - Inner diagonal (37,37) to outer cardinal (44,24): ≈59 units, gap 21.3.
/// All gaps ≥ 5 overlay units for the largest variants in each category.
/// </summary>
/// <param name="scale">Controls cluster density (0.1 = 3 buildings, 1.0 = 6 buildings).</param>
public class IndustryLayoutPass(float scale = 0.5f) : IWorldspacePass
{
    private readonly float _scale = Math.Clamp(scale, 0.1f, 1.0f);

    // Inner ring: 4 diagonal slots at step ±13 from centre (24, 24).
    // Diagonal-only so there is no same-axis conflict with the cardinal outer ring slots.
    // Step 13 × TileWorldSize 4 = 52 overlay units between centres.
    // Worst-case pair (two FluidStorageXLargeB — ±22 on Y): gap = 52 - 22 - 22 = 8 units ✓
    private static readonly (int x, int y)[] InnerSlots =
    {
        (37, 37), (37, 11),
        (11, 37), (11, 11),
    };

    // Outer ring: 4 cardinal slots at step ±20 from centre.
    // Step 20 × 4 = 80 overlay units from centre.
    // Nearest inner building is the diagonal at ≈59 overlay units from (44,24) → gap 21 ✓
    private static readonly (int x, int y)[] OuterSlots =
    {
        (44, 24), (4, 24), (24, 44), (24, 4),
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
