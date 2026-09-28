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
    //   types: mstt sntp gbfm cobj flst pkin stat cell
    //
    // The case it was written for: retiring a dead flip system (rule 4 -- dead records come out
    // in the same change that orphans them) and pruning the per-orientation COBJs when a family
    // regroups into a FormList set.
    //
    // CELL (added 2026-09-28, retiring the half-size hab_01): a cell is found in the
    // CellBlock/SubBlock tree and removed WITH its placed contents, which are its children in the
    // record tree. It is meant for PackIn STORAGE cells, the only cells this line authors.
    //
    // THE INBOUND GUARD (same day): before writing, every surviving record's FormLinks are walked,
    // and if any points at a removed record -- or at a placed object inside a removed cell -- the
    // tool refuses and writes NOTHING. So order is enforced rather than remembered: remove
    // leaf-first (COBJ/FLST, then GBFM, then PKIN, then its CELL, then MSTT/STAT/SNTP), and a
    // wrong order is a printed refusal, never a dangling link.
    class gen_removerecord
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "removerecord", type, editorid list]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: removerecord <modname> <type> <editorid>[,<editorid>...]");
                Console.WriteLine("types: mstt sntp gbfm cobj flst pkin stat cell");
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
                    case "stat": group = _ => myMod.Statics; remove = k => myMod.Statics.Remove(k); break;
                    case "cell":
                        group = _ => myMod.Cells.Records.SelectMany(b => b.SubBlocks).SelectMany(sb => sb.Cells);
                        remove = k =>
                        {
                            foreach (var sb in myMod.Cells.Records.SelectMany(b => b.SubBlocks))
                                sb.Cells.RemoveAll(c => c.FormKey == k);
                        };
                        break;
                    default:
                        Console.WriteLine($"Error: unknown type '{type}' (mstt sntp gbfm cobj flst pkin stat cell)");
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
                // Everything that leaves: the named records, plus a removed cell's placed children.
                var gone = new HashSet<FormKey>();
                foreach (var rec in doomed)
                    foreach (var r in rec.EnumerateMajorRecords())
                        gone.Add(r.FormKey);
                foreach (var rec in doomed) gone.Add(rec.FormKey);

                foreach (var rec in doomed)
                    remove(rec.FormKey);

                // The inbound guard: refuse the write if anything that survives still links to what left.
                var dangling = new List<string>();
                foreach (var rec in myMod.EnumerateMajorRecords())
                {
                    if (gone.Contains(rec.FormKey)) continue;
                    foreach (var link in rec.EnumerateFormLinks())
                        if (!link.IsNull && gone.Contains(link.FormKey))
                            dangling.Add($"{rec.EditorID ?? "(no EditorID)"} [{rec.FormKey}] -> {link.FormKey}");
                }
                if (dangling.Count > 0)
                {
                    Console.WriteLine($"REFUSED -- {dangling.Count} surviving link(s) point at what would be removed; nothing written:");
                    foreach (var d in dangling.Distinct().Take(20)) Console.WriteLine("  " + d);
                    return 1;
                }
                foreach (var rec in doomed)
                    Console.WriteLine($"  removed {type} {rec.EditorID} [{rec.FormKey}]");
                Console.WriteLine($"  inbound guard: 0 surviving links into {gone.Count} removed record(s)");
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {names.Count} record(s) removed.");
            return 0;
        }
    }
}
