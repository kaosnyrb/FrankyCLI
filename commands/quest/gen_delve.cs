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
            /// <summary>
            /// Which build path the row takes. "dualactivator" is the first row's: the base's own
            /// driver runs the mission and extra beats are journal-only. "delve4" REPLACES the base's
            /// driver with one of ours from FrankyCLI/papyrus, so every beat has an objective.
            /// </summary>
            public string kind { get; set; } = "dualactivator";
            public string @base { get; set; } = "";
            public string mod { get; set; } = "";
            public string driver { get; set; } = "";
            /// <summary>delve4: the base's script entry that ours replaces, and whose values it takes.</summary>
            public string? replacesDriver { get; set; }
            /// <summary>delve4: fewest who stand with the carrier. The most is taken off the base.</summary>
            public int gangMin { get; set; }
            public int beats { get; set; }
            public bool carriesItem { get; set; }
            public int placeAlias { get; set; }
            public int mapMarkerAlias { get; set; }
            public int briefingStage { get; set; }
            public int completeStage { get; set; }
            public List<BeatSlot> beatSlots { get; set; } = new();
            public int maxBeats { get; set; }
            public int extraBeatStageBase { get; set; }
            public int extraBeatStageStep { get; set; }
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
            /// <summary>delve4: this beat's marker alias does not exist on the base and is created.</summary>
            public bool create { get; set; }
            /// <summary>delve4: this beat is an earlier beat's place again (0-based index), not a new one.</summary>
            public int returnTo { get; set; } = -1;
            /// <summary>
            /// Set by the build, never by the registry: the alias this beat's objective points at when it
            /// is neither its activator nor its marker (beat 3's carrier, filled at runtime).
            /// </summary>
            [System.Text.Json.Serialization.JsonIgnore] public int targetAlias { get; set; } = -1;
            public int ObjectiveTarget => targetAlias >= 0 ? targetAlias : activatorAlias >= 0 ? activatorAlias : markerAlias;
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
            public Items? items { get; set; }
            public List<Beat> beats { get; set; } = new();
        }
        /// <summary>delve4: inventory names for the two halves. Both are CLONED items, never the base's.</summary>
        private sealed class Items { public string? load { get; set; } public string? missing { get; set; } }
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
            int maxBeats = t.maxBeats > 0 ? t.maxBeats : t.beats;
            if (r.beats.Count < t.beats)
                Fatal($"recipe has {r.beats.Count} beat(s); template '{t.id}' needs at least {t.beats}. "
                      + "What it cannot do: " + string.Join("; ", t.cannot));
            else if (r.beats.Count > maxBeats)
                Fatal($"recipe has {r.beats.Count} beat(s); template '{t.id}' tops out at {maxBeats}.");

            // The driver's own slots are the FIRST and LAST beat. Everything between them is an
            // extra slot this tool creates, and an extra beat has no objective because there is no
            // driver to display one -- writing the string anyway would put prose in the record that
            // no player can ever see, which is worse than refusing it.
            bool delve4 = t.kind == "delve4";
            if (delve4)
            {
                // Our driver displays every beat's objective, so every beat is driven, and the beat
                // count is the state machine's shape rather than a ceiling.
                if (r.beats.Count != t.beatSlots.Count)
                    Fatal($"template '{t.id}' is exactly {t.beatSlots.Count} beats; the recipe has {r.beats.Count}.");
                if (string.IsNullOrWhiteSpace(t.replacesDriver)) Fatal($"template '{t.id}' names no replacesDriver");
                if (string.IsNullOrWhiteSpace(r.items?.load) || string.IsNullOrWhiteSpace(r.items?.missing))
                    Fatal("items.load and items.missing are both required: each half is a cloned item the player carries, and an item with no name shows as a blank line in the inventory.");
                else if (Tokens(r.items!.load!).Concat(Tokens(r.items.missing!)).Any())
                    Fatal("an item name carries a <Token>. Item names are not alias contexts, so it would print literally in the inventory.");
                // ⭐ THE RETURN IS THE DESIGN, so it is refused rather than warned when it is not one.
                for (int i = 0; i < t.beatSlots.Count && i < r.beats.Count; i++)
                {
                    int back = t.beatSlots[i].returnTo;
                    if (back >= 0 && back < r.beats.Count
                        && !string.Equals(r.beats[i].at, r.beats[back].at, StringComparison.OrdinalIgnoreCase))
                        Fatal($"beat {i + 1} is a RETURN to beat {back + 1}'s place and must name the same marker "
                              + $"('{r.beats[back].at}'); it names '{r.beats[i].at}'.");
                }
            }
            for (int i = 0; i < r.beats.Count; i++)
            {
                bool driven = delve4 || i == 0 || i == r.beats.Count - 1;
                if (string.IsNullOrWhiteSpace(r.beats[i].at)) Fatal($"beat {i + 1} names no marker");
                if (driven && string.IsNullOrWhiteSpace(r.beats[i].objective))
                    Fatal($"beat {i + 1} is one of the driver's own slots and has no objective text");
                if (!driven && !string.IsNullOrWhiteSpace(r.beats[i].objective))
                    Fatal($"beat {i + 1} is an EXTRA beat and carries an objective. The driver displays "
                          + $"objectives {string.Join(" and ", t.beatSlots.Select(s => s.objective))} and no others, "
                          + "so this text would never reach a player. Give it a journal line instead.");
                if (!driven && string.IsNullOrWhiteSpace(r.beats[i].journal))
                    Fatal($"beat {i + 1} is an EXTRA beat with no journal line, so nothing about it would "
                          + "reach the player at all.");
            }
            int extras = Math.Max(0, r.beats.Count - t.beatSlots.Count);
            if (extras > 0)
            {
                int last = t.extraBeatStageBase + t.extraBeatStageStep * (extras - 1);
                if (t.extraBeatStageBase <= 0 || t.extraBeatStageStep <= 0)
                    Fatal($"template '{t.id}' declares no extra-beat stage numbering, so it cannot take extras");
                else if (last >= t.completeStage)
                    Fatal($"{extras} extra beat(s) would number stages up to {last}, at or past the "
                          + $"completing stage {t.completeStage}");
                else
                    Warn($"{extras} extra beat(s) will be CREATED: a marker alias, an activator, a stage and a "
                         + $"stock hook each, numbered {t.extraBeatStageBase} to {last}. Journal only, no objective.");
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

            var plan = new List<(BeatSlot slot, Beat beat, bool created)>();
            var made4 = new Delve4Made();
            if (t.kind == "delve4")
            {
                if (fail == 0) fail += BuildDelve4(myMod, clone, t, r, markers, plan, made4);
            }
            else
            {
                // The driver's own slots are the FIRST and LAST beat; extras are created between them.
                int nb = r.beats.Count;
                plan.Add((t.beatSlots[0], r.beats[0], false));
                for (int i = 1; i < nb - 1; i++)
                {
                    var made = AddBeatSlot(clone, t, i, t.extraBeatStageBase + t.extraBeatStageStep * (i - 1));
                    if (made == null) { fail++; break; }
                    plan.Add((made, r.beats[i], true));
                }
                plan.Add((t.beatSlots[^1], r.beats[^1], false));

                for (int i = 0; i < plan.Count && fail == 0; i++)
                    fail += WriteBeat(clone, t, plan[i].slot, plan[i].beat, markers, plan[i].created);

                // An extra beat fires on a stock DefaultAliasOnActivate gated on the PREVIOUS beat's
                // stage, so the chain sequences itself with no state variable. The first extra gates on
                // the driver's own stage-50, which it sets on the first activation.
                for (int i = 1; i < nb - 1 && fail == 0; i++)
                {
                    var slot = plan[i].slot;
                    int prereq = i == 1 ? t.beatSlots[0].journalStage : plan[i - 1].slot.journalStage;
                    fail += HookActivator(clone, slot.activatorAlias, slot.journalStage, prereq);
                }
            }
            fail += WriteProse(clone, t, r);
            if (fail > 0) { Console.WriteLine("\n=== " + fail + " problem(s). NOTHING WRITTEN. ==="); return 1; }

            if (dry) { Console.WriteLine("\n  --dry: nothing written."); return 0; }

            foreach (var rec in myMod.EnumerateMajorRecords()) rec.IsCompressed = false;
            env.Dispose();   // release the memory-mapped load order before overwriting one of its files
            myMod.WriteToBinary(modFile, gen_quest_main.BuildWriteParams());
            Console.WriteLine("\n  wrote " + modFile + " (" + new FileInfo(modFile).Length.ToString("N0") + " B)");

            return Verify(modFile, readParams, t, r, markers, plan, made4);
        }

        // ------------------------------------------------------------------ delve4

        /// <summary>What BuildDelve4 created, handed to Verify rather than re-derived there.</summary>
        private sealed class Delve4Made
        {
            public uint CarrierMarkerAlias, CarrierAlias;
            public FormKey LoadItem, MissingItem;
            public Dictionary<string, (FormKey obj, short alias)> Props = new();
            public Dictionary<string, int> IntProps = new();
        }

        /// <summary>
        /// THE FOUR-BEAT DELVE: carry, absence, recover, return.
        ///
        /// ⭐ WHAT MAKES IT POSSIBLE IS REPLACING THE DRIVER, NOT ADDING TO IT. The base's own script
        /// knows two objectives. Ours (FrankyCLI/papyrus/duo_delve_driver.psc) knows four, so the
        /// base's entry is removed and ours goes in its place, TAKING the values that are facts about
        /// this base (the cargo item, the gang list, the most who stand, the fail message) off the
        /// entry it replaces. Anything we cannot find there is a refusal, never a default.
        ///
        /// ⛔ NEITHER ITEM IS THE BASE'S. The base's cargo item ships in every Overtime cargo quest
        /// that uses it; renaming it would rename his live missions. Both halves are CLONES of it,
        /// keyed on this recipe's id so a rebuild replaces them instead of piling up copies.
        /// </summary>
        private static int BuildDelve4(StarfieldMod myMod, Quest clone, Template t, Recipe r,
                                       Dictionary<string, FormKey> markers,
                                       List<(BeatSlot slot, Beat beat, bool created)> plan, Delve4Made made)
        {
            // --- the base's driver, whose values we take ------------------------------------------
            var vma = clone.VirtualMachineAdapter;
            var old = vma?.Scripts.FirstOrDefault(s => string.Equals(s.Name, t.replacesDriver, StringComparison.OrdinalIgnoreCase));
            if (vma == null || old == null)
            { Console.WriteLine($"REFUSED: the base carries no '{t.replacesDriver}' script entry to replace."); return 1; }
            FormKey? ObjOf(string n) => old.Properties.OfType<ScriptObjectProperty>()
                .FirstOrDefault(p => string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase))?.Object.FormKey;
            int? IntOf(string n) => old.Properties.OfType<ScriptIntProperty>()
                .FirstOrDefault(p => string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase))?.Data;
            var cargo = ObjOf("CargoObject"); var gang = ObjOf("GangMembers"); var failMsg = ObjOf("FailMessage");
            var gangMax = IntOf("MaxGangMembers");
            if (cargo == null || gang == null || failMsg == null || gangMax == null)
            { Console.WriteLine("REFUSED: the replaced driver lacks CargoObject, GangMembers, FailMessage or MaxGangMembers."); return 1; }

            // --- the two halves, cloned ---------------------------------------------------------------
            var src = myMod.MiscItems.FirstOrDefault(m => m.FormKey == cargo.Value);
            if (src == null)
            { Console.WriteLine($"REFUSED: the cargo item {cargo} is not a MiscItem in {t.mod}; this tool clones only its own mod's items."); return 1; }
            FormKey CloneItem(string suffix, string name)
            {
                string edid = r.id + "_" + suffix;
                foreach (var k in myMod.MiscItems.Where(m => m.EditorID == edid).Select(m => m.FormKey).ToList())
                    myMod.MiscItems.Remove(k);
                var mi = myMod.MiscItems.DuplicateInAsNewRecord(src);
                mi.EditorID = edid;
                mi.Name = name;
                Console.WriteLine($"  +item    : {edid} {mi.FormKey}  \"{name}\"  (clone of {src.EditorID})");
                return mi.FormKey;
            }
            made.LoadItem = CloneItem("Load", r.items!.load!);
            made.MissingItem = CloneItem("OtherHalf", r.items.missing!);

            // --- stages the base does not have --------------------------------------------------------
            foreach (var s in t.beatSlots.Select(s => s.journalStage).Distinct())
            {
                if (clone.Stages!.Any(x => x.Index == s)) continue;
                var st = new QuestStage { Index = (ushort)s };
                st.LogEntries.Add(new QuestLogEntry());
                clone.Stages.Add(st);
                Console.WriteLine($"  +stage   : {s}");
            }

            // --- the created marker (beat 3's) -----------------------------------------------------------
            var srcMarker = clone.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)t.beatSlots[0].markerAlias);
            if (srcMarker?.Location == null) { Console.WriteLine("REFUSED: slot 0's marker alias has no location fill to clone."); return 1; }
            var slots = new List<BeatSlot>();
            for (int i = 0; i < t.beatSlots.Count; i++)
            {
                var ts = t.beatSlots[i];
                if (!ts.create) { slots.Add(ts); continue; }
                uint id = 1 + clone.Aliases!.SelectMany(Flatten).Select(x => x.id).DefaultIfEmpty(0u).Max();
                var m = srcMarker.DeepCopy();
                m.ID = id;
                m.Name = "DelveCarrierMarker";
                clone.Aliases.Add(m);
                made.CarrierMarkerAlias = id;
                Console.WriteLine($"  +alias   : beat {i + 1} marker {id} (cloned from alias {t.beatSlots[0].markerAlias})");

                // ⭐ THE CARRIER HIMSELF, and the objective points here rather than at the marker (his
                // eye, first play of duo_delve03). An EMPTY alias: cloned for its shape, then every
                // fill removed and Optional set, because a non-optional alias with no fill makes the
                // whole quest silently not start. The driver fills it with PlaceAtMe's akAliasToFill.
                var c = srcMarker.DeepCopy();
                c.ID = id + 1;
                c.Name = "DelveCarrier";
                c.Location = null;
                c.Flags = QuestReferenceAlias.Flag.Optional;
                clone.Aliases.Add(c);
                made.CarrierAlias = c.ID;
                Console.WriteLine($"  +alias   : beat {i + 1} carrier {c.ID} (EMPTY, Optional; the driver fills it on spawn)");

                slots.Add(new BeatSlot { markerAlias = (int)id, activatorAlias = -1, objective = ts.objective,
                                         journalStage = ts.journalStage, targetAlias = (int)c.ID });
            }

            // --- objectives the base does not have, cloned from its last one ---------------------------
            var lastOb = clone.Objectives?.OrderBy(o => o.Index).LastOrDefault();
            if (lastOb == null || lastOb.Targets == null || lastOb.Targets.Count != 1)
            { Console.WriteLine("REFUSED: the base's last objective is not a single-target objective to clone."); return 1; }
            foreach (var s in slots)
            {
                if (clone.Objectives!.Any(o => o.Index == s.objective)) continue;
                var ob = lastOb.DeepCopy();
                ob.Index = (ushort)s.objective;
                clone.Objectives.Add(ob);
                Console.WriteLine($"  +obj     : {s.objective}");
            }
            // Every objective's target is WRITTEN, including the base's own two: which alias an
            // objective points at is one fact per beat, and inheriting it is how a marker ends up
            // on the wrong thing. Beat 3 points at the carrier himself, through his empty alias.
            foreach (var s in slots)
            {
                var ob = clone.Objectives!.First(o => o.Index == s.objective);
                if (ob.Targets == null || ob.Targets.Count != 1) { Console.WriteLine($"REFUSED: objective {s.objective} has no single target."); return 1; }
                ob.Targets[0].AliasID = s.ObjectiveTarget;
            }

            // --- beats: fills, objective text, journals -------------------------------------------------
            for (int i = 0; i < slots.Count; i++)
            {
                plan.Add((slots[i], r.beats[i], false));
                // A return writes no fill: it IS the earlier beat's alias, already written.
                if (t.beatSlots[i].returnTo >= 0)
                {
                    var ob = clone.Objectives!.First(o => o.Index == slots[i].objective);
                    ob.DisplayText = Expand(r.beats[i].objective!, t);
                    if (r.beats[i].journal != null)
                        clone.Stages!.First(s => s.Index == slots[i].journalStage).LogEntries[0].Entry = Expand(r.beats[i].journal!, t);
                    Console.WriteLine($"  beat     : {i + 1} returns to beat {t.beatSlots[i].returnTo + 1}'s place, objective {slots[i].objective}");
                    continue;
                }
                int f = WriteBeat(clone, t, slots[i], r.beats[i], markers, false);
                if (f > 0) return f;
            }

            // --- the driver swap ------------------------------------------------------------------------
            vma.Scripts.Remove(old);
            var sc = new ScriptEntry { Name = t.driver };
            void Alias(string n, int aliasId)
            {
                var p = new ScriptObjectProperty { Name = n, Flags = ScriptProperty.Flag.Edited };
                p.Object.SetTo(clone.FormKey);
                p.Alias = (short)aliasId;
                sc.Properties.Add(p);
                made.Props[n] = (clone.FormKey, (short)aliasId);
            }
            void Obj(string n, FormKey k)
            {
                var p = new ScriptObjectProperty { Name = n, Flags = ScriptProperty.Flag.Edited };
                p.Object.SetTo(k);
                sc.Properties.Add(p);
                made.Props[n] = (k, (short)-1);
            }
            void Int(string n, int v)
            {
                sc.Properties.Add(new ScriptIntProperty { Name = n, Data = v, Flags = ScriptProperty.Flag.Edited });
                made.IntProps[n] = v;
            }
            Alias("LoadTarget", slots[0].activatorAlias);
            Alias("CentreTarget", slots[1].activatorAlias);
            Alias("CarrierMarker", (int)made.CarrierMarkerAlias);
            Alias("Carrier", (int)made.CarrierAlias);
            Obj("LoadItem", made.LoadItem);
            Obj("MissingItem", made.MissingItem);
            Obj("GangMembers", gang.Value);
            Obj("FailMessage", failMsg.Value);
            Int("MinGangMembers", t.gangMin);
            Int("MaxGangMembers", gangMax.Value);
            vma.Scripts.Add(sc);
            Console.WriteLine($"  driver   : {t.replacesDriver} REMOVED, {t.driver} in its place with {sc.Properties.Count} properties");
            return 0;
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
        /// CREATE A BEAT SLOT: a marker alias that fills inside the place, an activator create-obj'd
        /// at it, and a stage to carry the beat's journal line.
        ///
        /// ⭐ IT CLONES THE TEMPLATE'S FIRST SLOT RATHER THAN CONSTRUCTING FROM NOTHING, and that is
        /// the whole safety argument. A QuestReferenceAlias carries flags this tool has no business
        /// having an opinion about -- slot 0's activator is QuestObject | StoresText | CreateRefTemp,
        /// and a fresh object would have none of them. Copying a slot that demonstrably works in a
        /// shipped quest brings along every field I do not know exists, which is the same argument
        /// that made the layer-1 clone safe. Only ID, Name, RefType and the create-at target move.
        ///
        /// ⚠ His own warning on this operation, and it is why extras are built as a UNIT rather than
        /// one alias at a time: "aliases freak out if they are wrong it's better to build in layers."
        /// A half-made slot -- a marker with no activator, or an activator pointing at an alias that
        /// does not exist -- is an unfillable non-optional alias, and a mission with one of those
        /// SILENTLY DOES NOT LOAD. So this returns null and refuses rather than leaving a partial.
        /// </summary>
        private static BeatSlot? AddBeatSlot(Quest q, Template t, int index, int stage)
        {
            var src = t.beatSlots[0];
            var srcMarker = q.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)src.markerAlias);
            var srcAct = q.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)src.activatorAlias);
            if (srcMarker?.Location == null || srcAct?.CreateReferenceToObject == null)
            { Console.WriteLine("REFUSED: template slot 0 is not a marker+activator pair on this base."); return null; }

            uint next = 1 + (q.Aliases!.SelectMany(Flatten).Select(x => x.id).DefaultIfEmpty(0u).Max());
            uint mid = next, aid = next + 1;

            var marker = srcMarker.DeepCopy();
            marker.ID = mid;
            marker.Name = "DelveBeat" + index + "Marker";

            var act = srcAct.DeepCopy();
            act.ID = aid;
            act.Name = "DelveBeat" + index + "Target";
            act.CreateReferenceToObject!.AliasID = (short)mid;

            q.Aliases.Add(marker);
            q.Aliases.Add(act);

            // The stage has to exist before anything can set it, and a stage with no log entry
            // renders nothing at all, so the entry is created here rather than left to the prose
            // pass to discover missing.
            if (q.Stages!.Any(s => s.Index == stage))
            { Console.WriteLine($"REFUSED: stage {stage} already exists on this base; extra-beat numbering collides."); return null; }
            var st = new QuestStage { Index = (ushort)stage };
            st.LogEntries.Add(new QuestLogEntry());
            q.Stages.Add(st);

            Console.WriteLine($"  +slot    : beat {index + 1} created -- marker alias {mid}, activator {aid}, stage {stage}");
            return new BeatSlot { markerAlias = (int)mid, activatorAlias = (int)aid, objective = -1, journalStage = stage };
        }

        private static IEnumerable<(uint id, string name)> Flatten(IAQuestAliasGetter a)
        {
            if (a is IQuestReferenceAliasGetter r && r.Name != null) yield return (r.ID, r.Name);
            if (a is IQuestLocationAliasGetter l && l.Name != null) yield return (l.ID, l.Name);
            if (a is IQuestCollectionAliasGetter c)
                foreach (var m in c.Collection)
                    if (m.ReferenceAlias?.Name != null) yield return (m.ReferenceAlias.ID, m.ReferenceAlias.Name!);
        }

        /// <summary>
        /// Attach a stock DefaultAliasOnActivate to a created activator: gate it on the previous
        /// beat's stage, set its own. PrereqStage + StageToSet IS the sequencing mechanism, with no
        /// state variable and no Papyrus.
        /// ⛔ The gate is GetStageDone(PrereqStage), so a prereq nothing sets blocks the hook FOR
        /// EVER and in total silence. That cost an in-game run earlier today.
        /// </summary>
        private static int HookActivator(Quest q, int aliasId, int stageToSet, int prereq)
        {
            var vma = q.VirtualMachineAdapter;
            if (vma == null) { Console.WriteLine("REFUSED: quest has no VirtualMachineAdapter to hang a hook on."); return 1; }
            var entry = new QuestFragmentAlias();
            entry.Property.Object.SetTo(q.FormKey);
            entry.Property.Alias = (short)aliasId;
            var sc = new ScriptEntry { Name = "DefaultAliasOnActivate" };
            sc.Properties.Add(new ScriptIntProperty { Name = "StageToSet", Data = stageToSet, Flags = ScriptProperty.Flag.Edited });
            sc.Properties.Add(new ScriptIntProperty { Name = "PrereqStage", Data = prereq, Flags = ScriptProperty.Flag.Edited });
            entry.Scripts.Add(sc);
            vma.Aliases.Add(entry);
            Console.WriteLine($"  +hook    : alias {aliasId} DefaultAliasOnActivate  sets {stageToSet} after {prereq}");
            return 0;
        }

        /// <summary>
        /// Point one beat's marker alias at the place, and give the beat its objective and journal.
        /// ⚠ BOTH HALVES OF THE FILL ARE SET EVERY TIME, including the half that is not changing.
        /// "Which marker, inside which drawn place" is one fact; writing only the half that moved is
        /// how a quest ends up selecting a POI on one marker and hunting for another.
        /// </summary>
        private static int WriteBeat(Quest clone, Template t, BeatSlot slot, Beat beat,
                                     Dictionary<string, FormKey> markers, bool created)
        {
            var al = clone.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)slot.markerAlias);
            if (al?.Location == null) { Console.WriteLine("REFUSED: ref alias " + slot.markerAlias + " has no location fill."); return 1; }
            al.Location.AliasID = t.placeAlias;
            al.Location.RefType.SetTo(markers[beat.at]);

            // A created slot has no objective by construction: the driver displays only its own two,
            // and the lint has already refused any recipe that tried to give one an objective.
            if (!created)
            {
                var ob = clone.Objectives?.FirstOrDefault(o => o.Index == slot.objective);
                if (ob == null) { Console.WriteLine("REFUSED: no objective " + slot.objective + " on the base."); return 1; }
                ob.DisplayText = Expand(beat.objective!, t);
            }

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
                                  Template t, Recipe r, Dictionary<string, FormKey> markers,
                                  List<(BeatSlot slot, Beat beat, bool created)> plan, Delve4Made made4)
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

            // ⛔ THE PLAN IS HANDED IN, NEVER RE-DERIVED. The first version of this loop walked the
            // TEMPLATE's slots against the recipe's beats positionally, which is correct only while
            // the two have the same length. On the first three-beat build it compared the middle
            // beat against the last slot and then threw on a null objective, AFTER the file had been
            // written. Two loops over the same mapping will diverge; there is now one.
            for (int i = 0; i < plan.Count; i++)
            {
                var (slot, beat, created) = plan[i];
                string tag = $"beat {i + 1}{(created ? " (created)" : "")}";
                var al = q.Aliases?.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.ID == (uint)slot.markerAlias);
                fail += Check($"{tag} fills from {beat.at}",
                              al?.Location?.RefType.FormKey == markers[beat.at] ? "yes" : "no", "yes");
                fail += Check($"{tag} searches the place alias",
                              (al?.Location?.AliasID)?.ToString() ?? "unset", t.placeAlias.ToString());

                if (created)
                {
                    // A created slot's whole value is that the player sees it, and the two ways it
                    // can silently fail to are an activator that create-objs nowhere and a stage
                    // whose log entry never got its text.
                    var act = q.Aliases?.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.ID == (uint)slot.activatorAlias);
                    fail += Check($"{tag} activator create-objs AT its own marker",
                                  (act?.CreateReferenceToObject?.AliasID)?.ToString() ?? "unset", slot.markerAlias.ToString());
                    fail += Check($"{tag} activator has an object to place",
                                  act?.CreateReferenceToObject?.Object.IsNull == false ? "yes" : "no", "yes");
                    var st = q.Stages?.FirstOrDefault(s => s.Index == slot.journalStage);
                    fail += Check($"{tag} journal landed on stage {slot.journalStage}",
                                  (st?.LogEntries?.FirstOrDefault()?.Entry?.String ?? "") == Expand(beat.journal ?? "", t) ? "yes" : "no", "yes");
                    bool hooked = q.VirtualMachineAdapter?.Aliases?
                        .Any(fa => fa.Property.Alias == slot.activatorAlias
                                   && fa.Scripts.Any(s => s.Name == "DefaultAliasOnActivate"
                                                          && s.Properties.OfType<IScriptIntPropertyGetter>()
                                                              .Any(p => p.Name == "StageToSet" && p.Data == slot.journalStage))) ?? false;
                    fail += Check($"{tag} hook sets stage {slot.journalStage}", hooked ? "yes" : "no", "yes");
                }
                else
                {
                    var ob = q.Objectives?.FirstOrDefault(o => o.Index == slot.objective);
                    fail += Check($"{tag} objective rewritten",
                                  (ob?.DisplayText?.String ?? "") == Expand(beat.objective ?? "", t) ? "yes" : "no", "yes");
                }
            }
            fail += Check("name rewritten", (q.Name?.String ?? "") == Expand(r.prose.name!, t) ? "yes" : "no", "yes");

            if (t.kind == "delve4")
            {
                // The driver swap, every property, every objective's TARGET and every journal. The
                // off-disk read is the point: these are the fields a half-applied write would get
                // wrong while the in-memory objects looked perfect.
                var scripts = q.VirtualMachineAdapter?.Scripts ?? new List<IScriptEntryGetter>();
                fail += Check($"old driver {t.replacesDriver} gone",
                              scripts.Any(s => string.Equals(s.Name, t.replacesDriver, StringComparison.OrdinalIgnoreCase)) ? "present" : "gone", "gone");
                var drv = scripts.FirstOrDefault(s => s.Name == t.driver);
                fail += Check($"driver {t.driver} attached", drv != null ? "yes" : "no", "yes");
                foreach (var kv in made4.Props)
                {
                    var p = drv?.Properties.OfType<IScriptObjectPropertyGetter>().FirstOrDefault(x => x.Name == kv.Key);
                    string got = p == null ? "missing" : p.Object.FormKey + (kv.Value.alias >= 0 ? " alias " + p.Alias : "");
                    string want = kv.Value.obj + (kv.Value.alias >= 0 ? " alias " + kv.Value.alias : "");
                    fail += Check($"property {kv.Key}", got, want);
                }
                foreach (var kv in made4.IntProps)
                {
                    var p = drv?.Properties.OfType<IScriptIntPropertyGetter>().FirstOrDefault(x => x.Name == kv.Key);
                    fail += Check($"property {kv.Key}", p?.Data.ToString() ?? "missing", kv.Value.ToString());
                }
                for (int i = 0; i < plan.Count; i++)
                {
                    var (slot, beat, _) = plan[i];
                    var ob = q.Objectives?.FirstOrDefault(o => o.Index == slot.objective);
                    int wantAlias = slot.ObjectiveTarget;
                    fail += Check($"objective {slot.objective} targets alias {wantAlias}",
                                  (ob?.Targets?.FirstOrDefault()?.AliasID)?.ToString() ?? "none", wantAlias.ToString());
                    if (beat.journal != null)
                    {
                        var st = q.Stages?.FirstOrDefault(s => s.Index == slot.journalStage);
                        fail += Check($"beat {i + 1} journal on stage {slot.journalStage}",
                                      (st?.LogEntries?.FirstOrDefault()?.Entry?.String ?? "") == Expand(beat.journal, t) ? "yes" : "no", "yes");
                    }
                }
                var mod = StarfieldMod.CreateFromBinaryOverlay(modFile, StarfieldRelease.Starfield, readParams);
                foreach (var (label, key, name) in new[] { ("load", made4.LoadItem, r.items!.load!), ("missing", made4.MissingItem, r.items.missing!) })
                {
                    var mi = mod.MiscItems.FirstOrDefault(m => m.FormKey == key);
                    fail += Check($"item {label} {key} named", mi?.Name?.String ?? "missing", name);
                }
            }

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
