using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
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
    /// IT REUSES THE COMMANDS' OWN CORES (ShipProp.Set, gen_setvalue / gen_setsortorder /
    /// gen_setname / gen_setkeyword / gen_setcondition / gen_setrequiredperk .Apply), never a
    /// second copy of their rules, and reads records with gen_catalogue's readers, so the applier
    /// and the readback cannot disagree about what a field holds.
    ///
    /// A BUILT rung is EDITED: its sheet, price and sort. A rung whose recipe does not exist is
    /// CREATED from the plan's templates: each housing DUPLICATED from its template GBFM (so the
    /// PackIn link, the manufacturer keyword and every unplanned component come with it), named,
    /// re-classed and given the planned sheet; a new FormList of those housings; the recipe
    /// DUPLICATED from the template recipe and repointed at that FormList, then priced, sorted,
    /// gated and perked. ⛔ After the write it reads every GBFM it CREATED back off disk for STRV,
    /// the template-component string Mutagen cannot author for a constructed record (manual 17,
    /// the conform set): a missing one is a part that builds and draws nothing, so it is a loud
    /// failure, never a quiet success.
    ///
    /// ⛔ STILL OUT OF SCOPE, AND REFUSED BY NAME RATHER THAN SKIPPED: on a BUILT rung, a planned
    /// difference in name, class, gate, perk, category or FormList membership (creation sets all
    /// of these; editing them on an existing rung is not wired yet), and a category that differs
    /// from the template recipe's (there is no recipe-filter core yet).
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

            // Names the plan uses, resolved off the load order. A name the game does not define
            // refuses: it is a typo or a plan written against another game.
            var avs = new Dictionary<string, FormKey>(StringComparer.Ordinal);
            foreach (var av in cache.PriorityOrder.WinningOverrides<IActorValueInformationGetter>())
                if (!string.IsNullOrEmpty(av.EditorID)) avs[av.EditorID!] = av.FormKey;
            var keywords = new Dictionary<string, FormKey>(StringComparer.Ordinal);
            foreach (var kw in cache.PriorityOrder.WinningOverrides<IKeywordGetter>())
                if (!string.IsNullOrEmpty(kw.EditorID)) keywords[kw.EditorID!] = kw.FormKey;
            var perks = new Dictionary<string, FormKey>(StringComparer.Ordinal);
            foreach (var pk in cache.PriorityOrder.WinningOverrides<IPerkGetter>())
                if (!string.IsNullOrEmpty(pk.EditorID)) perks[pk.EditorID!] = pk.FormKey;

            // ---- pass 1: validate EVERYTHING, touch nothing ----------------------------------
            var errors = new List<string>();
            var skipped = new List<string>();
            var edits = new List<(IConstructibleObjectGetter co, JsonElement recipe, List<(IGenericBaseFormGetter g, JsonElement m)> members)>();
            var creates = new List<(string name, JsonElement recipe, IConstructibleObjectGetter coTemplate, List<(IGenericBaseFormGetter template, JsonElement m)> members)>();
            var newIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rung in plan.GetProperty("rungs").EnumerateArray())
            {
                string name = rung.GetProperty("rung").GetString()!;
                var recipe = rung.GetProperty("recipe");
                var planMembers = rung.GetProperty("members").EnumerateArray().ToList();
                string coId = recipe.GetProperty("editorId").GetString()!;
                var co = mod.ConstructibleObjects.FirstOrDefault(c => c.EditorID == coId);

                // Names, classes and perks the rung uses must exist whether it is built or not.
                foreach (var m in planMembers)
                {
                    foreach (var p in m.GetProperty("props").EnumerateObject().Where(p => !avs.ContainsKey(p.Name)))
                        errors.Add($"{name}: {m.GetProperty("editorId").GetString()} plans property {p.Name}, which is no ActorValue in the load order");
                    string cls = Str(m, "moduleClass") ?? "";
                    if (!keywords.ContainsKey(cls)) errors.Add($"{name}: class keyword {Show(cls)} is not in the load order");
                }
                foreach (var pr in recipe.GetProperty("requiredPerks").EnumerateArray())
                {
                    var (perk, _, ok) = ParsePerk(pr.GetString());
                    if (!ok || !perks.ContainsKey(perk)) errors.Add($"{name}: required perk {Show(pr.GetString())} is not '<Perk>:<rank>' over a perk in the load order");
                }
                if (Str(recipe, "levelGate") is string gate && !ParseGate(gate, out _, out _))
                    errors.Add($"{name}: levelGate {Show(gate)} is not '>= <level>'");

                if (co == null)
                {
                    if (builtOnly) { skipped.Add(name); continue; }
                    // ---- a rung to CREATE --------------------------------------------------
                    foreach (var id in new[] { coId, Str(recipe, "formList") }
                                 .Concat(planMembers.Select(m => m.GetProperty("editorId").GetString())))
                    {
                        if (string.IsNullOrEmpty(id)) { errors.Add($"{name}: a record in the plan has no EditorID"); continue; }
                        if (mod.EnumerateMajorRecords().Any(r => string.Equals(r.EditorID, id, StringComparison.OrdinalIgnoreCase)))
                            errors.Add($"{name}: {id} already exists, but the rung's recipe does not -- a half-built rung, refusing to guess");
                        if (!newIds.Add(id)) errors.Add($"{name}: {id} is planned twice");
                    }
                    string coT = Str(recipe, "template") ?? "";
                    var coTemplate = mod.ConstructibleObjects.FirstOrDefault(c => c.EditorID == coT);
                    if (coTemplate == null) { errors.Add($"{name}: template recipe {Show(coT)} does not exist"); continue; }
                    string tCat = string.Join(",", (coTemplate.RecipeFilters ?? Enumerable.Empty<IFormLinkGetter<IKeywordGetter>>())
                                                   .Select(f => gen_catalogue.Name(f.FormKey, cache)));
                    if (tCat != Str(recipe, "category"))
                        errors.Add($"{name}: plan category {Show(Str(recipe, "category"))} differs from the template recipe's {Show(tCat)}, and batch has no core for category yet");
                    var mem = new List<(IGenericBaseFormGetter, JsonElement)>();
                    foreach (var m in planMembers)
                    {
                        string gT = Str(m, "template") ?? "";
                        var tg = mod.GenericBaseForms.FirstOrDefault(g => g.EditorID == gT);
                        if (tg == null) { errors.Add($"{name}: template GBFM {Show(gT)} does not exist"); continue; }
                        var tInfo = gen_catalogue.Describe(tg);
                        var planProps = m.GetProperty("props").EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                        foreach (var (av, _) in tInfo.Props)
                            if (!planProps.Contains(gen_catalogue.Name(av, cache)))
                                errors.Add($"{name}: template {gT} carries {gen_catalogue.Name(av, cache)}, which the plan omits, and batch has no core to remove a property");
                        mem.Add((tg, m));
                    }
                    creates.Add((name, recipe, coTemplate, mem));
                    continue;
                }

                // ---- a BUILT rung to edit ---------------------------------------------------
                void Same(string field, string? live, string? want)
                {
                    if (live != want)
                        errors.Add($"{name}: {field} is {Show(live)}, plan says {Show(want)}, and batch cannot edit {field} on a built rung yet");
                }
                Same("levelGate", gen_catalogue.LevelGate(co), Str(recipe, "levelGate"));
                Same("requiredPerks", PerksOf(co, cache),
                     string.Join(",", recipe.GetProperty("requiredPerks").EnumerateArray().Select(e => e.GetString())));
                Same("category",
                     string.Join(",", (co.RecipeFilters ?? Enumerable.Empty<IFormLinkGetter<IKeywordGetter>>())
                                      .Select(f => gen_catalogue.Name(f.FormKey, cache))),
                     Str(recipe, "category"));
                Same("createdObject", gen_catalogue.Name(co.CreatedObject.FormKey, cache), Str(recipe, "formList"));

                var flst = mod.FormLists.FirstOrDefault(f => f.FormKey == co.CreatedObject.FormKey);
                var liveIds = flst == null ? new List<string>()
                    : flst.Items.Select(i => gen_catalogue.Name(i.FormKey, cache)).ToList();
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
                    foreach (var (av, _) in info.Props)
                    {
                        string avName = gen_catalogue.Name(av, cache);
                        if (!planProps.Contains(avName))
                            errors.Add($"{name}: {gId} carries {avName}, which the plan omits, and batch has no core to remove a property");
                    }
                    members.Add((g, m));
                }
                edits.Add((co, recipe, members));
            }

            if (errors.Count > 0)
            {
                Console.WriteLine($"REFUSED -- nothing written. {errors.Count} problem(s):");
                foreach (var e in errors) Console.WriteLine($"  {e}");
                return 1;
            }

            // ---- pass 2: apply, through the commands' own cores -------------------------------
            int changed = 0;
            bool? Track(bool? r) { if (r == true) changed++; return r; }

            foreach (var (co, recipe, members) in edits)
            {
                Console.WriteLine($"[edit {co.EditorID}]");
                foreach (var (g0, m) in members)
                    if (SetSheet(mod, g0.FormKey, m, avs, Track) == null) return Refused();
                Track(gen_setvalue.Apply(mod, Co(mod, co.FormKey), recipe.GetProperty("value").GetUInt32()));
                Track(gen_setsortorder.Apply(mod, Co(mod, co.FormKey), (float)recipe.GetProperty("menuSortOrder").GetDouble()));
            }

            var created = new List<string>();
            foreach (var (name, recipe, coTemplate, members) in creates)
            {
                Console.WriteLine($"[create {name}]");
                var flst = new FormList(mod) { EditorID = Str(recipe, "formList") };
                foreach (var (template, m) in members)
                {
                    string gId = m.GetProperty("editorId").GetString()!;
                    var g = template.Duplicate(mod.GetNextFormKey());
                    g.EditorID = gId;
                    mod.GenericBaseForms.Add(g);
                    Console.WriteLine($"  + {gId}  (duplicated from {template.EditorID})");
                    created.Add(gId);
                    changed++;

                    Track(gen_setname.Apply(mod, G(mod, g.FormKey), Str(m, "fullName")!));
                    string cls = Str(m, "moduleClass")!;
                    var oldCls = gen_catalogue.Describe(G(mod, g.FormKey)).Keyword(cache, "ShipModuleClass");
                    if (oldCls != cls)
                    {
                        if (oldCls != null)
                            Track(gen_setkeyword.ApplyGbfm(mod, G(mod, g.FormKey), new() { (oldCls, keywords[oldCls]) }, remove: true));
                        Track(gen_setkeyword.ApplyGbfm(mod, G(mod, g.FormKey), new() { (cls, keywords[cls]) }, remove: false));
                    }
                    if (SetSheet(mod, g.FormKey, m, avs, Track) == null) return Refused();
                    flst.Items.Add(g.FormKey.ToLink<IStarfieldMajorRecordGetter>());
                }
                mod.FormLists.Add(flst);
                Console.WriteLine($"  + {flst.EditorID}  ({flst.Items.Count} member(s))");
                created.Add(flst.EditorID!);
                changed++;

                var co = coTemplate.Duplicate(mod.GetNextFormKey());
                co.EditorID = Str(recipe, "editorId");
                co.CreatedObject = flst.FormKey.ToNullableLink<IConstructibleObjectTargetGetter>();
                mod.ConstructibleObjects.Add(co);
                Console.WriteLine($"  + {co.EditorID}  (duplicated from {coTemplate.EditorID}, creates {flst.EditorID})");
                created.Add(co.EditorID!);
                changed++;

                Track(gen_setvalue.Apply(mod, Co(mod, co.FormKey), recipe.GetProperty("value").GetUInt32()));
                Track(gen_setsortorder.Apply(mod, Co(mod, co.FormKey), (float)recipe.GetProperty("menuSortOrder").GetDouble()));
                // The template is ungated with no perk; anything it carried is replaced, never stacked.
                if (Str(recipe, "levelGate") is string gate && ParseGate(gate, out var op, out var lvl))
                {
                    if (Track(gen_setcondition.Apply(mod, Co(mod, co.FormKey), "getlevel", op, lvl, FormKey.Null, "", clear: false)) == null)
                        return Refused();
                }
                foreach (var pr in recipe.GetProperty("requiredPerks").EnumerateArray())
                {
                    var (perk, rank, _) = ParsePerk(pr.GetString());
                    if (Track(gen_setrequiredperk.Apply(mod, Co(mod, co.FormKey), perks[perk], perk, rank, clear: false)) == null)
                        return Refused();
                }
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
            Console.WriteLine($"Finished -- {changed} change(s) in ONE write ({created.Count} record(s) created), FormIDs of existing records unchanged.");

            // ⛔ READ OUR OWN OUTPUT BACK: every GBFM created must carry STRV on disk.
            var noStrv = created.Where(id => id.Contains("_gbfm_", StringComparison.OrdinalIgnoreCase))
                                .Where(id => gen_conform.GbfmHasStrv(session.PluginPath, id) != true).ToList();
            if (noStrv.Count > 0)
            {
                Console.WriteLine($"FAIL -- {noStrv.Count} created GBFM(s) have NO STRV on disk, so they will build and draw nothing:");
                foreach (var id in noStrv) Console.WriteLine($"  {id}");
                return 2;
            }
            if (created.Any(id => id.Contains("_gbfm_", StringComparison.OrdinalIgnoreCase)))
                Console.WriteLine("STRV present on every created GBFM, read back off disk.");
            Console.WriteLine("Read it back: python ladder_plan.py --check");
            return 0;
        }

        // Set every planned property through ShipProp.Set. Null = refused (printed).
        private static bool? SetSheet(StarfieldMod mod, FormKey g, JsonElement m, Dictionary<string, FormKey> avs,
                                      Func<bool?, bool?> track)
        {
            foreach (var p in m.GetProperty("props").EnumerateObject())
                if (track(ShipProp.Set(mod, G(mod, g), avs[p.Name], p.Name, (float)p.Value.GetDouble())) == null)
                    return null;
            return true;
        }

        // Every core REPLACES the record it edits, so re-resolve by FormKey before each call.
        private static IGenericBaseFormGetter G(StarfieldMod mod, FormKey k) => mod.GenericBaseForms.First(x => x.FormKey == k);
        private static IConstructibleObjectGetter Co(StarfieldMod mod, FormKey k) => mod.ConstructibleObjects.First(x => x.FormKey == k);

        private static int Refused()
        {
            Console.WriteLine("REFUSED mid-apply -- nothing written.");
            return 1;
        }

        private static string PerksOf(IConstructibleObjectGetter co, Mutagen.Bethesda.Plugins.Cache.ILinkCache cache) =>
            string.Join(",", (co.RequiredPerks ?? Enumerable.Empty<IConstructibleRequiredPerkGetter>())
                             .Select(p => $"{gen_catalogue.Name(p.Perk.FormKey, cache)}:{p.Rank}"));

        // "<Perk EditorID>:<rank>", the catalogue's own spelling.
        private static (string perk, uint rank, bool ok) ParsePerk(string? s)
        {
            var parts = (s ?? "").Split(':');
            if (parts.Length == 2 && uint.TryParse(parts[1], out var r) && r >= 1) return (parts[0], r, true);
            return ("", 0, false);
        }

        // ">= <level>", the catalogue's own spelling of a GetLevel gate.
        private static bool ParseGate(string s, out CompareOperator op, out float level)
        {
            op = CompareOperator.GreaterThanOrEqualTo; level = 0;
            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2 && parts[0] == ">="
                   && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out level) && level >= 1;
        }

        private static string? Str(JsonElement e, string prop)
        {
            if (!e.TryGetProperty(prop, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            return v.GetString();
        }

        private static string Show(string? s) => s == null ? "(none)" : $"'{s}'";
    }
}
