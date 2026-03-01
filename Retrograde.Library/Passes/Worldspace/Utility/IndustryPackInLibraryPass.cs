using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// First map pass for SmallIndustryBase POIs.
/// Scans Starfield.esm PackIns by GPPIPCMManMade_ EditorID prefix to populate
/// state.PackInLibrary with variant lists for each industry category.
/// </summary>
public class IndustryPackInLibraryPass : IWorldspacePass
{
    public void RunPass(WorldspaceState state)
    {
        var sf = RetrogradeContext.Current.StarfieldMod;

        // Inner ring — one category per building slot
        state.PackInLibrary["industry_centre"]     = FindByPattern(sf, "GPPIPCMManMade_AbandondedIndustrial");
        state.PackInLibrary["industry_large"]      = FindByPattern(sf, "GPPIPCMManMade_IndustrialLarge");
        state.PackInLibrary["industry_comms"]      = FindByPattern(sf, "GPPIPCMManMade_Communications");
        state.PackInLibrary["industry_mech_large"] = FindByPattern(sf, "GPPIPCMManMade_GenericMechanicalLarge");
        state.PackInLibrary["industry_fluid_xl"]   = FindByPattern(sf, "GPPIPCMManMade_FluidStorageXLarge");

        // Outer ring — solar panels placed at 0° rotation, misc pool at random rotation
        state.PackInLibrary["industry_solar"] = FindByPattern(sf, "GPPIPCMManMade_SolarPanels");
        state.PackInLibrary["industry_outer"] = BuildOuterMiscPool(sf);
    }

    private static List<FormKey> FindByPattern(IStarfieldModGetter mod, string pattern)
        => mod.PackIns
            .Where(p => p.EditorID != null && p.EditorID.Contains(pattern))
            .Select(p => p.FormKey)
            .ToList();

    private static List<FormKey> BuildOuterMiscPool(IStarfieldModGetter mod)
    {
        // "FluidStorageLarge" does not match "FluidStorageXLarge" — safe without exclusion filter.
        var patterns = new[]
        {
            "GPPIPCMManMade_Reactor",
            "GPPIPCMManMade_StorageBay",
            "GPPIPCMManMade_FluidStorageMedium",
            "GPPIPCMManMade_FluidStorageLarge",
            "GPPIPCMManMade_GenericMechanicalMedium",
            "GPPIPCMManMade_ConcreteFoundations",
            "GPPIPCMManMade_ClutterPile",
        };
        return mod.PackIns
            .Where(p => p.EditorID != null && patterns.Any(pt => p.EditorID.Contains(pt)))
            .Select(p => p.FormKey)
            .ToList();
    }
}
