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
    // Set COBJ.Value on ConstructibleObjects that already exist: the recipe's price in credits, before
    // the player's own discounts (faction, perks). gen_shipstruct writes 1000 at creation and nothing
    // could change it afterwards, so every generated part shipped on a price nobody chose.
    //
    //   setvalue <modname> <cobj_editorid>=<credits>[,<cobj_editorid>=<credits>...] [--dry]
    //
    // The price scale is read off vanilla by price_bands.py (Stardust repo) and written up in the
    // [Bethesda] manual, part 34. This command only writes the number it is given.
    //
    // Same shape as setsortorder, on purpose: MANY RECIPES IN ONE CALL (one load order load, ~28 s,
    // for a whole pricing pass), ALL OR NOTHING (an unknown EditorID, a non-integer or a duplicate
    // refuses the whole write), idempotent (a recipe already at its price is reported and left),
    // FormIDs never move. Credits are whole numbers; a decimal refuses rather than being rounded.
    class gen_setvalue
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "setvalue", pairs, (--dry)]
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: setvalue <modname> <cobj_editorid>=<credits>[,<cobj>=<credits>...] [--dry]");
                return 1;
            }
            string modname = args[0];
            bool dry = args.Skip(3).Contains("--dry");
            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            var wanted = new List<(string edid, uint value)>();
            foreach (var pair in args[2].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=');
                if (kv.Length != 2 || !uint.TryParse(kv[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var v))
                {
                    Console.WriteLine($"Error: '{pair}' is not <cobj_editorid>=<whole credits> -- nothing written");
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
                    if (existing.Value == value)
                    {
                        Console.WriteLine($"  {edid}: already {value} -- left as is");
                        continue;
                    }
                    Console.WriteLine($"  {edid}: {existing.Value} -> {value}");
                    var cobj = existing.DeepCopy();
                    cobj.Value = value;
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
