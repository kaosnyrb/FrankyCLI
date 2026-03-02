using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// Scans a built mod for FormLink references that point into template mods.
    /// Detects unintended template dependencies that should have been deep-copied.
    /// Usage: gen_deprefscan [modname]
    /// </summary>
    public class gen_deprefscan
    {
        public static int Run(string modname)
        {
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build();

            var readParams = gen_quest_main.BuildReadParams(env.LoadOrder);

            ModKey targetKey = new ModKey(modname, ModType.Master);
            if (!env.LoadOrder.ModExists(targetKey))
            {
                Console.WriteLine($"Mod '{modname}.esm' not found in load order.");
                return 1;
            }

            string modFilePath = Path.Combine(env.DataFolderPath, modname + ".esm");
            var mod = StarfieldMod.CreateFromBinaryOverlay(
                new ModPath(targetKey, modFilePath),
                StarfieldRelease.Starfield, readParams);

            Console.WriteLine($"=== Dependency Scan: {modname}.esm ===");
            Console.WriteLine();

            // --- 1. Check declared master list ---
            Console.WriteLine("--- Declared Masters ---");
            var templateMasters = new List<ModKey>();
            foreach (var master in mod.ModHeader.MasterReferences)
            {
                bool isTemplate = master.Master.FileName.String.Contains("template", StringComparison.OrdinalIgnoreCase);
                string flag = isTemplate ? "  <<< TEMPLATE DEPENDENCY" : "";
                Console.WriteLine($"  {master.Master.FileName}{flag}");
                if (isTemplate)
                    templateMasters.Add(master.Master);
            }
            Console.WriteLine();

            if (templateMasters.Count == 0)
            {
                Console.WriteLine("No template mod masters declared. Mod is clean.");
                return 0;
            }

            Console.WriteLine($"WARNING: {templateMasters.Count} template master(s) found!");
            Console.WriteLine($"  {string.Join(", ", templateMasters.Select(m => m.FileName))}");
            Console.WriteLine();

            // --- 2. Build EditorID lookup from template mods if present in load order ---
            var templateEditorIds = new Dictionary<FormKey, string>();
            var templateModKeys = new HashSet<ModKey>(templateMasters);
            foreach (var listing in env.LoadOrder.ListedOrder)
            {
                if (listing.Mod == null) continue;
                if (!templateModKeys.Contains(listing.Mod.ModKey)) continue;
                Console.WriteLine($"  (Resolving EditorIDs from {listing.FileName}...)");
                foreach (var rec in listing.Mod.EnumerateMajorRecords())
                {
                    if (!string.IsNullOrEmpty(rec.EditorID))
                        templateEditorIds[rec.FormKey] = rec.EditorID;
                }
            }
            Console.WriteLine();

            // --- 3. Scan all records for FormLinks pointing into template mods ---
            Console.WriteLine("--- Records with Template References ---");
            int hitCount = 0;

            foreach (var rec in mod.EnumerateMajorRecords())
            {
                if (rec is not IFormLinkContainerGetter container) continue;

                var hits = new List<FormKey>();
                foreach (var link in container.EnumerateFormLinks())
                {
                    if (link.FormKey.IsNull) continue;
                    if (templateModKeys.Contains(link.FormKey.ModKey))
                        hits.Add(link.FormKey);
                }

                if (hits.Count == 0) continue;

                string recType = rec.GetType().Name;
                if (recType.EndsWith("BinaryOverlay"))
                    recType = recType[..^"BinaryOverlay".Length];

                Console.WriteLine($"  [{recType}] {rec.EditorID ?? rec.FormKey.ToString()}  ({rec.FormKey})");
                foreach (var fk in hits.Distinct())
                {
                    string eid = templateEditorIds.TryGetValue(fk, out var e) ? e : "?";
                    Console.WriteLine($"    -> {fk}  [{eid}]");
                }
                hitCount++;
            }

            Console.WriteLine();
            Console.WriteLine($"=== Scan complete. {hitCount} record(s) with template references. ===");
            return 0;
        }
    }
}
