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
                if (b.elite != null)
                {
                    if (b.target != null) Fatal($"{at} sets target AND elite; an elite IS the target (made from elite.from), so name one.");
                    if (string.IsNullOrWhiteSpace(b.elite.name)) Fatal($"{at} elite has no name; an elite is named on its record, and the naming rules are blanked.");
                    if (string.IsNullOrWhiteSpace(b.elite.from)) Fatal($"{at} elite has no from: the creature it is made from.");
                    else if (OwnOrMaster<INpcGetter>(Allowed(env, t), env, b.elite.from!) is not INpcGetter src)
                        Fatal($"{at} elite.from '{b.elite.from}' is not an Npc in {t.mod} or one of its masters (a LeveledNpc cannot be cloned).");
                    else
                        Console.WriteLine($"  {at} elite: \"{b.elite.name}\" from {src.EditorID} ({src.Name?.String}), level {(src.Level as INpcLevelGetter)?.Level.ToString() ?? "levelled"}: challenge VeryHard, + Special_Boss, size up one, no template retarget");
                }
                else if (string.IsNullOrWhiteSpace(b.target)) Fatal($"{at} has no target: nobody whose death ends the step.");
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

            /// <summary>
            /// Each elite defeat's own creature, `<id>_b<N>elite`: the previous build's removed first (named for the Delve, so a
            /// rebuild sweeps it), the source DUPLICATED (ObjectTemplates and all; CloneNPC copies field by field), then
            /// vanilla's alpha swaps. The fight's target becomes the new record; SpawnTarget is unchanged.
            /// </summary>
            internal override int Prepare(BeatsBuild build)
            {
                RemovePrevious(build.myMod.Npcs, e => e.StartsWith(build.r.id + "_b") && e.EndsWith("elite"));
                var k = build.inputs.EliteKit;
                for (int i = 0; i < build.r.beats.Count; i++)
                {
                    var fb = build.r.beats[i];
                    if (fb.type != "defeat" || fb.elite == null) continue;
                    var ff = build.inputs.Fights[i];
                    var npc = build.myMod.Npcs.DuplicateInAsNewRecord(ff.EliteSrc!);
                    npc.EditorID = $"{build.r.id}_b{i + 1}elite";
                    npc.Name = fb.elite.name!;
                    npc.Aggression = Npc.AggressionType.VeryAggressive;
                    npc.Confidence = Npc.ConfidenceType.Foolhardy;
                    npc.Keywords ??= new Noggog.ExtendedList<IFormLinkGetter<IKeywordGetter>>();
                    if (!npc.Keywords.Any(x => x.FormKey == k!.Blank)) npc.Keywords.Add(new FormLink<IKeywordGetter>(k!.Blank));
                    if (npc.ObjectTemplates == null || npc.ObjectTemplates.Count == 0)
                    { Console.WriteLine($"REFUSED: beat {i + 1} elite.from {ff.EliteSrc!.EditorID} has no object template, so there is nothing to make elite."); return 1; }
                    var log = new List<string>();
                    foreach (var ot in npc.ObjectTemplates)
                    {
                        bool challenge = false, size = false, boss = false;
                        foreach (var inc in ot.Includes)
                        {
                            var m = inc.Mod.FormKey;
                            if (k!.Challenges.Contains(m)) { if (m != k.VeryHard) log.Add("challenge -> VeryHard"); inc.Mod.SetTo(k.VeryHard); challenge = true; }
                            else if (k.Sizes.IndexOf(m) is int si && si >= 0) { var up = k.Sizes[Math.Min(si + 1, k.Sizes.Count - 1)]; if (up != m) log.Add($"size up one ({si} -> {k.Sizes.IndexOf(up)})"); inc.Mod.SetTo(up); size = true; }
                            else if (m == k.Boss) boss = true;
                        }
                        ObjectTemplateInclude Add(FormKey fk) => new ObjectTemplateInclude
                        { Mod = fk.ToLink<IAObjectModificationGetter>(), AttachPointIndex = 0, DontUseAll = true, Optional = true };
                        if (!challenge) { ot.Includes.Add(Add(k!.VeryHard)); log.Add("+ challenge VeryHard"); }
                        if (!size) { ot.Includes.Add(Add(k!.Large)); log.Add("+ size Large"); }
                        if (!boss) { ot.Includes.Add(Add(k!.Boss)); log.Add("+ Special_Boss"); }
                    }
                    ff.Target = npc.FormKey;
                    Console.WriteLine($"  +elite   : {npc.EditorID} {npc.FormKey} \"{npc.Name}\" from {ff.EliteSrc!.EditorID}: {string.Join(", ", log.Distinct())}; VeryAggressive, Foolhardy, name blanked from the rules");
                }
                return 0;
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
