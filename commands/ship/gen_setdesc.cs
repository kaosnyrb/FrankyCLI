using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Linq;

namespace FrankyCLI
{
    // Set the DESCRIPTION on a ConstructibleObject that already exists -- the flavour text the
    // ship builder shows on the part card. Every ship generator defaults it to the bare <item>
    // stub, which is what players see until someone sets it.
    //
    //   setdesc <modname> <cobj_editorid> --from <source_cobj_editorid>
    //   setdesc <modname> <cobj_editorid> "<text>"
    //
    // --from copies the description BYTE-EXACT from another COBJ anywhere in the load order
    // (vanilla included, last override wins). That is the primary mode on purpose: reusing
    // Bethesda's own category blurbs ("Structural components are largely cosmetic...") keeps a
    // paid mod's UI text out of any generative-AI content question entirely, and copying at the
    // record layer means no shell quoting, no transcription, no curly-quote mangling -- the
    // string never passes through a human or a terminal. (Vanilla descriptions are LOCALIZED --
    // a string-table index, not inline text -- so Mutagen resolves the string at read and this
    // tool fails loud if the resolution comes back empty rather than writing a blank.)
    //
    // Patches in place WITHOUT moving a FormID; same env-close-before-write shape as setname.
    // Idempotent: a record already carrying the text is left untouched and reported as such.
    class gen_setdesc
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "setdesc", cobj_editorid, ("--from", source_editorid) | text]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setdesc <modname> <cobj_editorid> --from <source_cobj_editorid>");
                Console.WriteLine("       setdesc <modname> <cobj_editorid> \"<text>\"");
                return 1;
            }
            string modname = args[0];
            string target = args[2];
            bool fromMode = args[3] == "--from";
            string? literal = fromMode ? null : args[3];
            string? sourceName = fromMode ? (args.Length > 4 ? args[4] : null) : null;
            if (fromMode && sourceName == null)
            {
                Console.WriteLine("Error: --from needs a source COBJ EditorID");
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

            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                datapath = env.DataFolderPath;

                ModKey modKey = new ModKey(modname, ModType.Master);
                if (!env.LoadOrder.ModExists(modKey))
                {
                    Console.WriteLine($"Error: {modname}.esm is not in the load order");
                    return 1;
                }

                string text;
                if (fromMode)
                {
                    // Last override wins: walk the load order back to front.
                    string? resolved = null; string? foundIn = null;
                    foreach (var listing in env.LoadOrder.ListedOrder.Reverse())
                    {
                        var src = listing.Mod?.ConstructibleObjects?.FirstOrDefault(
                            c => string.Equals(c.EditorID, sourceName, StringComparison.OrdinalIgnoreCase));
                        if (src != null) { resolved = src.Description?.String; foundIn = listing.ModKey.FileName; break; }
                    }
                    if (foundIn == null)
                    {
                        Console.WriteLine($"Error: no COBJ '{sourceName}' anywhere in the load order");
                        return 1;
                    }
                    if (string.IsNullOrWhiteSpace(resolved))
                    {
                        // A localized string that failed to resolve reads as empty -- writing that
                        // forward would silently blank the card. Refuse instead.
                        Console.WriteLine($"Error: '{sourceName}' ({foundIn}) has an empty/unresolvable Description -- refusing to copy a blank");
                        return 1;
                    }
                    text = resolved;
                    Console.WriteLine($"  source {sourceName} ({foundIn}): \"{text}\"");
                }
                else
                {
                    text = literal!;
                }

                ModPath modPath = System.IO.Path.Combine(datapath, modname + ".esm");
                myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);

                var existing = myMod.ConstructibleObjects.FirstOrDefault(
                    c => string.Equals(c.EditorID, target, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    Console.WriteLine($"Error: no ConstructibleObject '{target}' in {modname}");
                    return 1;
                }

                if (string.Equals(existing.Description?.String, text, StringComparison.Ordinal))
                {
                    Console.WriteLine($"  {target}: already carries this description -- left as is");
                }
                else
                {
                    var cobj = existing.DeepCopy();
                    Console.WriteLine($"  {target}: \"{existing.Description?.String}\" -> \"{text}\"");
                    cobj.Description = text;
                    myMod.ConstructibleObjects.Remove(existing.FormKey);
                    myMod.ConstructibleObjects.Add(cobj);
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
            Console.WriteLine($"Finished -- {changed} record(s) updated, FormIDs unchanged.");
            return 0;
        }
    }
}
