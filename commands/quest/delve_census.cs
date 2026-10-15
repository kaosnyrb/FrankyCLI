using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde.Nouns.QuestAuthoring;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// THE CENSUS VERBS of gen_delve (census, spread, markers, bases, footprint, keywords, pcmpool): read-only
    /// analysis of the places a Delve can be sent, printed for a terminal. The measurement they share is
    /// Retrograde.Library's PoiCensus; these keep their own tables and print them. Moved out of gen_delve.cs
    /// unchanged, 2026-10-15 (split step 3d, his option A): a CLI's diagnostics, so no result types.
    /// </summary>
    internal static class DelveCensus
    {
        // The census itself (the pools, the PCM tree, marker separations, positions) is Retrograde.Library's
        // PoiCensus (split step 3, 2026-10-15). CensusOf holds ONE per environment, for these verbs and for
        // gen_delve's lint alike, so the two never read the load order twice or disagree.

        private static PoiCensus? _census;

        internal static PoiCensus CensusOf(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env)
            => _census != null && _census.Reads(env) ? _census : (_census = new PoiCensus(env));

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
        internal static int Spread(List<string> markerNames, bool dungeons)
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
            var census = CensusOf(env);

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
                    if (census.TryPos(refs[0], out var p)) pos[kv.Key] = p;
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
                    samples[pair].Add(PoiCensus.Dist(p1, p2));
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
            foreach (var kv in samples.OrderByDescending(s => PoiCensus.Median(s.Value)))
            {
                var v = kv.Value.OrderBy(x => x).ToList();
                if (v.Count == 0) { Console.WriteLine($"  {kv.Key,-52} {0,5}   (no POI carries both)"); continue; }
                Console.WriteLine($"  {kv.Key,-52} {v.Count,5} {v[0],9:F0} {PoiCensus.Pct(v, .25),9:F0} {PoiCensus.Pct(v, .5),9:F0} {PoiCensus.Pct(v, .75),9:F0} {v[^1],9:F0}");
            }
            Console.WriteLine();
            Console.WriteLine("  ⚠ A MEDIAN IS NOT A GUARANTEE. The draw is random, so a recipe picking the widest pair");
            Console.WriteLine("    still lands on its own p25 a quarter of the time. Read the SPREAD, not the middle.");
            return 0;
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
        internal static int Markers(bool dungeons)
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
        internal static int MarkerBases(List<string> typeNames)
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
            var pool = CensusOf(env).Pool.Select(p => p.Edid).ToHashSet(StringComparer.OrdinalIgnoreCase);

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
        internal static int Footprint()
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
            var census = CensusOf(env);
            var pool = census.Pool.Where(p => p.Vanilla).Select(p => p.Edid).ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                    if (census.TryPos(k, out var p)) reach = Math.Max(reach, Math.Max(Math.Abs(p.X), Math.Abs(p.Y)));
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
        /// <summary>
        /// `gen_delve pcmpool <pcmBranchNode> [refType ...]`: the places a PCM request resolving under that node can
        /// put down, and how many of them carry EVERY named ref type. A remote Delve's place fills by PCM request
        /// (ALPS), which gen_aliaslint can only call UNKNOWN; this is the read that turns it into a number.
        /// His 2026-10-13 idea: remote missions on the item/terminal markers "so they'll always work". That holds
        /// only if the content a request can place carries them, so this answers it.
        ///
        /// Reads WINNING overrides; the tree is built from each child's ParentNode (the game ignores Nodes, his
        /// docs/formlib/pcm.md). A leaf whose content is not a worldspace, or a worldspace with no Location, is
        /// counted and named, never dropped. ⚠ Branch conditions (a GetRandomPercent, a system keyword) are
        /// PRINTED and NOT evaluated: the pool is what CAN be placed, not what a given roll will place.
        /// ⚠ Ref types are read off the worldspace's own Location (both special-reference lists); whether a
        /// condition walks the location hierarchy is unread (manual 32).
        /// </summary>
        internal static int PcmPool(List<string> pos)
        {
            if (pos.Count < 1) { Console.WriteLine("REFUSED: gen_delve pcmpool <pcmBranchNode> [refType ...]"); return 1; }
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            var branches = env.LoadOrder.PriorityOrder.WinningOverrides<IPlanetContentManagerBranchNodeGetter>().ToList();
            var root = branches.FirstOrDefault(b => string.Equals(b.EditorID, pos[0], StringComparison.OrdinalIgnoreCase));
            FormKey rootFk;
            if (root != null) rootFk = root.FormKey;
            else
            {
                // A tree record (PCM_QuestRequest) is a valid root too: branch nodes parent straight to it.
                var tree = env.LoadOrder.PriorityOrder.WinningOverrides<IPlanetContentManagerTreeGetter>()
                    .FirstOrDefault(t => string.Equals(t.EditorID, pos[0], StringComparison.OrdinalIgnoreCase));
                if (tree == null) { Console.WriteLine($"REFUSED: no PCM branch node or tree named '{pos[0]}'"); return 1; }
                rootFk = tree.FormKey;
            }

            var need = new List<(string name, FormKey fk)>();
            foreach (var n in pos.Skip(1))
            {
                var rt = env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>()
                    .FirstOrDefault(x => string.Equals(x.EditorID, n, StringComparison.OrdinalIgnoreCase));
                if (rt == null) { Console.WriteLine($"REFUSED: '{n}' is not a location ref type"); return 1; }
                need.Add((n, rt.FormKey));
            }

            var (kids, leaves) = CensusOf(env).PcmTree();

            int total = 0, noWs = 0, noLoc = 0, joint = 0;
            var perNeed = new int[need.Count];
            var seenContent = new HashSet<FormKey>();
            void Walk(FormKey fk, string path, int depth)
            {
                if (leaves.TryGetValue(fk, out var ls))
                {
                    int here = 0, hereJoint = 0;
                    foreach (var leaf in ls)
                    {
                        if (!seenContent.Add(leaf.Content.FormKey)) continue;
                        here++; total++;
                        if (!env.LinkCache.TryResolve<IWorldspaceGetter>(leaf.Content.FormKey, out var ws))
                        { noWs++; Console.WriteLine($"    ? {leaf.EditorID}: content {leaf.Content.FormKey} is not a worldspace"); continue; }
                        if (ws.Location.IsNull || !env.LinkCache.TryResolve<ILocationGetter>(ws.Location.FormKey, out var loc))
                        { noLoc++; Console.WriteLine($"    ? {ws.EditorID}: worldspace has no Location"); continue; }
                        var rts = PoiCensus.PoiOf(loc).RefTypes;
                        bool all = true;
                        var marks = new List<string>();
                        for (int i = 0; i < need.Count; i++)
                        {
                            bool has = rts.Contains(need[i].fk);
                            if (has) perNeed[i]++;
                            marks.Add(has ? "Y" : ".");
                            all &= has;
                        }
                        if (all) { joint++; hereJoint++; }
                        if (need.Count > 0 && marks.Any(m => m == "Y"))
                            Console.WriteLine($"    {string.Join("", marks)} {ws.EditorID} -> {loc.EditorID}");
                    }
                    Console.WriteLine($"{new string(' ', depth * 2)}  leaves {here}, carrying all named: {hereJoint}   ({path})");
                }
                if (!kids.TryGetValue(fk, out var ks)) return;
                foreach (var k in ks)
                {
                    string conds = k.Conditions == null || k.Conditions.Count == 0 ? "" :
                        "  IF " + string.Join("; ", k.Conditions.Select(c => c.Data?.GetType().Name
                            .Replace("ConditionDataBinaryOverlay", "").Replace("ConditionData", "") ?? "?"));
                    Console.WriteLine($"{new string(' ', depth * 2)}[{k.NodeType}] {k.EditorID}{conds}");
                    Walk(k.FormKey, path + " > " + k.EditorID, depth + 1);
                }
            }
            Walk(rootFk, pos[0], 0);

            Console.WriteLine($"\n  distinct places under {pos[0]}: {total}" +
                              (noWs + noLoc > 0 ? $"  (unreadable: {noWs} not a worldspace, {noLoc} no Location)" : ""));
            for (int i = 0; i < need.Count; i++) Console.WriteLine($"    {need[i].name}: {perNeed[i]}");
            if (need.Count > 0) Console.WriteLine($"  carrying ALL {need.Count} named: {joint}");
            return 0;
        }

        internal static int PoolKeywords(List<string> narrow)
        {
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            var pool = CensusOf(env).Pool;
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

        private static (int, int, int, int) OriginCensus = (-1, -1, -1, -1);

        internal static int Census(bool vanillaOnly)
        {
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            var all = CensusOf(env).Pool;
            var pool = vanillaOnly ? all.Where(p => p.Vanilla).ToList() : all;

            // ⛔ THE ORIGIN-VERSUS-WINNER SPLIT, and it is the thing that explains a uniform offset.
            // PoiCensus.Pool reads the WINNING record, which is what the game evaluates. The manual's
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
    }
}
