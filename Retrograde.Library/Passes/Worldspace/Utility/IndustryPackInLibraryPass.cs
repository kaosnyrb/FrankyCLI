using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// First map pass for SmallIndustryBase POIs.
/// Scans Starfield.esm PackIns by GPPIPCMManMade_ EditorID prefix and populates
/// state.PackInLibrary with one variant list per category.
/// All 13 categories form a single flat pool used by IndustryLayoutPass.
///
/// Pattern safety notes:
///   "FluidStorageLarge"  is NOT a substring of "FluidStorageXLarge" (X breaks it) — safe.
///   "GenericMechanicalLarge" is NOT a substring of "GenericMechanicalMedium" — safe.
///   "StorageBay" does not appear in any other category prefix — safe.
/// </summary>
public class IndustryPackInLibraryPass : IWorldspacePass
{
    public void RunPass(WorldspaceState state)
    {
        var sf = RetrogradeContext.Current.StarfieldMod;

        state.PackInLibrary["industry_abandoned"]     = FindByPattern(sf, "GPPIPCMManMade_AbandondedIndustrial");
        state.PackInLibrary["industry_large"]         = FindByPattern(sf, "GPPIPCMManMade_IndustrialLarge");
        state.PackInLibrary["industry_comms"]         = FindByPattern(sf, "GPPIPCMManMade_Communications");
        state.PackInLibrary["industry_mech_large"]    = FindByPattern(sf, "GPPIPCMManMade_GenericMechanicalLarge");
        state.PackInLibrary["industry_mech_medium"]   = FindByPattern(sf, "GPPIPCMManMade_GenericMechanicalMedium");
        state.PackInLibrary["industry_fluid_xl"]      = FindByPattern(sf, "GPPIPCMManMade_FluidStorageXLarge");
        state.PackInLibrary["industry_fluid_large"]   = FindByPattern(sf, "GPPIPCMManMade_FluidStorageLarge");
        state.PackInLibrary["industry_fluid_medium"]  = FindByPattern(sf, "GPPIPCMManMade_FluidStorageMedium");
        state.PackInLibrary["industry_solar"]         = FindByPattern(sf, "GPPIPCMManMade_SolarPanels");
        state.PackInLibrary["industry_reactor"]       = FindByPattern(sf, "GPPIPCMManMade_Reactor");
        state.PackInLibrary["industry_storage"]       = FindByPattern(sf, "GPPIPCMManMade_StorageBay");
        state.PackInLibrary["industry_foundations"]   = FindByPattern(sf, "GPPIPCMManMade_ConcreteFoundations");
        state.PackInLibrary["industry_clutter"]       = FindByPattern(sf, "GPPIPCMManMade_ClutterPile");
    }

    private static List<FormKey> FindByPattern(IStarfieldModGetter mod, string pattern)
        => mod.PackIns
            .Where(p => p.EditorID != null && p.EditorID.Contains(pattern))
            .Select(p => p.FormKey)
            .ToList();
}
