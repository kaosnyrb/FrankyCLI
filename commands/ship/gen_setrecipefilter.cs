using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FrankyCLI
{
    // Set the recipe filter (COBJ FNAM -- the ship-builder category tab) on ConstructibleObjects
    // that already exist in a plugin. gen_shipstruct now attaches this at creation, but any part
    // built before that fix has none; this patches them in place WITHOUT moving a single FormID,
    // through Mutagen so all record/group sizes are recomputed and the file cannot be corrupted.
    //
    //   setrecipefilter <modname> <category> <cobj_editorid> [<cobj_editorid> ...]
    //     category      Category keyword: EditorID (resolved) or 0xHEX FormID.
    //                   Bare hex is Starfield.esm-relative; an index byte targets that master.
    //     cobj_editorid one or more ConstructibleObject EditorIDs to patch.
    //
    // Idempotent: a COBJ already carrying the category is left untouched and reported as such.
    class gen_setrecipefilter
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "setrecipefilter", category, cobj1, cobj2, ...]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setrecipefilter <modname> <category(EditorID|0xHEX)> <cobj_editorid> [<cobj_editorid> ...]");
                return 1;
            }
            string modname = args[0];
            string category = args[2];
            var targets = args.Skip(3).ToList();

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            // The GameEnvironment holds the plugin file OPEN for the life of the `using` block.
            // WriteToBinary to that same path while env is alive throws IOException ("being used
            // by another process"), which silently leaves the file unchanged. So env is scoped to
            // a block that CLOSES before the write -- everything env-dependent (load, resolve,
            // patch) happens inside, and only the captured myMod + datapath cross the boundary.
            // This mirrors gen_shipstruct, which writes after its using(env) block for the same
            // reason. (Cost me an hour of "byte-identical" results: the model was always correct;
            // the write was throwing after the fact.)
            StarfieldMod myMod;
            string datapath;
            int changed = 0;

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

                // Resolve the category keyword to a link.
                IFormLinkGetter<IKeywordGetter> categoryLink;
                if (category.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    if (!uint.TryParse(category.Substring(2), NumberStyles.HexNumber, null, out var catId))
                    {
                        Console.WriteLine($"Error: category '{category}' is not a hex FormID");
                        return 1;
                    }
                    var owner = (catId >> 24) == 0 ? env.LoadOrder[0].ModKey : modKey;
                    categoryLink = new FormKey(owner, catId & 0x00FFFFFF).ToLink<IKeywordGetter>();
                }
                else
                {
                    IFormLinkGetter<IKeywordGetter>? found = null;
                    foreach (var kw in myMod.Keywords)
                        if (string.Equals(kw.EditorID, category, StringComparison.OrdinalIgnoreCase)) found = kw.ToLink<IKeywordGetter>();
                    if (found == null)
                        foreach (var kw in env.LoadOrder[0].Mod!.Keywords)
                            if (string.Equals(kw.EditorID, category, StringComparison.OrdinalIgnoreCase)) found = kw.ToLink<IKeywordGetter>();
                    if (found == null)
                    {
                        Console.WriteLine($"Error: no Keyword with EditorID '{category}' in {modname} or Starfield.esm");
                        return 1;
                    }
                    categoryLink = found;
                }

                // Patch each named COBJ. Fail loud on a name that isn't there rather than
                // silently do nothing -- a typo must not read as success. Deep-copy + re-add
                // rather than mutate in place, so the writer re-serialises from current values.
                foreach (var name in targets)
                {
                    var existing = myMod.ConstructibleObjects.FirstOrDefault(
                        c => string.Equals(c.EditorID, name, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        Console.WriteLine($"Error: no ConstructibleObject '{name}' in {modname}");
                        return 1;
                    }

                    var co = existing.DeepCopy();
                    co.RecipeFilters ??= new ExtendedList<IFormLinkGetter<IKeywordGetter>>();
                    if (co.RecipeFilters.Any(f => f.FormKey == categoryLink.FormKey))
                    {
                        Console.WriteLine($"  {name}: already carries {category} -- left as is");
                        continue;
                    }
                    co.RecipeFilters.Add(categoryLink);
                    myMod.ConstructibleObjects.Remove(existing.FormKey);
                    myMod.ConstructibleObjects.Add(co);
                    Console.WriteLine($"  {name}: recipe filter set -> {category}");
                    changed++;
                }
            }

            if (changed == 0)
            {
                Console.WriteLine("Nothing to write (every named COBJ already had the filter).");
                return 0;
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {changed} record(s) patched, FormIDs unchanged.");
            return 0;
        }
    }
}
