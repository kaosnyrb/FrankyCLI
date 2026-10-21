using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// hold: waves of enemies at a marker, one after another, each when the last is down (Jessica's Type 6). Each wave
        /// is spawned by a fragment INTO its own empty RefCollection alias (a copy of vanilla's UC08_AB_AnomalyBattle
        /// ActiveHostiles), and a stock DefaultCollectionAliasOnDeath on that collection sets the next stage when every
        /// member is dead, so the waves are N stages and N stock hooks, no driver. The objective carries a "(n/N)" counter.
        /// A wave that cannot be finished (an enemy stuck out of reach) must not soft-lock the quest: the fragment that
        /// spawns a wave WAITS (Utility.Wait) and sets the wave's stage anyway after `stuck` seconds (his infestation
        /// driver's 180 s), and the hook's TurnOffStageDone stops a late kill from setting it twice. ⛔ NOT a timer:
        /// StartTimer is selfonly and a fragment script is not attached to the quest as a script, so its OnTimer never
        /// fired (his play of 09a, 2026-10-12: the reveal never showed; so the stuck guard had never worked either).
        /// Vanilla never puts OnTimer in a fragment; its one fragment timer calls a method on the quest's ATTACHED script
        /// (QF_RI06, kmyQuest).
        ///
        /// Moved here from GradeBeats' hold block and BuildBeats' hold blocks (split step (d3), 2026-10-15), unchanged.
        /// What stays where it was: the step's SHAPE (Step.Waves and WaveStages, one stage per wave, set in StepsOf and
        /// StagesOf for the lint and the build alike, and the counter global and "(n/N)" text the Counters and Objectives
        /// passes give it beside a group's); `defend`, a rule between beats (GradeBeats; Slots, after the returns are
        /// filled); every build dropping the previous wave package (NoDriver, hold or not); and the rules over sets of
        /// types, by name until the last step (leaves no object, a fight on the ring, TakesWaves, the wave and travel
        /// lookups in ResolveInputs).
        /// </summary>
        private sealed class HoldBeat : BeatKind
        {
            internal override string Name => "hold";

            // A wave's style -> duo_delve_lib.SpawnWave's origins: 1 anywhere but in front, 2 his left and his right.
            // Exact-case keys on purpose: the lint refuses "Horde" rather than reading it as a squad.
            private static readonly Dictionary<string, int> WaveStyles = new() { ["squad"] = 1, ["horde"] = 2 };

            /// <summary>The stuck-enemy guard's default, in seconds: his infestation driver's pity timer.</summary>
            private const int DefaultStuckSeconds = 180;
            /// <summary>The reveal default, in seconds (his "after like 20 seconds"), and its words (Eliza's placeholder).</summary>
            private const int DefaultRevealSeconds = 20;
            private const string DefaultRevealObjective = "Eliminate the attackers";

            internal override void GradeBeat(Beat b, int i, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                             Action<string> Fatal, Action<string> Warn)
            {
                if (b.waves is not int w || w < 1 || w > 5)
                    Fatal($"beat {i + 1} is a hold and needs \"waves\" from 1 to 5; it has {(b.waves?.ToString() ?? "none")}.");
                if (b.size == null || b.size.Count != 2 || b.size[0] < 1 || b.size[1] < b.size[0])
                    Fatal($"beat {i + 1} is a hold and needs \"size\": [min, max] with 1 <= min <= max; a wave of nobody has no last death, so it never clears.");
                else if (b.size[1] * (b.style?.Max(st => WaveStyles.GetValueOrDefault(st, 1)) ?? 1) is int peak && peak > 12)
                    Warn($"beat {i + 1}'s waves reach {peak} enemies (a horde is size per side, and usually lands on one side); more than 12 at one marker is a crowd the navmesh may not seat.");
                if (b.style != null)
                {
                    if (b.style.Count != (b.waves ?? 0))
                        Fatal($"beat {i + 1} has {b.style.Count} style(s) for {(b.waves?.ToString() ?? "no")} waves; give one per wave, or leave style out for all squads.");
                    foreach (var st in b.style.Where(st => !WaveStyles.ContainsKey(st)))
                        Fatal($"beat {i + 1} has wave style '{st}'; the styles are {string.Join(", ", WaveStyles.Keys)}.");
                }
                if (b.stuck is int s && s < 30)
                    Fatal($"beat {i + 1}'s stuck is {s} s; the guard would move the quest on mid-fight. 30 or more.");
                if (b.reveal is int rv && (rv < 0 || (rv > 0 && rv >= (b.stuck ?? DefaultStuckSeconds))))
                    Fatal($"beat {i + 1}'s reveal is {rv} s; it must be 0 (never) or less than the stuck guard ({b.stuck ?? DefaultStuckSeconds} s), or the wave is let go before it is pointed out.");
                if (b.reveal == 0 && b.revealObjective != null)
                    Fatal($"beat {i + 1} sets revealObjective with reveal 0, so the text would never show.");
                if (b.revealObjective is string ro && Tokens(ro).Any())
                    Fatal($"beat {i + 1}'s revealObjective carries a <Token>; keep it plain.");
                if ((b.waves ?? 0) > 9)
                    Fatal($"beat {i + 1} has {b.waves} waves; each revealed wave takes an objective index after the hold's, and there are 9.");
                if (b.model != null || b.name != null || b.replace)
                    Fatal($"beat {i + 1} is a hold: it has no object of its own (the waves come to a marker), so model, name and replace would be ignored.");
                if (b.objective is string ho && ho.Contains('('))
                    Warn($"beat {i + 1}'s objective has a bracket; the build appends the counter \"(n/{b.waves})\" itself.");
            }

            /// <summary>
            /// The waves are drawn from the removed driver's GangMembers, and copied from a source collection alias and a
            /// source package. Both checks were NoDriver's own until (d3), either side of the kinds' preflight loop.
            /// </summary>
            internal override int Preflight(BeatsBuild build)
            {
                if (build.gangKey == null)
                { Console.WriteLine($"REFUSED: a hold's waves are drawn from the removed driver's GangMembers, and '{build.t.replacesDriver}' lacks it."); return 1; }
                if (build.inputs.WaveSrc == null || build.inputs.TravelSrc == null)
                { Console.WriteLine("REFUSED: a hold needs a source collection alias and a source package to copy its waves from, and one was not handed in."); return 1; }
                return 0;
            }

            /// <summary>
            /// The waves come FOR the player: every wave collection wears the Delve's one Travel-to-player package,
            /// BeatsBuild.EnsureTravelPackage (moved there 2026-10-21 so a support team wears it too).
            /// </summary>
            internal override int Prepare(BeatsBuild build) => build.EnsureTravelPackage();

            /// <summary>
            /// A marker the waves spawn at, and one EMPTY collection per wave, each a copy of vanilla's ActiveHostiles
            /// (Optional, AllowDisabled, no fill), renamed and renumbered, wearing the waves' package. Filled at runtime by
            /// PlaceAtMe's akAliasToFill, as vanilla's MissionBoardCargoContainerScript does. The objective target is left
            /// for Slots to point at a defended beat's object, once the returns are filled.
            /// </summary>
            internal override int Slot(BeatsBuild build, int i)
            {
                var hm = build.NewMarker(i, "Spawn");
                build.slotOf[i] = new BeatSlot { markerAlias = (int)hm.ID, activatorAlias = -1, journalStage = build.beatStage[i] };
                build.waveAliases[i] = new List<int>();
                int nw = build.steps.First(s => s.Beats.Contains(i)).Waves;
                for (int w = 0; w < nw; w++)
                {
                    uint cid = NextAliasId(build.clone);
                    var col = EmptyCollection(build.inputs.WaveSrc!, cid, $"DelveBeat{i + 1}Wave{w + 1}");
                    if (col == null) { Console.WriteLine("REFUSED: the source collection carries a fill; a wave must start empty."); return 1; }
                    col.Collection[0].ReferenceAlias!.PackageData.Add(build.travelPkg.ToLink<IPackageGetter>());
                    build.clone.Aliases.Add(col);
                    build.waveAliases[i].Add((int)cid);
                    build.bm.Waves.Add((int)cid);
                }
                Console.WriteLine($"  +slot    : beat {i + 1} (hold) -- spawn marker alias {hm.ID}, wave collections {string.Join(", ", build.waveAliases[i])} (EMPTY, copies of ActiveHostiles)");
                return 0;
            }

            /// <summary>
            /// The revealed waves (his ask 2026-10-12): one objective per wave, index hold objective + 1 + w, targeting that
            /// wave's own collection. Not shown at the start: the fragment shows it when the reveal timer fires and hides it
            /// when the wave is down, so a cleared wave's bodies never keep a marker.
            /// </summary>
            internal override int Objectives(BeatsBuild build, int k, int b, QuestObjective source)
            {
                var beat = build.r.beats[b];
                if ((beat.reveal ?? DefaultRevealSeconds) == 0) return 0;
                string rtext = beat.revealObjective ?? DefaultRevealObjective;
                var waves = build.waveAliases[b];
                for (int w = 0; w < waves.Count; w++)
                {
                    int ri = build.steps[k].Objective + 1 + w;
                    if (build.clone.Objectives!.Any(o => o.Index == ri)) { Console.WriteLine($"REFUSED: objective {ri} for wave {w + 1}'s reveal is already in use."); return 1; }
                    var ro = source.DeepCopy(); ro.Index = (ushort)ri;
                    var rt = ro.Targets![0].DeepCopy();
                    rt.AliasID = waves[w];
                    rt.Conditions.Clear();
                    ro.Targets.Clear();
                    ro.Targets.Add(rt);
                    ro.DisplayText = rtext;
                    build.clone.Objectives.Add(ro);
                    build.bm.ObjectiveText[ri] = rtext;
                    Console.WriteLine($"  objective: {ri} \"{rtext}\" -> wave {w + 1}'s collection {waves[w]} (shown {beat.reveal ?? DefaultRevealSeconds} s after it spawns)");
                }
                return 0;
            }

            /// <summary>
            /// One stock death hook per wave, on that wave's collection: when every member is dead it sets the wave's stage,
            /// armed only once the wave before it is down. TurnOffStageDone is the wave's own stage, so a kill after the
            /// stuck guard moved on cannot set it twice.
            /// </summary>
            internal override int Hook(BeatsBuild build, int b, int prereq)
            {
                var ws = build.steps.First(s => s.Beats.Contains(b)).WaveStages;
                for (int w = 0; w < ws.Count; w++)
                {
                    int col = build.waveAliases[b][w];
                    int pre = w == 0 ? prereq : ws[w - 1];
                    var hs = StageHook("DefaultCollectionAliasOnDeath", ws[w], pre,
                                       new ScriptIntProperty { Name = "TurnOffStageDone", Data = ws[w], Flags = ScriptProperty.Flag.Edited },
                                       RemoveDeadFromCollection());
                    AttachHook(build.vma, build.clone.FormKey, col, hs, HookEntry.New, false, build.bm, ws[w], pre);
                    Console.WriteLine($"  +hook    : beat {b + 1} (hold) wave {w + 1} collection {col} {hs.Name} sets {ws[w]} after {pre}");
                }
                return 0;
            }

            /// <summary>The first wave comes when the step begins, ahead of any fight's spawn or recover's holder.</summary>
            internal override int EnterRank => 0;

            internal override void Enter(BeatsBuild build, int b, int stage) => SpawnWave(build, b, 0, stage);

            /// <summary>
            /// Each wave down: the counter ticks (and redisplays the objective, which is the player's only feedback: no
            /// message boxes in this type, his ruling), then the next wave comes.
            /// </summary>
            internal override void StageLines(BeatsBuild build, int k, int b)
            {
                var s = build.steps[k];
                for (int w = 0; w < s.WaveStages.Count; w++)
                {
                    build.Add(s.WaveStages[w], $"ModObjectiveGlobal(1.0, Counter{k + 1}, {s.Objective}, {s.Waves}.0)");
                    if (w + 1 < s.WaveStages.Count) SpawnWave(build, b, w + 1, s.WaveStages[w]);
                }
            }

            /// <summary>
            /// Wave w (0-based) appears round him, INTO its own collection. Then the spawning fragment WAITS, at its very
            /// end (a tail, after every other line of that stage): the reveal after `reveal` seconds, the stuck guard after
            /// `stuck`, each only while the wave is still up. Utility.Wait is latent, so the script is free for the next
            /// stage while it sleeps; stage 100 already waits on its radio line this way, and he has heard that one finish.
            /// </summary>
            private static void SpawnWave(BeatsBuild build, int b, int w, int stage)
            {
                var r = build.r;
                var ws = build.steps.First(s => s.Beats.Contains(b)).WaveStages;
                string mk = $"Alias_Beat{b + 1}Spawn", wv = $"Alias_Beat{b + 1}Wave{w + 1}";
                build.props.Add(("ReferenceAlias", mk, build.clone.FormKey, build.slotOf[b].markerAlias));
                build.props.Add(("RefCollectionAlias", wv, build.clone.FormKey, build.waveAliases[b][w]));
                build.props.Add(("FormList", "Gang", build.gangKey!.Value, -1));
                var sz = r.beats[b].size!;
                int origins = WaveStyles[r.beats[b].style?[w] ?? "squad"];
                build.Add(stage, $"duo_delve_lib.SpawnWave({mk}, {wv}, Gang, {sz[0]}, {sz[1]}, {origins})");
                int stuck = r.beats[b].stuck ?? DefaultStuckSeconds;
                int rv = r.beats[b].reveal ?? DefaultRevealSeconds;
                build.Tail(stage, $"; wave {w + 1}: {(rv > 0 ? $"pointed out after {rv} s, " : "")}let go after {stuck} s if it is still up");
                if (rv > 0)
                {
                    var hs = build.steps.First(s => s.Beats.Contains(b));
                    int robj = hs.Objective + 1 + w;
                    build.Add(ws[w], $"SetObjectiveDisplayed({robj}, False)   ; wave {w + 1} is down: its marker goes");
                    build.Tail(stage, $"Utility.Wait({rv}.0)");
                    build.Tail(stage, $"If !GetStageDone({ws[w]})");
                    build.Tail(stage, $"    SetObjectiveDisplayed({robj})");
                    build.Tail(stage, $"    Utility.Wait({stuck - rv}.0)");
                    build.Tail(stage, $"    If !GetStageDone({ws[w]})");
                    build.Tail(stage, $"        SetStage({ws[w]})");
                    build.Tail(stage, "    EndIf");
                    build.Tail(stage, "EndIf");
                    build.bm.Reveals.Add((robj, build.waveAliases[b][w], rv, ws[w]));
                }
                else
                {
                    build.Tail(stage, $"Utility.Wait({stuck}.0)");
                    build.Tail(stage, $"If !GetStageDone({ws[w]})");
                    build.Tail(stage, $"    SetStage({ws[w]})");
                    build.Tail(stage, "EndIf");
                }
                build.bm.StuckWaits.Add(ws[w]);
            }
        }
    }
}
