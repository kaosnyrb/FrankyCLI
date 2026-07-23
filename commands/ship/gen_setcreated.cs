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
    // Repoint a ConstructibleObject's CreatedObject at a different record, in place, FormID-stable.
    //
    //   setcreated <modname> <cobj_editorid> <target_editorid>
    //
    // The case it was written for: a part with flipped orientations is sold as a SET, and the
    // ship builder gets the set by the COBJ creating a FORMLIST of the oriented GenericBaseForms
    // rather than a single one. gen_shipstruct always writes a base COBJ pointing at the single
    // GBFM; gen_shipflips then writes a second COBJ pointing at the FormList. A flip part must end
    // up with exactly ONE recipe -- vanilla and his own shipped Shipyards parts both do
    // (f_co_ats_ms_dishs_01 -> f_ats_ms_dishs_01_franky, and no base recipe at all) -- so the base
    // one is repointed at the FormList rather than left offering a version that cannot flip.
    //
    // Surgical by design (his standing preference): the alternative is rebuilding the part, which
    // destroys the CK-repointed material-swap REFL payloads that exist nowhere else.
    class gen_setcreated
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "setcreated", cobj_editorid, target_editorid]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setcreated <modname> <cobj_editorid> <target_editorid>");
                return 1;
            }
            string modname = args[0];
            string cobjId = args[2];
            string targetId = args[3];

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
                ModPath modPath = System.IO.Path.Combine(datapath, modname + ".esm");
                myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);

                var cobj = myMod.ConstructibleObjects.FirstOrDefault(
                    c => string.Equals(c.EditorID, cobjId, StringComparison.OrdinalIgnoreCase));
                if (cobj == null)
                {
                    Console.WriteLine($"Error: no ConstructibleObject '{cobjId}' in {modname}");
                    return 1;
                }

                // Resolve the target across the record types a COBJ can legally create here.
                // Fail loud on a name that is not there rather than write a dangling link -- a
                // COBJ pointing at nothing is a recipe that produces nothing, and it looks fine.
                FormKey? target = null;
                string kind = "";
                var fl = myMod.FormLists.FirstOrDefault(f => string.Equals(f.EditorID, targetId, StringComparison.OrdinalIgnoreCase));
                if (fl != null) { target = fl.FormKey; kind = "FormList"; }
                if (target == null)
                {
                    var g = myMod.GenericBaseForms.FirstOrDefault(x => string.Equals(x.EditorID, targetId, StringComparison.OrdinalIgnoreCase));
                    if (g != null) { target = g.FormKey; kind = "GenericBaseForm"; }
                }
                if (target == null)
                {
                    Console.WriteLine($"Error: no FormList or GenericBaseForm '{targetId}' in {modname}");
                    return 1;
                }

                var old = cobj.CreatedObject.FormKey;
                if (old == target.Value)
                {
                    Console.WriteLine($"  {cobjId}: already creates {targetId} -- left as is");
                }
                else
                {
                    var patched = cobj.DeepCopy();
                    patched.CreatedObject = target.Value.ToNullableLink<IConstructibleObjectTargetGetter>();
                    myMod.ConstructibleObjects.Remove(cobj.FormKey);
                    myMod.ConstructibleObjects.Add(patched);
                    Console.WriteLine($"  {cobjId}: creates {old} -> {targetId} [{target.Value}] ({kind})");
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
            Console.WriteLine($"Finished -- {changed} COBJ(s) repointed, FormIDs unchanged.");
            return 0;
        }
    }
}
