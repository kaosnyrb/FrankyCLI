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
    /// A HOLD is waves of enemies at a marker, one after another, each when the last is down (Jessica's Type 6).
    /// Each wave is spawned by a fragment INTO its own empty RefCollection alias (a copy of vanilla's
    /// UC08_QueenBattle ActiveHostiles), and a stock DefaultCollectionAliasOnDeath on that collection sets
    /// the next stage when every member is dead, so the waves are N stages and N stock hooks, no driver.
    /// The objective carries a "(n/N)" counter. A wave that cannot be finished (an enemy stuck out of reach)
    /// must not soft-lock the quest: a pity timer in the fragment script sets the wave's stage anyway after
    /// `stuck` seconds (his infestation driver's 180 s), and the hook's TurnOffStageDone stops a late kill
    /// from setting it twice.
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
        private static readonly string[] BeatTypes = { "use", "pickup", "deliver", "recover", "hold" };

        /// <summary>The stuck-enemy guard's default, in seconds: his infestation driver's pity timer.</summary>
        private const int DefaultStuckSeconds = 180;

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
        private static Dictionary<int, int> StagesOf(List<Step> steps, int complete)
        {
            var beatStage = new Dictionary<int, int>();
            int next = 10;
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

        /// <summary>The lint's beats-only half. Called from Grade; Fatal/Warn are Grade's.</summary>
        private static void GradeBeats(Recipe r, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                       Action<string> Fatal, Action<string> Warn)
        {
            for (int i = 0; i < r.beats.Count; i++)
            {
                var b = r.beats[i];
                if (b.type == null || !BeatTypes.Contains(b.type))
                    Fatal($"beat {i + 1} has type '{b.type ?? "(none)"}'; a beats Delve knows {string.Join(", ", BeatTypes)}.");
                if ((b.type == "pickup" || b.type == "recover") && string.IsNullOrWhiteSpace(b.item))
                    Fatal($"beat {i + 1} is a {b.type} with no item: the player gets a thing with no name.");
                if (b.type != "pickup" && b.type != "recover" && b.item != null)
                    Fatal($"beat {i + 1} names an item and is not a pickup or a recover, so it would be silently ignored.");
                if (b.type == "recover" && (b.model != null || b.name != null || b.replace))
                    Fatal($"beat {i + 1} is a recover: it has no object of its own (the holder carries the item), so model, name and replace would be ignored.");
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
                if (b.type == "hold")
                {
                    if (b.waves is not int w || w < 1 || w > 5)
                        Fatal($"beat {i + 1} is a hold and needs \"waves\" from 1 to 5; it has {(b.waves?.ToString() ?? "none")}.");
                    if (b.size == null || b.size.Count != 2 || b.size[0] < 1 || b.size[1] < b.size[0])
                        Fatal($"beat {i + 1} is a hold and needs \"size\": [min, max] with 1 <= min <= max; a wave of nobody has no last death, so it never clears.");
                    else if (b.size[1] > 12)
                        Warn($"beat {i + 1}'s waves reach {b.size[1]} enemies each; more than 12 at one marker is a crowd the navmesh may not seat.");
                    if (b.stuck is int s && s < 30)
                        Fatal($"beat {i + 1}'s stuck is {s} s; the guard would move the quest on mid-fight. 30 or more.");
                    if (b.model != null || b.name != null || b.replace)
                        Fatal($"beat {i + 1} is a hold: it has no object of its own (the waves come to a marker), so model, name and replace would be ignored.");
                    if (b.objective is string ho && ho.Contains('('))
                        Warn($"beat {i + 1}'s objective has a bracket; the build appends the counter \"(n/{b.waves})\" itself.");
                    if (b.defend is int d)
                    {
                        if (d < 1 || d > i) Fatal($"beat {i + 1} defends beat {d}, which is not an earlier beat.");
                        else
                        {
                            var dt = r.beats[d - 1];
                            if (dt.type == "pickup" || dt.type == "recover" || dt.type == "hold")
                                Fatal($"beat {i + 1} defends beat {d}, a {dt.type}, which leaves no object standing to defend; defend a use or a deliver.");
                        }
                    }
                }
                else if (b.waves != null || b.size != null || b.defend != null || b.stuck != null)
                    Fatal($"beat {i + 1} sets waves, size, defend or stuck and is not a hold, so they would be silently ignored.");
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
                    if (tb.type == "recover" || tb.type == "hold" || tb.returnTo != null)
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
                    }
                    else Warn($"beat {b + 1} is an ending with no person, so nobody stands there; a box that talks about a person talks about nobody.");
                }
            }
            for (int i = 0; i < r.beats.Count; i++)
                if (r.beats[i].choose == null && (r.beats[i].reward != null || r.beats[i].person != null))
                    Fatal($"beat {i + 1} sets reward or person and is not a choose ending, so it would be silently ignored.");
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
            int personCount = r.beats.Count(b => b.person != null);
            if (personCount > 0)
            {
                if (t.approachStage < 0 || t.playerAlias < 0) Fatal($"template '{t.id}' names no approachStage or playerAlias for an ending's person.");
                else if (t.approachStage + personCount >= 10) Fatal($"{personCount} people need stages {t.approachStage + 1} to {t.approachStage + personCount}, which reach the first beat's 10.");
            }

            int recoverCount = r.beats.Count(b => b.type == "recover");
            if (recoverCount > 5) Fatal($"{recoverCount} recover beats; the player's OnItemAdded hook has the base and four duplicates, so five.");
            if (recoverCount > 0 && t.playerAlias < 0) Fatal($"template '{t.id}' names no playerAlias for a recover beat's hook.");

            // Delivering needs something in hand: the first deliver must come after a pickup or a recover.
            int carried = 0;
            bool endsOnChoice = false;
            for (int i = 0; i < r.beats.Count; i++)
            {
                if (r.beats[i].type == "pickup" || r.beats[i].type == "recover") carried++;
                else if (r.beats[i].type == "deliver")
                {
                    if (carried == 0) Fatal($"beat {i + 1} is a deliver with nothing picked up or recovered before it, so there is nothing to hand over.");
                    if (r.beats[i].choose == null) carried = 0;   // every ending takes the same things
                    else endsOnChoice = true;
                }
            }
            if (carried > 0 && !endsOnChoice) Warn($"{carried} picked-up item(s) are never delivered, so they stay in the player's inventory after the quest.");

            var beatStage = StagesOf(steps, t.completeStage);
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
            var spoken = new List<(string where, string text)>();
            if (r.broadcast != null) spoken.Add(("broadcast", r.broadcast));
            for (int i = 0; i < r.beats.Count; i++) if (r.beats[i].say != null) spoken.Add(($"beat {i + 1}'s say", r.beats[i].say!));
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
            public List<(int objective, int alias, int stage)> TargetGates = new();
            public List<(string name, int alias)> Replaced = new();
            public List<int> Holders = new();   // recover beats' empty Optional aliases
            public List<int> Waves = new();     // hold beats' wave collections (the inner ref alias ids), empty and Optional
            public FormKey WavePackage;         // the Travel-to-player package every wave collection wears
            public (int id, string name)? DroppedPlace;   // a one-place Delve's removed second place alias
            public FormKey Speaker;                        // the cloned NPC whose name and voice type the lines carry
            public List<(int stage, uint wem, string text, FormKey scene)> Lines = new();   // each spoken line, by the stage that plays it
            public string WavePackageShape = "";   // its source's begin/end/change topic counts, which the copy must keep
            public bool PityTimer;              // a hold put an OnTimer guard in the fragment script
            public (int on, int to)? ApproachTarget;   // the approach hook's alias and the alias it measures to
            public List<(int on, int to)> PersonApproach = new();   // each ending person's hook: its object, measuring to the player
            public Dictionary<int, (FormKey creds, FormKey xp)> StageReward = new();
            public Dictionary<int, (FormKey npc, string name)> People = new();
            public (int stage, string text)? OfferJournal;   // "replace" beats: the marker alias stage 0 disables  // a group member's target, lit until its stage
        }

        private static int BuildBeats(StarfieldMod myMod, Quest clone, Template t, Recipe r,
                                      Dictionary<string, FormKey> markers, BuildMade made, BeatsMade bm,
                                      Dictionary<int, (INpcGetter tmpl, FormKey? outfit, FormKey? company)> persons,
                                      IQuestCollectionAliasGetter? waveSrc, IPackageGetter? travelSrc,
                                      (INpcGetter tmpl, FormKey voice)? speakerSrc)
        {
            var vma = clone.VirtualMachineAdapter;
            if (vma == null) { Console.WriteLine("REFUSED: the base has no VMAD."); return 1; }

            // --- 1. no driver ---------------------------------------------------------------------------
            var old = vma.Scripts.FirstOrDefault(s => string.Equals(s.Name, t.replacesDriver, StringComparison.OrdinalIgnoreCase));
            if (old == null) { Console.WriteLine($"REFUSED: the base carries no '{t.replacesDriver}' to remove."); return 1; }
            var cargo = old.Properties.OfType<ScriptObjectProperty>()
                .FirstOrDefault(p => string.Equals(p.Name, "CargoObject", StringComparison.OrdinalIgnoreCase))?.Object.FormKey;
            var itemSrc = cargo == null ? null : myMod.MiscItems.FirstOrDefault(m => m.FormKey == cargo.Value);
            // recover: the holder and his gang are drawn from the removed driver's own list, as delve4's were.
            var gangKey = old.Properties.OfType<ScriptObjectProperty>()
                .FirstOrDefault(p => string.Equals(p.Name, "GangMembers", StringComparison.OrdinalIgnoreCase))?.Object.FormKey;
            int? gangMax = old.Properties.OfType<ScriptIntProperty>()
                .FirstOrDefault(p => string.Equals(p.Name, "MaxGangMembers", StringComparison.OrdinalIgnoreCase))?.Data;
            var civsKey = old.Properties.OfType<ScriptObjectProperty>()
                .FirstOrDefault(p => string.Equals(p.Name, "TargetCivListMembers", StringComparison.OrdinalIgnoreCase))?.Object.FormKey;
            if (r.approach?.civilians == true && civsKey == null)
            { Console.WriteLine($"REFUSED: approach.civilians needs the removed driver's TargetCivListMembers, and '{t.replacesDriver}' lacks it."); return 1; }
            if (r.approach?.lose != null && gangKey == null)
            { Console.WriteLine($"REFUSED: approach.lose places its helper from the removed driver's GangMembers, and '{t.replacesDriver}' lacks it."); return 1; }
            if (r.beats.Any(b => b.type == "recover") && (gangKey == null || gangMax == null))
            { Console.WriteLine($"REFUSED: a recover beat needs the removed driver's GangMembers and MaxGangMembers, and '{t.replacesDriver}' lacks one."); return 1; }
            if (r.beats.Any(b => b.type == "hold") && gangKey == null)
            { Console.WriteLine($"REFUSED: a hold's waves are drawn from the removed driver's GangMembers, and '{t.replacesDriver}' lacks it."); return 1; }
            if (r.beats.Any(b => b.type == "hold") && (waveSrc == null || travelSrc == null))
            { Console.WriteLine("REFUSED: a hold needs a source collection alias and a source package to copy its waves from, and one was not handed in."); return 1; }

            // A hold's waves come FOR the player: one package per Delve, a copy of the bounty hunters' Travel
            // (jog, weapon drawn, to PlayerRef), worn by every wave collection, so every member runs it. The
            // copy keeps the two GetDistance conditions (travel while the player is 9 to 1000 m away; closer
            // than that they are in combat) and drops what belongs to the bounty quest: its two GetStageDone
            // conditions, its fragment scripts and its dialogue on begin/end/change.
            FormKey travelPkg = default;
            foreach (var k in myMod.Packages.Where(p => p.EditorID != null && p.EditorID.StartsWith(r.id + "_wavepkg")).Select(p => p.FormKey).ToList())
                myMod.Packages.Remove(k);
            if (r.beats.Any(b => b.type == "hold"))
            {
                var pk = myMod.Packages.DuplicateInAsNewRecord(travelSrc!);
                pk.EditorID = r.id + "_wavepkg";
                pk.VirtualMachineAdapter = null;
                pk.OwnerQuest.SetTo(clone.FormKey);
                // ⛔ THE BEGIN/END/CHANGE EVENTS ARE COPIED UNTOUCHED. Each carries ONE TopicReference that is
                // an EMPTY placeholder (no topic set), so there is no dialogue to strip; the first build
                // cleared the list "to drop the bounty hunters' lines", changed the record's shape for
                // nothing, and his next play crashed to desktop (2026-10-09; the leading suspect, not proven).
                // Read a field before removing it.
                // NOT a suspect, measured: the copy's inputs are written sorted (1,3,5,7,8 where the source
                // stores 1,3,5,8,7), because Mutagen's writer sorts them whatever order they are given, and
                // Starfield.esm ships BOTH orders on Travel packages (248 sorted, 214 not).
                bm.WavePackageShape = $"topics {string.Join("/", new[] { travelSrc!.OnBegin, travelSrc.OnEnd, travelSrc.OnChange }.Select(e => e?.Topics.Count ?? -1))}";
                pk.Conditions.RemoveAll(c => c.Data is not GetDistanceConditionData);
                foreach (var c in pk.Conditions)
                {
                    var gd = (GetDistanceConditionData)c.Data;
                    if (gd.FirstParameter.Link.FormKey.ID != 0x14)
                    { Console.WriteLine($"REFUSED: the copied package's distance condition measures to {gd.FirstParameter.Link.FormKey}, not PlayerRef."); return 1; }
                }
                travelPkg = pk.FormKey;
                bm.WavePackage = pk.FormKey;
                Console.WriteLine($"  +package : {pk.EditorID} {pk.FormKey} (clone of {travelSrc!.EditorID}; Travel to PlayerRef, {pk.Conditions.Count} distance condition(s) kept)");
            }
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
            int baseNext = 0;   // the base's own marker+activator pairs, handed to object-bearing beats in order
            var waveAliases = new Dictionary<int, List<int>>();   // hold beat -> its wave collections, in order
            for (int i = 0; i < r.beats.Count; i++)
            {
                if (r.beats[i].returnTo != null) continue;   // shares an earlier beat's object; filled in below
                if (r.beats[i].type == "hold")
                {
                    // A marker the waves spawn at, and one EMPTY collection per wave, each a copy of vanilla's
                    // ActiveHostiles (Optional, AllowDisabled, no fill), renamed and renumbered. Filled at
                    // runtime by PlaceAtMe's akAliasToFill, as vanilla's MissionBoardCargoContainerScript does.
                    uint hid = 1 + clone.Aliases!.SelectMany(Flatten).Select(x => x.id).DefaultIfEmpty(0u).Max();
                    var hm = srcMarker.DeepCopy(); hm.ID = hid; hm.Name = "DelveBeat" + (i + 1) + "Spawn";
                    clone.Aliases.Add(hm);
                    slotOf[i] = new BeatSlot { markerAlias = (int)hm.ID, activatorAlias = -1, journalStage = beatStage[i] };
                    waveAliases[i] = new List<int>();
                    int nw = steps.First(s => s.Beats.Contains(i)).Waves;
                    for (int w = 0; w < nw; w++)
                    {
                        uint cid = 1 + clone.Aliases!.SelectMany(Flatten).Select(x => x.id).DefaultIfEmpty(0u).Max();
                        var col = waveSrc!.DeepCopy();
                        var entry = col.Collection[0];
                        var ra = entry.ReferenceAlias!;
                        if (ra.Location != null || !ra.ForcedReference.IsNull || !ra.UniqueActor.IsNull || ra.CreateReferenceToObject != null)
                        { Console.WriteLine("REFUSED: the source collection carries a fill; a wave must start empty."); return 1; }
                        // The entry carries its own ID beside the inner alias's; where vanilla keeps them equal,
                        // so does the copy.
                        if (entry.ID == ra.ID) entry.ID = cid;
                        ra.ID = cid;
                        ra.Name = $"DelveBeat{i + 1}Wave{w + 1}";
                        ra.PackageData.Clear();
                        ra.PackageData.Add(travelPkg.ToLink<IPackageGetter>());
                        clone.Aliases.Add(col);
                        waveAliases[i].Add((int)cid);
                        bm.Waves.Add((int)cid);
                    }
                    Console.WriteLine($"  +slot    : beat {i + 1} (hold) -- spawn marker alias {hm.ID}, wave collections {string.Join(", ", waveAliases[i])} (EMPTY, copies of ActiveHostiles)");
                    continue;
                }
                if (r.beats[i].type == "recover")
                {
                    // A marker where the holder appears, and an EMPTY Optional alias he is placed into, which the
                    // objective targets so it follows him (delve4's shape: a non-optional alias with no fill
                    // makes the whole quest silently not start).
                    uint rid = 1 + clone.Aliases!.SelectMany(Flatten).Select(x => x.id).DefaultIfEmpty(0u).Max();
                    var rm = srcMarker.DeepCopy(); rm.ID = rid; rm.Name = "DelveBeat" + (i + 1) + "Marker";
                    var rh = srcMarker.DeepCopy(); rh.ID = rid + 1; rh.Name = "DelveBeat" + (i + 1) + "Holder";
                    rh.Location = null;
                    rh.Flags = QuestReferenceAlias.Flag.Optional;
                    clone.Aliases.Add(rm);
                    clone.Aliases.Add(rh);
                    slotOf[i] = new BeatSlot { markerAlias = (int)rm.ID, activatorAlias = -1, targetAlias = (int)rh.ID, journalStage = beatStage[i] };
                    bm.Holders.Add((int)rh.ID);
                    Console.WriteLine($"  +slot    : beat {i + 1} (recover) -- marker alias {rm.ID}, holder {rh.ID} (EMPTY, Optional; filled on spawn)");
                    continue;
                }
                if (baseNext < t.beatSlots.Count)
                {
                    slotOf[i] = new BeatSlot { markerAlias = t.beatSlots[baseNext].markerAlias, activatorAlias = t.beatSlots[baseNext].activatorAlias,
                                               journalStage = beatStage[i] };
                    baseNext++;
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
            // A return is the earlier beat's own object visited again: same aliases, its own stage and journal.
            for (int i = 0; i < r.beats.Count; i++)
                if (r.beats[i].returnTo is int back)
                {
                    var o = slotOf[back - 1];
                    slotOf[i] = new BeatSlot { markerAlias = o.markerAlias, activatorAlias = o.activatorAlias, journalStage = beatStage[i] };
                    Console.WriteLine($"  slot     : beat {i + 1} returns to beat {back}'s object (alias {o.activatorAlias})");
                }
            if (baseNext < t.beatSlots.Count)
            { Console.WriteLine($"REFUSED: the recipe gives the base's {t.beatSlots.Count} object slots only {baseNext} beat(s) with an object of its own."); return 1; }
            // A hold's objective points at what is being defended, when it names one (a return shares its
            // original's object, so this runs after the returns are filled).
            for (int i = 0; i < r.beats.Count; i++)
                if (r.beats[i].type == "hold" && r.beats[i].defend is int d)
                {
                    slotOf[i].targetAlias = slotOf[d - 1].activatorAlias;
                    Console.WriteLine($"  slot     : beat {i + 1} (hold) defends beat {d}'s object (alias {slotOf[i].targetAlias})");
                }

            // --- 3. stages: one per beat, one per group's done, cloned from a working plain stage ---------
            // An ending's person is placed on its own stage, numbered after the approach's.
            var personStage = new Dictionary<int, int>();
            foreach (var b in Enumerable.Range(0, r.beats.Count).Where(b => r.beats[b].person != null))
                personStage[b] = t.approachStage + 1 + personStage.Count;
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
            var allStages = beatStage.Values.Concat(steps.Select(s => s.DoneStage)).Concat(steps.SelectMany(s => s.WaveStages))
                .Concat(r.approach != null ? new[] { t.approachStage } : Array.Empty<int>())
                .Concat(personStage.Values).Distinct().OrderBy(x => x).ToList();
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
            foreach (var cs in steps.Where(s => s.IsChoose))
                foreach (var b in cs.Beats)
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

            // --- 5. items, one clone per pickup -----------------------------------------------------------
            foreach (var k in myMod.MiscItems.Where(m => m.EditorID != null && m.EditorID.StartsWith(r.id + "_item")).Select(m => m.FormKey).ToList())
                myMod.MiscItems.Remove(k);
            var itemOf = new Dictionary<int, FormKey>();
            for (int i = 0; i < r.beats.Count; i++)
            {
                if (r.beats[i].type != "pickup" && r.beats[i].type != "recover") continue;
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

            // --- 7. objectives: one per step, targets = its beats' activators --------------------------------
            var lastOb = clone.Objectives?.OrderBy(o => o.Index).LastOrDefault();
            if (lastOb?.Targets == null || lastOb.Targets.Count != 1)
            { Console.WriteLine("REFUSED: the base's last objective is not a single-target objective to clone."); return 1; }
            var obSrc = lastOb.DeepCopy();
            int ObjOf(Step st, int b) => st.IsChoose ? st.Objective + 10 * st.Beats.IndexOf(b) : st.Objective;
            var obIdx = steps.SelectMany(st => st.Beats.Select(b => ObjOf(st, b))).ToHashSet();
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
            var sortedObs = clone.Objectives.OrderBy(o => o.Index).ToList();
            clone.Objectives.Clear();
            clone.Objectives.AddRange(sortedObs);

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

            FormKey offerMsg = default;
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
            var npcOf = new Dictionary<int, FormKey>();
            foreach (var k in myMod.Npcs.Where(n => n.EditorID != null && n.EditorID.StartsWith(r.id + "_person")).Select(n => n.FormKey).ToList())
                myMod.Npcs.Remove(k);
            foreach (var (b, pr) in persons)
            {
                var pp = r.beats[b].person!;
                var npc = Retrograde.Utils.NPCTools.CloneNPC(myMod, pr.tmpl.DeepCopy());
                npc.EditorID = $"{r.id}_person{b + 1}";
                npc.Name = pp.name!;
                npc.Voice.SetTo(pr.tmpl.Voice.FormKey);
                npc.Aggression = Npc.AggressionType.Unaggressive;
                npc.Confidence = Npc.ConfidenceType.Average;
                if (pr.outfit is FormKey ofk) npc.DefaultOutfit.SetTo(ofk);
                myMod.Npcs.Add(npc);
                npcOf[b] = npc.FormKey;
                bm.People[b] = (npc.FormKey, pp.name!);
                Console.WriteLine($"  +npc     : {npc.EditorID} {npc.FormKey}  \"{pp.name}\"  (clone of {pr.tmpl.EditorID})" + (pp.outfit != null ? $"  outfit {pp.outfit}" : ""));
            }

            // --- the speaker: an NPC that is never placed, carrying the subtitle's NAME and the VOICE TYPE whose
            // folder the audio is looked up in (gen_dlgtest's shape: clone, then set Voice) ----------------------
            foreach (var k in myMod.Npcs.Where(n => n.EditorID == r.id + "_speaker").Select(n => n.FormKey).ToList())
                myMod.Npcs.Remove(k);
            if (speakerSrc is (INpcGetter stmpl, FormKey svoice))
            {
                var sp = Retrograde.Utils.NPCTools.CloneNPC(myMod, stmpl.DeepCopy());
                sp.EditorID = r.id + "_speaker";
                sp.Name = r.speaker!.name!;
                sp.Voice.SetTo(svoice);
                myMod.Npcs.Add(sp);
                bm.Speaker = sp.FormKey;
                Console.WriteLine($"  +speaker : {sp.EditorID} {sp.FormKey}  \"{sp.Name}\"  voice {r.speaker.voice} (clone of {stmpl.EditorID}, never placed)");
            }

            // --- 9. the events: one stock hook per beat --------------------------------------------------
            int recovers = 0;
            for (int k = 0; k < steps.Count; k++)
            {
                int prereq = k == 0 ? 0 : steps[k - 1].DoneStage;
                foreach (var b in steps[k].Beats)
                {
                    var beat = r.beats[b];
                    if (beat.type == "hold")
                    {
                        // One stock death hook per wave, on that wave's collection: when every member is dead
                        // it sets the wave's stage, armed only once the wave before it is down. TurnOffStageDone
                        // is the wave's own stage, so a kill after the pity timer moved on cannot set it twice.
                        var ws = steps[k].WaveStages;
                        for (int w = 0; w < ws.Count; w++)
                        {
                            int col = waveAliases[b][w];
                            int pre = w == 0 ? prereq : ws[w - 1];
                            var hs = new ScriptEntry { Name = "DefaultCollectionAliasOnDeath" };
                            hs.Properties.Add(new ScriptIntProperty { Name = "StageToSet", Data = ws[w], Flags = ScriptProperty.Flag.Edited });
                            hs.Properties.Add(new ScriptIntProperty { Name = "PrereqStage", Data = pre, Flags = ScriptProperty.Flag.Edited });
                            hs.Properties.Add(new ScriptIntProperty { Name = "TurnOffStageDone", Data = ws[w], Flags = ScriptProperty.Flag.Edited });
                            var he = new QuestFragmentAlias();
                            he.Property.Object.SetTo(clone.FormKey);
                            he.Property.Alias = (short)col;
                            he.Scripts.Add(hs);
                            vma.Aliases.Add(he);
                            bm.Hooks.Add((col, hs.Name, ws[w], pre));
                            Console.WriteLine($"  +hook    : beat {b + 1} (hold) wave {w + 1} collection {col} {hs.Name} sets {ws[w]} after {pre}");
                        }
                        continue;
                    }
                    int hookAlias; string script;
                    if (beat.type == "recover")
                    {
                        // On the PLAYER: the stage is set however the item reaches the pack (looted, picked up,
                        // handed over). The A-D duplicates exist so one alias can carry the hook several times.
                        hookAlias = t.playerAlias;
                        script = "DefaultAliasOnItemAddedScript" + (recovers == 0 ? "" : ((char)('A' + recovers - 1)).ToString());
                        recovers++;
                    }
                    else if (beat.returnTo != null)
                    {
                        // The same object a second time: the stock hook's one duplicate, on the same alias.
                        hookAlias = slotOf[b].activatorAlias;
                        script = "DefaultAliasOnActivateA";
                    }
                    else
                    {
                        hookAlias = slotOf[b].activatorAlias;
                        script = beat.type == "pickup" ? "DefaultAliasOnActivateGiveItem" : "DefaultAliasOnActivate";
                    }
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
                    else if (beat.type == "recover")
                    {
                        var fp = new ScriptObjectProperty { Name = "ItemFilter", Flags = ScriptProperty.Flag.Edited };
                        fp.Object.SetTo(itemOf[b]);
                        sc.Properties.Add(fp);
                    }
                    else
                    {
                        // An object a later beat comes back to keeps its prompt, or the return could never fire.
                        bool comesBack = r.beats.Any(x => x.returnTo == b + 1);
                        sc.Properties.Add(new ScriptBoolProperty { Name = "ShouldHideActivationAfterSuccessfulActivation", Data = !comesBack, Flags = ScriptProperty.Flag.Edited });
                    }
                    var entry = vma.Aliases.FirstOrDefault(a => a.Property.Alias == hookAlias);
                    if (entry == null)
                    {
                        entry = new QuestFragmentAlias();
                        entry.Property.Object.SetTo(clone.FormKey);
                        entry.Property.Alias = (short)hookAlias;
                        vma.Aliases.Add(entry);
                    }
                    if (entry.Scripts.Any(x => x.Name == script))
                    { Console.WriteLine($"REFUSED: alias {hookAlias} already carries {script}; a beat cannot hook it twice."); return 1; }
                    entry.Scripts.Add(sc);
                    bm.Hooks.Add((hookAlias, script, beatStage[b], prereq));
                    Console.WriteLine($"  +hook    : beat {b + 1} ({beat.type}) alias {hookAlias} {script} sets {beatStage[b]} after {prereq}");
                }
            }

            // An ending's person: a stock distance hook on the ending's OWN object, measuring to the player,
            // armed only once the choice is open (PrereqStage = the stage that enters it), as the choice
            // driver registered its distance events at the find.
            foreach (var (b, pst) in personStage)
            {
                int own = slotOf[b].activatorAlias;
                int enter = steps[steps.FindIndex(s => s.Beats.Contains(b)) - 1].DoneStage;
                var sc = new ScriptEntry { Name = "DefaultAliasOnDistanceLessThan" };
                var ta = new ScriptObjectProperty { Name = "TargetAlias", Flags = ScriptProperty.Flag.Edited };
                ta.Object.SetTo(clone.FormKey);
                ta.Alias = (short)t.playerAlias;
                sc.Properties.Add(ta);
                sc.Properties.Add(new ScriptFloatProperty { Name = "TargetDistance", Data = ApproachDistance, Flags = ScriptProperty.Flag.Edited });
                sc.Properties.Add(new ScriptIntProperty { Name = "StageToSet", Data = pst, Flags = ScriptProperty.Flag.Edited });
                sc.Properties.Add(new ScriptIntProperty { Name = "PrereqStage", Data = enter, Flags = ScriptProperty.Flag.Edited });
                var entry = vma.Aliases.FirstOrDefault(a => a.Property.Alias == own);
                if (entry == null) { Console.WriteLine($"REFUSED: ending alias {own} carries no hook entry to add the approach to."); return 1; }
                entry.Scripts.Add(sc);
                bm.Hooks.Add((own, sc.Name, pst, enter));
                bm.PersonApproach.Add((own, t.playerAlias));
                Console.WriteLine($"  +hook    : beat {b + 1}'s person, within {ApproachDistance} of its object (alias {own}) after {enter}, sets {pst}");
            }

            // The approach: a stock distance hook on the PLAYER, once, at any stage (no PrereqStage).
            if (r.approach is Approach apr)
            {
                int toAlias = slotOf[apr.to - 1].activatorAlias;
                var sc = new ScriptEntry { Name = "DefaultAliasOnDistanceLessThan" };
                var ta = new ScriptObjectProperty { Name = "TargetAlias", Flags = ScriptProperty.Flag.Edited };
                ta.Object.SetTo(clone.FormKey);
                ta.Alias = (short)toAlias;
                sc.Properties.Add(ta);
                sc.Properties.Add(new ScriptFloatProperty { Name = "TargetDistance", Data = ApproachDistance, Flags = ScriptProperty.Flag.Edited });
                sc.Properties.Add(new ScriptIntProperty { Name = "StageToSet", Data = t.approachStage, Flags = ScriptProperty.Flag.Edited });
                sc.Properties.Add(new ScriptIntProperty { Name = "PrereqStage", Data = -1, Flags = ScriptProperty.Flag.Edited });
                var entry = vma.Aliases.FirstOrDefault(a => a.Property.Alias == t.playerAlias);
                if (entry == null)
                {
                    entry = new QuestFragmentAlias();
                    entry.Property.Object.SetTo(clone.FormKey);
                    entry.Property.Alias = (short)t.playerAlias;
                    vma.Aliases.Add(entry);
                }
                entry.Scripts.Add(sc);
                bm.Hooks.Add((t.playerAlias, sc.Name, t.approachStage, -1));
                bm.ApproachTarget = (t.playerAlias, toAlias);
                Console.WriteLine($"  +hook    : approach within {ApproachDistance} of beat {apr.to}'s object (alias {toAlias}) sets {t.approachStage}");
            }

            // --- 10. the fragment script: generated, bound per stage -----------------------------------------
            string name = FragmentScriptName(r);
            var props = new List<(string type, string pname, FormKey key, int alias)>();   // alias -1 = a plain form
            var code = new SortedDictionary<int, List<string>>();
            void Add(int st, string line) { if (!code.ContainsKey(st)) code[st] = new(); code[st].Add(line); }

            foreach (var kv in counterOf)
            {
                string p = $"Counter{kv.Key + 1}";
                props.Add(("GlobalVariable", p, kv.Value.key, -1));
                Add(0, $"{p}.SetValue(0)");
                Add(0, $"UpdateCurrentInstanceGlobal({p})");
            }
            // "replace": the beat's marker is a placed object (a box), so it goes when the quest starts and
            // the beat's own object stands there instead. His shape: GetRef().Disable(False), never re-enabled.
            for (int b = 0; b < r.beats.Count; b++)
            {
                if (!r.beats[b].replace) continue;
                string an = $"Alias_Beat{b + 1}Marker";
                props.Add(("ReferenceAlias", an, clone.FormKey, slotOf[b].markerAlias));
                bm.Replaced.Add((an, slotOf[b].markerAlias));
                Add(0, $"{an}.GetRef().Disable(False)");
            }
            // A hold's wave w (0-based) appears at the spawn marker, INTO its own collection, and the stuck-enemy
            // guard starts: the timer's id is the stage this wave's last death sets, which OnTimer sets anyway.
            var pity = new List<int>();
            void SpawnWave(int b, int w, int stage)
            {
                var ws = steps.First(s => s.Beats.Contains(b)).WaveStages;
                string mk = $"Alias_Beat{b + 1}Spawn", wv = $"Alias_Beat{b + 1}Wave{w + 1}";
                props.Add(("ReferenceAlias", mk, clone.FormKey, slotOf[b].markerAlias));
                props.Add(("RefCollectionAlias", wv, clone.FormKey, waveAliases[b][w]));
                props.Add(("FormList", "Gang", gangKey!.Value, -1));
                var sz = r.beats[b].size!;
                Add(stage, $"duo_delve_lib.SpawnWave({mk}, {wv}, Gang, {sz[0]}, {sz[1]})");
                Add(stage, $"StartTimer({r.beats[b].stuck ?? DefaultStuckSeconds}.0, {ws[w]})   ; the stuck-enemy guard for wave {w + 1}");
                pity.Add(ws[w]);
            }
            // Entering a step: a recover beat's holder appears (and fills the alias the objective targets)
            // BEFORE the objective is shown, so it never shows with nothing to point at.
            void Enter(int k, int stage)
            {
                foreach (var b in steps[k].Beats.Where(b => r.beats[b].type == "hold"))
                    SpawnWave(b, 0, stage);
                foreach (var b in steps[k].Beats.Where(b => r.beats[b].type == "recover"))
                {
                    string mk = $"Alias_Beat{b + 1}Marker", hd = $"Alias_Beat{b + 1}Holder";
                    props.Add(("ReferenceAlias", mk, clone.FormKey, slotOf[b].markerAlias));
                    props.Add(("ReferenceAlias", hd, clone.FormKey, slotOf[b].targetAlias));
                    props.Add(("FormList", "Gang", gangKey!.Value, -1));
                    props.Add(("Form", $"Item{b + 1}", itemOf[b], -1));
                    Add(stage, $"duo_delve_lib.SpawnHolder({mk}, {hd}, Gang, Item{b + 1}, {t.gangMin}, {gangMax})");
                }
                if (steps[k].IsChoose)
                {
                    if (!offerMsg.IsNull) { props.Add(("Message", "OfferMessage", offerMsg, -1)); Add(stage, "OfferMessage.Show()"); }
                    foreach (var b in steps[k].Beats) Add(stage, $"SetObjectiveDisplayed({ObjOf(steps[k], b)})");
                }
                else Add(stage, $"SetObjectiveDisplayed({steps[k].Objective})");
            }
            Enter(0, 0);
            foreach (var (b, pst) in personStage)
            {
                string ta = $"Alias_Beat{b + 1}Target", pn = $"Person{b + 1}";
                props.Add(("ReferenceAlias", ta, clone.FormKey, slotOf[b].activatorAlias));
                props.Add(("ActorBase", pn, npcOf[b], -1));
                Add(pst, $"duo_delve_lib.PlacePerson({ta}, {pn}, 1.5)");
                var pc = r.beats[b].person!.company;
                if (pc != null && persons[b].company is FormKey cfk)
                {
                    props.Add(("FormList", $"Company{b + 1}", cfk, -1));
                    Add(pst, $"duo_delve_lib.PlaceCompany({ta}, Company{b + 1}, {pc.min}, {pc.max}, 8.0)");
                }
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
            if (speakerSrc != null)
            {
                var lines = new List<(int stage, string text)>();
                if (r.broadcast != null) lines.Add((0, r.broadcast));
                for (int b = 0; b < r.beats.Count; b++) if (r.beats[b].say != null) lines.Add((beatStage[b], r.beats[b].say!));
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
                    info.Responses.Add(new DialogResponse
                    {
                        ResponseText = ltext,
                        WEMFile = topic.FormKey.ID,
                        TextHash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(ltext))[..4],
                    });
                    topic.Responses.Add(info);
                    // TPIC: the topic's own list of its infos. Missing, the CK crashes on a click (book_audio.md).
                    topic.TopicInfoList = new Noggog.ExtendedList<IFormLinkGetter<IDialogResponsesGetter>> { info.ToLink<IDialogResponsesGetter>() };
                    clone.DialogTopics.Add(topic);

                    var action = new RadioSceneAction { Name = "Say", AliasID = -4, Index = 0, StartPhase = 0, EndPhase = 0 };
                    action.Topic.SetTo(topic.FormKey);
                    var scene = new Scene(myMod)
                    {
                        EditorID = tag + "_scene",
                        Flags = (Scene.Flag)0x80,   // on every vanilla audio-log scene; undocumented (book_audio.md)
                        VNAM = new byte[] { 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0 },
                    };
                    scene.Quest.SetTo(clone.FormKey);
                    scene.Actors.Add(new SceneActor { ID = unchecked((uint)-4), Flags = SceneActor.Flag.NoCommandState, BehaviorFlags = 0 });
                    scene.Phases.Add(new ScenePhase { Name = "SayPhase", EditorWidth = 500 });
                    scene.Actions = new Noggog.ExtendedList<ASceneAction> { action };
                    clone.Scenes.Add(scene);

                    string sv = $"Say{n + 1}";
                    props.Add(("Scene", sv, scene.FormKey, -1));
                    Add(lst, $"{sv}.Start()");
                    bm.Lines.Add((lst, topic.FormKey.ID, ltext, scene.FormKey));
                    Console.WriteLine($"  +speech  : stage {lst} plays {scene.EditorID} -> {topic.FormKey.ID:X8}.wem  \"{ltext}\"");
                }
            }
            var held = new List<int>();   // pickups and recovered items not yet delivered
            for (int k = 0; k < steps.Count; k++)
            {
                var s = steps[k];
                bool last = k == steps.Count - 1;
                foreach (var b in s.Beats)
                {
                    int st = beatStage[b];
                    if (msgOf.ContainsKey(b)) { props.Add(("Message", $"Beat{b + 1}Message", msgOf[b], -1)); Add(st, $"Beat{b + 1}Message.Show()"); }
                    if (r.beats[b].type == "pickup" || r.beats[b].type == "recover") held.Add(b);
                    if (r.beats[b].type == "deliver")
                    {
                        foreach (var h in held)
                        {
                            props.Add(("Form", $"Item{h + 1}", itemOf[h], -1));
                            Add(st, $"Game.GetPlayer().RemoveItem(Item{h + 1}, 1)");
                        }
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
                        Add(st, "Stop()");
                    }
                    if (s.IsHold)
                    {
                        // Each wave down: the counter ticks (and redisplays the objective, which is the player's
                        // only feedback: no message boxes in this type, his ruling), then the next wave comes.
                        for (int w = 0; w < s.WaveStages.Count; w++)
                        {
                            Add(s.WaveStages[w], $"ModObjectiveGlobal(1.0, Counter{k + 1}, {s.Objective}, {s.Waves}.0)");
                            if (w + 1 < s.WaveStages.Count) SpawnWave(b, w + 1, s.WaveStages[w]);
                        }
                    }
                    if (s.IsGroup)
                    {
                        Add(st,$"If ModObjectiveGlobal(1.0, Counter{k + 1}, {s.Objective}, {s.Beats.Count}.0)");
                        Add(st, $"    SetStage({s.DoneStage})");
                        Add(st, "EndIf");
                    }
                }
                int done = s.DoneStage;
                if (!s.IsGroup && !s.IsChoose) Add(done, $"SetObjectiveCompleted({s.Objective})");
                if (last) { if (!s.IsChoose) { Add(done, "CompleteQuest()"); Add(done, "Stop()"); } }
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
                psc.AppendLine("EndFunction");
                psc.AppendLine();
            }
            if (pity.Count > 0)
            {
                // The stuck-enemy guard (his infestation driver: "There is a chance that we can't reach all
                // targets"). A wave still alive when its timer ends is let go: its stage is set as if it fell.
                psc.AppendLine("Event OnTimer(Int aiTimerID)");
                for (int p = 0; p < pity.Count; p++)
                {
                    psc.AppendLine($"    {(p == 0 ? "If" : "ElseIf")} aiTimerID == {pity[p]} && !GetStageDone({pity[p]})");
                    psc.AppendLine($"        SetStage({pity[p]})");
                }
                psc.AppendLine("    EndIf");
                psc.AppendLine("EndEvent");
                psc.AppendLine();
                bm.PityTimer = true;
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

            string dir = Path.Combine(PapyrusDir(), "gen");
            Directory.CreateDirectory(dir);
            bm.PscPath = Path.Combine(dir, name + ".psc");
            File.WriteAllText(bm.PscPath, psc.ToString().Replace("\r\n", "\n"));
            Console.WriteLine($"  fragments: {name} on stages [{string.Join(", ", code.Keys)}], {props.DistinctBy(p => p.pname).Count()} propert(ies); source {bm.PscPath}");
            return 0;
        }

        /// <summary>
        /// The voice cache, in the Overtime repo so it is versioned and backed up with the plugin (his ask,
        /// 2026-10-09: "can we keep the audio files somewhere so we can just rename when needed"). One
        /// file per spoken line, named by a hash of its ElevenLabs voice, voice type and exact words, with
        /// a .txt beside it saying which. A rebuild re-mints every topic id, so the game file's NAME changes
        /// each build; the cache is what makes that a copy rather than a new generation.
        /// </summary>
        private const string VoiceCacheDir = @"C:\modding\DU_Overtime\voicecache";

        /// <summary>
        /// Put each spoken line's audio where the game looks for it: Sound\Voice\&lt;plugin&gt;\&lt;voice type&gt;\
        /// &lt;topic id, 8 hex&gt;.wem. From the cache when the same words in the same voice exist, else generated
        /// (ElevenLabs, then Wwise) and cached. The previous build's files go first, by its manifest.
        /// </summary>
        private static int DeployVoice(Recipe r, Template t, BeatsMade bm)
        {
            if (bm.Lines.Count == 0) return 0;
            Console.WriteLine();
            Console.WriteLine("  voice:");
            string plugin = t.mod + ".esm";
            string vt = r.speaker!.voice!, vid = r.speaker.elevenlabs!;
            string gameDir = Path.Combine(SpeechTools.GameVoiceDir, plugin, vt);
            Directory.CreateDirectory(VoiceCacheDir);
            Directory.CreateDirectory(gameDir);

            string manifest = Path.Combine(VoiceCacheDir, $"deployed_{r.id}.txt");
            if (File.Exists(manifest))
                foreach (var p in File.ReadAllLines(manifest).Where(p => p.Length > 0 && File.Exists(p)))
                {
                    File.Delete(p);
                    Console.WriteLine($"    -old     : {p}");
                }

            int fail = 0;
            var deployed = new List<string>();
            var toMake = new List<(uint wem, string key, string text)>();
            foreach (var (_, wem, text, _) in bm.Lines)
            {
                string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes($"{vid}|{vt}|{text}")))[..16].ToLowerInvariant();
                string cached = Path.Combine(VoiceCacheDir, key + ".wem");
                string dest = Path.Combine(gameDir, $"{wem:X8}.wem");
                if (File.Exists(cached))
                {
                    File.Copy(cached, dest, true);
                    deployed.Add(dest);
                    Console.WriteLine($"    cached   : {key}.wem -> {wem:X8}.wem  \"{text}\"");
                }
                else toMake.Add((wem, key, text));
            }
            if (toMake.Count > 0)
            {
                SpeechTools.generateWavs = true;
                var mk = ModKey.FromNameAndExtension(plugin);
                foreach (var (wem, _, text) in toMake) SpeechTools.GenerateWavs(wem, vt, mk, text, vid);
                SpeechTools.GenerateAllWavs();
                SpeechTools.ConvertAndDeploy();
                foreach (var (wem, key, text) in toMake)
                {
                    string dest = Path.Combine(gameDir, $"{wem:X8}.wem");
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
            File.WriteAllLines(manifest, deployed);
            foreach (var (_, wem, _, _) in bm.Lines)
                fail += Check($"{wem:X8}.wem is where the game looks", File.Exists(Path.Combine(gameDir, $"{wem:X8}.wem")).ToString(), "True");
            Console.WriteLine(fail == 0 ? $"  VOICE DEPLOYED: {bm.Lines.Count} line(s) under {gameDir}" : $"  {fail} VOICE CHECK(S) FAILED.");
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
            foreach (var (lst, wem, text, sceneKey) in bm.Lines)
            {
                var sc = q.Scenes.FirstOrDefault(s => s.FormKey == sceneKey);
                var act = sc?.Actions?.OfType<IRadioSceneActionGetter>().FirstOrDefault();
                var tp = act == null ? null : q.DialogTopics.FirstOrDefault(d => d.FormKey == act.Topic.FormKey);
                var rsp = tp?.Responses.FirstOrDefault();
                var line = rsp?.Responses.FirstOrDefault();
                fail += Check($"stage {lst}'s line is a radio scene the speaker says, voiced as {wem:X8}.wem",
                              sc == null ? "scene missing"
                              : $"alias {act?.AliasID}, speaker {rsp?.Speaker.FormKey}, wem {line?.WEMFile:X8}, text \"{line?.ResponseText?.String}\"",
                              $"alias -4, speaker {bm.Speaker}, wem {wem:X8}, text \"{text}\"");
            }
            if (bm.PityTimer)
                fail += Check("the stuck-enemy guard is in the fragment script",
                              (File.Exists(bm.PscPath) && File.ReadAllText(bm.PscPath).Contains("Event OnTimer(")).ToString(), "True");
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
