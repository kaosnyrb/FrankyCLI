using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    // Delete records from a plugin by EditorID, in place, all-or-nothing: every named record
    // must resolve before anything is removed, so a typo can never write half a deletion.
    //
    //   removerecord <modname> <type> <editorid>[,<editorid>...]
    //   types: mstt sntp gbfm cobj flst pkin
    //
    // The case it was written for: retiring a dead flip system (rule 4 -- dead records come out
    // in the same change that orphans them) and pruning the per-orientation COBJs when a family
    // regroups into a FormList set. CELL is deliberately NOT supported -- cells live in the
    // CellBlock/SubBlock tree keyed off their own FormID digits, and removing one safely means
    // removing its placed contents with it; that is a bigger job than a flat group delete and
    // pretending otherwise here would corrupt quietly. If a retirement needs cells gone, say so
    // and it gets built properly.
    //
    // NOTE: this does not check inbound references. A record still referenced elsewhere in the
    // plugin will leave a dangling link -- run gen_inspect on the family first and remove
    // leaf-first (COBJ/FLST before GBFM before MSTT/SNTP).
    class gen_removerecord
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "removerecord", type, editorid list]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: removerecord <modname> <type> <editorid>[,<editorid>...]");
                Console.WriteLine("types: mstt sntp gbfm cobj flst pkin   (cell deliberately unsupported)");
                return 1;
            }
            string modname = args[0];
            string type = args[2].ToLowerInvariant();
            var names = args[3].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;

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

                Func<string, IEnumerable<IStarfieldMajorRecordGetter>> group;
                Action<FormKey> remove;
                switch (type)
                {
                    case "mstt": group = _ => myMod.MoveableStatics; remove = k => myMod.MoveableStatics.Remove(k); break;
                    case "sntp": group = _ => myMod.SnapTemplates; remove = k => myMod.SnapTemplates.Remove(k); break;
                    case "gbfm": group = _ => myMod.GenericBaseForms; remove = k => myMod.GenericBaseForms.Remove(k); break;
                    case "cobj": group = _ => myMod.ConstructibleObjects; remove = k => myMod.ConstructibleObjects.Remove(k); break;
                    case "flst": group = _ => myMod.FormLists; remove = k => myMod.FormLists.Remove(k); break;
                    case "pkin": group = _ => myMod.PackIns; remove = k => myMod.PackIns.Remove(k); break;
                    default:
                        Console.WriteLine($"Error: unknown type '{type}' (mstt sntp gbfm cobj flst pkin)");
                        return 1;
                }

                // Resolve everything first -- all-or-nothing.
                var doomed = new List<IStarfieldMajorRecordGetter>();
                foreach (var name in names)
                {
                    var rec = group(type).FirstOrDefault(
                        r => string.Equals(r.EditorID, name, StringComparison.OrdinalIgnoreCase));
                    if (rec == null)
                    {
                        Console.WriteLine($"Error: no {type} '{name}' in {modname} -- nothing removed");
                        return 1;
                    }
                    doomed.Add(rec);
                }
                foreach (var rec in doomed)
                {
                    remove(rec.FormKey);
                    Console.WriteLine($"  removed {type} {rec.EditorID} [{rec.FormKey}]");
                }
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {names.Count} record(s) removed.");
            return 0;
        }
    }
}
