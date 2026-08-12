using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;

namespace FrankyCLI
{
    // Re-stamp the ObjectBounds of a part that ALREADY EXISTS, in place, without moving a FormID.
    //
    //   setobnd <modname> <mstt_editorid> <minX,minY,minZ,maxX,maxY,maxZ> [--packin <editorid>]
    //
    // WHY THIS EXISTS. setbounds re-derives the bounds of a flip family's ORIENTATION VARIANTS
    // from their base; nothing could re-stamp the BASE itself. So when his mesh changed -- a
    // resize, a re-export, anything that moves an extent -- the only routes were to rebuild the
    // part, which mints new FormIDs and ORPHANS ITS CELL (removerecord refuses cells by design),
    // or to leave the record claiming a box the model no longer has. Neither is acceptable on a
    // shipped plugin, and "he resizes it after seeing it in game" is the normal case, not the
    // exception -- which is exactly the standing preference this line already keeps:
    // surgical changes over rebuilds.
    //
    // IT PATCHES BOTH RECORDS gen_shipstruct stamps, because they are one fact written twice:
    // the MoveableStatic and its PackIn take the SAME box there, and a PackIn left on the old
    // box declares a volume its own model no longer fills. The PackIn is found by the
    // generator's own naming (_ms_ -> _pkn_) unless --packin names it, because a hand-authored
    // part does not follow that convention (atsd_pk_sherpa_ext). It REPORTS what it patched
    // rather than assuming: a silent 1-of-2 is the failure mode worth refusing.
    //
    // The bounds are SUPPLIED here rather than derived, and the reason is a seam not a
    // shortcut: the mesh parser lives in nif_from_template.py, which already prints this exact
    // box ("unioned from the MESHES -- game space, no axis assumption"). Duplicating a .mesh
    // reader in C# to re-derive it would be a second implementation of the one thing that must
    // not disagree.
    class gen_setobnd
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "setobnd", mstt_editorid, bounds, (--packin <id>)]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setobnd <modname> <mstt_editorid> <minX,minY,minZ,maxX,maxY,maxZ> [--packin <editorid>]");
                Console.WriteLine("  the box nif_from_template.py prints as 'bounds ... unioned from the MESHES'");
                return 1;
            }
            string modname = args[0];
            string target = args[2];
            string spec = args[3];

            string? optPackin = null;
            for (int i = 4; i < args.Length; i++)
            {
                if (args[i] == "--packin")
                {
                    if (i + 1 >= args.Length) { Console.WriteLine("Error: --packin needs a value"); return 1; }
                    optPackin = args[++i];
                }
                else { Console.WriteLine("Error: unknown argument '" + args[i] + "'"); return 1; }
            }

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            var nums = spec.Split(',');
            if (nums.Length != 6)
            {
                Console.WriteLine("Error: bounds want six numbers -- minX,minY,minZ,maxX,maxY,maxZ");
                return 1;
            }
            var v = new float[6];
            for (int i = 0; i < 6; i++)
                if (!float.TryParse(nums[i], out v[i]))
                {
                    Console.WriteLine("Error: '" + nums[i] + "' is not a number");
                    return 1;
                }
            // min must actually be min. A transposed pair produces a record that parses, ships,
            // and declares an inside-out box -- the class of defect this line keeps catching by eye.
            for (int i = 0; i < 3; i++)
                if (v[i] > v[i + 3])
                {
                    Console.WriteLine($"Error: min > max on {"XYZ"[i]} ({v[i]} > {v[i + 3]}) -- the box is inside out");
                    return 1;
                }

            string packinId = optPackin ?? target.Replace("_ms_", "_pkn_");
            if (optPackin == null && packinId == target)
            {
                Console.WriteLine("Error: cannot derive the PackIn name from '" + target + "' (no '_ms_') -- pass --packin");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;
            int changed = 0;

            // env is scoped to close BEFORE the write -- it holds the plugin open, and a same-path
            // WriteToBinary inside the using throws while leaving the old bytes looking like a
            // persisted no-op. Same discipline as setsnap / setname / setrecipefilter.
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

                var box = new ObjectBounds()
                {
                    First = new P3Float(v[0], v[1], v[2]),
                    Second = new P3Float(v[3], v[4], v[5]),
                };

                bool foundMstt = false, foundPackin = false;
                foreach (var ms in myMod.MoveableStatics)
                    if (string.Equals(ms.EditorID, target, StringComparison.OrdinalIgnoreCase))
                    {
                        var old = ms.ObjectBounds;
                        Console.WriteLine($"  {ms.EditorID}: ({old?.First.X},{old?.First.Y},{old?.First.Z}) .. ({old?.Second.X},{old?.Second.Y},{old?.Second.Z})");
                        Console.WriteLine($"    -> ({v[0]},{v[1]},{v[2]}) .. ({v[3]},{v[4]},{v[5]})");
                        ms.ObjectBounds = box.DeepCopy();
                        foundMstt = true; changed++;
                    }
                foreach (var pk in myMod.PackIns)
                    if (string.Equals(pk.EditorID, packinId, StringComparison.OrdinalIgnoreCase))
                    {
                        pk.ObjectBounds = box.DeepCopy();
                        Console.WriteLine($"  {pk.EditorID}: PackIn box re-stamped to match");
                        foundPackin = true; changed++;
                    }

                if (!foundMstt)
                {
                    Console.WriteLine($"Error: no MoveableStatic '{target}' in this plugin");
                    return 1;
                }
                // A PackIn silently left on the old box is the half-done state this command exists
                // to prevent, so say so loudly rather than report a partial success as a success.
                if (!foundPackin)
                {
                    Console.WriteLine($"Error: no PackIn '{packinId}' -- the MoveableStatic was NOT written."
                                      + " Pass --packin with its real EditorID.");
                    return 1;
                }
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {changed} record(s) re-stamped, FormIDs unchanged.");
            return 0;
        }
    }
}
