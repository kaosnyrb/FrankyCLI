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
    //   setrecipefilter <modname> [--replace] <category> <cobj_editorid> [<cobj_editorid> ...]
    //     category      Category keyword: EditorID (resolved) or 0xHEX FormID.
    //                   Bare hex is Starfield.esm-relative; an index byte targets that master.
    //     cobj_editorid one or more ConstructibleObject EditorIDs to patch.
    //     --replace     make the category the ONLY filter: drop every other one first.
    //
    // Idempotent: a COBJ already carrying the category is left untouched and reported as such.
    //
    // WHY --replace EXISTS (added 2026-08-14, found by hitting it). RecipeFilters is a LIST and
    // the only write here was .Add, because this tool was written to patch parts that had NO
    // filter at all. That makes a WRONG category structurally uncorrectable: gen_shipstruct
    // DEFAULTS to Category_ShipMod_Structure when --category is omitted, so a cargo part built
    // without the flag carries Structure forever, and adding Cargo afterwards puts it on BOTH
    // tabs -- a second builder row, which is the menu bloat his one-recipe-per-set ruling exists
    // to avoid. Seen in the builder on atsd_co_cargolg_03 (the Drayman), not inferred.
    //
    // ⛔ AND THE SUBTLE HALF, which is why the idempotency check below is not shared: under
    // --replace, "already carries the category" is the WRONG test. A COBJ holding
    // [Structure, Cargo] does carry Cargo, so the plain check would report "left as is" and skip
    // the exact record --replace was asked to fix -- a no-op wearing a success line. Under
    // --replace the skip condition is therefore "carries this and NOTHING ELSE" (count == 1).
    // Every dropped filter is named on the way out: removing something silently is worse than
    // adding something silently, because nothing downstream will ever mention the absence.
    class gen_setrecipefilter
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "setrecipefilter", category, cobj1, cobj2, ...] with --replace
            // permitted anywhere after the mode -- stripped here so it can never be mistaken for
            // a COBJ EditorID (a flag silently consumed as a target would fail loud on the name
            // lookup, but a flag silently consumed as the CATEGORY would not).
            var rest = args.Skip(2).ToList();
            bool replace = rest.RemoveAll(a => string.Equals(a, "--replace", StringComparison.OrdinalIgnoreCase)) > 0;

            if (args.Length < 4 || rest.Count < 2)
            {
                Console.WriteLine("Usage: setrecipefilter <modname> [--replace] <category(EditorID|0xHEX)> <cobj_editorid> [<cobj_editorid> ...]");
                Console.WriteLine("  --replace  make the category the ONLY filter (drops every other one, naming each)");
                return 1;
            }
            string modname = args[0];
            string category = rest[0];
            var targets = rest.Skip(1).ToList();

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

                    bool carries = co.RecipeFilters.Any(f => f.FormKey == categoryLink.FormKey);
                    // Under --replace the goal is "this and nothing else", so carrying it is not
                    // enough -- see the header note. Under append it is exactly the old test.
                    bool satisfied = replace
                        ? carries && co.RecipeFilters.Count == 1
                        : carries;
                    if (satisfied)
                    {
                        Console.WriteLine($"  {name}: already carries {category}"
                                          + (replace ? " and nothing else" : "") + " -- left as is");
                        continue;
                    }

                    if (replace)
                    {
                        var dropped = co.RecipeFilters
                            .Where(f => f.FormKey != categoryLink.FormKey)
                            .Select(f => NameOfKeyword(f.FormKey, myMod, env.LoadOrder[0].Mod))
                            .ToList();
                        co.RecipeFilters.Clear();
                        if (dropped.Count > 0)
                            Console.WriteLine($"  {name}: dropped {dropped.Count} filter(s) -- {string.Join(", ", dropped)}");
                    }
                    if (!co.RecipeFilters.Any(f => f.FormKey == categoryLink.FormKey))
                        co.RecipeFilters.Add(categoryLink);

                    myMod.ConstructibleObjects.Remove(existing.FormKey);
                    myMod.ConstructibleObjects.Add(co);
                    Console.WriteLine($"  {name}: recipe filter{(replace ? "s" : "")} set -> {category}"
                                      + (replace ? " (sole filter)" : ""));
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

        // Report a dropped filter by NAME, not by FormKey. Same two sources the category
        // resolution above uses, in the same order (the mod shadows Starfield.esm); falls back
        // to the raw FormKey rather than inventing a name, because an unresolvable filter is
        // exactly the case someone needs to see verbatim.
        private static string NameOfKeyword(FormKey key, IStarfieldMod mine, IStarfieldModGetter? vanilla)
        {
            foreach (var kw in mine.Keywords)
                if (kw.FormKey == key && !string.IsNullOrEmpty(kw.EditorID)) return kw.EditorID!;
            if (vanilla != null)
                foreach (var kw in vanilla.Keywords)
                    if (kw.FormKey == key && !string.IsNullOrEmpty(kw.EditorID)) return kw.EditorID!;
            return key.ToString();
        }
    }
}
