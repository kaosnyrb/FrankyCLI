using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Nouns.QuestAuthoring
{
    /// <summary>How one alias depends on another: it fills inside it (ALLA), is created at it (ALCA),
    /// fills from its collection, or (a place) measures its GetDistance leash from it.</summary>
    public enum FillFrom { Inside, CreatedAt, FromCollection, Leash }

    /// <summary>
    /// How one alias USES another, or how the quest uses it. The first nine are an alias leaning on another (an EDGE: the
    /// used alias has a job only if the user has one); the last four are the quest's own jobs (TERMINAL: an objective points
    /// at it, a script holds it, it wears a script, or the quest's text names it).
    /// </summary>
    public enum UseKind { FillsInside, CreatedAt, FromCollection, Leash, SameLocation, Parent, SystemLocation, Linked, ClosestTo,
                          Objective, ScriptProperty, AliasScript, TextToken }

    /// <summary>One use of alias <see cref="Used"/>: by alias <see cref="ByAlias"/> for an edge, and <see cref="Where"/> the
    /// objective index, property name, script names or token for a terminal. Data, never a sentence.</summary>
    public sealed record AliasUse(uint Used, UseKind Kind, uint? ByAlias, string? Where);

    /// <summary>
    /// The answer to "put every alias below everything it fills from". <see cref="Order"/> is null when no
    /// order exists, and then <see cref="Unplaced"/> names the alias IDs left when no further entry could go:
    /// the cycle is among them, alongside anything that only depends on it.
    /// </summary>
    public sealed record AliasOrder<T>(IReadOnlyList<T>? Order, IReadOnlyList<uint> Unplaced)
    {
        public bool Cycle => Order == null;
    }

    /// <summary>
    /// THE ALIAS FILL GRAPH: which alias fills from which, and the list order that lets every one fill.
    ///
    /// The engine fills aliases in LIST order, so an alias that fills from another must be listed below it.
    /// Evidence, 2026-09-24: vanilla Starfield.esm, 2,092 quests, 7,731 such references, ZERO forward; the only
    /// two forward references in du_overtime were the two quests that did not start. ⚠ Corpus evidence, not an
    /// engine document. A quest that cannot fill a non-optional alias silently does not start.
    ///
    /// ONE definition of "depends on", used by the alias lint's R1 and R5 and by every builder's ordering, so
    /// the grader and the generator cannot drift: a second copy of a working check is a banked scar.
    ///
    /// ⚠ A LIBRARY PIECE: it returns what it found and never prints or returns exit codes, because a UI as well
    /// as a CLI calls it (his 2026-10-15 ruling, option B). The caller owns the words. Lifted out of FrankyCLI's
    /// gen_aliaslint on 2026-10-15, split step 3. It reads Mutagen's alias getters because both of its callers
    /// hold Mutagen objects today; it moves onto gen_questmodel's model when the build rules do.
    /// </summary>
    public static class AliasGraph
    {
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

        /// The alias IDs one ref alias fills FROM, and how: in-location (ALLA), create-at (ALCA), from-collection.
        public static List<(uint dep, FillFrom how)> DepsOf(IQuestReferenceAliasGetter a)
        {
            var d = new List<(uint, FillFrom)>();
            if (a.Location != null) d.Add(((uint)a.Location.AliasID, FillFrom.Inside));
            if (a.CreateReferenceToObject != null) d.Add(((uint)a.CreateReferenceToObject.AliasID, FillFrom.CreatedAt));
            if (a.ReferenceCollectionAliasID != null) d.Add(((uint)a.ReferenceCollectionAliasID.Value, FillFrom.FromCollection));
            return d;
        }

        /// The alias IDs a place MEASURES FROM: a GetDistance on the candidate whose parameter is an alias
        /// number. The leash is read when the place fills, so its anchor is a fill dependency exactly as an
        /// in-location alias is (his ruling, 2026-10-15, on 18a: a recover beat's new marker was appended below
        /// the third place that must be held near it, and ordering did not know).
        public static List<uint> LeashDepsOf(IQuestLocationAliasGetter l) =>
            (l.Conditions ?? new List<IConditionGetter>())
                .Select(c => c.Data).OfType<IGetDistanceConditionDataGetter>()
                .Where(d => d.UseAliases && IsSubject(d) && d.FirstParameter.Index is uint)
                .Select(d => (uint)d.FirstParameter.Index!).ToList();

        /// The alias IDs a place is tested AGAINST: IsInSameCurrentLocationAsAlias on the candidate names another alias by
        /// number, and is read when the place fills, so that alias must fill first, exactly as a leash's anchor
        /// (2026-10-22, gen_delve's orbit place: the orbit over the MAIN place's planet).
        public static List<uint> SameLocationDepsOf(IQuestLocationAliasGetter l) =>
            (l.Conditions ?? new List<IConditionGetter>())
                .Select(c => c.Data).OfType<IIsInSameCurrentLocationAsAliasConditionDataGetter>()
                .Where(IsSubject)
                .Select(d => (uint)d.FirstParameter).ToList();

        /// The alias a location alias is PARENTED to (ParentSystemLocationAliasID): "the location of my type over that alias's
        /// location", e.g. a planet over the main place, an orbit over the player's ship (SE_Player_*) or a settlement's trigger
        /// (RQ_Settlement_KillShips_01). Read when the place fills, so a fill dependency: in his load order 1,283 of 1,289
        /// such links point ABOVE the alias in the list (2026-10-22, before gen_delve's space kind first parented an orbit).
        public static List<uint> ParentDepsOf(IQuestLocationAliasGetter l) =>
            l.ParentSystemLocationAliasID is int p && p >= 0 ? new List<uint> { (uint)p } : new List<uint>();

        private static List<uint> EntryDeps(IAQuestAliasGetter a) => a switch
        {
            IQuestReferenceAliasGetter r => DepsOf(r).Select(d => d.dep).ToList(),
            IQuestLocationAliasGetter l => LeashDepsOf(l).Concat(SameLocationDepsOf(l)).Concat(ParentDepsOf(l)).ToList(),
            IQuestCollectionAliasGetter c => c.Collection?.Where(m => m.ReferenceAlias != null)
                                                 .SelectMany(m => DepsOf(m.ReferenceAlias!).Select(d => d.dep)).ToList() ?? new List<uint>(),
            _ => new List<uint>(),
        };

        /// Every (alias, depends-on) pair where the dependency is listed BELOW the alias.
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
        /// original order untouched. On a cycle it names what it could not place rather than guessing a way
        /// out. Only POSITION changes; IDs are what stages, objectives and script properties reference.
        public static AliasOrder<T> DependencyOrder<T>(IReadOnlyList<T> list) where T : IAQuestAliasGetter
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
                if (k < 0) return new AliasOrder<T>(null, remaining.SelectMany(a => IdsOf(a)).ToList());
                foreach (var id in IdsOf(remaining[k])) placed.Add(id);
                outp.Add(remaining[k]);
                remaining.RemoveAt(k);
            }
            return new AliasOrder<T>(outp, Array.Empty<uint>());
        }

        /// <summary>
        /// EVERY USE OF EVERY ALIAS the record itself shows. His frame, 2026-10-22: "Every none optional alias is a stick of
        /// dynamite. You need to account for them all on every quest." A required alias that cannot fill stops the quest
        /// silently, so one with no job is pure risk: rung S12 carried five such aliases and did not start; S13, the same
        /// anchor without them, did.
        /// ⚠ STATED LIMIT: only this QUST record is read. A use that lives in another record (a scene's actor, a dialogue
        /// condition, another quest's external alias) is not seen, so "no use found" means none HERE, never none at all.
        /// </summary>
        public static List<AliasUse> UsesOf(IQuestGetter q)
        {
            var u = new List<AliasUse>();
            var aliases = q.Aliases?.ToList() ?? new List<IAQuestAliasGetter>();
            void Ref(IQuestReferenceAliasGetter a)
            {
                foreach (var (dep, how) in DepsOf(a))
                    u.Add(new AliasUse(dep, how switch
                    {
                        FillFrom.Inside => UseKind.FillsInside,
                        FillFrom.CreatedAt => UseKind.CreatedAt,
                        FillFrom.FromCollection => UseKind.FromCollection,
                        _ => UseKind.Leash,
                    }, a.ID, null));
                foreach (var l in a.LinkedAliases ?? Enumerable.Empty<ILinkedAliasGetter>())
                    if (l.AliasID is int v && v >= 0) u.Add(new AliasUse((uint)v, UseKind.Linked, a.ID, null));
                if (a.ClosestToAlias is int c && c >= 0) u.Add(new AliasUse((uint)c, UseKind.ClosestTo, a.ID, null));
            }
            foreach (var a in aliases)
                switch (a)
                {
                    case IQuestReferenceAliasGetter r: Ref(r); break;
                    case IQuestCollectionAliasGetter col:
                        foreach (var m in col.Collection ?? Enumerable.Empty<ICollectionAliasGetter>())
                            if (m.ReferenceAlias != null) Ref(m.ReferenceAlias);
                        break;
                    case IQuestLocationAliasGetter l:
                        foreach (var d in LeashDepsOf(l)) u.Add(new AliasUse(d, UseKind.Leash, l.ID, null));
                        foreach (var d in SameLocationDepsOf(l)) u.Add(new AliasUse(d, UseKind.SameLocation, l.ID, null));
                        foreach (var d in ParentDepsOf(l)) u.Add(new AliasUse(d, UseKind.Parent, l.ID, null));
                        if (l.SystemLocationAliasID is int s && s >= 0) u.Add(new AliasUse((uint)s, UseKind.SystemLocation, l.ID, null));
                        break;
                }

            foreach (var o in q.Objectives ?? Enumerable.Empty<IQuestObjectiveGetter>())
                foreach (var t in o.Targets ?? Enumerable.Empty<IQuestObjectiveTargetGetter>())
                    if (t.AliasID is int v && v >= 0) u.Add(new AliasUse((uint)v, UseKind.Objective, null, o.Index.ToString()));

            var vma = q.VirtualMachineAdapter;
            if (vma != null)
            {
                IEnumerable<IScriptObjectPropertyGetter> Objs(IEnumerable<IScriptEntryGetter> scripts) => scripts
                    .SelectMany(s => s.Properties)
                    .SelectMany(p => p switch
                    {
                        IScriptObjectPropertyGetter o => new[] { o },
                        IScriptObjectListPropertyGetter ol => ol.Objects.ToArray(),
                        _ => Array.Empty<IScriptObjectPropertyGetter>(),
                    });
                var scripts = vma.Scripts.AsEnumerable();
                if (vma.Script != null) scripts = scripts.Append(vma.Script);
                scripts = scripts.Concat(vma.Aliases.SelectMany(e => e.Scripts));
                foreach (var p in Objs(scripts))
                    if (p.Object.FormKey == q.FormKey && p.Alias >= 0) u.Add(new AliasUse((uint)p.Alias, UseKind.ScriptProperty, null, p.Name));
                foreach (var e in vma.Aliases)
                    if (e.Property.Object.FormKey == q.FormKey && e.Property.Alias >= 0 && e.Scripts.Count > 0)
                        u.Add(new AliasUse((uint)e.Property.Alias, UseKind.AliasScript, null, string.Join(",", e.Scripts.Select(s => s.Name))));
            }

            var byName = new Dictionary<string, List<uint>>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in aliases)
                foreach (var id in IdsOf(a))
                {
                    string? n = a switch
                    {
                        IQuestReferenceAliasGetter r => r.Name,
                        IQuestLocationAliasGetter l => l.Name,
                        IQuestCollectionAliasGetter c => c.Collection?.FirstOrDefault(m => m.ReferenceAlias?.ID == id)?.ReferenceAlias?.Name,
                        _ => null,
                    };
                    if (n == null) continue;
                    if (!byName.TryGetValue(n, out var ids)) byName[n] = ids = new List<uint>();
                    ids.Add(id);
                }
            var text = new List<string?> { q.Name?.String };
            text.AddRange((q.Objectives ?? Enumerable.Empty<IQuestObjectiveGetter>()).Select(o => o.DisplayText?.String));
            text.AddRange((q.Stages ?? Enumerable.Empty<IQuestStageGetter>()).SelectMany(s => s.LogEntries).Select(e => e.Entry?.String));
            foreach (var s in text.Where(s => s != null))
                foreach (System.Text.RegularExpressions.Match m in AliasToken.Matches(s!))
                    if (byName.TryGetValue(m.Groups[1].Value, out var ids))
                        foreach (var id in ids) u.Add(new AliasUse(id, UseKind.TextToken, null, m.Value));
            return u;
        }

        /// <Alias=X>, <Alias.Name=X>, <Alias.PluralName=X> and the like: the quest's text naming an alias.
        private static readonly System.Text.RegularExpressions.Regex AliasToken = new(@"<Alias(?:\.[A-Za-z]+)?=([^>]+)>");

        public static bool IsTerminal(UseKind k) => k is UseKind.Objective or UseKind.ScriptProperty or UseKind.AliasScript or UseKind.TextToken;

        /// <summary>
        /// The aliases with a JOB: a terminal use, or used by an alias that has one, to a fixpoint. A chain whose last link
        /// nobody uses has no job anywhere along it (S12: TargetSystemLocation, the cell created at it, the marker filled
        /// from that cell, and nothing pointing at the marker).
        /// </summary>
        public static HashSet<uint> Accounted(IReadOnlyList<AliasUse> uses)
        {
            var acc = uses.Where(x => IsTerminal(x.Kind)).Select(x => x.Used).ToHashSet();
            for (bool grew = true; grew;)
            {
                grew = false;
                foreach (var x in uses)
                    if (!IsTerminal(x.Kind) && x.ByAlias is uint by && acc.Contains(by) && acc.Add(x.Used)) grew = true;
            }
            return acc;
        }

        /// The Optional bit (0x2) on an alias's flags, ref or location alike (gen_aliaslint's control holds it to the enum word).
        public static bool IsOptional(IAQuestAliasGetter a) => a switch
        {
            IQuestReferenceAliasGetter r => (Convert.ToUInt32(r.Flags) & 0x2) != 0,
            IQuestLocationAliasGetter l => (Convert.ToUInt32(l.Flags) & 0x2) != 0,
            _ => false,
        };

        /// <summary>The non-optional aliases with no job this record shows: every stick of dynamite not accounted for.</summary>
        public static List<(uint id, string name)> Unaccounted(IQuestGetter q)
        {
            var acc = Accounted(UsesOf(q));
            var outp = new List<(uint, string)>();
            foreach (var a in q.Aliases ?? Enumerable.Empty<IAQuestAliasGetter>())
            {
                var members = a is IQuestCollectionAliasGetter c
                    ? (c.Collection ?? Enumerable.Empty<ICollectionAliasGetter>()).Where(m => m.ReferenceAlias != null).Select(m => (IAQuestAliasGetter)m.ReferenceAlias!)
                    : new[] { a };
                foreach (var m in members)
                {
                    uint id = IdsOf(m).Single();
                    if (!IsOptional(m) && !acc.Contains(id))
                        outp.Add((id, m switch { IQuestReferenceAliasGetter r => r.Name ?? "", IQuestLocationAliasGetter l => l.Name ?? "", _ => "" }));
                }
            }
            return outp;
        }

        /// WHOSE location a condition tests. On a location alias the subject is the candidate; any other
        /// RunOn is a different question, left to runtime rather than graded as this one.
        public static bool IsSubject(object conditionData)
        {
            var p = conditionData.GetType().GetProperty("RunOnType");
            return p == null || (p.GetValue(conditionData)?.ToString() ?? "Subject") == "Subject";
        }
    }
}
