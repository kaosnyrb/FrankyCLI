using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// A BEAT TYPE, as one object: its word in a recipe and everything the lint and the build do for that word (split
        /// step 3's (d), 2026-10-15, his "yeah interface, we'll have loads of beats"). One file per type in this folder,
        /// so a new beat type is one new file and one row in BeatKinds, never a hunt through every section.
        ///
        /// What a kind owns is ITS OWN beat: its rules on one beat, its aliases, its stock hook, what it spawns when its
        /// step begins. What it does not own stays where it is: rules BETWEEN beats (a deliver needs a carried pickup
        /// before it; an ending is a deliver) in GradeBeats; the passes' loops, their order and the state shared across
        /// beats in BeatsBuild; the read-back in VerifyBeats, which checks record by record.
        /// </summary>
        private abstract class BeatKind
        {
            /// <summary>The beat's `type` in a recipe.</summary>
            internal abstract string Name { get; }

            /// <summary>This type's own rules on one beat, in the lint. Fatal and Warn are Grade's.</summary>
            internal virtual void GradeBeat(Beat b, int i, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                            Action<string> Fatal, Action<string> Warn) { }

            /// <summary>A precondition on the whole build, checked in NoDriver before anything is changed. Non-zero refuses.</summary>
            internal virtual int Preflight(BeatsBuild build) => 0;

            /// <summary>
            /// A build action of the kind's own, once per build that uses it (first-use order), in NoDriver after every
            /// precondition has passed and before the driver is removed: a hold clones its waves' package. Non-zero refuses.
            /// </summary>
            internal virtual int Prepare(BeatsBuild build) => 0;

            /// <summary>The beat's aliases, in the Slots pass (beat order). Non-zero refuses.</summary>
            internal abstract int Slot(BeatsBuild build, int i);

            /// <summary>
            /// The beat's own objectives beyond its step's one, in the Objectives pass once every step's objective is
            /// written and before they are sorted (step order, then beat order): a hold's revealed waves. `source` is the
            /// objective each new one is copied from. Non-zero refuses.
            /// </summary>
            internal virtual int Objectives(BeatsBuild build, int k, int b, QuestObjective source) => 0;

            /// <summary>
            /// The beat's own dialogue records, at the end of the Messages pass once every person's NPC is cloned (beat
            /// order): a talk points its person's alias at his NPC and builds its conversation.
            /// </summary>
            internal virtual void Dialogue(BeatsBuild build, int b) { }

            /// <summary>The beat's stock hook, in the Hooks pass (step order, then beat order). Non-zero refuses.</summary>
            internal abstract int Hook(BeatsBuild build, int b, int prereq);

            /// <summary>
            /// Which pass of a step's ENTRY the beat appears in, because the fragment's lines are emitted pass by pass, not
            /// beat by beat: 0 a hold's first wave, 1 a fight's spawn, 2 a recover's holder. -1: nothing at entry.
            /// </summary>
            internal virtual int EnterRank => -1;

            /// <summary>What appears when the beat's step begins, as fragment lines on `stage`.</summary>
            internal virtual void Enter(BeatsBuild build, int b, int stage) { }

            /// <summary>
            /// The beat's own fragment lines on its own stages, in the Fragment pass's walk of the steps (step order, then
            /// beat order; after the beat's message, the items it hands over and an ending's lines, before a group's
            /// counter): a hold's counter tick and next wave on each wave's stage. `k` is the beat's step.
            /// </summary>
            internal virtual void StageLines(BeatsBuild build, int k, int b) { }
        }

        /// <summary>
        /// The beat types, as kinds: every type in BeatTypes has one since split step (d5). ⚠ STILL MID-MIGRATION: BeatTypes
        /// stays the lint's vocabulary, and the rules over SETS of types still name them, until the last step turns those
        /// into kind properties and deletes BeatTypes. `gen_delve selftest` holds the two lists equal, both ways.
        /// </summary>
        private static readonly Dictionary<string, BeatKind> BeatKinds = new BeatKind[] { new DefeatBeat(), new ClearBeat(), new RecoverBeat(), new HoldBeat(), new TalkBeat(),
                                                                                       new ObjectBeat("use", givesItem: false), new ObjectBeat("pickup", givesItem: true),
                                                                                       new ObjectBeat("deliver", givesItem: false) }
            .ToDictionary(k => k.Name);

        /// <summary>The beat's kind, or null for a type that has none (the lint refuses that).</summary>
        private static BeatKind? KindOf(Beat b) => b.type != null && BeatKinds.TryGetValue(b.type, out var k) ? k : null;

        /// <summary>
        /// The beat's kind in a build, where the lint has already refused an unknown type: a beat with none here is a bug,
        /// so it throws rather than letting a pass skip the beat silently (Slots and Hooks had a default branch that took
        /// every non-kind beat until the object kinds, split step (d5)).
        /// </summary>
        private static BeatKind KindOrThrow(Beat b, int i) =>
            KindOf(b) ?? throw new InvalidOperationException($"beat {i + 1}'s type '{b.type}' passed the lint and has no kind");

        /// <summary>The kinds a recipe uses, each once, in the order its beats first use them.</summary>
        private static IEnumerable<BeatKind> KindsIn(Recipe r) => r.beats.Select(KindOf).OfType<BeatKind>().Distinct();
    }
}
