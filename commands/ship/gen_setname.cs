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
    // Set the DISPLAY NAME (FullName) on a GenericBaseForm that already exists -- the string the
    // ship builder shows on the part card. gen_shipstruct now takes --name at creation, but any
    // part built before that defaulted FullName to the <item> argument, which is why parts shipped
    // showing their EditorID stub ("eng01") instead of a product name.
    //
    //   setname <modname> <gbfm_editorid> "<display name>"
    //
    // Patches in place WITHOUT moving a FormID, through Mutagen so record/group sizes are
    // recomputed. Use this rather than regenerating whenever the plugin carries hand work that a
    // rebuild would destroy -- CK-authored REFL payloads (a repointed LayeredMaterialSwap) are the
    // standing example: they cannot be re-authored by any generator, so the plugin holding them is
    // the only copy.
    //
    // Idempotent: a record already carrying the name is left untouched and reported as such.
    class gen_setname
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "setname", gbfm_editorid, display name]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setname <modname> <gbfm_editorid> \"<display name>\"");
                return 1;
            }
            string modname = args[0];
            string target = args[2];
            string display = args[3];

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;
            int changed = 0;

            // env holds the plugin open, so it is scoped to close before the write (same reason as
            // gen_setrecipefilter -- a same-path WriteToBinary inside the using throws and leaves
            // the old bytes looking like a persisted no-op).
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

                var existing = myMod.GenericBaseForms.FirstOrDefault(
                    g => string.Equals(g.EditorID, target, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    // Fail loud on a name that is not there -- a typo must never read as success.
                    Console.WriteLine($"Error: no GenericBaseForm '{target}' in {modname}");
                    return 1;
                }

                var gbfm = existing.DeepCopy();
                var full = gbfm.Components.OfType<FullNameComponent>().FirstOrDefault();
                if (full == null)
                {
                    gbfm.Components.Add(new FullNameComponent() { Name = display });
                    Console.WriteLine($"  {target}: no FullName component -- added \"{display}\"");
                }
                else if (string.Equals(full.Name?.String, display, StringComparison.Ordinal))
                {
                    Console.WriteLine($"  {target}: already named \"{display}\" -- left as is");
                    goto done;
                }
                else
                {
                    Console.WriteLine($"  {target}: \"{full.Name?.String}\" -> \"{display}\"");
                    full.Name = display;
                }

                myMod.GenericBaseForms.Remove(existing.FormKey);
                myMod.GenericBaseForms.Add(gbfm);
                changed++;

                done: ;
            }

            if (changed == 0)
            {
                Console.WriteLine("Nothing to write.");
                return 0;
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {changed} record(s) renamed, FormIDs unchanged.");
            return 0;
        }
    }
}
