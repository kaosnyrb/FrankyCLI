using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FrankyCLI
{
    /// <summary>
    /// Apply an END-STATE PLAN to a plugin in ONE load and ONE write.
    ///
    ///   batch &lt;modname&gt; &lt;plan.json&gt; [--built-only] [--dry]
    ///
    /// WHY IT EXISTS (2026-10-01, his "A"): the A/B/C ladder is 36 rungs of ~15 edits each, and
    /// each FrankyCLI command pays a full load-order load (~28 s), so by hand the ladder is hours
    /// of loading and a failure between two commands leaves a half-built rung on disk. Here the
    /// only partial state is NOTHING WRITTEN: every rung is validated before any record is touched,
    /// and the plugin is written once at the end. So the per-change ORDERING rules this lane used
    /// to need (setvariant before setflipset, whichever leaves a safe partial state) stop being
    /// rules anybody has to remember.
    ///
    /// THE PLAN is ladder_plan.py's ladder_plan.json (Stardust repo): per rung, the recipe, the
    /// FormList and every member module as they should EXIST afterwards. It says what should BE,
    /// never what to DO, which is why the same file is the readback's acceptance test.
    ///
    /// IT REUSES THE COMMANDS' OWN CORES (gen_setvalue.Apply, gen_setsortorder.Apply,
    /// ShipProp.Set), never a second copy of their rules, and reads records with gen_catalogue's
    /// readers, so the applier and the readback cannot disagree about what a field holds.
    ///
    /// ⛔ V1 SCOPE, STATED RATHER THAN DISCOVERED: it EDITS rungs that exist (property sheet,
    /// price, menu sort). It does not yet CREATE a rung, and it has no core yet for name, class
    /// keyword, gate, required perk, category or FormList membership. Any of those that differ
    /// from the plan REFUSE the whole run by name. Never skipped, never reported as done.
    /// A rung that does not exist refuses too, unless --built-only says to apply the built ones.
    /// </summary>
    class gen_batch
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "batch", plan.json, flags...]
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: batch <modname> <plan.json> [--built-only] [--dry]");
                return 1;
            }
            string modname = args[0];
            bool dry = args.Skip(3).Contains("--dry");
            bool builtOnly = args.Skip(3).Contains("--built-only");

            JsonElement plan;
            try { plan = JsonDocument.Parse(File.ReadAllText(args[2])).RootElement; }
            catch (Exception e) { Console.WriteLine($"Error: cannot read plan {args[2]}: {e.Message}"); return 1; }
            string planPlugin = plan.GetProperty("plugin").GetString() ?? "";
            if (!string.Equals(planPlugin, modname + ".esm", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Error: the plan is for {planPlugin}, not {modname}.esm -- nothing written");
                return 1;
            }

            using var session = PluginSession.Open(modname);
            if (session == null) return 1;
            var mod = session.Mod;
            var cache = session.Cache;

            // ActorValue EditorID -> FormKey, off the load order. A name the plan uses that the
            // game does not define refuses: it is a typo or a plan written against another game.
            var avs = new Dictionary<string, FormKey>(StringComparer.Ordinal);
            foreach (var av in cache.PriorityOrder.WinningOverrides<IActorValueInformationGetter>())
                if (!string.IsNullOrEmpty(av.EditorID)) avs[av.EditorID!] = av.FormKey;

            // ---- pass 1: validate EVERYTHING, touch nothing ----------------------------------
            var errors = new List<string>();
            var skipped = new List<string>();
            var work = new List<(IConstructibleObjectGetter co, JsonElement recipe, List<(IGenericBaseFormGetter g, JsonElement m)> members)>();
            foreach (var rung in plan.GetProperty("rungs").EnumerateArray())
            {
                string name = rung.GetProperty("rung").GetString()!;
                var recipe = rung.GetProperty("recipe");
                string coId = recipe.GetProperty("editorId").GetString()!;
                var co = mod.ConstructibleObjects.FirstOrDefault(c => c.EditorID == coId);
                if (co == null)
                {
                    if (builtOnly) { skipped.Add(name); continue; }
                    errors.Add($"{name}: recipe {coId} does not exist, and batch cannot create a rung yet (pass --built-only to apply the built ones)");
                    continue;
                }

                void Same(string field, string? live, string? want)
                {
                    if (live != want)
                        errors.Add($"{name}: {field} is {Show(live)}, plan says {Show(want)}, and batch has no core for {field} yet");
                }
                Same("levelGate", gen_catalogue.LevelGate(co), Str(recipe, "levelGate"));
                Same("requiredPerks",
                     string.Join(",", (co.RequiredPerks ?? Enumerable.Empty<IConstructibleRequiredPerkGetter>())
                                      .Select(p => $"{gen_catalogue.Name(p.Perk.FormKey, cache)}:{p.Rank}")),
                     string.Join(",", recipe.GetProperty("requiredPerks").EnumerateArray().Select(e => e.GetString())));
                Same("category",
                     string.Join(",", (co.RecipeFilters ?? Enumerable.Empty<IFormLinkGetter<IKeywordGetter>>())
                                      .Select(f => gen_catalogue.Name(f.FormKey, cache))),
                     Str(recipe, "category"));
                Same("createdObject", gen_catalogue.Name(co.CreatedObject.FormKey, cache), Str(recipe, "formList"));

                // Members: the FormList the recipe creates must hold exactly the plan's members.
                var flst = mod.FormLists.FirstOrDefault(f => f.FormKey == co.CreatedObject.FormKey);
                var liveIds = flst == null ? new List<string>()
                    : flst.Items.Select(i => gen_catalogue.Name(i.FormKey, cache)).ToList();
                var planMembers = rung.GetProperty("members").EnumerateArray().ToList();
                Same("members", string.Join(",", liveIds.OrderBy(s => s, StringComparer.Ordinal)),
                     string.Join(",", planMembers.Select(m => m.GetProperty("editorId").GetString())
                                                 .OrderBy(s => s, StringComparer.Ordinal)));

                var members = new List<(IGenericBaseFormGetter, JsonElement)>();
                foreach (var m in planMembers)
                {
                    string gId = m.GetProperty("editorId").GetString()!;
                    var g = mod.GenericBaseForms.FirstOrDefault(x => x.EditorID == gId);
                    if (g == null) { errors.Add($"{name}: member {gId} does not exist"); continue; }
                    var info = gen_catalogue.Describe(g);
                    Same($"{gId}.fullName", info.FullName, Str(m, "fullName"));
                    Same($"{gId}.moduleClass", info.Keyword(cache, "ShipModuleClass"), Str(m, "moduleClass"));
                    var planProps = m.GetProperty("props").EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                    foreach (var p in planProps.Where(p => !avs.ContainsKey(p)))
                        errors.Add($"{name}: {gId} plans property {p}, which is no ActorValue in the load order");
                    // The plan is the WHOLE sheet. A property the record carries and the plan
                    // does not would survive the run, and no core removes one yet.
                    foreach (var (av, _) in info.Props)
                    {
                        string avName = gen_catalogue.Name(av, cache);
                        if (!planProps.Contains(avName))
                            errors.Add($"{name}: {gId} carries {avName}, which the plan omits, and batch has no core to remove a property");
                    }
                    members.Add((g, m));
                }
                work.Add((co, recipe, members));
            }

            if (errors.Count > 0)
            {
                Console.WriteLine($"REFUSED -- nothing written. {errors.Count} problem(s):");
                foreach (var e in errors) Console.WriteLine($"  {e}");
                return 1;
            }

            // ---- pass 2: apply, through the commands' own cores -------------------------------
            int changed = 0;
            foreach (var (co, recipe, members) in work)
            {
                Console.WriteLine($"[{co.EditorID}]");
                foreach (var (g0, m) in members)
                {
                    foreach (var p in m.GetProperty("props").EnumerateObject())
                    {
                        // Re-resolve every time: each core replaces the record it edits.
                        var g = mod.GenericBaseForms.First(x => x.FormKey == g0.FormKey);
                        var r = ShipProp.Set(mod, g, avs[p.Name], p.Name, (float)p.Value.GetDouble());
                        if (r == null) { Console.WriteLine("REFUSED mid-apply -- nothing written."); return 1; }
                        if (r == true) changed++;
                    }
                }
                var c1 = mod.ConstructibleObjects.First(x => x.FormKey == co.FormKey);
                if (gen_setvalue.Apply(mod, c1, recipe.GetProperty("value").GetUInt32())) changed++;
                var c2 = mod.ConstructibleObjects.First(x => x.FormKey == co.FormKey);
                if (gen_setsortorder.Apply(mod, c2, (float)recipe.GetProperty("menuSortOrder").GetDouble())) changed++;
            }
            session.Close();

            if (skipped.Count > 0)
                Console.WriteLine($"\n--built-only: {skipped.Count} rung(s) NOT BUILT and not touched: {string.Join(", ", skipped)}");
            if (dry)
            {
                Console.WriteLine($"--dry: {changed} change(s) shown, nothing written.");
                return 0;
            }
            if (changed == 0)
            {
                Console.WriteLine("Nothing to write.");
                return 0;
            }
            session.Write();
            Console.WriteLine($"Finished -- {changed} change(s) in ONE write, FormIDs unchanged. "
                              + "Read it back: python ladder_plan.py --check");
            return 0;
        }

        private static string? Str(JsonElement e, string prop)
        {
            var v = e.GetProperty(prop);
            return v.ValueKind == JsonValueKind.Null ? null : v.GetString();
        }

        private static string Show(string? s) => s == null ? "(none)" : $"'{s}'";
    }
}
