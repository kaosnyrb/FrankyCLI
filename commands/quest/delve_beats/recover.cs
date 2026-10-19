using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Starfield;
using System;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// recover: a holder appears at the beat's place when its step begins, carrying the item, with his gang drawn from
        /// the removed driver's list (as delve4's were); the step is done when the item reaches the player's pack, however
        /// it gets there (looted, picked up, handed over). Moved here from BuildBeats' recover blocks (split step (d2),
        /// 2026-10-15), unchanged. Its item is cloned in the Items pass with a pickup's, and counts as carried for a
        /// deliver: those are rules over the item-carrying types, and stay by name until the last step.
        /// </summary>
        private sealed class RecoverBeat : BeatKind
        {
            internal override string Name => "recover";

            /// <summary>The item reaches the player's pack, however it gets there, and is carried from then.</summary>
            internal override bool CarriesItem => true;

            internal override void GradeBeat(Beat b, int i, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                             Action<string> Fatal, Action<string> Warn)
            {
                if (b.model != null || b.name != null || b.replace)
                    Fatal($"beat {i + 1} is a recover: it has no object of its own (the holder carries the item), so model, name and replace would be ignored.");
                if (b.outside != null && (b.outside.Count != 2 || b.outside[0] < 1 || b.outside[1] < b.outside[0]))
                    Fatal($"beat {i + 1} outside must be [min, max] with 1 <= min <= max (distance from its marker), not [{string.Join(", ", b.outside)}].");
            }

            internal override int Preflight(BeatsBuild build)
            {
                if (build.gangKey == null || build.gangMax == null)
                { Console.WriteLine($"REFUSED: a recover beat needs the removed driver's GangMembers and MaxGangMembers, and '{build.t.replacesDriver}' lacks one."); return 1; }
                return 0;
            }

            /// <summary>
            /// A marker where the holder appears, and an EMPTY Optional alias he is placed into, which the objective targets
            /// so it follows him (delve4's shape: a non-optional alias with no fill makes the whole quest silently not start).
            /// </summary>
            internal override int Slot(BeatsBuild build, int i)
            {
                uint rid = NextAliasId(build.clone);
                var rm = build.markerPattern.DeepCopy(); rm.ID = rid; rm.Name = "DelveBeat" + (i + 1) + "Marker";
                var rh = build.markerPattern.DeepCopy(); rh.ID = rid + 1; rh.Name = "DelveBeat" + (i + 1) + "Holder";
                rh.Location = null;
                rh.Flags = QuestReferenceAlias.Flag.Optional;
                build.clone.Aliases.Add(rm);
                build.clone.Aliases.Add(rh);
                build.slotOf[i] = new BeatSlot { markerAlias = (int)rm.ID, activatorAlias = -1, targetAlias = (int)rh.ID, journalStage = build.beatStage[i] };
                build.bm.Holders.Add((int)rh.ID);
                Console.WriteLine($"  +slot    : beat {i + 1} (recover) -- marker alias {rm.ID}, holder {rh.ID} (EMPTY, Optional; filled on spawn)");
                return 0;
            }

            /// <summary>
            /// On the PLAYER: the stage is set however the item reaches the pack. The A-D duplicates exist so one alias can
            /// carry the hook several times, so the suffix counts recovers across the whole build (BeatsBuild.recoversHooked).
            /// </summary>
            internal override int Hook(BeatsBuild build, int b, int prereq)
            {
                int hookAlias = build.t.playerAlias;
                string script = ItemAddedScript(build.recoversHooked);
                build.recoversHooked++;
                var sc = StageHook(script, build.beatStage[b], prereq);
                var fp = new ScriptObjectProperty { Name = "ItemFilter", Flags = ScriptProperty.Flag.Edited };
                fp.Object.SetTo(build.itemOf[b]);
                sc.Properties.Add(fp);
                return build.AttachBeatHook(b, hookAlias, script, sc, prereq);
            }

            /// <summary>
            /// The OnItemAdded hook for the n-th recover the player alias carries (0-based): the base script, then its four
            /// duplicates A-D (the lint allows five recovers). Pure, so `gen_delve selftest` pins it: no recipe has two
            /// recovers, so no build has ever taken a suffix.
            /// </summary>
            internal static string ItemAddedScript(int n) => "DefaultAliasOnItemAddedScript" + (n == 0 ? "" : ((char)('A' + n - 1)).ToString());

            internal override int EnterRank => 2;

            /// <summary>The holder appears (and fills the alias the objective targets) BEFORE the objective is shown.</summary>
            internal override void Enter(BeatsBuild build, int b, int stage)
            {
                string mk = $"Alias_Beat{b + 1}Marker", hd = $"Alias_Beat{b + 1}Holder";
                build.props.Add(("ReferenceAlias", mk, build.clone.FormKey, build.slotOf[b].markerAlias));
                build.props.Add(("ReferenceAlias", hd, build.clone.FormKey, build.slotOf[b].targetAlias));
                build.props.Add(("FormList", "Gang", build.gangKey!.Value, -1));
                build.props.Add(("Form", $"Item{b + 1}", build.itemOf[b], -1));
                var bt = build.r.beats[b];
                if (bt.outside is { } o)
                    build.Add(stage, $"duo_delve_lib.SpawnHolderOutside({mk}, {hd}, Gang, Item{b + 1}, {build.t.gangMin}, {build.gangMax}, {o[0]}.0, {o[1]}.0)");
                else
                    build.Add(stage, $"duo_delve_lib.SpawnHolder({mk}, {hd}, Gang, Item{b + 1}, {build.t.gangMin}, {build.gangMax})");
            }
        }
    }
}
