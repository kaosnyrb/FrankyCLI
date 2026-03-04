using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Passes.Worldspace;

/// <summary>
/// First map pass for SmallIndustryBase POIs.
/// Scans Starfield.esm PackIns by GPPIPCMManMade_ EditorID prefix and populates
/// state.PackInLibrary with one variant list per category, and state.PackInRadii
/// with the max XY half-extent (in overlay units) across all variants in that category.
/// IndustryLayoutPass uses the radii for per-pair overlap prevention.
///
/// Pattern safety notes:
///   "FluidStorageLarge"  is NOT a substring of "FluidStorageXLarge" (X breaks it) — safe.
///   "GenericMechanicalLarge" is NOT a substring of "GenericMechanicalMedium" — safe.
///   "StorageBay" does not appear in any other category prefix — safe.
/// </summary>
public class IndustryPackInLibraryPass : IWorldspacePass
{
    // Minimum radius assigned when ObjectBounds data is absent or suspiciously small.
    private const float MinFallbackRadius = 4f; // overlay units ≈ 1 map tile

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

        // Compute the max XY half-extent across all variants in each category.
        // ObjectBounds for exterior PackIns is in overlay units (1 cell = 100 units).
        foreach (var (key, formKeys) in state.PackInLibrary)
        {
            float maxRadius = 0f;
            foreach (var fk in formKeys)
            {
                var packin = sf.PackIns.FirstOrDefault(p => p.FormKey == fk);
                if (packin?.ObjectBounds == null) continue;
                float ex = MathF.Abs(packin.ObjectBounds.Second.X - packin.ObjectBounds.First.X) / 2f;
                float ey = MathF.Abs(packin.ObjectBounds.Second.Y - packin.ObjectBounds.First.Y) / 2f;
                maxRadius = MathF.Max(maxRadius, MathF.Max(ex, ey));
            }
            state.PackInRadii[key] = MathF.Max(maxRadius, MinFallbackRadius);
        }
    }

    private static List<FormKey> FindByPattern(IStarfieldModGetter mod, string pattern)
        => mod.PackIns
            .Where(p => p.EditorID != null && p.EditorID.Contains(pattern))
            .Select(p => p.FormKey)
            .ToList();
}
