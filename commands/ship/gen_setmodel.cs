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
    // Repoint an existing record's Model.File at a different NIF, FormID-stable.
    //
    //   setmodel <modname> <editorid> <Meshes\path\to.nif> [--dry]
    //
    // WHY THIS EXISTS. Every site in this repo that touches a Model CREATES one, at record
    // creation: gen_shipstruct, gen_newstatic, gen_upgradegenerator. Nothing could edit one,
    // so renaming a NIF meant rebuilding the record -- and a rebuild DESTROYS the CK-repointed
    // LayeredMaterialSwap REFL payloads, which have no source anywhere outside the plugin.
    // That is the same reason gen_setlightlayer exists rather than a rebuild.
    //
    // ⛔⛔ IT WRITES Model.File AND NOTHING ELSE, AND THAT IS THE ENTIRE DESIGN. Replacing the
    // Model wholesale is one line shorter and would silently drop:
    //   * Model.LightLayer -- the field without which a part BUILDS, LINKS, VALIDATES CLEAN
    //     AND THEN RENDERS NOTHING. It hid on thirteen parts of this line behind a workflow
    //     that happened to cure it, and cost two of his game restarts to find.
    //   * Model.Flags 'Support Model Only Swap' -- without it the part is not paintable.
    // Both are asserted UNCHANGED after the write rather than assumed, because the failure
    // mode of losing them is a part that passes every check and is invisible in game.
    //
    // ⛔ AND IT REFUSES A PATH THAT DOES NOT RESOLVE ON DISK. A model reference to a file that
    // does not exist yet builds clean and renders nothing -- indistinguishable, from inside
    // the plugin, from a correct one. If the NIF is only in a .ba2 and not loose, pass
    // --allow-missing and it will say so in as many words rather than staying quiet.
    class gen_setmodel
    {
        public static int Generate(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setmodel <modname> <editorid> <Meshes\\path\\to.nif> [--dry] [--allow-missing]");
                Console.WriteLine();
                Console.WriteLine("  Repoints an existing record's Model.File. FormID-stable.");
                Console.WriteLine("  Writes Model.File ONLY -- LightLayer and Flags are preserved and asserted.");
                Console.WriteLine("  Records with a Model: MoveableStatic, Static.");
                return 1;
            }

            string modname = args[0];
            string edid = args[2].Trim();
            string newPath = args[3].Trim();
            bool dry = false, allowMissing = false;
            for (int i = 4; i < args.Length; i++)
            {
                if (args[i] == "--dry") { dry = true; continue; }
                if (args[i] == "--allow-missing") { allowMissing = true; continue; }
                Console.WriteLine("Error: unknown option " + args[i]);
                return 1;
            }

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }
            if (edid.Length == 0 || newPath.Length == 0)
            {
                Console.WriteLine("Error: EditorID and model path must both be non-empty");
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
                    Console.WriteLine("Error: " + modname + ".esm is not in the load order");
                    return 1;
                }
                ModPath modPath = System.IO.Path.Combine(datapath, modname + ".esm");
                myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);

                // Refuse the ambiguous source the same way setedid does -- there is no correct
                // guess, and picking one silently repoints a record nobody named.
                var msttMatches = myMod.MoveableStatics
                    .Where(m => string.Equals(m.EditorID, edid, StringComparison.OrdinalIgnoreCase)).ToList();
                var statMatches = myMod.Statics
                    .Where(s => string.Equals(s.EditorID, edid, StringComparison.OrdinalIgnoreCase)).ToList();
                int total = msttMatches.Count + statMatches.Count;
                if (total == 0)
                {
                    // Say WHAT WAS SEARCHED, not just that nothing was found -- "not found" and
                    // "not looked for" read identically and are different facts.
                    Console.WriteLine("Error: no MoveableStatic or Static '" + edid + "' in " + modname
                                      + " (those are the two record types this searches).");
                    return 1;
                }
                if (total > 1)
                {
                    Console.WriteLine("Error: '" + edid + "' matches " + total + " records -- refusing to guess.");
                    return 1;
                }

                string oldPath;
                uint? lightLayerBefore;
                int flagsBefore;
                Model model;
                string kind;
                if (msttMatches.Count == 1)
                {
                    var r = msttMatches[0];
                    kind = "MoveableStatic";
                    if (r.Model == null) { Console.WriteLine("Error: " + edid + " carries no Model block"); return 1; }
                    model = r.Model;
                }
                else
                {
                    var r = statMatches[0];
                    kind = "Static";
                    if (r.Model == null) { Console.WriteLine("Error: " + edid + " carries no Model block"); return 1; }
                    model = r.Model;
                }
                oldPath = model.File?.GivenPath ?? "";
                lightLayerBefore = model.LightLayer;
                flagsBefore = (int)(model.Flags ?? 0);

                if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Error: " + edid + " already points at " + newPath + " -- nothing to do");
                    return 1;
                }

                var onDisk = System.IO.Path.Combine(datapath, newPath.Replace('\\', System.IO.Path.DirectorySeparatorChar));
                bool exists = System.IO.File.Exists(onDisk);
                if (!exists && !allowMissing)
                {
                    Console.WriteLine("Error: " + newPath + " does not exist under " + datapath);
                    Console.WriteLine("  A model reference to a file that is not there builds clean and renders");
                    Console.WriteLine("  NOTHING -- from inside the plugin it is indistinguishable from a correct");
                    Console.WriteLine("  one. Deploy the NIF first, or pass --allow-missing if it lives only in a .ba2.");
                    return 1;
                }

                Console.WriteLine("  " + kind + "  " + edid);
                Console.WriteLine("    model : " + oldPath);
                Console.WriteLine("         -> " + newPath + (exists ? "   (resolves loose)" : "   (NOT on disk -- --allow-missing)"));
                Console.WriteLine("    keeping LightLayer=" + (lightLayerBefore?.ToString() ?? "<absent>")
                                  + "  Flags=0x" + flagsBefore.ToString("X"));

                if (dry)
                {
                    Console.WriteLine("\n--dry: nothing written");
                    return 0;
                }

                model.File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<
                    Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>(newPath);

                // ASSERT THE THINGS WE DID NOT MEAN TO TOUCH ARE UNTOUCHED. This is the whole
                // point of editing the field rather than replacing the block, so it is checked
                // rather than trusted -- a silently dropped LightLayer is a part that renders
                // nothing and passes every other gate.
                if (model.LightLayer != lightLayerBefore)
                {
                    Console.WriteLine("REFUSING TO WRITE: LightLayer changed "
                                      + lightLayerBefore + " -> " + model.LightLayer);
                    return 1;
                }
                if ((int)(model.Flags ?? 0) != flagsBefore)
                {
                    Console.WriteLine("REFUSING TO WRITE: Model.Flags changed 0x"
                                      + flagsBefore.ToString("X") + " -> 0x" + ((int)(model.Flags ?? 0)).ToString("X"));
                    return 1;
                }
            }

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine("\nwrote " + datapath + "\\" + modname + ".esm");
            return 0;
        }
    }
}
