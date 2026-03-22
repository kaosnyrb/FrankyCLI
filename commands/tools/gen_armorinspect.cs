using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FrankyCLI
{
    /// <summary>
    /// Investigates legendary armor OMODs, attach point keywords, and LGDI records.
    /// Usage: gen_armorinspect
    /// </summary>
    public class gen_armorinspect
    {
        public static int Run()
        {
            Console.WriteLine("=== Legendary Armor OMOD Inspector ===");
            Console.WriteLine();

            using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            var sfMod = env.LoadOrder[0].Mod;
            var sfModKey = env.LoadOrder[0].ModKey;

            if (sfMod == null)
            {
                Console.WriteLine("ERROR: Could not load Starfield.esm");
                return 1;
            }

            // === Direct FormKey lookups ===
            var directOmods = new Dictionary<string, uint>
            {
                { "mod_Legendary_Armor_01_Chameleon", 0x1336C1 },
                { "mod_Legendary_Armor_03_Incendiary", 0x002983 },
                { "mod_Legendary_Pack_03_ArmorPlated", 0x2EDE59 },
                { "mod_Legendary_Armor_02_CarryWeight_Resources", 0x060293 },
                { "mod_Legendary_Armor_02_FallDamage", 0x0710FD },
            };

            // === Search-based lookups ===
            var searchOmods = new[]
            {
                "mod_Legendary_Armor_01_LessDmgBallistic",
                "mod_Legendary_Helmet_01_OxygenFilter",
                "mod_Legendary_Armor_02_AutoHeal",
                "mod_Legendary_Armor_01_InverseHealthResist",
            };

            // Collect all OMODs to dump
            var omodsToDump = new List<IAObjectModificationGetter>();

            // Direct lookups
            foreach (var kvp in directOmods)
            {
                try
                {
                    var fk = new FormKey(sfModKey, kvp.Value);
                    var omod = sfMod.ObjectModifications[fk];
                    omodsToDump.Add(omod);
                    Console.WriteLine($"[OK] Found {kvp.Key} via direct lookup (0x{kvp.Value:X6})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FAIL] {kvp.Key} (0x{kvp.Value:X6}): {ex.Message}");
                }
            }

            // Search-based lookups
            foreach (var edid in searchOmods)
            {
                try
                {
                    var omod = sfMod.ObjectModifications.FirstOrDefault(o => o.EditorID == edid);
                    if (omod != null)
                    {
                        omodsToDump.Add(omod);
                        Console.WriteLine($"[OK] Found {edid} via search ({omod.FormKey})");
                    }
                    else
                    {
                        Console.WriteLine($"[MISS] {edid} not found by exact EditorID match");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FAIL] Search for {edid}: {ex.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("=== OMOD DETAILS ===");
            Console.WriteLine("========================================");

            foreach (var omod in omodsToDump)
            {
                DumpOmod(omod);
            }

            // === Attach Point Keywords ===
            Console.WriteLine("========================================");
            Console.WriteLine("=== ATTACH POINT KEYWORDS ===");
            Console.WriteLine("========================================");

            var attachPoints = new Dictionary<string, uint>
            {
                { "Tier01 attach point", 0x1E32C8 },
                { "Tier02 attach point", 0x329ABC },
                { "Tier03 attach point", 0x329ABD },
                { "Tier4/None attach point", 0x3197E8 },
            };

            foreach (var kvp in attachPoints)
            {
                try
                {
                    var fk = new FormKey(sfModKey, kvp.Value);
                    var kw = sfMod.Keywords[fk];
                    Console.WriteLine($"  0x{kvp.Value:X6} => EditorID: {kw.EditorID}, FormKey: {kw.FormKey}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  0x{kvp.Value:X6} => FAILED: {ex.Message}");
                }
            }
            Console.WriteLine();

            // === LGDI: DefaultLegendaryArmor ===
            Console.WriteLine("========================================");
            Console.WriteLine("=== LGDI: DefaultLegendaryArmor (0x001336C3) ===");
            Console.WriteLine("========================================");

            try
            {
                var lgdi = sfMod.LegendaryItems[new FormKey(sfModKey, 0x001336C3)];
                Console.WriteLine($"  EditorID: {lgdi.EditorID}");
                Console.WriteLine($"  FormKey: {lgdi.FormKey}");

                // LegendaryMods
                if (lgdi.LegendaryMods != null && lgdi.LegendaryMods.Count > 0)
                {
                    Console.WriteLine($"  LegendaryMods: [{lgdi.LegendaryMods.Count} items]");
                    for (int i = 0; i < lgdi.LegendaryMods.Count; i++)
                    {
                        var lm = lgdi.LegendaryMods[i];
                        Console.WriteLine($"    [{i}] Type={lm.GetType().Name}");
                        DumpAllProperties(lm, "      ");
                    }
                }
                else
                {
                    Console.WriteLine("  LegendaryMods: (empty or null)");
                }

                // IncludeFilters
                if (lgdi.IncludeFilters != null && lgdi.IncludeFilters.Count > 0)
                {
                    Console.WriteLine($"  IncludeFilters: [{lgdi.IncludeFilters.Count} items]");
                    for (int i = 0; i < lgdi.IncludeFilters.Count; i++)
                    {
                        var f = lgdi.IncludeFilters[i];
                        Console.WriteLine($"    [{i}] Type={f.GetType().Name}");
                        DumpAllProperties(f, "      ");
                    }
                }
                else
                {
                    Console.WriteLine("  IncludeFilters: (empty or null)");
                }

                // Dump all other top-level properties
                Console.WriteLine("  --- All top-level properties ---");
                DumpAllProperties(lgdi, "    ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAILED: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("=== Done ===");
            return 0;
        }

        private static void DumpOmod(IAObjectModificationGetter omod)
        {
            Console.WriteLine();
            Console.WriteLine($"--- {omod.EditorID} ---");
            Console.WriteLine($"  FormKey: {omod.FormKey}");
            Console.WriteLine($"  ConcreteType: {omod.GetType().Name}");

            // Dump ALL properties via reflection to see what's actually available
            var type = omod.GetType();
            var allProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0)
                .Where(p => p.Name != "Registration" && p.Name != "StaticRegistration")
                .OrderBy(p => p.Name)
                .ToList();

            Console.WriteLine($"  Available properties on type:");
            foreach (var p in allProps)
            {
                Console.WriteLine($"    {p.Name} : {p.PropertyType.Name}");
            }
            Console.WriteLine();

            // AttachPoint
            var apProp = type.GetProperty("AttachPoint");
            if (apProp != null)
            {
                var apVal = apProp.GetValue(omod);
                Console.WriteLine($"  AttachPoint: {apVal}");
            }

            // Description
            var descProp = type.GetProperty("Description");
            if (descProp != null)
            {
                var descVal = descProp.GetValue(omod);
                Console.WriteLine($"  Description: {descVal}");
            }

            // Try to find properties/data collections by scanning all properties
            foreach (var prop in allProps)
            {
                try
                {
                    var val = prop.GetValue(omod);
                    if (val == null) continue;

                    if (val is System.Collections.IEnumerable enumerable && !(val is string))
                    {
                        var items = new List<object>();
                        foreach (var item in enumerable)
                        {
                            items.Add(item);
                            if (items.Count > 50) break;
                        }
                        if (items.Count > 0)
                        {
                            Console.WriteLine($"  {prop.Name}: [{items.Count} items] (element type: {items[0].GetType().Name})");
                            for (int i = 0; i < Math.Min(items.Count, 30); i++)
                            {
                                var item = items[i];
                                Console.WriteLine($"    [{i}] {item.GetType().Name}");
                                DumpAllProperties(item, "      ");
                            }
                        }
                    }
                }
                catch { /* skip */ }
            }

            Console.WriteLine();
        }

        private static void DumpAllProperties(object obj, string indent)
        {
            if (obj == null) return;
            var type = obj.GetType();
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0)
                .Where(p => p.Name != "Registration" && p.Name != "StaticRegistration")
                .OrderBy(p => p.Name);

            foreach (var prop in props)
            {
                try
                {
                    var val = prop.GetValue(obj);
                    if (val == null) continue;

                    var valType = val.GetType();

                    if (val is System.Collections.ICollection coll && coll.Count == 0) continue;

                    if (valType.IsPrimitive || val is string || val is FormKey || val is Enum
                        || val is Noggog.P3Float || val is Noggog.P2Float || val is decimal)
                    {
                        Console.WriteLine($"{indent}{prop.Name}: {val}");
                    }
                    else if (valType.Name.Contains("FormLink"))
                    {
                        Console.WriteLine($"{indent}{prop.Name}: {val}");
                    }
                    else if (valType.Name.Contains("TranslatedString"))
                    {
                        Console.WriteLine($"{indent}{prop.Name}: {val}");
                    }
                    else if (val is System.Collections.ICollection coll2)
                    {
                        Console.WriteLine($"{indent}{prop.Name}: [{coll2.Count} items]");
                        int idx = 0;
                        foreach (var item in coll2)
                        {
                            if (idx >= 20) { Console.WriteLine($"{indent}  ... truncated"); break; }
                            Console.WriteLine($"{indent}  [{idx}] {item}");
                            idx++;
                        }
                    }
                    else if (valType.Name.Contains("MemorySlice"))
                    {
                        Console.WriteLine($"{indent}{prop.Name}: <binary>");
                    }
                    else
                    {
                        Console.WriteLine($"{indent}{prop.Name}: {val}");
                    }
                }
                catch { /* skip */ }
            }
        }
    }
}
