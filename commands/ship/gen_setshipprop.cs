using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    // The last four ship-module property setters, over one shared core.
    //
    //   setmaxpower  <modname> <gbfm>[,<gbfm>...] <value>   SpaceshipMaxAvailablePower
    //   setrepair    <modname> <gbfm>[,<gbfm>...] <value>   SpaceshipRepairRate
    //   sethealth    <modname> <gbfm>[,<gbfm>...] <value>   Health          (the card's HULL)
    //   setcrew      <modname> <gbfm>[,<gbfm>...] <value>   SpaceshipCrewRating
    //
    // WHY NAMED COMMANDS AND NOT ONE GENERIC setprop -- HIS CALL, 2026-08-17, and the argument
    // is better than the one I brought him. I proposed a general property setter on the
    // third-copy rule (a thing open-coded N times is N bugs). He said: "just have setmaxpower
    // etc. There's only like 4 of these left in the whole game."
    //
    // ⭐ THAT RETIRES MY OWN RULE FOR THIS CASE, and the distinction is worth keeping: the
    // third-copy law is about an OPEN-ENDED set. This set is CLOSED and nearly exhausted --
    // counted, not taken on faith: of the ship-module property vocabulary, exactly these four
    // had no setter (Health existed only as a side effect of setcargo, which also writes
    // CarryWeight, so reaching it meant giving a fuel tank a cargo capacity; CrewRating
    // existed only at creation time inside gen_shipstruct). A generic setter would also have
    // COST something real: each named command carries its own measured vanilla reference in
    // its header, and a `setprop <av-id> <value>` has nowhere to put that.
    //
    // WHAT HIS RULING DID NOT SAY, so it is taken and not assumed: he ruled on the INTERFACE,
    // not the internals. The four share one private core below rather than being four more
    // copies of the same 120 lines -- five copies of validate-then-mutate is five places for
    // the conform defect (a success line printed over an untouched disk) to come back.
    // gen_setmass / gen_setcargo / gen_setfuel are deliberately NOT refactored onto it: they
    // produced every part this line has shipped, and rewriting working code to save
    // duplication I have already stopped adding to is the "while we're in here" move.
    //
    // Each command is idempotent, validates EVERY target before mutating anything, and is
    // FormID-stable.
    internal static class ShipProp
    {
        /// <param name="allowZero">
        /// Mass, capacity and fuel are refused at zero -- a zero there is not a small part, it
        /// is a part the builder's arithmetic reads as absent. RepairRate and CrewRating are
        /// legitimately zero in vanilla (eng01 carries CrewRating 0.25; plenty carry 0), so
        /// the guard is per-property rather than a house rule copied by reflex.
        /// </param>
        public static int Apply(string[] args, uint actorValue, string propName,
                                string vanillaHint, bool allowZero)
        {
            // args: [modname, "<verb>", gbfm_editorids, value]
            if (args.Length < 4)
            {
                Console.WriteLine($"Usage: {args.ElementAtOrDefault(1) ?? "<verb>"} <modname> "
                                  + "<gbfm_editorid>[,<gbfm_editorid>...] <value>");
                Console.WriteLine($"Sets {propName}. {vanillaHint}");
                return 1;
            }
            string modname = args[0];
            var targets = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(t => t.Trim()).ToList();

            if (!float.TryParse(args[3], out float value))
            {
                Console.WriteLine($"Error: '{args[3]}' is not a valid value for {propName}");
                return 1;
            }
            if (value < 0 || (!allowZero && value == 0))
            {
                Console.WriteLine($"Error: {propName} must be {(allowZero ? "non-negative" : "positive")} (got {value})");
                return 1;
            }
            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            int changed = 0;
            using var session = PluginSession.Open(modname);
            if (session == null) return 1;
            var myMod = session.Mod;

            // Validate EVERY target before mutating anything. A lookup that fails halfway
            // leaves a partly-patched plugin -- the exact defect that bit conform, which
            // printed a success line over an untouched disk.
            var found = new List<IGenericBaseFormGetter>();
            foreach (var target in targets)
            {
                var existing = myMod.GenericBaseForms.FirstOrDefault(
                    g => string.Equals(g.EditorID, target, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    Console.WriteLine($"Error: no GenericBaseForm '{target}' in {modname}");
                    return 1;
                }
                found.Add(existing);
            }

            var key = new FormKey(session.SfKey, actorValue);
            foreach (var existing in found)
            {
                var r = Set(myMod, existing, key, propName, value);
                if (r == null) return 1;
                if (r == true) changed++;
            }
            session.Close();

            if (changed == 0)
            {
                Console.WriteLine("Nothing to write.");
                return 0;
            }
            session.Write();
            Console.WriteLine($"Finished -- {changed} GenericBaseForm(s) patched, FormIDs unchanged.");
            return 0;
        }

        /// <summary>
        /// The CORE: set (or add) one property on one GBFM's PropertySheet, on an already-loaded
        /// plugin. True = changed, false = already at the value, null = refused (printed).
        /// Shared by every ShipProp verb and by `batch`; the caller validates and owns load/write.
        /// It never INVENTS a sheet: a GBFM with no PropertySheet is refused, not given one.
        /// </summary>
        public static bool? Set(StarfieldMod myMod, IGenericBaseFormGetter existing, FormKey key,
                                string propName, float value)
        {
            var gbfm = existing.DeepCopy();
            var sheet = gbfm.Components.OfType<PropertySheetComponent>().FirstOrDefault();
            if (sheet == null)
            {
                Console.WriteLine($"Error: {gbfm.EditorID} has no PropertySheet -- refusing to invent one");
                return null;
            }

            var prop = sheet.Properties.FirstOrDefault(p => p.ActorValue.FormKey == key);
            if (prop != null)
            {
                if (Math.Abs(prop.Value - value) < 0.0001f)
                {
                    Console.WriteLine($"  {gbfm.EditorID}: {propName} already {value} -- left as is");
                    return false;
                }
                Console.WriteLine($"  {gbfm.EditorID}: {propName} {prop.Value} -> {value}");
                prop.Value = value;
            }
            else
            {
                // An add and an update are different events; the caller should be able
                // to tell them apart from the output alone.
                Console.WriteLine($"  {gbfm.EditorID}: + {propName} = {value}  (was absent)");
                sheet.Properties.Add(new ObjectProperty()
                {
                    ActorValue = key.ToNullableLink<IActorValueInformationGetter>(),
                    Value = value,
                });
            }

            myMod.GenericBaseForms.Remove(existing.FormKey);
            myMod.GenericBaseForms.Add(gbfm);
            return true;
        }
    }

    // ActorValue FormIDs are READ OFF vanilla records via `gen_inspect gbfm`, never guessed:
    // SMA_Reactor_* carries MaxAvailablePower/RepairRate/CrewRating, SMS_FuelTank_* carries Health.
    class gen_setmaxpower
    {
        public static int Generate(string[] args) => ShipProp.Apply(args, 0x001018,
            "SpaceshipMaxAvailablePower",
            "Vanilla reactors sit at 16 across the sampled A/B-class records; it is the headline stat on the card.",
            allowZero: false);
    }

    /// <summary>
    /// ShipModuleVariant -- the SHAPE axis of the builder's two controls (position is the mount
    /// SIDE, variant is the shape). Read off our own shipped cargo set: atsd_gbfm_cargolg_01/02
    /// carry variant 1 and 04/05 carry variant 2, so a Porter row is a 2x2 grid of
    /// variant x position.
    ///
    /// It exists because COMPRESSING two rows into one is a catalogue-shaping move and the
    /// variant number is the only thing that separates the members afterwards. Nothing could
    /// set it: setflipset writes positions and the FormList, and every other ShipProp verb is
    /// pinned to a stat. So folding a part into a set meant hand-editing in the CK -- on the one
    /// axis where a collision is silent, because two members sharing a variant number is not an
    /// error, it is just a shape the player can never reach.
    /// </summary>
    class gen_setvariant
    {
        public static int Generate(string[] args) => ShipProp.Apply(args, 0x27BACE,
            "ShipModuleVariant",
            "The SHAPE axis. Members of one flip set must carry DISTINCT variant numbers within "
            + "a given position, or the collision is silent -- the unreachable one just never "
            + "appears. Vanilla cargo carries no variant at all; ours all do.",
            allowZero: false);
    }

    class gen_setrepair
    {
        public static int Generate(string[] args) => ShipProp.Apply(args, 0x01CAC0,
            "SpaceshipRepairRate",
            "Vanilla reactors: 1.25 on the A-class sampled, 2.85 on the experimental B-class.",
            allowZero: true);
    }

    class gen_sethealth
    {
        public static int Generate(string[] args) => ShipProp.Apply(args, 0x0002D4,
            "Health",
            "The card's HULL row. Vanilla fuel tanks and our own cargo parts both carry 5. "
            + "Previously reachable ONLY through setcargo, which also writes CarryWeight -- so a "
            + "fuel tank could not be given hull without also being given a cargo capacity.",
            allowZero: false);
    }

    class gen_setreactorhealth
    {
        // The FIFTH property gap, found 2026-08-17 diffing our Linesman against
        // SMA_Reactor_AmunDunn_340T_Stellarator. It is the REACTOR SYSTEM's own health, which
        // is a different number from the module's Health -- vanilla carries
        // ShipSystemReactorHealth 25 AND Health 375 on the same record.
        public static int Generate(string[] args) => ShipProp.Apply(args, 0x1EF0CA,
            "ShipSystemReactorHealth",
            "Vanilla A-class reactors carry 25, alongside a module Health of 375.",
            allowZero: false);
    }

    class gen_setcrew
    {
        public static int Generate(string[] args) => ShipProp.Apply(args, 0x019080,
            "SpaceshipCrewRating",
            "Vanilla reactors 1-2; our eng01 carries 0.25. Zero is legal and common.",
            allowZero: true);
    }

    // 2026-10-05, the Foreman Bridge matched to the DS30.2 Ares Bridge: a COCKPIT carries two crew
    // properties that are NOT SpaceshipCrewRating (above), and nothing could write either one.
    // "only like 4 of these left" was true of the properties counted that day, which were the
    // reactor/engine/cargo sheets; cockpits had never been in the count.
    class gen_setcrewcapacity
    {
        public static int Generate(string[] args) => ShipProp.Apply(args, 0x040CE0,
            "SpaceshipCrew",
            "A cockpit's crew capacity. DS30.2 Ares Bridge 3; our Sherpa 2.",
            allowZero: true);
    }

    class gen_setcrewslots
    {
        public static int Generate(string[] args) => ShipProp.Apply(args, 0x2CC9EA,
            "SpaceshipCrewSlots",
            "A cockpit's crew stations. DS30.2 Ares Bridge 8; our Sherpa 2.",
            allowZero: true);
    }
}
