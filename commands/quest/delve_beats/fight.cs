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
                if (t.playerAlias < 0) Fatal($"template '{t.id}' names no playerAlias, and a fight spawns when the player comes near its marker.");
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
                if (s.near is int n && (n < 100 || n > 1000)) Fatal($"{at} support.near is {n}; 100 to 1000 (distance to the landing marker, absent = 300).");
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
                // team goes to the FIGHT, aimed near the beat's marker alias (where the clear's enemies and the defeat's target
                // spawn), with no condition, so they go whatever the player does: its own copy of the travel package (vanilla's
                // landed-crew shape, OE_KT_LandedShip_TravelPKG: LocationFallback AliasForReference). Then, on arriving: "Once the
                // marines reach the marker they huddle there doing nothing ... Maybe even just stopping the travel package". A
                // copy of vanilla's OE_Sandbox_20m_Run_Alert_Package was tried and they went "very chill, don't seem to want to
                // engage in combat" (his play, 2026-10-21), where on this Travel they had fought ("yup they got out and fought").
                // So the Travel stays, and duo_delve_landing takes each Marine OUT of the team as he reaches the fight: the package
                // stops and his own AI (DefaultMasterPackageList_Human; Aggressive, Brave) takes over.
                int marker = build.slotOf[i].markerAlias;
                var pk = build.myMod.Packages.DuplicateInAsNewRecord(build.inputs.TravelSrc!);
                pk.EditorID = $"{build.r.id}{SupportPackagePrefix}{i + 1}";
                pk.VirtualMachineAdapter = null;
                pk.OwnerQuest.SetTo(build.clone.FormKey);
                pk.Conditions.Clear();
                pk.PreferredSpeed = Package.Speed.Run;
                var loc = pk.Data.Values.OfType<PackageDataLocation>().FirstOrDefault();
                if (loc?.Location is not LocationTargetRadius src)
                { Console.WriteLine($"REFUSED: {build.inputs.TravelSrc!.EditorID} has no location input to aim at the fight."); return 1; }
                var radius = src.Radius;
                loc.Location = new LocationTargetRadius { Target = new LocationFallback { Type = LocationTargetRadius.LocationType.AliasForReference, Data = marker }, Radius = radius };
                uint cid = NextAliasId(build.clone);
                var col = EmptyCollection(build.inputs.WaveSrc!, cid, $"DelveBeat{i + 1}Support");
                if (col == null) { Console.WriteLine("REFUSED: the source collection carries a fill; a support team must start empty."); return 1; }
                col.Collection[0].ReferenceAlias!.PackageData.Add(pk.ToLink<IPackageGetter>());
                build.clone.Aliases.Add(col);
                build.supportOf[i] = (int)cid;
                build.bm.SupportTeams.Add(((int)cid, pk.FormKey, marker, $"{radius}", build.inputs.TravelSrc!.PackageTemplate.FormKey));
                Console.WriteLine($"  +support : beat {i + 1} team collection {cid} (EMPTY, a copy of ActiveHostiles) wearing {pk.EditorID} {pk.FormKey}: Travel near alias {marker}, the fight, at Run, no condition; each leaves the team on arriving");
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

            /// <summary>
            /// Nothing is placed when the step begins. duo_delve_near (our copy of the stock DefaultAliasOnDistanceLessThan) on the fight's marker, armed by
            /// the step's stage, sets the beat's SPAWNED stage when the player comes within ApproachDistance, and the spawn
            /// call runs in THAT stage's fragment. Until then the objective points at the marker; after, at who spawned.
            /// WHY NEAR, his play of 33a, 2026-10-21: "The spacers are spawned from a distance, which reads bad on the map and
            /// puts them in mid air. Spawn them when the player gets close". WHY THE ENGINE'S EVENT, his 2026-10-22 on the
            /// once-a-second timer that did this first (duo_delve_groupnear): "that won't scale very well". The event costs
            /// nothing until it fires. ⚠ It measures in 3D where the timer measured flat; the marker is a real one of the
            /// place, so its height is true. ⚠ Unmeasured: whether it fires at once if he is ALREADY inside the distance
            /// when the step begins (ScriptObject.psc does not say).
            /// </summary>
            internal override void Enter(BeatsBuild build, int b, int stage)
            {
                var fb = build.r.beats[b];
                var ff = build.inputs.Fights[b];
                string lootArgs = "None, 0";
                if (ff.Loot is FormKey lk) { build.props.Add(("FormList", $"Loot{b + 1}", lk, -1)); lootArgs = $"Loot{b + 1}, {fb.loot!.chance}"; }
                int marker = build.slotOf[b].markerAlias, spawned = build.spawnedStage[b];
                // Our own copy of the stock script (papyrus/duo_delve_near.psc), never the stock name: a board's step-one
                // marker already wears DefaultAliasOnDistanceLessThan for the arrival, and an alias carries a script once.
                var near = DistanceHook(build.clone.FormKey, build.t.playerAlias, spawned, stage);
                near.Name = NearScript;
                AttachHook(build.vma, build.clone.FormKey, marker, near, HookEntry.FindOrCreate, false, build.bm, spawned, stage);
                build.bm.SpawnHooks.Add((marker, build.t.playerAlias));
                Console.WriteLine($"  +near    : beat {b + 1} ({Name}) marker alias {marker} {NearScript} sets {spawned} after {stage}: spawned there when the player is within {ApproachDistance}");
                Spawn(build, b, spawned, fb, ff, lootArgs);
                if (fb.support is Support s)
                {
                    // Armed when the step begins; the ship is called when the player comes near (duo_delve_landing).
                    string la = $"Alias_Beat{b + 1}Landing";
                    build.props.Add(("ReferenceAlias", la, build.clone.FormKey, build.landingOf[b]));
                    build.props.Add(("Form", $"Ship{b + 1}", ff.Ship!.Value, -1));
                    build.props.Add(("FormList", $"Support{b + 1}", ff.Support!.Value, -1));
                    build.props.Add(("RefCollectionAlias", $"Alias_Beat{b + 1}Support", build.clone.FormKey, build.supportOf[b]));
                    // The fight's marker, where the team's Travel ends: the script takes each one out of the team there.
                    build.props.Add(("ReferenceAlias", $"Alias_Beat{b + 1}Fight", build.clone.FormKey, build.slotOf[b].markerAlias));
                    build.Add(stage, $"({la} as {LandingScript}).Arm(Ship{b + 1}, Support{b + 1}, {s.min}, {s.max}, {s.near ?? DefaultLandingNear}.0, Alias_Beat{b + 1}Support, Alias_Beat{b + 1}Fight)");
                }
            }

            /// <summary>The spawn call itself, written into the SPAWNED stage's fragment (<paramref name="stage"/>):
            /// SpawnTarget for a defeat, SpawnGroup or DropPods for a clear.</summary>
            protected abstract void Spawn(BeatsBuild build, int b, int stage, Beat fb, FightForms ff, string lootArgs);
        }

        /// <summary>A fight beat (defeat or clear).</summary>
        private static bool IsFight(Beat b) => KindOf(b) is FightBeat;

        /// <summary>
        /// A beat whose marker is only where people spawn (defeat, clear, hold), placing no object there, so it may share an
        /// earlier beat's spot: the three kinds that take their marker through FightMarker, and the lint's note says so.
        /// </summary>
        private static bool SpawnsAtMarker(Beat b) => KindOf(b) is FightBeat or HoldBeat;
    }
}
