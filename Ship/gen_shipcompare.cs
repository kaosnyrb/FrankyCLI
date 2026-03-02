using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Parameters;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    public class gen_shipcompare
    {
        static IStarfieldModGetter? _sfMod;

        static string ResolveEditorId(FormKey fk)
        {
            if (_sfMod == null) return fk.ToString();
            var lvl = _sfMod.LeveledBaseForms.FirstOrDefault(r => r.FormKey == fk);
            if (lvl != null) return $"{fk} ({lvl.EditorID ?? "?"})";
            var gbf = _sfMod.GenericBaseForms.FirstOrDefault(r => r.FormKey == fk);
            if (gbf != null) return $"{fk} ({gbf.EditorID ?? "?"})";
            return fk.ToString();
        }

        static void PrintVanillaShip(IStarfieldModGetter sfMod, ModKey sfKey, uint formId, string hint)
        {
            var fk = new FormKey(sfKey, formId);
            var ship = sfMod.GenericBaseForms.FirstOrDefault(r => r.FormKey == fk);
            if (ship == null) { Console.WriteLine($"  [VANILLA 0x{formId:X8} {hint}] NOT FOUND"); return; }
            PrintShip(ship, $"VANILLA/{hint}");
        }

        static void PrintShip(IGenericBaseFormGetter s, string label)
        {
            Console.WriteLine($"\n--- {label}: {s.EditorID} ---");
            Console.WriteLine($"  Template.IsNull:        {s.Template.IsNull}");
            Console.WriteLine($"  Template.FormKey:       {(s.Template.IsNull ? "(null)" : s.Template.FormKey.ToString())}");
            Console.WriteLine($"  ObjectTemplates:        {s.ObjectTemplates?.Count ?? -1}");
            Console.WriteLine($"  InstanceData count:     {s.ObjectTemplateInstanceData?.Count ?? -1}");
            Console.WriteLine($"  Components:             {s.Components?.Count ?? -1}");
            Console.WriteLine($"  VirtualMachineAdapter:  {(s.VirtualMachineAdapter == null ? "NULL" : s.VirtualMachineAdapter.Scripts.Count + " scripts")}");
            Console.WriteLine($"  NavmeshGeometry:        {(s.NavmeshGeometry == null ? "NULL" : "present")}");
            Console.WriteLine($"  MajorRecordFlagsRaw:    0x{s.MajorRecordFlagsRaw:X8}");
            Console.WriteLine($"  Filter:                 {s.Filter ?? "(null)"}");
            if (s.Components != null)
            {
                try
                {
                    foreach (var c in s.Components)
                    {
                        if (c is IExternalDataSourceComponentGetter edsc)
                        {
                            Console.WriteLine($"  ExternalDataSource ({edsc.Sources?.Count ?? 0} sources):");
                            if (edsc.Sources != null)
                                foreach (var src in edsc.Sources)
                                    Console.WriteLine($"    [{src.Name}] -> {ResolveEditorId(src.Source.FormKey)}");
                        }
                        else Console.WriteLine($"  Component: {c.GetType().FullName}");
                    }
                }
                catch (Exception ex) { Console.WriteLine($"  [Components ERROR: {ex.Message}]"); }
            }
            Console.WriteLine("  [past components]");
            var templates = s.ObjectTemplates;
            Console.WriteLine($"  Templates null={templates == null}, count={templates?.Count ?? -1}");
            if (templates != null)
                for (int ti = 0; ti < templates.Count; ti++)
                {
                    var t = templates[ti];
                    var includes = t.Includes;
                    Console.WriteLine($"  ObjectTemplate[{ti}]: Name={t.Name?.String ?? "(null)"}  Includes={includes?.Count ?? 0}  Props={t.Properties?.Count ?? 0}");
                    if (includes != null)
                        for (int ii = 0; ii < includes.Count; ii++)
                            Console.WriteLine($"    Include[{ii}].Mod={includes[ii].Mod.FormKey}  AttachIdx={includes[ii].AttachPointIndex}");
                }
            if (s.ObjectTemplateInstanceData?.Count > 0)
                foreach (var d in s.ObjectTemplateInstanceData)
                    Console.WriteLine($"  InstanceData len: {d?.Length ?? -1}");
        }

        public static int Run()
        {
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build();

            // Build master flags cache from load order
            var masterFlagsCache = new Cache<IModMasterStyledGetter, ModKey>(m => m.ModKey);
            foreach (var listing in env.LoadOrder.ListedOrder)
                if (listing.Mod != null)
                    masterFlagsCache.Set(listing.Mod);
            var readParams = new BinaryReadParameters() { MasterFlagsLookup = masterFlagsCache };

            // Grab Starfield.esm for EditorID resolution and vanilla ship inspection
            var sfKey = new ModKey("Starfield", ModType.Master);
            for (int i = 0; i < env.LoadOrder.Count; i++)
            {
                var listing = env.LoadOrder[i];
                if (listing.Mod != null &&
                    listing.FileName.ToString().Equals("Starfield.esm", StringComparison.OrdinalIgnoreCase))
                {
                    _sfMod = listing.Mod;
                    break;
                }
            }

            var working = StarfieldMod.CreateFromBinaryOverlay(
                new ModPath(new ModKey("du_outlaws_01", ModType.Master),
                    @"C:\Users\kaosn\Dropbox\modding\DU_Outlaws\bk\release\du_outlaws_01.esm"),
                StarfieldRelease.Starfield, readParams);

            var failing = StarfieldMod.CreateFromBinaryOverlay(
                new ModPath(new ModKey("outlaws02", ModType.Master),
                    @"C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data\outlaws02.esm"),
                StarfieldRelease.Starfield, readParams);

            Console.WriteLine($"du_outlaws_01 GenericBaseForms: {working.GenericBaseForms.Count}");
            Console.WriteLine($"outlaws02     GenericBaseForms: {failing.GenericBaseForms.Count}");

            int wCount = 0, fCount = 0;
            foreach (var s in working.GenericBaseForms)
                if (s.EditorID?.StartsWith("encship_") == true) { PrintShip(s, "WORKING"); wCount++; }
            foreach (var s in failing.GenericBaseForms)
                if (s.EditorID?.StartsWith("encship_") == true) { PrintShip(s, "FAILING"); fCount++; }

            Console.WriteLine($"\nTotal: {wCount} working, {fCount} failing");

            // ---- Vanilla source ship inspection ----
            if (_sfMod != null)
            {
                Console.WriteLine("\n\n=== VANILLA SOURCE SHIPS ===");
                PrintVanillaShip(_sfMod, sfKey, 0x00322C13, "Spacer_A");
                PrintVanillaShip(_sfMod, sfKey, 0x00322C19, "Spacer_B");
                PrintVanillaShip(_sfMod, sfKey, 0x0031DF00, "CrimsonFleet_A");
                PrintVanillaShip(_sfMod, sfKey, 0x00331AC7, "TradeAuth_Cargo");
                PrintVanillaShip(_sfMod, sfKey, 0x000423B1, "TradeAuth_Cargo2");
                PrintVanillaShip(_sfMod, sfKey, 0x00322BF7, "Ecliptic_A");
                PrintVanillaShip(_sfMod, sfKey, 0x00322BFF, "Ecliptic_B");
                PrintVanillaShip(_sfMod, sfKey, 0x0030ED45, "Galbank_B");
            }
            else
            {
                Console.WriteLine("\n[Starfield.esm not in load order - vanilla inspection skipped]");
            }

            return 0;
        }
    }
}
