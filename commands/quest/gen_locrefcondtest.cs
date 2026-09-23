using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// GATE B for the Delve quest-tooling lane: can a LocationHasRefType condition be
    /// CONSTRUCTED from scratch and written onto a quest's location alias, or must a
    /// recipe stay a Creation Kit record?
    ///
    /// Why a command and not a scratch script (2026-09-23, his "make a little script to
    /// test that now"): a method hand-rolled whose only committed artifact is its OUTPUT
    /// is an unversioned instrument. It ships with its controls.
    ///
    /// WHAT IT PROVES, and the wording is deliberate. It proves SERIALISATION: that a
    /// hand-built condition survives a write to disk and reads back identical in shape to
    /// the vanilla hand-authored one. It does NOT prove the engine honours it at runtime.
    /// That is his eye in game, and the diagnostic says so rather than letting a green
    /// imply it.
    ///
    /// THE ROUND TRIP IS OFF DISK, and that is the design. An assertion comparing the
    /// in-memory object to itself can never fail. The sibling gen_dlgtest reports off the
    /// object it just built, which is a structure check rather than a round trip; this
    /// writes the .esm, drops everything and re-reads the file.
    ///
    /// Usage: gen_locrefcondtest [refTypeEditorId] [modname]
    ///   refTypeEditorId defaults to RETravelA1LocRef (a POI marker the Delve lane wants)
    ///   modname         defaults to locrefcondtest
    /// </summary>
    public static class gen_locrefcondtest
    {
        // The vanilla hand-authored instance this is graded against: MB_Bounty01Far's
        // TargetLocation alias carries LocationHasRefType EqualTo 1 naming
        // LocDungeonBossLocRef. It is the ground bounty base the Delve lane clones, so it
        // is the right oracle rather than a convenient one.
        private const uint VanillaQuestId = 0x002C1C;
        private const string VanillaAliasName = "TargetLocation";

        public static int Run(string refTypeEditorId = "RETravelA1LocRef", string modname = "locrefcondtest")
        {
            int failures = 0;
            Console.WriteLine("=== GATE B: construct a LocationHasRefType condition and round-trip it ===");
            Console.WriteLine("  ref type : " + refTypeEditorId);
            Console.WriteLine("  mod      : " + modname + ".esm");
            Console.WriteLine();

            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build();

            // Mutagen 0.46+ needs a master-flags lookup for both the write and the read-back.
            // The repo already builds one; reusing it beats a second copy that can drift.
            gen_quest_main.BuildReadParams(env.LoadOrder);

            // 0. Resolve the ref type by EditorID. A magic FormID here would be a number
            //    nobody can check, and resolving proves the marker exists at all.
            var lcrt = env.LoadOrder.PriorityOrder
                .WinningOverrides<ILocationReferenceTypeGetter>()
                .FirstOrDefault(x => string.Equals(x.EditorID, refTypeEditorId, StringComparison.OrdinalIgnoreCase));
            if (lcrt == null)
            {
                Console.WriteLine("  REFUSED: no LocationReferenceType with EditorID '" + refTypeEditorId + "'.");
                Console.WriteLine("  Nothing was written. That is a needle failure, not a finding.");
                return 1;
            }
            Console.WriteLine("  [0] resolved " + lcrt.EditorID + " = " + lcrt.FormKey);

            // 1. Read the vanilla oracle BEFORE building anything. If the shape we are
            //    copying cannot be read, the test has no standard and must refuse rather
            //    than quietly grade against its own assumptions.
            var vanillaQuest = env.LoadOrder.PriorityOrder
                .WinningOverrides<IQuestGetter>()
                .FirstOrDefault(q => q.FormKey.ID == VanillaQuestId);

            IConditionGetter oracle = null;
            if (vanillaQuest != null && vanillaQuest.Aliases != null)
            {
                foreach (var a in vanillaQuest.Aliases)
                {
                    var la = a as IQuestLocationAliasGetter;
                    if (la != null && la.Name == VanillaAliasName
                        && la.Conditions != null && la.Conditions.Count > 0)
                    {
                        oracle = la.Conditions[0];
                        break;
                    }
                }
            }
            if (oracle == null)
            {
                Console.WriteLine("  REFUSED: could not read the vanilla oracle (quest 0x"
                                  + VanillaQuestId.ToString("X6") + " alias " + VanillaAliasName + ").");
                Console.WriteLine("  Nothing was written. Without the standard this grades against itself.");
                return 1;
            }
            string oracleType = oracle.Data == null ? "?" : oracle.Data.GetType().Name;
            Console.WriteLine("  [1] oracle read: " + Describe(oracle));

            // 2. CONSTRUCT the condition. This is the line the whole gate is about.
            //    SpaceCellTools says "we can't create conditions ourselves ... so we steal
            //    existing ones and clone them". That comment is stale for seven other
            //    condition types; this tests whether it is stale for this one.
            var data = new LocationHasRefTypeConditionData();
            data.FirstParameter = new FormLinkOrIndex<ILocationReferenceTypeGetter>(data, lcrt.FormKey);
            var built = new ConditionFloat
            {
                Data = data,
                CompareOperator = CompareOperator.EqualTo,
                ComparisonValue = 1f,
            };
            Console.WriteLine("  [2] constructed: " + Describe(built));

            // 3. Build a minimal quest carrying it on a LOCATION alias, and write it.
            var newModKey = new ModKey(modname, ModType.Master);
            var mod = new StarfieldMod(newModKey, StarfieldRelease.Starfield);
            var quest = new Quest(mod) { EditorID = modname + "_qst" };
            // Aliases is null on a fresh Quest for the same reason Conditions was. Three
            // nulls in a row, all in the plumbing, none of them in the thing under test.
            quest.Aliases = new ExtendedList<AQuestAlias>();
            // Conditions is NULL on a fresh alias and is INIT-ONLY, so it goes in the object
            // initializer rather than being appended to afterwards. Both facts were found by
            // running and then by the compiler: the construct in step 2 succeeded and the trouble
            // was entirely in the plumbing around it, which is the honest shape of this result.
            var alias = new QuestLocationAlias
            {
                ID = 0,
                Name = "TargetLocation",
                Conditions = new ExtendedList<Condition> { built },
            };
            quest.Aliases.Add(alias);
            mod.Quests.Add(quest);

            // Written to TEMP, never to the game Data folder. A test that litters a live install
            // with a throwaway .esm is a hazard the first time somebody forgets it is there, and
            // nothing here needs the file to sit beside its masters: the master flags come from
            // the lookup cache, not from the directory.
            string outPath = Path.Combine(Path.GetTempPath(), modname + ".esm");
            mod.WriteToBinary(outPath, gen_quest_main.BuildWriteParams());
            Console.WriteLine("  [3] wrote " + outPath + " (" + new FileInfo(outPath).Length.ToString("N0") + " B)");

            // 4. THE ROUND TRIP, OFF DISK. Everything above is dropped and the bytes are
            //    read back. An assertion over the in-memory object could never fail.
            var reread = StarfieldMod.CreateFromBinaryOverlay(outPath, StarfieldRelease.Starfield,
                                                              gen_quest_main.BuildReadParams(env.LoadOrder));
            var rq = reread.Quests.FirstOrDefault();

            IConditionGetter got = null;
            if (rq != null && rq.Aliases != null)
            {
                foreach (var a in rq.Aliases)
                {
                    var la = a as IQuestLocationAliasGetter;
                    if (la != null && la.Conditions != null && la.Conditions.Count > 0)
                    {
                        got = la.Conditions[0];
                        break;
                    }
                }
            }
            if (got == null)
            {
                Console.WriteLine("  [4] FAIL: no condition survived the write.");
                Console.WriteLine("=== GATE B: the condition does NOT round-trip. A recipe stays a CK record. ===");
                return 1;
            }
            Console.WriteLine("  [4] read back : " + Describe(got));
            Console.WriteLine();

            // 5. Grade against BOTH standards: what we asked for, and the vanilla
            //    hand-authored shape the exit condition names.
            failures += Check("data type matches vanilla", StripOverlay(got.Data == null ? "?" : got.Data.GetType().Name), StripOverlay(oracleType));
            failures += Check("compare operator", got.CompareOperator.ToString(), oracle.CompareOperator.ToString());
            failures += Check("comparison value", Val(got), "1");
            failures += Check("first parameter survived", ParamKey(got), lcrt.FormKey.ToString());
            failures += Check("parameter is OURS, not the vanilla one",
                              ParamKey(got) != ParamKey(oracle) ? "differs" : "same", "differs");

            // 6. NEGATIVE CONTROL. A comparison that cannot fail is not a comparison, so
            //    prove this one can: grade the round-tripped parameter against a key we
            //    never wrote and require a MISMATCH.
            Console.WriteLine();
            Console.WriteLine("  negative control (must report MISMATCH):");
            // Graded QUIETLY rather than through Check(): a passing run must not print the word
            // FAIL. A reader who greps output for failures would find one in a green run, and a
            // check that cries wolf has stopped being a check.
            string biteGot = ParamKey(got);
            bool biteFired = !string.Equals(biteGot, FormKey.Null.ToString(), StringComparison.OrdinalIgnoreCase);
            Console.WriteLine("    compared '" + biteGot + "' against a key never written ('"
                              + FormKey.Null + "'): " + (biteFired ? "MISMATCH, as required" : "MATCHED"));
            if (!biteFired)
            {
                Console.WriteLine("    FAIL: the negative control PASSED, so the checks above prove nothing.");
                failures++;
            }
            else
            {
                Console.WriteLine("    negative control behaved: it reported a mismatch, as it must.");
            }

            Console.WriteLine();
            if (failures == 0)
            {
                Console.WriteLine("=== GATE B: SERIALISATION PASSES ===");
                Console.WriteLine("  A hand-constructed LocationHasRefType condition survives a write and reads");
                Console.WriteLine("  back identical in shape to the vanilla hand-authored one.");
                Console.WriteLine("  THIS IS NOT THE WHOLE GATE. It proves the bytes, never the behaviour:");
                Console.WriteLine("  whether the story manager honours the condition and draws the intended POI");
                Console.WriteLine("  is his eye in game, and no run of this command can answer it.");
            }
            else
            {
                Console.WriteLine("=== GATE B: " + failures + " check(s) FAILED. The condition does not round-trip cleanly. ===");
            }
            return failures == 0 ? 0 : 1;
        }

        // Mutagen names the read-back type FooConditionDataBinaryOverlay and the built one
        // FooConditionData. Comparing them raw would fail on the overlay suffix alone, which
        // would be the test grading the reader rather than the record.
        private static string StripOverlay(string s)
            => s.Replace("ConditionDataBinaryOverlay", "").Replace("ConditionData", "");

        private static int Check(string label, string actual, string expected)
        {
            bool ok = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
            Console.WriteLine("    [" + (ok ? "OK  " : "FAIL") + "] " + label
                              + ": got '" + actual + "' expected '" + expected + "'");
            return ok ? 0 : 1;
        }

        private static string Val(IConditionGetter c)
        {
            var cf = c as IConditionFloatGetter;
            return cf != null ? cf.ComparisonValue.ToString("0.##") : "(not a float condition)";
        }

        // Reflection rather than a typed cast: the built object and the read-back overlay are
        // different CLR types exposing the same shape, so one typed path would work on only
        // one of them and silently return nothing for the other.
        private static string ParamKey(IConditionGetter c)
        {
            var d = c.Data;
            if (d == null) return "(no data)";
            var pi = d.GetType().GetProperty("FirstParameter");
            var v = pi == null ? null : pi.GetValue(d);
            if (v == null) return "(no first parameter)";
            var link = v.GetType().GetProperty("Link");
            var linkVal = link == null ? null : link.GetValue(v);
            object fk = null;
            if (linkVal != null)
            {
                var p = linkVal.GetType().GetProperty("FormKey");
                if (p != null) fk = p.GetValue(linkVal);
            }
            if (fk == null)
            {
                var p = v.GetType().GetProperty("FormKey");
                if (p != null) fk = p.GetValue(v);
            }
            return fk != null ? fk.ToString() : (v.ToString() ?? "(unreadable)");
        }

        private static string Describe(IConditionGetter c)
        {
            string fn = c.Data == null ? "?" : StripOverlay(c.Data.GetType().Name);
            return fn + " " + c.CompareOperator + " " + Val(c) + "  param=" + ParamKey(c);
        }
    }
}
