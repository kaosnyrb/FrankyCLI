using Mutagen.Bethesda.Starfield;
using System;
using System.Linq;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// use, pickup and deliver: an OBJECT beat, the player activating a thing at the beat's marker. One class, three
        /// instances in BeatKinds, because the three differ only in their name and whether the hook hands over an item:
        ///
        ///   use      duo_delve_use                   hides its prompt, then sets the stage
        ///   pickup   duo_delve_pickup                hides, gives the beat's item, then sets; the fragment disables the object
        ///   deliver  duo_delve_use                   the fragment takes every item picked up so far
        ///   return   duo_delve_useb                  a use visited again: duo_delve_use under a second name, on the same alias
        ///   destroy  DefaultAliasOnDestroyed         shot to pieces, never pressed; the fragment disables the object
        ///
        /// ⛔ EVERY ACTIVATE HOOK HERE IS OUR OWN, NOT VANILLA'S DefaultAliasOnActivate FAMILY (2026-10-30, his "yeah make our own
        /// activate hook", plan OT-91; the rest moved 2026-11-02 on his "make a ticket to move the other three over", OT-93).
        /// The stock hooks act only after SafeSetStage returns, which waits out the stage's fragment (a hold's wave can hold it
        /// 180 s), and act on GetReference(): by then a re-dealt board quest's alias can hold the NEXT run's object, so Runaway
        /// Plant's second run met a console with no prompt. Ours hide (and give) first, then set. The space template's hooks
        /// moved in the same change (delve_space.cs).
        ///
        /// DESTROY (2026-10-28, his "yeah build the beat now", off his note that the classic Sabotage "had you destroy the
        /// activator. They have a health system"). His classics do it with ccs_heatleechpoddeath on a per-quest activator
        /// (BlockActivation on load, OnDestroyed: Disable, then the quest's HandleNestDistruction); a Delve has no driver,
        /// so the same three moves are stock and data: the clone carries DestroyTemplate's Destructible, the entry stage
        /// blocks activation (BuildBeats, beside "replace"), and the stock DefaultAliasOnDestroyed sets the stage.
        ///
        /// ⛔ DELIVER IS NOT DefaultAliasOnActivateRemoveItems, and it looks like it should be. That script's parent SETS THE
        /// STAGE FIRST and only then runs the item check (DefaultAlias.CheckAndSetStageAndCallDoSpecificThing: SafeSetStage,
        /// then DoSpecificThing), so as a gate it would complete the beat empty-handed and merely show a message. The gate
        /// here is STAGE ORDER: deliver's prereq is a stage only a pickup can set.
        ///
        /// Moved here from Slots' default branch and the Hooks pass's object branch (split step (d5), 2026-10-15, his "yeah
        /// go", "generally lets keep things simple tho"), unchanged. A RETURN (a use or deliver with returnTo) is a property
        /// of the beat, not a type: Slots fills its slot from the earlier beat's after every kind has slotted (a rule
        /// between beats), and its hook is the A duplicate here. What stays by name until the last step: the rules over
        /// these three as a set or between beats (returns, groups, endings, carried items, defend and approach targets, a
        /// who's anchor, the lint's no-model and no-name warnings).
        /// </summary>
        /// <summary>A use, pickup or deliver beat: one with an object of its own to activate.</summary>
        private static bool IsObject(Beat b) => KindOf(b) is ObjectBeat;

        private sealed class ObjectBeat : BeatKind
        {
            private readonly string name;
            private readonly bool carriesItem;

            internal ObjectBeat(string name, bool carriesItem) { this.name = name; this.carriesItem = carriesItem; }

            internal override string Name => name;

            /// <summary>A pickup's hook hands the item over, so the player carries it from here.</summary>
            internal override bool CarriesItem => carriesItem;

            /// <summary>A destroy: the object is shot, not activated (see the summary).</summary>
            internal bool Destroys => name == "destroy";

            /// <summary>
            /// Beats 1 and 2 use the base's own two slots; every further beat is the first slot CLONED (the same argument
            /// as AddBeatSlot: a slot that works brings every field this tool does not know). The marker is a copy of the
            /// pattern, never NewMarker, which on the board would take the marker source itself.
            /// </summary>
            internal override int Slot(BeatsBuild build, int i)
            {
                // ⛔ An ORBIT beat never takes a base slot: the base's ids (1, 2) sit BELOW the orbit's cell collection, and a
                // marker filled from a collection numbered above it never started 19a (2026-10-22, his "weird ID order", after
                // the id clash, "planet", "system" and "near" all failed). A fresh slot is numbered above the cell, as MB01's
                // marker is above its own; the base slot it leaves is removed by the unused-slot pass.
                bool inOrbit = build.r.beats[i].PlaceIndex == 2 && build.made.OrbitCell >= 0;
                if (!inOrbit && build.baseNext < build.t.beatSlots.Count)
                {
                    build.slotOf[i] = new BeatSlot { markerAlias = build.t.beatSlots[build.baseNext].markerAlias, activatorAlias = build.t.beatSlots[build.baseNext].activatorAlias,
                                                     journalStage = build.beatStage[i] };
                    build.baseNext++;
                    return 0;
                }
                uint next = NextAliasId(build.clone);
                var m = build.markerPattern.DeepCopy(); m.ID = next; m.Name = "DelveBeat" + (i + 1) + "Marker";
                var a = build.activatorPattern!.DeepCopy(); a.ID = next + 1; a.Name = "DelveBeat" + (i + 1) + "Target";
                a.CreateReferenceToObject!.AliasID = (short)m.ID;
                build.clone.Aliases.Add(m);
                build.clone.Aliases.Add(a);
                build.slotOf[i] = new BeatSlot { markerAlias = (int)m.ID, activatorAlias = (int)a.ID, journalStage = build.beatStage[i] };
                Console.WriteLine($"  +slot    : beat {i + 1} -- marker alias {m.ID}, activator {a.ID}");
                return 0;
            }

            /// <summary>On the object's activator alias: the activation sets the beat's stage once the step before is done.</summary>
            internal override int Hook(BeatsBuild build, int b, int prereq)
            {
                var beat = build.r.beats[b];
                int hookAlias; string script;
                if (beat.returnTo != null)
                {
                    // The same object a second time: our hook under its second name, on the same alias.
                    hookAlias = build.slotOf[b].activatorAlias;
                    script = UseScriptB;
                }
                else
                {
                    hookAlias = build.slotOf[b].activatorAlias;
                    script = Destroys ? "DefaultAliasOnDestroyed" : CarriesItem && build.itemOf.ContainsKey(b) ? PickupScript
                           : beat.bring != null ? BringUseScript : UseScript;
                }
                if (Destroys && prereq != build.t.entryStage)
                {
                    // A DESTROY AFTER AN EARLIER STEP IS NOT THERE UNTIL ITS STEP (his MB116a play 2026-11-13: "you can destroy
                    // the vault before hitting the button and it breaks the mission"; his "yeah A"). The stock hook's prereq
                    // check (DefaultScriptFunctions.TryToSetStage, IsValidToSetStage) does nothing when the event fires early,
                    // and a destroyed object never fires again, so the job is stuck for good. So the object starts disabled
                    // and the step before it enables it: it cannot be shot before its turn. The same shape as the space
                    // boards' target (InitiallyDisabled, the accept enables it). A destroy in the first step is untouched.
                    // The flag is set here; the Enable() line is StageLines' (the fragment pass, where code and props exist).
                    var target = build.clone.Aliases.OfType<QuestReferenceAlias>().First(a => a.ID == (uint)hookAlias);
                    target.Flags = (target.Flags ?? 0) | QuestReferenceAlias.Flag.InitiallyDisabled;
                    Console.WriteLine($"  +reveal  : beat {b + 1}'s object (alias {hookAlias}) starts disabled; stage {prereq} enables it");
                }
                var sc = StageHook(script, build.beatStage[b], prereq);
                if (Destroys) { }   // no prompt to hide: the entry stage blocked activation
                else if (beat.bring != null)
                {
                    // OT-31: the object refuses with a box until he carries the count, then takes it and hides (never a return,
                    // the lint holds that, so nothing comes back to it). The counter goes on the player (Bring).
                    var (item, _, count) = build.inputs.Brings[b];
                    sc.Properties.Add(new ScriptBoolProperty { Name = "HideAfterUse", Data = true, Flags = ScriptProperty.Flag.Edited });
                    var ip = new ScriptObjectProperty { Name = "Item", Flags = ScriptProperty.Flag.Edited };
                    ip.Object.SetTo(item);
                    sc.Properties.Add(ip);
                    sc.Properties.Add(new ScriptIntProperty { Name = "ItemCount", Data = count, Flags = ScriptProperty.Flag.Edited });
                    var mp = new ScriptObjectProperty { Name = "ShortMessage", Flags = ScriptProperty.Flag.Edited };
                    mp.Object.SetTo(build.shortOf[b]);
                    sc.Properties.Add(mp);
                    int f = build.AttachBeatHook(b, hookAlias, script, sc, prereq);
                    return f != 0 ? f : build.Bring(b, hookAlias, prereq);
                }
                else if (CarriesItem && !build.itemOf.ContainsKey(b))
                    // OT-08: the last step's pickup that nothing takes gives nothing (Items made none). The plain use hook
                    // hides and sets; the stage's first line still disables the object, as any pickup's does (StageLines).
                    sc.Properties.Add(new ScriptBoolProperty { Name = "HideAfterUse", Data = true, Flags = ScriptProperty.Flag.Edited });
                else if (CarriesItem)
                {
                    var ip = new ScriptObjectProperty { Name = "ItemToGive", Flags = ScriptProperty.Flag.Edited };
                    ip.Object.SetTo(build.itemOf[b]);
                    sc.Properties.Add(ip);
                    // The hook hides the object and gives the item BEFORE it sets the stage (duo_delve_pickup); the object is
                    // disabled by the stage's first line (StageLines), never by a hook after the set: the stock hook's late
                    // Disable() once hit an alias Stop() had cleared (remote_planet_qst21a, Papyrus.1.log 2026-10-26 16:50:52).
                }
                else
                {
                    // An object a later beat comes back to keeps its prompt, or the return could never fire.
                    bool comesBack = build.r.beats.Any(x => x.returnTo == b + 1);
                    sc.Properties.Add(new ScriptBoolProperty { Name = "HideAfterUse", Data = !comesBack, Flags = ScriptProperty.Flag.Edited });
                }
                return build.AttachBeatHook(b, hookAlias, script, sc, prereq);
            }

            /// <summary>
            /// A pickup's object goes as the FIRST line of its stage, ahead of anything that waits (his play of
            /// remote_planet_qst21a, 2026-10-26: "the canister doesn't disappear on use"). See Hook for why the stock flag
            /// cannot do it. A use with vanish goes the same way (his play of duo_MB52a_qst, 2026-10-27: "the box kinda just
            /// sits there tho"); on a return its slot is the shared object, so the name is this beat's and the alias the same.
            /// </summary>
            internal override void StageLines(BeatsBuild build, int k, int b)
            {
                var beat = build.r.beats[b];
                bool pickupGoes = CarriesItem && beat.returnTo == null;
                // A destroyed object goes too, as his classics' OnDestroyed disables it: a Destructible with no destroyed
                // model would otherwise stand there whole.
                bool goes = pickupGoes || Destroys || beat.vanish;
                bool blows = build.inputs.Explosions.TryGetValue(b, out var xk);
                if (!goes && !blows) return;
                string ta = $"Alias_Beat{b + 1}Target";
                build.props.Add(("ReferenceAlias", ta, build.clone.FormKey, build.slotOf[b].activatorAlias));
                // A destroy after an earlier step starts disabled (Hook) and is enabled by the stage its step waits on.
                int prereq = k == 0 ? build.t.entryStage : build.steps[k - 1].DoneStage;
                if (Destroys && prereq != build.t.entryStage)
                {
                    if (!build.code.ContainsKey(prereq)) build.code[prereq] = new();
                    build.code[prereq].Add($"{ta}.GetRef().Enable()   ; its step starts: until now it could be shot out of order");
                }
                int st = build.beatStage[b];
                if (!build.code.ContainsKey(st)) build.code[st] = new();
                if (goes) build.code[st].Insert(0, $"{ta}.GetRef().Disable()");
                if (blows)
                {
                    // Placed BEFORE the disable, so it goes off where the object stood.
                    build.props.Add(("Form", $"Explosion{b + 1}", xk, -1));
                    build.code[st].Insert(0, $"{ta}.GetRef().PlaceAtMe(Explosion{b + 1})");
                }
            }
        }
    }
}
