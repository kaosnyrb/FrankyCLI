using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Starfield;
using System;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// clear: enemies appear at the beat's place when its step begins; the step is done when every one of them is dead
        /// (the Hunt driver). They hold the site rather than come for him: no travel package, unlike a hold's waves.
        /// </summary>
        private sealed class ClearBeat : FightBeat
        {
            internal override string Name => "clear";

            protected override void GradeWho(Beat b, string at, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                              Action<string> Fatal, Action<string, string?, bool> List, Action<string, Crowd, int> Range)
            {
                if (b.target != null || b.gang != null) Fatal($"{at} sets target or gang; a clear has enemies, every one of whom must die (target and gang are a defeat's).");
                if (b.enemies == null) Fatal($"{at} has no enemies: nobody to clear, so the step could never end.");
                else
                {
                    List("enemies", b.enemies.list, true);
                    Range("enemies", b.enemies, 1);
                }
            }

            internal override int Preflight(BeatsBuild build)
            {
                if (build.inputs.WaveSrc == null)
                { Console.WriteLine("REFUSED: a clear's enemies fill a collection copied from vanilla's ActiveHostiles, and none was handed in."); return 1; }
                return 0;
            }

            /// <summary>
            /// ONE hold wave at the site: a marker, and an EMPTY collection (a copy of vanilla's ActiveHostiles) the enemies
            /// are placed into, with NO travel package, so they hold the site rather than come for him.
            /// </summary>
            internal override int Slot(BeatsBuild build, int i)
            {
                var cm = build.NewMarker(i, "Spawn");
                uint cid = NextAliasId(build.clone);
                var col = EmptyCollection(build.inputs.WaveSrc!, cid, $"DelveBeat{i + 1}Enemies");
                if (col == null) { Console.WriteLine("REFUSED: the source collection carries a fill; a clear's group must start empty."); return 1; }
                build.clone.Aliases.Add(col);
                build.slotOf[i] = new BeatSlot { markerAlias = (int)cm.ID, activatorAlias = -1, targetAlias = (int)cid, journalStage = build.beatStage[i] };
                build.bm.ClearGroups.Add((int)cid);
                Console.WriteLine($"  +slot    : beat {i + 1} (clear) -- spawn marker alias {cm.ID}, enemies collection {cid} (EMPTY, a copy of ActiveHostiles, no package)");
                return SupportSlot(build, i);
            }

            protected override string DeathScript => "DefaultCollectionAliasOnDeath";
            protected override ScriptProperty[] DeathProperties(int stage) => new ScriptProperty[]
            {
                new ScriptIntProperty { Name = "TurnOffStageDone", Data = stage, Flags = ScriptProperty.Flag.Edited },
                RemoveDeadFromCollection()
            };

            protected override void Spawn(BeatsBuild build, int b, int stage, Beat fb, FightForms ff, string lootArgs)
            {
                string mk = $"Alias_Beat{b + 1}Spawn", en = $"Alias_Beat{b + 1}Enemies";
                build.props.Add(("ReferenceAlias", mk, build.clone.FormKey, build.slotOf[b].markerAlias));
                build.props.Add(("RefCollectionAlias", en, build.clone.FormKey, build.slotOf[b].targetAlias));
                build.props.Add(("FormList", $"Enemies{b + 1}", ff.Enemies!.Value, -1));
                var e = fb.enemies!;
                build.Add(stage, $"duo_delve_lib.SpawnGroup({mk}, {en}, Enemies{b + 1}, {e.min}, {e.max}, {(e.species == "mix" ? "False" : "True")}, {lootArgs})");
            }
        }
    }
}
