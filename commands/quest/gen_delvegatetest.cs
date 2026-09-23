using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// GATE B, BEHAVIOUR HALF: is a condition WE generated honoured by the engine the
    /// same way a Creation Kit authored one is?
    ///
    /// His build direction, 2026-09-23: "We'll add test stuff to Overtime. Build on
    /// duo_artifact_local_qst04a. It's a simple one POI kill quest, the reason artifact
    /// is good is because I can run a command in the ingame console to start the quest
    /// at any time." The console start is the load-bearing half: it removes the mission
    /// board, its rotation and its refresh timer from the experiment entirely.
    ///
    /// WHY TWO QUESTS AND NOT ONE. A single passing run cannot discriminate. If our
    /// generated condition were silently IGNORED, the quest would fill from the wider
    /// pool and start perfectly, and that looks identical to success. So this writes a
    /// matched pair differing in one field:
    ///
    ///   duo_delvetest_pass  condition names REWoundedMarkerLocRef, exactly what the
    ///                       source quest already uses. Only the BYTES are ours.
    ///                       Expect: behaves like duo_artifact_local_qst04a.
    ///
    ///   duo_delvetest_fail  condition names an LCRT MINTED BY THIS COMMAND, which no
    ///                       Location carries BY CONSTRUCTION.
    ///                       Expect: the location alias cannot fill, so the quest does
    ///                       not start.
    ///
    /// PASS starting and FAIL not starting means the condition is being EVALUATED.
    /// Both starting means it is being ignored and any green above it meant nothing.
    ///
    /// The minted LCRT is deliberate. LocDungeonBossLocRef was the first candidate for
    /// the empty filter, on the 2026-09-23 census showing POIs and dungeons are disjoint
    /// -- but 71 real locations carry it and this alias is literally named
    /// DungeonLocation, so a legitimate fill could not be ruled out. A ref type nothing
    /// has ever referenced needs no census to be trusted.
    ///
    /// Usage: gen_delvegatetest [modname] [sourceQuestEditorId]
    ///   defaults: du_overtime, duo_artifact_local_qst04a
    /// </summary>
    public static class gen_delvegatetest
    {
        private const string PassQuestId = "duo_delvetest_pass";
        private const string FailQuestId = "duo_delvetest_fail";
        private const string DeadRefId = "duo_delvetest_neverused";
        private const string LocAliasName = "DungeonLocation";
        private const string LiveMarker = "REWoundedMarkerLocRef";

        public static int Run(string modname = "du_overtime", string srcEditorId = "duo_artifact_local_qst04a")
        {
            Console.WriteLine("=== GATE B, behaviour half: build the matched pair into " + modname + " ===");
            Console.WriteLine();

            // THE ENVIRONMENT IS SCOPED AND DISPOSED BEFORE THE WRITE, deliberately.
            // GameEnvironment holds every listed plugin open through a memory-mapped
            // overlay, including the one we are about to overwrite, so writing while it
            // is alive throws "used by another process" against OUR OWN handle. The lock
            // is released only when the process dies, which makes it look intermittent
            // and like somebody else's fault. Everything needed after the block is
            // captured into locals here.
            StarfieldMod myMod;
            string modFile;
            FormKey liveKey, deadKey;
            string liveName, deadName;
            Mutagen.Bethesda.Plugins.Binary.Parameters.BinaryReadParameters readParams;
            int failures = 0;

            using (var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build())
            {
            readParams = gen_quest_main.BuildReadParams(env.LoadOrder);
            string datapath = env.DataFolderPath;

            modFile = Path.Combine(datapath, modname + ".esm");
            if (!File.Exists(modFile))
            {
                Console.WriteLine("  REFUSED: " + modFile + " does not exist. Nothing written.");
                return 1;
            }

            // The live marker is resolved from the load order rather than hardcoded as a
            // FormID: a magic number here is one nobody can check, and resolving proves it
            // is really there.
            var liveRef = env.LoadOrder.PriorityOrder
                .WinningOverrides<ILocationReferenceTypeGetter>()
                .FirstOrDefault(x => string.Equals(x.EditorID, LiveMarker, StringComparison.OrdinalIgnoreCase));
            if (liveRef == null)
            {
                Console.WriteLine("  REFUSED: could not resolve " + LiveMarker + ". Nothing written.");
                return 1;
            }

            myMod = StarfieldMod.CreateFromBinary(modFile, StarfieldRelease.Starfield, readParams);
            gen_quest_main.FixNextFormId(myMod);

            var source = myMod.Quests.FirstOrDefault(q => q.EditorID == srcEditorId);
            if (source == null)
            {
                Console.WriteLine("  REFUSED: no quest '" + srcEditorId + "' in " + modname + ". Nothing written.");
                return 1;
            }
            Console.WriteLine("  source   : " + source.EditorID + " " + source.FormKey);
            Console.WriteLine("  live ref : " + liveRef.EditorID + " " + liveRef.FormKey);

            // Re-running must not stack duplicates on top of the previous run's records.
            // StarfieldGroup removes BY FORMKEY, the repo idiom in gen_removerecord. The keys are
            // collected first: removing while enumerating the group is how a sweep eats its own feet.
            var stale = myMod.Quests.Where(q => q.EditorID == PassQuestId || q.EditorID == FailQuestId)
                             .Select(q => q.FormKey).ToList();
            foreach (var k in stale) myMod.Quests.Remove(k);
            var staleRefs = myMod.LocationReferenceTypes.Where(r => r.EditorID == DeadRefId)
                                 .Select(r => r.FormKey).ToList();
            foreach (var k in staleRefs) myMod.LocationReferenceTypes.Remove(k);
            int removed = stale.Count;
            int removedRefs = staleRefs.Count;
            if (removed + removedRefs > 0)
                Console.WriteLine("  cleaned  : " + removed + " old test quest(s), " + removedRefs + " old ref type(s)");

            // The empty filter, minted here so nothing carries it. This is the control and
            // it is the only reason the FAIL half means anything.
            var deadRef = new LocationReferenceType(myMod) { EditorID = DeadRefId };
            myMod.LocationReferenceTypes.Add(deadRef);
            Console.WriteLine("  dead ref : " + deadRef.EditorID + " " + deadRef.FormKey + "  (carried by nothing, by construction)");
            Console.WriteLine();

            failures += BuildClone(myMod, source, PassQuestId, liveRef.FormKey, liveRef.EditorID ?? LiveMarker, liveRef.FormKey);
            failures += BuildClone(myMod, source, FailQuestId, deadRef.FormKey, deadRef.EditorID ?? DeadRefId, liveRef.FormKey);
            if (failures > 0)
            {
                Console.WriteLine();
                Console.WriteLine("=== " + failures + " problem(s) building the pair. NOTHING WRITTEN. ===");
                return 1;
            }

            liveKey = liveRef.FormKey; liveName = liveRef.EditorID ?? LiveMarker;
            deadKey = deadRef.FormKey; deadName = deadRef.EditorID ?? DeadRefId;
            } // env disposed here, releasing the load order's hold on the target file

            foreach (var rec in myMod.EnumerateMajorRecords()) rec.IsCompressed = false;
            myMod.WriteToBinary(modFile, gen_quest_main.BuildWriteParams());
            Console.WriteLine();
            Console.WriteLine("  wrote " + modFile + " (" + new FileInfo(modFile).Length.ToString("N0") + " B)");

            // Verified OFF DISK. An assertion over the objects we just built in memory
            // cannot fail, and the whole point of this command is what landed in the file.
            Console.WriteLine();
            Console.WriteLine("  verification, re-read from disk:");
            var reread = StarfieldMod.CreateFromBinaryOverlay(modFile, StarfieldRelease.Starfield, readParams);
            failures += VerifyOnDisk(reread, PassQuestId, liveKey);
            failures += VerifyOnDisk(reread, FailQuestId, deadKey);

            Console.WriteLine();
            if (failures > 0)
            {
                Console.WriteLine("=== WRITTEN, BUT " + failures + " VERIFICATION FAILURE(S). Do not run the in-game test yet. ===");
                return 1;
            }

            Console.WriteLine("=== THE PAIR IS BUILT. THE REST IS HIS EYE. ===");
            Console.WriteLine();
            Console.WriteLine("  In the console, one at a time, on a fresh save each time:");
            Console.WriteLine("    startquest " + PassQuestId);
            Console.WriteLine("    startquest " + FailQuestId);
            Console.WriteLine();
            Console.WriteLine("  READING THE RESULT:");
            Console.WriteLine("    pass starts + fail does NOT  -> the generated condition IS evaluated. Gate B closes.");
            Console.WriteLine("    both start                   -> the condition is being IGNORED, and every green");
            Console.WriteLine("                                    up to now proved only that the bytes were well formed.");
            Console.WriteLine("    neither starts               -> something else is wrong; the pair says nothing");
            Console.WriteLine("                                    about the condition, so do not read it as a FAIL.");
            Console.WriteLine();
            Console.WriteLine("  The third outcome is why " + PassQuestId + " exists: it is the positive control,");
            Console.WriteLine("  and without it a quiet " + FailQuestId + " would be indistinguishable from a broken build.");
            return 0;
        }

        /// <summary>
        /// Clone the source quest, repoint its self-references, and swap the one condition.
        /// </summary>
        private static int BuildClone(IStarfieldMod myMod, IQuestGetter source, string newEditorId,
                                      FormKey conditionRefType, string conditionRefName, FormKey markerToReplace)
        {
            var copy = source.DeepCopy();
            var clone = new Quest(myMod)
            {
                EditorID = newEditorId,
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

            // A quest's script and alias-script properties hold FormLinks to the QUEST ITSELF
            // (that is how an alias-typed property is encoded). A clone that does not repoint
            // them drives the SOURCE quest's aliases, silently, and the clone looks correct in
            // every dump. Counted rather than assumed, and a count of zero is a refusal.
            int repointed = RepointSelfLinks(clone.VirtualMachineAdapter, source.FormKey, clone.FormKey);
            Console.WriteLine("  [" + newEditorId + "] " + clone.FormKey + "  self-links repointed: " + repointed);
            if (repointed == 0)
            {
                Console.WriteLine("    REFUSED: expected at least one self-reference to repoint and found none.");
                Console.WriteLine("    Either the encoding is not what this command assumes, or the clone is wrong.");
                return 1;
            }

            // Swap the one condition. Constructed fresh, never cloned: constructing it is the
            // thing under test.
            var loc = clone.Aliases?.OfType<QuestLocationAlias>()
                          .FirstOrDefault(a => a.Name == LocAliasName);
            if (loc == null || loc.Conditions == null)
            {
                Console.WriteLine("    REFUSED: no '" + LocAliasName + "' location alias with conditions on the clone.");
                return 1;
            }

            int swapped = 0;
            for (int i = 0; i < loc.Conditions.Count; i++)
            {
                var existing = loc.Conditions[i];
                if (existing.Data is not ILocationHasRefTypeConditionDataGetter had) continue;
                if (had.FirstParameter.Link.FormKey != markerToReplace) continue;

                var data = new LocationHasRefTypeConditionData();
                data.FirstParameter = new FormLinkOrIndex<ILocationReferenceTypeGetter>(data, conditionRefType);
                loc.Conditions[i] = new ConditionFloat
                {
                    Data = data,
                    CompareOperator = CompareOperator.EqualTo,
                    ComparisonValue = 1f,
                };
                swapped++;
            }

            if (swapped != 1)
            {
                Console.WriteLine("    REFUSED: expected to swap exactly 1 condition, swapped " + swapped + ".");
                Console.WriteLine("    A partial swap is worse than none: the alias would carry two filters.");
                return 1;
            }
            Console.WriteLine("    condition -> LocationHasRefType EqualTo 1  " + conditionRefName);
            return 0;
        }

        /// <summary>
        /// Repoint every FormLink in a quest's VMAD that points at the old quest. Walked by
        /// reflection: alias properties, quest script properties and alias script properties
        /// are three different shapes and a typed path would silently cover only one.
        /// </summary>
        private static int RepointSelfLinks(object vmad, FormKey oldKey, FormKey newKey)
        {
            int count = 0;
            Walk(vmad, oldKey, newKey, ref count, 0);
            return count;
        }

        private static void Walk(object node, FormKey oldKey, FormKey newKey, ref int count, int depth)
        {
            if (node == null || depth > 6) return;

            if (node is System.Collections.IEnumerable seq && node is not string)
            {
                foreach (var item in seq) Walk(item, oldKey, newKey, ref count, depth + 1);
                return;
            }

            var t = node.GetType();
            if (t.Namespace == null || !t.Namespace.StartsWith("Mutagen")) return;

            foreach (var pi in t.GetProperties())
            {
                if (pi.GetIndexParameters().Length > 0) continue;
                object val;
                try { val = pi.GetValue(node); } catch { continue; }
                if (val == null) continue;

                // A settable FormLink whose FormKey is the old quest: repoint it in place.
                var fkProp = val.GetType().GetProperty("FormKey");
                if (fkProp != null && fkProp.GetValue(val) is FormKey fk)
                {
                    if (fk == oldKey && fkProp.CanWrite)
                    {
                        fkProp.SetValue(val, newKey);
                        count++;
                    }
                    continue;
                }
                Walk(val, oldKey, newKey, ref count, depth + 1);
            }
        }

        private static int VerifyOnDisk(IStarfieldModGetter mod, string editorId, FormKey expectedRefType)
        {
            var q = mod.Quests.FirstOrDefault(x => x.EditorID == editorId);
            if (q == null)
            {
                Console.WriteLine("    [FAIL] " + editorId + " is not in the written file");
                return 1;
            }
            var loc = q.Aliases?.OfType<IQuestLocationAliasGetter>()
                       .FirstOrDefault(a => a.Name == LocAliasName);
            if (loc?.Conditions == null)
            {
                Console.WriteLine("    [FAIL] " + editorId + ": no " + LocAliasName + " conditions survived");
                return 1;
            }
            foreach (var c in loc.Conditions)
            {
                if (c.Data is not ILocationHasRefTypeConditionDataGetter had) continue;
                if (had.FirstParameter.Link.FormKey != expectedRefType) continue;
                Console.WriteLine("    [OK  ] " + editorId + ": LocationHasRefType -> " + expectedRefType);
                return 0;
            }
            Console.WriteLine("    [FAIL] " + editorId + ": no LocationHasRefType naming " + expectedRefType);
            return 1;
        }
    }
}
