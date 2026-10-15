using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Linq;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// A FIGHT: defeat (a target, and an optional gang who need not die) or clear (enemies, every one must die). What
        /// the two share: no object of their own, every list named in the recipe (his 2026-10-14: lists from the recipe,
        /// not off a removed driver), a death hook that ends the step, optional loot, and a spawn when the step begins.
        /// Moved here from GradeFight and BuildBeats' fight blocks (split step (d1), 2026-10-15), unchanged.
        /// </summary>
        private abstract class FightBeat : BeatKind
        {
            /// <summary>
            /// Everything is named in the recipe, and every list must resolve and hold something, or the step would spawn
            /// nobody and could never end.
            /// </summary>
            internal override void GradeBeat(Beat b, int i, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                             Action<string> Fatal, Action<string> Warn)
            {
                string at = $"beat {i + 1} ({b.type})";
                if (b.model != null || b.name != null || b.replace || b.item != null || b.returnTo != null || b.group != null || b.choose != null)
                    Fatal($"{at} sets model, name, replace, item, returnTo, group or choose; a fight has no object of its own (the enemies are its object), so they would be silently ignored.");
                void List(string where, string? edid, bool actors)
                {
                    var fl = OwnOrMaster<IFormListGetter>(Allowed(env, t), env, edid ?? "");
                    if (fl == null) Fatal($"{at} {where}.list '{edid}' is not a FormList in {t.mod} or one of its masters.");
                    else if (fl.Items.Count == 0) Fatal($"{at} {where}.list '{edid}' is EMPTY, so it would spawn nothing.");
                    else Console.WriteLine($"  {at} {where}: {fl.EditorID} ({fl.Items.Count} entries)");
                }
                void Range(string where, Crowd c, int lo)
                {
                    if (c.min < lo || c.max < c.min) Fatal($"{at} {where} needs {lo} <= min <= max; it has {c.min}-{c.max}.");
                    else if (c.max > 12) Warn($"{at} {where}.max is {c.max}; more than 12 at one marker is a crowd the navmesh may not seat.");
                    if (c.species != null && !SpeciesModes.Contains(c.species))
                        Fatal($"{at} {where}.species is '{c.species}'; it is \"one\" (a single entry from the list for all) or \"mix\".");
                }
                GradeWho(b, at, t, env, Fatal, List, Range);
                if (b.loot != null)
                {
                    List("loot", b.loot.list, false);
                    if (b.loot.chance < 1 || b.loot.chance > 100) Fatal($"{at} loot.chance is {b.loot.chance}; a percent from 1 to 100.");
                }
            }

            /// <summary>Who this fight is against: a defeat's target and gang, a clear's enemies.</summary>
            protected abstract void GradeWho(Beat b, string at, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                             Action<string> Fatal, Action<string, string?, bool> List, Action<string, Crowd, int> Range);

            /// <summary>The stock death hook that ends the step, and its properties beyond StageToSet and PrereqStage.</summary>
            protected abstract string DeathScript { get; }
            protected abstract ScriptProperty[] DeathProperties(int stage);

            /// <summary>
            /// The step ends on a death: the target's (defeat), or the last of the collection's (clear). Stock hooks;
            /// TurnOffStageDone on the collection's, as each hold wave's, so a late kill cannot set it twice.
            /// </summary>
            internal override int Hook(BeatsBuild build, int b, int prereq)
            {
                int on = build.slotOf[b].targetAlias;
                var fs = StageHook(DeathScript, build.beatStage[b], prereq, DeathProperties(build.beatStage[b]));
                AttachHook(build.vma, build.clone.FormKey, on, fs, HookEntry.New, false, build.bm, build.beatStage[b], prereq);
                Console.WriteLine($"  +hook    : beat {b + 1} ({Name}) alias {on} {fs.Name} sets {build.beatStage[b]} after {prereq}");
                return 0;
            }

            internal override int EnterRank => 1;

            internal override void Enter(BeatsBuild build, int b, int stage)
            {
                var fb = build.r.beats[b];
                var ff = build.inputs.Fights[b];
                string lootArgs = "None, 0";
                if (ff.Loot is FormKey lk) { build.props.Add(("FormList", $"Loot{b + 1}", lk, -1)); lootArgs = $"Loot{b + 1}, {fb.loot!.chance}"; }
                Spawn(build, b, stage, fb, ff, lootArgs);
            }

            /// <summary>The spawn call itself: SpawnTarget for a defeat, SpawnGroup for a clear.</summary>
            protected abstract void Spawn(BeatsBuild build, int b, int stage, Beat fb, FightForms ff, string lootArgs);
        }

        /// <summary>A fight beat (defeat or clear).</summary>
        private static bool IsFight(Beat b) => KindOf(b) is FightBeat;
    }
}
