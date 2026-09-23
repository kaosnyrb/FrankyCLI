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
    /// THE FIRST DELVE, LAYER 1 -- his 2026-09-23 ruling: "let's get to the 'open the dropped
    /// container' stage then end the quest ... aliases freak out if they are wrong it's better to
    /// build in layers."
    ///
    /// Beat one of Jessica's testcase 01 (office/projects/delves/testcase-01-two-halves.md) and
    /// nothing else: a container dropped short of where it was going, at a travel marker inside a
    /// drawn POI. Find it, open it, quest ends. The remaining three beats are later layers.
    ///
    /// ⭐ IT IS A MINIMUM DIFF FROM A QUEST THAT DEMONSTRABLY WORKS, which is the whole of his
    /// layering argument. duo_artifact_local_qst04a is the base the gate pair cloned this morning
    /// and it ran: it drew a POI, filled a ref alias from a marker inside it, and create-obj'd at
    /// that marker. Layer 1 changes four things and keeps everything else, including the quest
    /// script, so anything that breaks is attributable to one of the four.
    ///
    /// WHAT IT CHANGES, and why each one:
    ///   1. DungeonLocation's LocationHasRefType: REWoundedMarkerLocRef -> RETravelA1LocRef.
    ///      Jessica's beat one sits on the travel ring, "dropped short of where it was going".
    ///      RETravelA1 over RETravelB1 (which is 100.0% of POIs against A1's 99.6%) because A1
    ///      carries SHIPPED PRECEDENT -- 8 Overtime quests fill from it, and Gate B was proven on
    ///      it. Four tenths of a percent of pool is not worth trading precedent for.
    ///   2. The same swap on BountyTargetMarker's ALLA fill, so the marker the alias fills from is
    ///      the marker the location was selected FOR. These two must agree or the quest selects a
    ///      POI on one marker and then hunts for a different one.
    ///   3. The theme condition is REMOVED. Testcase 01 is deliberately themeless, and the base
    ///      excludes Natural, which is 42% of the pool on its own.
    ///   4. The create-obj object: a Crimson Fleet boss -> Loot_Storage_Miscbox_Crate_Large_Rare.
    ///      The alias SLOT is unchanged. duo_artifact_localcargo_qst01a already puts a lootable
    ///      container in exactly this slot, so this is a swap into a shipped shape rather than a
    ///      new mechanism.
    ///
    /// ⛔ WHAT IT DELIBERATELY DOES NOT DO. It does not add the completing stage or the activation
    /// hook, because gen_queststage already does both and has been bitten doing them. Composing two
    /// proven tools beats growing a third. The exact commands are printed at the end.
    ///
    /// ⚠ ALIAS NAMES ARE LEFT ALONE, including "BountyTarget" now holding a crate. Renaming is
    /// cosmetic and every rename is a chance to break a binding I cannot see; the objective text is
    /// rewritten instead and simply does not reference that alias.
    ///
    /// Usage: gen_delvelayer1 [modname] [sourceQuestEditorId]
    /// </summary>
    public static class gen_delvelayer1
    {
        private const string NewQuestId = "duo_delve01_layer1";
        private const string LocAliasName = "DungeonLocation";
        private const string MarkerAliasName = "BountyTargetMarker";
        private const string TargetAliasName = "BountyTarget";
        private const string OldMarker = "REWoundedMarkerLocRef";
        private const string NewMarker = "RETravelA1LocRef";
        private const string ThemeToDrop = "LocTypeOE_ThemeNaturalKeyword";
        private const string CrateEditorId = "Loot_Storage_Miscbox_Crate_Large_Rare";
        private const string ObjectiveText = "Recover the dropped container at <Alias=DungeonLocation>";

        public static int Run(string modname = "du_overtime", string srcEditorId = "duo_artifact_local_qst04a")
        {
            Console.WriteLine("=== DELVE 01, LAYER 1: find the dropped container, open it, quest ends ===");
            Console.WriteLine();

            StarfieldMod myMod;
            string modFile;
            Mutagen.Bethesda.Plugins.Binary.Parameters.BinaryReadParameters readParams;
            FormKey newMarkerKey, crateKey, oldMarkerKey;
            int failures = 0;

            // Scoped and disposed before the write. GameEnvironment memory-maps every listed
            // plugin including the one being overwritten, and a live env makes WriteToBinary throw
            // "used by another process" against our OWN handle. That cost a misdiagnosis earlier
            // today on gen_queststage, where the error looked like the game holding the file.
            using (var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build())
            {
                readParams = gen_quest_main.BuildReadParams(env.LoadOrder);
                modFile = Path.Combine(env.DataFolderPath, modname + ".esm");
                if (!File.Exists(modFile))
                {
                    Console.WriteLine("  REFUSED: " + modFile + " does not exist. Nothing written.");
                    return 1;
                }

                // Everything resolved by EditorID, never hardcoded as a FormID: a magic number is
                // one nobody can check, and resolving proves the thing is really there.
                var marker = env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>()
                    .FirstOrDefault(x => string.Equals(x.EditorID, NewMarker, StringComparison.OrdinalIgnoreCase));
                if (marker == null)
                {
                    Console.WriteLine("  REFUSED: could not resolve " + NewMarker + ". Nothing written.");
                    return 1;
                }
                var crate = env.LoadOrder.PriorityOrder.WinningOverrides<IContainerGetter>()
                    .FirstOrDefault(x => string.Equals(x.EditorID, CrateEditorId, StringComparison.OrdinalIgnoreCase));
                if (crate == null)
                {
                    Console.WriteLine("  REFUSED: could not resolve container " + CrateEditorId + ". Nothing written.");
                    return 1;
                }
                // The OLD marker is resolved too, so the swap can match on its key rather than on
                // position or on "the one that is not the map marker". A positional rule would
                // quietly swap the map marker the day the base's condition order changed.
                var oldMarker = env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>()
                    .FirstOrDefault(x => string.Equals(x.EditorID, OldMarker, StringComparison.OrdinalIgnoreCase));
                if (oldMarker == null)
                {
                    Console.WriteLine("  REFUSED: could not resolve " + OldMarker + ". Nothing written.");
                    return 1;
                }
                newMarkerKey = marker.FormKey;
                crateKey = crate.FormKey;
                oldMarkerKey = oldMarker.FormKey;

                Console.WriteLine("  marker   : " + marker.EditorID + " " + marker.FormKey);
                Console.WriteLine("  container: " + crate.EditorID + " " + crate.FormKey);
                Console.WriteLine();

                myMod = StarfieldMod.CreateFromBinary(modFile, StarfieldRelease.Starfield, readParams);
                gen_quest_main.FixNextFormId(myMod);

                var source = myMod.Quests.FirstOrDefault(q => q.EditorID == srcEditorId);
                if (source == null)
                {
                    Console.WriteLine("  REFUSED: no quest '" + srcEditorId + "' in " + modname + ". Nothing written.");
                    return 1;
                }
                Console.WriteLine("  source   : " + source.EditorID + " " + source.FormKey);

                // Re-running must not stack duplicates. Keys collected first: removing while
                // enumerating the group is how a sweep eats its own feet.
                var stale = myMod.Quests.Where(q => q.EditorID == NewQuestId).Select(q => q.FormKey).ToList();
                foreach (var k in stale) myMod.Quests.Remove(k);
                if (stale.Count > 0) Console.WriteLine("  cleaned  : " + stale.Count + " previous " + NewQuestId);

                var copy = source.DeepCopy();
                var clone = new Quest(myMod)
                {
                    EditorID = NewQuestId,
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

                // A clone that does not repoint its self-links drives the SOURCE quest's aliases,
                // silently, and looks correct in every dump. Shared with gen_delvegatetest rather
                // than copied. A count of zero is a refusal, not a shrug.
                int repointed = gen_delvegatetest.RepointSelfLinks(clone.VirtualMachineAdapter, source.FormKey, clone.FormKey);
                Console.WriteLine("  clone    : " + clone.FormKey + "  self-links repointed: " + repointed);
                if (repointed == 0)
                {
                    Console.WriteLine("  REFUSED: expected at least one self-reference and found none. Nothing written.");
                    return 1;
                }
                Console.WriteLine();

                failures += RewireLocation(clone, oldMarkerKey, newMarkerKey);
                failures += RewireMarkerFill(clone, newMarkerKey);
                failures += RewireCreateObject(clone, crateKey);
                failures += RewireObjective(clone);
                failures += RewirePlayerFacingText(clone);

                if (failures > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("=== " + failures + " problem(s) rewiring. NOTHING WRITTEN. ===");
                    return 1;
                }
            } // env disposed here, releasing the load order's hold on the target file

            foreach (var rec in myMod.EnumerateMajorRecords()) rec.IsCompressed = false;
            myMod.WriteToBinary(modFile, gen_quest_main.BuildWriteParams());
            Console.WriteLine();
            Console.WriteLine("  wrote " + modFile + " (" + new FileInfo(modFile).Length.ToString("N0") + " B)");

            // Verified OFF DISK. An assertion over the objects just built in memory cannot fail,
            // and what landed in the file is the only thing that matters.
            Console.WriteLine();
            Console.WriteLine("  verification, re-read from disk:");
            var reread = StarfieldMod.CreateFromBinaryOverlay(modFile, StarfieldRelease.Starfield, readParams);
            var q = reread.Quests.FirstOrDefault(x => x.EditorID == NewQuestId);
            if (q == null)
            {
                Console.WriteLine("    FAIL: the quest is not in the written file.");
                return 1;
            }

            var loc = q.Aliases?.OfType<IQuestLocationAliasGetter>().FirstOrDefault(a => a.Name == LocAliasName);
            int conds = loc?.Conditions?.Count ?? 0;
            bool locOnNew = loc?.Conditions?.Any(c => c.Data is ILocationHasRefTypeConditionDataGetter h
                                                      && h.FirstParameter.Link.FormKey == newMarkerKey) ?? false;
            bool themeGone = !(loc?.Conditions?.Any(c => c.Data is ILocationHasKeywordConditionDataGetter) ?? false);

            var mk = q.Aliases?.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.Name == MarkerAliasName);
            bool fillOnNew = mk?.Location?.RefType.FormKey == newMarkerKey;

            var tg = q.Aliases?.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.Name == TargetAliasName);
            bool crateSet = tg?.CreateReferenceToObject?.Object.FormKey == crateKey;

            string objText = q.Objectives?.FirstOrDefault()?.DisplayText?.String ?? "";

            failures += Check("location condition names " + NewMarker, locOnNew ? "yes" : "no", "yes");
            failures += Check("theme condition removed", themeGone ? "yes" : "no", "yes");
            failures += Check("location conditions remaining", conds.ToString(), "3");
            failures += Check("marker alias fills from " + NewMarker, fillOnNew ? "yes" : "no", "yes");
            failures += Check("create-obj is the crate", crateSet ? "yes" : "no", "yes");
            failures += Check("objective rewritten", objText == ObjectiveText ? "yes" : "no", "yes");

            // NEGATIVE CONTROL. Every check above would report "yes" for a marker key that happened
            // to equal the old one, so prove the comparison can say no: the old marker must be gone
            // from both the condition and the fill. A control that cannot fail is not a control.
            Console.WriteLine();
            Console.WriteLine("  negative control (the OLD marker must be absent from both sites):");
            // Compared on the KEY, not on a resolved EditorID: the key is what the record holds
            // and resolving would make this control depend on a second lookup succeeding.
            bool oldInCond = loc?.Conditions?.Any(c => c.Data is ILocationHasRefTypeConditionDataGetter h2
                                                       && h2.FirstParameter.Link.FormKey == oldMarkerKey) ?? false;
            bool oldInFill = mk?.Location?.RefType.FormKey == oldMarkerKey;
            Console.WriteLine("    old marker in condition: " + (oldInCond ? "PRESENT" : "absent")
                              + " | in fill: " + (oldInFill ? "PRESENT" : "absent"));
            if (oldInCond || oldInFill)
            {
                Console.WriteLine("    FAIL: the old marker survived, so the swap was partial.");
                failures++;
            }
            else
            {
                Console.WriteLine("    control behaved: the old marker is gone from both, so the swap was real.");
            }

            Console.WriteLine();
            if (failures > 0)
            {
                Console.WriteLine("=== WRITTEN, BUT " + failures + " VERIFICATION FAILURE(S). Do not test yet. ===");
                return 1;
            }

            Console.WriteLine("=== LAYER 1 RECORDS ARE IN. TWO STEPS LEFT, BOTH ON PROVEN TOOLS ===");
            Console.WriteLine();
            Console.WriteLine("  FrankyCLI queststage " + modname + " stage " + NewQuestId
                              + " 200 --log \"The container was never going to get where it was headed.\" --complete");
            Console.WriteLine("  FrankyCLI queststage " + modname + " hook " + NewQuestId
                              + " " + TargetAliasName + " DefaultAliasOnActivate --stage 200 --prereq 0");
            Console.WriteLine();
            Console.WriteLine("  Then in game, on a throwaway save:");
            Console.WriteLine("    help " + NewQuestId + " 0");
            Console.WriteLine("    startquest <id>");
            Console.WriteLine("  Expect: an objective pointing at a POI, a storage crate at a travel");
            Console.WriteLine("  marker inside it, and the quest completing when you open the crate.");
            return 0;
        }

        private static int RewireLocation(Quest clone, FormKey oldMarker, FormKey newMarker)
        {
            var loc = clone.Aliases?.OfType<QuestLocationAlias>().FirstOrDefault(a => a.Name == LocAliasName);
            if (loc?.Conditions == null)
            {
                Console.WriteLine("  REFUSED: no '" + LocAliasName + "' alias with conditions.");
                return 1;
            }

            // Matched on the OLD marker's key, never on position and never on "the one that is not
            // the map marker". The base carries two LocationHasRefType conditions and the other is
            // MapMarkerRefType, which MUST survive: every shipped target alias in Overtime demands
            // it, because the player has to be able to navigate to the place.
            int swapped = 0;
            for (int i = 0; i < loc.Conditions.Count; i++)
            {
                if (loc.Conditions[i].Data is not ILocationHasRefTypeConditionDataGetter had) continue;
                if (had.FirstParameter.Link.FormKey != oldMarker) continue;

                // Constructed fresh rather than mutated: constructing the condition is the route
                // Gate B proved, and a mutated overlay is a different object than a built one.
                var data = new LocationHasRefTypeConditionData();
                data.FirstParameter = new FormLinkOrIndex<ILocationReferenceTypeGetter>(data, newMarker);
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
                Console.WriteLine("  REFUSED: expected exactly 1 marker condition to swap, got " + swapped
                                  + ". A partial swap leaves the alias carrying two filters.");
                return 1;
            }

            int before = loc.Conditions.Count;
            var themeConds = loc.Conditions.Where(c => c.Data is ILocationHasKeywordConditionDataGetter).ToList();
            foreach (var c in themeConds) loc.Conditions.Remove(c);
            Console.WriteLine("  [1] " + LocAliasName + ": marker -> " + NewMarker
                              + ", dropped " + themeConds.Count + " theme condition(s), "
                              + before + " -> " + loc.Conditions.Count + " conditions");
            return 0;
        }

        private static int RewireMarkerFill(Quest clone, FormKey newMarker)
        {
            var mk = clone.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.Name == MarkerAliasName);
            if (mk?.Location == null)
            {
                Console.WriteLine("  REFUSED: no '" + MarkerAliasName + "' alias with a location fill.");
                return 1;
            }
            mk.Location.RefType.SetTo(newMarker);
            Console.WriteLine("  [2] " + MarkerAliasName + ": ALLA fill -> " + NewMarker
                              + " (must match the condition, or the quest picks a POI on one marker and hunts another)");
            return 0;
        }

        private static int RewireCreateObject(Quest clone, FormKey crateKey)
        {
            var tg = clone.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.Name == TargetAliasName);
            if (tg?.CreateReferenceToObject == null)
            {
                Console.WriteLine("  REFUSED: no '" + TargetAliasName + "' alias with a create-obj fill.");
                return 1;
            }
            tg.CreateReferenceToObject.Object.SetTo(crateKey);
            Console.WriteLine("  [3] " + TargetAliasName + ": create-obj -> " + CrateEditorId
                              + " (same alias slot duo_artifact_localcargo_qst01a puts a container in)");
            return 0;
        }

        private static int RewireObjective(Quest clone)
        {
            var ob = clone.Objectives?.FirstOrDefault();
            if (ob == null)
            {
                Console.WriteLine("  REFUSED: the clone carries no objective to rewrite.");
                return 1;
            }
            ob.DisplayText = ObjectiveText;
            Console.WriteLine("  [4] objective " + ob.Index + ": \"" + ObjectiveText + "\"");
            return 0;
        }

        /// <summary>
        /// Every player-facing string on the clone is bounty prose, and leaving it would make the
        /// in-game test read as a bounty on a crate. Found by reading the built record back rather
        /// than by planning for it: the objective was rewritten and the NAME and the stage-0
        /// journal were not, because those are the two the alias-level work never touches.
        ///
        /// ⚠ PLACEHOLDERS, AND MARKED AS SUCH. Prose is Jessica's, and her testcase says the words
        /// wait on his ruling of generic versus themed. These exist so the test is legible, not
        /// because anyone has written the mission. Handing over a dressed placeholder beats handing
        /// over the question.
        /// </summary>
        private static int RewirePlayerFacingText(Quest clone)
        {
            clone.Name = "Delve: the dropped container";

            var stage0 = clone.Stages?.FirstOrDefault(s => s.Index == 0);
            var entry = stage0?.LogEntries?.FirstOrDefault();
            if (entry == null)
            {
                Console.WriteLine("  REFUSED: no stage 0 log entry to rewrite.");
                return 1;
            }
            entry.Entry = "Somebody was carrying this somewhere and never arrived. "
                        + "The container is still out at <Alias=DungeonLocation>.";
            Console.WriteLine("  [5] name + stage 0 journal rewritten (PLACEHOLDER prose -- Jessica's lane)");
            return 0;
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
