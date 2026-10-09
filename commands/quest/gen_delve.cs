using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Aspects;
using Mutagen.Bethesda.Plugins.Records;
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
    ///   gen_delve lint  <recipe.json | id>      grade a recipe without writing anything
    ///   gen_delve lint  --all                   grade every recipe, one load-order index, one line each
    ///   gen_delve build <recipe.json | id> [--dry]   lint, then clone + rewire + verify OFF DISK
    ///   gen_delve keywords [filter ...]         theme tally inside a narrowed POI pool
    ///
    /// ⭐ THE FORMAT, EVERY FIELD AND EVERY RULE: docs/quests/delve-recipes.md. Three template kinds:
    /// dualactivator (two-beat-one-place), delve4 (carry-absence-recover-return), choice
    /// (find-owner-or-buyer, which CREATES a third place).
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
    public static partial class gen_delve
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
            /// <summary>
            /// The base's OTHER place alias, which a beat may name with "place": "second" to happen at a
            /// different POI. -1 = this template has none. His ruling 2026-09-24 on duo_delve03: the load
            /// "should have been at a different POI and then I take it to that one".
            /// </summary>
            public int secondPlaceAlias { get; set; } = -1;
            /// <summary>
            /// choice: a THIRD place the base does not have, CREATED by cloning one of its location
            /// aliases. Null = this template has none. His ruling 2026-10-08, "third place, time to
            /// learn": the medal's owner and buyer are two different peopled sites.
            /// </summary>
            public ThirdPlace? thirdPlace { get; set; }
            /// <summary>choice: the stage the offer's journal line lands on, between the find and the endings.</summary>
            public int offerStage { get; set; } = -1;
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
        /// <summary>
        /// A created place. cloneOf names the base location alias it is copied from, so it inherits
        /// that alias's flags and its GetDistance leash (the one condition type this tool preserves
        /// rather than constructs); its marker and theme conditions are then written from the recipe
        /// like any other place.
        /// </summary>
        private sealed class ThirdPlace { public int cloneOf { get; set; } = -1; public string name { get; set; } = ""; }
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
            /// <summary>choice: the surprise between the find and the endings (journal + optional box).</summary>
            public Offer? offer { get; set; }
            /// <summary>
            /// choice: Overtime's reward TIER per ending ("easy", "med", "hard"), written onto that ending's
            /// completing stage as the duo_reward_creds_TIER / duo_reward_xp_TIER globals. His ladder.
            /// </summary>
            public Reward? reward { get; set; }
            /// <summary>choice, OPTIONAL: who stands at each ending. Absent = nobody is placed.</summary>
            public People? people { get; set; }
            /// <summary>
            /// beats, REQUIRED when the last step is a group (and refused otherwise): the completing stage's
            /// journal line. The group's last member sets its own stage and then the completing stage in the
            /// same instant, and the quest log shows only the newest line, so without this the player reads
            /// the BASE's recap. Ending on a single beat, that beat's journal already IS the completing line.
            /// A new optional field, so schema stays 1. His play of duo_delve07, 2026-10-08.
            /// </summary>
            public string? recap { get; set; }
        }
        private sealed class People { public Person? owner { get; set; } public Person? buyer { get; set; } }
        /// <summary>A named NPC cloned from a vanilla template by EditorID (NPCTools' friendly set is the menu).</summary>
        private sealed class Person
        {
            public string? name { get; set; }
            public string? template { get; set; }
            /// <summary>OPTIONAL: an Outfit EditorID written as the NPC's DefaultOutfit. Absent = the template's own.</summary>
            public string? outfit { get; set; }
            /// <summary>OPTIONAL: nameless friendly NPCs placed around this person. Absent = they stand alone.</summary>
            public Company? company { get; set; }
        }
        /// <summary>A FormList of actor bases (Overtime's duo_GangMembersList_Civ_* are the friendly ones) and a count range.</summary>
        private sealed class Company { public string? list { get; set; } public int min { get; set; } public int max { get; set; } }
        private sealed class Offer { public string? journal { get; set; } public BeatMessage? message { get; set; } }
        private sealed class Reward { public string? owner { get; set; } public string? buyer { get; set; } }
        /// <summary>delve4: inventory names for the two halves. Both are CLONED items, never the base's.</summary>
        private sealed class Items
        {
            public string? load { get; set; }
            public string? missing { get; set; }
            /// <summary>
            /// OPTIONAL: the NIF the beat-1 crate wears. Absent = the base activator's own model. His eye,
            /// 2026-09-24: the base's is a BOSS container, so the load read as end-of-dungeon loot.
            /// </summary>
            public string? crateModel { get; set; }
            /// <summary>
            /// OPTIONAL: the NIF the delivery point (beat 2's centre activator) wears. Absent = the base's.
            /// His ask, 2026-09-24: "change the delivery point to like a machine? it's the water plant thing".
            /// </summary>
            public string? centreModel { get; set; }
            /// <summary>OPTIONAL: the delivery point's display name, i.e. its activate prompt. Absent = the base's.</summary>
            public string? centreName { get; set; }
            /// <summary>
            /// choice, REQUIRED there: the buyer's delivery point, a second clone of the base activator. Required
            /// because the buyer's slot is cloned from the owner's, so without its own model and name the buyer
            /// would wear the owner's prompt.
            /// </summary>
            public string? buyerModel { get; set; }
            public string? buyerName { get; set; }
        }
        private sealed class Place
        {
            public Theme theme { get; set; } = new();
            public string? leash { get; set; }
            /// <summary>The second POI's own theme, for beats with place "second". Absent = no theme.</summary>
            public Place? second { get; set; }
            /// <summary>choice: the third POI's own theme, for beats with place "third". Absent = no theme.</summary>
            public Place? third { get; set; }
            /// <summary>
            /// delve4: place civilians at the centre on the first approach. Default true. Set false when the
            /// theme already guarantees people (LocTypeOE_NonHostile), or the site gets a crowd.
            /// </summary>
            public bool civilians { get; set; } = true;
        }
        private sealed class Theme { public List<string> require { get; set; } = new(); public List<string> exclude { get; set; } = new(); }
        private sealed class Prose { public string? name { get; set; } public string? briefing { get; set; } }
        private sealed class Beat
        {
            public string at { get; set; } = "";
            public string? objective { get; set; }
            public string? journal { get; set; }
            /// <summary>"main" (default), "second" or "third": which drawn POI this beat happens at.</summary>
            public string? place { get; set; }
            /// <summary>0 main, 1 second, 2 third; -1 for a name that is none of them (the lint refuses it).</summary>
            public int PlaceIndex => place == null || place.Equals("main", StringComparison.OrdinalIgnoreCase) ? 0
                                   : place.Equals("second", StringComparison.OrdinalIgnoreCase) ? 1
                                   : place.Equals("third", StringComparison.OrdinalIgnoreCase) ? 2 : -1;
            public bool Second => PlaceIndex == 1;
            /// <summary>
            /// delve4, OPTIONAL: a pausing message box shown when this beat fires. Absent = no box.
            /// A new optional field, so schema stays 1 and every recipe written before it builds unchanged.
            /// </summary>
            public BeatMessage? message { get; set; }
            /// <summary>beats: "use", "pickup" or "deliver". Absent on every other kind.</summary>
            public string? type { get; set; }
            /// <summary>beats: consecutive use beats sharing a group name are done in ANY order, with one counted objective.</summary>
            public string? group { get; set; }
            /// <summary>beats, pickup only: the inventory name of what this beat hands the player (a cloned item).</summary>
            public string? item { get; set; }
            /// <summary>beats, OPTIONAL: the NIF this beat's object wears, and its activate prompt. Absent = the base's.</summary>
            public string? model { get; set; }
            public string? name { get; set; }
            /// <summary>
            /// beats, OPTIONAL: the marker is a PLACED OBJECT (REContainerLocRef is the box itself), so disable
            /// it when the quest starts and let this beat's object stand in its place. His way in his own
            /// quests (ccs_missioninfestation01: ContainerRef.Disable(False), never re-enabled). duo_delve08,
            /// 2026-10-09: the locker spawned INSIDE the box. Absent = false.
            /// </summary>
            public bool replace { get; set; }
        }
        private sealed class BeatMessage { public string? title { get; set; } public string? text { get; set; } }

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
                case "bases": return MarkerBases(args.Skip(2).Where(a => !a.StartsWith("--")).ToList());
                case "footprint": return Footprint();
                case "keywords": return PoolKeywords(args.Skip(2).Where(a => !a.StartsWith("--")).ToList());
                case "lint":
                    if (args.Any(a => a.Equals("--all", StringComparison.OrdinalIgnoreCase))) return LintAll(dataDir, templates);
                    return WithRecipe(path, dataDir, templates, (r, t, env) => Grade(r, t, env, null) ? 0 : 1);
                case "build": return WithRecipe(path, dataDir, templates, (r, t, env) => Build(r, t, env, dry));
                default:
                    Console.WriteLine("Usage: gen_delve templates");
                    Console.WriteLine("       gen_delve lint  <recipe.json | recipeId>");
                    Console.WriteLine("       gen_delve lint  --all          every recipe, one game-data load, one line each");
                    Console.WriteLine("       gen_delve build <recipe.json | recipeId> [--dry]");
                    return 1;
            }
        }

        private static int WithRecipe(string? path, string dataDir, List<Template> templates,
                                      Func<Recipe, Template, IGameEnvironment<IStarfieldMod, IStarfieldModGetter>, int> body)
        {
            if (path == null) { Console.WriteLine("REFUSED: name a recipe."); return 1; }
            var recipe = LoadRecipe(path, dataDir, templates, out var t);
            if (recipe == null || t == null) return 1;
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            return body(recipe, t, env);
        }

        /// <summary>
        /// Read one recipe and refuse it before any game data is touched: not JSON, an unknown key, an id that
        /// is not its file name, or a template that does not exist. Prints its own refusal.
        /// </summary>
        private static Recipe? LoadRecipe(string path, string dataDir, List<Template> templates, out Template? t)
        {
            t = null;
            // A bare id is resolved against the recipes folder, so the common case is short and the
            // explicit path still works for a recipe living anywhere else.
            string file = File.Exists(path) ? path : Path.Combine(dataDir, "recipes", path + ".json");
            if (!File.Exists(file)) { Console.WriteLine("REFUSED: no recipe at " + file); return null; }

            Recipe? recipe;
            string text = File.ReadAllText(file);
            try { recipe = JsonSerializer.Deserialize<Recipe>(text, JsonOpts); }
            catch (JsonException ex) { Console.WriteLine("REFUSED: " + Path.GetFileName(file) + " is not valid JSON -- " + ex.Message); return null; }
            if (recipe == null) { Console.WriteLine("REFUSED: " + file + " parsed to nothing."); return null; }
            // ⛔ A KEY THE TOOL DOES NOT KNOW IS A TYPO UNTIL PROVEN OTHERWISE. The reader ignores unknown
            // fields, so "outift" linted clean and built an undressed NPC. A default's harm is the error it
            // suppresses, and the cure is refusing unknown keys (his ask, 2026-10-08: "a linter for the json
            // that validates the outfits/companies etc"). Keys starting with "_" are the authors' notes.
            var unknown = new List<string>();
            using (var doc = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }))
                UnknownKeys(doc.RootElement, typeof(Recipe), "", unknown);
            if (unknown.Count > 0)
            {
                Console.WriteLine("REFUSED: " + Path.GetFileName(file) + " has key(s) this tool does not know, so they would be IGNORED:");
                foreach (var u in unknown) Console.WriteLine("  " + u);
                Console.WriteLine("  (a key starting with _ is a note and is always allowed)");
                return null;
            }
            // ⛔ THE ID IS THE QUEST'S EDITORID AND A BUILD REPLACES ANY QUEST OF THAT NAME. Two recipes sharing
            // an id would silently overwrite each other's mission, which is the first thing a batch of recipes
            // written quickly will do. So the id must be the file's own name: two files cannot share one.
            string stem = Path.GetFileNameWithoutExtension(file);
            if (!string.Equals(recipe.id, stem, StringComparison.Ordinal))
            { Console.WriteLine($"REFUSED: {Path.GetFileName(file)} has id '{recipe.id}'; a recipe's id must be its file name ('{stem}'), so no two can collide."); return null; }
            if (!System.Text.RegularExpressions.Regex.IsMatch(recipe.id, "^duo_delve[0-9a-z_]+$"))
            { Console.WriteLine($"REFUSED: id '{recipe.id}' must look like duo_delveNN (lower case, digits, underscores): it becomes an EditorID and every clone's prefix."); return null; }
            Console.WriteLine("  recipe: " + file);

            t = templates.FirstOrDefault(x => string.Equals(x.id, recipe.template, StringComparison.OrdinalIgnoreCase));
            if (t == null)
            {
                Console.WriteLine("REFUSED: no template '" + recipe.template + "'. Known: "
                                  + string.Join(", ", templates.Select(x => x.id)));
                return null;
            }
            Console.WriteLine("  template: " + t.id + "  (base " + t.@base + ", driver " + t.driver + ")");
            Console.WriteLine();
            return recipe;
        }

        /// <summary>
        /// EVERY RECIPE, ONE GAME-DATA LOAD, ONE LINE EACH. His ask, 2026-10-08: "I want Jessica to be able to
        /// spam these jsons". A single lint costs a minute of loading the load order; this pays it once. Each
        /// recipe's full lint is captured and only its verdict printed, with the fatal lines under a failure.
        /// Then the checks that only exist ACROSS recipes: two missions sharing a delivery model (his ruling,
        /// 2026-10-08 08:55: the flavour axis is the mission, one centreModel per recipe, distinct across them).
        /// </summary>
        private static int LintAll(string dataDir, List<Template> templates)
        {
            var files = Directory.GetFiles(Path.Combine(dataDir, "recipes"), "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            Console.WriteLine($"  lint --all: {files.Count} recipe(s) in {Path.Combine(dataDir, "recipes")}");
            using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            var real = Console.Out;
            int failed = 0;
            var models = new List<(string recipe, string field, string path)>();
            var names = new List<(string recipe, string field, string name)>();
            int clashes = 0;
            var loaded = new List<Recipe>();
            foreach (var f in files)
            {
                var sw = new StringWriter();
                Console.SetOut(sw);
                bool ok;
                Recipe? r = null;
                try
                {
                    r = LoadRecipe(f, dataDir, templates, out var t);
                    ok = r != null && t != null && Grade(r, t, env, null);
                }
                finally { Console.SetOut(real); }
                string log = sw.ToString();
                var lines = log.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
                string warns = lines.FirstOrDefault(l => l.Contains("LINT PASSES"))?.Trim() ?? "";
                Console.WriteLine($"  {(ok ? "PASS" : "FAIL"),-5} {Path.GetFileNameWithoutExtension(f),-24} {(ok ? warns : "")}");
                if (!ok)
                {
                    failed++;
                    foreach (var l in lines.Where(l => l.Contains("[FATAL]") || l.StartsWith("REFUSED") || l.StartsWith("  ") && log.Contains("REFUSED") && l.Contains("did you mean")))
                        Console.WriteLine("        " + l.Trim());
                }
                if (r?.items != null)
                    foreach (var (field, path) in new[] { ("centreModel", r.items.centreModel), ("buyerModel", r.items.buyerModel) })
                        if (!string.IsNullOrWhiteSpace(path)) models.Add((r.id, field, path!));
                if (r != null) loaded.Add(r);
                if (r != null)
                    foreach (var (field, nm) in new[] { ("items.load", r.items?.load), ("items.missing", r.items?.missing),
                                                        ("people.owner.name", r.people?.owner?.name), ("people.buyer.name", r.people?.buyer?.name) })
                        if (!string.IsNullOrWhiteSpace(nm)) names.Add((r.id, field, nm!));
            }
            Console.WriteLine();
            // Letter siblings may share a model (his ruling, 2026-10-08: "should be allowed the same model, it's
            // fine"), so the count is of mission NUMBERS: 06a and 06b are one mission, 06 and 07 are two.
            static string Mission(string id) => System.Text.RegularExpressions.Regex.Replace(id, "(?<=[0-9])[a-z]$", "");
            foreach (var g in models.GroupBy(m => m.path, StringComparer.OrdinalIgnoreCase).Where(g => g.Select(x => Mission(x.recipe)).Distinct().Count() > 1))
                Console.WriteLine($"  [warn ] {g.Key} is a delivery point in {string.Join(", ", g.Select(x => x.recipe + " (" + x.field + ")"))}: "
                                  + "his ruling is one delivery model per mission, distinct across missions.");
            // ⭐ LETTER SIBLINGS. His convention, 2026-10-08: "01a and 01b would have the same kinda content but
            // with different lore", as FULL recipes (his pick over a variant file). The cost of full copies is
            // drift: a fix made in 06a and forgotten in 06b. So siblings sharing a number are compared on
            // STRUCTURE (everything that is not lore) and any difference is named. A warning, not a refusal:
            // a sibling that differs on purpose is a new mission number, and that call is the author's.
            foreach (var fam in loaded.GroupBy(x => System.Text.RegularExpressions.Regex.Match(x.id, "^(duo_delve[0-9]+)[a-z]$").Groups[1].Value)
                                      .Where(g => g.Key != "" && g.Count() > 1))
            {
                string Shape(Recipe x, string part) => part switch
                {
                    "template" => x.template,
                    "beats" => string.Join(" | ", x.beats.Select(b => $"{b.at}@{b.place ?? "main"}")),
                    "themes" => string.Join(" | ", new[] { x.place.theme, x.place.second?.theme, x.place.third?.theme }
                                    .Select(th => th == null ? "-" : "+" + string.Join(",", th.require.OrderBy(k => k)) + " -" + string.Join(",", th.exclude.OrderBy(k => k)))),
                    "reward" => $"{x.reward?.owner}/{x.reward?.buyer}",
                    _ => ""
                };
                var first = fam.OrderBy(x => x.id).First();
                foreach (var sib in fam.OrderBy(x => x.id).Skip(1))
                    foreach (var part in new[] { "template", "beats", "themes", "reward" })
                        if (Shape(first, part) != Shape(sib, part))
                            Console.WriteLine($"  [warn ] {sib.id} and {first.id} are letter siblings with different {part}: "
                                              + $"'{Shape(sib, part)}' vs '{Shape(first, part)}'. Same number, same content: fix both, or give it its own number.");
            }

            // The item-name check inside each lint sees only what is already BUILT into the plugin, so two new
            // recipes naming the same thing both pass until one is built. Across the batch it is a failure.
            foreach (var g in names.GroupBy(n => n.name, StringComparer.OrdinalIgnoreCase).Where(g => g.Select(x => x.recipe).Distinct().Count() > 1))
            {
                clashes++;
                Console.WriteLine($"  [FATAL] \"{g.Key}\" is used by {string.Join(", ", g.Select(x => x.recipe + " (" + x.field + ")"))}: two things with one name in a player's inventory or world.");
            }
            Console.WriteLine(failed == 0 && clashes == 0 ? $"  ALL {files.Count} RECIPES LINT CLEAN."
                              : $"  {failed} of {files.Count} RECIPE(S) FAIL" + (clashes > 0 ? $", and {clashes} name clash(es) ACROSS recipes." : "."));
            return failed == 0 && clashes == 0 ? 0 : 1;
        }

        /// <summary>
        /// Walk a recipe's JSON against the model it deserialises into and name every key the model does
        /// not have, with its path. Recurses into object properties and list elements. Names match
        /// case-insensitively, as the deserialiser does.
        /// </summary>
        private static void UnknownKeys(JsonElement e, Type t, string path, List<string> outp)
        {
            if (e.ValueKind == JsonValueKind.Array)
            {
                var elem = t.IsGenericType ? t.GetGenericArguments()[0] : null;
                int i = 0;
                if (elem != null) foreach (var item in e.EnumerateArray()) UnknownKeys(item, elem, $"{path}[{i++}]", outp);
                return;
            }
            if (e.ValueKind != JsonValueKind.Object || t == typeof(string)) return;
            var props = t.GetProperties().ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in e.EnumerateObject())
            {
                if (kv.Name.StartsWith("_")) continue;
                string here = path == "" ? kv.Name : path + "." + kv.Name;
                if (!props.TryGetValue(kv.Name, out var prop))
                {
                    var near = props.Keys.Where(k => Close(k, kv.Name)).ToList();
                    outp.Add($"{here}" + (near.Count > 0 ? $"   (did you mean {string.Join(" / ", near)}?)" : "") + $"   known here: {string.Join(", ", props.Keys.Where(k => char.IsLower(k[0])))}");
                    continue;
                }
                var pt = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                UnknownKeys(kv.Value, pt, here, outp);
            }
        }

        /// <summary>Two names one edit apart, or one a transposition of the other: the shape of a typo.</summary>
        private static bool Close(string a, string b)
        {
            a = a.ToLowerInvariant(); b = b.ToLowerInvariant();
            if (a == b || Math.Abs(a.Length - b.Length) > 1) return false;
            if (a.Length == b.Length)
            {
                var diff = Enumerable.Range(0, a.Length).Where(i => a[i] != b[i]).ToList();
                return diff.Count == 1 || (diff.Count == 2 && diff[1] == diff[0] + 1 && a[diff[0]] == b[diff[1]] && a[diff[1]] == b[diff[0]]);
            }
            var (l, sh) = a.Length > b.Length ? (a, b) : (b, a);
            for (int i = 0; i < l.Length; i++) if (l.Remove(i, 1) == sh) return true;
            return false;
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
            bool choice = t.kind == "choice";
            bool beats = t.kind == "beats";
            if (beats) GradeBeats(r, t, env, Fatal, Warn);
            else if (r.beats.Any(b => b.type != null || b.group != null || b.item != null || b.model != null || b.name != null || b.replace))
                Fatal($"a beat sets type, group, item, model, name or replace, and template '{t.id}' ({t.kind}) is not a beats Delve, so they would be silently ignored.");
            if (!beats && r.recap != null)
                Fatal($"the recipe has a recap, and template '{t.id}' ({t.kind}) is not a beats Delve, so it would be silently ignored.");
            if (delve4)
            {
                if (string.IsNullOrWhiteSpace(r.items?.load) || string.IsNullOrWhiteSpace(r.items?.missing))
                    Fatal("items.load and items.missing are both required: each half is a cloned item the player carries, and an item with no name shows as a blank line in the inventory.");
            }
            if (choice)
            {
                // ⭐ THE SHAPE IS THE DESIGN: found at one place, owned at a second, wanted at a third.
                // Each place is a different POI by construction (and the pool check below proves the two
                // endings cannot coincide), so the choice is made by where the player walks.
                if (r.beats.Count == 3)
                {
                    var want = new[] { 1, 0, 2 };
                    var role = new[] { "the find", "the owner", "the buyer" };
                    for (int i = 0; i < 3; i++)
                        if (r.beats[i].PlaceIndex != want[i])
                            Fatal($"beat {i + 1} is {role[i]} and must be at the {PlaceLabel(want[i]).ToLowerInvariant()} place; it names '{r.beats[i].place ?? "main"}'.");
                }
                if (string.IsNullOrWhiteSpace(r.items?.load))
                    Fatal("items.load is required: it is the thing the player carries to whichever ending, and an item with no name shows as a blank line.");
                if (!string.IsNullOrWhiteSpace(r.items?.missing))
                    Fatal("items.missing is set; a choice Delve carries ONE thing, so it would never be used.");
                foreach (var (f, v) in new[] { ("centreModel", r.items?.centreModel), ("centreName", r.items?.centreName),
                                                ("buyerModel", r.items?.buyerModel), ("buyerName", r.items?.buyerName) })
                    if (string.IsNullOrWhiteSpace(v))
                        Fatal($"items.{f} is required on a choice Delve: the buyer's delivery point is cloned from the owner's, "
                              + "so each needs its own model and name or one wears the other's prompt.");
                if (string.IsNullOrWhiteSpace(r.offer?.journal))
                    Fatal("offer.journal is required: the offer is the beat that makes it a choice, and it must reach the journal.");
                foreach (var (who, pp) in new[] { ("owner", r.people?.owner), ("buyer", r.people?.buyer) })
                {
                    if (pp == null) { Warn($"people.{who} is absent, so nobody stands at that ending and a box that talks about a person talks about nobody."); continue; }
                    if (string.IsNullOrWhiteSpace(pp.name) || string.IsNullOrWhiteSpace(pp.template))
                    { Fatal($"people.{who} needs both a name and a template."); continue; }
                    if (Tokens(pp.name).Any()) Fatal($"people.{who}.name carries a <Token>; an NPC name is not an alias context.");
                    var tn = OwnOrMaster<INpcGetter>(Allowed(env, t), env, pp.template);
                    if (tn == null) Fatal($"people.{who}.template '{pp.template}' is not an NPC in {t.mod} or one of its masters.");
                    else Console.WriteLine($"  people.{who}: \"{pp.name}\" from {tn.EditorID} [{tn.FormKey}]");
                    if (!string.IsNullOrWhiteSpace(pp.outfit))
                    {
                        var of = OwnOrMaster<IOutfitGetter>(Allowed(env, t), env, pp.outfit);
                        if (of == null) Fatal($"people.{who}.outfit '{pp.outfit}' is not an Outfit in {t.mod} or one of its masters.");
                        else Console.WriteLine($"  people.{who}.outfit: {of.EditorID} [{of.FormKey}]");
                    }
                    if (pp.company != null)
                    {
                        var fl = OwnOrMaster<IFormListGetter>(Allowed(env, t), env, pp.company.list ?? "");
                        if (fl == null) Fatal($"people.{who}.company.list '{pp.company.list}' is not a FormList in {t.mod} or one of its masters.");
                        else if (fl.Items.Count == 0) Fatal($"people.{who}.company.list '{pp.company.list}' is EMPTY, so nobody would ever be placed.");
                        else Console.WriteLine($"  people.{who}.company: {pp.company.min}-{pp.company.max} from {fl.EditorID} ({fl.Items.Count} entries)");
                        if (pp.company.min < 0 || pp.company.max < pp.company.min)
                            Fatal($"people.{who}.company needs 0 <= min <= max; it has {pp.company.min}-{pp.company.max}.");
                        else if (pp.company.max > 8)
                            Warn($"people.{who}.company.max is {pp.company.max}; more than 8 at one delivery point is a crowd.");
                    }
                }
                // THE PAY IS ON THE STAGE, never in the driver. Each ending's completing stage names a credits
                // global and an xp global (QRCR / QRXP), and Overtime ships a ladder of them. The driver paid
                // on top of the inherited stage reward once, and the buyer's ending paid twice (his first play).
                var tierCredits = new Dictionary<string, float>();
                foreach (var (who, tier) in new[] { ("owner", r.reward?.owner), ("buyer", r.reward?.buyer) })
                {
                    if (string.IsNullOrWhiteSpace(tier)) { Fatal($"reward.{who} is required: a tier, e.g. easy / med / hard."); continue; }
                    foreach (var kind in new[] { "creds", "xp" })
                    {
                        var g = env.LoadOrder.PriorityOrder.WinningOverrides<IGlobalGetter>()
                            .FirstOrDefault(x => string.Equals(x.EditorID, $"duo_reward_{kind}_{tier}", StringComparison.OrdinalIgnoreCase));
                        if (g == null) { Fatal($"reward.{who} '{tier}': no global duo_reward_{kind}_{tier} in the load order."); continue; }
                        float v = (float)(g.Data ?? 0f);
                        Console.WriteLine($"  reward.{who}: {g.EditorID} = {v:G}");
                        if (kind == "creds") tierCredits[who] = v;
                    }
                }
                if (tierCredits.Count == 2 && tierCredits["buyer"] <= tierCredits["owner"])
                    Warn($"the buyer's tier pays {tierCredits["buyer"]:G} and the owner's {tierCredits["owner"]:G}; the design has the buyer paying more.");
            }
            if (delve4 || choice)
            {
                // Our driver displays every beat's objective, so every beat is driven, and the beat
                // count is the state machine's shape rather than a ceiling.
                if (r.beats.Count != t.beatSlots.Count)
                    Fatal($"template '{t.id}' is exactly {t.beatSlots.Count} beats; the recipe has {r.beats.Count}.");
                if (string.IsNullOrWhiteSpace(t.replacesDriver)) Fatal($"template '{t.id}' names no replacesDriver");
                var itemNames = new[] { r.items?.load, r.items?.missing }.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).ToList();
                if (itemNames.SelectMany(Tokens).Any())
                    Fatal("an item name carries a <Token>. Item names are not alias contexts, so it would print literally in the inventory.");
                else if (itemNames.Count > 0) ItemNameCollisions(env, r, itemNames, Fatal);
                foreach (var (f, v) in new[] { ("centreName", r.items?.centreName), ("buyerName", r.items?.buyerName) })
                    if (v != null && Tokens(v).Any())
                        Fatal($"items.{f} carries a <Token>. An activator name is not an alias context, so it would print literally.");
                if (r.items?.centreName != null && string.IsNullOrEmpty(r.items.centreModel))
                    Warn("items.centreName is set with no centreModel; a name only goes onto the clone, so it is not written.");
                foreach (var (field, path) in new[] { ("crateModel", r.items?.crateModel), ("centreModel", r.items?.centreModel),
                                                      ("buyerModel", r.items?.buyerModel) })
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    int uses = 0;
                    string? stands = null;
                    // ⭐ RULE 1 OF EVERY DELVE (2026-10-08, the tripod dish 2 m underground): read a mesh's lowest z
                    // before it becomes a placed activator, graded off a STATIC that owns the mesh (the index does it).
                    foreach (var u in IndexOf(env).ByModel.GetValueOrDefault(path!) ?? new List<(string? edid, string? stands)>())
                    {
                        if ((u.edid ?? "").StartsWith(r.id + "_", StringComparison.OrdinalIgnoreCase)) continue; // our own last build
                        uses++;
                        stands ??= u.stands;
                    }
                    Console.WriteLine($"  {field}: {path} is used by {uses} record(s) in the load order"
                                      + (stands != null ? $"; {stands}" : ""));
                    if (stands != null && !stands.StartsWith("STANDS"))
                        Warn($"items.{field} {stands}: a placed activator stands its origin on the marker, so it will not sit on the ground.");
                    else if (stands == null && uses > 0)
                        Warn($"items.{field}: no Static owns this mesh, so whether it stands cannot be read off the records; look at it (gen_inspect bounds, or his NifSkope).");
                    if (uses == 0)
                    {
                        // "Used by a record" was standing in for "the file exists", and the two part
                        // company exactly when the model is NEW (2026-10-08: the office's first kitbash,
                        // relaydish01, refused). So ask the real question: is the FILE there, loose in
                        // Data or inside this mod's own archives? Nowhere still fails, unchanged.
                        string where = FindAsset(env.DataFolderPath.Path, t.mod, path!);
                        if (where == "")
                            Fatal($"items.{field} '{path}' is used by no record in the load order AND is not in Data "
                                  + $"loose or in any '{t.mod} - *.ba2'. A path that resolves to nothing builds clean "
                                  + "and renders nothing; use one the game already ships.");
                        else if (where == "loose")
                            Warn($"items.{field} '{path}' is a NEW asset (no record uses it) and exists LOOSE ONLY. "
                                 + "It renders on this machine and on NO player's: pack it into the mod's archives before a release.");
                        else
                            Warn($"items.{field} '{path}' is a NEW asset (no record uses it), found in {where}.");
                    }
                }
                var boxes = r.beats.Select((b, i) => ($"beat {i + 1}", b.message)).ToList();
                if (r.offer?.message != null) boxes.Add(("the offer", r.offer.message));
                foreach (var (who, m) in boxes)
                {
                    if (m == null) continue;
                    if (string.IsNullOrWhiteSpace(m.text)) Fatal($"{who} has a message with no text: an empty pausing box.");
                    if (string.IsNullOrWhiteSpace(m.title)) Fatal($"{who} has a message with no title.");
                    foreach (var (what, str) in new[] { ("title", m.title ?? ""), ("text", m.text ?? "") })
                        if (str.Any(ch => ch > 126 || (ch < 32 && ch != '\n')))
                            Warn($"{who} message {what} carries a non-ASCII or control character; the lane writes plain ASCII.");
                }
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
            for (int i = 0; i < r.beats.Count && !beats; i++)
            {
                bool driven = delve4 || choice || i == 0 || i == r.beats.Count - 1;
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
            int extras = beats ? 0 : Math.Max(0, r.beats.Count - t.beatSlots.Count);
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
            // ⛔ A FIELD THE TEMPLATE NEVER READS IS SILENTLY DROPPED, which reads to an author exactly like it
            // worked. Each kind names what it writes; anything else set on the recipe is refused.
            var unused = new List<string>();
            if (!choice)
            {
                if (r.offer != null) unused.Add("offer");
                if (r.reward != null) unused.Add("reward");
                if (r.people != null) unused.Add("people");
                if (r.place.third != null && !beats) unused.Add("place.third");
                if (r.items?.buyerModel != null) unused.Add("items.buyerModel");
                if (r.items?.buyerName != null) unused.Add("items.buyerName");
            }
            if (!delve4 && !choice)
            {
                if (r.items != null) unused.Add("items");
                if (!beats && r.beats.Any(b => b.message != null)) unused.Add("beats[].message");
            }
            foreach (var u in unused)
                Fatal($"{u} is set, and template '{t.id}' ({t.kind}) never reads it, so it would be silently ignored.");

            // ⛔ THE QUEST LOG SHOWS ONLY THE NEWEST STAGE'S LINE (his play, 2026-10-08: "I think the family
            // delivery one is skipped"). On a choice, beat 1's stage and the offer's are set in one instant.
            if (choice && !string.IsNullOrWhiteSpace(r.beats.FirstOrDefault()?.journal))
                Warn("beat 1's journal is NEVER SHOWN on this template: its stage and the offer's are set in the same instant and "
                     + "the log shows only the newest line. Put what the player must read in offer.journal.");

            // --- prose, against the style guide (part 33) and the house rule -------------------------------
            foreach (var (where, text) in ProseStrings(r))
            {
                if (where.Contains("message")) continue;   // the message boxes have their own check above
                if (text.Contains('\u2014')) Warn($"{where} carries an em dash; the house rule is none, and vanilla's quest text has zero.");
                else if (text.Any(ch => ch > 126 || (ch < 32 && ch != '\n')))
                    Warn($"{where} carries a non-ASCII or control character; the lane writes plain ASCII.");
            }
            foreach (var (b, i) in r.beats.Select((b, i) => (b, i)))
            {
                var o = b.objective;
                if (string.IsNullOrWhiteSpace(o)) continue;
                int words = o.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                if (words > 12) Warn($"beat {i + 1} objective is {words} words; vanilla objectives run a median of 4 and never past 12.");
                if (".!?".Contains(o.TrimEnd()[^1])) Warn($"beat {i + 1} objective ends in punctuation; vanilla objectives never do.");
            }

            // --- two beats on top of each other ------------------------------------------------------------
            // Two beats on one marker in one place happen in one spot; two on the same travel RING (A1/A2/A3 or
            // B1/B2/B3) sit 7 to 14 units apart (part 32). A declared return is the one legitimate repeat.
            for (int i = 0; i < r.beats.Count; i++)
                for (int j = i + 1; j < r.beats.Count; j++)
                {
                    var (a, b) = (r.beats[i], r.beats[j]);
                    if (a.PlaceIndex != b.PlaceIndex) continue;
                    bool ret = j < t.beatSlots.Count && t.beatSlots[j].returnTo == i;
                    if (string.Equals(a.at, b.at, StringComparison.OrdinalIgnoreCase))
                    { if (!ret) Warn($"beats {i + 1} and {j + 1} are both on {a.at} at the same place: one spot, two beats."); }
                    else if (a.at.Length > 9 && b.at.Length > 9 && a.at.StartsWith("RETravel") && b.at.StartsWith("RETravel")
                             && a.at[8] == b.at[8])
                        Warn($"beats {i + 1} and {j + 1} are on the same travel ring ({a.at}, {b.at}): they land 7 to 14 m apart.");
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

            // --- which place each beat is at --------------------------------------------------
            bool anySecond = r.beats.Any(b => b.Second);
            bool anyThird = r.beats.Any(b => b.PlaceIndex == 2);
            for (int i = 0; i < r.beats.Count; i++)
                if (r.beats[i].PlaceIndex < 0)
                    Fatal($"beat {i + 1} names place '{r.beats[i].place}'; the places are \"main\", \"second\" and \"third\"");
            if (anySecond && t.secondPlaceAlias < 0)
                Fatal($"a beat is at the \"second\" place and template '{t.id}' has no second place alias");
            if (anyThird && t.thirdPlace == null)
                Fatal($"a beat is at the \"third\" place and template '{t.id}' creates no third place");
            if (!anyThird && r.place.third != null)
                Warn("place.third is set and no beat is at the third place, so it is never written");
            if (anySecond && t.kind == "delve4" && r.beats.Skip(1).Any(b => b.Second))
                Fatal("only beat 1 may be at the second place on a delve4: beats 2-4 are the centre and the carrier, "
                      + "and the driver's whole return is to ONE centre");
            if (!anySecond && r.place.second != null)
                Warn("place.second is set and no beat is at the second place, so it is never written");

            // --- the pool, PER PLACE, which is the number the design sits on ---------------------
            // ⭐ Each place alias is drawn on its OWN conditions, so each gets its own pool figure: the
            // markers of the beats that happen there, the map marker, and that place's theme.
            if (!issues.Any(i => i.Fatal))
            {
                var pool = PoolCensus(env);
                Console.WriteLine($"  POI corpus: {pool.Count} location(s) in the working pool");
                var drawn = new Dictionary<int, HashSet<string>>();
                foreach (int pi in PlacesUsed(r))
                {
                    var here = MarkersAt(r, keys, pi);
                    var theme = ThemeOf(r, pi);
                    Console.WriteLine($"  -- the {PlaceLabel(pi)} place (alias "
                                      + (pi == 2 ? $"CREATED at build, a clone of {t.thirdPlace?.cloneOf}" : PlaceAliasOf(t, pi, -1).ToString()) + ")");
                    drawn[pi] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    int all = 0, mapOnly = 0;
                    var perMarker = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var m in here.Keys) perMarker[m] = 0;
                    var reqK = ResolveKeywords(env, theme.require, Fatal);
                    var excK = ResolveKeywords(env, theme.exclude, Fatal);
                    foreach (var poi in pool)
                    {
                        foreach (var m in here) if (poi.RefTypes.Contains(m.Value)) perMarker[m.Key]++;
                        if (!here.Values.All(k => poi.RefTypes.Contains(k))) continue;
                        mapOnly++;
                        if (reqK.Any(k => !poi.Keywords.Contains(k))) continue;
                        if (excK.Any(k => poi.Keywords.Contains(k))) continue;
                        all++;
                        drawn[pi].Add(poi.Edid);
                    }
                    foreach (var kv in perMarker)
                        Console.WriteLine($"    {kv.Key,-34} {kv.Value,4} of {pool.Count}  ({100.0 * kv.Value / Math.Max(1, pool.Count):F1}%)");
                    Console.WriteLine($"    all markers together               {mapOnly,4}");
                    Console.WriteLine($"    after the theme predicate          {all,4}   <- the draw pool");
                    string which = $"the {PlaceLabel(pi).ToLowerInvariant()} place";
                    if (all == 0) Fatal($"{which}'s draw pool is ZERO. Nothing would ever select this mission.");
                    else if (all < 40) Warn($"{which}'s draw pool is {all}. Narrow -- the narrowest anything in Overtime leans on is 44.");
                }

                // ⛔ TWO ENDINGS MUST BE TWO PLACES. Whether the engine de-duplicates two location aliases
                // of one quest is NOT established (part 32: eight shipped cargo quests draw pickup and
                // delivery from an identical pool, untested). The shipped separation mechanism is the
                // THEME PREDICATE, disjoint by construction, so the choice's two delivery pools must not
                // share a single POI. Measured here, not trusted to the author's keywords.
                if (t.kind == "choice" && drawn.ContainsKey(0) && drawn.ContainsKey(2))
                {
                    var both = drawn[0].Intersect(drawn[2], StringComparer.OrdinalIgnoreCase).ToList();
                    Console.WriteLine($"  owner (main) and buyer (third) pools overlap on {both.Count} POI(s)");
                    if (both.Count > 0)
                        Fatal($"the owner's and the buyer's places can draw the SAME POI ({both.Count}: {string.Join(", ", both.Take(5))}). "
                              + "Make their themes disjoint: one requires a keyword the other excludes.");
                }

                // A beats Delve has no ending to keep apart, but two of its places drawing ONE POI would put
                // two legs of a trail on the same site. Whether the engine de-duplicates location aliases is
                // not established (part 32), so the overlap is measured and named rather than trusted.
                if (beats)
                    foreach (var (a, b) in new[] { (0, 1), (0, 2), (1, 2) })
                        if (drawn.ContainsKey(a) && drawn.ContainsKey(b))
                        {
                            int both = drawn[a].Intersect(drawn[b], StringComparer.OrdinalIgnoreCase).Count();
                            if (both > 0) Warn($"the {PlaceLabel(a).ToLowerInvariant()} and {PlaceLabel(b).ToLowerInvariant()} places can draw the same POI ({both} in common); give one a theme the other excludes if they must differ.");
                        }

                // --- what the build will DROP from the base --------------------------------------------
                // ⛔ WHY THIS EXISTS: the build rebuilds every keyword condition from the recipe, which is
                // the design, and for a day it silently threw away the base's own "not Natural, not Cave"
                // exclusions, so the Delves drew exactly the places his quest refused. His eye found it
                // ("It's mainly caves and natural locations atm"). The drop stays; it is never silent.
                var baseQ = env.LoadOrder.PriorityOrder.WinningOverrides<IQuestGetter>().FirstOrDefault(q => q.EditorID == t.@base);
                if (baseQ?.Aliases != null)
                    foreach (int pi in PlacesUsed(r))
                    {
                        // The third place is a clone, so what it would inherit and drop is its source's.
                        int aid = pi == 2 ? (t.thirdPlace?.cloneOf ?? -1) : PlaceAliasOf(t, pi, -1);
                        var theme = ThemeOf(r, pi);
                        var la = baseQ.Aliases.OfType<IQuestLocationAliasGetter>().FirstOrDefault(a => a.ID == (uint)aid);
                        foreach (var c in la?.Conditions ?? Enumerable.Empty<IConditionGetter>())
                        {
                            if (c.Data is not ILocationHasKeywordConditionDataGetter kd) continue;
                            var kfk = kd.FirstParameter.Link.FormKey;
                            string kn = env.LinkCache.TryResolve<IKeywordGetter>(kfk, out var kw) ? (kw.EditorID ?? kfk.ToString()) : kfk.ToString();
                            bool excluded = c is IConditionFloatGetter cf && cf.ComparisonValue == 0f;
                            var list = excluded ? theme.exclude : theme.require;
                            if (list.Contains(kn, StringComparer.OrdinalIgnoreCase)) continue;
                            Warn($"the base's alias {aid} {(excluded ? "EXCLUDES" : "REQUIRES")} {kn} and this recipe does not; "
                                 + "the build DROPS it. Put it in the recipe's theme if the mission should keep it.");
                        }
                    }
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
                Console.WriteLine("  separation, METRES (1 unit = 1 m, read off his HUD 2026-09-24)");
                var want = new List<(string, string, string)>
                {
                    ("THIS RECIPE", r.beats[0].at, r.beats[1].at),
                    ("site radius", "RECenterLocRef", "RETravelA1LocRef"),
                    ("site diameter (the ceiling)", "RETravelA1LocRef", "RETravelB1LocRef"),
                };
                if (r.beats[0].PlaceIndex != r.beats[1].PlaceIndex)
                {
                    // Two POIs: beats 1 and 2 are not in one site, so a marker-to-marker distance between
                    // them is not a thing. Their separation is the second place's leash (see the warn).
                    Console.WriteLine("    THIS RECIPE: beats 1 and 2 are at DIFFERENT POIs; no in-site separation applies.");
                    want.RemoveAt(0);
                }
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

        /// <summary>The places a recipe uses, by index: main always, then any a beat names.</summary>
        private static List<int> PlacesUsed(Recipe r)
            => new[] { 0 }.Concat(r.beats.Select(b => b.PlaceIndex).Where(i => i > 0)).Distinct().OrderBy(i => i).ToList();

        private static Theme ThemeOf(Recipe r, int place)
            => place == 0 ? r.place.theme : (place == 1 ? r.place.second : r.place.third)?.theme ?? new Theme();

        private static string PlaceLabel(int place) => place == 0 ? "MAIN" : place == 1 ? "SECOND" : "THIRD";

        /// <summary>
        /// The location alias a place is drawn on. The third place does not exist on the base: it is
        /// created at build time, so its id is handed in (-1 at lint time, where nothing is built yet).
        /// </summary>
        private static int PlaceAliasOf(Template t, int place, int thirdId)
            => place == 0 ? t.placeAlias : place == 1 ? t.secondPlaceAlias : thirdId;

        private static Dictionary<string, FormKey> MarkersAt(Recipe r, Dictionary<string, FormKey> all, int place)
        {
            // The markers one place must carry: its own beats' markers, plus the map marker every
            // shipped target place carries (the player has to be able to navigate there).
            var outp = new Dictionary<string, FormKey>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in r.beats.Where(b => b.PlaceIndex == place))
                if (all.TryGetValue(b.at, out var k)) outp[b.at] = k;
            if (all.TryGetValue("MapMarkerRefType", out var mm)) outp["MapMarkerRefType"] = mm;
            return outp;
        }

        /// <summary>
        /// AN ITEM NAME MUST NOT BE ONE THE LOAD ORDER ALREADY USES. An exact match on any named record
        /// is fatal (two different things with one name in an inventory); a record whose name merely
        /// CONTAINS the item's first word is listed, not judged, because whether it collides in the
        /// fiction is the author's call. Prints how many names it read, so a blind read shows as one.
        /// </summary>
        private static void ItemNameCollisions(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, Recipe r,
                                               List<string> names, Action<string> fatal)
        {
            var heads = names.Select(n => n.Split(' ')[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            int read = 0;
            var near = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (nm, edid, fk) in IndexOf(env).Named)
            {
                read++;
                if (edid != null && edid.StartsWith(r.id + "_", StringComparison.OrdinalIgnoreCase)) continue; // our own last build
                foreach (var n in names)
                    if (string.Equals(nm, n, StringComparison.OrdinalIgnoreCase))
                        fatal($"item name \"{n}\" is already the name of {edid} [{fk}]");
                foreach (var h in heads)
                    if (nm.Contains(h, StringComparison.OrdinalIgnoreCase)) near.Add($"{nm}  ({edid})");
            }
            Console.WriteLine($"  item names: {read:N0} named record(s) read; containing {string.Join(" / ", heads)}: {near.Count}");
            foreach (var n in near.Take(25)) Console.WriteLine("    " + n);
            if (read == 0) fatal("the item-name check read ZERO named records, so it checked nothing");
        }

        private static IEnumerable<(string, string)> ProseStrings(Recipe r)
        {
            yield return ("prose.name", r.prose.name ?? "");
            yield return ("prose.briefing", r.prose.briefing ?? "");
            for (int i = 0; i < r.beats.Count; i++)
            {
                yield return ($"beat {i + 1} objective", r.beats[i].objective ?? "");
                if (r.beats[i].journal != null) yield return ($"beat {i + 1} journal", r.beats[i].journal!);
                if (r.beats[i].message?.title != null) yield return ($"beat {i + 1} message title", r.beats[i].message!.title!);
                if (r.beats[i].message?.text != null) yield return ($"beat {i + 1} message text", r.beats[i].message!.text!);
            }
            if (r.offer?.journal != null) yield return ("offer journal", r.offer.journal);
            if (r.offer?.message?.title != null) yield return ("offer message title", r.offer.message.title);
            if (r.offer?.message?.text != null) yield return ("offer message text", r.offer.message.text);
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
            // ⭐ THE UNITS ARE METRES, read 2026-09-24: duo_worldprobe put a MapMarker_LongRange at
            // 196 in the same frame his HUD read 196M. The first version of this table printed a
            // metre column at ~70 units/m, the Skyrim/Fallout constant carried over on no evidence,
            // and called a whole POI one metre across. That column was deleted; this label is the
            // fact that replaced it, from an instrument beside a known quantity.
            //
            // ⭐ HIS FACT, 2026-09-23, and it is what makes this table readable: THE TRAVEL MARKERS
            // ARE THE EDGES OF THE POI. So an edge-to-edge pair is the site's DIAMETER and an
            // edge-to-centre pair is its radius. The largest number here is the most separation a
            // single-POI Delve can ever have, by construction.
            Console.WriteLine("  distance between beats, METRES (1 unit = 1 m, read off his HUD 2026-09-24)");
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

        /// <summary>
        /// WHICH BASE OBJECT EACH MARKER TYPE IS ACTUALLY PLACED AS, across the POI pool.
        ///
        /// ⛔ WHY THIS EXISTS: a runtime query (FindAllReferencesOfType) matches on the BASE, and the
        /// marker TYPE's name does not tell you the base. OEJM001Location's A1 travel marker is placed
        /// as REOverlayTravelA2. The first world probe was built on names, found zero markers standing
        /// beside a POI, and could not say whether that was the world or the list. So the list is
        /// DERIVED from placements, never typed from a naming convention.
        ///
        ///   gen_delve bases [RETravelA1LocRef RECenterLocRef ...]   (default: the six travel + centre)
        /// </summary>
        private static int MarkerBases(List<string> typeNames)
        {
            if (typeNames.Count == 0)
                typeNames = new() { "RETravelA1LocRef", "RETravelA2LocRef", "RETravelA3LocRef",
                                    "RETravelB1LocRef", "RETravelB2LocRef", "RETravelB3LocRef", "RECenterLocRef" };
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();

            var lcrt = env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>()
                .Where(x => x.EditorID != null)
                .GroupBy(x => x.EditorID!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().FormKey, StringComparer.OrdinalIgnoreCase);
            var want = new Dictionary<FormKey, string>();
            foreach (var n in typeNames)
            {
                if (!lcrt.TryGetValue(n, out var k)) { Console.WriteLine("REFUSED: '" + n + "' is not a LocationReferenceType."); return 1; }
                want[k] = n;
            }
            var pool = PoolCensus(env).Select(p => p.Edid).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var tally = new Dictionary<string, Dictionary<FormKey, int>>();
            foreach (var n in typeNames) tally[n] = new();
            int refs = 0, unresolved = 0;
            foreach (var loc in env.LoadOrder.PriorityOrder.WinningOverrides<ILocationGetter>())
            {
                if (!pool.Contains(loc.EditorID ?? "")) continue;
                foreach (var g in new[] { loc.MasterSpecialReferences, loc.AddedSpecialReferences })
                {
                    if (g == null) continue;
                    foreach (var e in g)
                    {
                        if (e.LocationRefType.IsNull || e.Marker.IsNull || !want.TryGetValue(e.LocationRefType.FormKey, out var tn)) continue;
                        refs++;
                        if (!env.LinkCache.TryResolve<IPlacedObjectGetter>(e.Marker.FormKey, out var po) || po.Base.IsNull) { unresolved++; continue; }
                        var t = tally[tn];
                        t[po.Base.FormKey] = t.GetValueOrDefault(po.Base.FormKey) + 1;
                    }
                }
            }

            var name = new Dictionary<FormKey, string>();
            foreach (var kv in tally) foreach (var b in kv.Value.Keys)
                if (!name.ContainsKey(b))
                    name[b] = env.LinkCache.TryResolve<IStarfieldMajorRecordGetter>(b, out var r) ? (r.EditorID ?? b.ToString()) : b + " (unresolved)";

            Console.WriteLine();
            Console.WriteLine($"  POI pool: {pool.Count}   marker refs of the named types: {refs}   would not resolve to a placed ref: {unresolved}");
            var every = new Dictionary<FormKey, int>();
            foreach (var kv in tally)
            {
                Console.WriteLine();
                Console.WriteLine($"  {kv.Key}  ({kv.Value.Values.Sum()} refs, {kv.Value.Count} distinct base(s))");
                foreach (var b in kv.Value.OrderByDescending(x => x.Value))
                {
                    Console.WriteLine($"    {b.Value,5}  {name[b.Key],-40} {b.Key}");
                    every[b.Key] = every.GetValueOrDefault(b.Key) + b.Value;
                }
            }
            Console.WriteLine();
            Console.WriteLine("  EVERY base, for a runtime query list (FormID in its plugin):");
            foreach (var b in every.OrderByDescending(x => x.Value))
                Console.WriteLine($"    0x{b.Key.ID:X6}  {b.Key.ModKey.FileName,-22} {name[b.Key],-40} {b.Value,5}");
            if (unresolved > 0)
                Console.WriteLine($"\n  ⚠ {unresolved} marker ref(s) did not resolve and are NOT in the tally above: the link cache reads exterior refs and not interior ones. Counted, not guessed.");
            return 0;
        }

        /// <summary>
        /// THE GRID EACH POOL POI WAS AUTHORED IN, in cells, off the records. NOT the runtime footprint:
        /// the first run of this was read as a footprint and it is not one. It is the authoring
        /// worldspace's cell set, and "does the generator stamp a 5x4 authored grid as a 5x4" is untested.
        ///
        /// ⚠ A BOUNDING BOX IS NOT A SHAPE. OESF007 is 10 cells in a 22x11 box. So a rectangle is
        /// reported with its FILLED count, and only a box with every cell filled is a real rectangle.
        /// Measured 2026-09-24: 226 of 278 are full squares; 8 are full rectangles, 6 of them
        /// OverlayTrait* planet-trait overlays and 2 ordinary OE POIs (OESF005 5x4, OEBB001 6x5).
        ///
        /// ⭐ HIS FACT, 2026-09-24, from building generated POIs: a POI is N x N cells and the engine did
        /// not allow rectangles. The census bears it out as the rule and finds two ordinary exceptions. It matters because a runtime query sees a POI cell by cell (the world probe found
        /// the near travel ring and not the far one), and because a crate meant to read as LOST has to
        /// land outside the whole square, not just past one marker.
        ///
        /// Each vanilla POI is authored in its own small worldspace; the centre marker's cell names it.
        /// Its exterior cells' grid gives N, and the travel markers' coordinates against that grid give
        /// the cell size. His square rule is CHECKED here rather than assumed: a rectangle is counted.
        /// </summary>
        private static int Footprint()
        {
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            var sf = env.LoadOrder.ListedOrder
                .FirstOrDefault(l => l.ModKey.FileName.String.Equals("Starfield.esm", StringComparison.OrdinalIgnoreCase))?.Mod;
            if (sf == null) { Console.WriteLine("REFUSED: Starfield.esm is not in the load order."); return 1; }

            // cell -> (worldspace, grid), and worldspace -> every exterior cell grid it holds
            var cellWs = new Dictionary<FormKey, (FormKey ws, int x, int y)>();
            var wsGrids = new Dictionary<FormKey, List<(int x, int y)>>();
            var wsName = new Dictionary<FormKey, string>();
            foreach (var ws in sf.Worldspaces)
            {
                var grids = new List<(int, int)>();
                foreach (var b in ws.SubCells)
                    foreach (var sb in b.Items)
                        foreach (var c in sb.Items)
                        {
                            if (c.Grid == null) continue;
                            var g = (c.Grid.Point.X, c.Grid.Point.Y);
                            cellWs[c.FormKey] = (ws.FormKey, g.X, g.Y);
                            grids.Add(g);
                        }
                wsGrids[ws.FormKey] = grids;
                wsName[ws.FormKey] = ws.EditorID ?? ws.FormKey.ToString();
            }

            var lcrt = env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>()
                .Where(x => x.EditorID != null)
                .ToDictionary(x => x.FormKey, x => x.EditorID!);
            var pool = PoolCensus(env).Where(p => p.Vanilla).Select(p => p.Edid).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var sizes = new Dictionary<string, int>();
            int sqOe = 0, rectOe = 0, sqFull = 0;
            var oeKw = env.LoadOrder.PriorityOrder.WinningOverrides<IKeywordGetter>()
                .FirstOrDefault(x => string.Equals(x.EditorID, "LocTypeOE_Keyword", StringComparison.OrdinalIgnoreCase))?.FormKey;
            int squares = 0, rects = 0, noWorld = 0;
            var rectNames = new List<string>();
            float maxAbs = 0; int maxAbsN = 0;
            var perN = new Dictionary<int, (float maxCoord, int n)>();
            foreach (var loc in sf.Locations)
            {
                if (!pool.Contains(loc.EditorID ?? "")) continue;
                FormKey? ws = null;
                var markerKeys = new List<FormKey>();
                foreach (var g in new[] { loc.MasterSpecialReferences, loc.AddedSpecialReferences })
                {
                    if (g == null) continue;
                    foreach (var e in g)
                    {
                        // ⛔ The special reference's "Location" is xEdit's World/Cell: for an exterior
                        // marker it names the WORLDSPACE, not a cell. The first run looked it up in a
                        // cell table and matched 0 of 278 (his screenshot showed the field's type).
                        if (e.Location.IsNull) continue;
                        FormKey? here = wsGrids.ContainsKey(e.Location.FormKey) ? e.Location.FormKey
                                      : cellWs.TryGetValue(e.Location.FormKey, out var cw) ? cw.ws : (FormKey?)null;
                        if (here == null) continue;
                        ws ??= here;
                        if (!e.Marker.IsNull && lcrt.TryGetValue(e.LocationRefType.FormKey, out var n) && n.StartsWith("RETravel"))
                            markerKeys.Add(e.Marker.FormKey);
                    }
                }
                if (ws == null || wsGrids[ws.Value].Count == 0) { noWorld++; continue; }
                var gs = wsGrids[ws.Value];
                int w = gs.Max(p => p.x) - gs.Min(p => p.x) + 1, h = gs.Max(p => p.y) - gs.Min(p => p.y) + 1;
                string key = w + "x" + h;
                sizes[key] = sizes.GetValueOrDefault(key) + 1;
                bool oe = oeKw != null && (loc.Keywords?.Any(k => k.FormKey == oeKw.Value) ?? false);
                if (w == h) { squares++; if (oe) sqOe++; if (gs.Distinct().Count() == w * h) sqFull++; }
                else { rects++; if (oe) rectOe++; rectNames.Add($"{key,-7} {(oe ? "OE " : "-- ")} cells {gs.Distinct().Count(),5}/{w * h,-6} {wsName[ws.Value],-44} {loc.EditorID}"); }

                // the travel markers' reach against the grid, so the cell size falls out
                float reach = 0;
                foreach (var k in markerKeys)
                    if (TryPos(env, k, out var p)) reach = Math.Max(reach, Math.Max(Math.Abs(p.X), Math.Abs(p.Y)));
                var cur = perN.GetValueOrDefault(w);
                perN[w] = (Math.Max(cur.maxCoord, reach), cur.n + 1);
            }

            Console.WriteLine();
            Console.WriteLine($"  vanilla pool POIs: {pool.Count}   placed in a worldspace this read could name: {pool.Count - noWorld}");
            Console.WriteLine($"  square: {squares} ({sqOe} carry LocTypeOE_Keyword, {sqFull} have EVERY cell of the box)   NOT square: {rects} ({rectOe} carry it)");
            foreach (var kv in sizes.OrderByDescending(k => k.Value))
                Console.WriteLine($"    {kv.Key,-7} {kv.Value,4}");
            foreach (var r in rectNames) Console.WriteLine("    not square: " + r);
            Console.WriteLine();
            Console.WriteLine("  furthest travel marker from the worldspace origin, per N (metres), for the cell size:");
            foreach (var kv in perN.OrderBy(k => k.Key))
                Console.WriteLine($"    N={kv.Key}  {kv.Value.n,4} POIs   max |x|,|y| = {kv.Value.maxCoord,7:F1}");
            return 0;
        }

        /// <summary>
        /// EVERY KEYWORD THE POOL'S LOCATIONS CARRY, with counts. What a recipe's theme can condition on
        /// is exactly this list and nothing else, so it is read rather than guessed.
        /// ⛔ WHY: I called "not Natural, not Cave, not Abandoned" a PEOPLED filter without looking for a
        /// keyword that means people. His question, 2026-09-24: "what peopled did you check?"
        /// </summary>
        /// <summary>
        /// Keyword tally over the POI pool, optionally NARROWED first: each argument is a keyword the
        /// POI must carry or a ref type it must carry (resolved as either; a name that is neither
        /// refuses). Added 2026-10-08 for the three-place Delve, whose two delivery sites must both be
        /// LocTypeOE_NonHostile and must draw from DISJOINT pools, so the question is which theme
        /// splits the peopled pool, not which themes the whole pool has.
        /// </summary>
        private static int PoolKeywords(List<string> narrow)
        {
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            var pool = PoolCensus(env);
            foreach (var n in narrow)
            {
                var kw = env.LoadOrder.PriorityOrder.WinningOverrides<IKeywordGetter>()
                    .FirstOrDefault(x => string.Equals(x.EditorID, n, StringComparison.OrdinalIgnoreCase));
                var rt = env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>()
                    .FirstOrDefault(x => string.Equals(x.EditorID, n, StringComparison.OrdinalIgnoreCase));
                if (kw == null && rt == null) { Console.WriteLine($"REFUSED: '{n}' is neither a keyword nor a ref type"); return 1; }
                int before = pool.Count;
                pool = pool.Where(p => (kw != null && p.Keywords.Contains(kw.FormKey)) || (rt != null && p.RefTypes.Contains(rt.FormKey))).ToList();
                Console.WriteLine($"  narrowed by {n} ({(kw != null ? "keyword" : "ref type")}): {before} -> {pool.Count}");
            }
            var tally = new Dictionary<FormKey, int>();
            foreach (var p in pool) foreach (var k in p.Keywords) tally[k] = tally.GetValueOrDefault(k) + 1;
            Console.WriteLine($"\n  POI pool: {pool.Count} location(s); {tally.Count} distinct keyword(s)\n");
            foreach (var kv in tally.OrderByDescending(k => k.Value))
            {
                string n = env.LinkCache.TryResolve<IKeywordGetter>(kv.Key, out var kw) ? (kw.EditorID ?? "?") : "(unresolved)";
                Console.WriteLine($"    {kv.Value,4}  {n,-50} {kv.Key}");
            }
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
        /// <summary>
        /// ONE PASS OVER THE LOAD ORDER, KEPT FOR THE LIFE OF THE ENVIRONMENT. lint --all took six minutes for
        /// four recipes because every recipe re-walked ~1.08M records for the item names and once per model
        /// field, and re-ran the location census. Same answers, read once. Keyed on the environment object,
        /// so a different load order is a different index.
        /// </summary>
        private sealed class LoadIndex
        {
            public object Env = null!;
            public List<(string name, string? edid, FormKey fk)> Named = new();
            public Dictionary<string, List<(string? edid, string? stands)>> ByModel = new(StringComparer.OrdinalIgnoreCase);
            public List<Poi>? Pool;
        }
        private static LoadIndex? _index;

        private static LoadIndex IndexOf(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env)
        {
            if (_index != null && ReferenceEquals(_index.Env, env)) return _index;
            var ix = new LoadIndex { Env = env };
            foreach (var rec in env.LoadOrder.PriorityOrder.WinningOverrides<IMajorRecordGetter>())
            {
                if (rec is INamedGetter ng && !string.IsNullOrEmpty(ng.Name)) ix.Named.Add((ng.Name!, rec.EditorID, rec.FormKey));
                if (rec is IModeledGetter mg && mg.Model?.File != null)
                {
                    string path = mg.Model.File.DataRelativePath.Path;
                    string? stands = rec is IStaticGetter sg && sg.ObjectBounds != null
                        ? gen_inspect.BoundsVerdict(sg.ObjectBounds.First.Z, sg.ObjectBounds.Second.Z) + $" ({sg.EditorID})" : null;
                    if (!ix.ByModel.TryGetValue(path, out var list)) ix.ByModel[path] = list = new();
                    // a Static's verdict first, so the first one read is the one a reader can trust
                    if (stands != null) list.Insert(0, (rec.EditorID, stands)); else list.Add((rec.EditorID, null));
                }
            }
            return _index = ix;
        }

        private static List<Poi> PoolCensus(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env)
            => IndexOf(env).Pool ??= PoolCensusUncached(env);

        private static List<Poi> PoolCensusUncached(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env)
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

        /// <summary>
        /// Where a data-relative asset path actually lives: the name of the first "{mod} - *.ba2" whose
        /// BTDX name table lists it, else "loose" if the file is in Data, else "" (nowhere). Archives
        /// are checked FIRST because loose-only is the state that ships broken.
        /// </summary>
        private static string FindAsset(string dataDir, string mod, string path)
        {
            string want = path.Replace('\\', '/').ToLowerInvariant();
            foreach (var ba2 in Directory.GetFiles(dataDir, mod + " - *.ba2"))
            {
                using var f = File.OpenRead(ba2);
                using var br = new BinaryReader(f);
                if (new string(br.ReadChars(4)) != "BTDX") continue;
                f.Seek(12, SeekOrigin.Begin);
                uint count = br.ReadUInt32();
                long nameTable = (long)br.ReadUInt64();
                f.Seek(nameTable, SeekOrigin.Begin);
                for (uint i = 0; i < count; i++)
                {
                    int len = br.ReadUInt16();
                    string name = System.Text.Encoding.ASCII.GetString(br.ReadBytes(len));
                    if (name.Replace('\\', '/').ToLowerInvariant() == want) return Path.GetFileName(ba2);
                }
            }
            return File.Exists(Path.Combine(dataDir, path)) ? "loose" : "";
        }

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


            var myMod = StarfieldMod.CreateFromBinary(modFile, StarfieldRelease.Starfield, readParams);
            gen_quest_main.FixNextFormId(myMod);
            var mastersBefore = string.Join(", ", myMod.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));

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
            var made4 = new BuildMade();
            var madeBeats = new BeatsMade();
            if (r.beats.Any(b => b.PlaceIndex == 2))
            {
                made4.ThirdPlaceAlias = CreateThirdPlace(clone, t);
                if (made4.ThirdPlaceAlias < 0) fail++;
            }
            foreach (int pi in PlacesUsed(r))
            {
                if (fail > 0) break;
                var theme = ThemeOf(r, pi);
                fail += WritePlaceConditions(clone, PlaceAliasOf(t, pi, made4.ThirdPlaceAlias), MarkersAt(r, markers, pi),
                                             ResolveKeywords(env, theme.require, _ => { }),
                                             ResolveKeywords(env, theme.exclude, _ => { }));
            }

            var plan = new List<(BeatSlot slot, Beat beat, bool created)>();
            if (t.kind == "delve4")
            {
                if (fail == 0) fail += BuildDelve4(myMod, clone, t, r, markers, plan, made4);
            }
            else if (t.kind == "beats")
            {
                if (fail == 0) fail += BuildBeats(myMod, clone, t, r, markers, made4, madeBeats);
            }
            else if (t.kind == "choice")
            {
                if (fail == 0)
                {
                    // ⛔ OWN-OR-MASTER, NEVER "WHATEVER WINS". du_overtime.esp (the CK bridge) carries a copy of
                    // every duo_ record under its own ModKey and loads later, so a winning-override lookup by
                    // EditorID found duo_GangMembersList_Civ_LIST in the ESP, the write linked to it, and
                    // Mutagen ADDED du_overtime.esp TO THE ESM'S MASTERS (2026-10-08, caught before he played).
                    var templates = new Dictionary<string, INpcGetter>();
                    var outfits = new Dictionary<string, FormKey>();
                    var companies = new Dictionary<string, FormKey>();
                    foreach (var (who, pp) in new[] { ("owner", r.people?.owner), ("buyer", r.people?.buyer) })
                    {
                        if (pp?.template != null)
                        {
                            var n = OwnOrMaster<INpcGetter>(myMod, env, pp.template);
                            if (n == null) { Console.WriteLine($"REFUSED: NPC '{pp.template}' is not in {t.mod} or one of its masters."); fail++; }
                            else templates[who] = n;
                        }
                        if (!string.IsNullOrWhiteSpace(pp?.outfit))
                        {
                            var o = OwnOrMaster<IOutfitGetter>(myMod, env, pp!.outfit!);
                            if (o == null) { Console.WriteLine($"REFUSED: Outfit '{pp.outfit}' is not in {t.mod} or one of its masters."); fail++; }
                            else outfits[who] = o.FormKey;
                        }
                        if (pp?.company != null)
                        {
                            var f = OwnOrMaster<IFormListGetter>(myMod, env, pp.company.list!);
                            if (f == null) { Console.WriteLine($"REFUSED: FormList '{pp.company.list}' is not in {t.mod} or one of its masters."); fail++; }
                            else companies[who] = f.FormKey;
                        }
                    }
                    if (fail == 0) fail += BuildChoice(myMod, clone, t, r, markers, plan, made4, templates, outfits, companies);
                }
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
                    fail += WriteBeat(clone, t, plan[i].slot, plan[i].beat, markers, plan[i].created, PlaceAliasOf(t, plan[i].beat.PlaceIndex, -1));

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
            SortStages(clone);
            fail += OrderAliases(clone);
            if (fail > 0) { Console.WriteLine("\n=== " + fail + " problem(s). NOTHING WRITTEN. ==="); return 1; }

            if (dry) { Console.WriteLine("\n  --dry: nothing written."); return 0; }

            foreach (var rec in myMod.EnumerateMajorRecords()) rec.IsCompressed = false;
            env.Dispose();   // release the memory-mapped load order before overwriting one of its files
            myMod.WriteToBinary(modFile, gen_quest_main.BuildWriteParams());
            Console.WriteLine("\n  wrote " + modFile + " (" + new FileInfo(modFile).Length.ToString("N0") + " B)");

            return t.kind == "beats" ? VerifyBeats(modFile, readParams, r, t, madeBeats, mastersBefore)
                                     : Verify(modFile, readParams, t, r, markers, plan, made4, mastersBefore);
        }

        // ------------------------------------------------------------------ delve4

        /// <summary>What a build created (delve4 or choice), handed to Verify rather than re-derived there.</summary>
        private sealed class BuildMade
        {
            /// <summary>The created third place's alias id, -1 when the recipe has none.</summary>
            public int ThirdPlaceAlias = -1;
            /// <summary>choice: the buyer's created marker and activator aliases.</summary>
            public uint BuyerMarkerAlias, BuyerAlias;
            public FormKey OfferMessage;
            /// <summary>choice: "owner"/"buyer" to the NPC placed at that ending.</summary>
            public Dictionary<string, FormKey> People = new();
            /// <summary>choice: "owner"/"buyer" to the Outfit written as that person's DefaultOutfit.</summary>
            public Dictionary<string, FormKey> Outfits = new();
            /// <summary>choice: stage index to the (credits, xp) globals its reward entry was pointed at.</summary>
            public Dictionary<int, (FormKey creds, FormKey xp)> StageReward = new();
            public uint CarrierMarkerAlias, CarrierAlias;
            public FormKey LoadItem, MissingItem;
            public Dictionary<string, (FormKey obj, short alias)> Props = new();
            public Dictionary<string, int> IntProps = new();
            public Dictionary<string, bool> BoolProps = new();
            /// <summary>Beat index (0-based) to the message box built for it.</summary>
            public Dictionary<int, FormKey> Messages = new();
        }

        /// <summary>
        /// GIVE ONE CREATED ACTIVATOR A DIFFERENT MESH, ON A CLONE. The base's activator is shared by his
        /// shipped cargo quests, so it is never edited: it is duplicated as {edid}, Model.File is written
        /// and NOTHING ELSE (gen_setmodel's rule: dropping LightLayer once made thirteen parts invisible),
        /// LightLayer and Flags are asserted unchanged, and the alias is pointed at the clone. Absent path
        /// = nothing happens and the base's model stands. Any previous clone of this name is removed first.
        /// </summary>
        private static int ReskinActivator(StarfieldMod myMod, Quest clone, Template t, string edid, int aliasId, string? path, string? name)
        {
            foreach (var k in myMod.Activators.Where(a => a.EditorID == edid).Select(a => a.FormKey).ToList())
                myMod.Activators.Remove(k);
            if (string.IsNullOrWhiteSpace(path)) return 0;
            var alias = clone.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)aliasId);
            var key = alias?.CreateReferenceToObject?.Object.FormKey;
            var src = key == null ? null : myMod.Activators.FirstOrDefault(a => a.FormKey == key.Value);
            if (src?.Model == null)
            { Console.WriteLine($"REFUSED: alias {aliasId} does not create an activator of {t.mod} with a model to clone."); return 1; }
            var act = myMod.Activators.DuplicateInAsNewRecord(src);
            act.EditorID = edid;
            var ll = act.Model!.LightLayer; var fl = act.Model.Flags;
            act.Model.File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<
                Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>(path);
            if (act.Model.LightLayer != ll || act.Model.Flags != fl)
            { Console.WriteLine("REFUSED: writing Model.File disturbed LightLayer or Flags."); return 1; }
            if (!string.IsNullOrWhiteSpace(name)) act.Name = name;
            alias!.CreateReferenceToObject!.Object.SetTo(act.FormKey);
            Console.WriteLine($"  +reskin  : {edid} {act.FormKey}  model {path}  (clone of {src.EditorID}, alias {aliasId})");
            return 0;
        }

        /// <summary>
        /// The pausing box every Delve message is cloned from: a MessageBox Overtime already ships
        /// (patch 04, 2026-07-31), so its flags are ones the game demonstrably shows.
        /// </summary>
        private const string MessageTemplate = "duo_alocal01_msg03";

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
                                       List<(BeatSlot slot, Beat beat, bool created)> plan, BuildMade made)
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
            var civs = ObjOf("TargetCivListMembers");
            var gangMax = IntOf("MaxGangMembers");
            if (cargo == null || gang == null || failMsg == null || gangMax == null || civs == null)
            { Console.WriteLine("REFUSED: the replaced driver lacks CargoObject, GangMembers, FailMessage, MaxGangMembers or TargetCivListMembers."); return 1; }

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

            // --- the crate's model, on a CLONE of the base activator ------------------------------------
            // The base's activator is shared by his shipped cargo quests, so it is never edited. Model.File
            // is written and NOTHING ELSE (gen_setmodel's rule: dropping LightLayer once made thirteen parts
            // invisible), and LightLayer + Flags are asserted unchanged.
            if (ReskinActivator(myMod, clone, t, r.id + "_crate", t.beatSlots[0].activatorAlias, r.items.crateModel, null) != 0) return 1;
            if (ReskinActivator(myMod, clone, t, r.id + "_centre", t.beatSlots[1].activatorAlias, r.items.centreModel, r.items.centreName) != 0) return 1;
            made.MissingItem = CloneItem("OtherHalf", r.items.missing!);

            // --- message boxes, one per beat that declares one ------------------------------------------
            // CLONED from a pausing box Overtime already ships, never constructed (a fresh record brings
            // none of the fields this tool does not know exist: the stage lesson of 2026-09-24).
            // OwnerQuest IS THE QUEST: vanilla, 1,152 MESG, 68 carry an <Alias=> token and ALL 68 have
            // OwnerQuest set; none has a token without one. A token needs an owner to resolve against.
            foreach (var k in myMod.Messages.Where(m => m.EditorID != null && m.EditorID.StartsWith(r.id + "_msg")).Select(m => m.FormKey).ToList())
                myMod.Messages.Remove(k);
            if (r.beats.Any(b => b.message != null))
            {
                var msgSrc = myMod.Messages.FirstOrDefault(m => m.EditorID == MessageTemplate);
                if (msgSrc == null) { Console.WriteLine($"REFUSED: no message template '{MessageTemplate}' in {t.mod}."); return 1; }
                for (int i = 0; i < r.beats.Count; i++)
                {
                    var bm = r.beats[i].message;
                    if (bm == null) continue;
                    var msg = myMod.Messages.DuplicateInAsNewRecord(msgSrc);
                    msg.EditorID = $"{r.id}_msg{i + 1}";
                    msg.Name = Expand(bm.title!, t);
                    msg.Description = Expand(bm.text!, t);
                    msg.OwnerQuest.SetTo(clone.FormKey);
                    made.Messages[i] = msg.FormKey;
                    Console.WriteLine($"  +message : beat {i + 1} {msg.EditorID} {msg.FormKey}  \"{msg.Name}\"  (clone of {MessageTemplate})");
                }
            }

            // --- stages the base does not have --------------------------------------------------------
            foreach (var s in t.beatSlots.Select(s => s.journalStage).Distinct())
            {
                if (clone.Stages!.Any(x => x.Index == s)) continue;
                var st = CloneStage(clone, s);
                if (st == null) return 1;
                clone.Stages.Add(st);
                Console.WriteLine($"  +stage   : {s} (cloned from a base stage)");
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
                int f = WriteBeat(clone, t, slots[i], r.beats[i], markers, false, PlaceAliasOf(t, r.beats[i].PlaceIndex, -1));
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
            void Bool(string n, bool v)
            {
                sc.Properties.Add(new ScriptBoolProperty { Name = n, Data = v, Flags = ScriptProperty.Flag.Edited });
                made.BoolProps[n] = v;
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
            Obj("CivilianList", civs.Value);
            // The lose-on-approach move is for a load at the MAIN place. At a second POI the other POI
            // is the story, and moving it out past the delivery site's edge would undo the journey.
            Bool("LoseLoadOnApproach", !r.beats[0].Second);
            Bool("CiviliansAtCentre", r.place.civilians);
            foreach (var kv in made.Messages) Obj($"Beat{kv.Key + 1}Message", kv.Value);
            vma.Scripts.Add(sc);
            Console.WriteLine($"  driver   : {t.replacesDriver} REMOVED, {t.driver} in its place with {sc.Properties.Count} properties");
            return 0;
        }

        // ------------------------------------------------------------------ choice

        /// <summary>
        /// A record this plugin may LINK to: one in the plugin itself or in one of its masters, found by
        /// EditorID. Never the load order's winner by name, because a plugin that is not a master (his CK
        /// bridge, du_overtime.esp) can carry a same-named copy, and linking to it makes it a master.
        /// </summary>
        private static T? OwnOrMaster<T>(StarfieldMod myMod, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, string edid)
            where T : class, IMajorRecordGetter
            => OwnOrMaster<T>(new HashSet<ModKey>(myMod.ModHeader.MasterReferences.Select(m => m.Master)) { myMod.ModKey }, env, edid);

        private static T? OwnOrMaster<T>(HashSet<ModKey> allowed, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, string edid)
            where T : class, IMajorRecordGetter
            => env.LoadOrder.PriorityOrder.WinningOverrides<T>()
                .FirstOrDefault(x => allowed.Contains(x.FormKey.ModKey) && string.Equals(x.EditorID, edid, StringComparison.OrdinalIgnoreCase));

        /// <summary>The template's mod and its masters, read off the load order (the lint has no mod object of its own).</summary>
        private static HashSet<ModKey> Allowed(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, Template t)
        {
            var key = ModKey.FromNameAndExtension(t.mod + ".esm");
            var mod = env.LoadOrder.ListedOrder.FirstOrDefault(l => l.ModKey == key)?.Mod;
            var set = new HashSet<ModKey> { key };
            if (mod != null) foreach (var m in mod.ModHeader.MasterReferences) set.Add(m.Master);
            return set;
        }

        /// <summary>
        /// CREATE THE THIRD PLACE by cloning one of the base's location aliases. The first location alias
        /// this tool has ever created (2026-10-08, the medal Delve, his "third place, time to learn").
        ///
        /// ⭐ A CLONE, NEVER A CONSTRUCTION, for the reason every created thing here is a clone: a
        /// QuestLocationAlias carries flags and fields this tool has no opinion about (the source's flags
        /// read 0x40000101 and its ParentSystemLocationAliasID -2, neither of them understood), and copying
        /// one that demonstrably fills brings them along. It also carries the source's GetDistance leash,
        /// which is the one condition WritePlaceConditions preserves, so the two delivery places share a
        /// leash: the closest thing to "both endings are about the same drive" the records can say.
        /// Its marker and theme conditions are then rebuilt from the recipe like any other place's.
        ///
        /// ⚠ What is NOT established, named rather than assumed: that the story manager fills a quest's
        /// location aliases independently, so that two aliases with disjoint pools always land on two
        /// different POIs. The disjointness is measured at lint; the fill is his playthrough.
        /// </summary>
        private static int CreateThirdPlace(Quest clone, Template t)
        {
            var src = clone.Aliases?.OfType<QuestLocationAlias>().FirstOrDefault(a => a.ID == (uint)(t.thirdPlace?.cloneOf ?? -1));
            if (src == null || string.IsNullOrWhiteSpace(t.thirdPlace?.name))
            { Console.WriteLine($"REFUSED: template '{t.id}' names no location alias {t.thirdPlace?.cloneOf} to clone a third place from, or no name for it."); return -1; }
            uint id = 1 + clone.Aliases!.SelectMany(Flatten).Select(x => x.id).DefaultIfEmpty(0u).Max();
            var loc = src.DeepCopy();
            loc.ID = id;
            loc.Name = t.thirdPlace!.name;
            clone.Aliases.Add(loc);
            Console.WriteLine($"  +place   : THIRD place alias {id} \"{loc.Name}\" (clone of location alias {src.ID} \"{src.Name}\", "
                              + $"{loc.Conditions?.Count ?? 0} condition(s) carried before the rebuild)");
            return (int)id;
        }

        /// <summary>
        /// THE CHOICE DELVE: find a thing with a name on it, then deliver it to its owner OR sell it to a
        /// buyer, at two different places; walking to one ends the mission. Jessica's Type 2.
        ///
        /// Same skeleton as BuildDelve4 and the same rules: the base's driver is REPLACED by ours
        /// (FrankyCLI/papyrus/duo_delve_choice.psc), the cargo item is CLONED never edited, every activator
        /// that changes model is a clone, every new stage and box is a clone of one that works.
        ///
        /// ⭐ WHAT IS NEW: the buyer's delivery point is a COPY OF THE OWNER'S SLOT (marker + activator)
        /// moved to the third place, and the buyer's ending is stage 100 cloned to 110, so it carries the
        /// CompleteQuest flag on its log entry exactly as the owner's does. Two completing stages, one
        /// recap each: the branching lives in which stage the driver sets, not in any conditional text.
        /// </summary>
        private static int BuildChoice(StarfieldMod myMod, Quest clone, Template t, Recipe r,
                                       Dictionary<string, FormKey> markers,
                                       List<(BeatSlot slot, Beat beat, bool created)> plan, BuildMade made,
                                       Dictionary<string, INpcGetter> personTemplates,
                                       Dictionary<string, FormKey> outfits, Dictionary<string, FormKey> companies)
        {
            var vma = clone.VirtualMachineAdapter;
            var old = vma?.Scripts.FirstOrDefault(s => string.Equals(s.Name, t.replacesDriver, StringComparison.OrdinalIgnoreCase));
            if (vma == null || old == null)
            { Console.WriteLine($"REFUSED: the base carries no '{t.replacesDriver}' script entry to replace."); return 1; }
            var cargo = old.Properties.OfType<ScriptObjectProperty>()
                .FirstOrDefault(p => string.Equals(p.Name, "CargoObject", StringComparison.OrdinalIgnoreCase))?.Object.FormKey;
            var src = cargo == null ? null : myMod.MiscItems.FirstOrDefault(m => m.FormKey == cargo.Value);
            if (src == null)
            { Console.WriteLine($"REFUSED: the replaced driver's CargoObject is not a MiscItem in {t.mod}; this tool clones only its own mod's items."); return 1; }

            // --- the one thing carried, cloned -------------------------------------------------------
            string itemEdid = r.id + "_Item";
            foreach (var k in myMod.MiscItems.Where(m => m.EditorID == itemEdid).Select(m => m.FormKey).ToList())
                myMod.MiscItems.Remove(k);
            var mi = myMod.MiscItems.DuplicateInAsNewRecord(src);
            mi.EditorID = itemEdid;
            mi.Name = r.items!.load!;
            made.LoadItem = mi.FormKey;
            Console.WriteLine($"  +item    : {itemEdid} {mi.FormKey}  \"{mi.Name}\"  (clone of {src.EditorID})");

            // --- the buyer's slot: the owner's marker + activator, copied, moved to the third place --------
            var ownerSlot = t.beatSlots[1];
            var ownerMarker = clone.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)ownerSlot.markerAlias);
            var ownerAct = clone.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)ownerSlot.activatorAlias);
            if (ownerMarker?.Location == null || ownerAct?.CreateReferenceToObject == null)
            { Console.WriteLine("REFUSED: the template's owner slot is not a marker+activator pair on this base."); return 1; }
            uint next = 1 + clone.Aliases!.SelectMany(Flatten).Select(x => x.id).DefaultIfEmpty(0u).Max();
            var bm = ownerMarker.DeepCopy(); bm.ID = next; bm.Name = "DelveBuyerMarker";
            var ba = ownerAct.DeepCopy(); ba.ID = next + 1; ba.Name = "DelveBuyerTarget";
            ba.CreateReferenceToObject!.AliasID = (short)bm.ID;
            clone.Aliases.Add(bm);
            clone.Aliases.Add(ba);
            made.BuyerMarkerAlias = bm.ID;
            made.BuyerAlias = ba.ID;
            Console.WriteLine($"  +alias   : buyer marker {bm.ID} + activator {ba.ID} (cloned from the owner's {ownerSlot.markerAlias}/{ownerSlot.activatorAlias})");

            // --- three activators, each a clone wearing its own model -------------------------------------
            if (ReskinActivator(myMod, clone, t, r.id + "_crate", t.beatSlots[0].activatorAlias, r.items.crateModel, null) != 0) return 1;
            if (ReskinActivator(myMod, clone, t, r.id + "_centre", ownerSlot.activatorAlias, r.items.centreModel, r.items.centreName) != 0) return 1;
            if (ReskinActivator(myMod, clone, t, r.id + "_buyer", (int)ba.ID, r.items.buyerModel, r.items.buyerName) != 0) return 1;

            // --- message boxes, cloned from a shipped pausing box, owned by the quest ----------------------
            foreach (var k in myMod.Messages.Where(m => m.EditorID != null && m.EditorID.StartsWith(r.id + "_msg")).Select(m => m.FormKey).ToList())
                myMod.Messages.Remove(k);
            var msgSrc = myMod.Messages.FirstOrDefault(m => m.EditorID == MessageTemplate);
            if (msgSrc == null) { Console.WriteLine($"REFUSED: no message template '{MessageTemplate}' in {t.mod}."); return 1; }
            FormKey Box(string suffix, BeatMessage m)
            {
                var msg = myMod.Messages.DuplicateInAsNewRecord(msgSrc);
                msg.EditorID = $"{r.id}_msg{suffix}";
                msg.Name = Expand(m.title!, t);
                msg.Description = Expand(m.text!, t);
                msg.OwnerQuest.SetTo(clone.FormKey);
                Console.WriteLine($"  +message : {msg.EditorID} {msg.FormKey}  \"{msg.Name}\"  (clone of {MessageTemplate})");
                return msg.FormKey;
            }
            for (int i = 0; i < r.beats.Count; i++)
                if (r.beats[i].message != null) made.Messages[i] = Box((i + 1).ToString(), r.beats[i].message!);
            if (r.offer?.message != null) made.OfferMessage = Box("Offer", r.offer.message);

            // --- stages: the offer and the buyer's ending -------------------------------------------------
            if (clone.Stages!.Any(s => s.Index == t.offerStage) || clone.Stages.Any(s => s.Index == t.beatSlots[2].journalStage))
            { Console.WriteLine($"REFUSED: stage {t.offerStage} or {t.beatSlots[2].journalStage} already exists on this base."); return 1; }
            var offerSt = CloneStage(clone, t.offerStage);
            if (offerSt == null) return 1;
            offerSt.LogEntries[0].Entry = Expand(r.offer!.journal!, t);
            clone.Stages.Add(offerSt);
            Console.WriteLine($"  +stage   : {t.offerStage} (the offer, cloned from a plain journal stage)");
            // The buyer's ending is the OWNER'S completing stage copied, so it ends the quest the same way.
            var done = clone.Stages.FirstOrDefault(s => s.Index == ownerSlot.journalStage);
            if (done == null || done.LogEntries.Count != 1 || done.LogEntries[0].Flags?.HasFlag(QuestLogEntry.Flag.CompleteQuest) != true)
            { Console.WriteLine($"REFUSED: stage {ownerSlot.journalStage} is not a single-entry completing stage to clone the buyer's ending from."); return 1; }
            var buyerSt = done.DeepCopy();
            buyerSt.Index = (ushort)t.beatSlots[2].journalStage;
            buyerSt.LogEntries[0].Entry = null;
            clone.Stages.Add(buyerSt);
            Console.WriteLine($"  +stage   : {buyerSt.Index} (the buyer's ending, a clone of completing stage {ownerSlot.journalStage})");

            // --- the pay, ON THE STAGE: each ending's completing stage names its tier's two globals ----------
            // QuestStage -> LogEntries -> StageCompleteDatas -> RewardDatas: BonusCredits (QRCR), XpAwarded (QRXP).
            // The base's stage 100 carries one reward entry pointing at the easy tier, and the clone copied
            // it, so both stages are written here rather than either being trusted.
            foreach (var (st, tier) in new[] { (done, r.reward!.owner!), (buyerSt, r.reward.buyer!) })
            {
                var rds = st.LogEntries[0].StageCompleteDatas.SelectMany(c => c.RewardDatas).ToList();
                if (rds.Count != 1) { Console.WriteLine($"REFUSED: stage {st.Index} carries {rds.Count} reward entries; this tool writes exactly one."); return 1; }
                var cg = myMod.Globals.FirstOrDefault(g => g.EditorID == $"duo_reward_creds_{tier}");
                var xg = myMod.Globals.FirstOrDefault(g => g.EditorID == $"duo_reward_xp_{tier}");
                if (cg == null || xg == null) { Console.WriteLine($"REFUSED: {t.mod} has no duo_reward_creds_{tier} / duo_reward_xp_{tier}."); return 1; }
                rds[0].BonusCredits.SetTo(cg.FormKey);
                rds[0].XpAwarded.SetTo(xg.FormKey);
                made.StageReward[st.Index] = (cg.FormKey, xg.FormKey);
                Console.WriteLine($"  reward   : stage {st.Index} pays {cg.EditorID} + {xg.EditorID}");
            }

            // --- objectives: the base has 10 and 20; 30 is the buyer's ---------------------------------------
            var slots = new List<BeatSlot>
            {
                t.beatSlots[0],
                ownerSlot,
                new BeatSlot { markerAlias = (int)bm.ID, activatorAlias = (int)ba.ID, objective = t.beatSlots[2].objective,
                               journalStage = t.beatSlots[2].journalStage },
            };
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
                int f = WriteBeat(clone, t, slots[i], r.beats[i], markers, false, PlaceAliasOf(t, r.beats[i].PlaceIndex, made.ThirdPlaceAlias));
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
            Alias("FindTarget", slots[0].activatorAlias);
            Alias("OwnerTarget", slots[1].activatorAlias);
            Alias("BuyerTarget", slots[2].activatorAlias);
            Obj("Item", made.LoadItem);
            foreach (var kv in made.Messages) Obj($"Beat{kv.Key + 1}Message", kv.Value);
            if (!made.OfferMessage.IsNull) Obj("OfferMessage", made.OfferMessage);

            // --- the people at each ending: named NPCs cloned from a friendly vanilla template ------------
            // NPCTools.CloneNPC copies the body field by field and NOT the name or the voice (gen_dlgtest
            // sets the voice after it for the same reason), so both are written here. Unaggressive and
            // Average confidence, as NPCTools.CreateRandomNpc does for a friendly NPC.
            foreach (var who in new[] { "owner", "buyer" })
            {
                string edid = $"{r.id}_{who}_npc";
                foreach (var k in myMod.Npcs.Where(n => n.EditorID == edid).Select(n => n.FormKey).ToList())
                    myMod.Npcs.Remove(k);
                var pp = who == "owner" ? r.people?.owner : r.people?.buyer;
                if (pp == null || !personTemplates.TryGetValue(who, out var tmpl)) continue;
                var npc = Retrograde.Utils.NPCTools.CloneNPC(myMod, tmpl.DeepCopy());
                npc.EditorID = edid;
                npc.Name = pp.name!;
                npc.Voice.SetTo(tmpl.Voice.FormKey);
                npc.Aggression = Npc.AggressionType.Unaggressive;
                npc.Confidence = Npc.ConfidenceType.Average;
                // ⭐ DRESSED FROM THE RECIPE (his ask, 2026-10-08: "can we set there outfit in the json"). Only
                // DefaultOutfit is written; the template's SpaceOutfit stands, so a hazard world still suits them.
                if (outfits.TryGetValue(who, out var outfit))
                {
                    npc.DefaultOutfit.SetTo(outfit);
                    made.Outfits[who] = outfit;
                }
                myMod.Npcs.Add(npc);
                made.People[who] = npc.FormKey;
                string role = who == "owner" ? "Owner" : "Buyer";
                Obj(role + "Person", npc.FormKey);
                Console.WriteLine($"  +npc     : {edid} {npc.FormKey}  \"{pp.name}\"  (clone of {tmpl.EditorID})"
                                  + (outfits.ContainsKey(who) ? $"  outfit {pp.outfit}" : ""));
                if (pp.company != null && companies.TryGetValue(who, out var list))
                {
                    Obj(role + "Company", list);
                    Int(role + "CompanyMin", pp.company.min);
                    Int(role + "CompanyMax", pp.company.max);
                    Console.WriteLine($"  company  : {who} gets {pp.company.min}-{pp.company.max} from {pp.company.list}");
                }
            }
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
        private static int WritePlaceConditions(Quest clone, int placeAlias,
                                                Dictionary<string, FormKey> markers,
                                                List<FormKey> require, List<FormKey> exclude)
        {
            var loc = clone.Aliases?.OfType<QuestLocationAlias>().FirstOrDefault(a => a.ID == (uint)placeAlias);
            if (loc?.Conditions == null) { Console.WriteLine("REFUSED: place alias " + placeAlias + " has no conditions."); return 1; }

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

            Console.WriteLine($"  place {placeAlias,-2} : {markers.Count} marker + {require.Count} require + {exclude.Count} exclude, "
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
            var st = CloneStage(q, stage);
            if (st == null) return null;
            q.Stages.Add(st);

            Console.WriteLine($"  +slot    : beat {index + 1} created -- marker alias {mid}, activator {aid}, stage {stage}");
            return new BeatSlot { markerAlias = (int)mid, activatorAlias = (int)aid, objective = -1, journalStage = stage };
        }

        /// <summary>
        /// A NEW STAGE IS A CLONE OF ONE THAT WORKS, never constructed. His xEdit, 2026-09-24: every
        /// created stage was written as INDX + CNAM only, because a fresh QuestLogEntry has no flags and
        /// Mutagen writes nothing for a field that was never set. The base's stages are INDX, QSDT,
        /// CNAM, QSRD, QSRD. xEdit lost its place at the first bare CNAM and read every objective and
        /// alias after it as "unexpected", so the record showed empty. Same argument as AddBeatSlot's
        /// alias clone: copying a working stage brings every field this tool does not know exists.
        /// The source is a plain journal stage that does NOT complete the quest; its text is cleared
        /// so it cannot leak the base's journal line.
        /// </summary>
        private static QuestStage? CloneStage(Quest q, int index)
        {
            var src = q.Stages?.OrderBy(x => x.Index)
                .FirstOrDefault(x => x.Index != 0 && x.LogEntries.Count == 1 && x.LogEntries[0].Flags != null
                                     && !x.LogEntries[0].Flags!.Value.HasFlag(QuestLogEntry.Flag.CompleteQuest));
            if (src == null) { Console.WriteLine("REFUSED: the base has no plain journal stage to clone a new one from."); return null; }
            var st = src.DeepCopy();
            st.Index = (ushort)index;
            st.LogEntries[0].Entry = null;
            return st;
        }

        /// <summary>Stages in ascending index order, as every shipped quest writes them.</summary>
        private static void SortStages(Quest q)
        {
            var sorted = q.Stages!.OrderBy(x => x.Index).ToList();
            q.Stages.Clear();
            q.Stages.AddRange(sorted);
        }

        /// <summary>
        /// PUT EVERY ALIAS BELOW EVERYTHING IT FILLS FROM. The engine fills aliases in list order
        /// (vanilla: 7,731 dependencies across 2,092 quests, ZERO pointing down the list), and a
        /// second place re-opened for beat one sits BELOW beat one's marker in the base's list.
        /// Stable: nothing moves unless it has to. Refuses on a cycle rather than guessing.
        /// </summary>
        private static int OrderAliases(Quest q)
        {
            if (q.Aliases == null) return 0;
            var before = q.Aliases.ToList();
            var fwd = gen_aliaslint.ForwardRefs(before);
            var order = gen_aliaslint.DependencyOrder(before);
            if (order == null) { Console.WriteLine("REFUSED: the aliases depend on each other in a cycle; no fill order exists."); return 1; }
            int moved = before.Where((a, i) => !ReferenceEquals(order[i], a)).Count();
            q.Aliases.Clear();
            q.Aliases.AddRange(order);
            Console.WriteLine($"  alias order: {fwd.Count} forward reference(s) before, {moved} list position(s) changed"
                              + (fwd.Count > 0 ? "  (" + string.Join(", ", fwd.Select(f => $"{f.alias}->{f.dep}")) + ")" : ""));
            return gen_aliaslint.ForwardRefs(q.Aliases.ToList()).Count == 0 ? 0 : 1;
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
                                     Dictionary<string, FormKey> markers, bool created, int placeAlias)
        {
            var al = clone.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)slot.markerAlias);
            if (al?.Location == null) { Console.WriteLine("REFUSED: ref alias " + slot.markerAlias + " has no location fill."); return 1; }
            al.Location.AliasID = placeAlias;
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

            Console.WriteLine($"  beat     : alias {slot.markerAlias} -> {beat.at} inside place alias {placeAlias}"
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
                                  List<(BeatSlot slot, Beat beat, bool created)> plan, BuildMade made4, string mastersBefore)
        {
            Console.WriteLine();
            Console.WriteLine("  verification, re-read from disk:");
            var reread = StarfieldMod.CreateFromBinaryOverlay(modFile, StarfieldRelease.Starfield, readParams);
            var q = reread.Quests.FirstOrDefault(x => x.EditorID == r.id);
            if (q == null) { Console.WriteLine("    FAIL: the quest is not in the written file."); return 1; }

            int fail = 0;
            // ⛔ A BUILD MUST NEVER CHANGE WHAT THE PLUGIN DEPENDS ON. A link to a record in a plugin that is not
            // a master silently ADDS that plugin as a master, and a player without it cannot load the mod. It
            // happened once (du_overtime.esp, 2026-10-08) and the property checks passed over it, because they
            // compared the link against the same wrong lookup that made it. This one compares against the file.
            fail += Check("the plugin's masters are unchanged by the build",
                          string.Join(", ", reread.ModHeader.MasterReferences.Select(m => m.Master.FileName.String)), mastersBefore);
            foreach (var (label, suffix, aliasId, path) in new[] {
                         ("crate", "_crate", t.beatSlots[0].activatorAlias, r.items?.crateModel),
                         ("delivery point", "_centre", t.beatSlots[1].activatorAlias, r.items?.centreModel) })
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                var act = reread.Activators.FirstOrDefault(a => a.EditorID == r.id + suffix);
                fail += Check($"{label} model", act?.Model?.File?.DataRelativePath.Path ?? "missing", path!);
                if (suffix == "_centre" && !string.IsNullOrWhiteSpace(r.items?.centreName))
                    fail += Check("delivery point name", act?.Name?.String ?? "missing", r.items!.centreName!);
                var la = q.Aliases?.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.ID == (uint)aliasId);
                fail += Check($"alias {aliasId} creates the {label} clone", la?.CreateReferenceToObject?.Object.FormKey.ToString() ?? "unset",
                              act?.FormKey.ToString() ?? "no clone");
            }
            for (int i = 0; i < r.beats.Count; i++)
            {
                var bm = r.beats[i].message;
                string edid = $"{r.id}_msg{i + 1}";
                var m = reread.Messages.FirstOrDefault(x => x.EditorID == edid);
                if (bm == null) { fail += Check($"beat {i + 1} has no message box", m == null ? "none" : edid, "none"); continue; }
                if (m == null) { fail += Check($"beat {i + 1} message {edid} written", "missing", "present"); continue; }
                fail += Check($"beat {i + 1} message text", m.Description?.String ?? "", Expand(bm.text!, t));
                fail += Check($"beat {i + 1} message owned by the quest (OwnerQuest)", m.OwnerQuest.FormKey.ToString(), q.FormKey.ToString());
                fail += Check($"beat {i + 1} message is a pausing box", m.Flags.ToString(), "MessageBox");
                var prop = q.VirtualMachineAdapter?.Scripts.SelectMany(sc => sc.Properties).OfType<IScriptObjectPropertyGetter>()
                    .FirstOrDefault(pp => pp.Name == $"Beat{i + 1}Message");
                fail += Check($"driver property Beat{i + 1}Message points at it", prop?.Object.FormKey.ToString() ?? "unset", m.FormKey.ToString());
            }
            // ⛔ FILL ORDER, off disk: an alias listed above the place it fills inside cannot fill, and
            // the quest silently never starts. duo_delve04 shipped that way on 2026-09-24.
            var fwd = gen_aliaslint.ForwardRefs(q.Aliases?.ToList() ?? new List<IAQuestAliasGetter>());
            fail += Check("no alias is listed above an alias it fills from (gen_aliaslint R1)",
                          fwd.Count == 0 ? "none" : string.Join(", ", fwd.Select(f => $"{f.alias}->{f.dep}")), "none");
            // Each place demands exactly ITS beats' markers plus the map marker: a place that also
            // demanded the other place's markers would shrink its pool for nothing.
            bool twoPlaces = r.beats.Any(b => b.Second);
            foreach (int pi in PlacesUsed(r))
            {
                int aid = PlaceAliasOf(t, pi, made4.ThirdPlaceAlias);
                var want = MarkersAt(r, markers, pi);
                var loc = q.Aliases?.OfType<IQuestLocationAliasGetter>().FirstOrDefault(a => a.ID == (uint)aid);
                var condMarkers = loc?.Conditions?
                    .Select(c => c.Data).OfType<ILocationHasRefTypeConditionDataGetter>()
                    .Select(h => h.FirstParameter.Link.FormKey).ToHashSet() ?? new HashSet<FormKey>();
                fail += Check($"place {aid} demands its beats' markers + the map marker",
                              want.Values.All(k => condMarkers.Contains(k)) ? "yes" : "no", "yes");
                fail += Check($"place {aid} demands NOTHING ELSE", condMarkers.Count.ToString(), want.Count.ToString());
            }

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
                fail += Check($"{tag} searches the {PlaceLabel(beat.PlaceIndex)} place alias",
                              (al?.Location?.AliasID)?.ToString() ?? "unset", PlaceAliasOf(t, beat.PlaceIndex, made4.ThirdPlaceAlias).ToString());

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
            // The two defects his xEdit found and every check above passed over: a stage whose log
            // entry has no flags (no QSDT on disk) and stages out of index order.
            var idx = q.Stages?.Select(x => (int)x.Index).ToList() ?? new List<int>();
            fail += Check("stages in ascending order", string.Join(",", idx), string.Join(",", idx.OrderBy(x => x)));
            var bare = q.Stages?.Where(x => x.LogEntries.Any(le => le.Flags == null)).Select(x => x.Index.ToString()).ToList() ?? new List<string>();
            fail += Check("every stage's log entry has flags (QSDT)", bare.Count == 0 ? "yes" : "no: " + string.Join(",", bare), "yes");

            if (t.kind == "choice")
            {
                // ⭐ THE THIRD PLACE, OFF DISK: it exists, it is a location alias, and the buyer's marker
                // searches inside it. A created alias that did not land is an unfillable reference and the
                // quest silently never starts, so this is the check that matters most on this kind.
                var third = q.Aliases?.OfType<IQuestLocationAliasGetter>().FirstOrDefault(a => a.ID == (uint)made4.ThirdPlaceAlias);
                fail += Check($"third place alias {made4.ThirdPlaceAlias} is a location alias named {t.thirdPlace?.name}",
                              third?.Name ?? "missing", t.thirdPlace?.name ?? "");
                var src = q.Aliases?.OfType<IQuestLocationAliasGetter>().FirstOrDefault(a => a.ID == (uint)(t.thirdPlace?.cloneOf ?? -1));
                string Leash(IQuestLocationAliasGetter? l) => string.Join(" | ", (l?.Conditions ?? new List<IConditionGetter>())
                    .Where(c => c.Data is not ILocationHasRefTypeConditionDataGetter && c.Data is not ILocationHasKeywordConditionDataGetter)
                    .Select(c => c.Data.GetType().Name + " " + c.CompareOperator + " " + (c is IConditionGlobalGetter g ? g.ComparisonValue.FormKey.ToString() : c is IConditionFloatGetter f ? f.ComparisonValue.ToString() : "?")));
                fail += Check("third place carries the same leash as the owner's place", Leash(third), Leash(src));
                var bmk = q.Aliases?.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.ID == made4.BuyerMarkerAlias);
                fail += Check("buyer marker searches the third place", (bmk?.Location?.AliasID)?.ToString() ?? "unset", made4.ThirdPlaceAlias.ToString());
                var bact = q.Aliases?.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.ID == made4.BuyerAlias);
                fail += Check("buyer activator create-objs AT the buyer marker", (bact?.CreateReferenceToObject?.AliasID)?.ToString() ?? "unset", made4.BuyerMarkerAlias.ToString());
                var buyerClone = reread.Activators.FirstOrDefault(a => a.EditorID == r.id + "_buyer");
                fail += Check("buyer activator creates the _buyer clone", bact?.CreateReferenceToObject?.Object.FormKey.ToString() ?? "unset", buyerClone?.FormKey.ToString() ?? "no clone");
                fail += Check("buyer model", buyerClone?.Model?.File?.DataRelativePath.Path ?? "missing", r.items!.buyerModel!);
                fail += Check("buyer name", buyerClone?.Name?.String ?? "missing", r.items.buyerName!);
                // Two endings, each a completing stage with its own recap.
                foreach (var st in new[] { t.beatSlots[1].journalStage, t.beatSlots[2].journalStage })
                {
                    var e = q.Stages?.FirstOrDefault(x => x.Index == st)?.LogEntries.FirstOrDefault();
                    fail += Check($"stage {st} ends the quest (entryFlags CompleteQuest)",
                                  e?.Flags?.HasFlag(QuestLogEntry.Flag.CompleteQuest) == true ? "yes" : "no", "yes");
                }
                var offer = q.Stages?.FirstOrDefault(x => x.Index == t.offerStage)?.LogEntries.FirstOrDefault();
                fail += Check($"offer journal on stage {t.offerStage}", offer?.Entry?.String ?? "missing", Expand(r.offer!.journal!, t));
                fail += Check($"stage {t.offerStage} does NOT end the quest",
                              offer?.Flags?.HasFlag(QuestLogEntry.Flag.CompleteQuest) == true ? "ends" : "no", "no");
                if (r.offer.message != null)
                {
                    var om = reread.Messages.FirstOrDefault(x => x.EditorID == r.id + "_msgOffer");
                    fail += Check("offer message text", om?.Description?.String ?? "missing", Expand(r.offer.message.text!, t));
                    fail += Check("offer message owned by the quest", om?.OwnerQuest.FormKey.ToString() ?? "missing", q.FormKey.ToString());
                }
                foreach (var (who, pp) in new[] { ("owner", r.people?.owner), ("buyer", r.people?.buyer) })
                {
                    if (pp == null) continue;
                    var npc = reread.Npcs.FirstOrDefault(n => n.EditorID == $"{r.id}_{who}_npc");
                    fail += Check($"{who} person named", npc?.Name?.String ?? "missing", pp.name!);
                    fail += Check($"{who} person is unaggressive", npc?.Aggression.ToString() ?? "missing", "Unaggressive");
                    fail += Check($"{who} person has a voice", npc == null || npc.Voice.IsNull ? "no" : "yes", "yes");
                    if (made4.Outfits.TryGetValue(who, out var of))
                        fail += Check($"{who} person wears the recipe's outfit", npc?.DefaultOutfit.FormKey.ToString() ?? "missing", of.ToString());
                }
                foreach (var kv in made4.StageReward)
                {
                    var rds = q.Stages?.FirstOrDefault(x => x.Index == kv.Key)?.LogEntries.FirstOrDefault()?
                        .StageCompleteDatas.SelectMany(c => c.RewardDatas).ToList() ?? new List<IQuestStageRewardDataGetter>();
                    fail += Check($"stage {kv.Key} carries ONE reward entry", rds.Count.ToString(), "1");
                    fail += Check($"stage {kv.Key} pays credits global", rds.FirstOrDefault()?.BonusCredits.FormKey.ToString() ?? "none", kv.Value.creds.ToString());
                    fail += Check($"stage {kv.Key} pays xp global", rds.FirstOrDefault()?.XpAwarded.FormKey.ToString() ?? "none", kv.Value.xp.ToString());
                }
                var drvc = q.VirtualMachineAdapter?.Scripts.FirstOrDefault(sx => sx.Name == t.driver);
                fail += Check("the driver pays nothing (no Credits / OwnerReward / BuyerReward property)",
                              drvc?.Properties.Any(pp => pp.Name is "Credits" or "OwnerReward" or "BuyerReward") == true ? "pays" : "nothing", "nothing");
                var it = reread.MiscItems.FirstOrDefault(m => m.FormKey == made4.LoadItem);
                fail += Check($"item {made4.LoadItem} named", it?.Name?.String ?? "missing", r.items.load!);
            }

            if (t.kind == "delve4" || t.kind == "choice")
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
                foreach (var kv in made4.BoolProps)
                {
                    var p = drv?.Properties.OfType<IScriptBoolPropertyGetter>().FirstOrDefault(x => x.Name == kv.Key);
                    fail += Check($"property {kv.Key}", p?.Data.ToString() ?? "missing", kv.Value.ToString());
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
            }
            if (t.kind == "delve4")
            {
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
            // A beat DECLARED at the second place is meant to search it; anything else searching a
            // collapsed place is a stray, and the second place is only exempt when a beat claims it.
            var claimed = plan.Where(p => p.beat.Second).Select(p => (uint)p.slot.markerAlias).ToHashSet();
            var strays = q.Aliases?.OfType<IQuestReferenceAliasGetter>()
                .Where(a => a.Location != null && t.collapsedPlaceAliases.Contains(a.Location.AliasID ?? -1)
                            && !claimed.Contains(a.ID))
                .Select(a => a.ID).ToList() ?? new List<uint>();
            Console.WriteLine("    ref aliases still searching a collapsed place: "
                              + (strays.Count == 0 ? "none" : string.Join(", ", strays)));
            if (strays.Count > 0) { Console.WriteLine("    FAIL: the collapse was partial, so this spans two POIs."); fail++; }
            else Console.WriteLine(twoPlaces
                ? "    control behaved: only the beat(s) declared at the second place search it."
                : "    control behaved: every beat resolves inside ONE drawn POI.");

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
