using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FrankyCLI
{
    /// <summary>
    /// DELVES FROM DATA. A recipe is a JSON file an author writes; a template is a row saying which
    /// shipped quest shape can carry it; this command lints one against the other and builds it.
    ///
    ///   gen_delve templates                     list the registry
    ///   gen_delve lint  <recipe.json>           grade a recipe without writing anything
    ///   gen_delve build <recipe.json> [--dry]   lint, then clone + rewire + verify OFF DISK
    ///
    /// ⭐ THE DIVISION IS HIS AND IT IS THE POINT: the recipe is content and belongs to whoever
    /// writes the mission; the template is mechanism and belongs to whoever reads records. Adding a
    /// hundred Delves adds a hundred recipes and no code. Adding a new KIND of beat adds a template
    /// row and, usually, nothing else either.
    ///
    /// ⛔ WHY A TEMPLATE IS NEEDED AT ALL, because it looks like ceremony until you hit it: the
    /// objective layer is PAPYRUS-ONLY. Of 245 vanilla Default* scripts exactly one calls
    /// SetObjectiveDisplayed. The stage graph is fully authorable from records through stock hooks,
    /// and the thing the player actually reads is not. So a multi-beat mission has to be driven by a
    /// script that already displays its objectives, and `driver` on the template row names it.
    ///
    /// ⭐ THE SELECTION CONDITIONS ARE DERIVED, NEVER AUTHORED. A recipe says which marker each beat
    /// sits on. The place's LocationHasRefType conditions are then the UNION of those markers plus
    /// MapMarkerRefType, computed here. An author cannot put a beat on a marker the place was not
    /// selected for, because there is no field in which to disagree -- the class of bug where a
    /// quest picks a POI on one marker and then hunts for another is unconstructible rather than
    /// checked.
    ///
    /// ⚠ ONE STATED LIMIT, v1: `place.leash` is ADVISORY. The base's own GetDistance condition is
    /// preserved verbatim and a recipe naming a different leash gets a WARN rather than a rewrite,
    /// because constructing a GetDistance condition has not been proven the way LocationHasRefType
    /// has. Named here rather than discovered by someone whose leash quietly did nothing.
    /// </summary>
    public static class gen_delve
    {
        // ------------------------------------------------------------------ models

        private sealed class Template
        {
            public string id { get; set; } = "";
            public string @base { get; set; } = "";
            public string mod { get; set; } = "";
            public string driver { get; set; } = "";
            public int beats { get; set; }
            public bool carriesItem { get; set; }
            public int placeAlias { get; set; }
            public int mapMarkerAlias { get; set; }
            public int briefingStage { get; set; }
            public int completeStage { get; set; }
            public List<BeatSlot> beatSlots { get; set; } = new();
            public Dictionary<string, string> tokens { get; set; } = new();
            public List<int> collapsedPlaceAliases { get; set; } = new();
            public List<string> cannot { get; set; } = new();
            public string verifiedFrom { get; set; } = "";
        }
        private sealed class BeatSlot
        {
            public int markerAlias { get; set; }
            public int activatorAlias { get; set; }
            public int objective { get; set; }
            public int journalStage { get; set; }
        }
        private sealed class TemplateFile { public int schema { get; set; } public List<Template> templates { get; set; } = new(); }

        private sealed class Recipe
        {
            public int schema { get; set; }
            public string id { get; set; } = "";
            public string template { get; set; } = "";
            public string author { get; set; } = "";
            public string source { get; set; } = "";
            public Place place { get; set; } = new();
            public Prose prose { get; set; } = new();
            public List<Beat> beats { get; set; } = new();
        }
        private sealed class Place { public Theme theme { get; set; } = new(); public string? leash { get; set; } }
        private sealed class Theme { public List<string> require { get; set; } = new(); public List<string> exclude { get; set; } = new(); }
        private sealed class Prose { public string? name { get; set; } public string? briefing { get; set; } }
        private sealed class Beat { public string at { get; set; } = ""; public string? objective { get; set; } public string? journal { get; set; } }

        private static readonly JsonSerializerOptions JsonOpts = new()
        { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        private sealed record Issue(bool Fatal, string Text);

        // ------------------------------------------------------------------ entry

        public static int Run(string[] args)
        {
            string verb = args.Length > 1 ? args[1].ToLowerInvariant() : "";
            bool dry = args.Any(a => a.Equals("--dry", StringComparison.OrdinalIgnoreCase));
            string? path = args.Skip(2).FirstOrDefault(a => !a.StartsWith("--"));

            string? dataDir = ResolveDataDir();
            if (dataDir == null)
            {
                Console.WriteLine("REFUSED: could not find data/delves. Tried, from the assembly and the working directory:");
                foreach (var c in CandidateRoots()) Console.WriteLine("  " + Path.Combine(c, "data", "delves"));
                return 1;
            }
            Console.WriteLine("  data: " + dataDir);

            var templates = LoadTemplates(dataDir, out string? terr);
            if (templates == null) { Console.WriteLine("REFUSED: " + terr); return 1; }

            switch (verb)
            {
                case "templates": return ListTemplates(templates);
                case "census": return Census(args.Any(a => a.Equals("--vanilla", StringComparison.OrdinalIgnoreCase)));
                case "spread": return Spread(args.Skip(2).Where(a => !a.StartsWith("--")).ToList(),
                                             args.Any(a => a.Equals("--dungeons", StringComparison.OrdinalIgnoreCase)));
                case "markers": return Markers(args.Any(a => a.Equals("--dungeons", StringComparison.OrdinalIgnoreCase)));
                case "lint": return WithRecipe(path, dataDir, templates, (r, t, env) => Grade(r, t, env, null) ? 0 : 1);
                case "build": return WithRecipe(path, dataDir, templates, (r, t, env) => Build(r, t, env, dry));
                default:
                    Console.WriteLine("Usage: gen_delve templates");
                    Console.WriteLine("       gen_delve lint  <recipe.json | recipeId>");
                    Console.WriteLine("       gen_delve build <recipe.json | recipeId> [--dry]");
                    return 1;
            }
        }

        private static int WithRecipe(string? path, string dataDir, List<Template> templates,
                                      Func<Recipe, Template, IGameEnvironment<IStarfieldMod, IStarfieldModGetter>, int> body)
        {
            if (path == null) { Console.WriteLine("REFUSED: name a recipe."); return 1; }
            // A bare id is resolved against the recipes folder, so the common case is short and the
            // explicit path still works for a recipe living anywhere else.
            string file = File.Exists(path) ? path : Path.Combine(dataDir, "recipes", path + ".json");
            if (!File.Exists(file)) { Console.WriteLine("REFUSED: no recipe at " + file); return 1; }

            Recipe? recipe;
            try { recipe = JsonSerializer.Deserialize<Recipe>(File.ReadAllText(file), JsonOpts); }
            catch (JsonException ex) { Console.WriteLine("REFUSED: " + Path.GetFileName(file) + " is not valid JSON -- " + ex.Message); return 1; }
            if (recipe == null) { Console.WriteLine("REFUSED: " + file + " parsed to nothing."); return 1; }
            Console.WriteLine("  recipe: " + file);

            var t = templates.FirstOrDefault(x => string.Equals(x.id, recipe.template, StringComparison.OrdinalIgnoreCase));
            if (t == null)
            {
                Console.WriteLine("REFUSED: no template '" + recipe.template + "'. Known: "
                                  + string.Join(", ", templates.Select(x => x.id)));
                return 1;
            }
            Console.WriteLine("  template: " + t.id + "  (base " + t.@base + ", driver " + t.driver + ")");
            Console.WriteLine();

            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            return body(recipe, t, env);
        }

        // ------------------------------------------------------------------ the lint

        /// <summary>
        /// Grade a recipe. Returns true if nothing fatal. Every check prints, pass or fail, because
        /// a lint that only speaks when it is unhappy cannot be told from one that did not run.
        /// </summary>
        private static bool Grade(Recipe r, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                  Dictionary<string, FormKey>? markersOut)
        {
            var issues = new List<Issue>();
            void Fatal(string s) => issues.Add(new Issue(true, s));
            void Warn(string s) => issues.Add(new Issue(false, s));

            Console.WriteLine("=== LINT ===");

            // --- shape ---------------------------------------------------------------
            if (r.schema != 1) Fatal($"schema is {r.schema}, this tool speaks 1");
            if (string.IsNullOrWhiteSpace(r.id)) Fatal("no id");
            if (r.beats.Count != t.beats)
                Fatal($"recipe has {r.beats.Count} beat(s); template '{t.id}' carries exactly {t.beats}. "
                      + "What it cannot do: " + string.Join("; ", t.cannot));
            for (int i = 0; i < r.beats.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(r.beats[i].at)) Fatal($"beat {i + 1} names no marker");
                if (string.IsNullOrWhiteSpace(r.beats[i].objective)) Fatal($"beat {i + 1} has no objective text");
            }
            if (string.IsNullOrWhiteSpace(r.prose.name)) Fatal("prose.name is empty");
            if (string.IsNullOrWhiteSpace(r.prose.briefing)) Fatal("prose.briefing is empty");

            // --- prose tokens ---------------------------------------------------------
            // An unknown <Token> is not an error at write time and not an error at load time: it
            // renders to the player as literal angle brackets. Only a lint can catch it.
            foreach (var (where, text) in ProseStrings(r))
            {
                foreach (var tok in Tokens(text))
                    if (!t.tokens.ContainsKey(tok))
                        Fatal($"{where}: <{tok}> is not a token this template supplies. "
                              + "It would print to the player literally. Known: "
                              + string.Join(", ", t.tokens.Keys.Select(k => "<" + k + ">")));
            }

            // --- markers --------------------------------------------------------------
            var lcrts = env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>()
                .Where(x => x.EditorID != null)
                .GroupBy(x => x.EditorID!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().FormKey, StringComparer.OrdinalIgnoreCase);

            var wanted = new List<string>();
            foreach (var b in r.beats) if (!wanted.Contains(b.at, StringComparer.OrdinalIgnoreCase)) wanted.Add(b.at);
            const string MapMarker = "MapMarkerRefType";
            if (!wanted.Contains(MapMarker, StringComparer.OrdinalIgnoreCase)) wanted.Add(MapMarker);

            var keys = new Dictionary<string, FormKey>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in wanted)
            {
                if (!lcrts.TryGetValue(m, out var k)) { Fatal($"marker '{m}' is not a LocationReferenceType in this load order"); continue; }
                keys[m] = k;
            }
            markersOut?.Clear();
            foreach (var kv in keys) markersOut?.Add(kv.Key, kv.Value);

            // --- the pool, which is the number the design sits on -----------------------
            if (!issues.Any(i => i.Fatal))
            {
                var pool = PoolCensus(env);
                Console.WriteLine($"  POI corpus: {pool.Count} location(s) in the working pool");

                int all = 0, mapOnly = 0;
                var perMarker = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var m in keys.Keys) perMarker[m] = 0;

                var reqK = ResolveKeywords(env, r.place.theme.require, Fatal);
                var excK = ResolveKeywords(env, r.place.theme.exclude, Fatal);

                foreach (var poi in pool)
                {
                    foreach (var m in keys) if (poi.RefTypes.Contains(m.Value)) perMarker[m.Key]++;
                    bool hasAll = keys.Values.All(k => poi.RefTypes.Contains(k));
                    if (!hasAll) continue;
                    mapOnly++;
                    if (reqK.Any(k => !poi.Keywords.Contains(k))) continue;
                    if (excK.Any(k => poi.Keywords.Contains(k))) continue;
                    all++;
                }

                foreach (var kv in perMarker)
                    Console.WriteLine($"    {kv.Key,-34} {kv.Value,4} of {pool.Count}  ({100.0 * kv.Value / Math.Max(1, pool.Count):F1}%)");
                Console.WriteLine($"    all markers together               {mapOnly,4}");
                Console.WriteLine($"    after the theme predicate          {all,4}   <- the draw pool");

                if (all == 0) Fatal("the draw pool is ZERO. Nothing would ever select this mission.");
                else if (all < 40) Warn($"the draw pool is {all}. Narrow -- the narrowest anything in Overtime leans on is 44.");
            }

            // --- how far apart the beats actually land -------------------------------------
            // ⛔ THIS EXISTS BECAUSE COVERAGE IS SILENT ABOUT GEOMETRY. The first playable Delve put
            // its two beats close enough together that the mission read as nothing, and both markers
            // were 99.6% of the pool. Nothing in a coverage table could have predicted it.
            //
            // ⭐ HIS FACT: THE TRAVEL MARKERS ARE THE EDGES OF THE POI. So the two reference rows
            // below are the site's RADIUS and its DIAMETER, and the diameter is the hard ceiling on
            // how far apart any two beats in ONE POI can ever be. No printed verdict and no
            // threshold: three numbers and the author decides, because "far enough" is a question
            // about the fiction and not about the records.
            if (!issues.Any(i => i.Fatal) && r.beats.Count >= 2)
            {
                Console.WriteLine();
                Console.WriteLine("  separation, GAME UNITS (units-to-metres is NOT established; compare rows)");
                var want = new List<(string, string, string)>
                {
                    ("THIS RECIPE", r.beats[0].at, r.beats[1].at),
                    ("site radius", "RECenterLocRef", "RETravelA1LocRef"),
                    ("site diameter (the ceiling)", "RETravelA1LocRef", "RETravelB1LocRef"),
                };
                foreach (var (label, a, b) in want)
                {
                    var v = Separations(env, a, b);
                    if (v.Count == 0) { Console.WriteLine($"    {label,-30} {a} <-> {b}: no POI carries both"); continue; }
                    v.Sort();
                    Console.WriteLine($"    {label,-30} n={v.Count,4}  min {v[0],6:F0}  p25 {Pct(v, .25),6:F0}"
                                      + $"  med {Pct(v, .5),6:F0}  p75 {Pct(v, .75),6:F0}  max {v[^1],6:F0}   ({a} <-> {b})");
                }
                Console.WriteLine("    ⚠ Two markers from the SAME travel ring (A1/A2/A3, or B1/B2/B3) sit on top of each");
                Console.WriteLine("      other: median 7 to 14 units. An A marker paired with a B marker is the widest");
                Console.WriteLine("      a single POI offers, and it is about double what the centre gives.");
            }

            // --- leash, the stated v1 limit ---------------------------------------------
            if (!string.IsNullOrWhiteSpace(r.place.leash))
            {
                var g = env.LoadOrder.PriorityOrder.WinningOverrides<IGlobalGetter>()
                    .FirstOrDefault(x => string.Equals(x.EditorID, r.place.leash, StringComparison.OrdinalIgnoreCase));
                if (g == null) Fatal($"leash '{r.place.leash}' is not a Global in this load order");
                else Warn($"leash '{r.place.leash}' is ADVISORY in v1: the base's own distance condition is preserved "
                          + "verbatim and this value is not written. Constructing a GetDistance condition is unproven.");
            }

            // --- report ------------------------------------------------------------------
            Console.WriteLine();
            foreach (var i in issues) Console.WriteLine((i.Fatal ? "  [FATAL] " : "  [warn ] ") + i.Text);
            int fatal = issues.Count(i => i.Fatal);
            Console.WriteLine();
            Console.WriteLine(fatal == 0
                ? $"  LINT PASSES ({issues.Count - fatal} warning(s))."
                : $"  LINT FAILS: {fatal} fatal, {issues.Count - fatal} warning(s).");
            return fatal == 0;
        }

        private static IEnumerable<(string, string)> ProseStrings(Recipe r)
        {
            yield return ("prose.name", r.prose.name ?? "");
            yield return ("prose.briefing", r.prose.briefing ?? "");
            for (int i = 0; i < r.beats.Count; i++)
            {
                yield return ($"beat {i + 1} objective", r.beats[i].objective ?? "");
                if (r.beats[i].journal != null) yield return ($"beat {i + 1} journal", r.beats[i].journal!);
            }
        }

        private static IEnumerable<string> Tokens(string s)
        {
            int i = 0;
            while ((i = s.IndexOf('<', i)) >= 0)
            {
                int j = s.IndexOf('>', i);
                if (j < 0) yield break;
                string inner = s[(i + 1)..j];
                // <Alias=X> is the engine's own spelling and is deliberately NOT graded here: a
                // recipe should not be writing one, and if it does it is naming a real alias.
                if (!inner.Contains('=')) yield return inner;
                i = j + 1;
            }
        }

        private static List<FormKey> ResolveKeywords(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                                    List<string> names, Action<string> fatal)
        {
            var outp = new List<FormKey>();
            foreach (var n in names)
            {
                var k = env.LoadOrder.PriorityOrder.WinningOverrides<IKeywordGetter>()
                    .FirstOrDefault(x => string.Equals(x.EditorID, n, StringComparison.OrdinalIgnoreCase));
                if (k == null) fatal($"theme keyword '{n}' does not resolve");
                else outp.Add(k.FormKey);
            }
            return outp;
        }

        // ------------------------------------------------------------------ the census

        private sealed class Poi
        {
            public HashSet<FormKey> RefTypes = new();
            public HashSet<FormKey> Keywords = new();
            public bool Vanilla;
            public bool ByKeyword;
            public bool ByCentre;
            public string Edid = "";
        }

        /// <summary>
        /// THE CENSUS'S OWN CONTROL. It prints the two halves of the pool union and the same figures
        /// restricted to Starfield.esm, because the manual's published numbers (part 32) were taken
        /// against vanilla alone and this walks the whole load order.
        ///
        /// ⛔ A POOL FIGURE THAT DOES NOT RECONCILE WITH THE PUBLISHED ONE IS AN INSTRUMENT PROBLEM
        /// UNTIL PROVEN OTHERWISE. Two readings of the same thing that disagree are two subjects or
        /// one broken reader, and the cheap way to tell is to make the corpora identical first.
        /// Expected on vanilla, measured 2026-09-23: 260 by keyword, 282 by centre marker, 259
        /// intersection, 283 union.
        /// </summary>
        /// <summary>
        /// HOW FAR APART TWO BEATS ACTUALLY LAND, measured across the pool.
        ///
        /// ⛔ THE GAP THIS EXISTS FOR, found at the glass on the first playable Delve: the two beats
        /// were about twenty metres apart and the mission read as nothing. Coverage said both markers
        /// were 99.6% of the pool and coverage is silent about geometry. There is no condition
        /// function that can filter a POI draw on the distance between two of its own markers, so the
        /// ONLY lever is which pair of markers a recipe names -- which makes this distribution the
        /// thing that decides whether a Delve is a walk or a shrug.
        ///
        /// Units are game units. Starfield's are roughly 1.4 cm, so ~70 units to the metre; the
        /// report prints both and says which is derived.
        /// </summary>
        private static int Spread(List<string> markerNames, bool dungeons)
        {
            if (markerNames.Count < 2)
            {
                Console.WriteLine("Usage: gen_delve spread [--dungeons] <markerA> <markerB> [markerC ...]");
                Console.WriteLine("       Every pair among the named markers is measured across the chosen population.");
                Console.WriteLine("       --dungeons measures the 71 LocDungeonBossLocRef locations instead of the POI pool.");
                return 1;
            }
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();

            var lcrt = env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>()
                .Where(x => x.EditorID != null)
                .GroupBy(x => x.EditorID!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().FormKey, StringComparer.OrdinalIgnoreCase);
            var keys = new Dictionary<string, FormKey>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in markerNames)
            {
                if (!lcrt.TryGetValue(n, out var k)) { Console.WriteLine("REFUSED: '" + n + "' is not a LocationReferenceType."); return 1; }
                keys[n] = k;
            }

            var oeK = env.LoadOrder.PriorityOrder.WinningOverrides<IKeywordGetter>()
                .FirstOrDefault(x => string.Equals(x.EditorID, "LocTypeOE_Keyword", StringComparison.OrdinalIgnoreCase))?.FormKey;
            var ctr = lcrt.TryGetValue("RECenterLocRef", out var ck) ? ck : (FormKey?)null;

            // per marker name -> list of positions, per POI
            var samples = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in markerNames) foreach (var b in markerNames)
                if (string.Compare(a, b, StringComparison.OrdinalIgnoreCase) < 0) samples[a + " <-> " + b] = new List<double>();

            var boss = lcrt.TryGetValue("LocDungeonBossLocRef", out var bk) ? bk : (FormKey?)null;

            // ⭐ HIS FACT, 2026-09-23: on a dungeon the boss markers are INSIDE and the travel
            // markers are OUTSIDE. That is corroborable without taking anyone's word for it, because
            // a special reference carries the CELL its marker sits in: an inside marker and an
            // outside marker cannot share one. Counted below as sameCell / crossCell.
            var cellOf = new Dictionary<FormKey, FormKey>();
            int sameCell = 0, crossCell = 0;

            int pois = 0, resolvedPois = 0, unresolvable = 0;
            foreach (var loc in env.LoadOrder.PriorityOrder.WinningOverrides<ILocationGetter>())
            {
                var rt = new Dictionary<FormKey, List<FormKey>>();   // reftype -> marker refs
                var kw = new HashSet<FormKey>();
                cellOf.Clear();
                if (loc.Keywords != null) foreach (var k in loc.Keywords) kw.Add(k.FormKey);
                foreach (var g in new[] { loc.MasterSpecialReferences, loc.AddedSpecialReferences })
                {
                    if (g == null) continue;
                    foreach (var e in g)
                    {
                        if (e.LocationRefType.IsNull || e.Marker.IsNull) continue;
                        if (!rt.TryGetValue(e.LocationRefType.FormKey, out var l)) rt[e.LocationRefType.FormKey] = l = new List<FormKey>();
                        l.Add(e.Marker.FormKey);
                        if (!cellOf.ContainsKey(e.Marker.FormKey) && !e.Location.IsNull) cellOf[e.Marker.FormKey] = e.Location.FormKey;
                    }
                }
                bool inPop = dungeons
                    ? (boss != null && rt.ContainsKey(boss.Value))
                    : ((oeK != null && kw.Contains(oeK.Value)) || (ctr != null && rt.ContainsKey(ctr.Value)));
                if (!inPop) continue;
                pois++;

                // Resolve one position per named marker type. ⚠ A POI can carry SEVERAL refs of one
                // type; the FIRST is taken and that is a stated simplification, not a measurement of
                // the nearest or the best. Recorded here rather than left for a reader to assume.
                var pos = new Dictionary<string, P3>();
                var cell = new Dictionary<string, FormKey>();
                foreach (var kv in keys)
                {
                    if (!rt.TryGetValue(kv.Value, out var refs) || refs.Count == 0) continue;
                    if (cellOf.TryGetValue(refs[0], out var c)) cell[kv.Key] = c;
                    if (TryPos(env, refs[0], out var p)) pos[kv.Key] = p;
                    else unresolvable++;
                }
                if (pos.Count < 2) continue;
                resolvedPois++;
                foreach (var pair in samples.Keys.ToList())
                {
                    var half = pair.Split(" <-> ");
                    if (!pos.TryGetValue(half[0], out var p1) || !pos.TryGetValue(half[1], out var p2)) continue;
                    // ⛔ A DISTANCE ACROSS TWO CELLS IS NOT A DISTANCE. Marker positions are local to
                    // the cell the reference sits in, so subtracting a coordinate in an interior from
                    // one on the surface produces a confident number that means nothing at all. Those
                    // pairs are COUNTED and EXCLUDED rather than quietly averaged in.
                    bool same = cell.TryGetValue(half[0], out var c1) && cell.TryGetValue(half[1], out var c2) && c1 == c2;
                    if (same) sameCell++; else { crossCell++; continue; }
                    samples[pair].Add(Dist(p1, p2));
                }
            }

            Console.WriteLine();
            Console.WriteLine($"  population: {(dungeons ? "DUNGEONS (LocDungeonBossLocRef)" : "the POI pool")}");
            Console.WriteLine($"  locations: {pois}, with at least two of these markers resolvable: {resolvedPois}");
            if (unresolvable > 0) Console.WriteLine($"  marker refs that would not resolve to a position: {unresolvable}");
            Console.WriteLine($"  marker pairs sharing a cell: {sameCell}   in DIFFERENT cells: {crossCell}");
            if (crossCell > 0)
                Console.WriteLine("    ⚠ cross-cell pairs are EXCLUDED from the rows below, not averaged in: positions are "
                                  + "cell-local, so subtracting across two cells gives a confident meaningless number.");
            Console.WriteLine();
            // ⛔ NO METRE COLUMN. The first version printed one at ~70 units/m, which is the
            // Skyrim/Fallout constant carried over on no evidence, and it produced a median of
            // "1 metre" for a whole POI. The unit-to-metre conversion is NOT established for this
            // engine and a fabricated precision beside a real measurement is worse than no column:
            // the numbers below are comparable to EACH OTHER, which is all a marker choice needs.
            //
            // ⭐ HIS FACT, 2026-09-23, and it is what makes this table readable: THE TRAVEL MARKERS
            // ARE THE EDGES OF THE POI. So an edge-to-edge pair is the site's DIAMETER and an
            // edge-to-centre pair is its radius. The largest number here is the most separation a
            // single-POI Delve can ever have, by construction.
            Console.WriteLine("  distance between beats, GAME UNITS (units-to-metres is NOT established; compare rows)");
            Console.WriteLine($"  {"pair",-52} {"n",5} {"min",9} {"p25",9} {"med",9} {"p75",9} {"max",9}");
            foreach (var kv in samples.OrderByDescending(s => Median(s.Value)))
            {
                var v = kv.Value.OrderBy(x => x).ToList();
                if (v.Count == 0) { Console.WriteLine($"  {kv.Key,-52} {0,5}   (no POI carries both)"); continue; }
                Console.WriteLine($"  {kv.Key,-52} {v.Count,5} {v[0],9:F0} {Pct(v, .25),9:F0} {Pct(v, .5),9:F0} {Pct(v, .75),9:F0} {v[^1],9:F0}");
            }
            Console.WriteLine();
            Console.WriteLine("  ⚠ A MEDIAN IS NOT A GUARANTEE. The draw is random, so a recipe picking the widest pair");
            Console.WriteLine("    still lands on its own p25 a quarter of the time. Read the SPREAD, not the middle.");
            return 0;
        }

        /// <summary>
        /// Distance between one marker of each named type, per POI. Shared by `spread` and the lint
        /// so the two can never report different numbers for the same question.
        /// ⚠ A POI can carry several refs of one type and the FIRST is taken. Stated rather than
        /// left to be assumed: this is not the nearest pair, nor the farthest, nor a mean.
        /// </summary>
        private static List<double> Separations(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, string a, string b)
        {
            var outp = new List<double>();
            var lcrt = env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>()
                .Where(x => x.EditorID != null)
                .GroupBy(x => x.EditorID!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().FormKey, StringComparer.OrdinalIgnoreCase);
            if (!lcrt.TryGetValue(a, out var ka) || !lcrt.TryGetValue(b, out var kb)) return outp;

            var oeK = env.LoadOrder.PriorityOrder.WinningOverrides<IKeywordGetter>()
                .FirstOrDefault(x => string.Equals(x.EditorID, "LocTypeOE_Keyword", StringComparison.OrdinalIgnoreCase))?.FormKey;
            var ctr = lcrt.TryGetValue("RECenterLocRef", out var ck) ? ck : (FormKey?)null;

            foreach (var loc in env.LoadOrder.PriorityOrder.WinningOverrides<ILocationGetter>())
            {
                FormKey? ma = null, mb = null;
                bool hasCentre = false;
                var kw = new HashSet<FormKey>();
                if (loc.Keywords != null) foreach (var k in loc.Keywords) kw.Add(k.FormKey);
                foreach (var g in new[] { loc.MasterSpecialReferences, loc.AddedSpecialReferences })
                {
                    if (g == null) continue;
                    foreach (var e in g)
                    {
                        if (e.LocationRefType.IsNull || e.Marker.IsNull) continue;
                        var t = e.LocationRefType.FormKey;
                        if (ctr != null && t == ctr.Value) hasCentre = true;
                        if (t == ka && ma == null) ma = e.Marker.FormKey;
                        if (t == kb && mb == null) mb = e.Marker.FormKey;
                    }
                }
                bool isPoi = (oeK != null && kw.Contains(oeK.Value)) || hasCentre;
                if (!isPoi || ma == null || mb == null) continue;
                if (TryPos(env, ma.Value, out var pa) && TryPos(env, mb.Value, out var pb)) outp.Add(Dist(pa, pb));
            }
            return outp;
        }

        /// <summary>
        /// WHAT EACH MARKER MEANS, on the one axis the records can answer: INSIDE or OUTSIDE.
        ///
        /// ⭐ HIS ASK, 2026-09-23: "it doesn't break things, just means we have to know what each
        /// marker means." A coverage table says how many places carry a marker and nothing about
        /// what kind of place it is IN, which is how a Delve ends up putting two beats on the same
        /// rock or straddling a door it cannot see.
        ///
        /// ⭐ THE SIGNAL IS MECHANICAL AND COSTS NOTHING: a special reference carries the CELL its
        /// marker sits in and the cell's GRID coordinate, and an interior cell's grid is the
        /// sentinel 32767, 32767 (0x7FFF). So "is this marker indoors" is a field, not an opinion.
        /// Corroborated against his own statement that a dungeon's boss markers are inside and its
        /// travel markers outside, and it agrees.
        ///
        /// ⛔ THIS DELIBERATELY RESOLVES NO POSITIONS. The link cache reads exterior placed refs and
        /// not interior ones -- 223 of 223 boss markers failed to resolve, and the distance table
        /// printed "no location carries both", which reads as an absence in the world rather than a
        /// blind reader. This measurement is built on the fields in the Location itself so it cannot
        /// go blind the same way.
        /// </summary>
        private static int Markers(bool dungeons)
        {
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();

            var name = new Dictionary<FormKey, string>();
            foreach (var x in env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>())
                if (x.EditorID != null) name[x.FormKey] = x.EditorID;

            var oeK = env.LoadOrder.PriorityOrder.WinningOverrides<IKeywordGetter>()
                .FirstOrDefault(x => string.Equals(x.EditorID, "LocTypeOE_Keyword", StringComparison.OrdinalIgnoreCase))?.FormKey;
            FormKey? ctr = null, boss = null;
            foreach (var kv in name)
            {
                if (kv.Value.Equals("RECenterLocRef", StringComparison.OrdinalIgnoreCase)) ctr = kv.Key;
                if (kv.Value.Equals("LocDungeonBossLocRef", StringComparison.OrdinalIgnoreCase)) boss = kv.Key;
            }

            var locs = new Dictionary<FormKey, int>();      // reftype -> locations carrying it
            var inside = new Dictionary<FormKey, int>();    // reftype -> refs in an interior cell
            var total = new Dictionary<FormKey, int>();     // reftype -> refs
            int pop = 0;

            foreach (var loc in env.LoadOrder.PriorityOrder.WinningOverrides<ILocationGetter>())
            {
                var seen = new HashSet<FormKey>();
                var rows = new List<(FormKey type, bool interior)>();
                var kw = new HashSet<FormKey>();
                if (loc.Keywords != null) foreach (var k in loc.Keywords) kw.Add(k.FormKey);
                foreach (var g in new[] { loc.MasterSpecialReferences, loc.AddedSpecialReferences })
                {
                    if (g == null) continue;
                    foreach (var e in g)
                    {
                        if (e.LocationRefType.IsNull) continue;
                        // 32767 is short.MaxValue and is the engine's "no grid", i.e. an interior.
                        bool interior = e.Grid.X == 32767 && e.Grid.Y == 32767;
                        rows.Add((e.LocationRefType.FormKey, interior));
                        seen.Add(e.LocationRefType.FormKey);
                    }
                }
                bool inPop = dungeons
                    ? (boss != null && seen.Contains(boss.Value))
                    : ((oeK != null && kw.Contains(oeK.Value)) || (ctr != null && seen.Contains(ctr.Value)));
                if (!inPop) continue;
                pop++;
                foreach (var t in seen) locs[t] = locs.GetValueOrDefault(t) + 1;
                foreach (var (t, i) in rows)
                {
                    total[t] = total.GetValueOrDefault(t) + 1;
                    if (i) inside[t] = inside.GetValueOrDefault(t) + 1;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"  population: {(dungeons ? "DUNGEONS (LocDungeonBossLocRef)" : "the POI pool")}, {pop} location(s)");
            Console.WriteLine($"  {"marker",-40} {"locs",6} {"cover",7} {"refs",6} {"inside",7}   where");
            foreach (var kv in locs.OrderByDescending(k => k.Value))
            {
                int t = total.GetValueOrDefault(kv.Key), ins = inside.GetValueOrDefault(kv.Key);
                double pctIn = t == 0 ? 0 : 100.0 * ins / t;
                string where = pctIn >= 99 ? "INSIDE" : pctIn <= 1 ? "outside" : "mixed";
                Console.WriteLine($"  {name.GetValueOrDefault(kv.Key, kv.Key.ToString()),-40} {kv.Value,6} "
                                  + $"{100.0 * kv.Value / Math.Max(1, pop),6:F1}% {t,6} {pctIn,6:F1}%   {where}");
            }
            Console.WriteLine();
            Console.WriteLine("  inside% is the share of that marker's references sitting in an INTERIOR cell");
            Console.WriteLine("  (grid sentinel 32767,32767). A 'mixed' marker means the SAME NAME is used both");
            Console.WriteLine("  sides of a door, which is the one a recipe author has to be careful with.");
            return 0;
        }

        private readonly record struct P3(float X, float Y, float Z);

        private static bool TryPos(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, FormKey k, out P3 p)
        {
            p = default;
            // Position sits directly on the placed record, not under a Placement sub-object.
            // Read off gen_moveref, which moves these for real, rather than guessed from the name.
            if (env.LinkCache.TryResolve<IPlacedObjectGetter>(k, out var po))
            { p = new P3(po.Position.X, po.Position.Y, po.Position.Z); return true; }
            return false;
        }

        private static double Dist(P3 a, P3 b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static double Median(List<double> v) => v.Count == 0 ? -1 : Pct(v.OrderBy(x => x).ToList(), .5);
        private static double Pct(List<double> sorted, double q)
            => sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)(q * sorted.Count))];

        private static (int, int, int, int) OriginCensus = (-1, -1, -1, -1);

        private static int Census(bool vanillaOnly)
        {
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            var all = PoolCensus(env);
            var pool = vanillaOnly ? all.Where(p => p.Vanilla).ToList() : all;

            // ⛔ THE ORIGIN-VERSUS-WINNER SPLIT, and it is the thing that explains a uniform offset.
            // PoolCensus reads the WINNING record, which is what the game evaluates. The manual's
            // published census was taken off Starfield.esm's own records. A vanilla POI that a later
            // plugin overrides keeps its vanilla FormKey, so it still reads as "vanilla" while the
            // record actually being graded is somebody else's. Counted here rather than reasoned:
            // a vanilla POI that leaves the pool on this box is a FACT about this load order.
            if (vanillaOnly)
            {
                var sf = env.LoadOrder.ListedOrder
                    .FirstOrDefault(l => l.ModKey.FileName.String.Equals("Starfield.esm", StringComparison.OrdinalIgnoreCase))?.Mod;
                if (sf != null)
                {
                    var oeK = env.LoadOrder.PriorityOrder.WinningOverrides<IKeywordGetter>()
                        .FirstOrDefault(x => string.Equals(x.EditorID, "LocTypeOE_Keyword", StringComparison.OrdinalIgnoreCase))?.FormKey;
                    var ctr = env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>()
                        .FirstOrDefault(x => string.Equals(x.EditorID, "RECenterLocRef", StringComparison.OrdinalIgnoreCase))?.FormKey;
                    int oKw = 0, oCtr = 0, oBoth = 0, oUnion = 0;
                    var lost = new List<string>();
                    var winners = pool.Select(p => p.Edid).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    foreach (var loc in sf.Locations)
                    {
                        var rt = new HashSet<FormKey>();
                        foreach (var g in new[] { loc.MasterSpecialReferences, loc.AddedSpecialReferences })
                        { if (g == null) continue; foreach (var e in g) if (!e.LocationRefType.IsNull) rt.Add(e.LocationRefType.FormKey); }
                        bool k = oeK != null && (loc.Keywords?.Any(x => x.FormKey == oeK.Value) ?? false);
                        bool c = ctr != null && rt.Contains(ctr.Value);
                        if (k) oKw++;
                        if (c) oCtr++;
                        if (k && c) oBoth++;
                        if (k || c) { oUnion++; if (!winners.Contains(loc.EditorID ?? "")) lost.Add(loc.EditorID ?? loc.FormKey.ToString()); }
                    }
                    OriginCensus = (oKw, oCtr, oBoth, oUnion);
                    Console.WriteLine();
                    Console.WriteLine("  the same corpus read at its ORIGIN record instead of its winner:");
                    Console.WriteLine($"    {oKw} / {oCtr} / {oBoth} / {oUnion}");
                    Console.WriteLine($"    vanilla POIs that LEAVE the pool once this load order's overrides win: {lost.Count}");
                    Console.WriteLine("    ⚠ part 32 predicted the live pool could only be LARGER than vanilla. It can be");
                    Console.WriteLine("      SMALLER: an override that drops the keyword or the centre marker removes a POI.");
                    foreach (var l in lost.Take(12)) Console.WriteLine("      " + l);
                    if (lost.Count > 12) Console.WriteLine($"      ... and {lost.Count - 12} more");
                }
            }

            int byKw = pool.Count(p => p.ByKeyword);
            int byCentre = pool.Count(p => p.ByCentre);
            int both = pool.Count(p => p.ByKeyword && p.ByCentre);

            Console.WriteLine();
            Console.WriteLine("  corpus: " + (vanillaOnly ? "Starfield.esm ONLY" : "the whole load order"));
            Console.WriteLine($"    LocTypeOE_Keyword      {byKw,5}");
            Console.WriteLine($"    RECenterLocRef         {byCentre,5}");
            Console.WriteLine($"    intersection           {both,5}");
            Console.WriteLine($"    union (the pool)       {pool.Count,5}");
            Console.WriteLine();
            Console.WriteLine("  published in the manual, part 32, vanilla ORIGIN records: 260 / 282 / 259 / 283");
            if (vanillaOnly)
            {
                // ⛔ THE CONTROL IS AGAINST THE ORIGIN READING, NOT THE WINNER, because that is the
                // corpus the published figures were taken on. Grading the winner against them would
                // convict the reader for a difference that is a fact about this load order. The
                // first version of this check did exactly that and reported CONTROL FAILS over a
                // reader that is exactly right.
                bool ok = OriginCensus == (260, 282, 259, 283);
                Console.WriteLine(ok
                    ? "  CONTROL HOLDS: read at the origin record this reader reproduces the published census exactly."
                    : "  ⛔ CONTROL FAILS: at the ORIGIN record this reader and the manual disagree on the same "
                      + "corpus. Do not trust a pool figure from it until that is explained.");
                if (!ok) return 1;
            }
            else
            {
                Console.WriteLine("  (a larger figure here is EXPECTED and is the point: the PCM registry is open,");
                Console.WriteLine("   so every POI another author ships widens the pool. Run --vanilla for the control.)");
            }
            return 0;
        }

        /// <summary>
        /// The POI working pool and what each one carries.
        ///
        /// ⛔ BOTH SPECIAL-REFERENCE LISTS ARE UNIONED. A Location carries MasterSpecialReferences
        /// and a sibling AddedSpecialReferences for entries added after the master set; reading only
        /// the first under-reports, silently and in the direction that makes a pool look smaller.
        ///
        /// ⛔ AND THE POOL IS A UNION, NOT AN INTERSECTION: 260 locations carry LocTypeOE_Keyword and
        /// 282 carry RECenterLocRef, and the intersection is 259. Keying on either alone loses real
        /// places. Measured 2026-09-23; part 32 of the manual carries the working.
        /// </summary>
        private static List<Poi> PoolCensus(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env)
        {
            var oeKeyword = env.LoadOrder.PriorityOrder.WinningOverrides<IKeywordGetter>()
                .FirstOrDefault(x => string.Equals(x.EditorID, "LocTypeOE_Keyword", StringComparison.OrdinalIgnoreCase))?.FormKey;
            var centre = env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>()
                .FirstOrDefault(x => string.Equals(x.EditorID, "RECenterLocRef", StringComparison.OrdinalIgnoreCase))?.FormKey;

            var outp = new List<Poi>();
            foreach (var loc in env.LoadOrder.PriorityOrder.WinningOverrides<ILocationGetter>())
            {
                var p = new Poi();
                foreach (var g in new[] { loc.MasterSpecialReferences, loc.AddedSpecialReferences })
                {
                    if (g == null) continue;
                    foreach (var e in g)
                        if (!e.LocationRefType.IsNull) p.RefTypes.Add(e.LocationRefType.FormKey);
                }
                if (loc.Keywords != null) foreach (var k in loc.Keywords) p.Keywords.Add(k.FormKey);

                p.ByKeyword = oeKeyword != null && p.Keywords.Contains(oeKeyword.Value);
                p.ByCentre = centre != null && p.RefTypes.Contains(centre.Value);
                p.Vanilla = loc.FormKey.ModKey.FileName.String.Equals("Starfield.esm", StringComparison.OrdinalIgnoreCase);
                p.Edid = loc.EditorID ?? "";
                if (p.ByKeyword || p.ByCentre) outp.Add(p);
            }
            return outp;
        }

        // ------------------------------------------------------------------ the build

        private static int Build(Recipe r, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, bool dry)
        {
            var markers = new Dictionary<string, FormKey>(StringComparer.OrdinalIgnoreCase);
            if (!Grade(r, t, env, markers))
            { Console.WriteLine("\n=== lint failed. NOTHING WRITTEN. ==="); return 1; }

            Console.WriteLine();
            Console.WriteLine("=== BUILD ===");

            string modFile = Path.Combine(env.DataFolderPath, t.mod + ".esm");
            if (!File.Exists(modFile)) { Console.WriteLine("REFUSED: " + modFile + " does not exist."); return 1; }
            var readParams = gen_quest_main.BuildReadParams(env.LoadOrder);

            var reqK = ResolveKeywords(env, r.place.theme.require, _ => { });
            var excK = ResolveKeywords(env, r.place.theme.exclude, _ => { });

            var myMod = StarfieldMod.CreateFromBinary(modFile, StarfieldRelease.Starfield, readParams);
            gen_quest_main.FixNextFormId(myMod);

            var source = myMod.Quests.FirstOrDefault(q => q.EditorID == t.@base);
            if (source == null) { Console.WriteLine("REFUSED: no base quest '" + t.@base + "' in " + t.mod); return 1; }

            var stale = myMod.Quests.Where(q => q.EditorID == r.id).Select(q => q.FormKey).ToList();
            foreach (var k in stale) myMod.Quests.Remove(k);
            if (stale.Count > 0) Console.WriteLine("  cleaned  : " + stale.Count + " previous " + r.id);

            var copy = source.DeepCopy();
            var clone = new Quest(myMod)
            {
                EditorID = r.id,
                Name = copy.Name,
                Aliases = copy.Aliases,
                Components = copy.Components,
                Data = copy.Data,
                Keywords = copy.Keywords,
                Location = copy.Location,
                MajorFlags = copy.MajorFlags,
                Objectives = copy.Objectives,
                MissionTypeKeyword = copy.MissionTypeKeyword,
                QuestType = copy.QuestType,
                Stages = copy.Stages,
                Summary = copy.Summary,
                VirtualMachineAdapter = copy.VirtualMachineAdapter,
                MissionBoardDescription = copy.MissionBoardDescription,
                MissionBoardInfoPanels = copy.MissionBoardInfoPanels,
            };
            myMod.Quests.Add(clone);

            // A clone that does not repoint its self-links drives the SOURCE quest's aliases,
            // silently, and looks correct in every dump. On a driver whose activator targets are
            // alias properties this is the difference between a new mission and a second copy of
            // the old one. A count of zero is a refusal, not a shrug.
            int repointed = gen_delvegatetest.RepointSelfLinks(clone.VirtualMachineAdapter, source.FormKey, clone.FormKey);
            Console.WriteLine("  clone    : " + r.id + " " + clone.FormKey + "  self-links repointed: " + repointed);
            if (repointed == 0) { Console.WriteLine("REFUSED: expected at least one self-reference and found none."); return 1; }

            int fail = 0;
            fail += WritePlaceConditions(clone, t, r, markers, reqK, excK);
            for (int i = 0; i < t.beatSlots.Count; i++)
                fail += WriteBeat(clone, t, t.beatSlots[i], r.beats[i], markers);
            fail += WriteProse(clone, t, r);
            if (fail > 0) { Console.WriteLine("\n=== " + fail + " problem(s). NOTHING WRITTEN. ==="); return 1; }

            if (dry) { Console.WriteLine("\n  --dry: nothing written."); return 0; }

            foreach (var rec in myMod.EnumerateMajorRecords()) rec.IsCompressed = false;
            env.Dispose();   // release the memory-mapped load order before overwriting one of its files
            myMod.WriteToBinary(modFile, gen_quest_main.BuildWriteParams());
            Console.WriteLine("\n  wrote " + modFile + " (" + new FileInfo(modFile).Length.ToString("N0") + " B)");

            return Verify(modFile, readParams, t, r, markers);
        }

        /// <summary>
        /// The place's conditions are REBUILT from the recipe, never patched.
        ///
        /// ⭐ Every LocationHasRefType and LocationHasKeyword is dropped and re-emitted from the beat
        /// markers and the theme predicate. Everything else on the list -- in practice the base's
        /// GetDistance leash -- is preserved verbatim, because it is the one condition type this tool
        /// has not proven it can construct.
        ///
        /// That is why an author cannot desynchronise the selection from the beats: there is no field
        /// in which to say something different.
        /// </summary>
        private static int WritePlaceConditions(Quest clone, Template t, Recipe r,
                                                Dictionary<string, FormKey> markers,
                                                List<FormKey> require, List<FormKey> exclude)
        {
            var loc = clone.Aliases?.OfType<QuestLocationAlias>().FirstOrDefault(a => a.ID == (uint)t.placeAlias);
            if (loc?.Conditions == null) { Console.WriteLine("REFUSED: place alias " + t.placeAlias + " has no conditions."); return 1; }

            var kept = loc.Conditions.Where(c => c.Data is not ILocationHasRefTypeConditionDataGetter
                                              && c.Data is not ILocationHasKeywordConditionDataGetter).ToList();
            int dropped = loc.Conditions.Count - kept.Count;
            loc.Conditions.Clear();

            foreach (var m in markers)
            {
                var d = new LocationHasRefTypeConditionData();
                d.FirstParameter = new FormLinkOrIndex<ILocationReferenceTypeGetter>(d, m.Value);
                loc.Conditions.Add(new ConditionFloat { Data = d, CompareOperator = CompareOperator.EqualTo, ComparisonValue = 1f });
            }
            foreach (var (k, want) in require.Select(k => (k, 1f)).Concat(exclude.Select(k => (k, 0f))))
            {
                var d = new LocationHasKeywordConditionData();
                d.FirstParameter = new FormLinkOrIndex<IKeywordGetter>(d, k);
                loc.Conditions.Add(new ConditionFloat { Data = d, CompareOperator = CompareOperator.EqualTo, ComparisonValue = want });
            }
            foreach (var c in kept) loc.Conditions.Add(c);

            Console.WriteLine($"  place    : {markers.Count} marker + {require.Count} require + {exclude.Count} exclude, "
                              + $"{kept.Count} preserved (dropped {dropped} authored by the base)");
            return 0;
        }

        /// <summary>
        /// Point one beat's marker alias at the place, and give the beat its objective and journal.
        /// ⚠ BOTH HALVES OF THE FILL ARE SET EVERY TIME, including the half that is not changing.
        /// "Which marker, inside which drawn place" is one fact; writing only the half that moved is
        /// how a quest ends up selecting a POI on one marker and hunting for another.
        /// </summary>
        private static int WriteBeat(Quest clone, Template t, BeatSlot slot, Beat beat, Dictionary<string, FormKey> markers)
        {
            var al = clone.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)slot.markerAlias);
            if (al?.Location == null) { Console.WriteLine("REFUSED: ref alias " + slot.markerAlias + " has no location fill."); return 1; }
            al.Location.AliasID = t.placeAlias;
            al.Location.RefType.SetTo(markers[beat.at]);

            var ob = clone.Objectives?.FirstOrDefault(o => o.Index == slot.objective);
            if (ob == null) { Console.WriteLine("REFUSED: no objective " + slot.objective + " on the base."); return 1; }
            ob.DisplayText = Expand(beat.objective!, t);

            if (beat.journal != null)
            {
                var e = clone.Stages?.FirstOrDefault(s => s.Index == slot.journalStage)?.LogEntries?.FirstOrDefault();
                if (e == null)
                {
                    Console.WriteLine($"REFUSED: stage {slot.journalStage} has no log entry to carry this beat's journal. "
                                      + "Authoring one is a stage write, not an alias write, and this tool does not do it.");
                    return 1;
                }
                e.Entry = Expand(beat.journal, t);
            }

            Console.WriteLine($"  beat     : alias {slot.markerAlias} -> {beat.at} inside place alias {t.placeAlias}"
                              + $", objective {slot.objective}"
                              + (beat.journal != null ? $", journal at stage {slot.journalStage}" : ", no journal"));
            return 0;
        }

        private static int WriteProse(Quest clone, Template t, Recipe r)
        {
            clone.Name = Expand(r.prose.name!, t);
            var e = clone.Stages?.FirstOrDefault(s => s.Index == t.briefingStage)?.LogEntries?.FirstOrDefault();
            if (e == null) { Console.WriteLine("REFUSED: no log entry on briefing stage " + t.briefingStage + "."); return 1; }
            e.Entry = Expand(r.prose.briefing!, t);
            Console.WriteLine("  prose    : name + briefing at stage " + t.briefingStage);
            return 0;
        }

        /// <summary>Map the recipe's portable tokens onto this template's actual alias names.</summary>
        private static string Expand(string s, Template t)
        {
            foreach (var kv in t.tokens) s = s.Replace("<" + kv.Key + ">", "<Alias=" + kv.Value + ">");
            return s;
        }

        // ------------------------------------------------------------------ verification

        /// <summary>
        /// Read the written file back OFF DISK and grade it. An assertion over the objects just
        /// built in memory cannot fail, and what landed in the file is the only thing that matters.
        /// </summary>
        private static int Verify(string modFile, Mutagen.Bethesda.Plugins.Binary.Parameters.BinaryReadParameters readParams,
                                  Template t, Recipe r, Dictionary<string, FormKey> markers)
        {
            Console.WriteLine();
            Console.WriteLine("  verification, re-read from disk:");
            var reread = StarfieldMod.CreateFromBinaryOverlay(modFile, StarfieldRelease.Starfield, readParams);
            var q = reread.Quests.FirstOrDefault(x => x.EditorID == r.id);
            if (q == null) { Console.WriteLine("    FAIL: the quest is not in the written file."); return 1; }

            int fail = 0;
            var loc = q.Aliases?.OfType<IQuestLocationAliasGetter>().FirstOrDefault(a => a.ID == (uint)t.placeAlias);
            var condMarkers = loc?.Conditions?
                .Select(c => c.Data).OfType<ILocationHasRefTypeConditionDataGetter>()
                .Select(h => h.FirstParameter.Link.FormKey).ToHashSet() ?? new HashSet<FormKey>();

            fail += Check("place demands every beat marker + the map marker",
                          markers.Values.All(k => condMarkers.Contains(k)) ? "yes" : "no", "yes");
            fail += Check("place demands NOTHING ELSE", condMarkers.Count.ToString(), markers.Count.ToString());

            for (int i = 0; i < t.beatSlots.Count; i++)
            {
                var slot = t.beatSlots[i];
                var al = q.Aliases?.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.ID == (uint)slot.markerAlias);
                fail += Check($"beat {i + 1} fills from {r.beats[i].at}",
                              al?.Location?.RefType.FormKey == markers[r.beats[i].at] ? "yes" : "no", "yes");
                fail += Check($"beat {i + 1} searches the place alias",
                              (al?.Location?.AliasID)?.ToString() ?? "unset", t.placeAlias.ToString());
                var ob = q.Objectives?.FirstOrDefault(o => o.Index == slot.objective);
                fail += Check($"beat {i + 1} objective rewritten",
                              (ob?.DisplayText?.String ?? "") == Expand(r.beats[i].objective!, t) ? "yes" : "no", "yes");
            }
            fail += Check("name rewritten", (q.Name?.String ?? "") == Expand(r.prose.name!, t) ? "yes" : "no", "yes");

            // NEGATIVE CONTROL. Every check above passes trivially if the collapse never happened --
            // an alias still pointing at the base's SECOND place would satisfy "fills from X" while
            // the mission quietly spanned two POIs. So assert the thing that must have MOVED.
            Console.WriteLine();
            Console.WriteLine("  negative control:");
            var strays = q.Aliases?.OfType<IQuestReferenceAliasGetter>()
                .Where(a => a.Location != null && t.collapsedPlaceAliases.Contains(a.Location.AliasID ?? -1))
                .Select(a => a.ID).ToList() ?? new List<uint>();
            Console.WriteLine("    ref aliases still searching a collapsed place: "
                              + (strays.Count == 0 ? "none" : string.Join(", ", strays)));
            if (strays.Count > 0) { Console.WriteLine("    FAIL: the collapse was partial, so this spans two POIs."); fail++; }
            else Console.WriteLine("    control behaved: every beat resolves inside ONE drawn POI.");

            Console.WriteLine();
            if (fail > 0) { Console.WriteLine("=== WRITTEN, BUT " + fail + " VERIFICATION FAILURE(S). Do not test yet. ==="); return 1; }

            Console.WriteLine("=== " + r.id + " IS IN. The template's driver owns the stage graph; nothing else to wire. ===");
            Console.WriteLine();
            Console.WriteLine("  In game, on a throwaway save:   help " + r.id + " 0   then   startquest <id>");
            foreach (var c in t.cannot) Console.WriteLine("  ⚠ this template cannot express: " + c);
            return 0;
        }

        private static int Check(string label, string actual, string expected)
        {
            bool ok = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
            Console.WriteLine("    [" + (ok ? "OK  " : "FAIL") + "] " + label + ": got '" + actual + "' expected '" + expected + "'");
            return ok ? 0 : 1;
        }

        // ------------------------------------------------------------------ plumbing

        private static int ListTemplates(List<Template> ts)
        {
            foreach (var t in ts)
            {
                Console.WriteLine();
                Console.WriteLine("  " + t.id);
                Console.WriteLine("    base    : " + t.@base + " (" + t.mod + ")");
                Console.WriteLine("    driver  : " + t.driver);
                Console.WriteLine("    beats   : " + t.beats + (t.carriesItem ? ", carries an item between them" : ""));
                Console.WriteLine("    tokens  : " + string.Join(", ", t.tokens.Keys.Select(k => "<" + k + ">")));
                foreach (var c in t.cannot) Console.WriteLine("    cannot  : " + c);
                Console.WriteLine("    verified: " + t.verifiedFrom);
            }
            return 0;
        }

        private static List<Template>? LoadTemplates(string dataDir, out string? err)
        {
            err = null;
            string f = Path.Combine(dataDir, "templates.json");
            if (!File.Exists(f)) { err = "no templates.json at " + f; return null; }
            try
            {
                var tf = JsonSerializer.Deserialize<TemplateFile>(File.ReadAllText(f), JsonOpts);
                if (tf == null || tf.templates.Count == 0) { err = f + " holds no templates"; return null; }
                if (tf.schema != 1) { err = $"templates.json schema is {tf.schema}, this tool speaks 1"; return null; }
                return tf.templates;
            }
            catch (JsonException ex) { err = "templates.json is not valid JSON -- " + ex.Message; return null; }
        }

        private static IEnumerable<string> CandidateRoots()
        {
            // Walk up from the assembly first (works for a published exe and for `dotnet run`, whose
            // binaries sit under bin/) and only then from the working directory. Printed on failure
            // rather than guessed at, so "it cannot find its data" is never a mystery.
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null) { yield return d.FullName; d = d.Parent; }
            d = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (d != null) { yield return d.FullName; d = d.Parent; }
        }

        private static string? ResolveDataDir()
        {
            foreach (var root in CandidateRoots())
            {
                string p = Path.Combine(root, "data", "delves");
                if (Directory.Exists(p)) return p;
            }
            return null;
        }
    }
}
