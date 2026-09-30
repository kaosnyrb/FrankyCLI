using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FrankyCLI
{
    // Set MenuSortOrder on ConstructibleObjects that already exist -- where a recipe sits in the
    // ship builder's list within its category. Every generator writes 1 at creation and nothing
    // could change it afterwards.
    //
    //   setsortorder <modname> <cobj_editorid>=<value>[,<cobj_editorid>=<value>...] [--dry]
    //
    // MANY RECIPES IN ONE CALL on purpose: each FrankyCLI call loads the whole load order (~28 s),
    // so a menu pass over N recipes as N calls pays N loads (2026-09-30, the same lesson as
    // rotate_part's batched member read). ALL OR NOTHING: an unknown EditorID or an unparseable
    // value refuses the whole write. Values parse with the INVARIANT culture, so 13.2 means 13.2
    // on any machine. Patches in place WITHOUT moving a FormID; idempotent (a recipe already at its
    // value is reported and left alone). Same env-close-before-write shape as setdesc.
    class gen_setsortorder
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "setsortorder", pairs, (--dry)]
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: setsortorder <modname> <cobj_editorid>=<value>[,<cobj>=<value>...] [--dry]");
                return 1;
            }
            string modname = args[0];
            bool dry = args.Skip(3).Contains("--dry");
            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            var wanted = new List<(string edid, float value)>();
            foreach (var pair in args[2].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=');
                if (kv.Length != 2 || !float.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                {
                    Console.WriteLine($"Error: '{pair}' is not <cobj_editorid>=<number> -- nothing written");
                    return 1;
                }
                wanted.Add((kv[0].Trim(), v));
            }
            var dupes = wanted.GroupBy(w => w.edid, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Count > 0)
            {
                Console.WriteLine($"Error: named twice: {string.Join(", ", dupes)} -- nothing written");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;
            int changed = 0;
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                datapath = env.DataFolderPath;
                if (!env.LoadOrder.ModExists(new ModKey(modname, ModType.Master)))
                {
                    Console.WriteLine($"Error: {modname}.esm is not in the load order");
                    return 1;
                }
                ModPath modPath = System.IO.Path.Combine(datapath, modname + ".esm");
                myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);

                var missing = wanted.Where(w => !myMod.ConstructibleObjects.Any(
                    c => string.Equals(c.EditorID, w.edid, StringComparison.OrdinalIgnoreCase))).Select(w => w.edid).ToList();
                if (missing.Count > 0)
                {
                    Console.WriteLine($"Error: no ConstructibleObject in {modname}: {string.Join(", ", missing)} -- nothing written");
                    return 1;
                }

                foreach (var (edid, value) in wanted)
                {
                    var existing = myMod.ConstructibleObjects.First(
                        c => string.Equals(c.EditorID, edid, StringComparison.OrdinalIgnoreCase));
                    if (existing.MenuSortOrder == value)
                    {
                        Console.WriteLine($"  {edid}: already {value.ToString(CultureInfo.InvariantCulture)} -- left as is");
                        continue;
                    }
                    Console.WriteLine($"  {edid}: {existing.MenuSortOrder.ToString(CultureInfo.InvariantCulture)} -> {value.ToString(CultureInfo.InvariantCulture)}");
                    var cobj = existing.DeepCopy();
                    cobj.MenuSortOrder = value;
                    myMod.ConstructibleObjects.Remove(existing.FormKey);
                    myMod.ConstructibleObjects.Add(cobj);
                    changed++;
                }
            }

            if (dry)
            {
                Console.WriteLine($"--dry: {changed} change(s) shown, nothing written.");
                return 0;
            }
            if (changed == 0)
            {
                Console.WriteLine("Nothing to write.");
                return 0;
            }
            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {changed} record(s) updated, FormIDs unchanged.");
            return 0;
        }
    }
}
