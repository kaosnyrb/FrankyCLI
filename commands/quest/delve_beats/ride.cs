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
        /// ride: the person of an earlier talk beat comes aboard the player's ship and is carried to the beat's place (plan
        /// OT-9H, his 2026-11-03 "I'm leaning go big"). Her side questions stay askable aboard, because a talk's questions
        /// outlive its completion and every gate on them is GetIsID, never a place.
        ///
        /// It is vanilla's own shape, RQ_OE_TransportPerson_02 and RQ_OE_Rescue_01, read 2026-11-04: the stock
        /// DefaultPassengerQuestScript on the quest, its Passengers collection wearing DefaultPassengerPackage, and
        /// AddPassenger from a stage fragment (the script then links her to the ship's marker and she runs to the ship).
        /// Not MissionPassengerQuestScript, the board's own: that one IS the mission base class, and fixes the passengers as
        /// created aboard at the accept, so a person met at a place is not its shape.
        ///
        /// Two stages, both set by the script: the beat's spawned stage when the ship lands or docks inside the place's
        /// location (the journal has nothing to say yet), then the beat's own stage when she has left the ship, which is her
        /// walking out or the ship's interior unloading round her, so a player who never waits for her still finishes.
        /// What it removes when the quest ends: she leaves the ship's list as the ship lands; a turn-down takes her off it.
        /// </summary>
        private sealed class RideBeat : BeatKind
        {
            internal override string Name => "ride";

            private const string PassengerScript = "DefaultPassengerQuestScript";
            /// <summary>MissionQuestScript's FailStage, its default: what a board's turn-down sets (templates.json, _questScript).</summary>
            private const int BoardFailStage = 200;

            // Vanilla's forms, as RQ_OE_TransportPerson_02 carries them.
            private static FormKey Sf(uint id) => new FormKey(ModKey.FromFileName("Starfield.esm"), id);
            private static readonly (string name, uint id)[] PassengerForms =
            {
                ("SQ_PlayerShip", 0x0174A2), ("LinkPassengerShipMarker", 0x1D3029), ("LinkPassengerDestinationMarker", 0x1D302A),
                ("DefaultPassengerState", 0x1D3028), ("DefaultPassengerState_0_WalkToShipThenSandbox", 0x1D3027),
                ("DefaultPassengerState_1_RunToShipThenSandbox", 0x1D3026), ("DefaultPassengerState_2_WalkToDestination", 0x1D3025),
                ("DefaultPassengerState_3_RunToDestination", 0x1D3024),
            };
            private static readonly FormKey PassengerPackage = Sf(0x1D302B);   // DefaultPassengerPackage

            internal override void GradeBeat(Beat b, int i, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                             Action<string> Fatal, Action<string> Warn)
            {
                string at = $"beat {i + 1} (ride)";
                if (!IsBoard(t)) Fatal($"{at} is on a {t.kind} template; a ride is proven on the ground board only.");
                if (b.model != null || b.name != null || b.replace || b.message != null || b.returnTo != null || b.group != null || b.choose != null || b.person != null)
                    Fatal($"{at} sets model, name, replace, message, returnTo, group, choose or person; a ride has none of them (the rider is its person), so they would be silently ignored.");
                if (b.PlaceIndex == 0) Fatal($"{at} is at the main place; a ride carries her somewhere, so it needs \"place\": \"second\" or \"third\".");
                if (b.rider is not int who) { Fatal($"{at} has no rider: which earlier talk beat's person comes aboard (1-based)."); return; }
                if (who < 1 || who - 1 >= i) Fatal($"{at} rider {who} is not an earlier beat.");
            }

            /// <summary>The script's one set of slots holds one ride.</summary>
            internal override int Preflight(BeatsBuild build)
            {
                if (build.r.beats.Count(x => x.type == Name) > 1) { Console.WriteLine("REFUSED: two ride beats; the stock script has one destination."); return 1; }
                int ride = build.r.beats.FindIndex(x => x.type == Name);
                int who = build.r.beats[ride].rider!.Value - 1;
                if (build.r.beats[who].type != "talk") { Console.WriteLine($"REFUSED: beat {ride + 1}'s rider is beat {who + 1}, a {build.r.beats[who].type}; a rider is a talk beat's person."); return 1; }
                return 0;
            }

            /// <summary>A marker at the destination, the Passengers collection in vanilla's shape, and the stock script on the quest.</summary>
            internal override int Slot(BeatsBuild build, int i)
            {
                uint mid = NextAliasId(build.clone);
                var mk = build.markerPattern.DeepCopy(); mk.ID = mid; mk.Name = "DelveBeat" + (i + 1) + "Destination";
                build.clone.Aliases.Add(mk);
                uint cid = NextAliasId(build.clone);
                var col = EmptyCollection(build.inputs.WaveSrc!, cid, "Passengers");
                if (col == null) { Console.WriteLine("REFUSED: the source collection carries a fill; the passengers must start empty."); return 1; }
                col.Collection[0].ReferenceAlias!.PackageData.Add(new FormLink<IPackageGetter>(PassengerPackage));
                build.clone.Aliases.Add(col);
                build.slotOf[i] = new BeatSlot { markerAlias = (int)mid, activatorAlias = -1, journalStage = build.beatStage[i] };
                build.spawnedStage[i] = SpawnedStageBase + i;   // a plain stage the script sets when the ship arrives

                var sc = new ScriptEntry { Name = PassengerScript };
                ScriptObjectProperty Link(string name, FormKey key, int alias = -1)
                {
                    var p = new ScriptObjectProperty { Name = name, Flags = ScriptProperty.Flag.Edited };
                    p.Object.SetTo(key);
                    if (alias >= 0) p.Alias = (short)alias;
                    return p;
                }
                foreach (var (name, id) in PassengerForms) sc.Properties.Add(Link(name, Sf(id)));
                sc.Properties.Add(Link("Alias_Passengers", build.clone.FormKey, (int)cid));
                sc.Properties.Add(Link("Alias_PassengerDestination", build.clone.FormKey, PlaceAliasOf(build.t, build.r.beats[i].PlaceIndex, build.made.ThirdPlaceAlias)));
                sc.Properties.Add(Link("Alias_PassengerDestinationMarker", build.clone.FormKey, (int)mid));
                sc.Properties.Add(new ScriptIntProperty { Name = "StageToSetOnPlayerShipArrivesAtDestination", Data = build.spawnedStage[i], Flags = ScriptProperty.Flag.Edited });
                sc.Properties.Add(new ScriptIntProperty { Name = "StageToSetWhenAllPassengersDisembark", Data = build.beatStage[i], Flags = ScriptProperty.Flag.Edited });
                sc.Properties.Add(new ScriptBoolProperty { Name = "ShowTraces", Data = true, Flags = ScriptProperty.Flag.Edited });
                build.vma.Scripts.Add(sc);
                build.bm.Ride = (i, (int)cid, (int)mid, build.spawnedStage[i], build.beatStage[i]);
                Console.WriteLine($"  +slot    : beat {i + 1} (ride) -- destination marker alias {mid}, Passengers collection {cid}, {PassengerScript}: arrives {build.spawnedStage[i]}, disembarked {build.beatStage[i]}");
                return 0;
            }

            /// <summary>No hook: the stock script sets both stages.</summary>
            internal override int Hook(BeatsBuild build, int b, int prereq) => 0;

            internal override int EnterRank => 2;

            /// <summary>She comes aboard as the step begins; a turn-down takes her off the ship's list, or she rides forever.</summary>
            internal override void Enter(BeatsBuild build, int b, int stage)
            {
                int rider = build.r.beats[b].rider!.Value;
                string pa = $"Alias_Beat{rider}Person";
                build.props.Add(("ReferenceAlias", pa, build.clone.FormKey, build.slotOf[rider - 1].targetAlias));
                string script = $"((Self as Quest) as {PassengerScript})";
                build.Add(stage, $"{script}.AddPassenger({pa})");
                build.Add(BoardFailStage, $"{script}.RemovePassengerActor({pa}.GetActorRef())");
                Console.WriteLine($"  +ride    : beat {rider}'s person boards on stage {stage}; a turn-down ({BoardFailStage}) takes her off the ship");
            }
        }
    }
}
