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
    // Set Model.LightLayer (subrecord FLLD) on a MoveableStatic that already exists.
    //
    //   setlightlayer <modname> <mstt_editorid>[,<mstt_editorid>...] [value]
    //
    // WHY THIS EXISTS. A ship part whose Model carries no LightLayer builds, attaches, flips and
    // paints -- and draws NOTHING in the ship builder. gen_shipstruct never wrote the field, and
    // that went unnoticed across thirteen shipped parts because every one of them passed through a
    // Creation Kit save (to repoint a swap, place a flare, swap a label) and the CK writes it. The
    // first part ever taken to the glass WITHOUT a CK save in between -- atsd_vent01_rear,
    // 2026-07-30 -- was invisible, which is how the gap finally surfaced.
    //
    // The generator is fixed at the root, so new parts do not need this. It is here for records
    // that already exist, because a rebuild is not a neutral operation once a part has been through
    // his hands: a CK-repointed LayeredMaterialSwap lives only in the plugin, there is no source for
    // it, and regenerating destroys it. FormID-stable, like the rest of the set* family.
    //
    // Vanilla reference: SMOD_Struct_Deimos_Hull_A carries LightLayer 1, which is the default here.
    //
    // Idempotent: a record already carrying the value is left untouched and reported as such.
    class gen_setlightlayer
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "setlightlayer", mstt_editorids, (value)]
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: setlightlayer <modname> <mstt_editorid>[,<mstt_editorid>...] [value]");
                Console.WriteLine("Default value 1 (vanilla SMOD_Struct_Deimos_Hull_A).");
                return 1;
            }
            string modname = args[0];
            var targets = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(t => t.Trim()).ToList();

            uint value = 1;
            if (args.Length >= 4 && !uint.TryParse(args[3], out value))
            {
                Console.WriteLine($"Error: '{args[3]}' is not a valid LightLayer value");
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

            // env holds the plugin open, so it is scoped to close before the write (same reason as
            // gen_setname / gen_setrecipefilter -- a same-path WriteToBinary inside the using throws
            // and leaves the old bytes looking like a persisted no-op).
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

                foreach (var target in targets)
                {
                    var existing = myMod.MoveableStatics.FirstOrDefault(
                        m => string.Equals(m.EditorID, target, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        // Fail loud on a name that is not there -- a typo must never read as success.
                        Console.WriteLine($"Error: no MoveableStatic '{target}' in {modname}");
                        return 1;
                    }

                    if (existing.Model == null)
                    {
                        // A MoveableStatic with no Model at all is a different defect, and silently
                        // authoring one here would hide it.
                        Console.WriteLine($"Error: {target} has no Model block -- refusing to invent one");
                        return 1;
                    }

                    if (existing.Model.LightLayer == value)
                    {
                        Console.WriteLine($"  {target}: LightLayer already {value} -- left as is");
                        continue;
                    }

                    var mstt = existing.DeepCopy();
                    var was = mstt.Model!.LightLayer;
                    mstt.Model!.LightLayer = value;
                    Console.WriteLine($"  {target}: LightLayer {(was == null ? "<absent>" : was.ToString())} -> {value}");

                    myMod.MoveableStatics.Remove(existing.FormKey);
                    myMod.MoveableStatics.Add(mstt);
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
            Console.WriteLine($"Finished -- {changed} MoveableStatic(s) patched, FormIDs unchanged.");
            return 0;
        }
    }
}
