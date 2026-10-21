using Mutagen.Bethesda;
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
                if (b.support is Support s) GradeSupport(s, b, at, t, env, Fatal, Warn, List);
            }

            /// <summary>
            /// A team who lands by ship. The list, the ship and the landing marker must all resolve, or the arming line would
            /// call nothing down; the marker is demanded of the place like a beat's own (Grade's markers).
            /// </summary>
            private static void GradeSupport(Support s, Beat b, string at, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                             Action<string> Fatal, Action<string> Warn, Action<string, string?, bool> List)
            {
                if (t.kind == "board")
                { Fatal($"{at} has a support team; a board mission runs from the refresh and the landing is not built for it yet (beats only)."); return; }
                List("support", s.list, true);
                if (s.min < 1 || s.max < s.min) Fatal($"{at} support needs 1 <= min <= max; it has {s.min}-{s.max}.");
                else if (s.max > 12) Warn($"{at} support.max is {s.max}; more than 12 off one ship's hatch is a crowd the navmesh may not seat.");
                if (string.IsNullOrWhiteSpace(s.ship)) Fatal($"{at} support has no ship, so nothing would land.");
                else if (OwnOrMaster<IGenericBaseFormGetter>(Allowed(env, t), env, s.ship) is not { } ship)
                    Fatal($"{at} support.ship '{s.ship}' is not a GenericBaseForm (a ship, such as LvlShip_UCSecurity_Transport) in {t.mod} or one of its masters.");
                else Console.WriteLine($"  {at} support ship: {ship.EditorID} [{ship.FormKey}]");
                if (string.IsNullOrWhiteSpace(s.land)) Fatal($"{at} support has no land marker; it names where at the place the ship comes down (a travel ring is open ground).");
                else if (string.Equals(s.land, b.at, StringComparison.OrdinalIgnoreCase))
                    Warn($"{at} support.land is the fight's own marker ({b.at}), so the ship comes down on top of the enemies.");
                if (s.near is int n && (n < 100 || n > 1000)) Fatal($"{at} support.near is {n}; 100 to 1000 (flat distance to the landing marker, absent = 300).");
            }

            /// <summary>
            /// A support team's landing marker: a new marker alias at the beat's place (its fill is written in Fills, beside the
            /// beat's own), carrying LandingScript, which the step's fragment arms.
            /// </summary>
            protected static int SupportSlot(BeatsBuild build, int i)
            {
                if (build.r.beats[i].support == null) return 0;
                if (build.inputs.WaveSrc == null || build.inputs.TravelSrc == null)
                { Console.WriteLine("REFUSED: a support team's collection and package are copied like a hold's waves, and a source was not handed in."); return 1; }
                var lm = build.NewMarker(i, "Landing");
                build.Landing(i, (int)lm.ID);
                // His plays of 33a, 2026-10-21. First: placed at the hatch with nothing to do, "The UC marines didn't get off the
                // ship and just hung out in the landing bay". Then, on the run-to-player package: "they didn't really attack the
                // Spacers tho. For enemy landings this is prob right but friendlys should attack the objectives". So a FRIENDLY
                // team runs to the FIGHT: its own copy of the travel package, aimed near the beat's marker alias (where the
                // clear's enemies and the defeat's target spawn), vanilla's own shape for a landed crew (OE_KT_LandedShip_TravelPKG:
                // LocationFallback AliasForReference), with no condition, so they go whatever the player does.
                int marker = build.slotOf[i].markerAlias;
                var pk = build.myMod.Packages.DuplicateInAsNewRecord(build.inputs.TravelSrc!);
                pk.EditorID = $"{build.r.id}{SupportPackagePrefix}{i + 1}";
                pk.VirtualMachineAdapter = null;
                pk.OwnerQuest.SetTo(build.clone.FormKey);
                pk.Conditions.Clear();
                pk.PreferredSpeed = Package.Speed.Run;
                var loc = pk.Data.Values.OfType<PackageDataLocation>().FirstOrDefault();
                if (loc == null) { Console.WriteLine($"REFUSED: {build.inputs.TravelSrc!.EditorID} has no location input to aim at the fight."); return 1; }
                loc.Location = new LocationTargetRadius { Target = new LocationFallback { Type = LocationTargetRadius.LocationType.AliasForReference, Data = marker } };
                uint cid = NextAliasId(build.clone);
                var col = EmptyCollection(build.inputs.WaveSrc!, cid, $"DelveBeat{i + 1}Support");
                if (col == null) { Console.WriteLine("REFUSED: the source collection carries a fill; a support team must start empty."); return 1; }
                col.Collection[0].ReferenceAlias!.PackageData.Add(pk.ToLink<IPackageGetter>());
                build.clone.Aliases.Add(col);
                build.supportOf[i] = (int)cid;
                build.bm.SupportTeams.Add(((int)cid, pk.FormKey, marker));
                Console.WriteLine($"  +support : beat {i + 1} team collection {cid} (EMPTY, a copy of ActiveHostiles) wearing {pk.EditorID} {pk.FormKey}: Travel near alias {marker}, the fight, at Run, no condition");
                return 0;
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
                if (fb.support is Support s)
                {
                    // Armed when the step begins; the ship is called when the player comes near (duo_delve_landing).
                    string la = $"Alias_Beat{b + 1}Landing";
                    build.props.Add(("ReferenceAlias", la, build.clone.FormKey, build.landingOf[b]));
                    build.props.Add(("Form", $"Ship{b + 1}", ff.Ship!.Value, -1));
                    build.props.Add(("FormList", $"Support{b + 1}", ff.Support!.Value, -1));
                    build.props.Add(("RefCollectionAlias", $"Alias_Beat{b + 1}Support", build.clone.FormKey, build.supportOf[b]));
                    build.Add(stage, $"({la} as {LandingScript}).Arm(Ship{b + 1}, Support{b + 1}, {s.min}, {s.max}, {s.near ?? DefaultLandingNear}.0, Alias_Beat{b + 1}Support)");
                }
            }

            /// <summary>The spawn call itself: SpawnTarget for a defeat, SpawnGroup for a clear.</summary>
            protected abstract void Spawn(BeatsBuild build, int b, int stage, Beat fb, FightForms ff, string lootArgs);
        }

        /// <summary>A fight beat (defeat or clear).</summary>
        private static bool IsFight(Beat b) => KindOf(b) is FightBeat;
    }
}
