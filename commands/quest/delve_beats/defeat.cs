using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// defeat: a target appears at the beat's place when its step begins, with an optional gang who need not die;
        /// the step is done when the target dies (the Bounty driver's boss, spike 2026-10-14-defeat-and-clear-beats).
        /// </summary>
        private sealed class DefeatBeat : FightBeat
        {
            internal override string Name => "defeat";

            protected override void GradeWho(Beat b, string at, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                              Action<string> Fatal, Action<string, string?, bool> List, Action<string, Crowd, int> Range)
            {
                if (b.enemies != null) Fatal($"{at} sets enemies; a defeat has a target and a gang (enemies is a clear's).");
                if (string.IsNullOrWhiteSpace(b.target)) Fatal($"{at} has no target: nobody whose death ends the step.");
                else if (OwnOrMaster<INpcGetter>(Allowed(env, t), env, b.target!) == null && OwnOrMaster<ILeveledNpcGetter>(Allowed(env, t), env, b.target!) == null)
                    Fatal($"{at} target '{b.target}' is not an Npc or LeveledNpc in {t.mod} or one of its masters.");
                if (b.gang != null)
                {
                    if (b.gang.pods != null) Fatal($"{at} gang sets pods; drop pods are built for a clear's enemies only (a defeat's gang spawns at the step's start, not when he is near).");
                    List("gang", b.gang.list, true);
                    Range("gang", b.gang, 0);
                }
            }

            /// <summary>
            /// recover's shape without the item: a marker, and an EMPTY Optional alias the target is placed into when the
            /// step begins, which the objective targets and the death hook sits on.
            /// </summary>
            internal override int Slot(BeatsBuild build, int i)
            {
                var dm = build.NewMarker(i, "Marker");
                uint did = NextAliasId(build.clone);
                var dt = build.markerPattern.DeepCopy(); dt.ID = did; dt.Name = "DelveBeat" + (i + 1) + "Target";
                dt.Location = null;
                dt.Flags = QuestReferenceAlias.Flag.Optional;
                build.clone.Aliases.Add(dt);
                build.slotOf[i] = new BeatSlot { markerAlias = (int)dm.ID, activatorAlias = -1, targetAlias = (int)dt.ID, journalStage = build.beatStage[i] };
                build.bm.Holders.Add((int)dt.ID);
                Console.WriteLine($"  +slot    : beat {i + 1} (defeat) -- marker alias {dm.ID}, target {dt.ID} (EMPTY, Optional; filled on spawn)");
                return SupportSlot(build, i);
            }

            protected override string DeathScript => "DefaultAliasOnDeath";
            protected override ScriptProperty[] DeathProperties(int stage) => Array.Empty<ScriptProperty>();

            protected override void Spawn(BeatsBuild build, int b, int stage, Beat fb, FightForms ff, string lootArgs)
            {
                string mk = $"Alias_Beat{b + 1}Marker", tg = $"Alias_Beat{b + 1}Target";
                build.props.Add(("ReferenceAlias", mk, build.clone.FormKey, build.slotOf[b].markerAlias));
                build.props.Add(("ReferenceAlias", tg, build.clone.FormKey, build.slotOf[b].targetAlias));
                build.props.Add(("Form", $"Target{b + 1}", ff.Target!.Value, -1));
                string gangArgs = "None, 0, 0, False";
                if (ff.Gang is FormKey gk)
                {
                    build.props.Add(("FormList", $"Gang{b + 1}", gk, -1));
                    gangArgs = $"Gang{b + 1}, {fb.gang!.min}, {fb.gang.max}, {(fb.gang.species == "one" ? "True" : "False")}";
                }
                build.Add(stage, $"duo_delve_lib.SpawnTarget({mk}, {tg}, Target{b + 1}, {gangArgs}, {lootArgs})");
            }
        }
    }
}
