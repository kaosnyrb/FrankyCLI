using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FrankyCLI
{
    /// <summary>
    /// THE BEATS KIND: a Delve with NO DRIVER. His ruling, 2026-10-08: "The stages of the quest are the
    /// state machine. All of vanilla Starfield was built this way." Proven first by the gen_stageprobe
    /// spike (duo_stageprobe, played: both objectives, completion, and `setstage` jumping a beat),
    /// which this kind graduates and which was deleted in the change that added it (git ba9a004).
    ///
    /// A recipe lists its beats, each with a TYPE. Each type is two halves, and nothing else:
    ///   - an EVENT: a stock Default* alias script that sets the beat's stage, gated on the previous
    ///     step's stage (PrereqStage). This is data; no Papyrus of ours runs to notice anything.
    ///   - a FRAGMENT: a few lines in one generated fragment script, bound to that stage, doing the half
    ///     that is Papyrus-only (objectives, the counter, taking delivered items, a message box).
    ///
    ///   use      DefaultAliasOnActivate          hides its prompt after use
    ///   pickup   DefaultAliasOnActivateGiveItem  gives the beat's item, disables the object
    ///   deliver  DefaultAliasOnActivate          the fragment takes every item picked up so far
    ///
    /// ⛔ DELIVER IS NOT DefaultAliasOnActivateRemoveItems, and it looks like it should be. That script's
    /// parent SETS THE STAGE FIRST and only then runs the item check (DefaultAlias.
    /// CheckAndSetStageAndCallDoSpecificThing: SafeSetStage, then DoSpecificThing), so as a gate it would
    /// complete the beat empty-handed and merely show a message. The gate here is STAGE ORDER: deliver's
    /// prereq is a stage only a pickup can set.
    ///
    /// A GROUP is consecutive `use` beats sharing a "group" name: done in ANY order, one objective with a
    /// vanilla counter "(<Global=X>/N)", each member's fragment calls ModObjectiveGlobal, and the last
    /// one sets the group's own done stage. The counter global is reset in stage 0's fragment, because
    /// these quests are REPEATABLE (his fact, 2026-10-08) and a restart must not begin at 3/3.
    ///
    /// Stages: 0 runs on start; then 10, 20, ... in beat order, with a group taking one extra for its
    /// done stage; the LAST step always lands on the base's completing stage (100), which carries the
    /// reward. A stage the base already has (50) is reused, never duplicated.
    /// </summary>
    public static partial class gen_delve
    {
        private static readonly string[] BeatTypes = { "use", "pickup", "deliver" };

        /// <summary>One step of a beats Delve: a single beat, or an any-order group of them.</summary>
        private sealed class Step
        {
            public List<int> Beats = new();          // indices into r.beats
            public string? Group;                    // null = a single beat
            public int Objective;                    // objective index, 10 * (step + 1)
            public int DoneStage;                    // the stage that means this step is finished
            public bool IsGroup => Group != null;
        }

        /// <summary>Split the beats into steps. Pure, so the lint and the build agree by construction.</summary>
        private static List<Step> StepsOf(Recipe r)
        {
            var steps = new List<Step>();
            for (int i = 0; i < r.beats.Count; i++)
            {
                var g = r.beats[i].group;
                if (g != null && steps.Count > 0 && steps[^1].Group == g) { steps[^1].Beats.Add(i); continue; }
                steps.Add(new Step { Beats = { i }, Group = g });
            }
            for (int k = 0; k < steps.Count; k++) steps[k].Objective = 10 * (k + 1);
            return steps;
        }

        /// <summary>
        /// Stage numbers: every beat gets one, a group also gets a done stage, the last step finishes on
        /// the completing stage. Returns beat index -> stage, and fills each step's DoneStage.
        /// </summary>
        private static Dictionary<int, int> StagesOf(List<Step> steps, int complete)
        {
            var beatStage = new Dictionary<int, int>();
            int next = 10;
            for (int k = 0; k < steps.Count; k++)
            {
                var s = steps[k];
                bool last = k == steps.Count - 1;
                if (!s.IsGroup)
                {
                    int st = last ? complete : next;
                    if (!last) next += 10;
                    beatStage[s.Beats[0]] = st;
                    s.DoneStage = st;
                }
                else
                {
                    foreach (var b in s.Beats) { beatStage[b] = next; next += 10; }
                    s.DoneStage = last ? complete : next;
                    if (!last) next += 10;
                }
            }
            return beatStage;
        }

        /// <summary>The lint's beats-only half. Called from Grade; Fatal/Warn are Grade's.</summary>
        private static void GradeBeats(Recipe r, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                       Action<string> Fatal, Action<string> Warn)
        {
            for (int i = 0; i < r.beats.Count; i++)
            {
                var b = r.beats[i];
                if (b.type == null || !BeatTypes.Contains(b.type))
                    Fatal($"beat {i + 1} has type '{b.type ?? "(none)"}'; a beats Delve knows {string.Join(", ", BeatTypes)}.");
                if (b.type == "pickup" && string.IsNullOrWhiteSpace(b.item))
                    Fatal($"beat {i + 1} is a pickup with no item: the player is handed a thing with no name.");
                if (b.type != "pickup" && b.item != null)
                    Fatal($"beat {i + 1} names an item and is not a pickup, so it would be silently ignored.");
                if (b.group != null && b.type != "use")
                    Fatal($"beat {i + 1} is in group '{b.group}' and is a {b.type}; only use beats can be done in any order.");
                if (b.name != null && Tokens(b.name).Any())
                    Fatal($"beat {i + 1} name carries a <Token>; an activator name is not an alias context.");
                if (b.name != null && string.IsNullOrWhiteSpace(b.model))
                    Warn($"beat {i + 1} has a name and no model; a name only goes onto a clone, so it is not written.");
            }

            var steps = StepsOf(r);
            // A group name used twice, apart, is two groups an author meant as one.
            var seen = new HashSet<string>();
            foreach (var s in steps.Where(s => s.IsGroup))
                if (!seen.Add(s.Group!)) Fatal($"group '{s.Group}' appears in two places; a group's beats must be consecutive.");
            foreach (var s in steps)
            {
                if (s.IsGroup && s.Beats.Count < 2) Fatal($"group '{s.Group}' has one beat; a group of one is a beat.");
                for (int j = 0; j < s.Beats.Count; j++)
                {
                    var b = r.beats[s.Beats[j]];
                    bool carries = j == 0;
                    if (carries && string.IsNullOrWhiteSpace(b.objective))
                        Fatal($"beat {s.Beats[j] + 1} has no objective" + (s.IsGroup ? $" (it opens group '{s.Group}', and the group's one objective is its)" : ""));
                    if (!carries && !string.IsNullOrWhiteSpace(b.objective))
                        Fatal($"beat {s.Beats[j] + 1} is in group '{s.Group}' after its first beat and has an objective; the group shows ONE, "
                              + "with a counter, so this text would never reach the player.");
                }
                if (s.IsGroup && r.beats[s.Beats[0]].objective is string go && go.Contains('('))
                    Warn($"group '{s.Group}' objective has a bracket; the build appends the counter \"(n/{s.Beats.Count})\" itself.");
            }
            // The completing stage's line. A group's last member sets its stage and the completing stage in
            // one instant, and the log shows only the newest line, so a Delve ENDING on a group needs a recap
            // or the player reads the base's. Ending on a single beat, that beat's journal is the line.
            if (steps.Count > 0 && steps[^1].IsGroup && string.IsNullOrWhiteSpace(r.recap))
                Fatal($"the Delve ends on group '{steps[^1].Group}', so stage {t.completeStage} is the line the player reads last "
                      + "and it would carry the base's recap; write a \"recap\".");
            if (steps.Count > 0 && !steps[^1].IsGroup && r.recap != null)
                Fatal($"the Delve ends on a single beat, whose journal is already stage {t.completeStage}'s line; the recap would overwrite it. "
                      + "Put the words in that beat's journal.");

            // Delivering needs something in hand: the first deliver must come after a pickup.
            int carried = 0;
            for (int i = 0; i < r.beats.Count; i++)
            {
                if (r.beats[i].type == "pickup") carried++;
                else if (r.beats[i].type == "deliver")
                {
                    if (carried == 0) Fatal($"beat {i + 1} is a deliver with nothing picked up before it, so there is nothing to hand over.");
                    carried = 0;
                }
            }
            if (carried > 0) Warn($"{carried} picked-up item(s) are never delivered, so they stay in the player's inventory after the quest.");

            var beatStage = StagesOf(steps, t.completeStage);
            int highest = beatStage.Values.Concat(steps.Select(s => s.DoneStage)).Where(x => x != t.completeStage).DefaultIfEmpty(0).Max();
            if (highest >= t.completeStage)
                Fatal($"the beats number stages up to {highest}, at or past the completing stage {t.completeStage}: too many beats.");
            Console.WriteLine("  steps    : " + string.Join("  ", steps.Select((s, k) =>
                $"[{k + 1}] " + (s.IsGroup ? $"group '{s.Group}' x{s.Beats.Count} -> {s.DoneStage}" : $"{r.beats[s.Beats[0]].type} -> {s.DoneStage}"))));

            var items = r.beats.Where(b => b.item != null).Select(b => b.item!).ToList();
            if (items.SelectMany(Tokens).Any()) Fatal("an item name carries a <Token>; item names are not alias contexts.");
            else if (items.Count > 0)
            {
                if (items.Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count) Fatal("two pickups name the same item.");
                ItemNameCollisions(env, r, items, Fatal);
            }
            foreach (var (b, i) in r.beats.Select((b, i) => (b, i)))
            {
                if (string.IsNullOrWhiteSpace(b.model)) continue;
                if (FindAsset(env.DataFolderPath.Path, t.mod, b.model!) == "" && !(IndexOf(env).ByModel.ContainsKey(b.model!)))
                    Fatal($"beat {i + 1} model '{b.model}' is used by no record and is not in Data or the mod's archives; it would render nothing.");
            }
            foreach (var (b, i) in r.beats.Select((b, i) => (b, i)))
            {
                var m = b.message;
                if (m == null) continue;
                if (string.IsNullOrWhiteSpace(m.text)) Fatal($"beat {i + 1} has a message with no text: an empty pausing box.");
                if (string.IsNullOrWhiteSpace(m.title)) Fatal($"beat {i + 1} has a message with no title.");
            }
            string fname = FragmentScriptName(r);
            if (fname.Length > 38) Fatal($"the fragment script name {fname} is {fname.Length} chars; the Papyrus compiler refuses over 38. Shorten the id.");
        }

        private static string FragmentScriptName(Recipe r) => "duo_qf_" + r.id;

        private static string FragmentFunction(int stage) => $"Fragment_Stage_{stage:D4}_Item_00";

        /// <summary>What BuildBeats made, for VerifyBeats to read back off disk.</summary>
        private sealed class BeatsMade
        {
            public string Script = "";
            public List<int> FragStages = new();
            public List<(int alias, string script, int set, int prereq)> Hooks = new();
            public List<FormKey> Globals = new();
            public Dictionary<int, string> ObjectiveText = new();
            public string PscPath = "";
            public string? Recap;                                        // the completing stage's line, when ending on a group
            public List<(int objective, int alias, int stage)> TargetGates = new();  // a group member's target, lit until its stage
        }

        private static int BuildBeats(StarfieldMod myMod, Quest clone, Template t, Recipe r,
                                      Dictionary<string, FormKey> markers, BuildMade made, BeatsMade bm)
        {
            var vma = clone.VirtualMachineAdapter;
            if (vma == null) { Console.WriteLine("REFUSED: the base has no VMAD."); return 1; }

            // --- 1. no driver ---------------------------------------------------------------------------
            var old = vma.Scripts.FirstOrDefault(s => string.Equals(s.Name, t.replacesDriver, StringComparison.OrdinalIgnoreCase));
            if (old == null) { Console.WriteLine($"REFUSED: the base carries no '{t.replacesDriver}' to remove."); return 1; }
            var cargo = old.Properties.OfType<ScriptObjectProperty>()
                .FirstOrDefault(p => string.Equals(p.Name, "CargoObject", StringComparison.OrdinalIgnoreCase))?.Object.FormKey;
            var itemSrc = cargo == null ? null : myMod.MiscItems.FirstOrDefault(m => m.FormKey == cargo.Value);
            vma.Scripts.Remove(old);
            if (vma.Scripts.Count != 0)
            { Console.WriteLine("REFUSED: the base carries other quest scripts: " + string.Join(", ", vma.Scripts.Select(s => s.Name))); return 1; }
            Console.WriteLine($"  driver   : {t.replacesDriver} REMOVED; the stages are the state machine");

            var steps = StepsOf(r);
            var beatStage = StagesOf(steps, t.completeStage);

            // --- 2. one marker + activator per beat -------------------------------------------------------
            // Beats 1 and 2 use the base's own two slots; every further beat is the first slot CLONED (the
            // same argument as AddBeatSlot: a slot that works brings every field this tool does not know).
            var slotOf = new Dictionary<int, BeatSlot>();
            var src0 = t.beatSlots[0];
            var srcMarker = clone.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)src0.markerAlias);
            var srcAct = clone.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)src0.activatorAlias);
            if (srcMarker?.Location == null || srcAct?.CreateReferenceToObject == null)
            { Console.WriteLine("REFUSED: template slot 0 is not a marker+activator pair on this base."); return 1; }
            for (int i = 0; i < r.beats.Count; i++)
            {
                if (i < t.beatSlots.Count)
                {
                    slotOf[i] = new BeatSlot { markerAlias = t.beatSlots[i].markerAlias, activatorAlias = t.beatSlots[i].activatorAlias,
                                               journalStage = beatStage[i] };
                    continue;
                }
                uint next = 1 + clone.Aliases!.SelectMany(Flatten).Select(x => x.id).DefaultIfEmpty(0u).Max();
                var m = srcMarker.DeepCopy(); m.ID = next; m.Name = "DelveBeat" + (i + 1) + "Marker";
                var a = srcAct.DeepCopy(); a.ID = next + 1; a.Name = "DelveBeat" + (i + 1) + "Target";
                a.CreateReferenceToObject!.AliasID = (short)m.ID;
                clone.Aliases.Add(m);
                clone.Aliases.Add(a);
                slotOf[i] = new BeatSlot { markerAlias = (int)m.ID, activatorAlias = (int)a.ID, journalStage = beatStage[i] };
                Console.WriteLine($"  +slot    : beat {i + 1} -- marker alias {m.ID}, activator {a.ID}");
            }

            // --- 3. stages: one per beat, one per group's done, cloned from a working plain stage ---------
            var allStages = beatStage.Values.Concat(steps.Select(s => s.DoneStage)).Distinct().OrderBy(x => x).ToList();
            foreach (var st in allStages)
            {
                if (clone.Stages!.Any(s => s.Index == st)) continue;
                var ns = CloneStage(clone, st);
                if (ns == null) return 1;
                clone.Stages.Add(ns);
                Console.WriteLine($"  +stage   : {st}");
            }
            // Reused stages (the base's 50) carry the base's journal line; clear it unless a beat writes one.
            foreach (var st in clone.Stages!.Where(s => allStages.Contains(s.Index) && s.Index != t.completeStage))
                if (st.LogEntries.FirstOrDefault() is QuestLogEntry e) e.Entry = null;
            // Ending on a group, the completing stage's line is the recipe's recap (the lint requires it).
            if (steps[^1].IsGroup)
            {
                var cs = clone.Stages.FirstOrDefault(s => s.Index == t.completeStage);
                if (cs?.LogEntries.FirstOrDefault() is not QuestLogEntry ce)
                { Console.WriteLine($"REFUSED: the base's completing stage {t.completeStage} has no log entry to carry the recap."); return 1; }
                bm.Recap = Expand(r.recap!, t);
                ce.Entry = bm.Recap;
                Console.WriteLine($"  recap    : stage {t.completeStage} \"{bm.Recap}\"");
            }
            var s0 = clone.Stages.FirstOrDefault(s => s.Index == 0);
            if (s0 == null) { Console.WriteLine("REFUSED: the base has no stage 0 to run on start."); return 1; }
            s0.Flags |= QuestStage.Flag.RunOnStart;

            // --- 4. fills, journals, reskins ---------------------------------------------------------
            for (int i = 0; i < r.beats.Count; i++)
            {
                int f = WriteBeat(clone, t, slotOf[i], r.beats[i], markers, true, PlaceAliasOf(t, r.beats[i].PlaceIndex, made.ThirdPlaceAlias));
                if (f > 0) return f;
                if (ReskinActivator(myMod, clone, t, $"{r.id}_b{i + 1}", slotOf[i].activatorAlias, r.beats[i].model, r.beats[i].name) != 0) return 1;
            }

            // --- 5. items, one clone per pickup -----------------------------------------------------------
            foreach (var k in myMod.MiscItems.Where(m => m.EditorID != null && m.EditorID.StartsWith(r.id + "_item")).Select(m => m.FormKey).ToList())
                myMod.MiscItems.Remove(k);
            var itemOf = new Dictionary<int, FormKey>();
            for (int i = 0; i < r.beats.Count; i++)
            {
                if (r.beats[i].type != "pickup") continue;
                if (itemSrc == null) { Console.WriteLine($"REFUSED: the removed driver's CargoObject is not a MiscItem in {t.mod} to clone."); return 1; }
                var mi = myMod.MiscItems.DuplicateInAsNewRecord(itemSrc);
                mi.EditorID = $"{r.id}_item{i + 1}";
                mi.Name = r.beats[i].item!;
                itemOf[i] = mi.FormKey;
                Console.WriteLine($"  +item    : {mi.EditorID} {mi.FormKey}  \"{mi.Name}\"  (clone of {itemSrc.EditorID})");
            }

            // --- 6. counters: one global per group, cloned, listed for <Global=> in the objective text -------
            foreach (var k in myMod.Globals.Where(g => g.EditorID != null && g.EditorID.StartsWith(r.id + "_count")).Select(g => g.FormKey).ToList())
                myMod.Globals.Remove(k);
            var globalSrc = myMod.Globals.FirstOrDefault(g => g.EditorID == "duo_reward_xp_easy");
            var counterOf = new Dictionary<int, (FormKey key, string edid)>();   // step index -> counter
            for (int k = 0; k < steps.Count; k++)
            {
                if (!steps[k].IsGroup) continue;
                if (globalSrc == null) { Console.WriteLine($"REFUSED: no global duo_reward_xp_easy in {t.mod} to clone a counter from."); return 1; }
                var g = (Global)myMod.Globals.DuplicateInAsNewRecord(globalSrc);
                g.EditorID = $"{r.id}_count{k + 1}";
                SetGlobalZero(g);
                counterOf[k] = (g.FormKey, g.EditorID);
                // Init-only and pre-constructed on Quest (part 13): append, never assign.
                clone.TextDisplayGlobals.Add(g.ToLink<IGlobalGetter>());
                bm.Globals.Add(g.FormKey);
                Console.WriteLine($"  +counter : {g.EditorID} {g.FormKey} ({g.GetType().Name}, clone of {globalSrc.EditorID}), on the quest's text-display globals");
            }

            // --- 7. objectives: one per step, targets = its beats' activators --------------------------------
            var lastOb = clone.Objectives?.OrderBy(o => o.Index).LastOrDefault();
            if (lastOb?.Targets == null || lastOb.Targets.Count != 1)
            { Console.WriteLine("REFUSED: the base's last objective is not a single-target objective to clone."); return 1; }
            var obSrc = lastOb.DeepCopy();
            clone.Objectives!.RemoveAll(o => !steps.Any(s => s.Objective == o.Index));
            for (int k = 0; k < steps.Count; k++)
            {
                var s = steps[k];
                var ob = clone.Objectives.FirstOrDefault(o => o.Index == s.Objective);
                if (ob == null) { ob = obSrc.DeepCopy(); ob.Index = (ushort)s.Objective; clone.Objectives.Add(ob); }
                var tgt = ob.Targets![0].DeepCopy();
                ob.Targets.Clear();
                foreach (var b in s.Beats)
                {
                    var tt = tgt.DeepCopy();
                    tt.AliasID = slotOf[b].activatorAlias;
                    if (s.IsGroup)
                    {
                        // A group's ONE objective targets every member and stays up until all are done, so
                        // each member's marker must go out on its own: lit while its stage is NOT done.
                        // Vanilla's way (1,006 of Starfield.esm's 2,968 targets are conditioned, 694 by
                        // GetStageDone; shape copied off SFTA06 obj 1135). Clearing the alias in the
                        // fragment was tried first and does NOT drop the marker (his play, 2026-10-09).
                        var d = new GetStageDoneConditionData { RunOnType = Condition.RunOnType.Subject, SecondParameter = beatStage[b] };
                        d.FirstParameter = new FormLinkOrIndex<IQuestGetter>(d, clone.FormKey);
                        tt.Conditions.Clear();
                        tt.Conditions.Add(new ConditionFloat { Data = d, CompareOperator = CompareOperator.EqualTo, ComparisonValue = 0f });
                        bm.TargetGates.Add((s.Objective, slotOf[b].activatorAlias, beatStage[b]));
                    }
                    ob.Targets.Add(tt);
                }
                string text = Expand(r.beats[s.Beats[0]].objective!, t);
                if (s.IsGroup) text += $" (<Global={counterOf[k].edid}>/{s.Beats.Count})";
                ob.DisplayText = text;
                bm.ObjectiveText[s.Objective] = text;
                Console.WriteLine($"  objective: {s.Objective} \"{text}\" -> alias(es) {string.Join(", ", s.Beats.Select(b => slotOf[b].activatorAlias))}");
            }
            var sortedObs = clone.Objectives.OrderBy(o => o.Index).ToList();
            clone.Objectives.Clear();
            clone.Objectives.AddRange(sortedObs);

            // --- 8. message boxes ---------------------------------------------------------------------
            foreach (var k in myMod.Messages.Where(m => m.EditorID != null && m.EditorID.StartsWith(r.id + "_msg")).Select(m => m.FormKey).ToList())
                myMod.Messages.Remove(k);
            var msgOf = new Dictionary<int, FormKey>();
            var msgSrc = myMod.Messages.FirstOrDefault(m => m.EditorID == MessageTemplate);
            for (int i = 0; i < r.beats.Count; i++)
            {
                var bmsg = r.beats[i].message;
                if (bmsg == null) continue;
                if (msgSrc == null) { Console.WriteLine($"REFUSED: no message template '{MessageTemplate}' in {t.mod}."); return 1; }
                var msg = myMod.Messages.DuplicateInAsNewRecord(msgSrc);
                msg.EditorID = $"{r.id}_msg{i + 1}";
                msg.Name = Expand(bmsg.title!, t);
                msg.Description = Expand(bmsg.text!, t);
                msg.OwnerQuest.SetTo(clone.FormKey);
                msgOf[i] = msg.FormKey;
                Console.WriteLine($"  +message : {msg.EditorID} {msg.FormKey}  \"{msg.Name}\"");
            }

            // --- 9. the events: one stock hook per beat --------------------------------------------------
            for (int k = 0; k < steps.Count; k++)
            {
                int prereq = k == 0 ? 0 : steps[k - 1].DoneStage;
                foreach (var b in steps[k].Beats)
                {
                    var beat = r.beats[b];
                    string script = beat.type == "pickup" ? "DefaultAliasOnActivateGiveItem" : "DefaultAliasOnActivate";
                    var entry = new QuestFragmentAlias();
                    entry.Property.Object.SetTo(clone.FormKey);
                    entry.Property.Alias = (short)slotOf[b].activatorAlias;
                    var sc = new ScriptEntry { Name = script };
                    sc.Properties.Add(new ScriptIntProperty { Name = "StageToSet", Data = beatStage[b], Flags = ScriptProperty.Flag.Edited });
                    sc.Properties.Add(new ScriptIntProperty { Name = "PrereqStage", Data = prereq, Flags = ScriptProperty.Flag.Edited });
                    if (beat.type == "pickup")
                    {
                        var ip = new ScriptObjectProperty { Name = "ItemToGive", Flags = ScriptProperty.Flag.Edited };
                        ip.Object.SetTo(itemOf[b]);
                        sc.Properties.Add(ip);
                        sc.Properties.Add(new ScriptBoolProperty { Name = "ShouldDisableAfterSuccessfulActivation", Data = true, Flags = ScriptProperty.Flag.Edited });
                    }
                    else
                        sc.Properties.Add(new ScriptBoolProperty { Name = "ShouldHideActivationAfterSuccessfulActivation", Data = true, Flags = ScriptProperty.Flag.Edited });
                    entry.Scripts.Add(sc);
                    vma.Aliases.Add(entry);
                    bm.Hooks.Add((slotOf[b].activatorAlias, script, beatStage[b], prereq));
                    Console.WriteLine($"  +hook    : beat {b + 1} ({beat.type}) alias {slotOf[b].activatorAlias} {script} sets {beatStage[b]} after {prereq}");
                }
            }

            // --- 10. the fragment script: generated, bound per stage -----------------------------------------
            string name = FragmentScriptName(r);
            var props = new List<(string type, string pname, FormKey key)>();
            var code = new SortedDictionary<int, List<string>>();
            void Add(int st, string line) { if (!code.ContainsKey(st)) code[st] = new(); code[st].Add(line); }

            foreach (var kv in counterOf)
            {
                string p = $"Counter{kv.Key + 1}";
                props.Add(("GlobalVariable", p, kv.Value.key));
                Add(0, $"{p}.SetValue(0)");
                Add(0, $"UpdateCurrentInstanceGlobal({p})");
            }
            Add(0, $"SetObjectiveDisplayed({steps[0].Objective})");
            var held = new List<int>();   // pickups not yet delivered
            for (int k = 0; k < steps.Count; k++)
            {
                var s = steps[k];
                bool last = k == steps.Count - 1;
                foreach (var b in s.Beats)
                {
                    int st = beatStage[b];
                    if (msgOf.ContainsKey(b)) { props.Add(("Message", $"Beat{b + 1}Message", msgOf[b])); Add(st, $"Beat{b + 1}Message.Show()"); }
                    if (r.beats[b].type == "pickup") held.Add(b);
                    if (r.beats[b].type == "deliver")
                    {
                        foreach (var h in held)
                        {
                            props.Add(("Form", $"Item{h + 1}", itemOf[h]));
                            Add(st, $"Game.GetPlayer().RemoveItem(Item{h + 1}, 1)");
                        }
                        held.Clear();
                    }
                    if (s.IsGroup)
                    {
                        Add(st,$"If ModObjectiveGlobal(1.0, Counter{k + 1}, {s.Objective}, {s.Beats.Count}.0)");
                        Add(st, $"    SetStage({s.DoneStage})");
                        Add(st, "EndIf");
                    }
                }
                int done = s.DoneStage;
                if (!s.IsGroup) Add(done, $"SetObjectiveCompleted({s.Objective})");
                if (last) { Add(done, "CompleteQuest()"); Add(done, "Stop()"); }
                else Add(done, $"SetObjectiveDisplayed({steps[k + 1].Objective})");
            }

            var psc = new StringBuilder();
            psc.AppendLine($"Scriptname {name} Extends Quest Hidden Const");
            psc.AppendLine($"{{GENERATED by FrankyCLI gen_delve from data/delves/recipes/{r.id}.json. Do not edit: change the recipe");
            psc.AppendLine("and rebuild. The stage fragments of a beats Delve: the stages are the state machine, stock alias");
            psc.AppendLine("scripts set them, and these functions do the Papyrus-only half (objectives, counters, items).}");
            psc.AppendLine();
            foreach (var kv in code)
            {
                psc.AppendLine($"Function {FragmentFunction(kv.Key)}()");
                foreach (var line in kv.Value) psc.AppendLine("    " + line);
                psc.AppendLine("EndFunction");
                psc.AppendLine();
            }
            foreach (var p in props.DistinctBy(p => p.pname))
                psc.AppendLine($"{p.type} Property {p.pname} Auto Const Mandatory");

            vma.Script ??= new ScriptEntry();
            vma.Script.Name = name;
            vma.Script.Properties.Clear();
            foreach (var p in props.DistinctBy(p => p.pname))
            {
                var op = new ScriptObjectProperty { Name = p.pname, Flags = ScriptProperty.Flag.Edited };
                op.Object.SetTo(p.key);
                vma.Script.Properties.Add(op);
            }
            vma.Fragments.Clear();
            foreach (var st in code.Keys)
                vma.Fragments.Add(new QuestScriptFragment { Stage = (ushort)st, StageIndex = 0, ScriptName = name, FragmentName = FragmentFunction(st) });
            bm.Script = name;
            bm.FragStages = code.Keys.ToList();

            string dir = Path.Combine(PapyrusDir(), "gen");
            Directory.CreateDirectory(dir);
            bm.PscPath = Path.Combine(dir, name + ".psc");
            File.WriteAllText(bm.PscPath, psc.ToString().Replace("\r\n", "\n"));
            Console.WriteLine($"  fragments: {name} on stages [{string.Join(", ", code.Keys)}], {props.DistinctBy(p => p.pname).Count()} propert(ies); source {bm.PscPath}");
            return 0;
        }

        /// <summary>A cloned counter starts at zero whatever the global it was cloned from holds.</summary>
        private static void SetGlobalZero(Global g) => g.Data = 0f;

        /// <summary>FrankyCLI/papyrus, found from the data directory's repo root.</summary>
        private static string PapyrusDir()
        {
            var root = ResolveDataDir();
            if (root == null) throw new InvalidOperationException("no data/delves to find the repo from");
            return Path.Combine(Directory.GetParent(Directory.GetParent(root)!.FullName)!.FullName, "papyrus");
        }

        /// <summary>Read the written plugin back OFF DISK and grade every claim the beats build made.</summary>
        private static int VerifyBeats(string modFile, Mutagen.Bethesda.Plugins.Binary.Parameters.BinaryReadParameters readParams,
                                       Recipe r, Template t, BeatsMade bm, string mastersBefore)
        {
            Console.WriteLine();
            Console.WriteLine("  verification, re-read from disk:");
            var m = StarfieldMod.CreateFromBinaryOverlay(modFile, StarfieldRelease.Starfield, readParams);
            int fail = 0;
            fail += Check("the plugin's masters are unchanged by the build",
                          string.Join(", ", m.ModHeader.MasterReferences.Select(x => x.Master.FileName.String)), mastersBefore);
            var q = m.Quests.FirstOrDefault(x => x.EditorID == r.id);
            if (q?.VirtualMachineAdapter == null) { Console.WriteLine("    FAIL: the quest or its VMAD is not in the written file."); return 1; }
            var vma = q.VirtualMachineAdapter;
            fail += Check("no quest-level script (no driver)", vma.Scripts.Count.ToString(), "0");
            fail += Check("fragment script", vma.Script?.Name ?? "none", bm.Script);
            fail += Check("fragment stages", string.Join(",", vma.Fragments.Select(f => f.Stage).OrderBy(x => x)), string.Join(",", bm.FragStages));
            foreach (var f in vma.Fragments)
                fail += Check($"stage {f.Stage} fragment", $"{f.ScriptName}.{f.FragmentName}", $"{bm.Script}.{FragmentFunction(f.Stage)}");
            fail += Check("stage 0 runs on start",
                          q.Stages.Any(s => s.Index == 0 && s.Flags.HasFlag(QuestStage.Flag.RunOnStart)).ToString(), "True");
            foreach (var h in bm.Hooks)
            {
                var sc = vma.Aliases.FirstOrDefault(a => a.Property.Alias == h.alias)?.Scripts.FirstOrDefault(s => s.Name == h.script);
                int? I(string n) => sc?.Properties.OfType<IScriptIntPropertyGetter>().FirstOrDefault(p => p.Name == n)?.Data;
                fail += Check($"alias {h.alias} {h.script}", $"sets {I("StageToSet")} after {I("PrereqStage")}", $"sets {h.set} after {h.prereq}");
            }
            foreach (var g in bm.Globals)
                fail += Check($"counter {g} is a text-display global", (q.TextDisplayGlobals?.Any(x => x.FormKey == g) ?? false).ToString(), "True");
            foreach (var kv in bm.ObjectiveText)
                fail += Check($"objective {kv.Key}", q.Objectives.FirstOrDefault(o => o.Index == kv.Key)?.DisplayText?.String ?? "missing", kv.Value);
            foreach (var (obj, alias, stage) in bm.TargetGates)
            {
                var tg = q.Objectives.FirstOrDefault(o => o.Index == obj)?.Targets?.FirstOrDefault(x => x.AliasID == alias);
                var c = tg?.Conditions.Count == 1 ? tg.Conditions[0] as IConditionFloatGetter : null;
                var gd = c?.Data as IGetStageDoneConditionDataGetter;
                string got = gd == null ? $"{tg?.Conditions.Count ?? -1} condition(s), not one GetStageDone"
                    : $"GetStageDone({gd.FirstParameter.Link.FormKey}, {gd.SecondParameter}) {c!.CompareOperator} {c.ComparisonValue} on {gd.RunOnType}";
                fail += Check($"objective {obj} target alias {alias} lit until its stage", got,
                              $"GetStageDone({q.FormKey}, {stage}) EqualTo 0 on Subject");
            }
            if (bm.Recap != null)
            {
                var ce = q.Stages.FirstOrDefault(s => s.Index == t.completeStage)?.LogEntries.FirstOrDefault();
                fail += Check($"stage {t.completeStage} recap", ce?.Entry?.String ?? "missing", bm.Recap);
                fail += Check($"stage {t.completeStage} still completes the quest",
                              (ce?.Flags?.HasFlag(QuestLogEntry.Flag.CompleteQuest) ?? false).ToString(), "True");
            }
            fail += Check("generated fragment source exists", File.Exists(bm.PscPath).ToString(), "True");
            Console.WriteLine();
            if (fail == 0)
            {
                Console.WriteLine($"  BUILD VERIFIED. Compile the fragment script, then startquest {r.id}:");
                Console.WriteLine($"    python papyrus/compile.py C:/modding/DU_Overtime/Data {bm.Script}");
            }
            else Console.WriteLine($"  {fail} CHECK(S) FAILED.");
            return fail == 0 ? 0 : 1;
        }
    }
}
