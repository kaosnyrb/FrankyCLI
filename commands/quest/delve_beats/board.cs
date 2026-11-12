using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Starfield;
using System;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// BOARD A SHIP (plan OT-24, his 2026-11-12 "yeah build the board"): he boards the target ship in orbit and takes
        /// something from inside it. Built ONLY on a one-beat boardspace, by BuildSpace (delve_space.cs, SpaceBoarding), so
        /// this kind is the word for the lint and refuses everywhere else; the beats build never reaches Slot or Hook.
        /// It CARRIES its item: the item is placed inside the ship and picking it up completes the job.
        /// </summary>
        private sealed class BoardBeat : BeatKind
        {
            internal override string Name => "board";
            internal override bool CarriesItem => true;

            internal override void GradeBeat(Beat b, int i, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                             Action<string> Fatal, Action<string> Warn)
            {
                if (!IsBoardSpace(t)) Fatal($"beat {i + 1} is a board on a {t.kind} template; boarding a ship is built on boardspace only (OT-24).");
            }

            internal override int Slot(BeatsBuild build, int i)
            {
                Console.WriteLine($"REFUSED: beat {i + 1} is a board, which only the space build makes; the lint should have refused this template.");
                return 1;
            }

            internal override int Hook(BeatsBuild build, int b, int prereq)
            {
                Console.WriteLine($"REFUSED: beat {b + 1} is a board, which only the space build makes; the lint should have refused this template.");
                return 1;
            }
        }
    }
}
