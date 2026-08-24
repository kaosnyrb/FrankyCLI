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
    // Set LINKED REFERENCES (XLKR) on a PlacedObject that already exists, FormID-stable.
    //
    //   setlinks <modname> <ref> <keyword>=<target>[,<keyword>=<target>...]
    //   setlinks <modname> <ref> --clear
    //     --replace    wipe the existing links and set exactly these
    //
    //   <ref> / <target>   a PlacedObject FormID: 0F3287, 0x000F3287 or 0F3287:mod.esm
    //   <keyword>          a Keyword EditorID (LinkShipModule) or FormID (0x2C1001)
    //
    // WHY THIS EXISTS. A ship HAB wires its interior door pieces to their exterior plugs
    // with linked references, and there was no way to read or write them outside the
    // Creation Kit. Doing it by hand is: open the ref, LinkedRef tab, Choose Reference,
    // pick the cell, pick the ref, pick the keyword, OK -- times two links times however
    // many faces. The read half now lives in gen_inspect (Cell and refr both render
    // LinkedReferences); this is the write half.
    //
    // ⭐ IT MERGES BY KEYWORD. IT DOES NOT REPLACE WHOLESALE, AND THAT IS A DELIBERATE
    // DEPARTURE FROM ITS SIBLINGS, WHICH IS WORTH THE INCONSISTENCY. setsnap does
    // Nodes.Clear() and setflipset replaces a FormList's items outright, so both demand
    // the COMPLETE intended list every time -- and setsnap's version of that would have
    // deleted the mount node off every shipped wing, breaking every player ship already
    // using one, had it not been caught by reading the source first (2026-08-18).
    // A linked-ref list is exactly the shape that trips over: a door piece carries
    // LinkShipModule (shared, boring, easy to forget) alongside the one you came to set.
    // Wholesale replace would silently drop it. So the default is additive and the
    // destructive mode is opt-in and named.
    //
    // VALIDATE EVERYTHING, THEN MUTATE. Every ref, keyword and target is resolved before
    // a single link is written -- a lookup that fails halfway leaves a partly-wired
    // plugin, and a half-wired door is the silent-nothing class this command exists to
    // remove rather than relocate.
    //
    // ⛔ A DANGLING TARGET IS REFUSED. The CK will happily let you point a linked ref at
    // nothing; this will not. A link to a FormID that resolves to no PlacedObject is
    // indistinguishable from a correct one in every view, including gen_inspect's, and
    // it fails at runtime rather than at authoring time.
    //
    // Idempotent: a link already carrying the same target is left untouched and reported.
    class gen_setlinks
    {
        /// A PlacedObject reference found in the mod, plus the cell that holds it --
        /// needed because a REFR is edited through its cell's entry list.
        private readonly struct Located
        {
            public readonly PlacedObject Ref;
            public readonly Cell Cell;
            public Located(PlacedObject r, Cell c) { Ref = r; Cell = c; }
        }

        /// Parse a FormID in any of the shapes the read side prints: `0F3287`,
        /// `0x000F3287`, `0F3287:avontechstardust.esm`. Permissive on FORMAT only --
        /// the id itself still has to resolve, which is checked at the call site.
        private static bool TryParseFormId(string s, out uint id)
        {
            id = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var t = s.Trim();
            int colon = t.IndexOf(':');
            if (colon >= 0) t = t.Substring(0, colon);
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t.Substring(2);
            return uint.TryParse(t, System.Globalization.NumberStyles.HexNumber,
                                 System.Globalization.CultureInfo.InvariantCulture, out id);
        }

        private static IEnumerable<Cell> AllCells(StarfieldMod mod)
        {
            foreach (var block in mod.Cells)
                foreach (var sub in block.SubBlocks)
                    foreach (var cell in sub.Cells)
                        yield return cell;
            foreach (var ws in mod.Worldspaces)
            {
                if (ws.TopCell != null) yield return ws.TopCell;
                foreach (var b in ws.SubCells)
                    foreach (var sb in b.Items)
                        foreach (var c in sb.Items)
                            yield return c;
            }
        }

        private static Located? Find(StarfieldMod mod, uint id)
        {
            foreach (var cell in AllCells(mod))
                foreach (var entry in cell.Persistent.Concat(cell.Temporary))
                    if (entry is PlacedObject po && (po.FormKey.ID & 0xFFFFFF) == (id & 0xFFFFFF))
                        return new Located(po, cell);
            return null;
        }

        public static int Generate(string[] args)
        {
            // args: [modname, "setlinks", ref, spec, ...flags]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setlinks <modname> <ref> <keyword>=<target>[,<keyword>=<target>...]");
                Console.WriteLine("       setlinks <modname> <ref> --clear");
                Console.WriteLine("         --replace   wipe existing links and set exactly these (default MERGES by keyword)");
                Console.WriteLine();
                Console.WriteLine("  <ref>/<target>  PlacedObject FormID: 0F3287, 0x000F3287 or 0F3287:mod.esm");
                Console.WriteLine("  <keyword>       Keyword EditorID (LinkShipModule) or FormID (0x2C1001)");
                Console.WriteLine();
                Console.WriteLine("  Read the current state with:  gen_inspect Cell <cell>   (renders LinkedReferences)");
                return 1;
            }

            string modname = args[0];
            string refArg = args[2];
            bool clear = false, replace = false;
            string? spec = null;

            for (int i = 3; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--clear": clear = true; break;
                    case "--replace": replace = true; break;
                    default:
                        if (args[i].StartsWith("--"))
                        {
                            Console.WriteLine("Error: unknown option " + args[i]);
                            return 1;
                        }
                        if (spec != null)
                        {
                            Console.WriteLine("Error: more than one link spec given ('" + spec + "' and '" + args[i] + "')."
                                              + " Separate pairs with commas, not spaces.");
                            return 1;
                        }
                        spec = args[i];
                        break;
                }
            }

            // --clear and a spec are a direct contradiction. Refuse rather than let
            // precedence decide it silently -- whichever way it fell, half the callers
            // would be wrong and the record would look deliberate either way.
            if (clear && spec != null)
            {
                Console.WriteLine("Error: --clear removes every link; a <keyword>=<target> spec adds one. Pick one.");
                return 1;
            }
            if (clear && replace)
            {
                Console.WriteLine("Error: --replace qualifies a spec, and --clear takes none. --clear alone is the wipe.");
                return 1;
            }
            if (!clear && spec == null)
            {
                Console.WriteLine("Error: nothing to do -- give a <keyword>=<target> spec, or --clear.");
                return 1;
            }
            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            if (!TryParseFormId(refArg, out uint refId))
            {
                Console.WriteLine("Error: '" + refArg + "' is not a FormID. Expected 0F3287, 0x000F3287 or 0F3287:mod.esm");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;
            int changed = 0;

            // env holds the plugin open, so it is scoped to close before the write -- a
            // same-path WriteToBinary inside the using throws and leaves the old bytes
            // looking like a persisted no-op. Same reason as gen_setmass / gen_setcargo.
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

                var located = Find(myMod, refId);
                if (located == null)
                {
                    Console.WriteLine("Error: no PlacedObject " + refArg + " in " + modname
                                      + " -- check the id with 'gen_inspect Cell <cell>'");
                    return 1;
                }
                var target = located.Value.Ref;

                // ---- resolve the whole spec BEFORE mutating anything -------------------
                var wanted = new List<(FormKey Keyword, FormKey Reference, string KwLabel, string RefLabel)>();
                if (spec != null)
                {
                    foreach (var pair in spec.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var bits = pair.Split('=', 2);
                        if (bits.Length != 2)
                        {
                            Console.WriteLine("Error: '" + pair + "' is not <keyword>=<target>");
                            return 1;
                        }
                        string kwArg = bits[0].Trim(), tgtArg = bits[1].Trim();

                        // Keyword: EditorID first (that is what a human types), FormID second.
                        FormKey kwKey;
                        var kw = env.LoadOrder.PriorityOrder.Keyword().WinningOverrides()
                                    .FirstOrDefault(k => string.Equals(k.EditorID, kwArg, StringComparison.OrdinalIgnoreCase));
                        if (kw != null) kwKey = kw.FormKey;
                        else if (TryParseFormId(kwArg, out uint kwId))
                        {
                            var byId = env.LoadOrder.PriorityOrder.Keyword().WinningOverrides()
                                          .FirstOrDefault(k => (k.FormKey.ID & 0xFFFFFF) == (kwId & 0xFFFFFF));
                            if (byId == null)
                            {
                                Console.WriteLine("Error: no Keyword " + kwArg + " in the load order");
                                return 1;
                            }
                            kwKey = byId.FormKey;
                        }
                        else
                        {
                            Console.WriteLine("Error: no Keyword '" + kwArg + "' in the load order"
                                              + " (give an EditorID like LinkShipModule, or a FormID like 0x2C1001)");
                            return 1;
                        }

                        if (!TryParseFormId(tgtArg, out uint tgtId))
                        {
                            Console.WriteLine("Error: link target '" + tgtArg + "' is not a FormID");
                            return 1;
                        }
                        // ⛔ The target must EXIST. A link pointing at nothing renders
                        // identically to a correct one in every view and fails at runtime.
                        var tgt = Find(myMod, tgtId);
                        if (tgt == null)
                        {
                            Console.WriteLine("Error: link target " + tgtArg + " is not a PlacedObject in " + modname
                                              + " -- refusing to write a dangling link");
                            return 1;
                        }
                        wanted.Add((kwKey, tgt.Value.Ref.FormKey, kwArg, tgtArg));
                    }
                }

                // ---- everything resolved; now mutate ----------------------------------
                // LinkedReferences is init-only on PlacedObject and is never null -- an
                // empty list is the no-links state, so there is nothing to construct here.
                if (clear)
                {
                    int n = target.LinkedReferences.Count;
                    if (n == 0)
                    {
                        Console.WriteLine("  " + target.FormKey + ": no linked references -- nothing to clear");
                    }
                    else
                    {
                        target.LinkedReferences.Clear();
                        Console.WriteLine("  " + target.FormKey + ": cleared " + n + " linked reference(s)");
                        changed++;
                    }
                }
                else
                {
                    if (replace && target.LinkedReferences.Count > 0)
                    {
                        Console.WriteLine("  " + target.FormKey + ": --replace, dropping "
                                          + target.LinkedReferences.Count + " existing link(s)");
                        target.LinkedReferences.Clear();
                        changed++;
                    }

                    foreach (var w in wanted)
                    {
                        var existing = target.LinkedReferences.FirstOrDefault(
                            l => l.KeywordOrReference.FormKey == w.Keyword);
                        if (existing != null)
                        {
                            if (existing.Reference.FormKey == w.Reference)
                            {
                                Console.WriteLine("  " + target.FormKey + ": " + w.KwLabel + " already -> "
                                                  + w.RefLabel + " -- left as is");
                                continue;
                            }
                            Console.WriteLine("  " + target.FormKey + ": " + w.KwLabel + " "
                                              + existing.Reference.FormKey + " -> " + w.Reference);
                            existing.Reference.SetTo(w.Reference);
                        }
                        else
                        {
                            Console.WriteLine("  " + target.FormKey + ": + " + w.KwLabel + " -> " + w.Reference);
                            target.LinkedReferences.Add(new LinkedReferences()
                            {
                                KeywordOrReference = w.Keyword.ToLink<IKeywordLinkedReferenceGetter>(),
                                Reference = w.Reference.ToLink<IPlacedGetter>(),
                            });
                        }
                        changed++;
                    }
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
            Console.WriteLine("Finished -- " + changed + " link change(s), FormIDs unchanged.");
            return 0;
        }
    }
}
