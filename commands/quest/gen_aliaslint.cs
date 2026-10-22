using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Retrograde.Nouns.QuestAuthoring;
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
    ///   R5 leash anchor -- a place's GetDistance that takes an ALIAS parameter must name an alias that
    ///                     exists, is listed above the place, and is not filled inside that same place.
    ///                     UNKNOWN when it fails (such quests start; their distance is not the promised one).
    ///   R6 dynamite    -- every NON-OPTIONAL alias must have a job: an objective, a script, a script on it, the
    ///                     quest's text, or an alias with a job leaning on it. His frame, 2026-10-22: "Every none
    ///                     optional alias is a stick of dynamite. You need to account for them all on every quest."
    ///                     Rung S12 carried required aliases nothing used and did not start; S13 without them did.
    ///                     Printed as [DYNAMITE] and counted beside the verdict; it does not change the verdict,
    ///                     because "no job" is a risk the records show, not a proof it cannot start. ⚠ Reads this
    ///                     QUST only: a use in a scene, a dialogue condition or another quest is not seen
    ///                     (AliasGraph.UsesOf), so a vanilla quest can show dynamite that has a job elsewhere.
    ///
    /// ⚠ STATED LIMIT: whether conditions evaluate up the location hierarchy (ParentLocation) is NOT
    /// established (manual part 32). Pools here count a location's OWN references and keywords, so a
    /// zero pool is a zero under that reading.
    ///
    /// "Depends on" itself lives in Retrograde.Library's AliasGraph (split step 3, 2026-10-15), shared with
    /// gen_delve's ordering; this command owns the load-order read, the five rules and the words.
    /// gen_aliaslint selftest grades AliasGraph with no plugin loaded and exits 1 on any failure.
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
                Console.WriteLine("       gen_aliaslint selftest   grades AliasGraph, no plugin loaded, exit 1 on failure.");
                return 1;
            }
            // Before the environment: it needs no plugin, and booting the load order to check a graph
            // would make nobody run it.
            if (search.Equals("selftest", StringComparison.OrdinalIgnoreCase)) return SelfTest();

            using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();

            var locs = new List<Loc>();
            foreach (var l in env.LoadOrder.PriorityOrder.WinningOverrides<ILocationGetter>())
            {
                var x = new Loc { Edid = l.EditorID ?? l.FormKey.ToString() };
                // ⛔ BOTH special-reference lists, unioned: reading only the master list under-reports,
                // in the direction that makes a pool look smaller (Retrograde.Library's PoiCensus.Pool, same rule).
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
            int dynamiteQuests = 0, dynamite = 0;
            foreach (var q in quests)
            {
                var (v, d) = Grade(q, locs, env, quiet);
                tally[v] = tally.GetValueOrDefault(v) + 1;
                if (d > 0) { dynamiteQuests++; dynamite += d; }
            }
            Console.WriteLine();
            Console.WriteLine($"  SUMMARY over {quests.Count}: CANNOT START {tally.GetValueOrDefault(Verdict.Cannot)} · "
                              + $"UNKNOWN {tally.GetValueOrDefault(Verdict.Unknown)} · CAN START IF {tally.GetValueOrDefault(Verdict.CanIf)} · "
                              + $"no runtime terms {tally.GetValueOrDefault(Verdict.Ok)}");
            Console.WriteLine($"  R6 DYNAMITE: {dynamite} required alias(es) with no job, in {dynamiteQuests} of {quests.Count} quest(s)");
            return tally.GetValueOrDefault(Verdict.Cannot) > 0 ? 2 : 0;
        }

        private static string Name(FormKey fk, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env)
            => env.LinkCache.TryResolve<IMajorRecordGetter>(fk, out var r) ? (r.EditorID ?? fk.ToString()) : fk.ToString();

        private static uint RawFlags(object flags) => Convert.ToUInt32(flags);

        private static (Verdict, int dynamite) Grade(IQuestGetter q, List<Loc> locs,
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
                foreach (var (dep, how) in AliasGraph.DepsOf(a))
                {
                    string line = $"alias {Label(a.ID)} {Words(how)} alias {dep}";
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

            // --- R5: leash anchors. A GetDistance whose parameter is an ALIAS NUMBER (UseAliases) measures from
            // that alias, and the alias must exist, be listed above the place, and not be filled INSIDE the place it
            // leashes (2026-10-13: six built Delves measured their second place from its own marker, and 11a from a
            // removed alias; all of them started and played, so this is UNKNOWN, never CANNOT: the distance the
            // record promises is not the one the engine can read).
            foreach (var la in aliases.OfType<IQuestLocationAliasGetter>())
                foreach (var c in la.Conditions ?? new List<IConditionGetter>())
                {
                    if (c.Data is not IGetDistanceConditionDataGetter d || !d.UseAliases || !AliasGraph.IsSubject(d)) continue;
                    if (d.FirstParameter.Index is not uint anchor) { unknown.Add($"R5 place {Label(la.ID)}'s GetDistance takes an alias parameter and carries no alias number"); continue; }
                    string line = $"R5 place {Label(la.ID)}'s GetDistance measures from alias {anchor}";
                    if (!byId.TryGetValue(anchor, out var an)) unknown.Add($"{line}, which does not exist");
                    else if (pos[anchor] > pos[la.ID]) unknown.Add($"{line} = {AliasName(an)}, listed BELOW the place, so it is unfilled when the leash is read");
                    else if (an is IQuestReferenceAliasGetter ar && ar.Location != null && (uint)ar.Location.AliasID == la.ID)
                        unknown.Add($"{line} = {AliasName(an)}, which fills INSIDE that same place, so the leash measures from itself");
                    else info.Add($"{line} = {AliasName(an)}");
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
                        case ILocationHasRefTypeConditionDataGetter d when eqOp && (want == 1f || want == 0f) && AliasGraph.IsSubject(d):
                        {
                            var fk = d.FirstParameter.Link.FormKey; bool has = want == 1f;
                            req.Add(($"{(has ? "" : "NOT ")}ref type {Name(fk, env)}", l => l.RefTypes.Contains(fk) == has));
                            break;
                        }
                        case ILocationHasKeywordConditionDataGetter d when eqOp && (want == 1f || want == 0f) && AliasGraph.IsSubject(d):
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

            // --- R6: every stick of dynamite accounted for --------------------------------------------
            var uses = AliasGraph.UsesOf(q);
            var acc = AliasGraph.Accounted(uses);
            var dyn = AliasGraph.Unaccounted(q);
            foreach (var id in byId.Keys.OrderBy(x => x).Where(acc.Contains))
            {
                var first = uses.Where(x => x.Used == id).OrderBy(x => AliasGraph.IsTerminal(x.Kind) ? 0 : 1).First();
                info.Add($"R6 ok   alias {Label(id)}: {UseWords(first, Label)}");
            }

            var v = cannot.Count > 0 ? Verdict.Cannot : unknown.Count > 0 ? Verdict.Unknown : runtime.Count > 0 ? Verdict.CanIf : Verdict.Ok;
            string word = v switch { Verdict.Cannot => "CANNOT START", Verdict.Unknown => "UNKNOWN", Verdict.CanIf => "CAN START IF", _ => "no runtime terms" };
            Console.WriteLine();
            Console.WriteLine($"  {word,-16} {q.EditorID}  [{q.FormKey}]  ({aliases.Count} aliases; R6 dynamite {dyn.Count})");
            foreach (var (id, name) in dyn)
                Console.WriteLine($"    [DYNAMITE] R6 alias {id} {name} is required and nothing in this quest gives it a job"
                                  + (uses.Any(x => x.Used == id) ? $" (only {string.Join(", ", uses.Where(x => x.Used == id).Select(x => UseWords(x, Label)))}, none of which has one)" : ""));
            if (quiet && v != Verdict.Cannot) return (v, dyn.Count);
            foreach (var s in cannot) Console.WriteLine("    [CANNOT ] " + s);
            foreach (var s in unknown) Console.WriteLine("    [UNKNOWN] " + s);
            if (quiet) return (v, dyn.Count);
            foreach (var s in runtime) Console.WriteLine("    [runtime] " + s);
            foreach (var s in info) Console.WriteLine("    [  read ] " + s);
            return (v, dyn.Count);
        }

        /// The CLI's words for one use; AliasGraph hands back the kind and the data.
        private static string UseWords(AliasUse x, Func<uint, string> label) => x.Kind switch
        {
            UseKind.FillsInside => $"alias {label(x.ByAlias!.Value)} fills inside it",
            UseKind.CreatedAt => $"alias {label(x.ByAlias!.Value)} is created at it",
            UseKind.FromCollection => $"alias {label(x.ByAlias!.Value)} fills from its collection",
            UseKind.Leash => $"alias {label(x.ByAlias!.Value)} measures its leash from it",
            UseKind.SameLocation => $"alias {label(x.ByAlias!.Value)} is tested against it",
            UseKind.Parent => $"alias {label(x.ByAlias!.Value)} is parented to it",
            UseKind.SystemLocation => $"alias {label(x.ByAlias!.Value)} searches its star system",
            UseKind.Linked => $"alias {label(x.ByAlias!.Value)} links to it",
            UseKind.ClosestTo => $"alias {label(x.ByAlias!.Value)} fills closest to it",
            UseKind.Objective => $"objective {x.Where} targets it",
            UseKind.ScriptProperty => $"script property {x.Where} holds it",
            UseKind.AliasScript => $"it wears {x.Where}",
            UseKind.TextToken => $"the text says {x.Where}",
            _ => throw new ArgumentOutOfRangeException(nameof(x), x.Kind, "a UseKind with no words"),
        };

        /// The CLI's words for a dependency; AliasGraph hands back the kind, never a sentence.
        private static string Words(FillFrom how) => how switch
        {
            FillFrom.Inside => "fills inside",
            FillFrom.CreatedAt => "is created at",
            FillFrom.FromCollection => "fills from the collection of",
            FillFrom.Leash => "measures its leash from",
            _ => throw new ArgumentOutOfRangeException(nameof(how), how, "a FillFrom with no words"),
        };

        /// <summary>
        /// gen_aliaslint selftest: AliasGraph graded on hand-built aliases, no plugin loaded. Each case is
        /// the shape of a real build: a marker listed above the place it fills inside (duo_delve04), a place
        /// leashed from a marker that fills inside it (18a, his 2026-10-15 ruling), and controls that must
        /// NOT move or refuse. Exit 1 on any failure, so it can gate.
        /// </summary>
        private static int SelfTest()
        {
            int fail = 0;
            void Check(string label, bool ok, string detail)
            {
                Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + label + "   " + detail);
                if (!ok) fail++;
            }
            string Ids(IEnumerable<IAQuestAliasGetter>? xs) => xs == null ? "null" : string.Join(",", xs.SelectMany(AliasGraph.IdsOf));

            QuestReferenceAlias MarkerIn(uint id, uint place) => new() { ID = id, Location = new LocationAliasReference { AliasID = (int)place } };
            QuestLocationAlias Place(uint id, uint? leashFrom = null, bool subject = true)
            {
                var l = new QuestLocationAlias { ID = id, Conditions = new Noggog.ExtendedList<Condition>() };
                if (leashFrom is uint anchor)
                {
                    var d = new GetDistanceConditionData();
                    d.UseAliases = true;
                    d.FirstParameter.Index = anchor;
                    if (!subject) d.RunOnType = Condition.RunOnType.Reference;
                    l.Conditions.Add(new ConditionFloat { Data = d, CompareOperator = CompareOperator.LessThan, ComparisonValue = 100f });
                }
                return l;
            }

            // 1. A marker listed ABOVE the place it fills inside: one forward reference, and the order moves the place up.
            var fwd = new List<IAQuestAliasGetter> { MarkerIn(1, 2), Place(2) };
            var f1 = AliasGraph.ForwardRefs(fwd);
            Check("forward ref is found", f1.Count == 1 && f1[0] == (1u, 2u), string.Join(" ", f1.Select(x => $"{x.alias}->{x.dep}")));
            var o1 = AliasGraph.DependencyOrder(fwd);
            Check("forward ref is reordered", !o1.Cycle && Ids(o1.Order) == "2,1" && o1.Unplaced.Count == 0, $"order {Ids(o1.Order)}");

            // 2. CONTROL: already in order. Nothing forward, nothing moves, the same objects come back.
            var ok = new List<IAQuestAliasGetter> { Place(2), MarkerIn(1, 2) };
            var o2 = AliasGraph.DependencyOrder(ok);
            Check("ordered list is untouched", AliasGraph.ForwardRefs(ok).Count == 0 && !o2.Cycle
                  && o2.Order!.Select((a, i) => ReferenceEquals(a, ok[i])).All(x => x), $"order {Ids(o2.Order)}");

            // 3. A place leashed from the marker that fills inside it: no order exists, and both are named.
            var cyc = new List<IAQuestAliasGetter> { Place(2, leashFrom: 1), MarkerIn(1, 2), Place(5) };
            var o3 = AliasGraph.DependencyOrder(cyc);
            Check("leash cycle refuses", o3.Cycle && o3.Order == null, o3.Cycle ? "refused" : $"ORDERED {Ids(o3.Order)}");
            Check("leash cycle names its aliases", o3.Unplaced.OrderBy(x => x).SequenceEqual(new uint[] { 1, 2 }),
                  "unplaced " + string.Join(",", o3.Unplaced));

            // 4. CONTROL on IsSubject: the same leash run on a REFERENCE, not the candidate, is not a fill dependency.
            var off = new List<IAQuestAliasGetter> { Place(2, leashFrom: 1, subject: false), MarkerIn(1, 2) };
            var o4 = AliasGraph.DependencyOrder(off);
            Check("non-subject leash is not a dependency", !o4.Cycle && Ids(o4.Order) == "2,1", $"order {Ids(o4.Order)}");

            // 5. Each kind comes back as a kind, never as words.
            var made = new QuestReferenceAlias { ID = 7, Location = new LocationAliasReference { AliasID = 2 },
                                                 CreateReferenceToObject = new CreateReferenceToObject { AliasID = 3 },
                                                 ReferenceCollectionAliasID = 4 };
            var kinds = AliasGraph.DepsOf(made);
            Check("DepsOf returns every kind", kinds.SequenceEqual(new[] { (2u, FillFrom.Inside), (3u, FillFrom.CreatedAt), (4u, FillFrom.FromCollection) }),
                  string.Join(" ", kinds.Select(k => $"{k.dep}:{k.how}")));

            // 6 to 10. R6, on hand-built quests in the shapes of the space ladder's rungs.
            Quest NewQuest(params AQuestAlias[] aliases)
            {
                var q = new Quest(FormKey.Factory("000800:R6SelfTest.esm"), StarfieldRelease.Starfield);
                q.Aliases = new Noggog.ExtendedList<AQuestAlias>(aliases);
                return q;
            }
            void Target(Quest q, int alias) { var o = new QuestObjective { Index = 10 }; o.Targets.Add(new QuestObjectiveTarget { AliasID = alias }); q.Objectives.Add(o); }
            QuestCollectionAlias Coll(QuestReferenceAlias member) { var c = new QuestCollectionAlias(); c.Collection.Add(new CollectionAlias { ID = member.ID, ReferenceAlias = member }); return c; }
            string Dyn(Quest q) => string.Join(",", AliasGraph.Unaccounted(q).Select(x => x.id).OrderBy(x => x));

            // 6. S13's shape: orbit 2 parented to Player 17, a cell created at 2, a marker from the cell, the object at the
            // marker, the objective on the object. Every link reaches the objective, so nothing is dynamite.
            var s13 = NewQuest(new QuestReferenceAlias { ID = 17, Name = "Player" },
                               new QuestLocationAlias { ID = 2, ParentSystemLocationAliasID = 17 },
                               Coll(new QuestReferenceAlias { ID = 10, CreateReferenceToObject = new CreateReferenceToObject { AliasID = 2 } }),
                               new QuestReferenceAlias { ID = 12, ReferenceCollectionAliasID = 10 },
                               new QuestReferenceAlias { ID = 3, CreateReferenceToObject = new CreateReferenceToObject { AliasID = 12 } });
            Target(s13, 3);
            Check("R6: S13's chain is all accounted", Dyn(s13) == "", "dynamite [" + Dyn(s13) + "]");

            // 7. S12's tail: a system over the orbit, a cell created at the system, a marker from that cell, and nothing
            // pointing at the marker. The WHOLE chain is dynamite, not only its end, and the S13 core stays accounted.
            var s12 = NewQuest(new QuestReferenceAlias { ID = 17, Name = "Player" },
                               new QuestLocationAlias { ID = 2, ParentSystemLocationAliasID = 17 },
                               Coll(new QuestReferenceAlias { ID = 10, CreateReferenceToObject = new CreateReferenceToObject { AliasID = 2 } }),
                               new QuestReferenceAlias { ID = 12, ReferenceCollectionAliasID = 10 },
                               new QuestReferenceAlias { ID = 3, CreateReferenceToObject = new CreateReferenceToObject { AliasID = 12 } },
                               new QuestLocationAlias { ID = 8, SystemLocationAliasID = 2 },
                               Coll(new QuestReferenceAlias { ID = 14, CreateReferenceToObject = new CreateReferenceToObject { AliasID = 8 } }),
                               new QuestReferenceAlias { ID = 15, ReferenceCollectionAliasID = 14 },
                               new QuestLocationAlias { ID = 0 });
            Target(s12, 3);
            Check("R6: S12's unused chain and its lone system are dynamite", Dyn(s12) == "0,8,14,15", "dynamite [" + Dyn(s12) + "]");

            // 8. CONTROL: the same unused alias marked Optional is not dynamite (an optional alias cannot stop the start).
            var opt = NewQuest(new QuestLocationAlias { ID = 0, Flags = (QuestLocationAlias.Flag)0x2 }, new QuestReferenceAlias { ID = 3 });
            Target(opt, 3);
            Check("R6: an unused Optional alias is not dynamite", Dyn(opt) == "", "dynamite [" + Dyn(opt) + "]");

            // 9. Terminal uses other than an objective: a token in the quest's text, and a link from an alias with a job.
            var txt = NewQuest(new QuestLocationAlias { ID = 2, Name = "TargetPlanetLocation" },
                               new QuestReferenceAlias { ID = 17, Name = "Patrol" },
                               new QuestReferenceAlias { ID = 3, LinkedAliases = new Noggog.ExtendedList<LinkedAlias> { new LinkedAlias { AliasID = 17 } } });
            txt.Name = "Repair it at <Alias=TargetPlanetLocation>";
            Target(txt, 3);
            Check("R6: a text token and a link from a used alias both count", Dyn(txt) == "", "dynamite [" + Dyn(txt) + "]");

            // 10. CONTROL on the direction of a link: a used alias linked FROM an unused one gains nothing for the unused one.
            var back = NewQuest(new QuestReferenceAlias { ID = 3 },
                                new QuestReferenceAlias { ID = 5, LinkedAliases = new Noggog.ExtendedList<LinkedAlias> { new LinkedAlias { AliasID = 3 } } });
            Target(back, 3);
            Check("R6: linking TO a used alias is not a job", Dyn(back) == "5", "dynamite [" + Dyn(back) + "]");

            Console.WriteLine(fail == 0 ? "  selftest: all passed" : $"  selftest: {fail} FAILED");
            return fail == 0 ? 0 : 1;
        }

        private static string AliasName(IAQuestAliasGetter a) => a switch
        {
            IQuestReferenceAliasGetter r => r.Name ?? "",
            IQuestLocationAliasGetter l => l.Name ?? "",
            _ => "",
        };
    }
}
