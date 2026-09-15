using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Retrograde.Utils
{
    /// The mod's record groups, discovered by reflection instead of by a hand-written list.
    ///
    /// ⛔ WHY THIS EXISTS (2026-09-15, his "we have a list of all record types now can we not fill
    /// it all out?"). gen_inspect carried THREE hand-maintained lists of record types and they had
    /// drifted apart: `SupportedTypes` (the usage text), the `switch` that decides what actually
    /// works (~40 cases), and `ListRecordGroups` (24 entries, a different subset again). A door
    /// could not be inspected at all, and adding a 41st case would have been the wrong fix: the
    /// defect is that the list is hand-written, not that it was short.
    ///
    /// ⭐ THE SEAM MOVED RATHER THAN THE LIST GROWING. Mutagen already declares every group as an
    /// `IStarfieldGroupGetter&lt;T&gt;` / `IStarfieldListGroupGetter&lt;T&gt;` property on the mod,
    /// so the authoritative list is already in the type system and a second copy in our source can
    /// only ever go stale. Derive it.
    ///
    /// ⚠ THIS IS THE SECOND COPY OF THE WALK, NOT THE THIRD, AND THE THIRD IS DELIBERATE.
    /// `FormKeyLookup` had this reflection walk privately and now calls in here. `gen_fkltest`
    /// keeps its OWN independent implementation ON PURPOSE: it is the probe that grades this
    /// mechanism, and a control that shares code with the thing it controls for is not a control.
    public static class RecordGroups
    {
        /// One record group: the name a human types, the records, and the element type.
        public readonly record struct Group(string Name, IEnumerable Records, Type ElementType, string PropertyName);

        /// Every record group Mutagen exposes on this mod, in alphabetical order.
        ///
        /// The NAME is derived from the group's element type rather than from the property name,
        /// because the property is a plural chosen by the library (`Doors`, `ActorValueInformation`)
        /// while the element type is the record's own name (`IDoorGetter` -> `Door`). That keeps the
        /// spelling a reader already knows from FormIDs and from xEdit.
        public static IEnumerable<Group> Enumerate(IStarfieldModGetter mod)
        {
            var found = new List<Group>();
            foreach (var prop in mod.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var typeName = prop.PropertyType.Name;
                if (!typeName.StartsWith("IStarfieldGroupGetter") &&
                    !typeName.StartsWith("IStarfieldListGroupGetter")) continue;

                object? val;
                try { val = prop.GetValue(mod); }
                catch { continue; }
                if (val is not IEnumerable records) continue;

                var args = prop.PropertyType.GetGenericArguments();
                if (args.Length != 1) continue;
                found.Add(new Group(NameOf(args[0]), records, args[0], prop.Name));
            }
            found.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return found;
        }

        /// `IDoorGetter` -> `Door`. Tolerates a non-interface element type, because the naming
        /// convention is the library's and not ours to assume: if it ever stops holding, the worst
        /// case is a group listed under a slightly odd name, never a group that disappears.
        public static string NameOf(Type t)
        {
            var n = t.Name;
            if (n.Length > 1 && n[0] == 'I' && char.IsUpper(n[1])) n = n.Substring(1);
            if (n.EndsWith("Getter", StringComparison.Ordinal)) n = n.Substring(0, n.Length - 6);
            return n;
        }

        /// Enumerates a group's records, skipping individual records that throw on parse.
        ///
        /// LIFTED VERBATIM from FormKeyLookup 2026-09-15 so its behaviour cannot change: the
        /// 20-consecutive-fault abort, the continue-on-throw and the message shape are all as they
        /// were, with only the log prefix parameterised so each caller still names itself. It
        /// matters because the [Bethesda] manual's standing caution is that some record shapes read
        /// flaky in Mutagen, so a dumper that dies on one bad record is a dumper nobody can use on
        /// the corpus that has one.
        public static IEnumerable<IMajorRecordGetter> Safe(
            IEnumerable source, string groupName, string modLabel, string logPrefix)
        {
            var en = source.GetEnumerator();
            int consecutiveFaults = 0;
            while (true)
            {
                bool moved;
                try
                {
                    moved = en.MoveNext();
                    consecutiveFaults = 0;
                }
                catch (Exception ex)
                {
                    consecutiveFaults++;
                    Console.WriteLine($"[{logPrefix}] Skipped record in {modLabel}/{groupName}: {ex.Message}");
                    if (consecutiveFaults >= 20)
                    {
                        Console.WriteLine($"[{logPrefix}] Aborting {groupName} after 20 consecutive errors.");
                        yield break;
                    }
                    continue;
                }
                if (!moved) yield break;
                if (en.Current is IMajorRecordGetter rec)
                    yield return rec;
            }
        }
    }
}
