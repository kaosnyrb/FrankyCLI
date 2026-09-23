using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FrankyCLI
{
    /// <summary>
    /// GATE C for the Delve quest-tooling lane: can a quest be ENDED from data?
    ///
    /// WHY IT EXISTS (2026-09-23, his "let's get to the open the dropped container stage
    /// then end the quest ... better to build in layers"). Layer 1 of the first Delve
    /// testcase needs the quest to finish after one beat. gen_queststage can author a
    /// stage and a journal line but sets NO completion flag -- read in its source, not
    /// inferred -- and duo_artifact_local_qst04a, the base this lane clones, ends through
    /// a Papyrus fragment (Fragment_Stage_0100_Item_00) rather than through a flag. So the
    /// question is whether the flag route exists at all, because it is the difference
    /// between a layer that keeps the no-CK-record promise and one that costs a script.
    ///
    /// ⛔ IT IS A DISCOVERY PROBE BEFORE IT IS AN ASSERTION, and that ordering is the
    /// design. The unknown here is the TYPE SURFACE: which member carries completion, on
    /// QuestStage or on QuestLogEntry, and under what name. Hardcoding a property name
    /// would be guessing at the exact thing being measured, so part 1 reflects and reports
    /// and part 2 only then constructs. A probe that assumes its own answer is not a probe.
    ///
    /// ⚠ WHY REFLECTION RATHER THAN A TYPED CAST: the same reason gen_locrefcondtest reads
    /// its parameter that way. The built object and the read-back overlay are different CLR
    /// types exposing the same shape, so one typed path works on exactly one of them and
    /// returns nothing for the other, silently.
    ///
    /// ⚠ THE ORACLE MAY LEGITIMATELY NOT EXIST, and that is a finding rather than a
    /// refusal. gen_inspect reports Flags=0 on every stage of every quest in both mods
    /// read so far; a uniform result across a whole sweep is the instrument far more often
    /// than the corpus, so this hunts vanilla directly for a log entry with the flag set.
    /// If none is found the construct half still runs, because vanilla not USING a field
    /// is not evidence the field cannot be written -- but the diagnostic says plainly that
    /// a pass then proves serialisation against no shipped standard.
    ///
    /// THE ROUND TRIP IS OFF DISK. An assertion comparing the in-memory object to itself
    /// can never fail.
    ///
    /// Usage: gen_questcompletetest [modname]
    ///   modname defaults to questcompletetest. Written to TEMP, never to the game Data
    ///   folder -- a test that litters a live install is a hazard the first time somebody
    ///   forgets it is there.
    /// </summary>
    public static class gen_questcompletetest
    {
        public static int Run(string modname = "questcompletetest", string questFilter = "")
        {
            int failures = 0;
            Console.WriteLine("=== GATE C: can a quest be ENDED from data? ===");
            Console.WriteLine("  mod : " + modname + ".esm  (written to TEMP)");
            Console.WriteLine();

            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build();

            // Mutagen 0.46+ needs a master-flags lookup for both the write and the read-back.
            gen_quest_main.BuildReadParams(env.LoadOrder);

            // ---------------------------------------------------------------- PART 1
            // The type surface. This is the half that could not be answered from outside
            // the process: the assembly will not reflect without its dependency graph, and
            // in here it is already loaded and working. The instrument I have, re-aimed.
            Console.WriteLine("  [1] TYPE SURFACE");
            var flagCarriers = new List<(Type owner, PropertyInfo prop, Type enumType)>();
            foreach (var t in new[] { typeof(QuestStage), typeof(QuestLogEntry), typeof(QuestObjective) })
            {
                Console.WriteLine("      " + t.Name + ":");
                foreach (var p in t.GetProperties().OrderBy(p => p.Name))
                {
                    string extra = "";
                    var pt = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                    if (pt.IsEnum)
                    {
                        extra = "   [enum: " + string.Join(", ", Enum.GetNames(pt)) + "]";
                        flagCarriers.Add((t, p, pt));
                    }
                    Console.WriteLine("        " + p.Name + " : " + p.PropertyType.Name + extra);
                }
            }
            Console.WriteLine();

            // Which enum member actually means "the quest is over". Named by the engine,
            // not by me -- if Bethesda spells it differently this reports what it found
            // rather than failing on my vocabulary.
            (Type owner, PropertyInfo prop, Type enumType, string member)? completion = null;
            foreach (var (owner, prop, enumType) in flagCarriers)
            {
                var hit = Enum.GetNames(enumType).FirstOrDefault(n =>
                    n.IndexOf("CompleteQuest", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("ShutDown", StringComparison.OrdinalIgnoreCase) >= 0);
                if (hit != null) { completion = (owner, prop, enumType, hit); break; }
            }

            if (completion == null)
            {
                Console.WriteLine("  [1] FINDING: no completion-shaped flag on any of those three types.");
                Console.WriteLine("      Nothing was written. Ending a quest from data is NOT available on");
                Console.WriteLine("      the stage record, so layer 1 ends through a Papyrus fragment, which");
                Console.WriteLine("      is what duo_artifact_local_qst04a already does.");
                Console.WriteLine("=== GATE C: the flag route does not exist. Copy the shipped fragment. ===");
                return 1;
            }
            Console.WriteLine("  [1] completion flag found: " + completion.Value.owner.Name + "."
                              + completion.Value.prop.Name + " -> " + completion.Value.member);
            Console.WriteLine();

            // ---------------------------------------------------------------- PART 2
            // The oracle hunt, over VANILLA, before anything is built. Read the standard
            // first or grade against your own assumptions.
            Console.WriteLine("  [2] ORACLE HUNT (vanilla quests carrying that flag set)");
            int scanned = 0, carriers = 0;
            var examples = new List<string>();
            foreach (var q in env.LoadOrder.PriorityOrder.WinningOverrides<IQuestGetter>())
            {
                // An optional filter, because a corpus count answers "is the flag used" and
                // a named quest answers "does THE ONE WE CLONE use it" -- and the second is
                // the question a build order actually rests on.
                if (questFilter.Length > 0
                    && (q.EditorID == null
                        || q.EditorID.IndexOf(questFilter, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                scanned++;
                if (q.Stages == null) continue;
                foreach (var st in q.Stages)
                {
                    if (st.LogEntries == null) continue;
                    foreach (var le in st.LogEntries)
                    {
                        if (!FlagIsSet(le, completion.Value.prop.Name, completion.Value.member)
                            && !FlagIsSet(st, completion.Value.prop.Name, completion.Value.member)) continue;
                        carriers++;
                        if (examples.Count < 5)
                            examples.Add(q.EditorID + " [" + q.FormKey.ModKey.FileName + "] stage " + st.Index);
                    }
                }
            }
            Console.WriteLine("      scanned " + scanned.ToString("N0") + " quest(s); "
                              + carriers + " stage(s) carry it");
            foreach (var e in examples) Console.WriteLine("        e.g. " + e);
            if (carriers == 0)
            {
                Console.WriteLine("      ⚠ NO SHIPPED STANDARD. A pass below proves the bytes survive a");
                Console.WriteLine("        write and NOTHING about whether the engine acts on them. Graded");
                Console.WriteLine("        as such rather than reported as a green.");
            }
            Console.WriteLine();

            // ---------------------------------------------------------------- PART 3
            // Construct, write, and read back OFF DISK.
            Console.WriteLine("  [3] CONSTRUCT AND ROUND-TRIP");
            var newModKey = new ModKey(modname, ModType.Master);
            var mod = new StarfieldMod(newModKey, StarfieldRelease.Starfield);
            var quest = new Quest(mod) { EditorID = modname + "_qst" };

            // Quest.Stages is INIT-ONLY and already constructed, so it is appended to rather
            // than assigned -- the mirror image of Gate B, where Aliases was null and had to
            // be assigned and Conditions was init-only. Both facts were found by the compiler
            // rather than reasoned, and neither is in the thing under test.
            var entry = new QuestLogEntry { Entry = "Gate C: this stage should end the quest." };
            // LogEntries assigned in the initializer rather than appended to: on the Gate B
            // build the sibling collections (Conditions, Aliases) came back null AND
            // init-only on fresh records, and assuming otherwise here would fault on a line
            // that has nothing to do with what is being measured.
            var stage = new QuestStage
            {
                Index = 100,
                LogEntries = new ExtendedList<QuestLogEntry> { entry },
            };
            quest.Stages.Add(stage);

            // Set the flag on whichever type carries it, by reflection, so this code does
            // not need to have been written against a member name it only just discovered.
            object target = completion.Value.owner == typeof(QuestLogEntry) ? (object)entry : (object)stage;
            if (!TrySetFlag(target, completion.Value.prop, completion.Value.enumType, completion.Value.member, out string setErr))
            {
                Console.WriteLine("      REFUSED: could not set the flag: " + setErr);
                Console.WriteLine("      Nothing was written.");
                return 1;
            }
            Console.WriteLine("      set " + completion.Value.owner.Name + "." + completion.Value.prop.Name
                              + " = " + completion.Value.member);

            mod.Quests.Add(quest);
            string outPath = Path.Combine(Path.GetTempPath(), modname + ".esm");
            mod.WriteToBinary(outPath, gen_quest_main.BuildWriteParams());
            Console.WriteLine("      wrote " + outPath + " (" + new FileInfo(outPath).Length.ToString("N0") + " B)");

            var reread = StarfieldMod.CreateFromBinaryOverlay(outPath, StarfieldRelease.Starfield,
                                                              gen_quest_main.BuildReadParams(env.LoadOrder));
            var rq = reread.Quests.FirstOrDefault();
            if (rq == null || rq.Stages == null || rq.Stages.Count == 0)
            {
                Console.WriteLine("      FAIL: no stage survived the write.");
                Console.WriteLine("=== GATE C: FAILED. The stage does not round-trip. ===");
                return 1;
            }
            var rs = rq.Stages.First();
            Console.WriteLine("      read back: stage " + rs.Index + ", "
                              + (rs.LogEntries == null ? 0 : rs.LogEntries.Count) + " log entry(ies)");
            Console.WriteLine();

            // ---------------------------------------------------------------- PART 4
            Console.WriteLine("    grading:");
            failures += Check("stage index survived", rs.Index.ToString(), "100");

            object readTarget = completion.Value.owner == typeof(QuestLogEntry)
                ? (rs.LogEntries != null && rs.LogEntries.Count > 0 ? (object)rs.LogEntries.First() : null)
                : (object)rs;
            bool flagSurvived = readTarget != null
                && FlagIsSet(readTarget, completion.Value.prop.Name, completion.Value.member);
            failures += Check("completion flag survived the write",
                              flagSurvived ? "set" : "not set", "set");

            // NEGATIVE CONTROL. A comparison that cannot fail is not a comparison. Build a
            // second stage WITHOUT the flag, through the same write and the same reader,
            // and require it to come back CLEAR. A control that shares the code path is the
            // only one that grades the code path.
            Console.WriteLine();
            Console.WriteLine("  negative control (an unflagged stage must read back CLEAR):");
            var mod2 = new StarfieldMod(new ModKey(modname + "_neg", ModType.Master), StarfieldRelease.Starfield);
            var q2 = new Quest(mod2) { EditorID = modname + "_neg_qst" };
            var st2 = new QuestStage
            {
                Index = 100,
                LogEntries = new ExtendedList<QuestLogEntry>
                {
                    new QuestLogEntry { Entry = "Gate C control: this stage must NOT end the quest." },
                },
            };
            q2.Stages.Add(st2);
            mod2.Quests.Add(q2);
            string negPath = Path.Combine(Path.GetTempPath(), modname + "_neg.esm");
            mod2.WriteToBinary(negPath, gen_quest_main.BuildWriteParams());
            var reread2 = StarfieldMod.CreateFromBinaryOverlay(negPath, StarfieldRelease.Starfield,
                                                               gen_quest_main.BuildReadParams(env.LoadOrder));
            var rs2 = reread2.Quests.FirstOrDefault()?.Stages?.FirstOrDefault();
            object negTarget = completion.Value.owner == typeof(QuestLogEntry)
                ? (rs2?.LogEntries != null && rs2.LogEntries.Count > 0 ? (object)rs2.LogEntries.First() : null)
                : (object)rs2;
            bool negSet = negTarget != null
                && FlagIsSet(negTarget, completion.Value.prop.Name, completion.Value.member);
            // Graded quietly: a passing run must not print the word FAIL, or a reader
            // grepping output for failures finds one in a green run.
            Console.WriteLine("    unflagged stage read back: " + (negSet ? "SET" : "clear"));
            if (negSet)
            {
                Console.WriteLine("    FAIL: the control came back flagged, so the check above proves nothing");
                Console.WriteLine("          -- it would report 'set' whatever we wrote.");
                failures++;
            }
            else
            {
                Console.WriteLine("    control behaved: clear, so the reader can tell the two apart.");
            }

            Console.WriteLine();
            if (failures == 0)
            {
                Console.WriteLine("=== GATE C: SERIALISATION PASSES ===");
                Console.WriteLine("  A completion flag set from data survives the write and reads back off disk,");
                Console.WriteLine("  and an unflagged stage does not. So layer 1 can end without a script.");
                Console.WriteLine("  THIS IS NOT THE WHOLE GATE. It proves the bytes, never the behaviour:");
                Console.WriteLine("  whether the engine actually stops the quest is his eye in game.");
                if (carriers == 0)
                {
                    Console.WriteLine("  ⚠ AND NO VANILLA QUEST SETS THIS FLAG, so there is no shipped standard");
                    Console.WriteLine("    behind it. Treat the in-game run as load-bearing rather than as confirmation.");
                }
            }
            else
            {
                Console.WriteLine("=== GATE C: " + failures + " check(s) FAILED. ===");
                Console.WriteLine("  Layer 1 ends through a Papyrus fragment instead -- the shipped route.");
            }
            return failures == 0 ? 0 : 1;
        }

        private static bool FlagIsSet(object obj, string propName, string memberName)
        {
            if (obj == null) return false;
            var pi = obj.GetType().GetProperty(propName);
            var v = pi?.GetValue(obj);
            if (v == null) return false;
            var et = Nullable.GetUnderlyingType(v.GetType()) ?? v.GetType();
            if (!et.IsEnum) return false;
            try
            {
                long have = Convert.ToInt64(v);
                long want = Convert.ToInt64(Enum.Parse(et, memberName));
                return want != 0 && (have & want) == want;
            }
            catch { return false; }
        }

        private static bool TrySetFlag(object obj, PropertyInfo prop, Type enumType, string memberName, out string err)
        {
            err = "";
            try
            {
                object cur = prop.GetValue(obj);
                long have = cur == null ? 0 : Convert.ToInt64(cur);
                long want = Convert.ToInt64(Enum.Parse(enumType, memberName));
                object next = Enum.ToObject(enumType, have | want);
                if (!prop.CanWrite) { err = prop.Name + " is read-only"; return false; }
                prop.SetValue(obj, next);
                return true;
            }
            catch (Exception ex) { err = ex.Message; return false; }
        }

        private static int Check(string label, string actual, string expected)
        {
            bool ok = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
            Console.WriteLine("    [" + (ok ? "OK  " : "FAIL") + "] " + label
                              + ": got '" + actual + "' expected '" + expected + "'");
            return ok ? 0 : 1;
        }
    }
}
