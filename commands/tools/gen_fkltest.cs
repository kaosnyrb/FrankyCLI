using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Retrograde;
using Retrograde.Abstractions;
using Retrograde.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace FrankyCLI
{
    /// <summary>
    /// Diagnostic tool for FormKeyLookup enumeration regressions after a Starfield.esm update.
    ///
    /// Two probes, run back-to-back:
    ///   1. SAFE WALK  — EnumerateSafe over EnumerateMajorRecords(), same path FormKeyLookup uses.
    ///                   Counts total records, breaks down by type, reports exceptions.
    ///                   Guards against infinite-loop (consecutive exceptions) with a hard cap.
    ///   2. GROUP WALK — Reflection over all IStarfieldModGetter properties whose values implement
    ///                   IGroupGetter (have Count + EnumerateMajorRecords).  Counts independently.
    ///
    /// Comparing the two totals reveals whether the issue is in the unified iterator vs individual groups.
    ///
    /// Usage: gen_fkltest
    /// </summary>
    public class gen_fkltest
    {
        private const int ConsecutiveExceptionLimit = 50; // give up on the iterator after this many in a row

        public static int Run()
        {
            Console.WriteLine("=== gen_fkltest: FormKeyLookup enumeration diagnostic ===");
            Console.WriteLine();

            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build();

            var readParams = gen_quest_main.BuildReadParams(env.LoadOrder);

            var sfKey = new ModKey("Starfield", ModType.Master);
            if (!env.LoadOrder.ModExists(sfKey))
            {
                Console.WriteLine("ERROR: Starfield.esm not found in load order.");
                return 1;
            }

            var sfMod = env.LoadOrder[sfKey].Mod;
            if (sfMod == null)
            {
                Console.WriteLine("ERROR: Starfield.esm loaded but Mod is null.");
                return 1;
            }

            Console.WriteLine($"Starfield.esm loaded. Running probes...");
            Console.WriteLine();

            // ── Probe 1: Safe Walk (mirrors FormKeyLookup exactly) ────────────────────────────
            Console.WriteLine("── Probe 1: EnumerateSafe over EnumerateMajorRecords() ──────────────────");
            var safeTypeCounts   = new Dictionary<string, int>();
            int safeTotal        = 0;
            int safeExceptions   = 0;
            int consecutiveExc   = 0;

            using (var en = sfMod.EnumerateMajorRecords().GetEnumerator())
            {
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = en.MoveNext();
                        consecutiveExc = 0; // reset on success
                    }
                    catch (Exception ex)
                    {
                        safeExceptions++;
                        consecutiveExc++;
                        Console.WriteLine($"  [EXCEPTION #{safeExceptions}] {ex.GetType().Name}: {ex.Message}");
                        if (consecutiveExc >= ConsecutiveExceptionLimit)
                        {
                            Console.WriteLine($"  [ABORT] {consecutiveExc} consecutive exceptions — iterator is stuck. Stopping Probe 1.");
                            break;
                        }
                        continue;
                    }

                    if (!moved) break;

                    safeTotal++;
                    var rec = en.Current;
                    string typeName = rec.GetType().Name;
                    if (typeName.EndsWith("BinaryOverlay"))
                        typeName = typeName[..^"BinaryOverlay".Length];

                    safeTypeCounts.TryGetValue(typeName, out int existing);
                    safeTypeCounts[typeName] = existing + 1;
                }
            }

            Console.WriteLine($"  Total records found:   {safeTotal:N0}");
            Console.WriteLine($"  Total exceptions:      {safeExceptions}");
            Console.WriteLine($"  Unique types seen:     {safeTypeCounts.Count}");
            Console.WriteLine();
            if (safeTypeCounts.Count > 0)
            {
                Console.WriteLine("  Type breakdown (top 30 by count):");
                var sorted = new List<KeyValuePair<string, int>>(safeTypeCounts);
                sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
                int shown = 0;
                foreach (var (type, count) in sorted)
                {
                    Console.WriteLine($"    {count,8:N0}  {type}");
                    if (++shown >= 30) { Console.WriteLine($"    ... ({sorted.Count - 30} more types)"); break; }
                }
            }
            Console.WriteLine();

            // ── Probe 2: Group Walk (per-group typed access via reflection) ──────────────────
            Console.WriteLine("── Probe 2: Per-group typed access via reflection ───────────────────────");
            int groupTotal      = 0;
            int groupsFound     = 0;
            int groupErrors     = 0;
            var groupResults    = new List<(string Name, int Count, string? Error)>();

            // Groups are declared as IStarfieldGroupGetter<T> or IStarfieldListGroupGetter<T>
            // on the mod properties. Identify them by property type name.
            foreach (var prop in sfMod.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propTypeName = prop.PropertyType.Name;
                if (!propTypeName.StartsWith("IStarfieldGroupGetter") &&
                    !propTypeName.StartsWith("IStarfieldListGroupGetter")) continue;

                object? val;
                try { val = prop.GetValue(sfMod); }
                catch { continue; }
                if (val == null) continue;

                var valType = val.GetType();

                // Count via the Count property.
                var countProp = valType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                if (countProp == null) continue;

                int count = 0;
                string? error = null;
                try
                {
                    count = (int)countProp.GetValue(val)!;
                }
                catch (Exception ex)
                {
                    error = $"Count threw: {ex.GetType().Name}: {ex.Message}";
                    groupErrors++;
                    groupResults.Add((prop.Name, -1, error));
                    continue;
                }

                // Also probe enumeration of the Values to verify the group can actually be walked.
                // (Count can succeed even if individual records throw during parse.)
                if (count > 0)
                {
                    var valuesProp = valType.GetProperty("Values", BindingFlags.Public | BindingFlags.Instance);
                    if (valuesProp != null)
                    {
                        try
                        {
                            var values = valuesProp.GetValue(val) as System.Collections.IEnumerable;
                            if (values != null)
                            {
                                int probed = 0;
                                foreach (var item in values)
                                {
                                    // Access EditorID to force lazy parse (BinaryOverlay is lazy).
                                    if (item is IMajorRecordGetter rec)
                                        _ = rec.EditorID;
                                    if (++probed >= 5) break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            error = $"Enum probe threw: {ex.GetType().Name}: {ex.Message}";
                            groupErrors++;
                        }
                    }
                }

                groupResults.Add((prop.Name, count, error));
                if (error == null) { groupTotal += count; groupsFound++; }
            }

            // Sort by count descending
            groupResults.Sort((a, b) => b.Count.CompareTo(a.Count));

            Console.WriteLine($"  Groups found via reflection:  {groupResults.Count}");
            Console.WriteLine($"  Groups read without error:    {groupsFound}");
            Console.WriteLine($"  Groups with errors:           {groupErrors}");
            Console.WriteLine($"  Total records across groups:  {groupTotal:N0}");
            Console.WriteLine();
            Console.WriteLine("  Per-group counts:");
            foreach (var (name, count, err) in groupResults)
            {
                if (err != null)
                    Console.WriteLine($"    {"ERROR",8}  {name}  [{err}]");
                else
                    Console.WriteLine($"    {count,8:N0}  {name}");
            }
            Console.WriteLine();

            // ── Summary ──────────────────────────────────────────────────────────────────────
            Console.WriteLine("── Summary ──────────────────────────────────────────────────────────────");
            Console.WriteLine($"  Probe 1 (EnumerateMajorRecords): {safeTotal:N0} records, {safeExceptions} exceptions");
            Console.WriteLine($"  Probe 2 (per-group reflection):  {groupTotal:N0} records across {groupsFound} groups");
            int delta = groupTotal - safeTotal;
            if (delta > 0)
                Console.WriteLine($"  DELTA: Probe 2 found {delta:N0} MORE records — EnumerateMajorRecords is stopping early.");
            else if (delta < 0)
                Console.WriteLine($"  DELTA: Probe 1 found {-delta:N0} MORE records than sum of groups (unexpected).");
            else
                Console.WriteLine($"  DELTA: Both probes agree — no iterator gap detected.");

            if (safeExceptions > 0)
                Console.WriteLine($"  => {safeExceptions} exception(s) in iterator — likely a new/changed record type Mutagen can't parse.");
            if (delta > 0 && safeExceptions == 0)
                Console.WriteLine($"  => Iterator stopped without exceptions — likely a Mutagen update needed for new ESM structure.");

            // ── Probe 3: FormKeyLookup end-to-end (new per-group code path) ─────────────────
            Console.WriteLine("── Probe 3: FormKeyLookup end-to-end (per-group fix) ───────────────────");

            // Build a minimal context using just Starfield.esm so we can call FormKeyLookup directly.
            gen_quest_main.StarfieldModKey = sfKey;
            gen_quest_main._StarfieldMod   = sfMod;
            gen_quest_main.myMod           = new StarfieldMod(new ModKey("fkltest_dummy", ModType.Master), StarfieldRelease.Starfield);
            ModContextImpl.TemplateModsList = [sfMod];  // include sfMod as its own template so the cache scan runs
            FormKeyLookup.InitializeCache(env.DataFolderPath, [Path.Combine(env.DataFolderPath, "Starfield.esm")]);
            RetrogradeContext.Current = new ModContextImpl();

            // Look up a handful of well-known Starfield.esm EditorIDs from different groups.
            var probes = new[]
            {
                "PlayerAllyFaction",              // Faction
                "Outfit_Spacesuit_UCMarine_Any",  // Outfit
                "LvlHumanHostile_Boss",           // Npc
                "LvlHumanHostile_Assault",        // Npc (different record)
                "LvlHumanHostile_Sniper",         // Npc (different record)
            };

            int p3ok = 0, p3fail = 0;
            foreach (var eid in probes)
            {
                try
                {
                    var fk = FormKeyLookup.GetFormKey(eid);
                    Console.WriteLine($"  OK   {eid} => {fk}");
                    p3ok++;
                }
                catch (KeyNotFoundException)
                {
                    Console.WriteLine($"  MISS {eid}");
                    p3fail++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"  {p3ok}/{probes.Length} lookups succeeded, {p3fail} misses.");
            if (p3fail == 0)
                Console.WriteLine("  Probe 3 PASSED — FormKeyLookup per-group fix is working.");
            else
                Console.WriteLine("  Probe 3 FAILED — check misses above.");

            Console.WriteLine();
            Console.WriteLine("=== Done ===");
            return 0;
        }
    }
}
