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
        ///   use      DefaultAliasOnActivate          hides its prompt after use
        ///   pickup   DefaultAliasOnActivateGiveItem  gives the beat's item, disables the object
        ///   deliver  DefaultAliasOnActivate          the fragment takes every item picked up so far
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
        private sealed class ObjectBeat : BeatKind
        {
            private readonly string name;
            private readonly bool givesItem;

            internal ObjectBeat(string name, bool givesItem) { this.name = name; this.givesItem = givesItem; }

            internal override string Name => name;

            /// <summary>
            /// Beats 1 and 2 use the base's own two slots; every further beat is the first slot CLONED (the same argument
            /// as AddBeatSlot: a slot that works brings every field this tool does not know). The marker is a copy of the
            /// pattern, never NewMarker, which on the board would take the marker source itself.
            /// </summary>
            internal override int Slot(BeatsBuild build, int i)
            {
                if (build.baseNext < build.t.beatSlots.Count)
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
                    // The same object a second time: the stock hook's one duplicate, on the same alias.
                    hookAlias = build.slotOf[b].activatorAlias;
                    script = "DefaultAliasOnActivateA";
                }
                else
                {
                    hookAlias = build.slotOf[b].activatorAlias;
                    script = givesItem ? "DefaultAliasOnActivateGiveItem" : "DefaultAliasOnActivate";
                }
                var sc = StageHook(script, build.beatStage[b], prereq);
                if (givesItem)
                {
                    var ip = new ScriptObjectProperty { Name = "ItemToGive", Flags = ScriptProperty.Flag.Edited };
                    ip.Object.SetTo(build.itemOf[b]);
                    sc.Properties.Add(ip);
                    sc.Properties.Add(new ScriptBoolProperty { Name = "ShouldDisableAfterSuccessfulActivation", Data = true, Flags = ScriptProperty.Flag.Edited });
                }
                else
                {
                    // An object a later beat comes back to keeps its prompt, or the return could never fire.
                    bool comesBack = build.r.beats.Any(x => x.returnTo == b + 1);
                    sc.Properties.Add(new ScriptBoolProperty { Name = "ShouldHideActivationAfterSuccessfulActivation", Data = !comesBack, Flags = ScriptProperty.Flag.Edited });
                }
                return build.AttachBeatHook(b, hookAlias, script, sc, prereq);
            }
        }
    }
}
