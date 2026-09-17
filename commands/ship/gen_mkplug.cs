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
    // Mint a PLUG MoveableStatic by cloning a sibling and swapping its ONE snap node, in this
    // plugin, no Creation Kit minute.
    //
    //   mkplug <modname> <new_plug_editorid> --from <plug_editorid> --node <snapnode_editorid>
    //          [--snap-edid <new_template_editorid>] [--keep-behavior] [--dry]
    //
    // WHY THIS EXISTS. A docker attaches through a `_NoRemove` plug placed in its PackIn cell,
    // and the _NoRemove family in Starfield.esm is eleven records with NO Fore member -- so no
    // aft docker was constructible from vanilla parts, and vanilla ships none. Read field by
    // field (2026-09-17), a _NoRemove plug is its plain sibling with exactly two differences:
    // SnapBehavior is Null, and the SnapTemplate holds ONE node of the ToIntOnly family
    // (SnapNode_SHIP_Plug_ToIntOnly_<Face>01) in place of SHIP_Plug_<Face>01, same rotation,
    // same offset. Same mesh, same keywords, same bounds. And Bethesda minted
    // SnapNode_SHIP_Plug_ToIntOnly_Fore01 without ever hanging a template or a plug on it.
    // So the missing plug is a clone with one node swapped -- which is what this does.
    //
    // THE SHAPE IS COPIED, NEVER COMPOSED. The new SnapTemplate takes the source template's
    // NextNodeID, STPT, NodeID, rotation and offset verbatim and replaces only the node link.
    // gen_shipstruct's node vocabulary is deliberately closed (six faces + four mounts), so the
    // ToIntOnly nodes are not spellable there; this verb resolves ANY SnapTemplateNode by
    // EditorID instead, because here the node is the whole point.
    //
    // REFUSALS, each named: the new EditorID already exists (a rerun must not duplicate); the
    // source plug is not in this mod or Starfield.esm; the source template carries other than
    // exactly one node (a multi-node plug is not this shape and would be guessed at); the node
    // is not a SnapTemplateNode anywhere in Starfield.esm.
    //
    // --keep-behavior leaves SnapBehavior as the source has it, for a clone that is NOT a
    // _NoRemove. The default clears it, because that is the case this was built for and the
    // vanilla _NoRemove plugs all carry Null.
    class gen_mkplug
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "mkplug", new_plug_editorid, --from X, --node Y, ...]
            if (args.Length < 3 || args[2].StartsWith("--"))
            {
                Console.WriteLine("Usage: mkplug <modname> <new_plug_editorid> --from <plug_editorid> --node <snapnode_editorid> [--snap-edid <editorid>] [--keep-behavior] [--dry]");
                return 1;
            }
            string modname = args[0];
            string newId = args[2];
            string? fromId = null, nodeId = null, snapId = null;
            bool keepBehavior = false, dry = false;
            for (int i = 3; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--from": if (i + 1 >= args.Length) { Console.WriteLine("Error: --from needs a value"); return 1; } fromId = args[++i]; break;
                    case "--node": if (i + 1 >= args.Length) { Console.WriteLine("Error: --node needs a value"); return 1; } nodeId = args[++i]; break;
                    case "--snap-edid": if (i + 1 >= args.Length) { Console.WriteLine("Error: --snap-edid needs a value"); return 1; } snapId = args[++i]; break;
                    case "--keep-behavior": keepBehavior = true; break;
                    case "--dry": dry = true; break;
                    default: Console.WriteLine("Error: unknown argument '" + args[i] + "'"); return 1;
                }
            }
            if (fromId == null || nodeId == null)
            {
                Console.WriteLine("Error: --from and --node are both required");
                return 1;
            }
            snapId ??= "ShipSnap_" + newId;

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
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
                    Console.WriteLine($"Error: {modname}.esm is not in the load order");
                    return 1;
                }
                ModPath modPath = System.IO.Path.Combine(datapath, modname + ".esm");
                myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);
                var vanilla = env.LoadOrder[0].Mod!;

                // A rerun must refuse, not mint a twin: duplicate EditorIDs are how the CK ends up
                // with _frankyDUPLICATE000 records, and a second plug of one name would be picked
                // by whichever lookup ran first.
                if (myMod.MoveableStatics.Any(m => string.Equals(m.EditorID, newId, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($"Error: MoveableStatic '{newId}' already exists in {modname} -- refusing to mint a second");
                    return 1;
                }
                if (myMod.SnapTemplates.Any(s => string.Equals(s.EditorID, snapId, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($"Error: SnapTemplate '{snapId}' already exists in {modname} -- refusing to mint a second");
                    return 1;
                }

                IMoveableStaticGetter? source =
                    myMod.MoveableStatics.FirstOrDefault(m => string.Equals(m.EditorID, fromId, StringComparison.OrdinalIgnoreCase))
                    ?? vanilla.MoveableStatics.FirstOrDefault(m => string.Equals(m.EditorID, fromId, StringComparison.OrdinalIgnoreCase));
                if (source == null)
                {
                    Console.WriteLine($"Error: no MoveableStatic '{fromId}' in {modname} or Starfield.esm to clone");
                    return 1;
                }
                if (source.SnapTemplate.IsNull)
                {
                    Console.WriteLine($"Error: {fromId} carries no SnapTemplate -- a plug has exactly one node to swap, and this has none");
                    return 1;
                }
                ISnapTemplateGetter? srcTemplate =
                    myMod.SnapTemplates.FirstOrDefault(s => s.FormKey == source.SnapTemplate.FormKey)
                    ?? vanilla.SnapTemplates.FirstOrDefault(s => s.FormKey == source.SnapTemplate.FormKey);
                if (srcTemplate == null)
                {
                    Console.WriteLine($"Error: {fromId}'s SnapTemplate {source.SnapTemplate.FormKey} resolves in neither {modname} nor Starfield.esm");
                    return 1;
                }
                if (srcTemplate.Nodes.Count != 1)
                {
                    Console.WriteLine($"Error: {srcTemplate.EditorID} carries {srcTemplate.Nodes.Count} node(s), not one -- this verb swaps exactly one node and will not guess which");
                    return 1;
                }
                ISnapTemplateNodeGetter? node =
                    vanilla.EnumerateMajorRecords<ISnapTemplateNodeGetter>().FirstOrDefault(n => string.Equals(n.EditorID, nodeId, StringComparison.OrdinalIgnoreCase))
                    ?? myMod.EnumerateMajorRecords<ISnapTemplateNodeGetter>().FirstOrDefault(n => string.Equals(n.EditorID, nodeId, StringComparison.OrdinalIgnoreCase));
                if (node == null)
                {
                    Console.WriteLine($"Error: no SnapTemplateNode '{nodeId}' in Starfield.esm or {modname}");
                    return 1;
                }

                var srcNode = srcTemplate.Nodes[0];
                var oldNodeName = vanilla.EnumerateMajorRecords<ISnapTemplateNodeGetter>()
                    .FirstOrDefault(n => n.FormKey == srcNode.Node.FormKey)?.EditorID ?? srcNode.Node.FormKey.ToString();

                Console.WriteLine($"  source plug     {source.EditorID} [{source.FormKey}]");
                Console.WriteLine($"    model         {source.Model?.File}");
                Console.WriteLine($"    keywords      {string.Join(", ", (source.Keywords ?? new ExtendedList<IFormLinkGetter<IKeywordGetter>>()).Select(k => vanilla.Keywords.FirstOrDefault(x => x.FormKey == k.FormKey)?.EditorID ?? k.FormKey.ToString()))}");
                Console.WriteLine($"    SnapBehavior  {(source.SnapBehavior.IsNull ? "Null" : source.SnapBehavior.FormKey.ToString())} -> {(keepBehavior ? "kept" : "Null")}");
                Console.WriteLine($"  source template {srcTemplate.EditorID} [{srcTemplate.FormKey}] NextNodeID {srcTemplate.NextNodeID}");
                Console.WriteLine($"    node          {oldNodeName} NodeID={srcNode.NodeID} rot={srcNode.Rotation} off={srcNode.Offset}");
                Console.WriteLine($"    ->            {node.EditorID} [{node.FormKey}]  (rotation, offset, NodeID and NextNodeID copied verbatim)");

                if (dry)
                {
                    Console.WriteLine($"  --dry: would mint SnapTemplate {snapId} and MoveableStatic {newId}; nothing written");
                    return 0;
                }

                var template = new SnapTemplate(myMod)
                {
                    EditorID = snapId,
                    NextNodeID = srcTemplate.NextNodeID,
                    STPT = srcTemplate.STPT,
                };
                template.Nodes.Add(new SnapNodeEntry()
                {
                    Node = node.ToLink<ISnapTemplateNodeGetter>(),
                    NodeID = srcNode.NodeID,
                    Rotation = srcNode.Rotation,
                    Offset = srcNode.Offset,
                });
                myMod.SnapTemplates.Add(template);

                var plug = myMod.MoveableStatics.DuplicateInAsNewRecord(source);
                plug.EditorID = newId;
                plug.SnapTemplate = template.ToNullableLink<ISnapTemplateGetter>();
                if (!keepBehavior) plug.SnapBehavior.SetToNull();

                Console.WriteLine($"  minted SnapTemplate  {snapId} [{template.FormKey}] 1 node");
                Console.WriteLine($"  minted MoveableStatic {newId} [{plug.FormKey}] SnapTemplate -> {snapId}, SnapBehavior {(keepBehavior ? "as source" : "Null")}");
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- 2 record(s) minted, existing FormIDs unchanged.");
            return 0;
        }
    }
}
