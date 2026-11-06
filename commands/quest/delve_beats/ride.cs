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
                if (!IsBoard(t) && !IsBoardSpace(t)) Fatal($"{at} is on a {t.kind} template; a ride is built on the two boards only.");
                if (IsBoardSpace(t))
                {
                    // The rescue: the check-in's person rides.
                    if (b.place != "second") Fatal($"{at} needs \"place\": \"second\", the city she is carried to.");
                    if (!string.Equals(b.at, "AutofillGeneric01", StringComparison.OrdinalIgnoreCase)) Fatal($"{at} is at '{b.at}'; the one spot every city lists is AutofillGeneric01.");
                }
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

            /// <summary>The Passengers collection: empty, wearing vanilla's package. Null, having said why, if the source carries a fill.</summary>
            private static QuestCollectionAlias? PassengersAlias(IQuestCollectionAliasGetter src, uint id)
            {
                var col = EmptyCollection(src, id, "Passengers");
                if (col == null) { Console.WriteLine("REFUSED: the source collection carries a fill; the passengers must start empty."); return null; }
                col.Collection[0].ReferenceAlias!.PackageData.Add(new FormLink<IPackageGetter>(PassengerPackage));
                return col;
            }

            /// <summary>The stock script, as RQ_OE_TransportPerson_02 carries it, with the quest's own aliases and stages.</summary>
            private static ScriptEntry PassengerEntry(FormKey quest, int col, int place, int marker, int arrived, int off)
            {
                var sc = new ScriptEntry { Name = PassengerScript };
                ScriptObjectProperty Link(string name, FormKey key, int alias = -1)
                {
                    var p = new ScriptObjectProperty { Name = name, Flags = ScriptProperty.Flag.Edited };
                    p.Object.SetTo(key);
                    if (alias >= 0) p.Alias = (short)alias;
                    return p;
                }
                foreach (var (name, id) in PassengerForms) sc.Properties.Add(Link(name, Sf(id)));
                sc.Properties.Add(Link("Alias_Passengers", quest, col));
                sc.Properties.Add(Link("Alias_PassengerDestination", quest, place));
                sc.Properties.Add(Link("Alias_PassengerDestinationMarker", quest, marker));
                sc.Properties.Add(new ScriptIntProperty { Name = "StageToSetOnPlayerShipArrivesAtDestination", Data = arrived, Flags = ScriptProperty.Flag.Edited });
                sc.Properties.Add(new ScriptIntProperty { Name = "StageToSetWhenAllPassengersDisembark", Data = off, Flags = ScriptProperty.Flag.Edited });
                sc.Properties.Add(new ScriptBoolProperty { Name = "ShowTraces", Data = true, Flags = ScriptProperty.Flag.Edited });
                return sc;
            }

            /// <summary>A marker at the destination, the Passengers collection in vanilla's shape, and the stock script on the quest.</summary>
            internal override int Slot(BeatsBuild build, int i)
            {
                uint mid = NextAliasId(build.clone);
                var mk = build.markerPattern.DeepCopy(); mk.ID = mid; mk.Name = "DelveBeat" + (i + 1) + "Destination";
                build.clone.Aliases.Add(mk);
                uint cid = NextAliasId(build.clone);
                var col = PassengersAlias(build.inputs.WaveSrc!, cid);
                if (col == null) return 1;
                build.clone.Aliases.Add(col);
                build.slotOf[i] = new BeatSlot { markerAlias = (int)mid, activatorAlias = -1, journalStage = build.beatStage[i] };
                build.spawnedStage[i] = SpawnedStageBase + i;   // a plain stage the script sets when the ship arrives

                build.vma.Scripts.Add(PassengerEntry(build.clone.FormKey, (int)cid, PlaceAliasOf(build.t, build.r.beats[i].PlaceIndex, build.made.ThirdPlaceAlias),
                                                     (int)mid, build.spawnedStage[i], build.beatStage[i]));
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

            private static QuestReferenceAlias EmptyRefAlias(Quest clone, string name, QuestReferenceAlias.Flag flags)
            {
                var a = new QuestReferenceAlias { ID = NextAliasId(clone), Name = name, Flags = flags, ALFG = 0 };
                a.VoiceTypes.SetTo(FormKey.Null);
                return a;
            }

            /// <summary>Her check-in done: she is a passenger from here (the talk's closing reply sets it).</summary>
            private const int CheckedInStage = 50;
            /// <summary>The plain stage the script sets as the ship lands (the ground ride's is 900 + its beat).</summary>
            private const int RescueArrivedStage = SpawnedStageBase + 1;
            /// <summary>The stage the script sets when she has left the ship: the goodbye opens, and it is the ride's end.</summary>
            private const int OffShipStage = SpawnedStageBase + 2;

            /// <summary>
            /// THE RESCUE (MB104a, his 2026-11-04 "MB104a should have you rescue her", then his 16:42 "It's odd you don't talk to her
            /// after she gets on board" and "she leaves without saying goodbye"): four steps on a boardspace, a use, a talk aboard,
            /// a ride to a city, a talk to see her off. Using the object creates her at the ship's own passenger marker, vanilla's
            /// way (SQ_PlayerShipScript.DebugAddPassengers). The check-in's closing reply makes her a passenger, not the use: until
            /// then a landing is nothing to the stock script, so landing before talking cannot put her off early. The goodbye's
            /// closing reply completes the mission, so the script's "everyone off" is a stage of ours and no longer the end; if he
            /// left the ship first the script counts her off while she is still aboard, so that stage moves her to the city's
            /// marker, because an actor left on the ship after the quest stops would ride on it for good. Both talks are one
            /// person's, each gated on its own step (plan OT-96).
            /// The city is new to this shell, which has no second place: a location alias pinned as the ground build pins one,
            /// and the destination marker filled inside it, both as played on duo_delvetest_r1.
            /// What it removes when the quest ends: a turn-down takes her off the ship and deletes her; a landing leaves her in
            /// the city, a non-persistent ref the game clears with the cell.
            /// </summary>
            internal static int Rescue(StarfieldMod myMod, Quest clone, Template t, Recipe r,
                                       IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, Dictionary<string, FormKey> markers, BeatsMade bm,
                                       QuestAdapter vma, QuestObjective ob, int useObjective, int firstObjective, int pickupStage, int objectAlias,
                                       SortedDictionary<int, List<string>> code, List<(string type, string pname, FormKey key, int alias)> extraProps)
            {
                Beat checkIn = r.beats[1], ride = r.beats[2], goodbye = r.beats[3];
                foreach (var (st, b) in new[] { (CheckedInStage, checkIn), (RescueArrivedStage, (Beat?)null), (OffShipStage, ride) })
                {
                    if (clone.Stages!.Any(s => s.Index == st)) { Console.WriteLine($"REFUSED: the shell already has a stage {st}; a rescue's must be its own."); return 1; }
                    var ns = CloneStage(clone, st);
                    if (ns == null) return 1;
                    if (b?.journal != null) ns.LogEntries[0].Entry = Expand(b.journal, t);
                    clone.Stages.Add(ns);
                }
                SortStages(clone);

                var city = r.place.second!.settlement!;
                // ⛔ ALFG AND VoiceTypes ARE WRITTEN EMPTY, NEVER LEFT OUT (duo_delvetest_r2's first build, 2026-11-04: all three aliases
                // came from `new`, had neither subrecord, and the game crashed to desktop loading the plugin, before the save).
                // Every alias vanilla and we have written carries both; the verify now grades that on these three.
                var loc = new QuestLocationAlias { ID = NextAliasId(clone), Name = "FinalLocation", ALFG = 0 };
                clone.Aliases!.Add(loc);
                if (MakePlaceSettlement(clone, (int)loc.ID, city) != 0) return 1;
                var mk = EmptyRefAlias(clone, "DelveBeat3Destination", QuestReferenceAlias.Flag.AllowReserved);
                var inCity = new LocationAliasReference { AliasID = (int)loc.ID };
                inCity.RefType.SetTo(markers[ride.at]);
                mk.Location = inCity;
                clone.Aliases.Add(mk);
                var pa = EmptyRefAlias(clone, "DelveBeat2Person", QuestReferenceAlias.Flag.Optional);
                clone.Aliases.Add(pa);

                var wave = WaveCollectionOf(env.LoadOrder[0].Mod?.Quests.FirstOrDefault(q => q.FormKey.ID == WaveQuestId));
                if (wave == null) { Console.WriteLine("REFUSED: a rescue copies its Passengers collection from UC08_AB_AnomalyBattle, and it is not in the load."); return 1; }
                uint cid = NextAliasId(clone);
                var col = PassengersAlias(wave, cid);
                if (col == null) return 1;
                clone.Aliases.Add(col);

                var p = checkIn.person!;
                var tmpl = OwnOrMaster<INpcGetter>(myMod, env, p.template!);
                if (tmpl == null) { Console.WriteLine($"REFUSED: NPC '{p.template}' is not in {t.mod} or one of its masters."); return 1; }
                var vt = OwnOrMaster<IVoiceTypeGetter>(myMod, env, p.voice!);
                if (vt == null) { Console.WriteLine($"REFUSED: VoiceType '{p.voice}' is not in {t.mod} or one of its masters."); return 1; }
                FormKey? outfit = null;
                if (!string.IsNullOrWhiteSpace(p.outfit))
                {
                    var o = OwnOrMaster<IOutfitGetter>(myMod, env, p.outfit!);
                    if (o == null) { Console.WriteLine($"REFUSED: Outfit '{p.outfit}' is not in {t.mod} or one of its masters."); return 1; }
                    outfit = o.FormKey;
                }
                RemovePrevious(myMod.Npcs, e => e.StartsWith(r.id + "_person"));
                var npc = ClonePerson(myMod, r, 1, tmpl, vt.FormKey, outfit, string.IsNullOrWhiteSpace(p.face) ? null : OwnOrMaster<INpcGetter>(myMod, env, p.face!)?.FormKey);
                bm.People[1] = (npc.FormKey, p.name!);
                Console.WriteLine($"  +npc     : {npc.EditorID} {npc.FormKey}  \"{p.name}\"  (clone of {tmpl.EditorID})");

                // The talks' own build. Each opens on the step before it: the check-in once she exists, the goodbye once she is off.
                var steps = new List<Step>
                {
                    new Step { Beats = { 0 }, DoneStage = pickupStage }, new Step { Beats = { 1 }, DoneStage = CheckedInStage },
                    new Step { Beats = { 2 }, DoneStage = OffShipStage }, new Step { Beats = { 3 }, DoneStage = t.completeStage },
                };
                var stages = new Dictionary<int, int> { [1] = CheckedInStage, [3] = t.completeStage };
                foreach (int b in new[] { 1, 3 })
                    TalkBeat.BuildTalk(myMod, clone, r, b, steps, stages, t.entryStage, (int)pa.ID, npc.FormKey,
                                       npc.Flags.HasFlag(Npc.Flag.Female), bm, new Dictionary<(int beat, int ex), FormKey>());
                clone.Data!.Priority = TalkQuestPriority;

                vma.Scripts.Add(PassengerEntry(clone.FormKey, (int)cid, (int)loc.ID, (int)mk.ID, RescueArrivedStage, OffShipStage));
                bm.Ride = (2, (int)cid, (int)mk.ID, RescueArrivedStage, OffShipStage);

                // One objective per step after the use's, ten apart, each a copy of the use's pointed at its own target.
                int checkObj = firstObjective, rideObj = firstObjective + 10, byeObj = firstObjective + 20, at = clone.Objectives!.IndexOf(ob);
                foreach (var (i, b, target) in new[] { (checkObj, checkIn, pa.ID), (rideObj, ride, mk.ID), (byeObj, goodbye, pa.ID) })
                {
                    var o2 = ob.DeepCopy();
                    o2.Index = (ushort)i;
                    foreach (var x in o2.Targets.Where(x => x.AliasID == objectAlias)) x.AliasID = (int)target;
                    o2.DisplayText = Expand(b.objective!, t);
                    clone.Objectives.Insert(++at, o2);
                }

                extraProps.Add(("ReferenceAlias", "Alias_Beat2Person", clone.FormKey, (int)pa.ID));
                extraProps.Add(("ReferenceAlias", "Alias_Beat3Destination", clone.FormKey, (int)mk.ID));
                extraProps.Add(("SQ_PlayerShipScript", "SQ_PlayerShip", Sf(0x0174A2), -1));
                extraProps.Add(("ActorBase", "Rider", npc.FormKey, -1));
                string script = $"((Self as Quest) as {PassengerScript})";
                code[pickupStage] = new()
                {
                    $"SetObjectiveCompleted({useObjective})",
                    "Alias_Beat2Person.ForceRefTo(SQ_PlayerShip.HomeShipPassengerMarker.GetRef().PlaceActorAtMe(Rider))",
                    $"SetObjectiveDisplayed({checkObj})",
                };
                code[CheckedInStage] = new()
                {
                    $"SetObjectiveCompleted({checkObj})",
                    $"{script}.AddPassenger(Alias_Beat2Person)",
                    $"SetObjectiveDisplayed({rideObj})",
                };
                code[OffShipStage] = new()
                {
                    $"SetObjectiveCompleted({rideObj})",
                    "Actor offShip = Alias_Beat2Person.GetActorRef()",
                    "If offShip.GetCurrentShipRef()   ; he left first, and the interior unloaded round her",
                    "    offShip.MoveTo(Alias_Beat3Destination.GetRef())",
                    "EndIf",
                    $"SetObjectiveDisplayed({byeObj})",
                };
                code[t.completeStage] = new() { $"SetObjectiveCompleted({byeObj})" };
                code[BoardFailStage] = new()
                {
                    "Actor aboard = Alias_Beat2Person.GetActorRef()",
                    "If aboard",
                    $"    {script}.RemovePassengerActor(aboard)",
                    "    aboard.Disable()",
                    "    aboard.Delete()",
                    "EndIf",
                };
                Console.WriteLine($"  +rescue  : use {pickupStage} (aboard), check-in {CheckedInStage} (a passenger), landed {RescueArrivedStage}, off {OffShipStage}, goodbye {t.completeStage}; {city} (alias {loc.ID}), destination {mk.ID}, person {pa.ID}, Passengers {cid}");
                return 0;
            }
        }
    }
}
