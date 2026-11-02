using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde.Nouns.Hunt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// `gen_delve fauna [--dry]`: write data/delves/fauna.json, every vanilla planet and moon with life, read off
        /// Starfield.esm ONLY. His 2026-11-02 "theres like 500 odd quest we can do from just 'this animal is on this
        /// planet'", then "lets see if we can get the creature name from the forms. They're in there somewhere", then
        /// "yeah build that data out". Manual: bethesda.md Inbox, the two 2026-11-02 fauna entries (for 40).
        ///
        /// Every join is a RECORD LINK, never a name match: a planet (PNDT) lists its biomes and each biome's fauna
        /// (Planet.Biomes[].Fauna); a planet's LOCATION is the LCTN carrying the same star and planet ids as the
        /// planet's GalaxyData (Location.StarID / PlanetID, read on Akila: 0x11AF0 / 2 on both), and of those the one
        /// keyworded LocTypeMajorOrbital (the planet or moon itself, not its _Surface or _Orbit).
        ///
        /// A creature's NAME: its own FULL when someone named it by hand (24 of 946: Akila's set, the Red Mile Mauler),
        /// else the game's instance naming rules dn_CCTPrefixes / dn_CCTSuffixes applied to the keywords on the NPC and
        /// on the CCT object mods its templates include. The suffix rules are two rulesets: the first holds the body-type
        /// fallback (index 1: Hexapod, Mantid...) and the SPECIES noun (index 5: Leafbug, Coralbug...), the second an
        /// ending (Herbivore, Grazer, Stalker...). The table stores the parts. ⚠ How the game composes them ON SCREEN is
        /// unverified (one scanner look settles it), so `name` is the species noun, never a guessed composition.
        ///
        /// Vanilla only, deliberately: another mod's planet in this table would make it a master of a free mod.
        /// Generated: never hand-edit fauna.json, re-run this.
        /// </summary>
        private static int Fauna(string dataDir, bool dry)
        {
            using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            var sfKey = ModKey.FromNameAndExtension("Starfield.esm");
            var sf = env.LoadOrder.PriorityOrder.FirstOrDefault(l => l.ModKey == sfKey)?.Mod;
            if (sf == null) { Console.WriteLine("REFUSED: Starfield.esm is not in the load order."); return 1; }

            var table = BuildFaunaTable(sf, out var problems);
            foreach (var p in problems) Console.WriteLine("  ⚠ " + p);
            if (table == null) return 1;

            int creatures = table.planets.Sum(p => p.creatures.Count);
            int byRecord = table.planets.Sum(p => p.creatures.Count(c => c.nameFrom == "record"));
            int byRules = table.planets.Sum(p => p.creatures.Count(c => c.nameFrom == "rules"));
            Console.WriteLine($"  planets and moons with fauna: {table.planets.Count}; creature entries: {creatures} "
                              + $"(named on the record {byRecord}, by the rules {byRules}, unnamed {creatures - byRecord - byRules})");
            Console.WriteLine($"  distinct creatures: {table.planets.SelectMany(p => p.creatures).Select(c => c.formKey).Distinct().Count()}; "
                              + $"distinct names: {table.planets.SelectMany(p => p.creatures).Select(c => c.name).Where(n => n != null).Distinct().Count()}");

            // THE CONTROLS, asserted before anything is written: each was read independently (a raw esm reader, 2026-11-02)
            // and a table that disagrees with any of them is an instrument fault until proven otherwise.
            var controls = new (string planet, string editorId, string name)[]
            {
                ("Akila", "PCM_Cheyenne_Akila_Ashta", "Ashta"),                        // MB98a's waves, played
                ("Porrima III", "PCM_Porrima_Porrima-III_Predator01", "Red Mile Mauler"), // named on the record
                ("Jemison", "PCM_Alpha-Centauri_Jemison_Predator01", "Coralbug"),        // named by the rules
                ("Porrima III", "PCM_Porrima_Porrima-III_Prey01", "Leafbug"),            // rules, a body fallback beside it
                ("Kreet", "PCM_Narion_Kreet_StalkerPredatorA", "Kreet Stalker"),         // a moon
            };
            int bad = 0;
            foreach (var (planet, eid, name) in controls)
            {
                var row = table.planets.FirstOrDefault(p => p.name == planet);
                var c = row?.creatures.FirstOrDefault(x => x.editorId == eid);
                bool ok = c?.name == name;
                Console.WriteLine($"  control  : {planet} / {eid} -> {(row == null ? "NO SUCH PLANET" : c == null ? "NOT LISTED" : c.name ?? "(no name)")} "
                                  + $"{(ok ? "OK" : $"FAIL (want {name})")}");
                if (!ok) bad++;
            }
            var akila = table.planets.FirstOrDefault(p => p.name == "Akila");
            bool akilaLoc = akila?.location == "01ABE3:Starfield.esm";
            Console.WriteLine($"  control  : Akila's location -> {akila?.location ?? "none"} {(akilaLoc ? "OK" : "FAIL (want 01ABE3:Starfield.esm, MB98a's pin)")}");
            if (!akilaLoc) bad++;
            if (bad > 0) { Console.WriteLine($"REFUSED: {bad} control(s) failed; nothing written."); return 1; }

            string path = Path.Combine(dataDir, "fauna.json");
            string json = JsonSerializer.Serialize(table, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            });
            var all = table.planets.SelectMany(p => p.creatures).ToList();
            Console.WriteLine($"  roles    : predator {all.Count(c => c.role == "predator")}, prey {all.Count(c => c.role == "prey")}, "
                              + $"critter {all.Count(c => c.role == "critter")}, more than one {all.Count(c => c.role?.Contains('/') == true)}, none {all.Count(c => c.role == null)}");
            // A cross-check, printed not graded: vanilla's PCM_ EditorIDs say Predator/Prey/Critter for most fauna.
            var byName = all.Where(c => c.editorId.StartsWith("PCM_"))
                            .Select(c => (c, eid: c.editorId.Contains("Predator") ? "predator" : c.editorId.Contains("Prey") ? "prey" : c.editorId.Contains("Critter") ? "critter" : null))
                            .Where(x => x.eid != null).ToList();
            Console.WriteLine($"  roles vs EditorIDs: {byName.Count(x => x.c.role == x.eid)} of {byName.Count} agree");

            string mdPath = Path.Combine(dataDir, "fauna.md");
            string md = FaunaMarkdown(table);
            if (dry) { Console.WriteLine($"  --dry: would write {path} ({json.Length:N0} chars) and {mdPath} ({md.Length:N0} chars)"); return 0; }
            File.WriteAllText(path, json + "\n");
            File.WriteAllText(mdPath, md);
            Console.WriteLine($"  wrote {path} ({json.Length:N0} chars) and {mdPath} ({md.Length:N0} chars)");
            return 0;
        }

        /// <summary>
        /// fauna.md: the same table for a WRITER, planet by planet, every creature's name, role and the EditorID a recipe
        /// pastes. His 2026-11-02 "can we commmit that list so Jessica can use it". Generated with fauna.json, never apart.
        /// </summary>
        private static string FaunaMarkdown(FaunaTable table)
        {
            var sb = new System.Text.StringBuilder();
            void L(string line = "") => sb.Append(line).Append('\n');
            L("# Starfield's wildlife, planet by planet");
            L();
            L("*GENERATED by `gen_delve fauna` from Starfield.esm, with `fauna.json` (the data). Never hand-edit; re-run it.*");
            L();
            L("- **`place.planet`** takes the planet's name exactly as in its heading (a moon is pinned the same way).");
            L("- **`hold.creatures`** and a `defeat` target take the creature's EditorID, the code after its name.");
            L("- **Names:** a name the game's record carries is used as is. The rest come from the game's naming rules, and the name here is the SPECIES word; "
              + "in game a role word (Pack, Herding...) and an ending (Herbivore, Grazer...) may sit around it, shown as `rules:`. How they show on screen is unverified.");
            L("- **Role** is the game's own predator / prey / critter keyword. **Level** is the system's level from his table, not a game field.");
            L("- **Only wildlife is here.** A creature a quest or a cell places (the Akila Ashta Alpha, in `City_Akila_Ashta03`) is in no planet's list, "
              + "and is still usable in `hold.creatures` by its EditorID.");
            L();
            L($"{table.planets.Count} planets and moons, {table.planets.Sum(p => p.creatures.Count)} creatures.");
            string Rank(string? r) => r switch { "predator" => "0", "prey" => "1", "critter" => "2", _ => "3" };
            foreach (var sys in table.planets.GroupBy(p => p.system ?? "(no system)").OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var lvl = sys.First().systemLevel;
                L();
                L($"## {sys.Key}{(lvl is int l ? $" (level {l})" : "")}");
                foreach (var p in sys.OrderBy(p => p.name, StringComparer.Ordinal))
                {
                    L();
                    L($"### {p.name}");
                    L();
                    foreach (var c in p.creatures.OrderBy(c => Rank(c.role)).ThenBy(c => c.name, StringComparer.Ordinal).ThenBy(c => c.editorId, StringComparer.Ordinal))
                    {
                        string parts = c.nameFrom == "rules"
                            ? string.Join(" ", new[] { c.prefix?.FirstOrDefault(), c.name, c.ending }.Where(x => !string.IsNullOrEmpty(x)))
                            : "";
                        var bits = new List<string>();
                        if (c.role != null) bits.Add(c.role);
                        if (c.body == "Swimmer") bits.Add("swimmer");
                        if (c.body == "Flyer") bits.Add("flyer");
                        if (parts.Length > 0 && parts != c.name) bits.Add($"rules: {parts}");
                        L($"- **{c.name ?? "(no name)"}**{(bits.Count > 0 ? " · " + string.Join(", ", bits) : "")} · `{c.editorId}`");
                    }
                }
            }
            return sb.ToString();
        }

        private static FaunaTable? BuildFaunaTable(IStarfieldModGetter sf, out List<string> problems)
        {
            problems = new List<string>();
            var keywords = sf.Keywords.ToDictionary(k => k.FormKey, k => k.EditorID ?? "");
            var majorOrbital = sf.Keywords.FirstOrDefault(k => k.EditorID == "LocTypeMajorOrbital")?.FormKey;
            if (majorOrbital == null) { problems.Add("no LocTypeMajorOrbital keyword in Starfield.esm"); return null; }

            // The naming rules, as (ruleset index, rule index, text, required keywords).
            var prefixes = NamingRules(sf, "dn_CCTPrefixes", problems);
            var suffixes = NamingRules(sf, "dn_CCTSuffixes", problems);
            if (prefixes == null || suffixes == null) return null;

            // A creature's ROLE is the game's own actor-type keyword (on the NPC, its race, or an object mod it includes).
            var roles = new[] { ("ActorTypePredator", "predator"), ("ActorTypePrey", "prey"), ("ActorTypeCritter", "critter") }
                .Select(r => (fk: keywords.FirstOrDefault(k => k.Value == r.Item1).Key, name: r.Item2)).ToList();
            if (roles.Any(r => r.fk.IsNull)) { problems.Add("an ActorTypePredator/Prey/Critter keyword is missing from Starfield.esm"); return null; }
            var raceKeywords = sf.Races.ToDictionary(r => r.FormKey, r => r.Keywords?.Select(k => k.FormKey).ToHashSet() ?? new HashSet<FormKey>());

            // Each CCT object mod's keyword adds, the NPC-property kind.
            var omodKeywords = new Dictionary<FormKey, HashSet<FormKey>>();
            foreach (var o in sf.ObjectModifications.OfType<INpcModificationGetter>())
            {
                var set = new HashSet<FormKey>();
                foreach (var prop in o.Properties)
                    if (prop is IObjectModFormLinkIntPropertyGetter<Npc.Property> fl && fl.Property == Npc.Property.Keyword)
                        set.Add(fl.Record.FormKey);
                omodKeywords[o.FormKey] = set;
            }

            var stars = new Dictionary<uint, string>();
            foreach (var s in sf.Stars)
                if (s.ID is uint id && !string.IsNullOrEmpty(s.Name)) stars[id] = s.Name;

            // A planet's location: same star and planet ids, keyworded LocTypeMajorOrbital.
            var locs = new Dictionary<(uint, uint), List<ILocationGetter>>();
            foreach (var l in sf.Locations)
                if (l.StarID is int st && l.PlanetID is int pl && l.Keywords != null && l.Keywords.Any(k => k.FormKey == majorOrbital))
                {
                    if (!locs.TryGetValue(((uint)st, (uint)pl), out var list)) locs[((uint)st, (uint)pl)] = list = new List<ILocationGetter>();
                    list.Add(l);
                }

            var npcs = sf.Npcs.ToDictionary(n => n.FormKey);
            var rows = new Dictionary<(uint, uint), FaunaPlanet>();
            foreach (var planet in sf.Planets)
            {
                var fauna = planet.Biomes.SelectMany(b => b.Fauna.Select(f => f.FormKey)).Distinct().ToList();
                if (fauna.Count == 0) continue;
                if (planet.GalaxyData == null) { problems.Add($"{planet.EditorID} has fauna and no galaxy data; left out"); continue; }
                var ids = (planet.GalaxyData.StarId, planet.GalaxyData.PlanetId);
                if (rows.TryGetValue(ids, out var already))
                {
                    // A second planet record for the same body (Akila has _AkilaArrivalCellPlanetData): keep the one with more fauna.
                    problems.Add($"{planet.EditorID} and {already.planetData} are the same body (star {ids.Item1:X}, planet {ids.Item2}); kept the one with more fauna");
                    if (fauna.Count <= already.creatures.Count) continue;
                }
                if (!locs.TryGetValue(ids, out var found) || found.Count != 1)
                {
                    problems.Add($"{planet.EditorID} ({planet.Name}): {(found == null ? "no" : found.Count.ToString())} LocTypeMajorOrbital location(s) for star {ids.Item1:X}, planet {ids.Item2}; left out");
                    continue;
                }
                string? system = stars.TryGetValue(ids.Item1, out var sn) ? sn : null;
                var row = new FaunaPlanet
                {
                    name = planet.Name ?? planet.EditorID ?? "",
                    system = system,
                    systemLevel = system != null && PredatorHuntTarget.SystemLevels.TryGetValue(system, out int lv) ? lv : null,
                    location = found[0].FormKey.ToString(),
                    locationEditorId = found[0].EditorID,
                    planetData = planet.EditorID,
                    biomes = planet.Biomes.Select(b => new FaunaBiome
                    {
                        biome = sf.Biomes.FirstOrDefault(x => x.FormKey == b.Biome.FormKey)?.EditorID ?? b.Biome.FormKey.ToString(),
                        fauna = b.Fauna.Select(f => f.FormKey.ToString()).ToList(),
                    }).Where(b => b.fauna.Count > 0).ToList(),
                };
                foreach (var fk in fauna)
                {
                    if (!npcs.TryGetValue(fk, out var npc)) { problems.Add($"{row.name}: fauna {fk} is not an NPC in Starfield.esm"); continue; }
                    row.creatures.Add(NameCreature(npc, omodKeywords, prefixes, suffixes, roles, raceKeywords));
                }
                rows[ids] = row;
            }

            var dupeNames = rows.Values.GroupBy(r => r.name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupeNames.Count > 0) { problems.Add("two bodies share a name, so a recipe could not say which: " + string.Join(", ", dupeNames)); return null; }

            return new FaunaTable
            {
                schema = 1,
                _note = new List<string>
                {
                    "GENERATED by `gen_delve fauna` from Starfield.esm. Never hand-edit; re-run it. His 2026-11-02 \"yeah build that data out\".",
                    "Every vanilla planet and moon with fauna: its location (joined by star and planet id, the LocTypeMajorOrbital one, so place.planet pins exactly this), its biomes and each biome's fauna, and the creatures with their names.",
                    "nameFrom 'record' = the creature's own FULL; 'rules' = the CCT instance naming rules (dn_CCTPrefixes, dn_CCTSuffixes) on its keywords. `name` is the SPECIES noun; prefix, body and ending are the other parts, and how the game composes them on screen is unverified (one scanner look settles it).",
                    "systemLevel is his table (Retrograde.Library PredatorHuntTarget.SystemLevels), not a game field. Manual: bethesda.md Inbox, 2026-11-02 fauna entries.",
                },
                source = "Starfield.esm",
                planets = rows.Values.OrderBy(r => r.system).ThenBy(r => r.name).ToList(),
            };
        }

        private static List<(int set, int index, string text, HashSet<FormKey> needs)>? NamingRules(IStarfieldModGetter sf, string editorId, List<string> problems)
        {
            var rec = sf.InstanceNamingRules.FirstOrDefault(r => r.EditorID == editorId);
            if (rec == null) { problems.Add($"no instance naming rules {editorId} in Starfield.esm"); return null; }
            if (rec.Rules is not IActorInstanceNamingRulesGetter actor) { problems.Add($"{editorId} is not an actor naming rule set"); return null; }
            var outList = new List<(int, int, string, HashSet<FormKey>)>();
            for (int s = 0; s < actor.Rules.Count; s++)
                foreach (var n in actor.Rules[s].Names)
                {
                    string? text = n.Text?.String;
                    var needs = n.Keywords?.Select(k => k.FormKey).ToHashSet() ?? new HashSet<FormKey>();
                    if (string.IsNullOrEmpty(text) || needs.Count == 0) continue;
                    outList.Add((s, n.Index ?? 0, text, needs));
                }
            return outList;
        }

        private static FaunaCreature NameCreature(INpcGetter npc, Dictionary<FormKey, HashSet<FormKey>> omodKeywords,
            List<(int set, int index, string text, HashSet<FormKey> needs)> prefixes,
            List<(int set, int index, string text, HashSet<FormKey> needs)> suffixes,
            List<(FormKey fk, string name)> roles, Dictionary<FormKey, HashSet<FormKey>> raceKeywords)
        {
            var have = new HashSet<FormKey>(npc.Keywords?.Select(k => k.FormKey) ?? Enumerable.Empty<FormKey>());
            foreach (var t in npc.ObjectTemplates ?? Enumerable.Empty<IObjectTemplateGetter<Npc.Property>>())
                foreach (var inc in t.Includes)
                    if (omodKeywords.TryGetValue(inc.Mod.FormKey, out var ks)) have.UnionWith(ks);
            List<string> Match(IEnumerable<(int set, int index, string text, HashSet<FormKey> needs)> rules)
                => rules.Where(r => r.needs.IsSubsetOf(have)).Select(r => r.text).ToList();

            var c = new FaunaCreature { formKey = npc.FormKey.ToString(), editorId = npc.EditorID ?? "" };
            var species = Match(suffixes.Where(r => r.set == 0 && r.index == 5));
            c.body = Match(suffixes.Where(r => r.set == 0 && r.index == 1)).FirstOrDefault();
            c.prefix = Match(prefixes);
            c.ending = Match(suffixes.Where(r => r.set == 1)).FirstOrDefault();
            if (npc.Name?.String is string full && full.Length > 0) { c.name = full; c.nameFrom = "record"; }
            else if (species.Count == 1) { c.name = species[0]; c.nameFrom = "rules"; }
            else c.nameFrom = species.Count == 0 ? "none" : "ambiguous: " + string.Join(" / ", species);
            if (c.prefix.Count == 0) c.prefix = null;
            // Role only: the race's keywords join here and NOT the naming set above, whose rules were matched without them.
            var roleSet = new HashSet<FormKey>(have);
            if (raceKeywords.TryGetValue(npc.Race.FormKey, out var rk)) roleSet.UnionWith(rk);
            var rs = roles.Where(r => roleSet.Contains(r.fk)).Select(r => r.name).ToList();
            c.role = rs.Count == 0 ? null : string.Join("/", rs);
            return c;
        }

        /// <summary>
        /// place.planet's table, READ from fauna.json (2026-11-02): the planet's name to its location. Replaces the
        /// closed table a planet used to be added to by hand. Refused loudly if the file is missing or malformed, because
        /// a planet a recipe names and the build cannot find must stop the build, never default.
        /// </summary>
        private static Dictionary<string, FormKey>? _planets;
        private static Dictionary<string, FormKey> Planets => _planets ??= LoadPlanets();

        private static Dictionary<string, FormKey> LoadPlanets()
        {
            var dataDir = ResolveDataDir() ?? throw new InvalidOperationException("place.planet: no data/delves directory to find fauna.json in.");
            string path = Path.Combine(dataDir, "fauna.json");
            if (!File.Exists(path)) throw new InvalidOperationException($"place.planet: no {path}; run `gen_delve fauna`.");
            var table = JsonSerializer.Deserialize<FaunaTable>(File.ReadAllText(path), StrictJsonOpts)
                        ?? throw new InvalidOperationException($"place.planet: {path} is empty.");
            if (table.schema != 1) throw new InvalidOperationException($"place.planet: {path} is schema {table.schema}; this build reads 1.");
            return table.planets.ToDictionary(p => p.name, p => FormKey.Factory(p.location), StringComparer.Ordinal);
        }

        private sealed class FaunaTable
        {
            public int schema { get; set; }
            public List<string>? _note { get; set; }
            public string source { get; set; } = "";
            public List<FaunaPlanet> planets { get; set; } = new();
        }

        private sealed class FaunaPlanet
        {
            public string name { get; set; } = "";
            public string? system { get; set; }
            public int? systemLevel { get; set; }
            public string location { get; set; } = "";
            public string? locationEditorId { get; set; }
            public string? planetData { get; set; }
            public List<FaunaBiome> biomes { get; set; } = new();
            public List<FaunaCreature> creatures { get; set; } = new();
        }

        private sealed class FaunaBiome
        {
            public string biome { get; set; } = "";
            public List<string> fauna { get; set; } = new();
        }

        private sealed class FaunaCreature
        {
            public string formKey { get; set; } = "";
            public string editorId { get; set; } = "";
            public string? name { get; set; }
            public string nameFrom { get; set; } = "";
            public List<string>? prefix { get; set; }
            public string? body { get; set; }
            public string? ending { get; set; }
            /// <summary>predator, prey or critter: the actor-type keyword on the creature, its race or its object mods.</summary>
            public string? role { get; set; }
        }
    }
}
