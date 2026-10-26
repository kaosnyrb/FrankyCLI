using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde.Nouns.QuestAuthoring;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FrankyCLI
{
    /// <summary>
    /// THE SPACE KIND: a Delve whose one place is an ORBIT. His 2026-10-22, after a morning of 19a not starting: "we faffed
    /// about trying to get a local, remote and space all working at the same time. Instead lets take a backup then make a
    /// series of tests for remote and space alias sets. Idea being we learn in isolation how to build these sets", and "A"
    /// (teach gen_delve an orbit-only Delve, rather than strip a clone of his MB01).
    ///
    /// ⭐ WHY ITS OWN BUILD AND NOT THE BEATS MACHINERY. The beats build is shaped round a POI main place: its marker slots
    /// fill INSIDE the place, the place's conditions are derived from the markers, the map marker fills inside it, the
    /// leashes measure from it. An orbit has none of that: its markers are the space cell's own, filled FROM the cell's
    /// collection. So this build starts from the template's shell WHOLE (a shipped remote-space quest, the set every one of
    /// his 62 `duo_artifact_remote_qst*` carries) and changes only what a test needs: the event, the driver, the object, the
    /// hook, the objective and the fragment. The first rung of the ladder is therefore the shipped set exactly, and every
    /// later rung takes pieces away with `place.drop`. It shares the plumbing (shell, self-links, prose, ordering, the
    /// model, the write, the alias-graph checks) and none of the beats.
    ///
    /// ⚠ WHAT IT DOES NOT CLAIM. That an orbit-only Delve is a product: it is the ladder's harness first. Whether it
    /// graduates into a real template or comes out is the ladder's exit condition, in
    /// office/spikes/2026-10-22-alias-set-ladders.md (home-office). One beat, `use` only, no speech, until then.
    /// </summary>
    public static partial class gen_delve
    {
        /// <summary>
        /// The space recipe's own shape, refused rather than half-built. Called from Grade, which returns before its POI
        /// census on a space template, so nothing below the markers section grades a space recipe.
        /// </summary>
        private static void GradeSpace(Recipe r, Template t, Action<string> Fatal)
        {
            var p = r.place;
            if (p.remote != null || p.habitability != null)
                Fatal("place.remote or place.habitability is set; a space Delve's one place is the orbit its shell carries, never a request.");
            if (p.system != null && p.system != "same")
                Fatal($"place.system is \"{p.system}\"; the one value built is \"same\" (the orbit in the player's star system, vanilla's way).");
            if (p.system != null && p.parsecs != null)
                Fatal("place.system and place.parsecs are both set; one rung, one way of saying near (S6 was the number, S7 vanilla's test).");
            if (p.over != null && (p.system != null || p.parsecs != null))
                Fatal("place.over is set beside place.system or place.parsecs; over CLEARS the orbit's conditions, so the others would test nothing.");
            if (p.playerShip != null && p.playerShip != "flying")
                Fatal($"place.playerShip is \"{p.playerShip}\"; the one value built is \"flying\" (SQ_PlayerShip's PlayerShip with him aboard, SE_Player_GenericTemplate's).");
            if (p.player == false)
                Fatal("place.player is false; leave it out to add no Player alias, true to add one.");
            if (string.Equals(p.over, "Player", StringComparison.OrdinalIgnoreCase) && p.player != true)
                Fatal("place.over is \"Player\" and place.player is not set; the shell carries no Player alias until place.player adds one.");
            if (p.cellAt != null && string.IsNullOrWhiteSpace(p.cellAt))
                Fatal("place.cellAt is empty; it names the alias the space cell is created at (vanilla's encounters: PlayerShip).");
            if (p.altitude is double alt && !(alt >= OrbitAltitudeBand.min && alt <= OrbitAltitudeBand.max))
                Fatal($"place.altitude is {alt}; his settled band is {OrbitAltitudeBand.min} to {OrbitAltitudeBand.max} (past ~32 the engine discards it).");
            if (p.second != null || p.third != null)
                Fatal("place.second or place.third is set; a space Delve has one place, the orbit.");
            if (p.orbit != null)
                Fatal("place.orbit is set; on a space template the main place IS the orbit, so there is nothing to choose.");
            if (p.who != null) Fatal("place.who is set; nobody stands in orbit.");
            if (p.reach != null || p.leash != null)
                Fatal("place.reach or place.leash is set; an orbit is drawn by its own conditions, not by a drive.");
            if (p.theme.require.Count > 0 || p.theme.exclude.Count > 0)
                Fatal("place.theme is set; an orbit is not a drawn POI, so a theme would test nothing and is never written.");
            if (p.parsecs is double ps && ps <= 0) Fatal($"place.parsecs is {ps}; it is the orbit's distance limit from the player, above 0.");
            if (p.drop != null)
                foreach (var d in p.drop.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                    Fatal($"place.drop names {d.Key} twice.");
            if (r.speaker != null || r.broadcast != null || r.recapSay != null)
                Fatal("speech is set; the space kind builds none yet (its harness is one silent beat).");
            if (r.approach != null || r.offer != null || r.recap != null)
                Fatal("approach, offer or recap is set; the space kind builds one beat and none of those.");
            for (int i = 0; i < r.beats.Count; i++)
            {
                var b = r.beats[i];
                if (t.allows.Count > 0 && !t.allows.Contains(b.type ?? ""))
                    Fatal($"beat {i + 1} is a '{b.type}' and template '{t.id}' builds only {string.Join(", ", t.allows)}. What it cannot do: {string.Join("; ", t.cannot)}");
                if (b.PlaceIndex != 0) Fatal($"beat {i + 1} is at the {PlaceKey[Math.Max(b.PlaceIndex, 0)]}; a space Delve has one place, the orbit.");
                if (!b.at.StartsWith("se_", StringComparison.OrdinalIgnoreCase))
                    Fatal($"beat {i + 1} is at '{b.at}'; an orbit's markers are its space cell's own (se_*), filled from the cell.");
                if (string.IsNullOrWhiteSpace(b.objective)) Fatal($"beat {i + 1} has no objective; it is the one line that points him at the object.");
                if (string.IsNullOrWhiteSpace(b.model) || string.IsNullOrWhiteSpace(b.name))
                    Fatal($"beat {i + 1} needs a model and a name: its object is a clone of {t.activatorFrom}, reskinned.");
                if (b.say != null || b.message != null || b.group != null || b.returnTo != null)
                    Fatal($"beat {i + 1} carries a say, message, group or returnTo; the space kind builds none of those.");
                // A board pays through the board's reward globals (BuildSpace); a startquest test has nobody to pay it, so a
                // tier there would be written nowhere.
                if (b.reward != null && !IsBoardSpace(t))
                    Fatal($"beat {i + 1} carries a reward; template '{t.id}' is started by hand and pays nothing, so it would be silently ignored.");
            }
        }

        /// <summary>
        /// The space build, between the self-links and the prose (Build calls it in place of the beats' places and beats).
        /// In order: the start event, the driver, the drops, the orbit's parsecs, the object, its marker, the objective and
        /// journal, the hook, stage 0, the fragment. Every refusal writes nothing.
        /// </summary>
        private static int BuildSpace(StarfieldMod myMod, Quest clone, Template t, Recipe r,
                                      IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, Dictionary<string, FormKey> markers, BeatsMade bm)
        {
            var beat = r.beats[0];
            var slot = t.beatSlots.FirstOrDefault();
            if (slot == null) { Console.WriteLine($"REFUSED: template '{t.id}' names no beat slot (the marker and the object it creates there)."); return 1; }
            var vma = clone.VirtualMachineAdapter;
            if (vma == null) { Console.WriteLine("REFUSED: the shell has no VMAD."); return 1; }

            // No start event to clear: a shell keeps none (gen_questmodel.ShellOf), so this quest is started by hand like
            // every Delve. His startquest on the base itself did not start it (2026-10-22, "I think it's expecting an
            // event"); the verify asserts the absence rather than this build assuming it.

            if (IsBoardSpace(t))
            {
                // THE BOARD'S SCRIPTS STAY. The board talks to the quest script (vanilla MissionBountySpaceScript: the accept
                // enables the space map marker, MissionComplete pays), and his MB01a's location script sets the arrival stage
                // when he reaches the system. Both are his shipped mission's own, played; nothing replaces them.
                if (!vma.Scripts.Any(s => string.Equals(s.Name, t.questScript, StringComparison.OrdinalIgnoreCase)))
                { Console.WriteLine($"REFUSED: the shell carries no '{t.questScript}', the script the board talks to."); return 1; }
                Console.WriteLine($"  scripts  : kept, the board's: {string.Join(", ", vma.Scripts.Select(s => s.Name))}");
            }
            else
            {
                // THE DRIVER. The shell's quest script runs the shipped mission (its ship, its gang); a test runs none.
                var driver = vma.Scripts.FirstOrDefault(s => string.Equals(s.Name, t.replacesDriver, StringComparison.OrdinalIgnoreCase));
                if (driver == null) { Console.WriteLine($"REFUSED: the shell carries no '{t.replacesDriver}' to remove."); return 1; }
                vma.Scripts.Remove(driver);
                Console.WriteLine($"  -driver  : {driver.Name} removed ({vma.Scripts.Count} quest script(s) left)");
            }
            // The shell's own fragments go too: this build writes its own, below, and their properties must not count as users.
            vma.Script = new ScriptEntry();
            vma.Fragments.Clear();

            var refs = clone.Aliases!.OfType<QuestReferenceAlias>().ToList();
            var marker = refs.FirstOrDefault(a => a.ID == (uint)slot.markerAlias);
            var target = refs.FirstOrDefault(a => a.ID == (uint)slot.activatorAlias);
            if (marker?.ReferenceCollectionAliasID is not int cellId || target?.CreateReferenceToObject == null)
            { Console.WriteLine($"REFUSED: alias {slot.markerAlias} does not fill from a collection, or alias {slot.activatorAlias} creates nothing."); return 1; }
            var keep = new HashSet<uint> { (uint)t.placeAlias, (uint)cellId, marker.ID, target.ID };

            if (DropAliases(clone, vma, r.place.drop ?? new List<string>(), keep, target) != 0) return 1;

            // THE ORBIT'S PARSECS (rung 6): the orbit's GetDistanceGalacticParsec limit becomes a number, as a request's does.
            var orbit = clone.Aliases!.OfType<QuestLocationAlias>().FirstOrDefault(a => a.ID == (uint)t.placeAlias);
            if (orbit == null) { Console.WriteLine($"REFUSED: the shell has no location alias {t.placeAlias}, the orbit."); return 1; }
            if (r.place.parsecs is double ps)
            {
                int pi = orbit.Conditions.FindIndex(c => c.Data is IGetDistanceGalacticParsecConditionDataGetter);
                if (pi < 0) { Console.WriteLine($"REFUSED: the orbit {orbit.Name} carries no GetDistanceGalacticParsec condition for place.parsecs to set."); return 1; }
                var old = orbit.Conditions[pi];
                orbit.Conditions[pi] = new ConditionFloat { CompareOperator = old.CompareOperator, Flags = old.Flags, Data = old.Data, ComparisonValue = (float)ps };
                Console.WriteLine($"  orbit    : {orbit.Name} GetDistanceGalacticParsec {old.CompareOperator} {ps} (was {(old is ConditionGlobal ? "a Global" : "a number")})");
            }
            // IN THE PLAYER'S SYSTEM, the way the ONE shipped same-system orbit does it (MB_BountySpace06Local): its orbit carries
            // exactly three conditions, a major orbital body, not a settled planet, and IsInSameCurrentLocationAsRef(player,
            // LocTypeStarSystem) == 1. The shell's far-mission filters (a parsec limit, MB_IsPlayerLevelAppropriate,
            // LocTypeSENotAllowed == 0) are its OTHER shape, and go. ⛔ Rung S7 (2026-10-22 13:14) ADDED the test and kept those
            // filters, under a comment claiming 238 vanilla orbits say "same system"; 236 of the 238 say == 0, NOT the player's
            // system (the Far missions). S7 did not start. This is the shape the one precedent actually has.
            if (r.place.system == "same")
            {
                var body = orbit.Conditions.Where(c => c.Data is IBodyHasKeywordConditionDataGetter).ToList();
                string Body(IConditionGetter c) => $"{((IBodyHasKeywordConditionDataGetter)c.Data).FirstParameter.Link.FormKey} == {(c as IConditionFloatGetter)?.ComparisonValue}";
                var want = new[] { $"{MajorOrbital} == 1", $"{SettledPlanet} == 0" };
                if (!body.Select(Body).OrderBy(x => x).SequenceEqual(want.OrderBy(x => x)))
                { Console.WriteLine($"REFUSED: the orbit's planet tests are [{string.Join("; ", body.Select(Body))}], not MB_BountySpace06Local's [{string.Join("; ", want)}]."); return 1; }
                var gone = orbit.Conditions.Where(c => !body.Contains(c)).Select(c => c.Data.GetType().Name.Replace("ConditionData", "")).ToList();
                orbit.Conditions.Clear();
                orbit.Conditions.AddRange(body);
                orbit.Conditions.Add(SameSystemCondition());
                Console.WriteLine($"  orbit    : {orbit.Name} in the player's star system, MB_BountySpace06Local's three conditions (dropped: {string.Join(", ", gone)})");
            }
            // THE PLAYER AS A GROUND REF (rung S12). RQ_Settlement_KillShips_01 parents its orbit to Trigger, a ref standing in
            // the settlement, and only that ref comes from its event; the player standing on a planet is the ref a startquest
            // quest can have. The alias is COPIED from SE_Player_GenericTemplate (unique actor Player, as in all 82), new id.
            if (r.place.player == true && AddVanillaPlayer(myMod, env, clone) < 0) return 1;
            // OVER A THING, vanilla's way (rung S10): no conditions, a parent link to the alias, and the flag every one of the
            // 125 vanilla orbits carrying a parent link has (0x40000000, unnamed in Mutagen; named here by its users).
            if (r.place.over is string overName)
            {
                var anchor = AliasNames(clone.Aliases!).Where(x => !x.member && string.Equals(x.name, overName, StringComparison.OrdinalIgnoreCase)).ToList();
                if (anchor.Count != 1) { Console.WriteLine($"REFUSED: place.over names '{overName}', and {anchor.Count} alias(es) of the shell carry that name."); return 1; }
                var gone = orbit.Conditions.Select(c => c.Data.GetType().Name.Replace("ConditionData", "")).ToList();
                orbit.Conditions.Clear();
                orbit.ParentSystemLocationAliasID = (int)anchor[0].id;
                orbit.Flags = (orbit.Flags ?? 0) | ParentLinkFlag;
                Console.WriteLine($"  orbit    : {orbit.Name} over alias {anchor[0].id} {anchor[0].name} (ParentSystemLocationAliasID, flags {orbit.Flags}); conditions cleared: {string.Join(", ", gone)}");
            }
            if (r.place.altitude is double alt)
            {
                Console.WriteLine($"  orbit    : {orbit.Name} altitude {alt} (was {AltitudeOf(orbit):0.###})");
                SetAltitude(orbit, alt);
            }
            // THE SHIP HE IS FLYING, vanilla's way (rung S11). The shell's PlayerShip is SQ_PlayerShip's HomeShip (alias 16),
            // Optional and unconditioned; vanilla's player space encounters use SQ_PlayerShip's PlayerShip (alias 0) and fill
            // only while he is aboard (ShipContainsRef(player) == 1). COPIED from SE_Player_GenericTemplate, never composed:
            // its external fill, its flags and its conditions, onto the shell's alias id and name.
            if (r.place.playerShip == "flying")
            {
                var ship = clone.Aliases!.OfType<QuestReferenceAlias>().Where(a => string.Equals(a.Name, "PlayerShip", StringComparison.OrdinalIgnoreCase)).ToList();
                if (ship.Count != 1 || ship[0].External == null) { Console.WriteLine($"REFUSED: place.playerShip needs one external PlayerShip alias on the shell, and found {ship.Count}."); return 1; }
                var vq = OwnOrMaster<IQuestGetter>(myMod, env, VanillaEncounter);
                var vs = vq?.Aliases.OfType<IQuestReferenceAliasGetter>().Where(a => a.Name == "PlayerShip").ToList();
                if (vs == null || vs.Count != 1 || vs[0].External == null)
                { Console.WriteLine($"REFUSED: {VanillaEncounter} is not in this plugin's masters, or carries no single external PlayerShip to copy."); return 1; }
                var shipWas = $"{ship[0].External!.Quest.FormKey} alias {ship[0].External!.AliasID}, flags {ship[0].Flags}, {ship[0].Conditions.Count} condition(s)";
                ship[0].External = vs[0].External!.DeepCopy();
                ship[0].Flags = vs[0].Flags;
                ship[0].Conditions.Clear();
                ship[0].Conditions.AddRange(vs[0].Conditions.Select(c => c.DeepCopy()));
                Console.WriteLine($"  ship     : alias {ship[0].ID} PlayerShip = {ship[0].External.Quest.FormKey} alias {ship[0].External.AliasID}, flags {ship[0].Flags}, " +
                                  $"{string.Join("; ", ship[0].Conditions.Select(CondLine))} (copied from {VanillaEncounter}; was {shipWas})");
            }
            // THE CELL AT A THING (rung S11): the shell creates its space cell AT the orbit; vanilla's player encounters create
            // theirs at PlayerShip (80 of 82). The create-at alias of the collection the beat's marker fills from changes; the
            // order pass then puts the anchor above it, as a fill dependency.
            if (r.place.cellAt is string cellAtName)
            {
                var anchor = AliasNames(clone.Aliases!).Where(x => !x.member && string.Equals(x.name, cellAtName, StringComparison.OrdinalIgnoreCase)).ToList();
                if (anchor.Count != 1) { Console.WriteLine($"REFUSED: place.cellAt names '{cellAtName}', and {anchor.Count} alias(es) of the shell carry that name."); return 1; }
                var cell = clone.Aliases!.OfType<QuestCollectionAlias>().SelectMany(c => c.Collection).Select(m => m.ReferenceAlias)
                    .FirstOrDefault(m => m?.ID == (uint)cellId);
                if (cell?.CreateReferenceToObject == null) { Console.WriteLine($"REFUSED: collection member {cellId} creates nothing to move."); return 1; }
                int wasAt = cell.CreateReferenceToObject.AliasID;
                cell.CreateReferenceToObject.AliasID = (short)anchor[0].id;
                bm.CellAt = ((int)cell.ID, (int)anchor[0].id);
                Console.WriteLine($"  cell     : {cell.Name} (alias {cell.ID}) creates {cell.CreateReferenceToObject.Object.FormKey} at alias {anchor[0].id} {anchor[0].name} (was at {wasAt})");
            }

            // THE OBJECT. The shell's target is a ship; it becomes a clone of activatorFrom, reskinned to the beat's model.
            var src = myMod.Activators.FirstOrDefault(a => a.EditorID == t.activatorFrom);
            if (src == null) { Console.WriteLine($"REFUSED: activatorFrom '{t.activatorFrom}' is not an activator in {t.mod}."); return 1; }
            if (src.VirtualMachineAdapter?.Scripts.Count > 0)
            { Console.WriteLine($"REFUSED: activatorFrom '{t.activatorFrom}' carries a script, and a clone would run it."); return 1; }
            target.CreateReferenceToObject!.Object.SetTo(src.FormKey);
            if (ReskinActivator(myMod, clone, t, r.id + "_b1", (int)target.ID, beat.model, beat.name) != 0) return 1;

            // ITS MARKER: the cell's own, by the beat's ref type (the shell's is kept when the recipe names the same one).
            var rt = marker.Conditions.Select(c => c.Data).OfType<HasRefTypeConditionData>().FirstOrDefault();
            if (rt == null) { Console.WriteLine($"REFUSED: marker alias {marker.ID} carries no HasRefType condition to fill from the cell by."); return 1; }
            var was = rt.FirstParameter.Link.FormKey;
            rt.FirstParameter.Link.SetTo(markers[beat.at]);
            bm.OrbitMarkers.Add(((int)marker.ID, markers[beat.at]));   // for the verify: what this build wrote
            Console.WriteLine($"  beat     : alias {marker.ID} {marker.Name} -> {beat.at} from cell {cellId}" + (was == markers[beat.at] ? " (the shell's own)" : $" (was {was})"));

            var ob = clone.Objectives?.FirstOrDefault(o => o.Index == slot.objective);
            if (ob == null) { Console.WriteLine($"REFUSED: the shell has no objective {slot.objective}."); return 1; }
            if (!ob.Targets.Any(x => x.AliasID == (int)target.ID))
            { Console.WriteLine($"REFUSED: objective {slot.objective} does not target alias {target.ID}, the object."); return 1; }
            ob.DisplayText = Expand(beat.objective!, t);
            if (beat.journal != null)
            {
                var e = clone.Stages?.FirstOrDefault(s => s.Index == slot.journalStage)?.LogEntries?.FirstOrDefault();
                if (e == null) { Console.WriteLine($"REFUSED: stage {slot.journalStage} has no log entry to carry the journal."); return 1; }
                e.Entry = Expand(beat.journal, t);
            }

            // THE HOOK: activating the object completes it (his MB01's is used from the ship; this is the stock one). On a
            // board it waits for the accept, since the quest runs from the board's refresh and its object is already placed.
            int prereq = IsBoardSpace(t) ? t.entryStage : 0;
            var hook = StageHook("DefaultAliasOnActivate", t.completeStage, prereq,
                                 new ScriptBoolProperty { Name = "ShouldHideActivationAfterSuccessfulActivation", Data = true, Flags = ScriptProperty.Flag.Edited });
            if (AttachHook(vma, clone.FormKey, (int)target.ID, hook, HookEntry.FindOrCreate, true, bm, t.completeStage, prereq) == Hooked.AlreadyCarries)
            { Console.WriteLine($"REFUSED: alias {target.ID} already carries DefaultAliasOnActivate."); return 1; }
            Console.WriteLine($"  +hook    : alias {target.ID} DefaultAliasOnActivate sets {t.completeStage} after {prereq}");

            string name = FragmentScriptName(r);
            var code = new SortedDictionary<int, List<string>>();
            if (IsBoardSpace(t))
            {
                // His MB01a's object is a ship-shaped target that completes by DYING (DefaultAliasOnDeath sets 100). Ours is
                // used, so the death hook goes with the ship it was written for; printed, never silent.
                var entry = vma.Aliases.FirstOrDefault(a => a.Property.Alias == (int)target.ID);
                int deaths = entry?.Scripts.RemoveAll(s => s.Name == "DefaultAliasOnDeath") ?? 0;
                Console.WriteLine($"  -hook    : alias {target.ID} DefaultAliasOnDeath removed ({deaths}); the object is used, not killed");
                // THE FRAGMENT IS HIS MB01a's (vanilla QF_MB_BountySpace01Far_001EF957), with the beat's objective as the
                // second: the system objective on the accept, the beat's on arrival in the system (the location script sets
                // ArriveInSystemStage), and the board's pay on the hook's stage. MissionComplete() sets CompleteStage itself
                // only when it is not already done, so calling it from that stage is vanilla's own shape.
                // Unset on his MB01a, so the script's own declaration holds (MissionBountySpaceScript.psc: `= 20`). That is the
                // engine's value, not a guess: an unset Const property reads its declared default.
                const int ArriveInSystemDeclared = 20;
                var arrive = vma.Scripts.First(s => string.Equals(s.Name, t.questScript, StringComparison.OrdinalIgnoreCase))
                    .Properties.OfType<ScriptIntProperty>().FirstOrDefault(p => p.Name == "ArriveInSystemStage")?.Data ?? ArriveInSystemDeclared;
                if (clone.Stages?.Any(s => s.Index == arrive) != true) { Console.WriteLine($"REFUSED: the shell has no arrival stage {arrive}."); return 1; }
                const int systemObjective = 10;
                if (clone.Objectives?.Any(o => o.Index == systemObjective) != true) { Console.WriteLine($"REFUSED: the shell has no system objective {systemObjective}."); return 1; }
                code[t.entryStage] = new() { $"SetObjectiveDisplayed({systemObjective})" };
                code[arrive] = new() { $"SetObjectiveCompleted({systemObjective})", $"SetObjectiveDisplayed({slot.objective})" };
                code[t.completeStage] = new()
                {
                    $"SetObjectiveCompleted({slot.objective})",
                    "Quest __temp = Self as Quest",
                    $"{t.questScript} kmyQuest = __temp as {t.questScript}",
                    "kmyQuest.MissionComplete()",
                };
                bm.ArriveStage = arrive;
                // THE PAY goes onto the board's reward globals, which the card also shows (the ground board's rule: never onto
                // the stage, or it pays twice). Absent, his MB01a's own stand (duo_reward_creds_easy).
                if (beat.reward is string tier)
                {
                    var cg = myMod.Globals.FirstOrDefault(g => g.EditorID == $"duo_reward_creds_{tier}");
                    if (cg == null) { Console.WriteLine($"REFUSED: {t.mod} has no duo_reward_creds_{tier}."); return 1; }
                    var qs = vma.Scripts.First(s => string.Equals(s.Name, t.questScript, StringComparison.OrdinalIgnoreCase));
                    foreach (var pn in new[] { "RewardAmountGlobal", "RewardAmountGlobalActual" })
                    {
                        var rp = qs.Properties.OfType<ScriptObjectProperty>().FirstOrDefault(p => p.Name == pn);
                        if (rp == null) { Console.WriteLine($"REFUSED: {t.questScript} carries no {pn} to pay through."); return 1; }
                        rp.Object.SetTo(cg.FormKey);
                    }
                    bm.PaidGlobal = cg.FormKey;
                    Console.WriteLine($"  reward   : the board pays {cg.EditorID} (RewardAmountGlobal and its Actual)");
                }
            }
            else
            {
                var s0 = clone.Stages?.FirstOrDefault(s => s.Index == 0);
                if (s0 == null) { Console.WriteLine("REFUSED: the shell has no stage 0 to run on start."); return 1; }
                s0.Flags |= QuestStage.Flag.RunOnStart;
                // THE FRAGMENT: the objective shows on start, and the hook's stage completes and stops the quest.
                code[0] = new() { $"SetObjectiveDisplayed({slot.objective})" };
                code[t.completeStage] = new() { $"SetObjectiveCompleted({slot.objective})", "CompleteQuest()", "Stop()" };
            }
            var psc = new StringBuilder();
            psc.AppendLine($"Scriptname {name} Extends Quest Hidden Const");
            psc.AppendLine($"{{GENERATED by FrankyCLI gen_delve from data/delves/recipes/{r.id}.json (template {t.id}). Do not edit: change");
            psc.AppendLine(IsBoardSpace(t)
                ? "the recipe and rebuild. A board space Delve: the system objective on the accept, the beat's on arrival, and the object's use pays.}"
                : "the recipe and rebuild. A space Delve's two stages: the objective on start, and the object's use completes it.}");
            psc.AppendLine();
            foreach (var kv in code)
            {
                psc.AppendLine($"Function {FragmentFunction(kv.Key)}()");
                foreach (var line in kv.Value) psc.AppendLine("    " + line);
                psc.AppendLine("EndFunction");
                psc.AppendLine();
            }
            vma.Script = new ScriptEntry { Name = name };
            foreach (var st in code.Keys)
                vma.Fragments.Add(new QuestScriptFragment { Stage = (ushort)st, StageIndex = 0, ScriptName = name, FragmentName = FragmentFunction(st) });
            bm.Script = name;
            bm.FragStages = code.Keys.ToList();
            string dir = Path.Combine(PapyrusDir(), "gen");
            Directory.CreateDirectory(dir);
            bm.PscPath = Path.Combine(dir, name + ".psc");
            File.WriteAllText(bm.PscPath, psc.ToString().Replace("\r\n", "\n"));
            Console.WriteLine($"  fragments: {name} on stages [{string.Join(", ", code.Keys)}]; source {bm.PscPath}");

            Console.WriteLine("  set      : " + SetOf(clone.Aliases!));
            return 0;
        }

        /// <summary>
        /// THE LADDER'S KNOB: remove the named base aliases, then refuse if anything left still names one. A user is any
        /// alias filling from, created at, measured from, tested against, parented to, closest to or linked to a removed id;
        /// an objective target; a script property or alias script entry; or an &lt;Alias=Name&gt; token in the quest's text.
        /// The four aliases the beat itself stands on (the orbit, its cell, the marker, the object) cannot be dropped.
        /// ⭐ ONE USER IS REMOVED RATHER THAN REFUSED, and it is named: the object's own link to a dropped alias. The shipped
        /// object is a ship and links to PatrolMarker01 (found by this check's first run, 2026-10-22: S4 refused on it), and
        /// this build replaces the ship, so the ship's link goes with the piece it pointed at. Printed, never silent.
        /// ⚠ Wider than SlotUsers on purpose: a slot pair is only ever named by fills and objectives, while a dropped piece
        /// of a space set can be named by a condition or a system alias. Two readers of "who names this", for two shapes.
        /// </summary>
        private static int DropAliases(Quest clone, QuestAdapter vma, List<string> drop, HashSet<uint> keep, QuestReferenceAlias obj)
        {
            if (drop.Count == 0) return 0;
            var names = AliasNames(clone.Aliases!).ToDictionary(x => x.id, x => x.name);
            var gone = new HashSet<uint>();
            foreach (var n in drop)
            {
                var hit = names.Where(kv => string.Equals(kv.Value, n, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).ToList();
                if (hit.Count != 1) { Console.WriteLine($"REFUSED: place.drop names '{n}', and {hit.Count} alias(es) of the shell carry that name."); return 1; }
                if (keep.Contains(hit[0])) { Console.WriteLine($"REFUSED: place.drop names {n} (alias {hit[0]}); the beat stands on it."); return 1; }
                gone.Add(hit[0]);
            }
            foreach (var a in clone.Aliases!.Where(a => AliasGraph.IdsOf(a).Any(gone.Contains)).ToList())
            {
                if (AliasGraph.IdsOf(a).Any(id => !gone.Contains(id))) { Console.WriteLine("REFUSED: a collection holds a dropped alias beside a kept one."); return 1; }
                clone.Aliases!.Remove(a);
            }
            foreach (var e in vma.Aliases.Where(e => gone.Contains((uint)e.Property.Alias)).ToList()) vma.Aliases.Remove(e);

            var users = new List<string>();
            bool G(int? id) => id is int v && v >= 0 && gone.Contains((uint)v);
            if (obj.LinkedAliases != null)
                foreach (var l in obj.LinkedAliases.Where(l => G(l.AliasID)).ToList())
                {
                    obj.LinkedAliases.Remove(l);
                    Console.WriteLine($"  -link    : {obj.Name} (alias {obj.ID}) no longer links to {l.AliasID}: the shipped ship's link, and this build replaced the ship");
                }
            void Ref(IQuestReferenceAliasGetter a)
            {
                foreach (var (dep, how) in AliasGraph.DepsOf(a)) if (gone.Contains(dep)) users.Add($"alias {a.ID} {a.Name} ({how} {dep})");
                if (a.LinkedAliases != null)
                    foreach (var l in a.LinkedAliases) if (G(l.AliasID)) users.Add($"alias {a.ID} {a.Name} (links to {l.AliasID})");
            }
            foreach (var a in clone.Aliases!)
            {
                if (a is QuestReferenceAlias ra) Ref(ra);
                else if (a is QuestCollectionAlias ca) foreach (var m in ca.Collection.Where(m => m.ReferenceAlias != null)) Ref(m.ReferenceAlias!);
                else if (a is QuestLocationAlias la)
                {
                    foreach (var dep in AliasGraph.LeashDepsOf(la).Concat(AliasGraph.SameLocationDepsOf(la)))
                        if (gone.Contains(dep)) users.Add($"location alias {la.ID} {la.Name} (a condition on {dep})");
                    if (G(la.SystemLocationAliasID)) users.Add($"location alias {la.ID} {la.Name} (system alias {la.SystemLocationAliasID})");
                    if (G(la.ParentSystemLocationAliasID)) users.Add($"location alias {la.ID} {la.Name} (parent {la.ParentSystemLocationAliasID})");
                }
            }
            foreach (var o in clone.Objectives!)
                if (o.Targets.Any(x => G(x.AliasID))) users.Add($"objective {o.Index}");
            foreach (var p in vma.Scripts.SelectMany(s => s.Properties).Concat(vma.Aliases.SelectMany(e => e.Scripts).SelectMany(s => s.Properties)).OfType<ScriptObjectProperty>())
                if (p.Object.FormKey == clone.FormKey && G(p.Alias)) users.Add($"script property {p.Name}");
            var text = new[] { clone.Name?.String }.Concat(clone.Objectives!.Select(o => o.DisplayText?.String))
                .Concat(clone.Stages!.SelectMany(s => s.LogEntries).Select(e => e.Entry?.String)).Where(s => s != null).ToList();
            foreach (var id in gone)
                if (text.Any(s => s!.Contains($"<Alias={names[id]}>", StringComparison.OrdinalIgnoreCase))) users.Add($"the quest's text (<Alias={names[id]}>)");
            if (users.Count > 0) { Console.WriteLine($"REFUSED: dropped alias(es) are still named by {string.Join(", ", users.Distinct())}."); return 1; }
            foreach (var id in gone.OrderBy(x => x)) Console.WriteLine($"  -drop    : {names[id]} (alias {id})");
            return 0;
        }

        /// <summary>The flag bit every vanilla location alias with a parent link carries (125 of 125 orbit aliases, 2026-10-22).</summary>
        private const QuestLocationAlias.Flag ParentLinkFlag = (QuestLocationAlias.Flag)0x40000000;

        /// <summary>
        /// Vanilla's own template for a space encounter round the player's ship, the copy source for place.playerShip. Its
        /// shape is the family's: 82 SE_Player_* quests, orbit parented to PlayerShip in 81, space cell created at PlayerShip
        /// in 80, PlayerShip = SQ_PlayerShip alias 0 with ShipContainsRef(player) == 1 (gen_inspect qall, Defiant, 2026-10-22).
        /// </summary>
        private const string VanillaEncounter = "SE_Player_GenericTemplate";

        /// <summary>
        /// Add a ref alias Player (unique actor Player) COPIED from SE_Player_GenericTemplate under a new id, for an orbit to
        /// hang off. Shared by the space kind (place.player, rung S13 proved it) and the beats kind (place.third.orbit
        /// "player"). Returns the new id, or -1 having said why.
        /// ⛔ A quest that ALREADY has a unique-actor Player alias gets that one back, never a second: whether one quest may hold
        /// the player in two aliases is unread, and our contention check covers in-place fills only, so a second would be a
        /// stick of dynamite of our own making (19a's base carries MBRIR02Player, alias 32, found by its first dry run).
        /// </summary>
        private static int AddVanillaPlayer(StarfieldMod myMod, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, Quest clone)
        {
            var have = clone.Aliases!.OfType<QuestReferenceAlias>().Where(a => a.UniqueActor.FormKey == PlayerNpc).ToList();
            if (have.Count > 0)
            {
                Console.WriteLine($"  =player  : alias {have[0].ID} {have[0].Name}, the unique actor Player the quest already carries (flags {have[0].Flags}); no second added");
                return (int)have[0].ID;
            }
            if (AliasNames(clone.Aliases!).Any(x => string.Equals(x.name, "Player", StringComparison.OrdinalIgnoreCase)))
            { Console.WriteLine("REFUSED: the quest already carries an alias named Player."); return -1; }
            var vq = OwnOrMaster<IQuestGetter>(myMod, env, VanillaEncounter);
            var vp = vq?.Aliases.OfType<IQuestReferenceAliasGetter>().Where(a => a.Name == "Player").ToList();
            if (vp == null || vp.Count != 1 || vp[0].UniqueActor.IsNull)
            { Console.WriteLine($"REFUSED: {VanillaEncounter} is not in this plugin's masters, or carries no single unique-actor Player to copy."); return -1; }
            var pa = vp[0].DeepCopy();
            pa.ID = NextAliasId(clone);
            clone.Aliases!.Add(pa);
            Console.WriteLine($"  +player  : alias {pa.ID} Player, unique actor {pa.UniqueActor.FormKey}, flags {pa.Flags} (copied from {VanillaEncounter} alias {vp[0].ID})");
            return (int)pa.ID;
        }

        /// <summary>One condition as a line: function, first parameter (a link's FormKey), operator, value.</summary>
        private static string CondLine(IConditionGetter c)
        {
            string fn = c.Data.GetType().Name.Replace("ConditionDataBinaryOverlay", "").Replace("ConditionData", "");
            object? fp = c.Data.GetType().GetProperty("FirstParameter")?.GetValue(c.Data);
            object? link = fp?.GetType().GetProperty("Link")?.GetValue(fp) ?? fp;
            object? key = link?.GetType().GetProperty("FormKey")?.GetValue(link) ?? link;
            string val = c is IConditionFloatGetter f ? f.ComparisonValue.ToString() : "a Global";
            return $"{fn}({key}) {c.CompareOperator} {val}";
        }

        /// <summary>Every alias id a quest carries with its name; a collection's member is named, and flagged as a member.</summary>
        private static IEnumerable<(uint id, string name, bool member)> AliasNames(IEnumerable<IAQuestAliasGetter> aliases)
            => aliases.SelectMany(a => AliasGraph.IdsOf(a).Select(id => a switch
               {
                   IQuestReferenceAliasGetter x => (id, x.Name ?? "", false),
                   IQuestLocationAliasGetter x => (id, x.Name ?? "", false),
                   IQuestCollectionAliasGetter x => (id, x.Collection.First(m => m.ReferenceAlias?.ID == id).ReferenceAlias!.Name ?? "", true),
                   _ => (id, "?", false),
               }));

        /// <summary>A quest's alias set as one line, by id (a collection member in brackets): the rung's record of what it carried.</summary>
        private static string SetOf(IEnumerable<IAQuestAliasGetter> aliases)
            => string.Join(", ", AliasNames(aliases).OrderBy(x => x.id).Select(x => x.member ? $"{x.id} [{x.name}]" : $"{x.id} {x.name}"));

        /// <summary>
        /// Read the written plugin back OFF DISK and grade every claim the space build made. The alias graph is the beats
        /// verify's own (AliasGraphChecks); the fragment and hook lines repeat VerifyBeats' four, a second copy, named.
        /// </summary>
        private static int VerifySpace(string modFile, Mutagen.Bethesda.Plugins.Binary.Parameters.BinaryReadParameters readParams,
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
            var baseKey = m.Quests.FirstOrDefault(x => x.EditorID == t.@base)?.FormKey;
            fail += Check($"nothing in the quest points at its base {t.@base}",
                          baseKey == null ? "base not found" : LinksTo(q, baseKey.Value), "0");   // the model's reader: it sees script structs (LinksTo)
            fail += Check(IsBoardSpace(t) ? "no story-manager start event (the board's node deals it, as every board Delve)"
                                          : "no story-manager start event (started with startquest, as every Delve)", q.Event?.ToString() ?? "none", "none");
            fail += AliasGraphChecks(q);
            if (IsBoardSpace(t))
                // Pinned off his MB01a's census (gen_inspect qall duo_MB01a_qst, Defiant, 2026-10-26), never re-read from the shell.
                fail += Check("quest-level scripts (the board's, kept)", string.Join(",", vma.Scripts.Select(s => s.Name.ToLowerInvariant()).OrderBy(x => x)),
                              "defaultquestchangelocationscript,missionbountyspacescript");
            else
                fail += Check("quest-level scripts (the driver removed, none added)", string.Join(",", vma.Scripts.Select(s => s.Name)), "");

            var slot = t.beatSlots[0];
            var orbit = q.Aliases.OfType<IQuestLocationAliasGetter>().FirstOrDefault(a => a.ID == (uint)t.placeAlias);
            fail += Check($"the orbit (alias {t.placeAlias}) fills by type LocTypeOrbit", orbit?.LocationTypeKeyword.FormKey.ToString() ?? "missing", "016504:Starfield.esm");
            if (r.place.parsecs is double ps)
            {
                var pc = orbit?.Conditions.FirstOrDefault(c => c.Data is IGetDistanceGalacticParsecConditionDataGetter);
                fail += Check($"the orbit's GetDistanceGalacticParsec is the number {ps}",
                              pc is IConditionFloatGetter cf ? cf.ComparisonValue.ToString() : pc == null ? "missing" : "a Global", ((float)ps).ToString());
            }
            fail += Check("the orbit keeps to the player's star system (IsInSameCurrentLocationAsRef player LocTypeStarSystem == 1)",
                          HoldsToSystem(orbit?.Conditions).ToString(), (r.place.system == "same").ToString());
            if (r.place.system == "same")
                fail += Check("the orbit's conditions are MB_BountySpace06Local's three",
                              // Both spellings: a read-back type is ...ConditionData or ...ConditionDataBinaryOverlay (this check's first run
                              // stripped only the second and failed S9 with the right three conditions on disk).
                              string.Join(",", (orbit?.Conditions ?? new List<IConditionGetter>()).Select(c => c.Data.GetType().Name.Replace("ConditionDataBinaryOverlay", "").Replace("ConditionData", "")).OrderBy(x => x)),
                              "BodyHasKeyword,BodyHasKeyword,IsInSameCurrentLocationAsRef");
            if (r.place.altitude is double alt)
                fail += Check($"the orbit's altitude is {alt}", AltitudeOf(orbit).ToString("0.###"), alt.ToString("0.###"));
            if (r.place.over is string overName)
            {
                var anchorId = AliasNames(q.Aliases).Where(x => !x.member && string.Equals(x.name, overName, StringComparison.OrdinalIgnoreCase)).Select(x => (int?)x.id).FirstOrDefault();
                fail += Check($"the orbit is parented to {overName} (alias {anchorId}), with the parent flag and no conditions",
                              $"parent {orbit?.ParentSystemLocationAliasID}, flag {((orbit?.Flags ?? 0) & ParentLinkFlag) == ParentLinkFlag}, {orbit?.Conditions.Count} condition(s)",
                              $"parent {anchorId}, flag True, 0 condition(s)");
            }
            if (r.place.playerShip == "flying")
            {
                // Expected values are the census's (SE_Player_GenericTemplate read 2026-10-22), pinned here, never re-read from
                // the build's own copy source: a check whose expectation comes from where the actual came from cannot fail.
                var ship = q.Aliases.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.Name == "PlayerShip");
                fail += Check("PlayerShip is SQ_PlayerShip's PlayerShip (alias 0), the ship he is flying",
                              ship?.External == null ? "not external" : $"{ship.External.Quest.FormKey} alias {ship.External.AliasID}",
                              "0174A2:Starfield.esm alias 0");
                fail += Check("PlayerShip fills only while he is aboard", string.Join("; ", (ship?.Conditions ?? new List<IConditionGetter>()).Select(CondLine)),
                              "ShipContainsRef(000014:Starfield.esm) EqualTo 1");
                fail += Check("PlayerShip's flags are the vanilla encounter's", ship?.Flags.ToString() ?? "missing", "CreateRefTemp");
            }
            if (r.place.player == true)
            {
                var pl = q.Aliases.OfType<IQuestReferenceAliasGetter>().Where(a => a.Name == "Player").ToList();
                fail += Check("one Player alias, the unique actor Player (the census value, pinned)",
                              pl.Count != 1 ? $"{pl.Count} alias(es) named Player" : pl[0].UniqueActor.FormKey.ToString(), "000007:Starfield.esm");
            }
            if (r.place.cellAt is string cellAtName)
            {
                var anchorId = AliasNames(q.Aliases).Where(x => !x.member && string.Equals(x.name, cellAtName, StringComparison.OrdinalIgnoreCase)).Select(x => (int?)x.id).FirstOrDefault();
                var cell = q.Aliases.OfType<IQuestCollectionAliasGetter>().SelectMany(c => c.Collection).Select(x => x.ReferenceAlias)
                    .FirstOrDefault(x => x?.ID == (uint)(bm.CellAt?.cell ?? -1));
                fail += Check($"the space cell is created at {cellAtName} (alias {anchorId})",
                              cell?.CreateReferenceToObject == null ? "creates nothing" : $"at {cell.CreateReferenceToObject.AliasID}", $"at {anchorId}");
            }
            var target = q.Aliases.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.ID == (uint)slot.activatorAlias);
            var act = m.Activators.FirstOrDefault(a => a.EditorID == r.id + "_b1");
            fail += Check($"alias {slot.activatorAlias} creates {r.id}_b1 at marker alias {slot.markerAlias}",
                          target?.CreateReferenceToObject == null ? "creates nothing"
                              : $"{target.CreateReferenceToObject.Object.FormKey} at {target.CreateReferenceToObject.AliasID}",
                          $"{act?.FormKey.ToString() ?? "no activator"} at {slot.markerAlias}");
            foreach (var (ma, wrote) in bm.OrbitMarkers)
            {
                var marker = q.Aliases.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.ID == (uint)ma);
                var mrt = marker?.Conditions.Select(c => c.Data).OfType<IHasRefTypeConditionDataGetter>().FirstOrDefault();
                fail += Check($"marker alias {ma} fills from a cell collection by {r.beats[0].at}",
                              marker?.ReferenceCollectionAliasID == null ? "not from a collection" : mrt?.FirstParameter.Link.FormKey.ToString() ?? "no HasRefType",
                              wrote.ToString());
            }
            fail += Check($"objective {slot.objective}", q.Objectives.FirstOrDefault(o => o.Index == slot.objective)?.DisplayText?.String ?? "missing", Expand(r.beats[0].objective!, t));
            var left = AliasNames(q.Aliases).Select(x => x.name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var d in r.place.drop ?? new List<string>())
                fail += Check($"{d} is gone", left.Contains(d) ? "PRESENT" : "gone", "gone");

            fail += Check("fragment script", vma.Script?.Name ?? "none", bm.Script);
            fail += Check("fragment stages", string.Join(",", vma.Fragments.Select(f => f.Stage).OrderBy(x => x)), string.Join(",", bm.FragStages));
            foreach (var f in vma.Fragments)
                fail += Check($"stage {f.Stage} fragment", $"{f.ScriptName}.{f.FragmentName}", $"{bm.Script}.{FragmentFunction(f.Stage)}");
            if (IsBoardSpace(t))
            {
                fail += Check("nothing runs on start (the board starts it at its refresh, long before the accept)",
                              string.Join(",", q.Stages.Where(s => s.Flags.HasFlag(QuestStage.Flag.RunOnStart)).Select(s => s.Index)), "");
                fail += Check($"the object (alias {slot.activatorAlias}) no longer carries DefaultAliasOnDeath",
                              (vma.Aliases.FirstOrDefault(a => a.Property.Alias == slot.activatorAlias)?.Scripts.Any(s => s.Name == "DefaultAliasOnDeath") == true).ToString(), "False");
                string src = File.ReadAllText(bm.PscPath);
                string done = src.Split($"Function {FragmentFunction(t.completeStage)}()").ElementAtOrDefault(1)?.Split("EndFunction")[0] ?? "";
                fail += Check($"stage {t.completeStage} pays through {t.questScript}.MissionComplete()", done.Contains("kmyQuest.MissionComplete()").ToString(), "True");
                if (bm.PaidGlobal is FormKey paid)
                {
                    var qs = vma.Scripts.FirstOrDefault(s => string.Equals(s.Name, t.questScript, StringComparison.OrdinalIgnoreCase));
                    foreach (var pn in new[] { "RewardAmountGlobal", "RewardAmountGlobalActual" })
                        fail += Check($"{t.questScript}.{pn} is the beat's tier",
                                      qs?.Properties.OfType<IScriptObjectPropertyGetter>().FirstOrDefault(p => p.Name == pn)?.Object.FormKey.ToString() ?? "missing", paid.ToString());
                }
                fail += Check($"the beat's objective {slot.objective} shows on arrival stage {bm.ArriveStage}",
                              (src.Split($"Function {FragmentFunction(bm.ArriveStage)}()").ElementAtOrDefault(1)?.Split("EndFunction")[0].Contains($"SetObjectiveDisplayed({slot.objective})") == true).ToString(), "True");
            }
            else
                fail += Check("stage 0 runs on start", q.Stages.Any(s => s.Index == 0 && s.Flags.HasFlag(QuestStage.Flag.RunOnStart)).ToString(), "True");
            foreach (var h in bm.Hooks)
            {
                var sc = vma.Aliases.FirstOrDefault(a => a.Property.Alias == h.alias)?.Scripts.FirstOrDefault(s => s.Name == h.script);
                int? I(string n) => sc?.Properties.OfType<IScriptIntPropertyGetter>().FirstOrDefault(p => p.Name == n)?.Data;
                fail += Check($"alias {h.alias} {h.script}", $"sets {I("StageToSet")} after {I("PrereqStage")}", $"sets {h.set} after {h.prereq}");
            }
            var twice = vma.Aliases.SelectMany(a => a.Scripts.GroupBy(s => s.Name).Where(g => g.Count() > 1).Select(g => $"alias {a.Property.Alias} {g.Key}")).ToList();
            fail += Check("no alias carries the same script twice", twice.Count == 0 ? "none" : string.Join(", ", twice), "none");
            Console.WriteLine("    set, off disk: " + SetOf(q.Aliases));
            if (fail == 0)
            {
                Console.WriteLine();
                Console.WriteLine(IsBoardSpace(t)
                    ? $"  BUILD VERIFIED. Compile the fragment script, then take it from the board ({r.deal}); a board quest is never started by hand:"
                    : $"  BUILD VERIFIED. Compile the fragment script, then startquest {r.id}:");
                Console.WriteLine($"    python papyrus/compile.py C:/modding/DU_Overtime/Data {bm.Script}");
            }
            return fail;
        }
    }
}
