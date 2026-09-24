using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// gen_aliaslint &lt;search&gt; [--quiet]
    ///
    /// CAN THIS QUEST FILL ITS ALIASES? Graded off the BUILT record in the live load order, never off
    /// a recipe, so it grades a hand edit exactly as it grades a generator's output.
    ///
    /// ⛔ WHY IT EXISTS: a quest that cannot fill every non-optional alias SILENTLY DOES NOT START.
    /// No log, no error. His words: "most of the quest dev time is getting the aliases right". The
    /// manual's rule is that the validator comes before the composer, and on 2026-09-24 the composer
    /// shipped a quest (duo_delve04) whose beat-one marker filled inside a place listed BELOW it.
    ///
    /// THREE VERDICTS, because some conditions cannot be answered from records:
    ///   CANNOT START  -- proven from the records alone.
    ///   CAN START IF  -- every static term has candidates; the runtime-only terms are LISTED, never
    ///                    silently assumed true. "Could not look" must never read as "looked, fine".
    ///   UNKNOWN       -- a fill or condition this tool does not evaluate. Named, never passed.
    ///
    /// RULES
    ///   R1 fill order  -- an alias that fills FROM another alias (in-location ALLA, create-at ALCA,
    ///                     from-collection) must be listed BELOW it. Evidence, 2026-09-24: vanilla
    ///                     Starfield.esm, 2,092 quests, 7,731 such references, ZERO forward; du_overtime,
    ///                     660 records, 1,507 backward, 2 forward, and those 2 were duo_delve04/04t,
    ///                     the two that do not start. ⚠ Corpus evidence, not an engine document.
    ///   R2 dependency  -- the alias depended on must exist.
    ///   R3 place pool  -- a location alias's STATIC conditions (LocationHasRefType, LocationHasKeyword,
    ///                     the LocationTypeKeyword fill) evaluated over every winning Location.
    ///   R4 joint pool  -- the place must ALSO carry every ref type its non-optional in-location ref
    ///                     aliases ask for, at the SAME location. Each pool non-empty alone proves
    ///                     nothing about the intersection.
    ///
    /// ⚠ STATED LIMIT: whether conditions evaluate up the location hierarchy (ParentLocation) is NOT
    /// established (manual part 32). Pools here count a location's OWN references and keywords, so a
    /// zero pool is a zero under that reading.
    /// </summary>
    public static class gen_aliaslint
    {
        private const uint OptionalBit = 0x2;
        private const byte OrFlag = 0x01;

        private sealed class Loc
        {
            public string Edid = "";
            public HashSet<FormKey> RefTypes = new();
            public HashSet<FormKey> Keywords = new();
        }

        private enum Verdict { Ok, CanIf, Unknown, Cannot }

        public static int Run(string[] args)
        {
            string? search = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
            bool quiet = args.Any(a => a.Equals("--quiet", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(search))
            {
                Console.WriteLine("Usage: gen_aliaslint <questEditorIdSubstring> [--quiet]");
                Console.WriteLine("       --quiet prints one verdict line per quest (for sweeps).");
                return 1;
            }

            using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();

            var locs = new List<Loc>();
            foreach (var l in env.LoadOrder.PriorityOrder.WinningOverrides<ILocationGetter>())
            {
                var x = new Loc { Edid = l.EditorID ?? l.FormKey.ToString() };
                // ⛔ BOTH special-reference lists, unioned: reading only the master list under-reports,
                // in the direction that makes a pool look smaller (gen_delve's PoolCensus, same rule).
                foreach (var g in new[] { l.MasterSpecialReferences, l.AddedSpecialReferences })
                {
                    if (g == null) continue;
                    foreach (var e in g) if (!e.LocationRefType.IsNull) x.RefTypes.Add(e.LocationRefType.FormKey);
                }
                if (l.Keywords != null) foreach (var k in l.Keywords) x.Keywords.Add(k.FormKey);
                locs.Add(x);
            }

            var quests = env.LoadOrder.PriorityOrder.WinningOverrides<IQuestGetter>()
                .Where(q => q.EditorID != null && q.EditorID.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
            Console.WriteLine($"  load order: {locs.Count} locations; {quests.Count} quest(s) matching '{search}'");
            if (quests.Count == 0) { Console.WriteLine("  REFUSED: nothing matched -- a needle failure, not a finding."); return 1; }

            var tally = new Dictionary<Verdict, int>();
            foreach (var q in quests)
            {
                var v = Grade(q, locs, env, quiet);
                tally[v] = tally.GetValueOrDefault(v) + 1;
            }
            Console.WriteLine();
            Console.WriteLine($"  SUMMARY over {quests.Count}: CANNOT START {tally.GetValueOrDefault(Verdict.Cannot)} · "
                              + $"UNKNOWN {tally.GetValueOrDefault(Verdict.Unknown)} · CAN START IF {tally.GetValueOrDefault(Verdict.CanIf)} · "
                              + $"no runtime terms {tally.GetValueOrDefault(Verdict.Ok)}");
            return tally.GetValueOrDefault(Verdict.Cannot) > 0 ? 2 : 0;
        }

        private static string Name(FormKey fk, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env)
            => env.LinkCache.TryResolve<IMajorRecordGetter>(fk, out var r) ? (r.EditorID ?? fk.ToString()) : fk.ToString();

        private static uint RawFlags(object flags) => Convert.ToUInt32(flags);

        private static Verdict Grade(IQuestGetter q, List<Loc> locs,
                                     IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, bool quiet)
        {
            var cannot = new List<string>();
            var unknown = new List<string>();
            var runtime = new List<string>();
            var info = new List<string>();

            var aliases = q.Aliases?.ToList() ?? new List<IAQuestAliasGetter>();
            var pos = new Dictionary<uint, int>();
            var byId = new Dictionary<uint, IAQuestAliasGetter>();
            var refs = new List<IQuestReferenceAliasGetter>();
            for (int i = 0; i < aliases.Count; i++)
            {
                switch (aliases[i])
                {
                    case IQuestReferenceAliasGetter r: pos[r.ID] = i; byId[r.ID] = r; refs.Add(r); break;
                    case IQuestLocationAliasGetter l: pos[l.ID] = i; byId[l.ID] = l; break;
                    case IQuestCollectionAliasGetter c:
                        // A collection has no ID of its own: its members carry theirs (SpaceCellRefs is ID
                        // 10 INSIDE one). Each member takes the collection's list position.
                        if (c.Collection != null) foreach (var m in c.Collection)
                            if (m.ReferenceAlias != null) { pos[m.ReferenceAlias.ID] = i; byId[m.ReferenceAlias.ID] = m.ReferenceAlias; refs.Add(m.ReferenceAlias); }
                        break;
                    default: unknown.Add($"alias #{i} is a {aliases[i].GetType().Name}, a kind this tool does not read"); break;
                }
            }

            string Label(uint id) => byId.TryGetValue(id, out var a) ? $"{id} {AliasName(a)}" : $"{id} (?)";

            // --- R1 + R2: every alias-to-alias dependency ------------------------------------------
            var inPlace = new Dictionary<uint, List<(uint refAlias, FormKey refType, bool optional)>>();
            foreach (var a in refs)
            {
                uint f = RawFlags(a.Flags);
                bool optional = (f & OptionalBit) != 0;
                // The control on the bit: the enum's own word must agree with it, every alias, or refuse.
                if (optional != a.Flags.ToString().Contains("Optional"))
                    throw new InvalidOperationException($"{q.EditorID} alias {a.ID}: Optional bit 0x2 disagrees with the flag enum '{a.Flags}'. Refusing to grade on a guessed bit.");

                // A non-optional alias with no fill this tool recognises is UNKNOWN, never a silent pass.
                bool anyFill = a.Location != null || a.CreateReferenceToObject != null || a.ReferenceCollectionAliasID != null
                               || !a.ForcedReference.IsNull || !a.UniqueActor.IsNull || !a.UniqueBaseForm.IsNull || a.External != null;
                if (a.FindMatchingRefFromEvent != null) { runtime.Add($"alias {Label(a.ID)} fills from a story-manager EVENT"); anyFill = true; }
                if (!anyFill && !optional) unknown.Add($"alias {Label(a.ID)} is not Optional and carries no fill this tool recognises");

                if (a.Location != null)
                {
                    if (!inPlace.TryGetValue((uint)a.Location.AliasID, out var lst)) inPlace[(uint)a.Location.AliasID] = lst = new();
                    lst.Add((a.ID, a.Location.RefType.FormKey, optional));
                }
                var deps = DepsWithHow(a);

                foreach (var (dep, how) in deps)
                {
                    string line = $"alias {Label(a.ID)} {how} alias {dep}";
                    if (!pos.ContainsKey(dep))
                    {
                        (optional ? unknown : cannot).Add($"R2 {line}, and no alias {dep} exists{(optional ? " (the alias is Optional)" : "")}");
                        continue;
                    }
                    if (dep == a.ID) { unknown.Add($"R1 {line}, which is ITSELF"); continue; }
                    if (pos[dep] > pos[a.ID])
                        (optional ? unknown : cannot).Add($"R1 {line} = {AliasName(byId[dep])}, which is listed BELOW it "
                                                          + $"(position {pos[dep]} vs {pos[a.ID]}), so it is filled AFTER the thing that needs it"
                                                          + (optional ? " (the alias is Optional)" : ""));
                    else info.Add($"R1 ok   {line}");
                }
            }

            // --- R3 + R4: each location alias's static pool, then the joint pool ----------------------
            foreach (var la in aliases.OfType<IQuestLocationAliasGetter>())
            {
                bool optional = (RawFlags(la.Flags) & OptionalBit) != 0;
                if (!la.SpecificLocation.IsNull) { info.Add($"R3 place {Label(la.ID)} is PINNED to {Name(la.SpecificLocation.FormKey, env)}"); continue; }
                if (la.ALPS != null) { unknown.Add($"R3 place {Label(la.ID)} fills through a PCM request (ALPS), which this tool does not evaluate"); continue; }

                var req = new List<(string what, Func<Loc, bool> test)>();
                bool evaluable = true;
                if (!la.LocationTypeKeyword.IsNull)
                {
                    var k = la.LocationTypeKeyword.FormKey;
                    req.Add(($"type keyword {Name(k, env)}", l => l.Keywords.Contains(k)));
                }
                foreach (var c in la.Conditions ?? Enumerable.Empty<IConditionGetter>())
                {
                    if (((byte)c.Flags & OrFlag) != 0)
                    {
                        unknown.Add($"R3 place {Label(la.ID)} has an OR-flagged condition ({c.Data?.GetType().Name}); this tool evaluates AND chains only, so the pool is not graded");
                        evaluable = false; break;
                    }
                    float want = c is IConditionFloatGetter cf ? cf.ComparisonValue : float.NaN;
                    string rhs = c is IConditionGlobalGetter cg ? "global:" + Name(cg.ComparisonValue.FormKey, env)
                               : float.IsNaN(want) ? "?" : want.ToString("0.##");
                    bool eqOp = c.CompareOperator == CompareOperator.EqualTo;
                    switch (c.Data)
                    {
                        case ILocationHasRefTypeConditionDataGetter d when eqOp && (want == 1f || want == 0f) && IsSubject(d):
                        {
                            var fk = d.FirstParameter.Link.FormKey; bool has = want == 1f;
                            req.Add(($"{(has ? "" : "NOT ")}ref type {Name(fk, env)}", l => l.RefTypes.Contains(fk) == has));
                            break;
                        }
                        case ILocationHasKeywordConditionDataGetter d when eqOp && (want == 1f || want == 0f) && IsSubject(d):
                        {
                            var fk = d.FirstParameter.Link.FormKey; bool has = want == 1f;
                            req.Add(($"{(has ? "" : "NOT ")}keyword {Name(fk, env)}", l => l.Keywords.Contains(fk) == has));
                            break;
                        }
                        default:
                            runtime.Add($"place {Label(la.ID)}: {c.Data?.GetType().Name?.Replace("ConditionData", "")} {c.CompareOperator} "
                                        + $"{rhs} -- not evaluable from records");
                            break;
                    }
                }
                if (!evaluable) continue;
                if (req.Count == 0)
                {
                    (optional ? info : unknown).Add($"R3 place {Label(la.ID)} has no static term at all, so its pool is every location and says nothing");
                    continue;
                }

                var pool = locs.Where(l => req.All(r => r.test(l))).ToList();
                info.Add($"R3 place {Label(la.ID)}: {string.Join(" + ", req.Select(r => r.what))} -> {pool.Count} location(s)");
                if (pool.Count == 0)
                {
                    (optional ? unknown : cannot).Add($"R3 place {Label(la.ID)} matches ZERO locations on its static terms"
                                                      + (optional ? " (the alias is Optional)" : ""));
                    continue;
                }

                if (!inPlace.TryGetValue(la.ID, out var wants)) continue;
                var joint = pool;
                foreach (var (refAlias, rt, opt) in wants)
                {
                    int alone = pool.Count(l => l.RefTypes.Contains(rt));
                    info.Add($"R4   alias {Label(refAlias)} wants {Name(rt, env)} inside it: {alone} of {pool.Count}{(opt ? " (Optional)" : "")}");
                    if (!opt) joint = joint.Where(l => l.RefTypes.Contains(rt)).ToList();
                }
                info.Add($"R4 place {Label(la.ID)} JOINT pool (every non-optional in-place marker at the same location): {joint.Count}");
                if (joint.Count == 0)
                    (optional ? unknown : cannot).Add($"R4 place {Label(la.ID)}: no location satisfies its own terms AND carries every marker its aliases fill from");
            }

            var v = cannot.Count > 0 ? Verdict.Cannot : unknown.Count > 0 ? Verdict.Unknown : runtime.Count > 0 ? Verdict.CanIf : Verdict.Ok;
            string word = v switch { Verdict.Cannot => "CANNOT START", Verdict.Unknown => "UNKNOWN", Verdict.CanIf => "CAN START IF", _ => "no runtime terms" };
            Console.WriteLine();
            Console.WriteLine($"  {word,-16} {q.EditorID}  [{q.FormKey}]  ({aliases.Count} aliases)");
            if (quiet && v != Verdict.Cannot) return v;
            foreach (var s in cannot) Console.WriteLine("    [CANNOT ] " + s);
            foreach (var s in unknown) Console.WriteLine("    [UNKNOWN] " + s);
            if (quiet) return v;
            foreach (var s in runtime) Console.WriteLine("    [runtime] " + s);
            foreach (var s in info) Console.WriteLine("    [  read ] " + s);
            return v;
        }

        // ---------------------------------------------------------------- shared with gen_delve
        // ONE definition of "depends on", used by the linter's R1 and by the generator's ordering, so
        // the two cannot drift: a second copy of a working check is a banked scar.

        /// The alias IDs a list ENTRY carries: one for a ref or location alias, one per member for a
        /// collection (which has no ID of its own).
        public static List<uint> IdsOf(IAQuestAliasGetter a) => a switch
        {
            IQuestReferenceAliasGetter r => new List<uint> { r.ID },
            IQuestLocationAliasGetter l => new List<uint> { l.ID },
            IQuestCollectionAliasGetter c => c.Collection?.Where(m => m.ReferenceAlias != null)
                                                 .Select(m => m.ReferenceAlias!.ID).ToList() ?? new List<uint>(),
            _ => new List<uint>(),
        };

        /// The alias IDs one ref alias fills FROM: in-location (ALLA), create-at (ALCA), from-collection.
        public static List<uint> DepsOf(IQuestReferenceAliasGetter a) => DepsWithHow(a).Select(d => d.dep).ToList();

        private static List<(uint dep, string how)> DepsWithHow(IQuestReferenceAliasGetter a)
        {
            var d = new List<(uint, string)>();
            if (a.Location != null) d.Add(((uint)a.Location.AliasID, "fills inside"));
            if (a.CreateReferenceToObject != null) d.Add(((uint)a.CreateReferenceToObject.AliasID, "is created at"));
            if (a.ReferenceCollectionAliasID != null) d.Add(((uint)a.ReferenceCollectionAliasID.Value, "fills from the collection of"));
            return d;
        }

        private static List<uint> EntryDeps(IAQuestAliasGetter a) => a switch
        {
            IQuestReferenceAliasGetter r => DepsOf(r),
            IQuestCollectionAliasGetter c => c.Collection?.Where(m => m.ReferenceAlias != null)
                                                 .SelectMany(m => DepsOf(m.ReferenceAlias!)).ToList() ?? new List<uint>(),
            _ => new List<uint>(),
        };

        /// R1 alone, as data: every (alias, depends-on) pair where the dependency is listed BELOW.
        public static List<(uint alias, uint dep)> ForwardRefs(IReadOnlyList<IAQuestAliasGetter> list)
        {
            var pos = new Dictionary<uint, int>();
            for (int i = 0; i < list.Count; i++) foreach (var id in IdsOf(list[i])) pos[id] = i;
            var outp = new List<(uint, uint)>();
            for (int i = 0; i < list.Count; i++)
                foreach (var dep in EntryDeps(list[i]))
                    if (pos.TryGetValue(dep, out var p) && p > i)
                        foreach (var id in IdsOf(list[i])) outp.Add((id, dep));
            return outp;
        }

        /// A STABLE topological order: every entry after everything it fills from, and otherwise the
        /// original order untouched. Returns null on a cycle rather than guessing a way out of one.
        /// Only POSITION changes; IDs are what stages, objectives and script properties reference.
        public static List<T>? DependencyOrder<T>(IReadOnlyList<T> list) where T : IAQuestAliasGetter
        {
            var present = list.SelectMany(a => IdsOf(a)).ToHashSet();
            var placed = new HashSet<uint>();
            var remaining = list.ToList();
            var outp = new List<T>();
            while (remaining.Count > 0)
            {
                int k = remaining.FindIndex(a =>
                {
                    var own = IdsOf(a);
                    return EntryDeps(a).All(d => !present.Contains(d) || placed.Contains(d) || own.Contains(d));
                });
                if (k < 0) return null;
                foreach (var id in IdsOf(remaining[k])) placed.Add(id);
                outp.Add(remaining[k]);
                remaining.RemoveAt(k);
            }
            return outp;
        }

        private static bool IsSubject(object data)
        {
            // WHOSE location is tested. On a location alias the subject is the candidate; any other
            // RunOn is a different question and is left to runtime rather than graded as this one.
            var p = data.GetType().GetProperty("RunOnType");
            return p == null || (p.GetValue(data)?.ToString() ?? "Subject") == "Subject";
        }

        private static string AliasName(IAQuestAliasGetter a) => a switch
        {
            IQuestReferenceAliasGetter r => r.Name ?? "",
            IQuestLocationAliasGetter l => l.Name ?? "",
            _ => "",
        };
    }
}
