using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Nouns.QuestAuthoring
{
    /// <summary>A location as the pool census reads it: its ref types (BOTH special-reference lists) and keywords,
    /// whether it entered the POI pool by LocTypeOE_Keyword, by RECenterLocRef or both, and whether its record is
    /// Starfield.esm's own.</summary>
    public sealed class Poi
    {
        public HashSet<FormKey> RefTypes = new();
        public HashSet<FormKey> Keywords = new();
        public bool Vanilla;
        public bool ByKeyword;
        public bool ByCentre;
        public string Edid = "";
    }

    /// <summary>A placed reference's position, in metres (1 unit = 1 m, read off his HUD 2026-09-24).</summary>
    public readonly record struct P3(float X, float Y, float Z);

    /// <summary>
    /// THE PLACES A QUEST CAN BE SENT, read off the load order: the POI pool, what a place alias can draw, what a
    /// PCM request can put down, and how far apart two markers land. Read-only analysis; the alias lint grades a
    /// recipe's places against it and the census verbs print it.
    ///
    /// ONE census per load order: build it from the environment and keep it, and each corpus is read once, on
    /// first ask. Holding it is the caller's job, so the library carries no static state.
    ///
    /// ⚠ A LIBRARY PIECE (Nouns/QuestAuthoring): it returns what it found and never prints. Lifted out of FrankyCLI's
    /// gen_delve on 2026-10-15, split step 3. Reads Mutagen getters until the build rules move onto the model.
    /// </summary>
    public sealed class PoiCensus
    {
        /// <summary>The PCM branch a PCM_QuestGeneral request places from (measured 2026-10-14: no node tests that
        /// keyword, and every _Special node demands its own forced-location keyword).</summary>
        public const string PcmGeneralNode = "PCM_LocationRequest_General";

        private readonly IGameEnvironment<IStarfieldMod, IStarfieldModGetter> _env;
        private List<Poi>? _pool, _draw;

        public PoiCensus(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env) => _env = env;

        /// <summary>True when this census was read off that environment.</summary>
        public bool Reads(object env) => ReferenceEquals(_env, env);

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
        public List<Poi> Pool => _pool ??= ReadPool();

        /// <summary>
        /// WHAT A PLACE CAN DRAW: the POI pool plus every location carrying LocDungeonBossLocRef. The marker conditions
        /// do the selecting, so the lint must count every location the game could select, not only the POI pool.
        /// ⛔ WHY IT IS A UNION, his 2026-10-13: "loads of pois have mapmarkers and boss markers". Part 32 held the
        /// two populations disjoint off a Starfield.esm census; on Defiant 106 of the 491 POIs carry the boss marker,
        /// and vanilla's MB_Bounty01Far asks a place for that one marker and nothing else. A first cut of this change
        /// (a place "kind" switching between two populations) encoded the split and was deleted before it shipped.
        /// </summary>
        public List<Poi> Draw => _draw ??= ReadDraw();

        private List<Poi> ReadPool()
        {
            var oeKeyword = _env.LoadOrder.PriorityOrder.WinningOverrides<IKeywordGetter>()
                .FirstOrDefault(x => string.Equals(x.EditorID, "LocTypeOE_Keyword", StringComparison.OrdinalIgnoreCase))?.FormKey;
            var centre = _env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>()
                .FirstOrDefault(x => string.Equals(x.EditorID, "RECenterLocRef", StringComparison.OrdinalIgnoreCase))?.FormKey;

            var outp = new List<Poi>();
            foreach (var loc in _env.LoadOrder.PriorityOrder.WinningOverrides<ILocationGetter>())
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

        private List<Poi> ReadDraw()
        {
            var boss = _env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>()
                .FirstOrDefault(x => string.Equals(x.EditorID, "LocDungeonBossLocRef", StringComparison.OrdinalIgnoreCase))?.FormKey;
            var outp = Pool.ToList();
            var seen = outp.Select(p => p.Edid).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (boss == null) return outp;
            foreach (var loc in _env.LoadOrder.PriorityOrder.WinningOverrides<ILocationGetter>())
            {
                if (seen.Contains(loc.EditorID ?? "")) continue;
                var p = new Poi();
                foreach (var g in new[] { loc.MasterSpecialReferences, loc.AddedSpecialReferences })
                {
                    if (g == null) continue;
                    foreach (var e in g)
                        if (!e.LocationRefType.IsNull) p.RefTypes.Add(e.LocationRefType.FormKey);
                }
                if (!p.RefTypes.Contains(boss.Value)) continue;
                if (loc.Keywords != null) foreach (var k in loc.Keywords) p.Keywords.Add(k.FormKey);
                p.Vanilla = loc.FormKey.ModKey.FileName.String.Equals("Starfield.esm", StringComparison.OrdinalIgnoreCase);
                p.Edid = loc.EditorID ?? "";
                outp.Add(p);
            }
            return outp;
        }

        /// <summary>The PCM tree as the game builds it: children grouped by their ParentNode, winning overrides.</summary>
        public (Dictionary<FormKey, List<IPlanetContentManagerBranchNodeGetter>> kids,
                Dictionary<FormKey, List<IPlanetContentManagerContentNodeGetter>> leaves) PcmTree()
            => (_env.LoadOrder.PriorityOrder.WinningOverrides<IPlanetContentManagerBranchNodeGetter>()
                    .GroupBy(b => b.ParentNode.FormKey).ToDictionary(g => g.Key, g => g.ToList()),
                _env.LoadOrder.PriorityOrder.WinningOverrides<IPlanetContentManagerContentNodeGetter>()
                    .GroupBy(c => c.ParentNode.FormKey).ToDictionary(g => g.Key, g => g.ToList()));

        /// <summary>
        /// THE REMOTE PLACE'S CORPUS: every place a PCM_QuestGeneral request can put down, one Poi per distinct
        /// worldspace's Location under PcmGeneralNode. Same walk as `gen_delve pcmpool`. Leaves with no Location are
        /// unreadable and left out (7 on Defiant, 2026-10-14); branch conditions are not evaluated.
        /// </summary>
        public List<Poi> PcmRequestPool()
        {
            var root = _env.LoadOrder.PriorityOrder.WinningOverrides<IPlanetContentManagerBranchNodeGetter>()
                .FirstOrDefault(b => b.EditorID == PcmGeneralNode);
            var outp = new List<Poi>();
            if (root == null) return outp;
            var (kids, leaves) = PcmTree();
            var seen = new HashSet<FormKey>();
            var stack = new Stack<FormKey>(new[] { root.FormKey });
            while (stack.Count > 0)
            {
                var fk = stack.Pop();
                if (leaves.TryGetValue(fk, out var ls))
                    foreach (var leaf in ls)
                        if (seen.Add(leaf.Content.FormKey)
                            && _env.LinkCache.TryResolve<IWorldspaceGetter>(leaf.Content.FormKey, out var ws)
                            && !ws.Location.IsNull && _env.LinkCache.TryResolve<ILocationGetter>(ws.Location.FormKey, out var loc))
                            outp.Add(PoiOf(loc));
                if (kids.TryGetValue(fk, out var ks)) foreach (var k in ks) stack.Push(k.FormKey);
            }
            return outp;
        }

        /// <summary>A location's ref types (BOTH special-reference lists) and keywords, as the pool census reads them.</summary>
        public static Poi PoiOf(ILocationGetter loc)
        {
            var p = new Poi { Edid = loc.EditorID ?? "" };
            foreach (var g in new[] { loc.MasterSpecialReferences, loc.AddedSpecialReferences })
            { if (g == null) continue; foreach (var e in g) if (!e.LocationRefType.IsNull) p.RefTypes.Add(e.LocationRefType.FormKey); }
            if (loc.Keywords != null) foreach (var k in loc.Keywords) p.Keywords.Add(k.FormKey);
            p.Vanilla = loc.FormKey.ModKey.FileName.String.Equals("Starfield.esm", StringComparison.OrdinalIgnoreCase);
            return p;
        }

        /// <summary>
        /// Distance between one marker of each named type, per POI. Shared by `spread` and the lint
        /// so the two can never report different numbers for the same question.
        /// ⚠ A POI can carry several refs of one type and the FIRST is taken. Stated rather than
        /// left to be assumed: this is not the nearest pair, nor the farthest, nor a mean.
        /// </summary>
        public List<double> Separations(string a, string b)
        {
            var outp = new List<double>();
            var lcrt = _env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>()
                .Where(x => x.EditorID != null)
                .GroupBy(x => x.EditorID!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().FormKey, StringComparer.OrdinalIgnoreCase);
            if (!lcrt.TryGetValue(a, out var ka) || !lcrt.TryGetValue(b, out var kb)) return outp;

            var oeK = _env.LoadOrder.PriorityOrder.WinningOverrides<IKeywordGetter>()
                .FirstOrDefault(x => string.Equals(x.EditorID, "LocTypeOE_Keyword", StringComparison.OrdinalIgnoreCase))?.FormKey;
            var ctr = lcrt.TryGetValue("RECenterLocRef", out var ck) ? ck : (FormKey?)null;

            foreach (var loc in _env.LoadOrder.PriorityOrder.WinningOverrides<ILocationGetter>())
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
                if (TryPos(ma.Value, out var pa) && TryPos(mb.Value, out var pb)) outp.Add(Dist(pa, pb));
            }
            return outp;
        }

        /// <summary>A placed object's position. Position sits directly on the placed record, not under a Placement
        /// sub-object (read off gen_moveref, which moves these for real). ⚠ The link cache reads exterior placed refs
        /// and not interior ones, so false here is often "indoors", never "absent".</summary>
        public bool TryPos(FormKey k, out P3 p)
        {
            p = default;
            if (_env.LinkCache.TryResolve<IPlacedObjectGetter>(k, out var po))
            { p = new P3(po.Position.X, po.Position.Y, po.Position.Z); return true; }
            return false;
        }

        public static double Dist(P3 a, P3 b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>-1 for an empty list, so an empty row sorts last in a table ordered by median.</summary>
        public static double Median(List<double> v) => v.Count == 0 ? -1 : Pct(v.OrderBy(x => x).ToList(), .5);

        /// <summary>The q-quantile of an ascending list by nearest rank, 0 when empty.</summary>
        public static double Pct(List<double> sorted, double q)
            => sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)(q * sorted.Count))];
    }
}
