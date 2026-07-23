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
    // Replace the snap nodes of a part that ALREADY EXISTS, in place, without moving a FormID --
    // and carry the change through to its flipped orientation variants.
    //
    //   setsnap <modname> <mstt_editorid> "<node spec>" [--dirs Top,Port,Starboard,Bottom]
    //
    // The spec grammar is gen_shipstruct's, reused verbatim (Face@x,y,z, or Node@x,y,z@rx,ry,rz
    // for equipment mounts), so there is exactly one parser and one node vocabulary.
    //
    // WHY THIS EXISTS, and it is a standing preference of his: "I want you to be able to make
    // surgical changes." Rebuilding a part to adjust one number destroys work that no generator
    // can re-author -- above all the CK-repointed LayeredMaterialSwap REFL payloads, which exist
    // only in the plugin. Snap offsets in particular are a thing you get wrong until you see the
    // part in the editor, so "adjust and re-check" has to be cheap or it does not happen.
    //
    // The variants are refreshed rather than rebuilt: gen_shipflips would ADD a second set of
    // records rather than update the existing ones, and duplicate EditorIDs are how the CK ends
    // up with the _frankyDUPLICATE000 mess already sitting in AvontechShipyards.
    class gen_setsnap
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "setsnap", mstt_editorid, spec, (--dirs ...)]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setsnap <modname> <mstt_editorid> \"<node spec>\" [--dirs Top,Port,Starboard,Bottom]");
                Console.WriteLine("  spec: Face@x,y,z[;...]  or  Node@x,y,z@rx,ry,rz  (same grammar as --snap-nodes)");
                return 1;
            }
            string modname = args[0];
            string target = args[2];
            string spec = args[3];

            string? optDirs = null;
            for (int i = 4; i < args.Length; i++)
            {
                if (args[i] == "--dirs")
                {
                    if (i + 1 >= args.Length) { Console.WriteLine("Error: --dirs needs a value"); return 1; }
                    optDirs = args[++i];
                }
                else { Console.WriteLine("Error: unknown argument '" + args[i] + "'"); return 1; }
            }

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            var alias = new Dictionary<string, directions>(StringComparer.OrdinalIgnoreCase)
            {
                { "Fore", directions.ShipModPositionFore },
                { "Aft", directions.ShipModPositionAft },
                { "Port", directions.ShipModPositionPort },
                { "Stbd", directions.ShipModPositionStbd },
                { "Starboard", directions.ShipModPositionStbd },
                { "Top", directions.ShipModPositionTop },
                { "Bottom", directions.ShipModPositionBottom },
                { "Btm", directions.ShipModPositionBottom },
            };
            var dirs = new List<directions>();
            if (optDirs != null)
                foreach (var d in optDirs.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!alias.TryGetValue(d.Trim(), out var dir))
                    {
                        Console.WriteLine("Error: unknown direction '" + d.Trim() + "'. Use: " + string.Join(" ", alias.Keys));
                        return 1;
                    }
                    if (!dirs.Contains(dir)) dirs.Add(dir);
                }

            StarfieldMod myMod;
            string datapath;
            int changed = 0;

            // env is scoped to close BEFORE the write -- it holds the plugin open, and a same-path
            // WriteToBinary inside the using throws and leaves the old bytes looking like a
            // persisted no-op. Same reason as setrecipefilter / setname.
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

                // Parse the spec ONCE, through gen_shipstruct's builder, so the node vocabulary
                // (including the equipment mounts) and the offset/rotation grammar cannot drift
                // between the two commands. The scratch template is only a carrier for its Nodes.
                var scratch = gen_shipstruct.BuildSnapTemplate(myMod, env, "__setsnap_scratch", spec);
                if (scratch == null) return 1;      // BuildSnapTemplate prints the reason
                var newNodes = scratch.Nodes;
                Console.WriteLine($"Parsed {newNodes.Count} node(s) from the spec");

                if (!PatchTemplateOf(myMod, target, newNodes, "base", ref changed)) return 1;

                foreach (var dir in dirs)
                {
                    var variant = target + dir.ToString();
                    var rotated = gen_shipflips.CalculateNodes(dir, newNodes, env);
                    // A named direction whose variant does not exist is a typo or a wrong --dirs;
                    // say so rather than silently doing five of six.
                    if (!PatchTemplateOf(myMod, variant, rotated, dir.ToString().Replace("ShipModPosition", ""), ref changed))
                        return 1;
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
            Console.WriteLine($"Finished -- {changed} SnapTemplate(s) patched, FormIDs unchanged.");
            return 0;
        }

        /// <summary>Replace the nodes of the SnapTemplate that <paramref name="mstt"/> links.</summary>
        static bool PatchTemplateOf(StarfieldMod myMod, string mstt, ExtendedList<SnapNodeEntry> nodes, string label, ref int changed)
        {
            var ms = myMod.MoveableStatics.FirstOrDefault(
                m => string.Equals(m.EditorID, mstt, StringComparison.OrdinalIgnoreCase));
            if (ms == null)
            {
                Console.WriteLine($"Error: no MoveableStatic '{mstt}' in this plugin");
                return false;
            }
            if (ms.SnapTemplate.IsNull)
            {
                Console.WriteLine($"Error: {mstt} links no SnapTemplate to patch");
                return false;
            }
            var tpl = myMod.SnapTemplates.FirstOrDefault(t => t.FormKey == ms.SnapTemplate.FormKey);
            if (tpl == null)
            {
                Console.WriteLine($"Error: {mstt} links SnapTemplate {ms.SnapTemplate.FormKey}, which is not in this plugin");
                return false;
            }

            var patched = tpl.DeepCopy();
            patched.Nodes.Clear();
            uint next = 0;
            foreach (var n in nodes)
            {
                patched.Nodes.Add(n.DeepCopy());
                if (n.NodeID >= next) next = n.NodeID + 1;
            }
            patched.NextNodeID = next;
            myMod.SnapTemplates.Remove(tpl.FormKey);
            myMod.SnapTemplates.Add(patched);
            Console.WriteLine($"  {label,-9} {tpl.EditorID}: {tpl.Nodes.Count} -> {patched.Nodes.Count} node(s)");
            changed++;
            return true;
        }
    }
}
