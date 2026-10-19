using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde.Utils;
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
    /// Each type is a kind with its own file in delve_beats/ (beat_kind.cs is the interface): use, pickup and deliver
    /// are one object beat (object.cs), and its note on why deliver is NOT DefaultAliasOnActivateRemoveItems lives there.
    ///
    /// A HOLD is waves of enemies at a marker, one after another, each when the last is down (Jessica's Type 6):
    /// N stages and N stock death hooks, no driver. Its whole design is in delve_beats/hold.cs.
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
        private static readonly string[] SpeciesModes = { "one", "mix" };

        /// <summary>
        /// The stock death hook's own switch: each member leaves the collection as it dies, so an objective
        /// targeting the collection marks only the living (his play, 2026-10-14: the markers showed the dead too).
        /// Completion still holds: the removal runs BEFORE DefaultCollectionAliasParent's all-dead check, which
        /// counts against the collection's CURRENT size, so the last death leaves it empty and 0 >= 0 sets the stage.
        /// </summary>
        private static ScriptBoolProperty RemoveDeadFromCollection() =>
            new() { Name = "RemoveFromRefCollection", Data = true, Flags = ScriptProperty.Flag.Edited };

        /// <summary>One step of a beats Delve: a single beat, or an any-order group of them.</summary>
        private sealed class Step
        {
            public List<int> Beats = new();          // indices into r.beats
            public string? Group;                    // null = a single beat
            public string? Choose;                   // the endings: one objective and one completing stage each
            public int Waves;                        // a hold: its wave count; 0 on every other step
            public List<int> WaveStages = new();     // a hold: the stage each wave's last death sets; the last is DoneStage
            public int Objective;                    // objective index, 10 * (step + 1)
            public int DoneStage;                    // the stage that means this step is finished
            public bool IsGroup => Group != null;
            public bool IsChoose => Choose != null;
            public bool IsHold => Waves > 0;
        }

        /// <summary>
        /// The places that say who is there (place / place.second / place.third .who), each with the beat whose
        /// object the occupants are placed around: the first pickup, use or deliver at that place that is not a
        /// return (a return shares its original's object). -1 when there is none. Pure, so the lint and the
        /// build agree by construction.
        /// </summary>
        private static List<(int place, Who who, int anchor)> WhoOf(Recipe r)
        {
            var found = new List<(int, Who, int)>();
            var places = new[] { r.place, r.place.second, r.place.third };
            for (int p = 0; p < places.Length; p++)
            {
                if (places[p]?.who is not Who w) continue;
                bool Holds(int i) => r.beats[i].PlaceIndex == p && r.beats[i].returnTo == null
                            && IsObject(r.beats[i]);
                // who.beat names the object; one that is not a pickup / use / deliver at this place resolves to -1,
                // which the lint refuses by name rather than quietly falling back to the first object.
                int a = w.beat is int nb
                    ? (nb >= 1 && nb <= r.beats.Count && Holds(nb - 1) ? nb - 1 : -1)
                    : Enumerable.Range(0, r.beats.Count).FirstOrDefault(Holds, -1);
                found.Add((p, w, a));
            }
            return found;
        }
        private static readonly string[] PlaceKey = { "place", "place.second", "place.third" };

        /// <summary>Split the beats into steps. Pure, so the lint and the build agree by construction.</summary>
        private static List<Step> StepsOf(Recipe r)
        {
            var steps = new List<Step>();
            for (int i = 0; i < r.beats.Count; i++)
            {
                var g = r.beats[i].group;
                var c = r.beats[i].choose;
                if (g != null && steps.Count > 0 && steps[^1].Group == g) { steps[^1].Beats.Add(i); continue; }
                if (c != null && steps.Count > 0 && steps[^1].Choose == c) { steps[^1].Beats.Add(i); continue; }
                // A hold with no usable wave count is still one wave here, so the lint and the build number
                // its stages the same way; the lint refuses the count itself.
                int waves = r.beats[i].type == "hold" ? Math.Max(1, r.beats[i].waves ?? 1) : 0;
                steps.Add(new Step { Beats = { i }, Group = g, Choose = c, Waves = waves });
            }
            for (int k = 0; k < steps.Count; k++) steps[k].Objective = 10 * (k + 1);
            return steps;
        }

        /// <summary>
        /// Stage numbers: every beat gets one, a group also gets a done stage, the last step finishes on
        /// the completing stage. Returns beat index -> stage, and fills each step's DoneStage.
        /// </summary>
        private static Dictionary<int, int> StagesOf(List<Step> steps, int complete, int first = 10)
        {
            var beatStage = new Dictionary<int, int>();
            int next = first;   // the first beat's stage: 10 after a slate Delve's 0, 20 after the board's accept at 10
            for (int k = 0; k < steps.Count; k++)
            {
                var s = steps[k];
                bool last = k == steps.Count - 1;
                if (s.IsChoose)
                {
                    // The endings (the lint holds them to the last step): each completes on its own stage.
                    for (int j = 0; j < s.Beats.Count; j++) beatStage[s.Beats[j]] = complete + 10 * j;
                    s.DoneStage = complete;
                    continue;
                }
                if (s.IsHold)
                {
                    // One stage per wave cleared; the last wave's is the step's done stage.
                    for (int w = 0; w < s.Waves - 1; w++) { s.WaveStages.Add(next); next += 10; }
                    int st = last ? complete : next;
                    if (!last) next += 10;
                    s.WaveStages.Add(st);
                    beatStage[s.Beats[0]] = st;
                    s.DoneStage = st;
                }
                else if (!s.IsGroup)
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

        /// <summary>
        /// Who still points at a marker+activator pair from OUTSIDE it: a ref alias filling in it or created at it, an
        /// objective target, an alias script. The pair's own activator, created at its own marker, is not a user. One
        /// function for the build's refusal and the off-disk check, so they cannot disagree about what "unused" means.
        /// </summary>
        private static List<string> SlotUsers(IEnumerable<IQuestReferenceAliasGetter> refs, IEnumerable<IQuestObjectiveGetter> objectives,
                                              IEnumerable<int> scriptedAliases, int[] pair)
            => refs.Where(a => !pair.Contains((int)a.ID)
                               && ((a.Location?.AliasID is int li && pair.Contains(li))
                                   || (a.CreateReferenceToObject?.AliasID is short ci && pair.Contains(ci))))
                   .Select(a => $"ref alias {a.ID}")
                .Concat(objectives.Where(o => o.Targets?.Any(x => x.AliasID is int id && pair.Contains(id)) == true).Select(o => $"objective {o.Index}"))
                .Concat(scriptedAliases.Where(pair.Contains).Select(a => $"alias {a}'s scripts"))
                .ToList();

        /// <summary>
        /// The talk beat that ENDS the Delve, or -1: a talk that is the whole last step. Its closing reply sets the
        /// completing stage, and that stage takes everything the player still carries, as a deliver's does (11a,
        /// 2026-10-13: the bag goes back to its owner in the conversation). A talk anywhere earlier takes nothing.
        /// Pure, so the lint and the build agree by construction.
        /// </summary>
        private static int HandBackTalk(Recipe r, List<Step> steps)
            => steps.Count > 0 && !steps[^1].IsGroup && !steps[^1].IsChoose && r.beats[steps[^1].Beats[0]].type == "talk"
               ? steps[^1].Beats[0] : -1;

        /// <summary>The lint's beats-only half. Called from Grade; Fatal/Warn are Grade's.</summary>
        private static void GradeBeats(Recipe r, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                       Action<string> Fatal, Action<string> Warn)
        {
            for (int i = 0; i < r.beats.Count; i++)
            {
                var b = r.beats[i];
                if (KindOf(b) == null)
                    Fatal($"beat {i + 1} has type '{b.type ?? "(none)"}'; a beats Delve knows {string.Join(", ", AllKinds.Select(k => k.Name))}.");
                if (Carries(b) && string.IsNullOrWhiteSpace(b.item))
                    Fatal($"beat {i + 1} is a {b.type} with no item: the player gets a thing with no name.");
                if (!Carries(b) && b.item != null)
                    Fatal($"beat {i + 1} names an item and is not a pickup or a recover, so it would be silently ignored.");
                if (b.type != "talk" && (b.greeting != null || b.exchanges != null))
                    Fatal($"beat {i + 1} has a greeting or exchanges and is a {b.type}; only a talk beat has a conversation, so they would be silently ignored.");
                if (b.returnTo is int back)
                {
                    if (back < 1 || back > i)
                        Fatal($"beat {i + 1} returns to beat {back}, which is not an earlier beat.");
                    else
                    {
                        var tgt = r.beats[back - 1];
                        if (tgt.type != "use" || tgt.returnTo != null)
                            Fatal($"beat {i + 1} returns to beat {back}, a {tgt.type}{(tgt.returnTo != null ? " return" : "")}; only a use beat's object stays in the world to come back to.");
                        if (!string.Equals(tgt.at, b.at, StringComparison.OrdinalIgnoreCase) || tgt.PlaceIndex != b.PlaceIndex)
                            Fatal($"beat {i + 1} returns to beat {back} and must name its marker and place ('{tgt.at}', {tgt.place ?? "main"}).");
                        if (r.beats.Take(i).Count(x => x.returnTo == back) > 0)
                            Fatal($"beat {back} is returned to twice; the stock hook has ONE duplicate (DefaultAliasOnActivateA), so once.");
                    }
                    if (b.type != "use" && b.type != "deliver")
                        Fatal($"beat {i + 1} is a {b.type} return; a return activates an object already there, so it is a use or a deliver.");
                    if (b.group != null) Fatal($"beat {i + 1} is a return inside group '{b.group}'; a return comes after something, so it cannot be any-order.");
                    if (b.model != null || b.name != null || b.replace)
                        Fatal($"beat {i + 1} is a return; the object is beat {back}'s, so model, name and replace would be ignored.");
                }
                if (b.group != null && b.type != "use")
                    Fatal($"beat {i + 1} is in group '{b.group}' and is a {b.type}; only use beats can be done in any order.");
                if (b.type != "hold" && (b.waves != null || b.size != null || b.defend != null || b.stuck != null || b.style != null || b.reveal != null || b.revealObjective != null))
                    Fatal($"beat {i + 1} sets waves, size, defend, stuck, style, reveal or revealObjective and is not a hold, so they would be silently ignored.");
                KindOf(b)?.GradeBeat(b, i, t, env, Fatal, Warn);
                // A hold's `defend` names an earlier beat, so it is a rule BETWEEN beats and stays here, after the kind's
                // own rules, where it always printed.
                if (b.type == "hold" && b.defend is int d)
                {
                    if (d < 1 || d > i) Fatal($"beat {i + 1} defends beat {d}, which is not an earlier beat.");
                    else
                    {
                        // Only a use or a deliver leaves an object standing (a pickup's is disabled). Said as the two it
                        // allows: listing the types it refused let a talk through, whose objective then fell back to
                        // the hold's own spawn marker (2026-10-15).
                        var dt = r.beats[d - 1];
                        if (dt.type != "use" && dt.type != "deliver")
                            Fatal($"beat {i + 1} defends beat {d}, a {dt.type}, which leaves no object standing to defend; defend a use or a deliver.");
                    }
                }
                if (!IsFight(b) && (b.target != null || b.gang != null || b.enemies != null || b.loot != null))
                    Fatal($"beat {i + 1} sets target, gang, enemies or loot and is a {b.type}; only a defeat or a clear spawns a fight, so they would be silently ignored.");
                if (b.name != null && Tokens(b.name).Any())
                    Fatal($"beat {i + 1} name carries a <Token>; an activator name is not an alias context.");
                if (b.name != null && string.IsNullOrWhiteSpace(b.model))
                    Warn($"beat {i + 1} has a name and no model; a name only goes onto a clone, so it is not written.");
                // His, 2026-10-11, after 09's pickup played as the base's boss crate with a generic prompt:
                // "we dont really want defaults". An object beat with no model or name falls back to the base's.
                if (IsObject(b) && b.returnTo == null)
                {
                    if (string.IsNullOrWhiteSpace(b.model))
                        Warn($"beat {i + 1} is a {b.type} with no model; the player sees the base's default object. Pick one from docs/quests/objects.md.");
                    if (string.IsNullOrWhiteSpace(b.name))
                        Warn($"beat {i + 1} is a {b.type} with no name; its prompt is the base's generic one.");
                }
            }

            // His, 2026-10-11: "the first objective name should have a POI name in it. It helps to know
            // what POI your looking for". So the first objective names its place by token.
            if (r.beats.Count > 0 && r.beats[0].objective is string first
                && !Tokens(first).Any(tk => tk is "Place" or "SecondPlace" or "ThirdPlace"))
                Warn($"beat 1's objective \"{first}\" names no place; the first objective tells the player which POI to look for, so give it <Place> (or <SecondPlace>, <ThirdPlace>, wherever beat 1 is).");

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
                    bool carries = j == 0 || s.IsChoose;
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
            if (steps.Count > 0 && !steps[^1].IsGroup && r.recapSay != null)
                Fatal("recapSay is set and the Delve ends on a single beat, whose own say is already the closing line; put the words there.");
            if (steps.Count > 0 && !steps[^1].IsGroup && r.recap != null)
                Fatal($"the Delve ends on a single beat, whose journal is already stage {t.completeStage}'s line; the recap would overwrite it. "
                      + "Put the words in that beat's journal.");

            if (r.place.civilians != null)
                Fatal("place.civilians is the delve4 spelling and a beats Delve ignores it; write approach.civilians.");
            if (r.approach is Approach ap)
            {
                if (t.approachStage < 0) Fatal($"template '{t.id}' names no approachStage, so an approach has no stage to set.");
                if (t.playerAlias < 0) Fatal($"template '{t.id}' names no playerAlias for the approach's hook.");
                if (ap.to < 1 || ap.to > r.beats.Count) Fatal($"approach.to is beat {ap.to}, which does not exist.");
                else
                {
                    var tb = r.beats[ap.to - 1];
                    // Only an object beat's own object can be approached. Said as what it allows: listing the types it
                    // refused let a talk through, whose distance hook then measured to alias -1 (2026-10-15).
                    if (!IsObject(tb) || tb.returnTo != null)
                        Fatal($"approach.to is beat {ap.to}, a {(tb.returnTo != null ? "return" : tb.type)}, which has no object of its own to approach.");
                    if (ap.lose is int l)
                    {
                        if (l < 1 || l > r.beats.Count || r.beats[l - 1].type != "pickup")
                            Fatal($"approach.lose is beat {l}; only a pickup's crate can be lost.");
                        else if (r.beats[l - 1].PlaceIndex != tb.PlaceIndex)
                            Fatal($"approach.lose is beat {l} at another place; the crate is moved out past the edge of the site being approached, so it must be at that site.");
                    }
                }
                if (!ap.civilians && ap.lose == null) Fatal("approach does nothing: set civilians, lose, or both.");
            }

            // --- the endings ------------------------------------------------------------------------------
            var chooseSteps = steps.Where(s => s.IsChoose).ToList();
            foreach (var cs in chooseSteps)
            {
                if (cs != steps[^1]) Fatal($"choose '{cs.Choose}' is not the last step; an ending ends the quest, so nothing can follow it.");
                if (cs == steps[0]) Fatal($"choose '{cs.Choose}' is the first step; there is nothing found yet to choose what to do with.");
                if (cs.Beats.Count < 2) Fatal($"choose '{cs.Choose}' has one ending; a choice of one is a deliver.");
                foreach (var b in cs.Beats)
                {
                    var eb = r.beats[b];
                    if (eb.type != "deliver") Fatal($"beat {b + 1} is an ending of choose '{cs.Choose}' and a {eb.type}; an ending is a deliver.");
                    if (eb.group != null || eb.returnTo != null) Fatal($"beat {b + 1} is an ending and also a group member or a return; an ending is its own object.");
                    if (cs.Beats.Any(o => o != b && r.beats[o].PlaceIndex == eb.PlaceIndex))
                        Fatal($"beat {b + 1} is an ending at the same place as another ending; the choice is made by WHERE the player goes, so each ending is its own place.");
                    if (string.IsNullOrWhiteSpace(eb.reward)) Fatal($"beat {b + 1} is an ending with no reward tier (easy / med / hard).");
                    else
                        foreach (var kind in new[] { "creds", "xp" })
                            if (!env.LoadOrder.PriorityOrder.WinningOverrides<IGlobalGetter>()
                                    .Any(x => string.Equals(x.EditorID, $"duo_reward_{kind}_{eb.reward}", StringComparison.OrdinalIgnoreCase)))
                                Fatal($"beat {b + 1} reward '{eb.reward}': no global duo_reward_{kind}_{eb.reward} in the load order.");
                    if (eb.person is Person pp)
                    {
                        if (string.IsNullOrWhiteSpace(pp.name) || string.IsNullOrWhiteSpace(pp.template)) Fatal($"beat {b + 1} person needs both a name and a template.");
                        else if (Tokens(pp.name).Any()) Fatal($"beat {b + 1} person name carries a <Token>; an NPC name is not an alias context.");
                        if (pp.company != null && (pp.company.min < 0 || pp.company.max < pp.company.min))
                            Fatal($"beat {b + 1} person.company needs 0 <= min <= max; it has {pp.company.min}-{pp.company.max}.");
                        if (pp.hellos != null)
                            Fatal($"beat {b + 1} person.hellos is a talk person's: an ending's person has no voice of his own to say them in, so they would be silently ignored.");
                    }
                    else Warn($"beat {b + 1} is an ending with no person, so nobody stands there; a box that talks about a person talks about nobody.");
                }
            }
            for (int i = 0; i < r.beats.Count; i++)
                if (r.beats[i].choose == null && r.beats[i].person != null && r.beats[i].type != "talk")
                    Fatal($"beat {i + 1} sets person and is not a choose ending or a talk, so it would be silently ignored.");
            // A reward tier is read on an ENDING: every choose ending, or the beat that is the whole last step (it sets
            // the completing stage, whose pay this replaces). Anywhere else the stage it would pay on completes nothing.
            int soleLast = steps.Count > 0 && !steps[^1].IsChoose && !steps[^1].IsGroup ? steps[^1].Beats[0] : -1;
            for (int i = 0; i < r.beats.Count; i++)
            {
                if (r.beats[i].reward is not string tier || r.beats[i].choose != null) continue;
                if (i != soleLast)
                {
                    Fatal($"beat {i + 1} sets reward and is not an ending (a choose ending, or the single beat that ends the Delve), so it would be silently ignored.");
                    continue;
                }
                foreach (var kind in new[] { "creds", "xp" })
                    if (!env.LoadOrder.PriorityOrder.WinningOverrides<IGlobalGetter>()
                            .Any(x => string.Equals(x.EditorID, $"duo_reward_{kind}_{tier}", StringComparison.OrdinalIgnoreCase)))
                        Fatal($"beat {i + 1} reward '{tier}': no global duo_reward_{kind}_{tier} in the load order.");
            }
            if (r.offer != null)
            {
                if (chooseSteps.Count == 0) Fatal("offer is set and no step is a choose; the offer is the beat that makes it a choice.");
                else
                {
                    if (string.IsNullOrWhiteSpace(r.offer.journal)) Fatal("offer.journal is required: it is the line the player reads at the find.");
                    var before = steps[steps.IndexOf(chooseSteps[0]) - 1];
                    foreach (var b in before.Beats)
                        if (r.beats[b].journal != null)
                            Fatal($"beat {b + 1} has a journal, and the offer's journal lands on the same stage in the same instant and overwrites it (the log shows only the newest line). Put what the player must read in offer.journal.");
                    if (r.offer.message != null && (string.IsNullOrWhiteSpace(r.offer.message.title) || string.IsNullOrWhiteSpace(r.offer.message.text)))
                        Fatal("offer.message needs a title and text.");
                }
            }
            // The people the approach places at the endings; a talk beat's person is created by its alias instead.
            int personCount = r.beats.Count(b => b.person != null && b.type != "talk");
            int occBase = OccupantBase(t), firstBeat = t.entryStage + 10;
            if (personCount > 0)
            {
                if (occBase < 0 || t.playerAlias < 0) Fatal($"template '{t.id}' names no approachStage or arrivalStage, or no playerAlias, for an ending's person.");
                else if (occBase + personCount >= firstBeat) Fatal($"{personCount} people need stages {occBase + 1} to {occBase + personCount}, which reach the first beat's {firstBeat}.");
            }

            // --- who is there (his dial, 2026-10-11) --------------------------------------------------------
            var whos = WhoOf(r);
            foreach (var (p, w, a) in whos)
            {
                string at = PlaceKey[p] + ".who";
                if (!WhoKinds.Contains(w.kind))
                {
                    Fatal($"{at}.kind is '{w.kind}'; built: {string.Join(", ", WhoKinds.Select(k => "\"" + k + "\""))}. Empty is the default (leave who out); the dead "
                          + "is designed in office/projects/delves/backlog.md and not built, and a briefing that names them at an empty site is the failure this field exists for.");
                    continue;
                }
                if (a < 0)
                    Fatal(w.beat is int nb
                        ? $"{at}.beat is {nb}, which is not a pickup, use or deliver at this place, so there is no object to gather round."
                        : $"{at} is {w.kind} and no pickup, use or deliver is at that place, so there is no object to place them around.");
                else if (r.beats[a].person != null) Fatal($"{at}: they gather round beat {a + 1}, an ending whose person already hooks that object's distance; one alias carries one distance hook.");
                else Console.WriteLine($"  {at}: {w.kind}, {w.min}-{w.max} placed around beat {a + 1}'s object when the player is within {ApproachDistance} of it");
                var fl = OwnOrMaster<IFormListGetter>(Allowed(env, t), env, w.list ?? "");
                if (fl == null) Fatal($"{at}.list '{w.list}' is not a FormList in {t.mod} or one of its masters.");
                else if (fl.Items.Count == 0) Fatal($"{at}.list '{w.list}' is EMPTY, so nothing would ever be placed.");
                else Console.WriteLine($"  {at}.list: {(w.kind == "infested" ? "one species" : "a mix")} from {fl.EditorID} ({fl.Items.Count} entries)");
                if (w.min < 1 || w.max < w.min) Fatal($"{at} needs 1 <= min <= max; it has {w.min}-{w.max}.");
                else if (w.kind == "workers" && w.max > 8) Warn($"{at}.max is {w.max}; more than 8 workers round one object is a crowd.");
                else if (w.max > 12) Warn($"{at}.max is {w.max}; more than 12 round one object is a swarm.");
            }
            if (whos.Count > 0)
            {
                if (occBase < 0 || t.playerAlias < 0) Fatal($"template '{t.id}' names no approachStage or arrivalStage, or no playerAlias, for a nest's stage and hook.");
                else if (occBase + personCount + whos.Count >= firstBeat)
                    Fatal($"{personCount} people and {whos.Count} nest(s) need stages {occBase + 1} to {occBase + personCount + whos.Count}, which reach the first beat's {firstBeat}.");
            }

            int recoverCount = r.beats.Count(b => b.type == "recover");
            if (recoverCount > 5) Fatal($"{recoverCount} recover beats; the player's OnItemAdded hook has the base and four duplicates, so five.");
            if (recoverCount > 0 && t.playerAlias < 0) Fatal($"template '{t.id}' names no playerAlias for a recover beat's hook.");

            // Delivering needs something in hand: the first deliver must come after a pickup or a recover.
            int carried = 0;
            bool endsOnChoice = false;
            int handBack = HandBackTalk(r, steps);
            for (int i = 0; i < r.beats.Count; i++)
            {
                if (i == handBack) carried = 0;   // the talk that ends the Delve takes what is carried
                else if (Carries(r.beats[i])) carried++;
                else if (r.beats[i].type == "deliver")
                {
                    if (carried == 0) Fatal($"beat {i + 1} is a deliver with nothing picked up or recovered before it, so there is nothing to hand over.");
                    if (r.beats[i].choose == null) carried = 0;   // every ending takes the same things
                    else endsOnChoice = true;
                }
            }
            if (carried > 0 && !endsOnChoice) Warn($"{carried} picked-up item(s) are never delivered, so they stay in the player's inventory after the quest.");

            var beatStage = StagesOf(steps, t.completeStage, t.entryStage + 10);
            int highest = beatStage.Values.Concat(steps.Select(s => s.DoneStage)).Concat(steps.SelectMany(s => s.WaveStages))
                .Where(x => x < t.completeStage).DefaultIfEmpty(0).Max();
            if (highest >= t.completeStage)
                Fatal($"the beats number stages up to {highest}, at or past the completing stage {t.completeStage}: too many beats.");
            Console.WriteLine("  steps    : " + string.Join("  ", steps.Select((s, k) =>
                $"[{k + 1}] " + (s.IsGroup ? $"group '{s.Group}' x{s.Beats.Count} -> {s.DoneStage}"
                                : s.IsChoose ? $"choose '{s.Choose}' x{s.Beats.Count} -> {string.Join("/", s.Beats.Select(b => beatStage[b]))}"
                                : s.IsHold ? $"hold x{s.Waves} waves -> {string.Join("/", s.WaveStages)}"
                                : $"{r.beats[s.Beats[0]].type} -> {s.DoneStage}"))));

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
            // --- speech: a speaker and the lines it says (broadcast at the start, say on a beat's stage) -------
            // Schema 2: every spoken field is a LIST of responses said back to back, 1 to ResponsesMax; each is checked.
            var spoken = new List<(string where, string text)>();
            void Said(string where, List<string>? said)
            {
                if (said == null) return;
                if (said.Count == 0 || said.Count > ResponsesMax) { Fatal($"{where} has {said.Count} response(s); give 1 to {ResponsesMax}, or leave it out."); return; }
                for (int k = 0; k < said.Count; k++) spoken.Add((said.Count == 1 ? where : $"{where}[{k}]", said[k]));
            }
            Said("broadcast", r.broadcast);
            for (int i = 0; i < r.beats.Count; i++) Said($"beat {i + 1}'s say", r.beats[i].say);
            Said("offer.say", r.offer?.say);
            Said("recapSay", r.recapSay);
            if (r.offer?.say != null && r.offer.message != null)
                Fatal("offer has both a message and a say; his \"either or\": a box and a line on one stage clash. Keep one.");
            if (spoken.Count > 0 && r.speaker == null) Fatal("there are spoken lines and no speaker: say who speaks them (name, voice, elevenlabs).");
            if (r.speaker != null)
            {
                if (spoken.Count == 0) Fatal("a speaker is set and nothing is said (no broadcast, no beat says anything), so it would be silently ignored.");
                if (string.IsNullOrWhiteSpace(r.speaker.name)) Fatal("speaker.name is required: it is the name on the subtitle.");
                else if (Tokens(r.speaker.name).Any()) Fatal("speaker.name carries a <Token>; an NPC name is not an alias context.");
                if (string.IsNullOrWhiteSpace(r.speaker.elevenlabs)) Fatal("speaker.elevenlabs is required: the voice the audio is generated in.");
                if (string.IsNullOrWhiteSpace(r.speaker.voice)
                    || !env.LoadOrder.PriorityOrder.WinningOverrides<IVoiceTypeGetter>().Any(v => Allowed(env, t).Contains(v.FormKey.ModKey)
                           && string.Equals(v.EditorID, r.speaker.voice, StringComparison.OrdinalIgnoreCase)))
                    Fatal($"speaker.voice '{r.speaker.voice}' is not a VoiceType in {t.mod} or a master (GenericMale01, GenericFemale01, ...).");
            }
            foreach (var (where, text) in spoken)
            {
                if (string.IsNullOrWhiteSpace(text)) Fatal($"{where} is empty.");
                else if (Tokens(text).Any()) Fatal($"{where} carries a <Token>: the subtitle could fill it and the voice could not say it.");
                if (text.Length > 250) Fatal($"{where} is {text.Length} characters; a spoken response holds 250 (split it into two beats' lines).");
            }

            string fname = FragmentScriptName(r);
            if (fname.Length > 38) Fatal($"the fragment script name {fname} is {fname.Length} chars; the Papyrus compiler refuses over 38. Shorten the id.");
        }

        private static string FragmentScriptName(Recipe r) => "duo_qf_" + r.id;

        /// <summary>The driver's LoseDistance: how close to the approached object the player comes before the approach fires.</summary>
        private const float ApproachDistance = 250f;
        /// <summary>The stage that stops a Delve ending on a talk, set when the person unloads (vanilla's shutdown number).</summary>
        private const int LingerStage = 1000;
        /// <summary>The most responses one spoken line may carry. Vanilla, measured 2026-10-19 over MQ101 and OE_Dialogue's
        /// 1,027 lines: 909 one, 101 two, 17 three.</summary>
        private const int ResponsesMax = 4;

        /// <summary>
        /// One spoken line's RESPONSES, said back to back (schema 2, his 2026-10-19: "every line of dialouge is one string.
        /// Need to add multiple line support"). Vanilla's own shape: one INFO, several responses, each with its own subtitle
        /// and its own audio (MQ101 005730: three responses, three WEMFiles). The first keeps the id the line always had
        /// (its topic's, or its INFO's), so a one-response line builds exactly as before; each later one takes a fresh id
        /// minted from the plugin's own counter, which no other line's file can share. Returns each response's (wem, text)
        /// for the voice step. A silent line (the player's) passes voiced false: WEMFile 0, nothing returned.
        /// </summary>
        private static List<(uint wem, string text)> AddResponses(StarfieldMod myMod, DialogResponses info, List<string> texts,
                                                                    uint firstWem, bool voiced, float? emotionOut = null)
        {
            var made = new List<(uint wem, string text)>();
            for (int i = 0; i < texts.Count; i++)
            {
                uint wem = !voiced ? 0u : i == 0 ? firstWem : myMod.GetNextFormKey().ID;
                var resp = new DialogResponse
                {
                    ResponseText = texts[i],
                    WEMFile = wem,
                    TextHash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(texts[i]))[..4],
                };
                if (emotionOut is float eo) { resp.EmotionOut = eo; resp.Emotion.SetTo(FormKey.None); }
                info.Responses.Add(resp);
                if (voiced) made.Add((wem, texts[i]));
            }
            return made;
        }
        /// <summary>A talk Delve's quest priority: above RQ_Dialogue (45), below the story quests (70 to 82). Measured 2026-10-19.</summary>
        private const byte TalkQuestPriority = 50;
        /// <summary>How far round its object a nest scatters, in metres. His infestation missions use 40 round a
        /// creature; a stash is a smaller thing to guard, so the nest sits closer.</summary>
        private const float NestRadius = 20f;
        /// <summary>How far round its object a who: workers crowd stands, in metres: wider than an ending's 8 m
        /// company (people at work, not a welcoming party), tighter than a nest.</summary>
        private const float WorkersRadius = 12f;

        private static string FragmentFunction(int stage) => $"Fragment_Stage_{stage:D4}_Item_00";

        /// <summary>The quest-level script every beats Delve carries: OnQuestRejected only (papyrus/duo_delve_quest.psc).</summary>
        private const string RejectScript = "duo_delve_quest";

        /// <summary>What BuildBeats made, for VerifyBeats to read back off disk.</summary>
        private sealed class BeatsMade
        {
            public List<int> ClearGroups = new();   // clear beats' collections: empty, Optional, and wearing NO package (they hold the site)
            public string Script = "";
            public List<int> FragStages = new();
            public List<(int alias, string script, int set, int prereq)> Hooks = new();
            public List<FormKey> Globals = new();
            public Dictionary<int, string> ObjectiveText = new();
            public string PscPath = "";
            public string? Recap;                                        // the completing stage's line, when ending on a group
            public List<(int objective, int alias, int stage)> TargetGates = new();
            public List<(string name, int alias)> Replaced = new();
            public List<int> Holders = new();   // recover beats' empty Optional aliases
            public List<int> Waves = new();     // hold beats' wave collections (the inner ref alias ids), empty and Optional
            public FormKey WavePackage;         // the Travel-to-player package every wave collection wears
            public (int id, string name)? DroppedPlace;   // a one-place Delve's removed second place alias
            public List<int> RejectObjectives = new();    // what duo_delve_quest hides when the mission is turned down
            public List<(int marker, int activator)> DroppedSlots = new();   // the base's object slots no beat took, removed
            public List<int> DroppedDriverAliases = new();   // board: the removed driver's own aliases, removed with it
            public bool MarkerSourceAtMap;
            public int? SecondPlanet;   // the created second planet's alias, when a beat is at the second place   // board: the marker source took no fight and fills from the map marker
            public (int on, int to)? Arrival;   // board: the arrival hook's alias (step one's marker) and the alias it measures to (the player)
            public (int stage, List<string> lines)? HandBack;   // the talk that ends the Delve: its stage and the RemoveItem lines it must run
            public FormKey Speaker;                        // the cloned NPC whose name and voice type the lines carry
            public List<(int stage, uint wem, string text, FormKey scene)> Lines = new();   // each spoken line, by the stage that plays it
            public string WavePackageShape = "";   // its source's begin/end/change topic counts, which the copy must keep
            public List<int> StuckWaits = new();   // each hold wave's death stage, which its spawning fragment sets after the stuck wait
            public List<(int objective, int alias, int seconds, int stage)> Reveals = new();   // each revealed wave: its objective, collection, wait, death stage
            public (int on, int to)? ApproachTarget;   // the approach hook's alias and the alias it measures to
            public List<(int on, int to)> PersonApproach = new();   // each ending person's hook: its object, measuring to the player
            public List<(uint wem, string text, string voice, string elevenlabs, bool female)> TalkLines = new();   // a talk person's voiced lines (female picks the face actor)
            public List<(int beat, int stage, int alias, FormKey npc, int exchanges, int conditions)> Talks = new();   // each talk beat, for the verify
            public int Linger = -1;   // the stage that stops a quest ending on a talk, once its person unloads; -1 = none
            public int LingerFrom;    // the completing stage it waits behind (the last step's DoneStage)
            public List<(int beat, FormKey npc, int lines)> Hellos = new();   // a talk person's walk-up lines, for the verify
            public List<(int place, int anchor, FormKey global)> Leashes = new();   // each place's GetDistance: the alias it measures from, under which global
            public Dictionary<int, (FormKey creds, FormKey xp)> StageReward = new();
            public Dictionary<int, (FormKey npc, string name)> People = new();
            public (int stage, string text)? OfferJournal;   // "replace" beats: the marker alias stage 0 disables  // a group member's target, lit until its stage
        }

        /// <summary>
        /// EVERY PLACE WITHIN duo_longdrivedistance OF THE OTHERS (his, 2026-10-13: "make all points within 7500").
        /// The base's leashes are GetDistance conditions whose parameter is an ALIAS NUMBER (UseAliases): the main
        /// place measures from alias 32, the player, under duo_samemapdistance (20,000; left alone, it is the slate to
        /// the first site); the second place measured from alias 1, a marker that the BASE fills inside the main place.
        /// A beat at the second place takes slot alias 1 INTO the second place, so its leash measured from its own
        /// unfilled marker, and a one-object Delve removes alias 1 outright, leaving the leash pointing at nothing:
        /// measured on six built Delves that afternoon, and the likely cause of "the delves spawn really far apart".
        /// So, after every alias is final: the second place is leashed to the MAIN place's map marker (the alias
        /// filling inside place 0 on MapMarkerRefType, which every Delve demands); the third place to that marker AND
        /// to a marker filling inside the second place, so all three are within one drive of each other pairwise.
        /// Only the condition's parameter and global change: each is a copy of the base's own condition, never a
        /// constructed one. Refuses if an anchor is missing; its POSITION is OrderAliases' (a leash is a fill dependency).
        /// </summary>
        private static int LeashPlaces(Quest clone, BeatsMade bm, FormKey? reach, bool secondRemote = false)
        {
            // A REQUESTED second place is a new site on (meant to be) another planet: no drive leash to the first, which is
            // the point of it, and its own request carries the parsec limit from the player (the remote source's).
            if (secondRemote) { Console.WriteLine("  leash    : none on the second place; it is a second request, not a drive away"); return 0; }
            var all = clone.Aliases.ToList();
            // A collection alias has no id of its own; its members carry theirs at its list position (manual 12).
            static uint? IdOf(AQuestAlias a) => a switch { QuestReferenceAlias x => x.ID, QuestLocationAlias x => x.ID, _ => null };
            int Pos(uint id) => all.FindIndex(a => IdOf(a) == id);
            var loc = all.OfType<QuestLocationAlias>().ToDictionary(a => a.ID);
            if (!loc.ContainsKey(10) && !loc.ContainsKey(33)) return 0;
            var refs = all.OfType<QuestReferenceAlias>().ToList();
            QuestReferenceAlias? Inside(uint place, bool mapMarker) => refs.FirstOrDefault(a => a.Location != null && a.Location.AliasID == (int)place
                && (!mapMarker || a.Location.RefType.FormKey.ToString().StartsWith("02271F:")));   // MapMarkerRefType [02271F:Starfield.esm]

            var mainMap = Inside(0, true);
            if (mainMap == null) { Console.WriteLine("REFUSED: no alias fills the main place's map marker, so the other places have nothing to be near."); return 1; }
            ConditionGlobal? Leash(QuestLocationAlias l) => l.Conditions.OfType<ConditionGlobal>().SingleOrDefault(c => c.Data is GetDistanceConditionData);
            var second = loc.GetValueOrDefault(10u);
            var longDrive = second != null ? Leash(second) : null;
            if (longDrive == null) { Console.WriteLine("REFUSED: the second place carries no single GetDistance leash to copy duo_longdrivedistance from."); return 1; }
            // place.reach overrides the base's duo_longdrivedistance (his 08a, 2026-10-13).
            var driveGlobal = reach ?? longDrive.ComparisonValue.FormKey;

            int Anchor(QuestLocationAlias place, ConditionGlobal c, QuestReferenceAlias to, string why)
            {
                // Position is OrderAliases' job: a leash anchor is a fill dependency (AliasGraph.LeashDepsOf),
                // so the place is moved below it, and no order existing (the anchor fills inside the place) is refused there.
                if (Pos(to.ID) < 0)
                { Console.WriteLine($"REFUSED: {place.Name}'s leash anchor alias {to.ID} is not in the quest."); return 1; }
                var d = (GetDistanceConditionData)c.Data;
                if (!d.UseAliases) { Console.WriteLine($"REFUSED: {place.Name}'s leash does not take an alias parameter; nothing here has been proven for that shape."); return 1; }
                d.FirstParameter.Index = to.ID;
                c.ComparisonValue.SetTo(driveGlobal);
                bm.Leashes.Add(((int)place.ID, (int)to.ID, driveGlobal));
                Console.WriteLine($"  +leash   : {place.Name} (alias {place.ID}) within {(reach == null ? "duo_longdrivedistance" : "place.reach " + driveGlobal)} of alias {to.ID} {to.Name} ({why})");
                return 0;
            }

            int fail = 0;
            if (second != null) fail += Anchor(second, longDrive, mainMap, "the main place's map marker");
            if (loc.GetValueOrDefault(33u) is QuestLocationAlias third)
            {
                var c = Leash(third);
                if (c == null) { Console.WriteLine("REFUSED: the third place carries no single GetDistance leash."); return fail + 1; }
                var toSecond = Inside(10, false);
                if (toSecond == null) { Console.WriteLine("REFUSED: a third place with no alias filling inside the second, so it cannot be held near it."); return fail + 1; }
                var c2 = c.DeepCopy();
                third.Conditions.Add(c2);
                fail += Anchor(third, c, mainMap, "the main place's map marker");
                fail += Anchor(third, c2, toSecond, "a marker in the second place");
            }
            return fail;
        }

        /// <summary>
        /// The beats build: BeatsBuild's ten sections, run in order until one refuses (split step 3's (c), 2026-10-15).
        /// </summary>
        private static int BuildBeats(StarfieldMod myMod, Quest clone, Template t, Recipe r,
                                      Dictionary<string, FormKey> markers, BuildMade made, BeatsMade bm, DelveInputs inputs)
        {
            var b = new BeatsBuild(myMod, clone, t, r, markers, made, bm, inputs);
            foreach (var section in new Func<int>[] { b.NoDriver, b.Slots, b.Stages, b.Fills, b.Items, b.Counters,
                                                      b.Objectives, b.Messages, b.Hooks, b.Fragment })
            {
                int f = section();
                if (f != 0) return f;
            }
            return 0;
        }

        /// <summary>
        /// ONE RUN OF BuildBeats, as a class (split step 3's (c), 2026-10-15, his "B sounds good"): what it is handed,
        /// the values its sections hand each other, and the ten sections as methods. They moved verbatim out of one
        /// 1,240-line function; the only changes are that a value one section writes and a later one reads is a field
        /// here rather than a local, and that each section ends in its own `return 0`. Every refusal was already an
        /// early `return 1`, so stopping at the first section that refuses is the old control flow exactly.
        /// </summary>
        private sealed class BeatsBuild
        {
            internal readonly StarfieldMod myMod;
            internal readonly Quest clone;
            internal readonly Template t;
            internal readonly Recipe r;
            internal readonly Dictionary<string, FormKey> markers;
            internal readonly BuildMade made;
            internal readonly BeatsMade bm;
            internal readonly DelveInputs inputs;

            internal BeatsBuild(StarfieldMod myMod, Quest clone, Template t, Recipe r, Dictionary<string, FormKey> markers,
                                BuildMade made, BeatsMade bm, DelveInputs inputs)
            {
                this.myMod = myMod; this.clone = clone; this.t = t; this.r = r;
                this.markers = markers; this.made = made; this.bm = bm; this.inputs = inputs;
            }

            // WHAT THE SECTIONS HAND EACH OTHER. Each is set by exactly one section and read only by later ones. The
            // references start `null!`, so a reader that ran too early would fail loudly; the value types (arrival,
            // travelPkg, offerMsg, the nullable keys) would read a default, and only the section order prevents that.
            /// <summary>The quest's script adapter, refused if absent. Set in section 1 (its head); read in 1, 4, 7, 9, 10.</summary>
            internal QuestAdapter vma = null!;
            /// <summary>The misc item a pickup is cloned from (the driver's CargoObject, else the template's). Set in section 1; read in 5.</summary>
            internal MiscItem? itemSrc;
            /// <summary>The removed driver's GangMembers list. Set in section 1; read in 10.</summary>
            internal FormKey? gangKey;
            /// <summary>The removed driver's MaxGangMembers. Set in section 1; read in 10.</summary>
            internal int? gangMax;
            /// <summary>The removed driver's TargetCivListMembers list. Set in section 1; read in 10.</summary>
            internal FormKey? civsKey;
            /// <summary>The package a hold's waves run. Set in section 1 (the hold kind's Prepare); read in 2 (its Slot).</summary>
            internal FormKey travelPkg;
            /// <summary>The recipe's beats grouped into steps. Set in section 1; read in 2, 3, 4, 6, 7, 8, 9, 10.</summary>
            internal List<Step> steps = null!;
            /// <summary>Beat index to the stage it sets. Set in section 1; read in 2, 3, 4, 7, 8, 9, 10.</summary>
            internal Dictionary<int, int> beatStage = null!;
            /// <summary>Whether the Delve has an arrival broadcast. Set in section 1; read in 3, 9, 10.</summary>
            internal bool arrival;
            /// <summary>Beat index to its marker, activator and target aliases. Set in section 2; read in 4, 7, 8, 9, 10.</summary>
            internal Dictionary<int, BeatSlot> slotOf = null!;
            /// <summary>Talk beat to the alias that creates its person. Set in section 2 (the talk kind's Slot); read in 8 (its Dialogue).</summary>
            internal Dictionary<int, QuestReferenceAlias> talkAlias = null!;
            /// <summary>Hold beat to its wave collections, in order. Set in section 2 (the hold kind's Slot); read in 7, 9, 10 (the kind's
            /// Objectives, Hook and fragment lines).</summary>
            internal Dictionary<int, List<int>> waveAliases = null!;
            /// <summary>How many of the base's own marker and activator pairs the object beats have taken, in beat order. Set in
            /// section 2 (the object kind's Slot); read in 2 (unusedSlots).</summary>
            internal int baseNext;
            /// <summary>The base's marker and activator pairs no beat took. Set in section 2; read in 7.</summary>
            internal List<BeatSlot> unusedSlots = null!;
            /// <summary>An ending person's beat to the stage that meets them. Set in section 3; read in 9, 10.</summary>
            internal Dictionary<int, int> personStage = null!;
            /// <summary>Who is at each place. Set in section 3; read in 9, 10.</summary>
            internal List<(int place, Who who, int anchor)> whos = null!;
            /// <summary>A who's place to its stage. Set in section 3; read in 9, 10.</summary>
            internal Dictionary<int, int> whoStage = null!;
            /// <summary>Every stage the build made, sorted. Set in section 3; read in 7.</summary>
            internal List<int> allStages = null!;
            /// <summary>Pickup beat to its cloned item. Set in section 5; read in 9, 10.</summary>
            internal Dictionary<int, FormKey> itemOf = null!;
            /// <summary>Step index to its counter global. Set in section 6; read in 7, 10.</summary>
            internal Dictionary<int, (FormKey key, string edid)> counterOf = null!;
            /// <summary>Beat index to its message box. Set in section 8; read in 10.</summary>
            internal Dictionary<int, FormKey> msgOf = null!;
            /// <summary>The offer's message box. Set in section 8; read in 10.</summary>
            internal FormKey offerMsg;
            /// <summary>A beat's person to its cloned NPC. Set in section 8; read in 10.</summary>
            internal Dictionary<int, FormKey> npcOf = null!;

            // WHAT THE BEAT KINDS USE (split step (d), 2026-10-15): the marker maker Slots sets up, and the fragment's
            // property list and line table Fragment fills. Set in their section; used by it and by the kinds it calls.
            /// <summary>The marker pattern: the template's marker source, copied before beat 1 may rewrite it. Set in section 2.</summary>
            internal QuestReferenceAlias markerPattern = null!;
            /// <summary>The template's first activator alias, live (copied at each use), or null on a base with no slots. Set in section 2.</summary>
            internal QuestReferenceAlias? activatorPattern;
            /// <summary>The marker source's alias id, and whether a beat has taken it yet. Set in section 2.</summary>
            internal int markerSourceId;
            internal bool markerSourceTaken;
            /// <summary>The fragment script's properties (alias -1 = a plain form) and its lines per stage. Set in section 10.</summary>
            internal List<(string type, string pname, FormKey key, int alias)> props = null!;
            internal SortedDictionary<int, List<string>> code = null!;

            /// <summary>
            /// A beat's marker alias. A slotless base's marker alias IS the first fight's marker (on the board, the system
            /// and planet resolve from it, so it stays), so the first call takes it; every later one is a copy of the pattern.
            /// A local function of section 2 until the kinds needed it.
            /// </summary>
            internal QuestReferenceAlias NewMarker(int i, string suffix)
            {
                if (!markerSourceTaken)
                {
                    markerSourceTaken = true;
                    return clone.Aliases!.OfType<QuestReferenceAlias>().First(a => a.ID == (uint)markerSourceId);
                }
                uint id = NextAliasId(clone);
                var m = markerPattern.DeepCopy(); m.ID = id; m.Name = "DelveBeat" + (i + 1) + suffix;
                clone.Aliases.Add(m);
                return m;
            }

            /// <summary>One line of the fragment script, on a stage. A local function of section 10 until the kinds needed it.</summary>
            internal void Add(int st, string line) { if (!code.ContainsKey(st)) code[st] = new(); code[st].Add(line); }

            /// <summary>
            /// The lines that run at the very END of a stage's fragment, after every other line of it: a hold wave's waits,
            /// and the offer's radio call (after the find's box is closed). Set in section 10.
            /// </summary>
            internal Dictionary<int, List<string>> tails = null!;

            /// <summary>One line at the end of a stage's fragment. A local function of section 10 until the hold kind needed it.</summary>
            internal void Tail(int st, string line) { if (!tails.ContainsKey(st)) tails[st] = new(); tails[st].Add(line); }

            /// <summary>How many recover hooks the player alias carries so far: each takes the next A-D duplicate. Set in section 9.</summary>
            internal int recoversHooked;

            /// <summary>
            /// The end of an activation-style stock hook a beat has built (its script and properties chosen): found or
            /// created on the alias, refused if the alias already carries that script, and printed. Shared by section 9's
            /// object beats and the recover kind.
            /// </summary>
            internal int AttachBeatHook(int b, int hookAlias, string script, ScriptEntry sc, int prereq)
            {
                if (AttachHook(vma, clone.FormKey, hookAlias, sc, HookEntry.FindOrCreate, true, bm, beatStage[b], prereq) == Hooked.AlreadyCarries)
                { Console.WriteLine($"REFUSED: alias {hookAlias} already carries {script}; a beat cannot hook it twice."); return 1; }
                Console.WriteLine($"  +hook    : beat {b + 1} ({r.beats[b].type}) alias {hookAlias} {script} sets {beatStage[b]} after {prereq}");
                return 0;
            }

            /// <summary>A beat's objective index: a choose step's beats take one each, ten apart; any other step shares one.
            /// Used by sections 7 and 10 (a local function of section 7 until they became methods).</summary>
            private static int ObjOf(Step st, int b) => st.IsChoose ? st.Objective + 10 * st.Beats.IndexOf(b) : st.Objective;

            // --- 1. no driver ---------------------------------------------------------------------------
            internal int NoDriver()
            {
                vma = clone.VirtualMachineAdapter!;
                if (vma == null) { Console.WriteLine("REFUSED: the base has no VMAD."); return 1; }

                var old = vma.Scripts.FirstOrDefault(s => string.Equals(s.Name, t.replacesDriver, StringComparison.OrdinalIgnoreCase));
                if (old == null) { Console.WriteLine($"REFUSED: the base carries no '{t.replacesDriver}' to remove."); return 1; }
                var cargo = old.Properties.OfType<ScriptObjectProperty>()
                    .FirstOrDefault(p => string.Equals(p.Name, "CargoObject", StringComparison.OrdinalIgnoreCase))?.Object.FormKey;
                itemSrc = cargo != null ? myMod.MiscItems.FirstOrDefault(m => m.FormKey == cargo.Value)
                            : t.itemSource != null ? myMod.MiscItems.FirstOrDefault(m => m.EditorID == t.itemSource) : null;
                // recover: the holder and his gang are drawn from the removed driver's own list, as delve4's were.
                gangKey = old.Properties.OfType<ScriptObjectProperty>()
                    .FirstOrDefault(p => string.Equals(p.Name, "GangMembers", StringComparison.OrdinalIgnoreCase))?.Object.FormKey;
                gangMax = old.Properties.OfType<ScriptIntProperty>()
                    .FirstOrDefault(p => string.Equals(p.Name, "MaxGangMembers", StringComparison.OrdinalIgnoreCase))?.Data;
                civsKey = old.Properties.OfType<ScriptObjectProperty>()
                    .FirstOrDefault(p => string.Equals(p.Name, "TargetCivListMembers", StringComparison.OrdinalIgnoreCase))?.Object.FormKey;
                if (r.approach?.civilians == true && civsKey == null)
                { Console.WriteLine($"REFUSED: approach.civilians needs the removed driver's TargetCivListMembers, and '{t.replacesDriver}' lacks it."); return 1; }
                if (r.approach?.lose != null && gangKey == null)
                { Console.WriteLine($"REFUSED: approach.lose places its helper from the removed driver's GangMembers, and '{t.replacesDriver}' lacks it."); return 1; }
                foreach (var kind in KindsIn(r)) if (kind.Preflight(this) != 0) return 1;

                // Every build drops the previous build's wave package, hold or not, so a rebuild that lost its hold leaves
                // none behind; the hold kind's Prepare clones the new one.
                travelPkg = default;
                RemovePrevious(myMod.Packages, e => e.StartsWith(r.id + "_wavepkg"));
                foreach (var kind in KindsIn(r)) if (kind.Prepare(this) != 0) return 1;
                vma.Scripts.Remove(old);
                if (vma.Scripts.Count != 0)
                { Console.WriteLine("REFUSED: the base carries other quest scripts: " + string.Join(", ", vma.Scripts.Select(s => s.Name))); return 1; }
                Console.WriteLine($"  driver   : {t.replacesDriver} REMOVED; the stages are the state machine");
                if (t.questScript != null)
                {
                    // A board mission: the base class the board talks to goes on in the driver's place, carrying the driver's
                    // values for the properties it declares. It handles a turn-down itself (OnQuestRejected -> FailStage).
                    var qs = new ScriptEntry { Name = t.questScript };
                    foreach (var keep in t.questScriptKeeps)
                    {
                        var p = old.Properties.FirstOrDefault(x => string.Equals(x.Name, keep, StringComparison.OrdinalIgnoreCase));
                        if (p == null) { Console.WriteLine($"REFUSED: the removed driver carries no '{keep}' for {t.questScript} to keep."); return 1; }
                        qs.Properties.Add(p.DeepCopy());
                    }
                    vma.Scripts.Add(qs);
                    var gone = old.Properties.Select(x => x.Name).Where(n => !t.questScriptKeeps.Contains(n, StringComparer.OrdinalIgnoreCase)).ToList();
                    Console.WriteLine($"  script   : {t.questScript} attached with the driver's {qs.Properties.Count} board properties; dropped its own: {string.Join(", ", gone)}");
                }
                else
                {
                    // The driver was also the only OnQuestRejected handler, so one small quest script takes that job alone
                    // (his 2026-10-14: a turned-down Delve could not be restarted). Its Objectives are filled at the end of
                    // the build, once every objective is written (RejectScript).
                    vma.Scripts.Add(new ScriptEntry { Name = RejectScript });
                    Console.WriteLine($"  reject   : {RejectScript} attached (a turned-down mission hides its objectives and stops)");
                }
                // The base's own alias hooks on the alias the beats take over as a marker went with its driver's design
                // (MB28a's DefaultAliasOnDeath on PrimaryRef sets 100 with no prereq; a marker never dies).
                if (t.markerSource >= 0)
                    foreach (var e in vma.Aliases.Where(a => a.Property.Alias == t.markerSource).ToList())
                    {
                        vma.Aliases.Remove(e);
                        Console.WriteLine($"  -hook    : the base's {string.Join(", ", e.Scripts.Select(s => s.Name))} on alias {t.markerSource} (the driver's kill; the beats own this alias now)");
                    }

                steps = StepsOf(r);
                beatStage = StagesOf(steps, t.completeStage, t.entryStage + 10);
                // A board's broadcast waits for his arrival at the site (arrivalStage), set by a distance hook below.
                arrival = t.arrivalStage >= 0 && r.broadcast != null;
                return 0;
            }

            // --- 2. one marker + activator per beat -------------------------------------------------------
            internal int Slots()
            {
                // Each beat's kind makes its slot, in beat order (an object beat takes the base's own pairs first, then
                // clones the first: ObjectBeat.Slot); then a return shares its earlier beat's, and a hold its defended one's.
                // A base with no slots names a marker alias instead, and has no activator to give.
                slotOf = new Dictionary<int, BeatSlot>();
                bool slotless = t.beatSlots.Count == 0;
                int srcMarkerId = slotless ? t.markerSource : t.beatSlots[0].markerAlias;
                var srcMarker = clone.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)srcMarkerId);
                var srcAct = slotless ? null : clone.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)t.beatSlots[0].activatorAlias);
                if (srcMarker?.Location == null || (!slotless && srcAct?.CreateReferenceToObject == null))
                { Console.WriteLine(slotless ? $"REFUSED: markerSource alias {srcMarkerId} is not a ref alias filled from a place on this base."
                                             : "REFUSED: template slot 0 is not a marker+activator pair on this base."); return 1; }
                srcMarker = srcMarker.DeepCopy();   // the pattern, taken before beat 1 may rewrite the alias itself
                // A slotless base's marker alias IS the first fight's marker (on the board, the system and planet resolve
                // from it, so it stays); every later marker is a copy of it. NewMarker does both, for this pass and the kinds.
                markerPattern = srcMarker;
                activatorPattern = srcAct;
                // ⛔ What NewMarker hands out first is the template's marker source when it names one, never slot 0's marker
                // (2026-10-19, missionboard01a): once the board gained slots (1d1a01d) a defeat was handed slot 0's marker,
                // so it stood on alias 20 beside PrimaryRef 3, the slot pass then removed it, and the two contended. On
                // 10-14, slotless, it stood on 3 and played. The pattern above is still slot 0's, as every board build since.
                markerSourceId = t.markerSource >= 0 ? t.markerSource : srcMarkerId;
                markerSourceTaken = t.markerSource < 0;
                baseNext = 0;
                talkAlias = new Dictionary<int, QuestReferenceAlias>();   // talk beat -> the alias that creates its person
                waveAliases = new Dictionary<int, List<int>>();   // hold beat -> its wave collections, in order
                for (int i = 0; i < r.beats.Count; i++)
                {
                    if (r.beats[i].returnTo != null) continue;   // shares an earlier beat's object; filled in below
                    if (KindOrThrow(r.beats[i], i).Slot(this, i) != 0) return 1;
                }
                // A marker source no fight took (a board mission that opens with a talk or a pickup) is still the board's PrimaryRef,
                // which the system and planet resolve from, so it stays; it is pointed at the place's MAP MARKER, which every
                // requested site carries and the lint always demands, so it never asks the site for a ref type no beat named.
                if (!markerSourceTaken)
                {
                    var ps = clone.Aliases!.OfType<QuestReferenceAlias>().First(a => a.ID == (uint)t.markerSource);
                    if (!markers.TryGetValue("MapMarkerRefType", out var mm)) { Console.WriteLine("REFUSED: the lint handed in no MapMarkerRefType to point the marker source at."); return 1; }
                    ps.Location!.RefType.SetTo(mm);
                    // ⛔ AND IT SHARES THAT REFERENCE: the board's own MapMarker alias fills from the same one map marker, and an
                    // alias cannot take a reference another alias of the quest already holds unless it allows reuse. Without
                    // this flag PrimaryRef (not Optional) could not fill and the quest never started, so missionboard02a never
                    // listed (his play, 2026-10-14). Vanilla's flag for exactly this: MB_Cargo01Far's DestinationCargo.
                    // The flag belongs on whichever of them fills SECOND, and the alias order is the build's to change, so every
                    // alias sharing the reference carries it (the contention check's bite caught it on the wrong one first).
                    foreach (var sh in clone.Aliases!.OfType<QuestReferenceAlias>().Where(a => a.Location != null && a.CreateReferenceToObject == null
                                 && a.Location.AliasID == ps.Location.AliasID && a.Location.RefType.FormKey == mm))
                    {
                        sh.Flags = (sh.Flags ?? 0) | QuestReferenceAlias.Flag.AllowReuseInQuest;   // a null flag set |= stays null
                        Console.WriteLine($"  reuse    : alias {sh.ID} {sh.Name} may share the place's map marker (AllowReuseInQuest)");
                    }
                    bm.MarkerSourceAtMap = true;
                    Console.WriteLine($"  source   : alias {t.markerSource} {ps.Name} took no fight, so it fills from the place's map marker (no ref type demanded)");
                }
                // A return is the earlier beat's own object visited again: same aliases, its own stage and journal.
                for (int i = 0; i < r.beats.Count; i++)
                    if (r.beats[i].returnTo is int back)
                    {
                        var o = slotOf[back - 1];
                        slotOf[i] = new BeatSlot { markerAlias = o.markerAlias, activatorAlias = o.activatorAlias, journalStage = beatStage[i] };
                        Console.WriteLine($"  slot     : beat {i + 1} returns to beat {back}'s object (alias {o.activatorAlias})");
                    }
                // The base's object slots no beat took (11a, 2026-10-13: a recover and a talk, neither with an object of its
                // own). Each is removed after the objectives are rebuilt, below, as the one-place Delve's second place is: a
                // non-Optional marker alias left in still demands its marker at the place, which a dungeon does not carry, so
                // the whole quest would silently never start.
                unusedSlots = t.beatSlots.Skip(baseNext).ToList();
                // A hold's objective points at what is being defended, when it names one (a return shares its
                // original's object, so this runs after the returns are filled).
                for (int i = 0; i < r.beats.Count; i++)
                    if (r.beats[i].type == "hold" && r.beats[i].defend is int d)
                    {
                        slotOf[i].targetAlias = slotOf[d - 1].activatorAlias;
                        Console.WriteLine($"  slot     : beat {i + 1} (hold) defends beat {d}'s object (alias {slotOf[i].targetAlias})");
                    }
                return 0;
            }

            // --- 3. stages: one per beat, one per group's done, cloned from a working plain stage ---------
            internal int Stages()
            {
                // A Delve whose person talks speaks ABOVE the ambient greeting pool (his 2026-10-19, lever B). The engine
                // takes a line from the highest-priority quest that has one: ours sat at 10 (slate) and 15 (board), under
                // OE_Dialogue 30 and RQ_Dialogue 45, so a vanilla walk-up line beat his own. Vanilla's story quests, whose
                // named people greet in their own lines, sit at 70 to 82; this goes between the two. Every line it raises
                // is gated on our own person (GetIsID) or our own quest's stages, so it outranks nobody else's.
                if (r.beats.Any(x => x.type == "talk"))
                {
                    int was = clone.Data!.Priority;
                    clone.Data.Priority = TalkQuestPriority;
                    Console.WriteLine($"  priority : {was} -> {TalkQuestPriority} (a person talks; above the ambient greeting pool, below the story quests)");
                }
                // An ending's person is placed on its own stage, numbered after the approach's.
                personStage = new Dictionary<int, int>();
                foreach (var b in Enumerable.Range(0, r.beats.Count).Where(b => r.beats[b].person != null && r.beats[b].type != "talk"))
                    personStage[b] = OccupantBase(t) + 1 + personStage.Count;
                // A place's occupants, after the people: their own stage, set by a distance hook on the object they surround.
                whos = WhoOf(r);
                whoStage = new Dictionary<int, int>();
                foreach (var (p, _, _) in whos) whoStage[p] = OccupantBase(t) + 1 + personStage.Count + whoStage.Count;
                // A second ending completes the quest the same way as the first: a COPY of the completing stage
                // (its CompleteQuest flag and its reward entry), never a plain stage, which completes nothing.
                foreach (var cs in steps.Where(s => s.IsChoose))
                    for (int j = 1; j < cs.Beats.Count; j++)
                    {
                        int st = beatStage[cs.Beats[j]];
                        if (clone.Stages!.Any(x => x.Index == st)) { Console.WriteLine($"REFUSED: stage {st} already exists on this base."); return 1; }
                        var done = clone.Stages.FirstOrDefault(x => x.Index == t.completeStage);
                        if (done == null || done.LogEntries.Count != 1 || done.LogEntries[0].Flags?.HasFlag(QuestLogEntry.Flag.CompleteQuest) != true)
                        { Console.WriteLine($"REFUSED: stage {t.completeStage} is not a single-entry completing stage to copy an ending from."); return 1; }
                        var copy = done.DeepCopy();
                        copy.Index = (ushort)st;
                        copy.LogEntries[0].Entry = null;
                        clone.Stages.Add(copy);
                        Console.WriteLine($"  +stage   : {st} (ending {j + 1} of choose '{cs.Choose}', a copy of completing stage {t.completeStage})");
                    }
                // A Delve that ENDS on a talk outlives its completion (his 2026-10-19, 15a: "Think we need to have the quest
                // run for a little while after so you can speak to her"). Every topic loops back to the menu, and a stopped
                // quest takes its scenes, so the completing stage does not Stop(): this stage does, set by vanilla's
                // DefaultAliasOnUnload on the person once the quest is complete, i.e. when he walks away from her. That is
                // vanilla's shape (64+ attachments in Starfield.esm; the timer script, DefaultQuestShutdownScript, shows none).
                if (!steps[^1].IsChoose && steps[^1].Beats.Any(b => r.beats[b].type == "talk"))
                {
                    bm.Linger = LingerStage;
                    bm.LingerFrom = steps[^1].DoneStage;
                    if (clone.Stages!.Any(x => x.Index == LingerStage)) { Console.WriteLine($"REFUSED: stage {LingerStage} already exists on this base; the talk's shutdown stage collides."); return 1; }
                }
                allStages = beatStage.Values.Concat(steps.Select(s => s.DoneStage)).Concat(steps.SelectMany(s => s.WaveStages))
                    .Concat(bm.Linger >= 0 ? new[] { bm.Linger } : Array.Empty<int>())
                    .Concat(r.approach != null ? new[] { t.approachStage } : Array.Empty<int>())
                    .Concat(personStage.Values).Concat(whoStage.Values)
                    .Concat(arrival ? new[] { t.arrivalStage } : Array.Empty<int>()).Distinct().OrderBy(x => x).ToList();
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
                if (t.entryStage == 0)
                {
                    var s0 = clone.Stages.FirstOrDefault(s => s.Index == 0);
                    if (s0 == null) { Console.WriteLine("REFUSED: the base has no stage 0 to run on start."); return 1; }
                    s0.Flags |= QuestStage.Flag.RunOnStart;
                }
                else
                {
                    // Set by something outside the quest (the board's accept), never on start: on start is the board refresh.
                    var se = clone.Stages.FirstOrDefault(s => s.Index == t.entryStage);
                    if (se == null) { Console.WriteLine($"REFUSED: the base has no entry stage {t.entryStage}."); return 1; }
                    if (se.Flags.HasFlag(QuestStage.Flag.RunOnStart)) { Console.WriteLine($"REFUSED: entry stage {t.entryStage} runs on start; the mission would begin before it is taken."); return 1; }
                    Console.WriteLine($"  entry    : stage {t.entryStage} (set by the board's accept); step one spawns and shows there, not on start");
                }
                return 0;
            }

            // --- 4. fills, journals, reskins ---------------------------------------------------------
            internal int Fills()
            {
                for (int i = 0; i < r.beats.Count; i++)
                {
                    if (r.beats[i].returnTo != null)
                    {
                        // The fill is the earlier beat's; only this beat's journal is its own.
                        if (r.beats[i].journal != null)
                        {
                            var je = clone.Stages.FirstOrDefault(s => s.Index == beatStage[i])?.LogEntries.FirstOrDefault();
                            if (je == null) { Console.WriteLine($"REFUSED: stage {beatStage[i]} has no log entry for beat {i + 1}'s journal."); return 1; }
                            je.Entry = Expand(r.beats[i].journal!, t);
                        }
                        continue;
                    }
                    int f = WriteBeat(clone, t, slotOf[i], r.beats[i], markers, true, PlaceAliasOf(t, r.beats[i].PlaceIndex, made.ThirdPlaceAlias));
                    if (f > 0) return f;
                    if (slotOf[i].activatorAlias >= 0
                        && ReskinActivator(myMod, clone, t, $"{r.id}_b{i + 1}", slotOf[i].activatorAlias, r.beats[i].model, r.beats[i].name) != 0) return 1;
                }

                // The endings' pay, ON THE STAGE: QuestStage -> LogEntries -> StageCompleteDatas -> RewardDatas,
                // BonusCredits (QRCR) and XpAwarded (QRXP), pointed at the tier's two globals. Written for every
                // ending, the first one too, rather than trusting what the copy inherited.
                // A single-beat last step with a tier is an ending too: it sets the completing stage, so its tier
                // replaces the base's own pay there (the lint holds reward to these two places).
                var paid = steps.Where(s => s.IsChoose).SelectMany(s => s.Beats)
                    .Concat(steps[^1].IsChoose || steps[^1].IsGroup || r.beats[steps[^1].Beats[0]].reward == null
                            ? Enumerable.Empty<int>() : new[] { steps[^1].Beats[0] }).ToList();
                if (t.questScript != null)
                {
                    // A board mission pays through the board (MissionComplete -> MissionParent), off the script's reward
                    // globals, which the card also shows: so a tier goes THERE, and never onto the stage, or it pays twice.
                    // Absent, the base's own globals stand (MB28a: duo_reward_creds_easy), as a slate Delve keeps its base's stage pay.
                    var qs = vma.Scripts.First(s => s.Name == t.questScript);
                    foreach (var b in paid)
                    {
                        var cg = myMod.Globals.FirstOrDefault(g => g.EditorID == $"duo_reward_creds_{r.beats[b].reward}");
                        if (cg == null) { Console.WriteLine($"REFUSED: {t.mod} has no duo_reward_creds_{r.beats[b].reward}."); return 1; }
                        foreach (var pn in new[] { "RewardAmountGlobal", "RewardAmountGlobalActual" })
                        {
                            var rp = qs.Properties.OfType<ScriptObjectProperty>().FirstOrDefault(p => p.Name == pn);
                            if (rp == null) { Console.WriteLine($"REFUSED: {t.questScript} carries no {pn} to pay through."); return 1; }
                            rp.Object.SetTo(cg.FormKey);
                        }
                        Console.WriteLine($"  reward   : the board pays {cg.EditorID} (RewardAmountGlobal and its Actual)");
                    }
                    paid.Clear();
                }
                foreach (var b in paid)
                {
                    var st = clone.Stages.First(x => x.Index == beatStage[b]);
                    var rds = st.LogEntries[0].StageCompleteDatas.SelectMany(c => c.RewardDatas).ToList();
                    if (rds.Count != 1) { Console.WriteLine($"REFUSED: stage {st.Index} carries {rds.Count} reward entries; this tool writes exactly one."); return 1; }
                    var cg = myMod.Globals.FirstOrDefault(g => g.EditorID == $"duo_reward_creds_{r.beats[b].reward}");
                    var xg = myMod.Globals.FirstOrDefault(g => g.EditorID == $"duo_reward_xp_{r.beats[b].reward}");
                    if (cg == null || xg == null) { Console.WriteLine($"REFUSED: {t.mod} has no duo_reward_creds_{r.beats[b].reward} / duo_reward_xp_{r.beats[b].reward}."); return 1; }
                    rds[0].BonusCredits.SetTo(cg.FormKey);
                    rds[0].XpAwarded.SetTo(xg.FormKey);
                    bm.StageReward[st.Index] = (cg.FormKey, xg.FormKey);
                    Console.WriteLine($"  reward   : stage {st.Index} pays {cg.EditorID} + {xg.EditorID}");
                }
                // The offer: the line the player reads at the find, on the stage that enters the choice.
                if (r.offer?.journal != null)
                {
                    int enter = steps[steps.FindIndex(s => s.IsChoose) - 1].DoneStage;
                    var oe = clone.Stages.FirstOrDefault(x => x.Index == enter)?.LogEntries.FirstOrDefault();
                    if (oe == null) { Console.WriteLine($"REFUSED: stage {enter} has no log entry for the offer's journal."); return 1; }
                    oe.Entry = Expand(r.offer.journal, t);
                    bm.OfferJournal = (enter, oe.Entry.String!);
                    Console.WriteLine($"  offer    : journal on stage {enter}");
                }
                return 0;
            }

            // --- 5. items, one clone per pickup -----------------------------------------------------------
            internal int Items()
            {
                RemovePrevious(myMod.MiscItems, e => e.StartsWith(r.id + "_item"));
                itemOf = new Dictionary<int, FormKey>();
                for (int i = 0; i < r.beats.Count; i++)
                {
                    if (!Carries(r.beats[i])) continue;
                    if (itemSrc == null) { Console.WriteLine($"REFUSED: the removed driver's CargoObject is not a MiscItem in {t.mod} to clone."); return 1; }
                    var mi = myMod.MiscItems.DuplicateInAsNewRecord(itemSrc);
                    mi.EditorID = $"{r.id}_item{i + 1}";
                    mi.Name = r.beats[i].item!;
                    itemOf[i] = mi.FormKey;
                    Console.WriteLine($"  +item    : {mi.EditorID} {mi.FormKey}  \"{mi.Name}\"  (clone of {itemSrc.EditorID})");
                }
                return 0;
            }

            // --- 6. counters: one global per group, cloned, listed for <Global=> in the objective text -------
            internal int Counters()
            {
                RemovePrevious(myMod.Globals, e => e.StartsWith(r.id + "_count"));
                var globalSrc = myMod.Globals.FirstOrDefault(g => g.EditorID == "duo_reward_xp_easy");
                counterOf = new Dictionary<int, (FormKey key, string edid)>();   // step index -> counter
                for (int k = 0; k < steps.Count; k++)
                {
                    if (!steps[k].IsGroup && !steps[k].IsHold) continue;
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
                return 0;
            }

            // --- 7. objectives: one per step, targets = its beats' activators --------------------------------
            internal int Objectives()
            {
                var lastOb = clone.Objectives?.OrderBy(o => o.Index).LastOrDefault();
                if (lastOb?.Targets == null || lastOb.Targets.Count != 1)
                { Console.WriteLine("REFUSED: the base's last objective is not a single-target objective to clone."); return 1; }
                var obSrc = lastOb.DeepCopy();
                var obIdx =steps.SelectMany(st => st.Beats.Select(b => ObjOf(st, b))).ToHashSet();
                clone.Objectives!.RemoveAll(o => !obIdx.Contains(o.Index));
                for (int k = 0; k < steps.Count; k++)
                {
                    var s = steps[k];
                    if (s.IsChoose)
                    {
                        // One objective per ending, each pointing at its own delivery point.
                        foreach (var b in s.Beats)
                        {
                            int oi = ObjOf(s, b);
                            var eo = clone.Objectives.FirstOrDefault(o => o.Index == oi);
                            if (eo == null) { eo = obSrc.DeepCopy(); eo.Index = (ushort)oi; clone.Objectives.Add(eo); }
                            var et = eo.Targets![0].DeepCopy();
                            et.AliasID = slotOf[b].ObjectiveTarget;
                            eo.Targets.Clear();
                            eo.Targets.Add(et);
                            string etext = Expand(r.beats[b].objective!, t);
                            eo.DisplayText = etext;
                            bm.ObjectiveText[oi] = etext;
                            Console.WriteLine($"  objective: {oi} \"{etext}\" -> alias {slotOf[b].ObjectiveTarget} (ending of '{s.Choose}')");
                        }
                        continue;
                    }
                    var ob = clone.Objectives.FirstOrDefault(o => o.Index == s.Objective);
                    if (ob == null) { ob = obSrc.DeepCopy(); ob.Index = (ushort)s.Objective; clone.Objectives.Add(ob); }
                    var tgt = ob.Targets![0].DeepCopy();
                    ob.Targets.Clear();
                    foreach (var b in s.Beats)
                    {
                        var tt = tgt.DeepCopy();
                        tt.AliasID = slotOf[b].ObjectiveTarget;
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
                            bm.TargetGates.Add((s.Objective, slotOf[b].ObjectiveTarget, beatStage[b]));
                        }
                        ob.Targets.Add(tt);
                    }
                    string text = Expand(r.beats[s.Beats[0]].objective!, t);
                    if (s.IsGroup) text += $" (<Global={counterOf[k].edid}>/{s.Beats.Count})";
                    if (s.IsHold) text += $" (<Global={counterOf[k].edid}>/{s.Waves})";
                    ob.DisplayText = text;
                    bm.ObjectiveText[s.Objective] = text;
                    Console.WriteLine($"  objective: {s.Objective} \"{text}\" -> alias(es) {string.Join(", ", s.Beats.Select(b => slotOf[b].ObjectiveTarget))}");
                }
                // The kinds' own objectives beyond their step's (a hold's revealed waves), once every step's is written.
                for (int k = 0; k < steps.Count; k++)
                    foreach (var b in steps[k].Beats)
                        if (KindOf(r.beats[b]) is BeatKind kind && kind.Objectives(this, k, b, obSrc) != 0) return 1;
                var sortedObs = clone.Objectives.OrderBy(o => o.Index).ToList();
                clone.Objectives.Clear();
                clone.Objectives.AddRange(sortedObs);

                // The unused object slots go FIRST (2026-10-14, the first Delves with no object beat at all, a lone defeat or
                // clear): base slot 2's marker fills inside the second place, so while it stands the second place is still
                // "named" and its own removal below refuses.
                foreach (var us in unusedSlots)
                {
                    var pair = new[] { us.markerAlias, us.activatorAlias };
                    var gone = clone.Aliases!.OfType<QuestReferenceAlias>().Where(a => pair.Contains((int)a.ID)).ToList();
                    if (gone.Count != 2) { Console.WriteLine($"REFUSED: unused base slot {us.markerAlias}/{us.activatorAlias} is not a marker+activator pair on this base."); return 1; }
                    var users = SlotUsers(clone.Aliases!.OfType<IQuestReferenceAliasGetter>(), clone.Objectives!, vma.Aliases.Select(a => (int)a.Property.Alias), pair);
                    if (users.Count > 0) { Console.WriteLine($"REFUSED: unused base slot {us.markerAlias}/{us.activatorAlias} is still named by {string.Join(", ", users)}."); return 1; }
                    foreach (var a in gone) clone.Aliases!.Remove(a);
                    bm.DroppedSlots.Add((us.markerAlias, us.activatorAlias));
                    Console.WriteLine($"  -slot    : base slot {us.markerAlias}/{us.activatorAlias} removed: no beat took it, and its marker would be demanded at the place for nothing");
                }
                // The removed driver's own aliases (the board's EnemyType), named by nothing once it is gone.
                foreach (int da in t.driverAliases)
                {
                    var gone = clone.Aliases!.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)da);
                    if (gone == null) { Console.WriteLine($"REFUSED: the template names driver alias {da} and the base has none."); return 1; }
                    var users = SlotUsers(clone.Aliases!.OfType<IQuestReferenceAliasGetter>(), clone.Objectives!, vma.Aliases.Select(a => (int)a.Property.Alias), new[] { da });
                    string token = $"<Alias={gone.Name}>";
                    if (clone.MissionBoardDescription?.String?.Contains(token) == true || clone.Name?.String?.Contains(token) == true) users.Add("the quest's name or card");
                    if (users.Count > 0) { Console.WriteLine($"REFUSED: driver alias {da} ({gone.Name}) is still named by {string.Join(", ", users)}."); return 1; }
                    clone.Aliases!.Remove(gone);
                    bm.DroppedDriverAliases.Add(da);
                    Console.WriteLine($"  -alias   : {gone.Name} (alias {da}) removed: the driver's, and the driver is gone");
                }
                // A ONE-PLACE Delve drops the base's second place. Left in, it is a non-Optional location alias that
                // still draws a SECOND POI the quest never visits, so the quest only starts where two qualifying POIs
                // are in range, and the base's text about it lingers (his catch on duo_delve09, 2026-10-09: "we have
                // DungeonLocation and FinalLocation but the quest takes part in one POI"). Refused, writing nothing,
                // if anything the player can read or any alias still depends on it.
                if (t.secondPlaceAlias >= 0 && !r.beats.Any(b => b.PlaceIndex == 1))
                {
                    var second = clone.Aliases!.OfType<QuestLocationAlias>().FirstOrDefault(a => a.ID == (uint)t.secondPlaceAlias);
                    if (second == null) { Console.WriteLine($"REFUSED: the template names second place alias {t.secondPlaceAlias} and the base has none."); return 1; }
                    string token = $"<Alias={second.Name}>";
                    var users = clone.Aliases!.OfType<QuestReferenceAlias>().Where(a => a.Location?.AliasID == t.secondPlaceAlias).Select(a => $"ref alias {a.ID}")
                        .Concat(clone.Aliases!.OfType<QuestLocationAlias>().Where(a => a.ParentSystemLocationAliasID == t.secondPlaceAlias).Select(a => $"location alias {a.ID}"))
                        .Concat(clone.Objectives!.Where(o => o.DisplayText?.String?.Contains(token) == true).Select(o => $"objective {o.Index}"))
                        .Concat(clone.Stages!.Where(s => allStages.Contains(s.Index))   // stage 0's briefing is the recipe's, written after this and graded off disk
                            .Where(s => s.LogEntries.Any(e => e.Entry?.String?.Contains(token) == true)).Select(s => $"stage {s.Index}'s journal"))
                        .ToList();
                    if (users.Count > 0) { Console.WriteLine($"REFUSED: the unused second place {second.Name} is still named by {string.Join(", ", users)}."); return 1; }
                    // A base stage this Delve never sets can still carry the base's line about it: clear the line.
                    foreach (var s in clone.Stages!.Where(s => !allStages.Contains(s.Index) && s.Index != 0))
                        foreach (var e in s.LogEntries.Where(e => e.Entry?.String?.Contains(token) == true))
                        {
                            e.Entry = null;
                            Console.WriteLine($"  unused   : stage {s.Index} (never set here) loses the base's line naming {second.Name}");
                        }
                    clone.Aliases!.Remove(second);
                    bm.DroppedPlace = (t.secondPlaceAlias, second.Name!);
                    Console.WriteLine($"  -place   : {second.Name} (alias {t.secondPlaceAlias}) removed: every beat is at the main place, so it would draw a second POI for nothing");
                }
                return 0;
            }

            // --- 8. message boxes ---------------------------------------------------------------------
            internal int Messages()
            {
                RemovePrevious(myMod.Messages, e => e.StartsWith(r.id + "_msg"));
                msgOf = new Dictionary<int, FormKey>();
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

                offerMsg = default;
                if (r.offer?.message != null)
                {
                    if (msgSrc == null) { Console.WriteLine($"REFUSED: no message template '{MessageTemplate}' in {t.mod}."); return 1; }
                    var om = myMod.Messages.DuplicateInAsNewRecord(msgSrc);
                    om.EditorID = $"{r.id}_msgOffer";
                    om.Name = Expand(r.offer.message.title!, t);
                    om.Description = Expand(r.offer.message.text!, t);
                    om.OwnerQuest.SetTo(clone.FormKey);
                    offerMsg = om.FormKey;
                    Console.WriteLine($"  +message : {om.EditorID} {om.FormKey}  \"{om.Name}\"  (the offer)");
                }

                // --- the people at the endings: named NPCs cloned from a friendly vanilla template -------------
                // NPCTools.CloneNPC copies the body field by field and NOT the name or the voice, so both are
                // written here; unaggressive, average confidence, dressed from the recipe (his 2026-10-08 asks).
                npcOf = new Dictionary<int, FormKey>();
                RemovePrevious(myMod.Npcs, e => e.StartsWith(r.id + "_person"));
                foreach (var (b, pr) in inputs.Persons)
                {
                    var pp = r.beats[b].person!;
                    var npc = Retrograde.Utils.NPCTools.CloneNPC(myMod, pr.tmpl.DeepCopy());
                    npc.EditorID = $"{r.id}_person{b + 1}";
                    npc.Name = pp.name!;
                    npc.Voice.SetTo(pr.voice ?? pr.tmpl.Voice.FormKey);
                    npc.Aggression = Npc.AggressionType.Unaggressive;
                    npc.Confidence = Npc.ConfidenceType.Average;
                    // ⛔ Not a New Atlantis MAST worker (his 2026-10-19, off "default ones with different voice actors and its
                    // jarring"). The clone copied the template's factions (NewAtlantisFaction, NewAtlantisMASTWorkerFaction,
                    // CrimeFactionUC), and vanilla's ambient greetings gate on faction, so a vanilla actor greeted him on every
                    // planet, and harming him was a UC crime anywhere. He belongs to no faction and no crime faction.
                    npc.Factions.Clear();
                    npc.CrimeFaction.Clear();
                    if (pr.outfit is FormKey ofk) npc.DefaultOutfit.SetTo(ofk);
                    myMod.Npcs.Add(npc);
                    npcOf[b] = npc.FormKey;
                    bm.People[b] = (npc.FormKey, pp.name!);
                    Console.WriteLine($"  +npc     : {npc.EditorID} {npc.FormKey}  \"{pp.name}\"  (clone of {pr.tmpl.EditorID})" + (pp.outfit != null ? $"  outfit {pp.outfit}" : ""));
                }

                // --- the speaker: an NPC that is never placed, carrying the subtitle's NAME and the VOICE TYPE whose
                // folder the audio is looked up in (gen_dlgtest's shape: clone, then set Voice) ----------------------
                RemovePrevious(myMod.Npcs, e => e == r.id + "_speaker");
                if (inputs.Speaker is (INpcGetter stmpl, FormKey svoice))
                {
                    var sp = Retrograde.Utils.NPCTools.CloneNPC(myMod, stmpl.DeepCopy());
                    sp.EditorID = r.id + "_speaker";
                    sp.Name = r.speaker!.name!;
                    sp.Voice.SetTo(svoice);
                    myMod.Npcs.Add(sp);
                    bm.Speaker = sp.FormKey;
                    Console.WriteLine($"  +speaker : {sp.EditorID} {sp.FormKey}  \"{sp.Name}\"  voice {r.speaker.voice} (clone of {stmpl.EditorID}, never placed)");
                }

                // The kinds' dialogue, once every person is cloned (a talk's conversation).
                for (int b = 0; b < r.beats.Count; b++)
                    KindOf(r.beats[b])?.Dialogue(this, b);
                return 0;
            }

            // --- 9. the events: one stock hook per beat --------------------------------------------------
            internal int Hooks()
            {
                recoversHooked = 0;
                for (int k = 0; k < steps.Count; k++)
                {
                    int prereq = k == 0 ? t.entryStage : steps[k - 1].DoneStage;
                    foreach (var b in steps[k].Beats)
                    {
                        if (KindOrThrow(r.beats[b], b).Hook(this, b, prereq) != 0) return 1;
                    }
                }

                // An ending's person: a stock distance hook on the ending's OWN object, measuring to the player,
                // armed only once the choice is open (PrereqStage = the stage that enters it), as the choice
                // driver registered its distance events at the find.
                foreach (var (b, pst) in personStage)
                {
                    int own = slotOf[b].activatorAlias;
                    int enter = steps[steps.FindIndex(s => s.Beats.Contains(b)) - 1].DoneStage;
                    if (AttachHook(vma, clone.FormKey, own, DistanceHook(clone.FormKey, t.playerAlias, pst, enter), HookEntry.MustExist, false, bm, pst, enter) == Hooked.NoEntry)
                    { Console.WriteLine($"REFUSED: ending alias {own} carries no hook entry to add the approach to."); return 1; }
                    bm.PersonApproach.Add((own, t.playerAlias));
                    Console.WriteLine($"  +hook    : beat {b + 1}'s person, within {ApproachDistance} of its object (alias {own}) after {enter}, sets {pst}");
                }

                // A nest: a stock distance hook on the object it surrounds, measuring to the player, as a person's is on
                // its ending: 250 m out, so they are there before he is. At any stage on a slate (no PrereqStage); on a
                // board only after the accept (entryStage), because a board quest runs from the refresh and its site
                // exists before anyone has taken the job.
                int whoPrereq = t.entryStage > 0 ? t.entryStage : -1;
                foreach (var (p, w, a) in whos)
                {
                    int own = slotOf[a].activatorAlias;
                    var sc = DistanceHook(clone.FormKey, t.playerAlias, whoStage[p], whoPrereq);
                    if (AttachHook(vma, clone.FormKey, own, sc, HookEntry.FindOrCreate, true, bm, whoStage[p], whoPrereq) == Hooked.AlreadyCarries)
                    { Console.WriteLine($"REFUSED: alias {own} already carries {sc.Name}; the {w.kind} at {PlaceKey[p]} cannot hook it twice."); return 1; }
                    Console.WriteLine($"  +hook    : {PlaceKey[p]} is {w.kind}: within {ApproachDistance} of beat {a + 1}'s object (alias {own}) sets {whoStage[p]}"
                                      + (whoPrereq >= 0 ? $" after {whoPrereq}" : ""));
                }

                // Arrival (board): a stock distance hook on step one's MARKER, measuring to the player, armed only once the
                // board's accept has set entryStage, so the poster's call comes when he reaches the site and never on the
                // accept (his 2026-10-14: "you should get the radio message when you are near the POI").
                if (arrival)
                {
                    int at = slotOf[steps[0].Beats[0]].markerAlias;
                    var sc = DistanceHook(clone.FormKey, t.playerAlias, t.arrivalStage, t.entryStage);
                    if (AttachHook(vma, clone.FormKey, at, sc, HookEntry.FindOrCreate, true, bm, t.arrivalStage, t.entryStage) == Hooked.AlreadyCarries)
                    { Console.WriteLine($"REFUSED: alias {at} already carries {sc.Name}; the arrival cannot hook it twice."); return 1; }
                    bm.Arrival = (at, t.playerAlias);
                    Console.WriteLine($"  +hook    : arrival within {ApproachDistance} of step one's marker (alias {at}) sets {t.arrivalStage} after {t.entryStage}; the broadcast plays there");
                }

                // Ending on a talk: the person's unload sets the shutdown stage, armed once the quest is complete. On a board,
                // MissionComplete() would shut the quest down itself (MissionQuestScript.ShutdownOnCompletion, default true),
                // so it is turned off and the shutdown stage calls MissionShutdown() instead.
                if (bm.Linger >= 0)
                {
                    int done = steps[^1].DoneStage;
                    foreach (var b in steps[^1].Beats.Where(b => r.beats[b].type == "talk"))
                    {
                        int pa = (int)talkAlias[b].ID;
                        var sc = StageHook("DefaultAliasOnUnload", bm.Linger, done);
                        if (AttachHook(vma, clone.FormKey, pa, sc, HookEntry.FindOrCreate, true, bm, bm.Linger, done) == Hooked.AlreadyCarries)
                        { Console.WriteLine($"REFUSED: alias {pa} already carries DefaultAliasOnUnload."); return 1; }
                        Console.WriteLine($"  +hook    : beat {b + 1}'s person (alias {pa}) unloading after {done} sets {bm.Linger}, which stops the quest; until then she can still be talked to");
                    }
                    if (t.questScript != null)
                    {
                        var qs = vma.Scripts.First(s => s.Name == t.questScript);
                        qs.Properties.RemoveAll(p => p.Name == "ShutdownOnCompletion");
                        qs.Properties.Add(new ScriptBoolProperty { Name = "ShutdownOnCompletion", Data = false, Flags = ScriptProperty.Flag.Edited });
                        Console.WriteLine($"  linger   : {t.questScript}.ShutdownOnCompletion false; stage {bm.Linger} calls MissionShutdown()");
                    }
                }

                // The approach: a stock distance hook on the PLAYER, once, at any stage (no PrereqStage).
                if (r.approach is Approach apr)
                {
                    int toAlias = slotOf[apr.to - 1].activatorAlias;
                    var sc = DistanceHook(clone.FormKey, toAlias, t.approachStage, -1);
                    AttachHook(vma, clone.FormKey, t.playerAlias, sc, HookEntry.FindOrCreate, false, bm, t.approachStage, -1);
                    bm.ApproachTarget = (t.playerAlias, toAlias);
                    Console.WriteLine($"  +hook    : approach within {ApproachDistance} of beat {apr.to}'s object (alias {toAlias}) sets {t.approachStage}");
                }
                return 0;
            }

            // --- 10. the fragment script: generated, bound per stage -----------------------------------------
            internal int Fragment()
            {
                string name = FragmentScriptName(r);
                props = new List<(string type, string pname, FormKey key, int alias)>();   // alias -1 = a plain form
                code = new SortedDictionary<int, List<string>>();

                foreach (var kv in counterOf)
                {
                    string p = $"Counter{kv.Key + 1}";
                    props.Add(("GlobalVariable", p, kv.Value.key, -1));
                    Add(t.entryStage, $"{p}.SetValue(0)");
                    Add(t.entryStage, $"UpdateCurrentInstanceGlobal({p})");
                }
                // "replace": the beat's marker is a placed object (a box), so it goes when the quest starts and
                // the beat's own object stands there instead. His shape: GetRef().Disable(False), never re-enabled.
                for (int b = 0; b < r.beats.Count; b++)
                {
                    if (!r.beats[b].replace) continue;
                    string an = $"Alias_Beat{b + 1}Marker";
                    props.Add(("ReferenceAlias", an, clone.FormKey, slotOf[b].markerAlias));
                    bm.Replaced.Add((an, slotOf[b].markerAlias));
                    Add(t.entryStage, $"{an}.GetRef().Disable(False)");
                }
                tails = new Dictionary<int, List<string>>();
                // Entering a step: what the beats spawn appears (and fills the alias the objective targets) BEFORE the
                // objective is shown, so it never shows with nothing to point at.
                void Enter(int k, int stage)
                {
                    // The kinds' entry passes, in EnterRank order: 0 a hold's first wave, 1 the fights (defeat and clear
                    // together, in beat order), 2 the recover holders.
                    for (int rank = 0; rank <= 2; rank++)
                        foreach (var b in steps[k].Beats.Where(b => KindOf(r.beats[b])?.EnterRank == rank))
                            KindOf(r.beats[b])!.Enter(this, b, stage);
                    if (steps[k].IsChoose)
                    {
                        if (!offerMsg.IsNull) { props.Add(("Message", "OfferMessage", offerMsg, -1)); Add(stage, "OfferMessage.Show()"); }
                        foreach (var b in steps[k].Beats) Add(stage, $"SetObjectiveDisplayed({ObjOf(steps[k], b)})");
                    }
                    else Add(stage, $"SetObjectiveDisplayed({steps[k].Objective})");
                }
                Enter(0, t.entryStage);
                foreach (var (b, pst) in personStage)
                {
                    string ta = $"Alias_Beat{b + 1}Target", pn = $"Person{b + 1}";
                    props.Add(("ReferenceAlias", ta, clone.FormKey, slotOf[b].activatorAlias));
                    props.Add(("ActorBase", pn, npcOf[b], -1));
                    Add(pst, $"duo_delve_lib.PlacePerson({ta}, {pn}, 1.5)");
                    var pc = r.beats[b].person!.company;
                    if (pc != null && inputs.Persons[b].company is FormKey cfk)
                    {
                        props.Add(("FormList", $"Company{b + 1}", cfk, -1));
                        Add(pst, $"duo_delve_lib.PlaceCompany({ta}, Company{b + 1}, {pc.min}, {pc.max}, 8.0)");
                    }
                }
                foreach (var (p, w, a) in whos)
                {
                    string ta = $"Alias_Beat{a + 1}Target", nl2 = $"Who{p + 1}";
                    props.Add(("ReferenceAlias", ta, clone.FormKey, slotOf[a].activatorAlias));
                    props.Add(("FormList", nl2, inputs.WhoLists[p], -1));
                    // One placer per kind, all in duo_delve_lib: a nest is one species and attacks, workers are a
                    // friendly mix stood round the object, spacers a hostile mix that holds it.
                    string call = w.kind switch
                    {
                        "infested" => $"duo_delve_lib.PlaceNest({ta}, {nl2}, {w.min}, {w.max}, {NestRadius:0.0})",
                        "workers"  => $"duo_delve_lib.PlaceCompany({ta}, {nl2}, {w.min}, {w.max}, {WorkersRadius:0.0})",
                        "spacers"  => $"duo_delve_lib.PlaceCrew({ta}, {nl2}, {w.min}, {w.max}, {NestRadius:0.0})",
                        _ => throw new InvalidOperationException($"who.kind '{w.kind}' passed the lint and has no placer")
                    };
                    Add(whoStage[p], call);
                }
                if (r.approach is Approach af)
                {
                    // Civilians first, then the crate, as the driver did.
                    string ta = $"Alias_Beat{af.to}Target";
                    props.Add(("ReferenceAlias", ta, clone.FormKey, slotOf[af.to - 1].activatorAlias));
                    if (af.civilians)
                    {
                        props.Add(("FormList", "Civilians", civsKey!.Value, -1));
                        Add(t.approachStage, $"duo_delve_lib.PlaceCivilians({ta}, Civilians, 1, 5)");
                    }
                    if (af.lose is int l)
                    {
                        string la = $"Alias_Beat{l}Target";
                        props.Add(("ReferenceAlias", la, clone.FormKey, slotOf[l - 1].activatorAlias));
                        props.Add(("FormList", "Gang", gangKey!.Value, -1));
                        Add(t.approachStage, $"If !GetStageDone({beatStage[l - 1]})   ; lose the crate only while it is still there");
                        Add(t.approachStage, $"    duo_delve_lib.LoseTheLoad({la}, {ta}, Gang.GetAt(0))");
                        Add(t.approachStage, "EndIf");
                    }
                }
                // Speech: each line is a RADIO scene (no actor in the world: AliasID -4, the vanilla audio-log shape,
                // FrankyCLI docs/formlib/book_audio.md) holding one topic and one response the speaker says, started
                // by the fragment of the stage it belongs to. The response's WEMFile is the topic's own id, and
                // the game looks the audio up as Sound\Voice\<plugin>\<voice type>\<that id, 8 hex>.wem.
                var spokenAt = new List<(int stage, int n)>();   // fragment-started lines, by stage
                if (inputs.Speaker != null)
                {
                    var lines = new List<(int stage, List<string> text)>();
                    if (r.broadcast != null) lines.Add((arrival ? t.arrivalStage : t.entryStage, r.broadcast));   // on start (0) the engine plays it; on the board's accept, the fragment
                    for (int b = 0; b < r.beats.Count; b++) if (r.beats[b].say != null) lines.Add((beatStage[b], r.beats[b].say!));
                    // A group ending's closing call: on the completing stage, which every member's last fix sets whatever the order.
                    if (r.recapSay != null) lines.Add((steps[^1].DoneStage, r.recapSay));
                    // The offer's line plays on the stage that opens the choice as a TAIL, after every other line of that
                    // stage, so it starts once the find's box is closed (Message.Show waits). Added like a beat's line it
                    // landed BEFORE the box (the speech lines are emitted ahead of the boxes), so the radio played under
                    // an open box: the clash his "either or" exists for. Caught reading 06b's fragment, 2026-10-12.
                    int offerLine = -1;
                    if (r.offer?.say != null)
                    {
                        var cs = steps.First(s => s.IsChoose);
                        offerLine = lines.Count;
                        lines.Add((steps[steps.IndexOf(cs) - 1].DoneStage, r.offer.say));
                    }
                    for (int n = 0; n < lines.Count; n++)
                    {
                        var (lst, ltext) = lines[n];
                        string tag = $"{r.id}_say{n + 1}";
                        var topic = new DialogTopic(myMod)
                        {
                            EditorID = tag + "_topic",
                            Category = DialogTopic.CategoryEnum.Scene,
                            Subtype = DialogTopic.SubtypeEnum.CustomScene,
                            SubtypeName = DialogTopic.SubtypeNameEnum.CustomScene,
                        };
                        topic.Quest.SetTo(clone.FormKey);
                        var info = new DialogResponses(myMod) { EditorID = tag + "_info", SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low };
                        info.Speaker.SetTo(bm.Speaker);
                        var said = AddResponses(myMod, info, ltext, topic.FormKey.ID, true);
                        topic.Responses.Add(info);
                        // TPIC: the topic's own list of its infos. Missing, the CK crashes on a click (book_audio.md).
                        topic.TopicInfoList = new Noggog.ExtendedList<IFormLinkGetter<IDialogResponsesGetter>> { info.ToLink<IDialogResponsesGetter>() };
                        clone.DialogTopics.Add(topic);

                        var action = new RadioSceneAction { Name = "Say", AliasID = -4, Index = 0, StartPhase = 0, EndPhase = 0 };
                        action.Topic.SetTo(topic.FormKey);
                        // ⛔ THE BROADCAST CANNOT BE STARTED FROM STAGE 0's FRAGMENT: that runs while the quest is still
                        // starting, and Start() then does nothing at all. Measured in his play, 2026-10-09, with an
                        // on-screen probe: stage 0 "playing = FALSE", the same scene started again at stage 10 played,
                        // voice and subtitle. So a stage-0 line is started by the ENGINE, with the scene's own
                        // BeginOnQuestStart flag, and every later stage's line by its fragment.
                        bool atStart = lst == 0;
                        var scene = new Scene(myMod)
                        {
                            EditorID = tag + "_scene",
                            // 0x80 is on every vanilla audio-log scene; undocumented (book_audio.md)
                            Flags = (Scene.Flag)0x80 | (atStart ? Scene.Flag.BeginOnQuestStart : 0),
                            VNAM = new byte[] { 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0 },
                        };
                        scene.Quest.SetTo(clone.FormKey);
                        scene.Actors.Add(new SceneActor { ID = unchecked((uint)-4), Flags = SceneActor.Flag.NoCommandState, BehaviorFlags = 0 });
                        scene.Phases.Add(new ScenePhase { Name = "SayPhase", EditorWidth = 500 });
                        scene.Actions = new Noggog.ExtendedList<ASceneAction> { action };
                        clone.Scenes.Add(scene);

                        string sv = $"Say{n + 1}";
                        if (!atStart)
                        {
                            props.Add(("Scene", sv, scene.FormKey, -1));
                            if (n == offerLine) Tail(lst, $"{sv}.Start()   ; the offer's call, after the find's box is closed");
                            else Add(lst, $"{sv}.Start()");
                            spokenAt.Add((lst, n + 1));
                        }
                        foreach (var (wem, text) in said)
                        {
                            bm.Lines.Add((lst, wem, text, scene.FormKey));
                            Console.WriteLine($"  +speech  : {(atStart ? "on quest start (BeginOnQuestStart)" : $"stage {lst}")} plays {scene.EditorID} -> {wem:X8}.wem  \"{text}\"");
                        }
                    }
                }
                // A quest's scenes stop with it, so a line on the stage that ENDS the quest would be cut off by the
                // Stop() after it ("Link is online", 2026-10-09). There, wait for the line to finish first: a second
                // for the scene to begin, then while it plays, capped at 30 s so a scene that never reports done
                // cannot hold the quest open.
                // Ending on a talk, the quest stops on the shutdown stage (set when the person unloads), never on the
                // completing one, so the conversation can go on after it (his 2026-10-19). A 10-19 morning version waited
                // on IsInDialogueWithPlayer instead, and his play of 15a ended the talk the moment a side question was
                // picked: the reading went false between topics and the quest stopped under him.
                bool Lingers(int st) => bm.Linger >= 0 && st == steps[^1].DoneStage;
                void StopQuest(int st)
                {
                    if (Lingers(st)) { Add(bm.Linger, "Stop()   ; the person unloaded after the completion (DefaultAliasOnUnload)"); return; }
                    WaitForLines(st);
                    Add(st, "Stop()");
                }
                // A board mission ends through the script the board talks to: MissionComplete() pays through MissionParent,
                // completes every objective, sets CompleteStage and shuts the quest down. Vanilla's own stage-100 fragment
                // (QF_MB_Bounty05_UC_003303A5) is exactly this autocast and call.
                void CompleteMission(int st)
                {
                    if (!Lingers(st)) WaitForLines(st);   // lingering, MissionComplete no longer shuts it down (ShutdownOnCompletion false)
                    Add(st, "Quest __temp = Self as Quest");
                    Add(st, $"{t.questScript} kmyQuest = __temp as {t.questScript}");
                    Add(st, "kmyQuest.MissionComplete()");
                    if (Lingers(st))
                    {
                        Add(bm.Linger, "Quest __temp = Self as Quest");
                        Add(bm.Linger, $"{t.questScript} kmyQuest = __temp as {t.questScript}");
                        Add(bm.Linger, "kmyQuest.MissionShutdown()   ; the person unloaded after the completion (DefaultAliasOnUnload)");
                    }
                }
                void WaitForLines(int st)
                {
                    foreach (var (_, n) in spokenAt.Where(x => x.stage == st))
                    {
                        Add(st, $"Utility.Wait(1.0)   ; let Say{n} begin before asking whether it plays");
                        Add(st, $"Int waited{n} = 0");
                        Add(st, $"While Say{n}.IsPlaying() && waited{n} < 30");
                        Add(st, "    Utility.Wait(1.0)");
                        Add(st, $"    waited{n} += 1");
                        Add(st, "EndWhile");
                    }
                }
                var held = new List<int>();   // pickups and recovered items not yet delivered
                int handBack = HandBackTalk(r, steps);
                for (int k = 0; k < steps.Count; k++)
                {
                    var s = steps[k];
                    bool last = k == steps.Count - 1;
                    foreach (var b in s.Beats)
                    {
                        int st = beatStage[b];
                        if (msgOf.ContainsKey(b)) { props.Add(("Message", $"Beat{b + 1}Message", msgOf[b], -1)); Add(st, $"Beat{b + 1}Message.Show()"); }
                        if (Carries(r.beats[b])) held.Add(b);
                        if (r.beats[b].type == "deliver" || b == handBack)
                        {
                            foreach (var h in held)
                            {
                                props.Add(("Form", $"Item{h + 1}", itemOf[h], -1));
                                Add(st, $"Game.GetPlayer().RemoveItem(Item{h + 1}, 1)");
                            }
                            if (b == handBack) bm.HandBack = (st, held.Select(h => $"Game.GetPlayer().RemoveItem(Item{h + 1}, 1)").ToList());
                            if (!s.IsChoose) held.Clear();   // every ending takes the same things
                        }
                        if (s.IsChoose)
                        {
                            // Whichever ending the player walked to: every ending's prompt goes, the others' objectives
                            // are HIDDEN (never failed: neither side is the villain), and the quest ends here.
                            int own = ObjOf(s, b);
                            foreach (var e in s.Beats)
                            {
                                string ea = $"Alias_Beat{e + 1}Target";
                                props.Add(("ReferenceAlias", ea, clone.FormKey, slotOf[e].activatorAlias));
                                Add(st, $"{ea}.GetRef().BlockActivation(True, True)");
                                if (e != b) Add(st, $"SetObjectiveDisplayed({ObjOf(s, e)}, False)");
                            }
                            Add(st, $"SetObjectiveCompleted({own})");
                            Add(st, "CompleteQuest()");
                            StopQuest(st);
                        }
                        KindOf(r.beats[b])?.StageLines(this, k, b);
                        if (s.IsGroup)
                        {
                            Add(st,$"If ModObjectiveGlobal(1.0, Counter{k + 1}, {s.Objective}, {s.Beats.Count}.0)");
                            Add(st, $"    SetStage({s.DoneStage})");
                            Add(st, "EndIf");
                        }
                    }
                    int done = s.DoneStage;
                    if (!s.IsGroup && !s.IsChoose) Add(done, $"SetObjectiveCompleted({s.Objective})");
                    if (last)
                    {
                        if (!s.IsChoose && t.questScript != null) CompleteMission(done);
                        else if (!s.IsChoose) { Add(done, "CompleteQuest()"); StopQuest(done); }
                    }
                    else Enter(k + 1, done);
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
                    if (tails.TryGetValue(kv.Key, out var tail))
                        foreach (var line in tail) psc.AppendLine("    " + line);
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
                    if (p.alias >= 0) op.Alias = (short)p.alias;   // a ReferenceAlias: the quest plus its alias id
                    vma.Script.Properties.Add(op);
                }
                vma.Fragments.Clear();
                foreach (var st in code.Keys)
                    vma.Fragments.Add(new QuestScriptFragment { Stage = (ushort)st, StageIndex = 0, ScriptName = name, FragmentName = FragmentFunction(st) });
                bm.Script = name;
                bm.FragStages = code.Keys.ToList();

                // The reject script's Objectives: every objective the quest now carries, so a turned-down Delve leaves
                // nothing on screen whichever step was showing.
                // (A board mission has no reject script: the board's own handles a turn-down.)
                if (t.questScript == null)
                {
                    var reject = vma.Scripts.FirstOrDefault(s => s.Name == RejectScript);
                    if (reject == null) { Console.WriteLine($"REFUSED: {RejectScript} is not on the quest."); return 1; }
                    var objs = clone.Objectives.Select(o => (int)o.Index).OrderBy(i => i).ToList();
                    if (objs.Count == 0) { Console.WriteLine("REFUSED: the quest has no objectives for the reject script to hide."); return 1; }
                    reject.Properties.Clear();
                    var ol = new ScriptIntListProperty { Name = "Objectives", Flags = ScriptProperty.Flag.Edited };
                    ol.Data.AddRange(objs);
                    reject.Properties.Add(ol);
                    bm.RejectObjectives = objs;
                }

                string dir = Path.Combine(PapyrusDir(), "gen");
                Directory.CreateDirectory(dir);
                bm.PscPath = Path.Combine(dir, name + ".psc");
                File.WriteAllText(bm.PscPath, psc.ToString().Replace("\r\n", "\n"));
                Console.WriteLine($"  fragments: {name} on stages [{string.Join(", ", code.Keys)}], {props.DistinctBy(p => p.pname).Count()} propert(ies); source {bm.PscPath}");
                return 0;
            }
        }

        /// <summary>
        /// The voice cache, in the Overtime repo so it is versioned and backed up with the plugin (his ask,
        /// 2026-10-09: "can we keep the audio files somewhere so we can just rename when needed"). One
        /// file per spoken line, named by a hash of its ElevenLabs voice, voice type and exact words, with
        /// a .txt beside it saying which. A rebuild re-mints every topic id, so the game file's NAME changes
        /// each build; the cache is what makes that a copy rather than a new generation.
        /// </summary>
        private const string VoiceCacheDir = @"C:\modding\DU_Overtime\voicecache";

        // The Creation Kit's lipsync, without the CK (office/projects/bethesda/39-voice-dialogue-and-lipsync.md, proven
        // at his glass 2026-10-12: "LIP SYNC WORKED 100%"). Its own command lines, read out of CreationKit.exe.
        private static readonly string ToolsDir = Path.Combine(Path.GetDirectoryName(SpeechTools.GameVoiceDir.TrimEnd('\\'))!, "..", "..", "Tools");

        /// <summary>
        /// One lip file per voiced talk line, beside its .wem: LipGenerator (the TEXT argument is the spoken words,
        /// measured: a file path is lip-synced as text) to an .animset, a one-animset .ffxcproj (two in one project
        /// collide on their animation group), ffxc -p=x86, then &lt;id&gt;.ffxanim copied in. Radio lines have no face
        /// and get none.
        /// ⛔ NOT CACHED, deliberately: the file carries a 12-byte value at offset 12 that differs with the line id (the
        /// animation is named after the wav, which is named after the id; measured 2026-10-12, two builds of one line
        /// identical but for bytes 12-23). A cached file copied under a NEW id would carry the old one, and whether the
        /// game checks it is unmeasured. The tools are local and take about a second a line, so every build re-makes them.
        /// </summary>
        private static int DeployLips(BeatsMade bm, Func<string, string> dir, List<string> deployed)
        {
            if (bm.TalkLines.Count == 0) return 0;
            string tools = Path.GetFullPath(ToolsDir);
            string lipgen = Path.Combine(tools, "LipGenerator", "LipGenerator.exe"), ffxc = Path.Combine(tools, "FaceFX", "ffxc.exe");
            string analysis = Path.Combine(tools, "FaceFX", "GenesisDefault.facefx");
            foreach (var exe in new[] { lipgen, ffxc, analysis })
                if (!File.Exists(exe)) { Console.WriteLine($"    FAIL     : lipsync needs {exe}, which is not there"); return 1; }
            string work = Path.Combine(VoiceCacheDir, "lipwork");
            int fail = 0;
            foreach (var (wem, text, vt, vid, female) in bm.TalkLines)
            {
                string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes($"{vid}|{vt}|{text}")))[..16].ToLowerInvariant();
                string dest = Path.Combine(dir(vt), $"{wem:X8}.ffxanim");
                string wav = Path.Combine(VoiceCacheDir, key + ".wav");
                if (!File.Exists(wav)) { Console.WriteLine($"    FAIL     : no {wav} to lip-sync {wem:X8} from"); fail++; continue; }
                string actor = Path.Combine(tools, "FaceFX", female ? "StarfieldHumanFemale.facefx" : "StarfieldHumanMale.facefx");
                if (Directory.Exists(work)) Directory.Delete(work, true);
                Directory.CreateDirectory(work);
                string id = $"{wem:X8}";   // the animation is named after the wav, so the wav is named after the line
                File.Copy(wav, Path.Combine(work, id + ".wav"));
                string animset = Path.Combine(work, id + ".animset");
                int c1 = RunTool(lipgen, work, Path.Combine(work, id + ".wav"), text, actor, analysis, $"-OutputFileName:{animset}", $"-AnimationGroupName:{vt}");
                string proj = Path.Combine(work, id + ".ffxcproj");
                File.WriteAllText(proj, string.Join("\r\n", new[] { "version: 10300", "items:", $" - facefx_file: {actor}", "   mount_files:", $"    - {animset}" }) + "\r\n");
                int c2 = c1 != 0 || !File.Exists(animset) ? -1
                       : RunTool(ffxc, work, "-p=x86", "-Wno-empty-bone-pose", "-Wno-unused-track", "-Wno-snap", $"-o={Path.Combine(work, "out")}", proj);
                string built = Path.Combine(work, "out", Path.GetFileNameWithoutExtension(actor), "x86", vt, id + ".ffxanim");
                if (c2 != 0 || !File.Exists(built))
                { Console.WriteLine($"    FAIL     : lipsync for {id} (LipGenerator exit {c1}, ffxc exit {c2}; {work})"); fail++; continue; }
                Console.WriteLine($"    lip      : {id}.ffxanim  ({(female ? "female" : "male")} face)");
                File.Copy(built, dest, true);
                deployed.Add(dest);
                fail += Check($"{wem:X8}.ffxanim sits beside its .wem ({vt})",
                              (File.Exists(dest) && File.Exists(Path.ChangeExtension(dest, ".wem"))).ToString(), "True");
            }
            return fail;
        }

        /// <summary>Runs a tool with each argument passed as its own argument (no shell, no quoting), returns its exit code.</summary>
        private static int RunTool(string exe, string cwd, params string[] args)
        {
            var psi = new System.Diagnostics.ProcessStartInfo(exe) { WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = System.Diagnostics.Process.Start(psi)!;
            p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode;
        }

        /// <summary>
        /// Put each spoken line's audio where the game looks for it: Sound\Voice\&lt;plugin&gt;\&lt;voice type&gt;\
        /// &lt;topic id, 8 hex&gt;.wem. From the cache when the same words in the same voice exist, else generated
        /// (ElevenLabs, then Wwise) and cached. The previous build's files go first, by its manifest.
        /// </summary>
        private static int DeployVoice(Recipe r, Template t, BeatsMade bm)
        {
            // Every voiced line with the voice it is said in: the radio speaker's, and each talk person's own.
            var all = bm.Lines.Select(l => (l.wem, l.text, vt: r.speaker!.voice!, vid: r.speaker.elevenlabs!))
                      .Concat(bm.TalkLines.Select(l => (l.wem, l.text, vt: l.voice, vid: l.elevenlabs))).ToList();
            if (all.Count == 0) return 0;
            Console.WriteLine();
            Console.WriteLine("  voice:");
            string plugin = t.mod + ".esm";
            string Dir(string vt) => Path.Combine(SpeechTools.GameVoiceDir, plugin, vt);
            Directory.CreateDirectory(VoiceCacheDir);
            foreach (var vt in all.Select(l => l.vt).Distinct()) Directory.CreateDirectory(Dir(vt));

            string manifest = Path.Combine(VoiceCacheDir, $"deployed_{r.id}.txt");
            if (File.Exists(manifest))
                foreach (var p in File.ReadAllLines(manifest).Where(p => p.Length > 0 && File.Exists(p)))
                {
                    File.Delete(p);
                    Console.WriteLine($"    -old     : {p}");
                }

            int fail = 0;
            var deployed = new List<string>();
            var toMake = new List<(uint wem, string key, string text, string vt, string vid)>();
            foreach (var (wem, text, vt, vid) in all)
            {
                string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes($"{vid}|{vt}|{text}")))[..16].ToLowerInvariant();
                string cached = Path.Combine(VoiceCacheDir, key + ".wem");
                string dest = Path.Combine(Dir(vt), $"{wem:X8}.wem");
                if (File.Exists(cached))
                {
                    File.Copy(cached, dest, true);
                    deployed.Add(dest);
                    Console.WriteLine($"    cached   : {key}.wem -> {wem:X8}.wem  \"{text}\"");
                }
                else toMake.Add((wem, key, text, vt, vid));
            }
            if (toMake.Count > 0)
            {
                SpeechTools.generateWavs = true;
                var mk = ModKey.FromNameAndExtension(plugin);
                foreach (var (wem, _, text, vt, vid) in toMake) SpeechTools.GenerateWavs(wem, vt, mk, text, vid);
                SpeechTools.GenerateAllWavs();
                SpeechTools.ConvertAndDeploy();
                foreach (var (wem, key, text, vt, vid) in toMake)
                {
                    string dest = Path.Combine(Dir(vt), $"{wem:X8}.wem");
                    // SpeechTools also writes the .wav beside it and an .esp-named twin; all of it is this build's.
                    string espDir = Path.Combine(SpeechTools.GameVoiceDir, t.mod + ".esp", vt);
                    foreach (var p in new[] { dest, Path.ChangeExtension(dest, ".wav"),
                                              Path.Combine(espDir, $"{wem:X8}.wem"), Path.Combine(espDir, $"{wem:X8}.wav") })
                        if (File.Exists(p)) deployed.Add(p);
                    if (!File.Exists(dest)) { Console.WriteLine($"    FAIL     : no {dest} after generation (Wwise prints its own error above)"); fail++; continue; }
                    if (SpeechTools.UsedSapiFallback)
                    {
                        Console.WriteLine($"    NOT CACHED: {wem:X8}.wem is the Windows fallback voice, not ElevenLabs; it plays, and the next build tries again");
                        continue;
                    }
                    File.Copy(dest, Path.Combine(VoiceCacheDir, key + ".wem"), true);
                    string wav = Path.Combine(SpeechTools.AudioStagingDir, plugin, vt, $"{wem:X8}.wav");
                    if (File.Exists(wav)) File.Copy(wav, Path.Combine(VoiceCacheDir, key + ".wav"), true);
                    File.WriteAllText(Path.Combine(VoiceCacheDir, key + ".txt"), $"elevenlabs {vid}\nvoice {vt}\n{text}\n");
                    Console.WriteLine($"    new      : {wem:X8}.wem, cached as {key}  \"{text}\"");
                }
            }
            fail += DeployLips(bm, Dir, deployed);
            File.WriteAllLines(manifest, deployed);
            foreach (var (wem, _, vt, _) in all)
                fail += Check($"{wem:X8}.wem is where the game looks ({vt})", File.Exists(Path.Combine(Dir(vt), $"{wem:X8}.wem")).ToString(), "True");
            Console.WriteLine(fail == 0 ? $"  VOICE DEPLOYED: {all.Count} line(s) under {Path.Combine(SpeechTools.GameVoiceDir, plugin)}" : $"  {fail} VOICE CHECK(S) FAILED.");
            return fail == 0 ? 0 : 1;
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
            // A clone drives ITSELF: nothing in it, script or condition, may still point at the quest it was copied from.
            var baseKey = m.Quests.FirstOrDefault(x => x.EditorID == t.@base)?.FormKey;
            fail += Check($"nothing in the quest points at its base {t.@base}",
                          baseKey == null ? "base not found" : q.EnumerateFormLinks().Count(l => l.FormKey == baseKey.Value).ToString(), "0");
            // Each REQUESTED place reads back as a PCM request (a remote main place, and a remote second one).
            foreach (var (on, label) in new[] { (r.place.remote == true, t.placeAlias), (r.place.second?.remote == true, t.secondPlaceAlias) })
            {
                if (!on) continue;
                var pl = q.Aliases.OfType<IQuestLocationAliasGetter>().FirstOrDefault(a => a.ID == (uint)label);
                fail += Check($"place alias {label} is a PCM request (a new site, placed by the planet content manager)",
                              pl == null ? "missing" : (pl.ALPS != null).ToString(), "True");
            }
            // EVERY alias link resolves: nothing fills from, is created at, or parents to an alias the quest does not have. A
            // renumbering (the board's borrowed slots) or a removal (an unused slot, a dropped place) that left one dangling
            // fails here rather than in his game.
            var ids = new HashSet<uint>(q.Aliases.OfType<IQuestReferenceAliasGetter>().Select(a => a.ID)
                .Concat(q.Aliases.OfType<IQuestLocationAliasGetter>().Select(a => a.ID))
                .Concat(q.Aliases.OfType<IQuestCollectionAliasGetter>().SelectMany(c => c.Collection.Where(e => e.ReferenceAlias != null).Select(e => e.ReferenceAlias!.ID))));
            var dangling = q.Aliases.OfType<IQuestReferenceAliasGetter>()
                .SelectMany(a => new[] { (a.ID, "fills from", a.Location?.AliasID), (a.ID, "is created at", (int?)a.CreateReferenceToObject?.AliasID) })
                .Where(x => x.Item3 is int v && v >= 0 && !ids.Contains((uint)v))
                .Select(x => $"alias {x.Item1} {x.Item2} {x.Item3}")
                .Concat(q.Aliases.OfType<IQuestLocationAliasGetter>()
                    .Where(a => a.ParentSystemLocationAliasID is int v && v >= 0 && !ids.Contains((uint)v))
                    .Select(a => $"location alias {a.ID} parents to {a.ParentSystemLocationAliasID}"))
                .ToList();
            fail += Check("every alias link resolves to an alias the quest has", dangling.Count == 0 ? "all resolve" : string.Join("; ", dangling), "all resolve");
            // No two aliases CONTEND for one reference: those filling from the same place and the same ref type pick the same
            // ref where the site has one (a map marker always), and a later one that neither allows reuse nor is Optional
            // cannot fill, so the quest never starts (missionboard02a's PrimaryRef against MapMarker, 2026-10-14).
            var contend = q.Aliases.OfType<IQuestReferenceAliasGetter>()
                .Where(a => a.Location != null && a.CreateReferenceToObject == null)
                .GroupBy(a => (a.Location!.AliasID, a.Location.RefType.FormKey))
                .Where(g => g.Count() > 1)
                .SelectMany(g => g.Skip(1).Where(a => !(a.Flags ?? 0).HasFlag(QuestReferenceAlias.Flag.AllowReuseInQuest) && !(a.Flags ?? 0).HasFlag(QuestReferenceAlias.Flag.Optional))
                                  .Select(a => $"alias {a.ID} {a.Name} wants alias {g.First().ID} {g.First().Name}'s reference"))
                .ToList();
            fail += Check("no two aliases contend for one reference", contend.Count == 0 ? "none" : string.Join("; ", contend), "none");
            if (bm.SecondPlanet is int spId)
            {
                var sp = q.Aliases.OfType<IQuestLocationAliasGetter>().FirstOrDefault(a => a.ID == (uint)spId);
                fail += Check($"the second planet (alias {spId}) is the planet of the second place",
                              sp == null ? "missing" : $"parent {sp.ParentSystemLocationAliasID}", $"parent {t.secondPlaceAlias}");
            }
            if (bm.MarkerSourceAtMap)
            {
                var ms = q.Aliases.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.ID == (uint)t.markerSource);
                fail += Check($"alias {t.markerSource} took no fight and fills from the place's map marker",
                              ms?.Location == null ? "missing" : $"{ms.Location.AliasID} {ms.Location.RefType.FormKey}", $"{t.placeAlias} 02271F:Starfield.esm");
            }
            if (r.place.second?.remote == true)
            {
                var sc2 = q.Aliases.OfType<IQuestLocationAliasGetter>().FirstOrDefault(a => a.ID == (uint)t.secondPlaceAlias)?.Conditions;
                // ⛔ Two planet guards crashed the game on start (10a, 12a); his 2026-10-19 ruling is none, ever.
                fail += Check("the second request carries no planet guard",
                              sc2 == null ? "missing" : sc2.Any(c => c.Data is IIsInSameCurrentLocationAsAliasConditionDataGetter).ToString(), "False");
                fail += Check("the second request carries no drive leash to the first (the planet content manager places it, not a drive)",
                              sc2 == null ? "missing" : sc2.Any(c => c.Data is IGetDistanceConditionDataGetter g && g.UseAliases).ToString(), "False");
            }
            if (t.questScript == null)
            {
                // No driver: the only quest-level script is the reject handler, carrying every objective.
                fail += Check("quest-level scripts (no driver, the reject handler only)", string.Join(",", vma.Scripts.Select(s => s.Name)), RejectScript);
                var rj = vma.Scripts.FirstOrDefault(s => s.Name == RejectScript)?.Properties.OfType<IScriptIntListPropertyGetter>()
                    .FirstOrDefault(p => p.Name == "Objectives");
                fail += Check("the reject handler hides every objective",
                              rj == null ? "no Objectives" : string.Join(",", rj.Data),
                              string.Join(",", q.Objectives.Select(o => (int)o.Index).OrderBy(i => i)));
            }
            else
            {
                // A board mission: the script the board talks to, alone, with exactly the driver's board properties.
                fail += Check($"quest-level scripts (no driver, {t.questScript} only)", string.Join(",", vma.Scripts.Select(s => s.Name)), t.questScript);
                var qs = vma.Scripts.FirstOrDefault(s => s.Name == t.questScript);
                // Ending on a talk adds exactly one: ShutdownOnCompletion, off (graded below with the linger).
                var keeps = t.questScriptKeeps.Concat(bm.Linger >= 0 ? new[] { "ShutdownOnCompletion" } : Array.Empty<string>());
                fail += Check($"{t.questScript} carries the driver's board properties" + (bm.Linger >= 0 ? " and ShutdownOnCompletion" : ""),
                              string.Join(",", (qs?.Properties.Select(p => p.Name) ?? Enumerable.Empty<string>()).OrderBy(n => n)),
                              string.Join(",", keeps.OrderBy(n => n)));
                foreach (var pn in new[] { "MissionParent", "PrimaryRef", "TargetLocation" })
                {
                    var op = qs?.Properties.OfType<IScriptObjectPropertyGetter>().FirstOrDefault(p => p.Name == pn);
                    fail += Check($"{t.questScript}.{pn} is set", (op != null && !op.Object.IsNull).ToString(), "True");
                }
                var pr = qs?.Properties.OfType<IScriptObjectPropertyGetter>().FirstOrDefault(p => p.Name == "PrimaryRef");
                fail += Check($"{t.questScript}.PrimaryRef names this quest's alias {t.markerSource}",
                              pr == null ? "missing" : $"{pr.Object.FormKey} alias {pr.Alias}", $"{q.FormKey} alias {t.markerSource}");
                var pa = q.Aliases.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.ID == (uint)t.markerSource);
                fail += Check($"alias {t.markerSource} stands, filled from the place (the system and planet resolve from it)",
                              pa?.Location == null ? "missing" : $"from alias {pa.Location.AliasID}", $"from alias {t.placeAlias}");
                fail += Check($"alias {t.markerSource} carries none of the base's hooks",
                              vma.Aliases.Any(a => a.Property.Alias == t.markerSource && a.Scripts.Any(s => !bm.Hooks.Any(h => h.alias == t.markerSource && h.script == s.Name))).ToString(), "False");
                foreach (int da in bm.DroppedDriverAliases)
                    fail += Check($"driver alias {da} is gone", q.Aliases.OfType<IQuestReferenceAliasGetter>().Any(a => a.ID == (uint)da) ? "PRESENT" : "gone", "gone");
                fail += Check("the board's card is the recipe's", q.MissionBoardDescription?.String ?? "none", Expand(r.prose.card!, t));
                // The pay and the shutdown are in the generated SOURCE: a fragment the compiler has not seen yet.
                string src = File.ReadAllText(bm.PscPath);
                fail += Check($"stage {t.completeStage} pays through {t.questScript}.MissionComplete()",
                              src.Contains($"Function {FragmentFunction(t.completeStage)}()") && src.Contains("kmyQuest.MissionComplete()")
                              && !src.Contains("CompleteQuest()") && !src.Contains("    Stop()") ? "True" : "False", "True");
            }
            fail += Check("fragment script", vma.Script?.Name ?? "none", bm.Script);
            fail += Check("fragment stages", string.Join(",", vma.Fragments.Select(f => f.Stage).OrderBy(x => x)), string.Join(",", bm.FragStages));
            foreach (var f in vma.Fragments)
                fail += Check($"stage {f.Stage} fragment", $"{f.ScriptName}.{f.FragmentName}", $"{bm.Script}.{FragmentFunction(f.Stage)}");
            if (t.entryStage == 0)
                fail += Check("stage 0 runs on start",
                              q.Stages.Any(s => s.Index == 0 && s.Flags.HasFlag(QuestStage.Flag.RunOnStart)).ToString(), "True");
            else
            {
                // Nothing of the mission may happen at the board refresh: no stage runs on start, and step one's
                // spawn and objective live on the entry stage's fragment.
                fail += Check("no stage runs on start (the quest starts at the board refresh)",
                              q.Stages.Any(s => s.Flags.HasFlag(QuestStage.Flag.RunOnStart)).ToString(), "False");
                fail += Check($"no fragment on stages below the entry stage {t.entryStage}",
                              string.Join(",", vma.Fragments.Where(f => f.Stage < t.entryStage).Select(f => f.Stage)), "");
                fail += Check($"step one begins on entry stage {t.entryStage}",
                              bm.FragStages.Contains(t.entryStage).ToString(), "True");
            }
            foreach (var h in bm.Hooks)
            {
                var sc = vma.Aliases.FirstOrDefault(a => a.Property.Alias == h.alias)?.Scripts.FirstOrDefault(s => s.Name == h.script);
                int? I(string n) => sc?.Properties.OfType<IScriptIntPropertyGetter>().FirstOrDefault(p => p.Name == n)?.Data;
                fail += Check($"alias {h.alias} {h.script}", $"sets {I("StageToSet")} after {I("PrereqStage")}", $"sets {h.set} after {h.prereq}");
                if (h.script == "DefaultCollectionAliasOnDeath")
                    fail += Check($"alias {h.alias} drops its dead (the marker shows only the living)",
                                  (sc?.Properties.OfType<IScriptBoolPropertyGetter>().FirstOrDefault(p => p.Name == "RemoveFromRefCollection")?.Data ?? false).ToString(), "True");
            }
            foreach (var g in bm.Globals)
                fail += Check($"counter {g} is a text-display global", (q.TextDisplayGlobals?.Any(x => x.FormKey == g) ?? false).ToString(), "True");
            foreach (var (place, anchor, global) in bm.Leashes)
            {
                var la = q.Aliases.OfType<IQuestLocationAliasGetter>().FirstOrDefault(a => a.ID == (uint)place);
                var ok = la?.Conditions.OfType<IConditionGlobalGetter>().Any(c => c.Data is IGetDistanceConditionDataGetter d
                             && d.UseAliases && d.FirstParameter.Index == (uint)anchor && c.ComparisonValue.FormKey == global) ?? false;
                fail += Check($"place alias {place} is leashed within {global} of alias {anchor}", ok.ToString(), "True");
            }
            foreach (var kv in bm.ObjectiveText)
                fail += Check($"objective {kv.Key}", q.Objectives.FirstOrDefault(o => o.Index == kv.Key)?.DisplayText?.String ?? "missing", kv.Value);
            if (bm.Arrival is (int von, int vto))
            {
                var dsc = vma.Aliases.FirstOrDefault(a => a.Property.Alias == von)?.Scripts.FirstOrDefault(x => x.Name == "DefaultAliasOnDistanceLessThan");
                var tp = dsc?.Properties.OfType<IScriptObjectPropertyGetter>().FirstOrDefault(x => x.Name == "TargetAlias");
                var dp = dsc?.Properties.OfType<IScriptFloatPropertyGetter>().FirstOrDefault(x => x.Name == "TargetDistance");
                fail += Check("arrival measures from step one's marker to the player", tp == null ? "missing" : $"{tp.Object.FormKey} alias {tp.Alias} within {dp?.Data}",
                              $"{q.FormKey} alias {vto} within {ApproachDistance}");
                var pl = q.Aliases.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.ID == (uint)vto);
                fail += Check($"alias {vto} is the player", pl == null ? "missing" : pl.UniqueActor.FormKey.ToString(), "000007:Starfield.esm");   // the Player NPC, as the slate base fills it
            }
            if (bm.ApproachTarget is (int aon, int ato))
            {
                var dsc = vma.Aliases.FirstOrDefault(a => a.Property.Alias == aon)?.Scripts.FirstOrDefault(x => x.Name == "DefaultAliasOnDistanceLessThan");
                var tp = dsc?.Properties.OfType<IScriptObjectPropertyGetter>().FirstOrDefault(x => x.Name == "TargetAlias");
                var dp = dsc?.Properties.OfType<IScriptFloatPropertyGetter>().FirstOrDefault(x => x.Name == "TargetDistance");
                fail += Check("approach measures to its object", tp == null ? "missing" : $"{tp.Object.FormKey} alias {tp.Alias} within {dp?.Data}",
                              $"{q.FormKey} alias {ato} within {ApproachDistance}");
            }
            foreach (var (on, to) in bm.PersonApproach)
            {
                var dsc = vma.Aliases.FirstOrDefault(a => a.Property.Alias == on)?.Scripts.FirstOrDefault(x => x.Name == "DefaultAliasOnDistanceLessThan");
                var tp = dsc?.Properties.OfType<IScriptObjectPropertyGetter>().FirstOrDefault(x => x.Name == "TargetAlias");
                fail += Check($"ending alias {on}'s person hook measures to the player", tp == null ? "missing" : $"alias {tp.Alias}", $"alias {to}");
            }
            foreach (var (stg, (cg, xg)) in bm.StageReward)
            {
                var le = q.Stages.FirstOrDefault(x => x.Index == stg)?.LogEntries.FirstOrDefault();
                var rd = le?.StageCompleteDatas.SelectMany(c => c.RewardDatas).FirstOrDefault();
                fail += Check($"stage {stg} completes the quest and pays its tier",
                              $"{le?.Flags?.HasFlag(QuestLogEntry.Flag.CompleteQuest) ?? false} {rd?.BonusCredits.FormKey} {rd?.XpAwarded.FormKey}",
                              $"True {cg} {xg}");
            }
            foreach (var (b, (npc, nm)) in bm.People)
            {
                fail += Check($"beat {b + 1}'s person", m.Npcs.FirstOrDefault(n => n.FormKey == npc)?.Name?.String ?? "missing", nm);
            }
            if (bm.OfferJournal is (int ost, string otx))
                fail += Check($"offer journal on stage {ost}", q.Stages.FirstOrDefault(x => x.Index == ost)?.LogEntries.FirstOrDefault()?.Entry?.String ?? "missing", otx);
            foreach (var h in bm.Holders)
            {
                var ha = q.Aliases.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.ID == (uint)h);
                fail += Check($"holder alias {h} is empty and Optional", ha == null ? "missing"
                              : $"fill {(ha.Location == null && ha.ForcedReference.IsNull && ha.UniqueActor.IsNull ? "none" : "SET")}, optional {ha.Flags?.HasFlag(QuestReferenceAlias.Flag.Optional) ?? false}",
                              "fill none, optional True");
            }
            foreach (var wv in bm.Waves)
            {
                var wa = q.Aliases.OfType<IQuestCollectionAliasGetter>()
                    .Select(c => c.Collection.FirstOrDefault()?.ReferenceAlias).FirstOrDefault(a => a?.ID == (uint)wv);
                fail += Check($"wave collection {wv} is empty and Optional", wa == null ? "missing"
                              : $"fill {(wa.Location == null && wa.ForcedReference.IsNull && wa.UniqueActor.IsNull && wa.CreateReferenceToObject == null ? "none" : "SET")}, optional {wa.Flags?.HasFlag(QuestReferenceAlias.Flag.Optional) ?? false}",
                              "fill none, optional True");
                fail += Check($"wave collection {wv} wears the wave package", wa == null ? "missing"
                              : string.Join(",", wa.PackageData.Select(p => p.FormKey)), bm.WavePackage.ToString());
            }
            foreach (var cg in bm.ClearGroups)
            {
                var ca = q.Aliases.OfType<IQuestCollectionAliasGetter>()
                    .Select(c => c.Collection.FirstOrDefault()?.ReferenceAlias).FirstOrDefault(a => a?.ID == (uint)cg);
                fail += Check($"clear collection {cg} is empty, Optional and wears no package", ca == null ? "missing"
                              : $"fill {(ca.Location == null && ca.ForcedReference.IsNull && ca.UniqueActor.IsNull && ca.CreateReferenceToObject == null ? "none" : "SET")}, optional {ca.Flags?.HasFlag(QuestReferenceAlias.Flag.Optional) ?? false}, packages {ca.PackageData.Count}",
                              "fill none, optional True, packages 0");
            }
            if (bm.Waves.Count > 0)
            {
                var pk = m.Packages.FirstOrDefault(p => p.FormKey == bm.WavePackage);
                var loc = pk?.Data.Values.OfType<IPackageDataLocationGetter>().FirstOrDefault()?.Location as ILocationTargetRadiusGetter;
                string target = (loc?.Target as ILocationTargetGetter)?.Link.FormKey.ToString() ?? "unreadable";
                fail += Check("the wave package travels to PlayerRef, with no script and no bounty-quest condition",
                              pk == null ? "missing" : $"{target} vma {(pk.VirtualMachineAdapter == null ? "none" : "SET")} conditions {string.Join("+", pk.Conditions.Select(c => c.Data.GetType().Name.Replace("BinaryOverlay", "")))}",
                              "000014:Starfield.esm vma none conditions GetDistanceConditionData+GetDistanceConditionData");
                fail += Check("the wave package keeps its source's begin/end/change events",
                              pk == null ? "missing" : $"topics {string.Join("/", new[] { pk.OnBegin, pk.OnEnd, pk.OnChange }.Select(e => e?.Topics.Count ?? -1))}",
                              bm.WavePackageShape);
            }
            if (bm.DroppedPlace is (int dpId, string dpName))
            {
                string tok = $"<Alias={dpName}>";
                bool aliasGone = !q.Aliases.OfType<IQuestLocationAliasGetter>().Any(a => a.ID == (uint)dpId);
                var named = q.Stages.Where(s => s.LogEntries.Any(e => e.Entry?.String?.Contains(tok) == true)).Select(s => $"stage {s.Index}")
                    .Concat(q.Objectives.Where(o => o.DisplayText?.String?.Contains(tok) == true).Select(o => $"objective {o.Index}"))
                    .Concat(q.Aliases.OfType<IQuestReferenceAliasGetter>().Where(a => a.Location?.AliasID == dpId).Select(a => $"ref alias {a.ID}")).ToList();
                fail += Check($"the unused second place {dpName} is gone and nothing names it",
                              $"alias {(aliasGone ? "gone" : "PRESENT")}, named by {(named.Count == 0 ? "nothing" : string.Join(", ", named))}",
                              "alias gone, named by nothing");
            }
            string fsrc = File.Exists(bm.PscPath) ? File.ReadAllText(bm.PscPath) : "";
            if (bm.HandBack is (int hst, List<string> hlines))
            {
                // The completing stage's own function, cut out of the generated source, must take every carried item.
                string fn = $"Function {FragmentFunction(hst)}()";
                int at = fsrc.IndexOf(fn, StringComparison.Ordinal);
                string body = at < 0 ? "" : fsrc[at..(fsrc.IndexOf("EndFunction", at, StringComparison.Ordinal) is int e && e > at ? e : fsrc.Length)];
                fail += Check($"the talk that ends the Delve takes what is carried, on stage {hst}",
                              at < 0 ? "no fragment" : $"{hlines.Count(body.Contains)} of {hlines.Count} item(s) taken",
                              $"{hlines.Count} of {hlines.Count} item(s) taken");
            }
            foreach (var (sm, sa) in bm.DroppedSlots)
            {
                var pair = new[] { sm, sa };
                bool aliasGone = !q.Aliases.OfType<IQuestReferenceAliasGetter>().Any(a => pair.Contains((int)a.ID));
                var users = SlotUsers(q.Aliases.OfType<IQuestReferenceAliasGetter>(), q.Objectives, vma.Aliases.Select(a => (int)a.Property.Alias), pair);
                fail += Check($"the unused base slot {sm}/{sa} is gone and nothing names it",
                              $"aliases {(aliasGone ? "gone" : "PRESENT")}, named by {(users.Count == 0 ? "nothing" : string.Join(", ", users))}",
                              "aliases gone, named by nothing");
            }
            foreach (var (lst, wem, text, sceneKey) in bm.Lines)
            {
                var sc = q.Scenes.FirstOrDefault(s => s.FormKey == sceneKey);
                var act = sc?.Actions?.OfType<IRadioSceneActionGetter>().FirstOrDefault();
                var tp = act == null ? null : q.DialogTopics.FirstOrDefault(d => d.FormKey == act.Topic.FormKey);
                var rsp = tp?.Responses.FirstOrDefault();
                var line = rsp?.Responses.FirstOrDefault(x => x.WEMFile == wem);   // one of the line's responses (schema 2)
                fail += Check($"stage {lst}'s line is a radio scene the speaker says, voiced as {wem:X8}.wem",
                              sc == null ? "scene missing"
                              : $"alias {act?.AliasID}, speaker {rsp?.Speaker.FormKey}, wem {line?.WEMFile:X8}, text \"{line?.ResponseText?.String}\"",
                              $"alias -4, speaker {bm.Speaker}, wem {wem:X8}, text \"{text}\"");
                if (lst == 0)
                    fail += Check("the broadcast scene begins on quest start (a stage-0 Start() plays nothing)",
                                  (sc?.Flags?.HasFlag(Scene.Flag.BeginOnQuestStart) ?? false).ToString(), "True");
            }
            // A talk beat, read back: the alias that creates him, every scene gated on him and on the step, the
            // greeting's shape, and the one reply that ends the talk by setting the beat's stage.
            if (bm.Talks.Count > 0)
                fail += Check("a person talks, so the quest sits above the ambient greeting pool", q.Data?.Priority.ToString() ?? "none", TalkQuestPriority.ToString());
            // Every person the build cloned: no faction and no crime faction (not a New Atlantis MAST worker).
            foreach (var (pb, (pk, pname)) in bm.People)
            {
                var pn = m.Npcs.FirstOrDefault(n => n.FormKey == pk);
                fail += Check($"beat {pb + 1}'s person {pname} carries no faction and no crime faction",
                              pn == null ? "missing" : $"{pn.Factions.Count} faction(s), crime {(pn.CrimeFaction.IsNull ? "none" : pn.CrimeFaction.FormKey.ToString())}",
                              "0 faction(s), crime none");
            }
            // Walk-up lines: vanilla's Hello shape, read back.
            if (bm.Hellos.Count > 0)
            {
                var ht = q.DialogTopics.FirstOrDefault(d => d.EditorID == $"{r.id}_hellos");
                fail += Check("one Hello topic (Misc / Hello, priority 50)",
                              ht == null ? "missing" : $"{ht.Category} / {ht.Subtype} / {ht.Priority}", "Misc / Hello / 50");
                foreach (var (hb, hnpc, hn) in bm.Hellos)
                {
                    var grp = ht?.Responses.FirstOrDefault(x => x.EditorID == $"{r.id}_talk{hb + 1}_hellos");
                    var gid = grp?.Conditions.FirstOrDefault()?.Data as IGetIsIDConditionDataGetter;
                    fail += Check($"beat {hb + 1}'s walk-up group is gated on him alone",
                                  grp == null ? "missing" : $"flags {grp.Flags?.Flags}, {grp.Conditions.Count} condition(s), GetIsID {gid?.FirstParameter.Link.FormKey}",
                                  $"flags 256, 1 condition(s), GetIsID {hnpc}");
                    var kids = ht?.Responses.Where(x => grp != null && x.DialogGroup.FormKey == grp.FormKey).ToList() ?? new();
                    fail += Check($"beat {hb + 1}'s walk-up lines sit under the group, each named for its own wem",
                                  $"{kids.Count} line(s), flags {string.Join("/", kids.Select(k => k.Flags?.Flags).Distinct())}, own-id wems {kids.All(k => k.Responses.FirstOrDefault()?.WEMFile == k.FormKey.ID)}",
                                  $"{hn} line(s), flags 1024, own-id wems True");
                }
            }
            foreach (var (tbeat, tstage, talias, tnpc, nex, tconds) in bm.Talks)
            {
                string who = $"beat {tbeat + 1} (talk)";
                var pa = q.Aliases.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(a => a.ID == (uint)talias);
                fail += Check($"{who}: alias {talias} creates the person at his marker",
                              pa?.CreateReferenceToObject == null ? "no create" : $"{pa.CreateReferenceToObject.Object.FormKey} at alias {pa.CreateReferenceToObject.AliasID}",
                              $"{tnpc} at alias {pa?.CreateReferenceToObject?.AliasID}");
                var mine = q.Scenes.Where(x => x.EditorID != null && x.EditorID.StartsWith($"{r.id}_talk{tbeat + 1}_")).ToList();
                fail += Check($"{who}: a greeting and one scene per exchange", mine.Count.ToString(), (1 + nex).ToString());
                var greet = mine.FirstOrDefault(x => x.EditorID!.EndsWith("_greet_scene"));
                fail += Check($"{who}: the greeting is a Greeting scene", greet == null ? "missing" : $"0x{(uint)(greet.Flags ?? 0):X8}", "0x00001834");
                foreach (var sc in mine)
                {
                    // The greeting and the completion (topic1) close with the beat's stage; a side question outlives it.
                    bool untilDone = sc == greet || sc.EditorID!.EndsWith("_topic1_scene");
                    var isId = sc.Conditions.FirstOrDefault()?.Data as IGetIsIDConditionDataGetter;
                    fail += Check($"{who}: {sc.EditorID} is his and gated on the step" + (untilDone ? "" : " having opened"),
                                  $"{isId?.FirstParameter.Link.FormKey}, {sc.Conditions.Count} conditions, actor {sc.Actors.FirstOrDefault()?.ID}",
                                  $"{tnpc}, {(untilDone ? tconds : tconds - 1)} conditions, actor {talias}");
                    if (sc != greet)
                        fail += Check($"{who}: {sc.EditorID} loops back to the menu (his 2026-10-19 ruling)",
                                      $"0x{(uint)(sc.Flags ?? 0):X8}", "0x00002814");
                }
                var closer = q.DialogTopics.FirstOrDefault(d => d.EditorID == $"{r.id}_talk{tbeat + 1}_reply1")?.Responses.FirstOrDefault();
                fail += Check($"{who}: the first reply ends the talk by setting {tstage}",
                              closer?.SetParentQuestStage == null ? "none" : $"OnEnd {closer.SetParentQuestStage.OnEnd}, speaker {closer.Speaker.FormKey}",
                              $"OnEnd {tstage}, speaker {tnpc}");
                int setters = q.DialogTopics.Where(d => d.EditorID != null && d.EditorID.StartsWith($"{r.id}_talk{tbeat + 1}_"))
                               .SelectMany(d => d.Responses).Count(x => x.SetParentQuestStage != null);
                fail += Check($"{who}: no other line sets a stage", setters.ToString(), "1");
            }
            if (bm.Linger >= 0)
            {
                // Ending on a talk: the completing stage completes and does not stop; the shutdown stage stops; a board's
                // MissionComplete no longer shuts it down. Read off the generated source and the written record.
                string Body(int st)
                {
                    string head = $"Function {FragmentFunction(st)}()";
                    int i = fsrc.IndexOf(head);
                    if (i < 0) return "";
                    int j = fsrc.IndexOf("EndFunction", i);
                    return j < 0 ? "" : fsrc[i..j];
                }
                int done = bm.LingerFrom;
                string end = t.questScript != null ? "kmyQuest.MissionShutdown()" : "    Stop()";
                fail += Check($"ending on a talk: stage {done} completes the quest and does not stop it",
                              (!Body(done).Contains("    Stop()") && !Body(done).Contains("MissionShutdown()")).ToString(), "True");
                fail += Check($"ending on a talk: stage {bm.Linger} exists and its fragment stops the quest",
                              (q.Stages.Any(s => s.Index == bm.Linger) && Body(bm.Linger).Contains(end)).ToString(), "True");
                if (t.questScript != null)
                    fail += Check($"ending on a talk: {t.questScript}.ShutdownOnCompletion is off",
                                  vma.Scripts.FirstOrDefault(s => s.Name == t.questScript)?.Properties.OfType<IScriptBoolPropertyGetter>()
                                      .FirstOrDefault(p => p.Name == "ShutdownOnCompletion")?.Data.ToString() ?? "unset (true)", "False");
            }
            foreach (var ds in bm.StuckWaits)
                fail += Check($"wave stage {ds}: the stuck guard waits in its spawning fragment and sets it",
                              (fsrc.Contains($"If !GetStageDone({ds})") && fsrc.Contains($"SetStage({ds})")).ToString(), "True");
            fail += Check("no timer event in the fragment script (a fragment's OnTimer never fires)",
                          fsrc.Contains("Event OnTimer(").ToString(), "False");
            foreach (var (an, alias) in bm.Replaced)
            {
                var p = vma.Script?.Properties.OfType<IScriptObjectPropertyGetter>().FirstOrDefault(x => x.Name == an);
                fail += Check($"fragment property {an} (disabled at start)", p == null ? "missing" : $"{p.Object.FormKey} alias {p.Alias}", $"{q.FormKey} alias {alias}");
            }
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
            foreach (var (robj, alias, rsecs, rstage) in bm.Reveals)
            {
                var ro = q.Objectives.FirstOrDefault(o => o.Index == robj);
                fail += Check($"objective {robj} points at wave collection {alias}, unconditioned",
                              ro == null ? "missing" : string.Join("+", ro.Targets.Select(x => $"{x.AliasID}/{x.Conditions.Count}")), $"{alias}/0");
                fail += Check($"objective {robj} is shown after a {rsecs} s wait and hidden at stage {rstage}",
                              (fsrc.Contains($"Utility.Wait({rsecs}.0)") && fsrc.Contains($"    SetObjectiveDisplayed({robj})")
                               && fsrc.Contains($"SetObjectiveDisplayed({robj}, False)")).ToString(), "True");
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
