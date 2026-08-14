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
    // Set SpaceshipPartMass on GenericBaseForms that already exist, FormID-stable.
    //
    //   setmass <modname> <gbfm_editorid>[,<gbfm_editorid>...] <mass>
    //
    // WHY THIS EXISTS. gen_shipstruct writes SpaceshipPartMass at creation (--mass) and
    // NOTHING could change it afterwards. Mass is the one stat an author re-tunes after
    // seeing the part on a ship -- the Porter set came in at 400 and wanted 160 once he
    // saw that the parts tile and you fly several at once -- and the only route was a
    // rebuild. A rebuild is not neutral: removerecord refuses cells by design, so
    // regenerating a part ORPHANS its Cell, and a CK-repointed LayeredMaterialSwap lives
    // only inside the plugin with no source to rebuild from. Same argument that produced
    // setcargo, setobnd and setlightlayer, and the same shape.
    //
    // ⛔ WHY THIS IS ITS OWN COMMAND AND NOT A --mass FLAG ON setcargo, which is smaller
    // and was the obvious move: SpaceshipPartMass is carried by EVERY ship module --
    // structural, engine, cockpit, gear, cargo. Hanging it off the cargo command would
    // mean "to change a structural part's mass, run setcargo", which is a false statement
    // in the command name and couples two things that are not related. Capacity and mass
    // move together for a CARGO author (the vanilla ratio is ~4.7), but that is a usage
    // correlation, not a structural one. The cost is honest and small: retuning a cargo
    // part is two commands rather than one.
    //
    // VANILLA REFERENCE, measured off 91 SMS_Cargo GBFMs in Starfield.esm: mass runs 30
    // (min) 84 (median) 500 (max) across 20 distinct rungs, and CarryWeight is ~4.7x mass.
    // Deliberately NOT applied here -- picking the number is the author's call, and baking
    // a measured ratio in would turn it into a law. Pass the mass you mean.
    //
    // Idempotent: a record already carrying the value is left untouched and reported.
    class gen_setmass
    {
        // ActorValue FormID, Starfield.esm, read off vanilla ship-module records.
        const uint AV_SPACESHIP_PART_MASS = 0x00ACDB;

        public static int Generate(string[] args)
        {
            // args: [modname, "setmass", gbfm_editorids, mass]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setmass <modname> <gbfm_editorid>[,<gbfm_editorid>...] <mass>");
                Console.WriteLine("Vanilla cargo runs 30 (min) 84 (median) 500 (max); capacity is ~4.7x mass.");
                return 1;
            }
            string modname = args[0];
            var targets = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(t => t.Trim()).ToList();

            if (!float.TryParse(args[3], out float mass))
            {
                Console.WriteLine($"Error: '{args[3]}' is not a valid mass");
                return 1;
            }
            if (mass <= 0)
            {
                // A massless ship part is not a lighter part, it is a part the builder's
                // mass arithmetic reads as absent. Refused rather than written, same as
                // setcargo refuses a zero-capacity hold.
                Console.WriteLine($"Error: mass must be positive (got {mass})");
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
            // looking like a persisted no-op. Same reason as gen_setcargo / gen_setname.
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

                    var key = new FormKey(sfKey, AV_SPACESHIP_PART_MASS);
                    var prop = sheet.Properties.FirstOrDefault(p => p.ActorValue.FormKey == key);
                    if (prop != null)
                    {
                        if (Math.Abs(prop.Value - mass) < 0.0001f)
                        {
                            Console.WriteLine($"  {gbfm.EditorID}: SpaceshipPartMass already {mass} -- left as is");
                            continue;
                        }
                        Console.WriteLine($"  {gbfm.EditorID}: SpaceshipPartMass {prop.Value} -> {mass}");
                        prop.Value = mass;
                    }
                    else
                    {
                        // Every gen_shipstruct part has one, so an absent mass means a
                        // hand-authored record. Added rather than refused (setcargo's
                        // precedent for CarryWeight), but SAID -- an add and an update are
                        // different events and the caller should be able to tell them apart.
                        Console.WriteLine($"  {gbfm.EditorID}: + SpaceshipPartMass = {mass}  (was absent)");
                        sheet.Properties.Add(new ObjectProperty()
                        {
                            ActorValue = key.ToNullableLink<IActorValueInformationGetter>(),
                            Value = mass,
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
