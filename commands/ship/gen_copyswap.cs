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
    // Wire LayeredMaterialSwap records onto a MoveableStatic's Model.MaterialSwaps -- either by
    // DEEP-COPYING existing ones under new EditorIDs (the "REFL-opaque => scaffold + flag" workflow)
    // or by wiring existing swaps AS-IS (a part that shares another's swaps -- no new records).
    //
    // A LayeredMaterialSwap keeps its base->variant material mapping in an opaque REFL blob that
    // Mutagen reads but cannot author, so such a swap cannot be built from scratch here. This
    // clones a working one (new FormID, new EditorID -- the record + its wiring are done) and
    // leaves the one thing it can't touch, the material mapping, for a CK repoint. Every existing
    // FormID is unchanged; the write goes through Mutagen so all record/group sizes are recomputed
    // and the file cannot be corrupted. Same env-close-before-write discipline as gen_setrecipefilter.
    //
    //   copyswap <modname> <target_mstt> <new=src | existing_swap_editorid> ...
    //
    // Two mixable arg forms: `new=src` DEEP-COPIES src (REFL mapping copied, CK repoint if it
    // differs); a bare `editorid` WIRES an existing swap as-is -- for a part sharing another's swaps
    // (e.g. a starboard wing reusing the port's wing01 swaps: no new records, no repoint).
    //
    // e.g. copyswap avontechstardust atsd_ms_wing01_port \
    //        atsd_matswap_wing01_P=atsd_matswap_sherpa_P ...                          (copy)
    //   or  copyswap avontechstardust atsd_ms_wing01_stb \
    //        atsd_matswap_wing01_P atsd_matswap_wing01_S atsd_matswap_wing01_T        (wire existing)
    class gen_copyswap
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "copyswap", target_mstt, "new=src", ...]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: copyswap <modname> <target_moveablestatic_editorid> <new_editorid>=<source_editorid> [<new>=<source> ...]");
                return 1;
            }
            string modname = args[0];
            string targetMstt = args[2];

            var pairs = new List<(string NewId, string SrcId)>();
            var wireExisting = new List<string>();
            foreach (var a in args.Skip(3))
            {
                if (a.Contains('='))
                {
                    var bits = a.Split('=', 2);
                    if (bits.Length != 2 || bits[0].Trim().Length == 0 || bits[1].Trim().Length == 0)
                    {
                        Console.WriteLine($"Error: '{a}' is not of the form <new_editorid>=<source_editorid>");
                        return 1;
                    }
                    pairs.Add((bits[0].Trim(), bits[1].Trim()));
                }
                else if (a.Trim().Length > 0)
                {
                    wireExisting.Add(a.Trim());
                }
                else
                {
                    Console.WriteLine("Error: empty swap argument");
                    return 1;
                }
            }
            if (pairs.Count == 0 && wireExisting.Count == 0)
            {
                Console.WriteLine("Usage: copyswap <modname> <target_mstt> <new=src | existing_swap_editorid> ...");
                return 1;
            }

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;
            var newLinks = new List<IFormLinkGetter<ILayeredMaterialSwapGetter>>();

            // env holds the plugin file OPEN for the life of the using block; WriteToBinary to that
            // same path while env is alive throws and silently leaves the file unchanged. So env is
            // scoped to close BEFORE the write -- load/resolve/copy inside, only myMod + datapath cross.
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

                foreach (var (newId, srcId) in pairs)
                {
                    if (myMod.LayeredMaterialSwaps.Any(s => string.Equals(s.EditorID, newId, StringComparison.OrdinalIgnoreCase)))
                    {
                        Console.WriteLine($"Error: a LayeredMaterialSwap '{newId}' already exists in {modname} -- refusing to overwrite");
                        return 1;
                    }
                    ILayeredMaterialSwapGetter? source =
                        myMod.LayeredMaterialSwaps.FirstOrDefault(s => string.Equals(s.EditorID, srcId, StringComparison.OrdinalIgnoreCase));
                    if (source == null)
                        source = env.LoadOrder[0].Mod!.LayeredMaterialSwaps.FirstOrDefault(s => string.Equals(s.EditorID, srcId, StringComparison.OrdinalIgnoreCase));
                    if (source == null)
                    {
                        Console.WriteLine($"Error: no LayeredMaterialSwap '{srcId}' in {modname} or Starfield.esm to copy from");
                        return 1;
                    }

                    var copy = myMod.LayeredMaterialSwaps.DuplicateInAsNewRecord(source);
                    copy.EditorID = newId;
                    newLinks.Add(copy.ToLink<ILayeredMaterialSwapGetter>());
                    Console.WriteLine($"  copied {srcId} -> {newId} ({copy.FormKey.ID:X6})");
                }

                foreach (var swapId in wireExisting)
                {
                    ILayeredMaterialSwapGetter? swap =
                        myMod.LayeredMaterialSwaps.FirstOrDefault(s => string.Equals(s.EditorID, swapId, StringComparison.OrdinalIgnoreCase))
                        ?? env.LoadOrder[0].Mod!.LayeredMaterialSwaps.FirstOrDefault(s => string.Equals(s.EditorID, swapId, StringComparison.OrdinalIgnoreCase));
                    if (swap == null)
                    {
                        Console.WriteLine($"Error: no LayeredMaterialSwap '{swapId}' in {modname} or Starfield.esm to wire");
                        return 1;
                    }
                    newLinks.Add(swap.ToLink<ILayeredMaterialSwapGetter>());
                    Console.WriteLine($"  wiring existing {swapId} ({swap.FormKey.ID:X6})");
                }

                var existing = myMod.MoveableStatics.FirstOrDefault(
                    m => string.Equals(m.EditorID, targetMstt, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    Console.WriteLine($"Error: no MoveableStatic '{targetMstt}' in {modname}");
                    return 1;
                }
                var mstt = existing.DeepCopy();
                if (mstt.Model == null)
                {
                    Console.WriteLine($"Error: {targetMstt} has no Model to wire swaps onto");
                    return 1;
                }
                mstt.Model.MaterialSwaps = new ExtendedList<IFormLinkGetter<ILayeredMaterialSwapGetter>>();
                foreach (var l in newLinks) mstt.Model.MaterialSwaps.Add(l);
                myMod.MoveableStatics.Remove(existing.FormKey);
                myMod.MoveableStatics.Add(mstt);
                Console.WriteLine($"  wired {newLinks.Count} swap(s) onto {targetMstt}.Model.MaterialSwaps");
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {pairs.Count} copied, {wireExisting.Count} existing wired onto {targetMstt}; existing FormIDs unchanged.");
            if (pairs.Count > 0)
            {
                Console.WriteLine("!! DEEP COPIES: each carries the SOURCE swap's material mapping in its opaque REFL.");
                Console.WriteLine("   Repoint each in the CK to its own base -> variant, then re-bridge .esm -> .esp.");
            }
            return 0;
        }
    }
}
