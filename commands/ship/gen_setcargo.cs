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
    // Make an existing GenericBaseForm a CARGO part -- add CarryWeight (the hold's capacity)
    // and Health to its PropertySheet.
    //
    //   setcargo <modname> <gbfm_editorid>[,<gbfm_editorid>...] <capacity> [health]
    //
    // WHY THIS EXISTS. gen_shipstruct writes a 2-property sheet (SpaceshipPartMass +
    // ShipModuleVariant). That is the STRUCTURAL shape. A cargo GBFM carries three --
    // CarryWeight, SpaceshipPartMass, Health -- and CarryWeight is the whole point of the
    // part: without it the record builds, renders, snaps and paints, and the hold carries
    // nothing. Same class of gap as Model.LightLayer, and found the same way (by reading a
    // vanilla record field-by-field rather than by the part failing loudly).
    //
    // The generator is fixed at the root too (--cargo on struct), so new parts do not need
    // this. It is here for records that already exist, because a rebuild is not neutral:
    // removerecord refuses cells by design, so regenerating a part orphans its Cell, and a
    // CK-repointed LayeredMaterialSwap lives only in the plugin with no source to rebuild
    // from. FormID-stable, like the rest of the set* family.
    //
    // VANILLA REFERENCE, measured not chosen -- 91 SMS_Cargo GBFMs read off Starfield.esm:
    // Health is 5 on every one of them, and CarryWeight / SpaceshipPartMass runs 4.67 (min)
    // 4.72 (median) 6.10 (max). Capacity is therefore ~4.7x mass across the whole vanilla
    // catalogue. That ratio is deliberately NOT applied here: it is a design guide for
    // picking the number, and baking it in would turn a measurement into a law and take the
    // choice away from the author. Pass the capacity you mean.
    //
    // Idempotent: a record already carrying the values is left untouched and reported.
    class gen_setcargo
    {
        // ActorValue FormIDs, all Starfield.esm, read off vanilla cargo records.
        const uint AV_CARRY_WEIGHT = 0x0002DC;
        const uint AV_HEALTH = 0x0002D4;

        public static int Generate(string[] args)
        {
            // args: [modname, "setcargo", gbfm_editorids, capacity, (health)]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setcargo <modname> <gbfm_editorid>[,<gbfm_editorid>...] <capacity> [health]");
                Console.WriteLine("Default health 5 (every vanilla SMS_Cargo GBFM carries 5).");
                return 1;
            }
            string modname = args[0];
            var targets = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(t => t.Trim()).ToList();

            if (!float.TryParse(args[3], out float capacity))
            {
                Console.WriteLine($"Error: '{args[3]}' is not a valid capacity");
                return 1;
            }
            if (capacity <= 0)
            {
                // A zero-capacity cargo hold is the exact silent-nothing this command exists to
                // prevent, so it is refused rather than written.
                Console.WriteLine($"Error: capacity must be positive (got {capacity})");
                return 1;
            }
            float health = 5f;
            if (args.Length >= 5 && !float.TryParse(args[4], out health))
            {
                Console.WriteLine($"Error: '{args[4]}' is not a valid health value");
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

            // env holds the plugin open, so it is scoped to close before the write -- a same-path
            // WriteToBinary inside the using throws and leaves the old bytes looking like a
            // persisted no-op. Same reason as gen_setname / gen_setlightlayer.
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

                // Validate every target BEFORE mutating anything -- a lookup that fails halfway
                // through leaves a partly-patched plugin, and the conform command was bitten by
                // exactly that (it printed a success line over an untouched disk).
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

                    bool touched = false;
                    foreach (var (av, value, label) in new[]
                             { (AV_CARRY_WEIGHT, capacity, "CarryWeight"), (AV_HEALTH, health, "Health") })
                    {
                        var key = new FormKey(sfKey, av);
                        var prop = sheet.Properties.FirstOrDefault(p => p.ActorValue.FormKey == key);
                        if (prop != null)
                        {
                            if (Math.Abs(prop.Value - value) < 0.0001f)
                            {
                                Console.WriteLine($"  {gbfm.EditorID}: {label} already {value} -- left as is");
                                continue;
                            }
                            Console.WriteLine($"  {gbfm.EditorID}: {label} {prop.Value} -> {value}");
                            prop.Value = value;
                        }
                        else
                        {
                            Console.WriteLine($"  {gbfm.EditorID}: + {label} = {value}");
                            sheet.Properties.Add(new ObjectProperty()
                            {
                                ActorValue = key.ToNullableLink<IActorValueInformationGetter>(),
                                Value = value,
                            });
                        }
                        touched = true;
                    }

                    if (!touched) continue;

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
