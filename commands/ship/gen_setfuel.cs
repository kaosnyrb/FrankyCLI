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
    // Set SpaceshipGravJumpFuel on GenericBaseForms that already exist, FormID-stable.
    //
    //   setfuel <modname> <gbfm_editorid>[,<gbfm_editorid>...] <capacity>
    //
    // WHY THIS EXISTS. Nothing in this tool could author a fuel tank's capacity -- a grep
    // for "GravJumpFuel" across the whole repo returned ZERO hits (2026-08-17, found while
    // building atsd_fuel_01, the line's first fuel part). gen_shipstruct has --mass and
    // --cargo and no fuel equivalent, so a tank could be built, snapped, painted and
    // shipped while storing nothing. The stat is the entire reason the part class exists.
    //
    // ⛔ WHY ITS OWN COMMAND AND NOT A --fuel FLAG, and this is setmass's argument reused
    // rather than re-derived: a flag on gen_shipstruct would only ever reach a part at
    // CREATION, and capacity is exactly the number an author re-tunes after seeing the tank
    // on a ship. Rebuilding to change it is not neutral -- removerecord refuses cells by
    // design, so regenerating ORPHANS the part's Cell, and a repointed LayeredMaterialSwap
    // lives only inside the plugin with no source to rebuild from. Same shape as setmass,
    // setcargo, setobnd and setlightlayer, for the same reason each time.
    //
    // ⭐ AND WHY NOT FOLDED INTO setmass, which would have been one fewer file: mass is on
    // EVERY ship module and fuel is on exactly one class. Hanging capacity off the mass
    // command would put a fuel-only concept in a command every part type has to call, which
    // is the coupling setmass's own header refuses in the opposite direction.
    //
    // VANILLA REFERENCE, measured off 91 fuel GBFMs in Starfield.esm (2026-08-17):
    // SpaceshipGravJumpFuel runs 50 (min) 200 (median) 650 (max); the paired
    // SpaceshipPartMass runs 10 / 25 / 71. Deliberately NOT applied here -- picking the
    // number is the author's call, and baking a measured ratio in would turn an observation
    // into a law. Pass the capacity you mean.
    //
    // Idempotent: a record already carrying the value is left untouched and reported.
    class gen_setfuel
    {
        // ActorValue FormID, Starfield.esm. READ OFF vanilla ship-module records via
        // `gen_inspect gbfm Fuel` ("SpaceshipGravJumpFuel [00854F:Starfield.esm]"), not
        // guessed and not carried from a sibling constant.
        const uint AV_SPACESHIP_GRAV_JUMP_FUEL = 0x00854F;

        public static int Generate(string[] args)
        {
            // args: [modname, "setfuel", gbfm_editorids, capacity]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setfuel <modname> <gbfm_editorid>[,<gbfm_editorid>...] <capacity>");
                Console.WriteLine("Vanilla fuel runs 50 (min) 200 (median) 650 (max) across 91 tanks.");
                return 1;
            }
            string modname = args[0];
            var targets = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(t => t.Trim()).ToList();

            if (!float.TryParse(args[3], out float fuel))
            {
                Console.WriteLine($"Error: '{args[3]}' is not a valid capacity");
                return 1;
            }
            if (fuel <= 0)
            {
                // A zero-capacity fuel tank is not a small tank, it is a tank the builder's
                // range arithmetic reads as absent -- and it would ship looking correct on
                // every other axis. Refused rather than written, same as setmass refuses a
                // massless part and setcargo a zero-capacity hold.
                Console.WriteLine($"Error: capacity must be positive (got {fuel})");
                return 1;
            }

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;
            int changed = 0;

            // env holds the plugin open, so it is scoped to close before the write -- a
            // same-path WriteToBinary inside the using throws and leaves the old bytes
            // looking like a persisted no-op. Same reason as gen_setmass / gen_setcargo.
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                datapath = env.DataFolderPath;

                ModKey modKey = new ModKey(modname, ModType.Master);
                if (!env.LoadOrder.ModExists(modKey))
                {
                    Console.WriteLine($"Error: {modname}.esm is not in the load order");
                    return 1;
                }
                ModPath modPath = System.IO.Path.Combine(datapath, modname + ".esm");
                myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);

                var sfKey = env.LoadOrder[0].ModKey;

                // Validate EVERY target before mutating anything -- a lookup that fails
                // halfway through leaves a partly-patched plugin, which is what bit conform
                // (it printed a success line over an untouched disk).
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

                foreach (var existing in found)
                {
                    var gbfm = ((IGenericBaseFormGetter)existing).DeepCopy();
                    var sheet = gbfm.Components.OfType<PropertySheetComponent>().FirstOrDefault();
                    if (sheet == null)
                    {
                        Console.WriteLine($"Error: {gbfm.EditorID} has no PropertySheet -- refusing to invent one");
                        return 1;
                    }

                    var key = new FormKey(sfKey, AV_SPACESHIP_GRAV_JUMP_FUEL);
                    var prop = sheet.Properties.FirstOrDefault(p => p.ActorValue.FormKey == key);
                    if (prop != null)
                    {
                        if (Math.Abs(prop.Value - fuel) < 0.0001f)
                        {
                            Console.WriteLine($"  {gbfm.EditorID}: SpaceshipGravJumpFuel already {fuel} -- left as is");
                            continue;
                        }
                        Console.WriteLine($"  {gbfm.EditorID}: SpaceshipGravJumpFuel {prop.Value} -> {fuel}");
                        prop.Value = fuel;
                    }
                    else
                    {
                        // gen_shipstruct cannot write this property at all, so on a
                        // generated part it is ALWAYS absent -- the add is the normal path
                        // here, not the exception it is for mass. Still said out loud,
                        // because an add and an update are different events.
                        Console.WriteLine($"  {gbfm.EditorID}: + SpaceshipGravJumpFuel = {fuel}  (was absent)");
                        sheet.Properties.Add(new ObjectProperty()
                        {
                            ActorValue = key.ToNullableLink<IActorValueInformationGetter>(),
                            Value = fuel,
                        });
                    }

                    myMod.GenericBaseForms.Remove(existing.FormKey);
                    myMod.GenericBaseForms.Add(gbfm);
                    changed++;
                }
            }

            if (changed == 0)
            {
                Console.WriteLine("Nothing to write.");
                return 0;
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {changed} GenericBaseForm(s) patched, FormIDs unchanged.");
            return 0;
        }
    }
}
