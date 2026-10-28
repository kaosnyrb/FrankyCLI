using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// defeat: a target appears at the beat's place when the player comes NEAR it (FightBeat.Enter), with an optional
        /// gang who need not die; the step is done when the target dies (the Bounty driver's boss, spike
        /// 2026-10-14-defeat-and-clear-beats). Until he is there the objective points at the marker, as a clear's does.
        /// Near since 2026-10-22 (his "Make a note that we want to fix the spawn-when-near stuff", 10-21); placed when the
        /// step began before that, which on a far site put them over ground that was not loaded.
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
                    if (b.gang.pods != null) Fatal($"{at} gang sets pods; drop pods are built for a clear's enemies only (a defeat's gang is placed beside its target).");
                    List("gang", b.gang.list, true);
                    Range("gang", b.gang, 0);
                }
            }

            /// <summary>
            /// recover's shape without the item: a marker, and an EMPTY Optional alias the target is placed into when the
            /// player comes near, which the objective targets and the death hook sits on. The marker is the site target until then.
            /// </summary>
            internal override int Slot(BeatsBuild build, int i)
            {
                var dm = build.FightMarker(i, "Marker");
                uint did = NextAliasId(build.clone);
                var dt = build.markerPattern.DeepCopy(); dt.ID = did; dt.Name = "DelveBeat" + (i + 1) + "Target";
                dt.Location = null;
                dt.Flags = QuestReferenceAlias.Flag.Optional;
                build.clone.Aliases.Add(dt);
                build.slotOf[i] = new BeatSlot { markerAlias = (int)dm.ID, activatorAlias = -1, targetAlias = (int)dt.ID, siteAlias = (int)dm.ID, journalStage = build.beatStage[i] };
                build.bm.Holders.Add((int)dt.ID);
                build.spawnedStage[i] = SpawnedStageBase + i;
                Console.WriteLine($"  +slot    : beat {i + 1} (defeat) -- marker alias {dm.ID}, target {dt.ID} (EMPTY, Optional; filled on spawn), site target {dm.ID} until stage {build.spawnedStage[i]}");
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
