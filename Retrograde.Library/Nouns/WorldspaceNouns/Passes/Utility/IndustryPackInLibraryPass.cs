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
        var allMods = RetrogradeContext.AllMods;

        state.PackInLibrary["industry_abandoned"]     = FindByPattern(allMods, "GPPIPCMManMade_AbandondedIndustrial");
        state.PackInLibrary["industry_large"]         = FindByPattern(allMods, "GPPIPCMManMade_IndustrialLarge");
        state.PackInLibrary["industry_comms"]         = FindByPattern(allMods, "GPPIPCMManMade_Communications");
        state.PackInLibrary["industry_mech_large"]    = FindByPattern(allMods, "GPPIPCMManMade_GenericMechanicalLarge");
        state.PackInLibrary["industry_mech_medium"]   = FindByPattern(allMods, "GPPIPCMManMade_GenericMechanicalMedium");
        state.PackInLibrary["industry_fluid_xl"]      = FindByPattern(allMods, "GPPIPCMManMade_FluidStorageXLarge");
        state.PackInLibrary["industry_fluid_large"]   = FindByPattern(allMods, "GPPIPCMManMade_FluidStorageLarge");
        state.PackInLibrary["industry_fluid_medium"]  = FindByPattern(allMods, "GPPIPCMManMade_FluidStorageMedium");
        state.PackInLibrary["industry_solar"]         = FindByPattern(allMods, "GPPIPCMManMade_SolarPanels");
        state.PackInLibrary["industry_reactor"]       = FindByPattern(allMods, "GPPIPCMManMade_Reactor");
        state.PackInLibrary["industry_storage"]       = FindByPattern(allMods, "GPPIPCMManMade_StorageBay");
        state.PackInLibrary["industry_foundations"]   = FindByPattern(allMods, "GPPIPCMManMade_ConcreteFoundations");
        state.PackInLibrary["industry_clutter"]       = FindByPattern(allMods, "GPPIPCMManMade_ClutterPile");
        state.PackInLibrary["industry_misc"]          = FindByEditorIds(allMods,
        [
            // Add specific PackIn EditorIDs here.
            "HopeTownWaterTower_Packin",
            "FPIPCM_ManMade_WindmillA",
            "FPIPCMManMade_Driller01"
        ]);

        // Props scattered around buildings by IndustryPropScatterPass.
        state.PackInLibrary["prop_crates_large"]  = FindByPattern(allMods, "SC_LD_CratesLarge");
        state.PackInLibrary["prop_crates_medium"] = FindByPattern(allMods, "SC_LD_CratesMedium");
        state.PackInLibrary["prop_crate_pi"]      = FindByPattern(allMods, "PI_Manmade_LargeCrate");

        // Compute the max XY half-extent across all variants in each category.
        // ObjectBounds for exterior PackIns is in overlay units (1 cell = 100 units).
        foreach (var (key, formKeys) in state.PackInLibrary)
        {
            float maxRadius = 0f;
            foreach (var fk in formKeys)
            {
                var packin = allMods.SelectMany(m => m.PackIns).FirstOrDefault(p => p.FormKey == fk);
                if (packin?.ObjectBounds == null) continue;
                float ex = MathF.Abs(packin.ObjectBounds.Second.X - packin.ObjectBounds.First.X) / 2f;
                float ey = MathF.Abs(packin.ObjectBounds.Second.Y - packin.ObjectBounds.First.Y) / 2f;
                maxRadius = MathF.Max(maxRadius, MathF.Max(ex, ey));
            }
            state.PackInRadii[key] = MathF.Max(maxRadius, MinFallbackRadius);
        }
    }

    private static List<FormKey> FindByPattern(IEnumerable<IStarfieldModGetter> mods, string pattern)
        => mods.SelectMany(m => m.PackIns)
               .Where(p => p.EditorID != null && p.EditorID.Contains(pattern))
               .Select(p => p.FormKey)
               .Distinct()
               .ToList();

    private static List<FormKey> FindByEditorIds(IEnumerable<IStarfieldModGetter> mods, IEnumerable<string> editorIds)
    {
        var wanted = new HashSet<string>(editorIds);
        if (wanted.Count == 0) return [];

        var found  = new HashSet<string>();
        var result = new List<FormKey>();

        foreach (var p in mods.SelectMany(m => m.PackIns))
            if (p.EditorID != null && wanted.Contains(p.EditorID) && found.Add(p.EditorID))
                result.Add(p.FormKey);

        foreach (var missing in wanted.Except(found))
            Console.Error.WriteLine($"[IndustryPackInLibraryPass] WARNING: PackIn '{missing}' not found in any mod");

        return result;
    }
}
