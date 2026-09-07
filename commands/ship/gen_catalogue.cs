using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FrankyCLI
{
    /// <summary>
    /// The CATALOGUE census: what a mod actually sells, and where each thing lands in the
    /// ship builder's menu.
    ///
    /// WHY THIS EXISTS. Nothing could answer "what does the line sell" without somebody
    /// hand-rolling it. The census was run by hand on 2026-09-07 and threw off four things a
    /// list of EditorIDs would never have shown -- a live recipe named "Avontech Test Gear",
    /// 13 of 24 recipes shipping their editor stub as the player-facing Description, 22 of 24
    /// sitting on gen_shipstruct's default Value of 1000, and 61 of 63 modules filed under
    /// ShipModuleManufacturerDeimos. A method hand-rolled once whose only artifact is its
    /// OUTPUT is an unversioned instrument; this is that method committed.
    ///
    /// ⛔ IT IS SCOPED TO ONE PLUGIN, AND THAT IS THE POINT RATHER THAN A CONVENIENCE.
    /// The load order carries avontechstardust.esm (active) AND avontechstardust.esp (an older
    /// twin, listed without the '*'). Mutagen loads both, so the hand-rolled first pass counted
    /// 44 recipes -- 24 live plus 20 stale duplicates -- and a long list looks exactly like work.
    /// ModKey.Name is "avontechstardust" for BOTH, so matching on the bare name silently picks
    /// whichever comes first in the load order. This refuses an ambiguous name loudly and prints
    /// the candidates; it does not guess, and it does not quietly take the first.
    ///
    /// ⛔ IT READS RecipeFilters, WHICH gen_inspect DOES NOT PRINT. The category keyword is the
    /// single field deciding which TAB of the builder a part appears under, and it has been
    /// invisible to every dump this office has ever taken of a COBJ. Same shape as the RQPK
    /// note in gen_inspect's own COBJ dumper: an absent field prints as nothing, and nothing is
    /// invisible.
    ///
    /// WHAT IT REPORTS AND WHAT IT REFUSES TO. Facts only, the gen_checkpart posture -- no
    /// verdicts, no thresholds, no "this looks wrong". A description IS printed; whether it
    /// reads like a stub is the reader's call. The one derived number is the count, and every
    /// count says what it counted.
    ///
    /// Usage: catalogue &lt;modname&gt; [--json]
    ///   modname: "avontechstardust.esm" (exact, preferred) or "avontechstardust" (refused if
    ///   more than one plugin in the load order carries that name).
    /// </summary>
    public class gen_catalogue
    {
        // A GBFM is a ship module when its Template is FormSpaceshipModule. Keyed on the
        // TEMPLATE and never on an EditorID prefix: this line already carries atsd_gbfm_*,
        // atsd_gbf_* and atsd_bf_* for the same kind of thing, and a name is a handle, not a
        // specification. The 2026-09-03 corpus sweep missed the hab for exactly that reason.
        private const uint SpaceshipModuleTemplateId = 0x0003058E;

        public static int Generate(string[] args)
        {
            string modname = args[1];
            bool json = args.Contains("--json", StringComparer.OrdinalIgnoreCase);

            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            var cache = env.LinkCache;

            var mod = ResolveMod(env, modname, out string? error);
            if (mod == null) { Console.WriteLine(error); return 1; }

            var sfKey = env.LoadOrder[0].ModKey;
            var templateKey = new FormKey(sfKey, SpaceshipModuleTemplateId);

            // ---- modules in this plugin ------------------------------------------------
            var modules = new Dictionary<FormKey, ModuleInfo>();
            foreach (var g in mod.GenericBaseForms)
            {
                bool isShipModule = !g.Template.IsNull && g.Template.FormKey == templateKey;
                if (!isShipModule) continue;
                modules[g.FormKey] = Describe(g);
            }

            // ---- recipes in this plugin ------------------------------------------------
            var recipes = new List<RecipeInfo>();
            foreach (var co in mod.ConstructibleObjects)
            {
                var r = new RecipeInfo
                {
                    EditorId = co.EditorID ?? "(none)",
                    FormKey = co.FormKey.ToString(),
                    Description = co.Description?.ToString() ?? "",
                    Value = co.Value,
                    MenuSortOrder = co.MenuSortOrder,
                    Workbench = Name(co.WorkbenchKeyword.FormKey, cache),
                    Categories = (co.RecipeFilters ?? Enumerable.Empty<IFormLinkGetter<IKeywordGetter>>())
                                 .Select(f => Name(f.FormKey, cache)).ToList(),
                    LevelGate = LevelGate(co),
                    CreatedObject = Name(co.CreatedObject.FormKey, cache),
                };

                // CreatedObject is either a module directly, or a FormList of the flip-set.
                if (cache.TryResolve<IFormListGetter>(co.CreatedObject.FormKey, out var flst))
                {
                    r.CreatedKind = "FormList";
                    foreach (var it in flst.Items) r.Members.Add(it.FormKey);
                }
                else
                {
                    r.CreatedKind = modules.ContainsKey(co.CreatedObject.FormKey) ? "GBFM" : "other";
                    r.Members.Add(co.CreatedObject.FormKey);
                }
                recipes.Add(r);
            }

            // ---- reachability -----------------------------------------------------------
            var reached = new HashSet<FormKey>();
            foreach (var r in recipes) foreach (var m in r.Members) reached.Add(m);
            var orphans = modules.Keys.Where(k => !reached.Contains(k)).ToList();
            var offPlugin = reached.Where(k => !modules.ContainsKey(k)).ToList();

            if (json) { EmitJson(mod, recipes, modules, orphans, offPlugin, cache); return 0; }
            EmitText(mod, recipes, modules, orphans, offPlugin, cache);
            return 0;
        }

        // ---------------------------------------------------------------------------------

        private static IStarfieldModGetter? ResolveMod(
            IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, string modname, out string? error)
        {
            error = null;
            var listed = env.LoadOrder.ListedOrder.Where(l => l.Mod != null).ToList();

            var exact = listed.Where(l => string.Equals(l.ModKey.ToString(), modname, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 1) return exact[0].Mod;

            var byName = listed.Where(l => string.Equals(l.ModKey.Name, modname, StringComparison.OrdinalIgnoreCase)).ToList();
            if (byName.Count == 1) return byName[0].Mod;
            if (byName.Count > 1)
            {
                error = $"'{modname}' is ambiguous -- {byName.Count} plugins in the load order carry that name:\n"
                      + string.Join("\n", byName.Select(l => $"    {l.ModKey}"))
                      + "\n  Pass the full filename. A bare name would silently pick the first, and that is\n"
                      + "  how a catalogue gets counted twice.";
                return null;
            }
            error = $"'{modname}' is not in the load order. Enable it, or pass the full filename.";
            return null;
        }

        private static ModuleInfo Describe(IGenericBaseFormGetter g)
        {
            var m = new ModuleInfo { EditorId = g.EditorID ?? "(none)", FormKey = g.FormKey.ToString() };
            if (g.Components == null) return m;
            foreach (var c in g.Components)
            {
                if (c is IFullNameComponentGetter fn) m.FullName = fn.Name?.ToString() ?? "";
                else if (c is IKeywordFormComponentGetter kw && kw.Keywords != null)
                    foreach (var k in kw.Keywords) m.KeywordKeys.Add(k.FormKey);
            }
            return m;
        }

        /// <summary>
        /// The level gate, or null. Read off the condition FUNCTION rather than the recipe's
        /// EditorID: the vanilla `_lvlNN` label and the real GetLevel value disagree by four on
        /// at least one shipped record, so the label is not the gate.
        /// </summary>
        private static string? LevelGate(IConstructibleObjectGetter co)
        {
            if (co.Conditions == null) return null;
            foreach (var cond in co.Conditions)
            {
                if (cond.Data?.GetType().Name.Contains("GetLevel", StringComparison.OrdinalIgnoreCase) != true) continue;
                string val = cond is IConditionFloatGetter cf ? ((int)cf.ComparisonValue).ToString() : "?";
                // Short symbol where we know one; otherwise the enum name, verbatim. An operator
                // this map has never met must still render -- honestly and wide -- rather than
                // silently becoming one we do know.
                string op = cond.CompareOperator switch
                {
                    CompareOperator.EqualTo => "==",
                    CompareOperator.NotEqualTo => "!=",
                    CompareOperator.GreaterThan => ">",
                    CompareOperator.GreaterThanOrEqualTo => ">=",
                    CompareOperator.LessThan => "<",
                    CompareOperator.LessThanOrEqualTo => "<=",
                    _ => cond.CompareOperator.ToString(),
                };
                return $"{op} {val}";
            }
            return null;
        }

        private static string Name(FormKey k, ILinkCache cache)
        {
            if (k.IsNull) return "-";
            if (cache.TryResolve<IStarfieldMajorRecordGetter>(k, out var rec) && !string.IsNullOrEmpty(rec.EditorID))
                return rec.EditorID!;
            return k.ToString();
        }

        // ---------------------------------------------------------------------------------

        private static void EmitText(
            IStarfieldModGetter mod, List<RecipeInfo> recipes, Dictionary<FormKey, ModuleInfo> modules,
            List<FormKey> orphans, List<FormKey> offPlugin, ILinkCache cache)
        {
            Console.WriteLine($"=== {mod.ModKey} — catalogue ===");
            Console.WriteLine($"  recipes (COBJ):            {recipes.Count}");
            Console.WriteLine($"  ship modules (GBFM):       {modules.Count}");
            Console.WriteLine($"  builder entries offered:   {recipes.Sum(r => r.Members.Count)}");
            Console.WriteLine();

            Console.WriteLine($"{"recipe",-30} {"n",2}  {"value",6} {"gate",-8} {"category",-30} product");
            Console.WriteLine(new string('-', 128));
            foreach (var r in recipes.OrderBy(x => x.EditorId, StringComparer.OrdinalIgnoreCase))
            {
                string cat = r.Categories.Count == 0 ? "(none)" : string.Join(",", r.Categories);
                string product = r.Members.Select(m => modules.TryGetValue(m, out var mi) ? mi.FullName : "")
                                          .FirstOrDefault(s => !string.IsNullOrEmpty(s)) ?? "";
                Console.WriteLine($"{r.EditorId,-30} {r.Members.Count,2}  {r.Value,6} {r.LevelGate ?? "-",-8} {cat,-30} {product}");
            }
            Console.WriteLine();

            Console.WriteLine("--- descriptions (player-facing; vanilla authors real prose here) ---");
            foreach (var r in recipes.OrderBy(x => x.EditorId, StringComparer.OrdinalIgnoreCase))
                Console.WriteLine($"  {r.EditorId,-30} {Trim(r.Description, 78)}");
            Console.WriteLine();

            Console.WriteLine("--- the menu axes, counted ---");
            Count("category (COBJ.RecipeFilters)", recipes.SelectMany(r => r.Categories.DefaultIfEmpty("(none)")));
            Count("manufacturer keyword", modules.Values.Select(m => m.Keyword(cache, "ShipModuleManufacturer") ?? "(none)"));
            Count("class keyword", modules.Values.Select(m => m.Keyword(cache, "ShipModuleClass") ?? "(none)"));
            Count("sort key (s_*)", modules.Values.Select(m => m.SortKey(cache) ?? "(none)"));
            Count("MenuSortOrder", recipes.Select(r => r.MenuSortOrder.ToString("0.##")));
            Count("Value", recipes.Select(r => r.Value.ToString()));
            Console.WriteLine($"  level-gated recipes: {recipes.Count(r => r.LevelGate != null)} of {recipes.Count}");
            Console.WriteLine($"  distinct product names: {modules.Values.Select(m => m.FullName).Where(s => !string.IsNullOrEmpty(s)).Distinct().Count()}");
            Console.WriteLine();

            Console.WriteLine($"--- built but NOT constructable: {orphans.Count} ---");
            foreach (var o in orphans.OrderBy(k => Name(k, cache), StringComparer.OrdinalIgnoreCase))
                Console.WriteLine($"  {Name(o, cache)}  [{o}]");
            if (offPlugin.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"--- recipe targets that are not ship modules in this plugin: {offPlugin.Count} ---");
                foreach (var k in offPlugin) Console.WriteLine($"  {Name(k, cache)}  [{k}]");
            }
        }

        private static void Count(string label, IEnumerable<string> values)
        {
            var g = values.GroupBy(v => v).OrderByDescending(x => x.Count()).ToList();
            Console.WriteLine($"  {label}:");
            foreach (var x in g) Console.WriteLine($"      {x.Count(),4}  {x.Key}");
        }

        private static string Trim(string s, int n)
        {
            s = s.Replace("\n", " ").Replace("\r", " ");
            return s.Length <= n ? s : s.Substring(0, n - 1) + "…";
        }

        private static void EmitJson(
            IStarfieldModGetter mod, List<RecipeInfo> recipes, Dictionary<FormKey, ModuleInfo> modules,
            List<FormKey> orphans, List<FormKey> offPlugin, ILinkCache cache)
        {
            var payload = new
            {
                plugin = mod.ModKey.ToString(),
                recipeCount = recipes.Count,
                moduleCount = modules.Count,
                builderEntries = recipes.Sum(r => r.Members.Count),
                recipes = recipes.Select(r => new
                {
                    r.EditorId, r.FormKey, r.Description, r.Value, r.MenuSortOrder,
                    r.Workbench, r.Categories, r.LevelGate, r.CreatedObject, r.CreatedKind,
                    members = r.Members.Select(m => new
                    {
                        editorId = Name(m, cache),
                        fullName = modules.TryGetValue(m, out var mi) ? mi.FullName : null,
                        manufacturer = modules.TryGetValue(m, out var m2) ? m2.Keyword(cache, "ShipModuleManufacturer") : null,
                        position = modules.TryGetValue(m, out var m3) ? m3.Keyword(cache, "ShipModPosition") : null,
                        moduleClass = modules.TryGetValue(m, out var m4) ? m4.Keyword(cache, "ShipModuleClass") : null,
                        sortKey = modules.TryGetValue(m, out var m5) ? m5.SortKey(cache) : null,
                    }).ToList(),
                }).ToList(),
                orphans = orphans.Select(k => Name(k, cache)).ToList(),
                offPluginTargets = offPlugin.Select(k => Name(k, cache)).ToList(),
            };
            Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        }

        private class ModuleInfo
        {
            public string EditorId = "";
            public string FormKey = "";
            public string FullName = "";
            public List<FormKey> KeywordKeys = new();

            public string? Keyword(ILinkCache cache, string prefix)
            {
                foreach (var k in KeywordKeys)
                {
                    var n = gen_catalogue.Name(k, cache);
                    if (n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return n;
                }
                return null;
            }

            public string? SortKey(ILinkCache cache)
            {
                foreach (var k in KeywordKeys)
                {
                    var n = gen_catalogue.Name(k, cache);
                    if (n.StartsWith("s_", StringComparison.Ordinal)) return n;
                }
                return null;
            }
        }

        private class RecipeInfo
        {
            public string EditorId = "";
            public string FormKey = "";
            public string Description = "";
            public uint Value;
            public float MenuSortOrder;   // MenuSortOrder is a float on the record, not an index
            public string Workbench = "";
            public List<string> Categories = new();
            public string? LevelGate;
            public string CreatedObject = "";
            public string CreatedKind = "";
            public List<FormKey> Members = new();
        }
    }
}
