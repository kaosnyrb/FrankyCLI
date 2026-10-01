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

            int changed = 0;
            using var session = PluginSession.Open(modname);
            if (session == null) return 1;
            var myMod = session.Mod;

            var existing = myMod.GenericBaseForms.FirstOrDefault(
                g => string.Equals(g.EditorID, target, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                // Fail loud on a name that is not there -- a typo must never read as success.
                Console.WriteLine($"Error: no GenericBaseForm '{target}' in {modname}");
                return 1;
            }
            if (Apply(myMod, existing, display)) changed++;
            session.Close();

            if (changed == 0)
            {
                Console.WriteLine("Nothing to write.");
                return 0;
            }
            session.Write();
            Console.WriteLine($"Finished -- {changed} record(s) renamed, FormIDs unchanged.");
            return 0;
        }

        /// <summary>
        /// The CORE: set one GBFM's display name (FullName) on an already-loaded plugin. True when
        /// it changed. Shared by this command and `batch`; the caller validates and owns load/write.
        /// </summary>
        public static bool Apply(StarfieldMod myMod, IGenericBaseFormGetter existing, string display)
        {
            string target = existing.EditorID ?? existing.FormKey.ToString();
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
                return false;
            }
            else
            {
                Console.WriteLine($"  {target}: \"{full.Name?.String}\" -> \"{display}\"");
                full.Name = display;
            }
            myMod.GenericBaseForms.Remove(existing.FormKey);
            myMod.GenericBaseForms.Add(gbfm);
            return true;
        }
    }
}
