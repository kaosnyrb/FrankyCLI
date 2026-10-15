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

            /// <summary>The beat's aliases, in the Slots pass (beat order). Non-zero refuses.</summary>
            internal abstract int Slot(BeatsBuild build, int i);

            /// <summary>The beat's stock hook, in the Hooks pass (step order, then beat order). Non-zero refuses.</summary>
            internal abstract int Hook(BeatsBuild build, int b, int prereq);

            /// <summary>
            /// Which pass of a step's ENTRY the beat appears in, because the fragment's lines are emitted pass by pass, not
            /// beat by beat: 0 a hold's first wave, 1 a fight's spawn, 2 a recover's holder. -1: nothing at entry.
            /// </summary>
            internal virtual int EnterRank => -1;

            /// <summary>What appears when the beat's step begins, as fragment lines on `stage`.</summary>
            internal virtual void Enter(BeatsBuild build, int b, int stage) { }
        }

        /// <summary>
        /// The beat types that are kinds. ⚠ MID-MIGRATION: until every type in BeatTypes has a kind here, BeatTypes stays
        /// the lint's vocabulary and the sections still branch on the rest by name; the step that moves the last one
        /// deletes BeatTypes. `gen_delve selftest` holds every kind to a name BeatTypes knows.
        /// </summary>
        private static readonly Dictionary<string, BeatKind> BeatKinds = new BeatKind[] { new DefeatBeat(), new ClearBeat() }
            .ToDictionary(k => k.Name);

        /// <summary>The beat's kind, or null for a type that has none yet (or none at all: the lint refuses that).</summary>
        private static BeatKind? KindOf(Beat b) => b.type != null && BeatKinds.TryGetValue(b.type, out var k) ? k : null;

        /// <summary>The kinds a recipe uses, each once, in the order its beats first use them.</summary>
        private static IEnumerable<BeatKind> KindsIn(Recipe r) => r.beats.Select(KindOf).OfType<BeatKind>().Distinct();
    }
}
