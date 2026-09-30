using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FrankyCLI
{
    // Give each ship part its OWN shipbuilder-icon transform (TRNS) and point the part's PackIn at it
    // (PTT2 slot "Ship"). The icon itself is rendered by the Creation Kit:
    //   CreationKit.exe -GenerateShipBuilderIcons:<plugin>.esm
    // and framed ENTIRELY by that transform. Generated parts inherit vanilla's one shared
    // ShipTF_SMOD_Struct_1x1x1 from their templates, which crops anything big and shrinks anything
    // small -- 129 of Stardust's 133 icons on 2026-09-30.
    //
    //   seticontransform <modname> <plan.json> [--dry]
    //
    // plan.json is {"<packin editorid>": [x, y, z], ...} -- a POSITION per part and nothing else.
    // The positions are SOLVED, not authored: scripts/icons/icon_solve.py renders, measures each icon
    // and solves the position that centres it at a target size, then calls this, and repeats. The
    // camera model it solves against is in the [Bethesda] manual, part 23.
    //
    // Everything but the position is CLONED from vanilla ShipTF_SMOD_Struct_1x1x1 through the link
    // cache (rotation, scale, zoom, BNAM, ENAM), so the view angle matches vanilla's icons and no
    // rotation unit (radians or degrees) is ever typed by hand.
    //
    // One TRNS per part is deliberate. The CK docs prefer shared transforms, but the camera aims at
    // the part's ORIGIN, not its bounds, so a part whose origin is off its middle (every flip member
    // does it differently) needs its own offset to be centred. The TRNS EditorID derives from the
    // PackIn's (_pkn_ -> _trns_), so a re-run UPDATES the same record: FormIDs never move and nothing
    // accumulates. Same shape as setvalue: many parts per call, all or nothing, idempotent.
    class gen_seticontransform
    {
        static readonly FormKey Template = FormKey.Factory("050FAC:Starfield.esm");

        static string TrnsEdid(string packin) =>
            packin.Contains("_pkn_") ? packin.Replace("_pkn_", "_trns_") : packin + "_trns";

        public static int Generate(string[] args)
        {
            // args: [modname, "seticontransform", plan.json, (--dry)]
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: seticontransform <modname> <plan.json> [--dry]");
                return 1;
            }
            string modname = args[0];
            bool dry = args.Skip(3).Contains("--dry");
            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            Dictionary<string, double[]> plan;
            try
            {
                plan = JsonSerializer.Deserialize<Dictionary<string, double[]>>(System.IO.File.ReadAllText(args[2]))
                       ?? throw new JsonException("empty plan");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: cannot read plan '{args[2]}': {e.Message} -- nothing written");
                return 1;
            }
            var bad = plan.Where(kv => kv.Value is null || kv.Value.Length != 3 || kv.Value.Any(v => !double.IsFinite(v)))
                          .Select(kv => kv.Key).ToList();
            if (bad.Count > 0)
            {
                Console.WriteLine($"Error: not [x, y, z]: {string.Join(", ", bad)} -- nothing written");
                return 1;
            }
            var dupes = plan.Keys.GroupBy(k => k, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Count > 0)
            {
                Console.WriteLine($"Error: named twice (case-insensitively): {string.Join(", ", dupes)} -- nothing written");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;
            int created = 0, moved = 0, relinked = 0, unchanged = 0;
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                datapath = env.DataFolderPath;
                if (!env.LoadOrder.ModExists(new ModKey(modname, ModType.Master)))
                {
                    Console.WriteLine($"Error: {modname}.esm is not in the load order");
                    return 1;
                }
                if (!env.LinkCache.TryResolve<ITransformGetter>(Template, out var tpl))
                {
                    Console.WriteLine($"Error: vanilla template transform {Template} not found -- nothing written");
                    return 1;
                }
                Console.WriteLine($"  template {tpl.EditorID}: rotation {tpl.Rotation}, scale {tpl.Scale}, zoom {tpl.ZoomMin}..{tpl.ZoomMax}");

                ModPath modPath = System.IO.Path.Combine(datapath, modname + ".esm");
                myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);

                var missing = plan.Keys.Where(k => !myMod.PackIns.Any(
                    p => string.Equals(p.EditorID, k, StringComparison.OrdinalIgnoreCase))).ToList();
                if (missing.Count > 0)
                {
                    Console.WriteLine($"Error: no PackIn in {modname}: {string.Join(", ", missing)} -- nothing written");
                    return 1;
                }

                foreach (var (edid, p) in plan.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                {
                    var pk = myMod.PackIns.First(x => string.Equals(x.EditorID, edid, StringComparison.OrdinalIgnoreCase));
                    var pos = new P3Float((float)p[0], (float)p[1], (float)p[2]);
                    string tEdid = TrnsEdid(pk.EditorID!);
                    var trns = myMod.Transforms.FirstOrDefault(t => string.Equals(t.EditorID, tEdid, StringComparison.OrdinalIgnoreCase));
                    string what;
                    if (trns is null)
                    {
                        trns = new Transform(myMod)
                        {
                            EditorID = tEdid,
                            Position = pos,
                            Rotation = tpl.Rotation,
                            Scale = tpl.Scale,
                            ZoomMin = tpl.ZoomMin,
                            ZoomMax = tpl.ZoomMax,
                        };
                        if (tpl.BNAM is { } bn) trns.BNAM = bn.ToArray();
                        if (tpl.ENAM is { } en) trns.ENAM = en.ToArray();
                        myMod.Transforms.Add(trns);
                        created++;
                        what = "created";
                    }
                    else if (!Same(trns.Position, pos))
                    {
                        what = $"moved from {trns.Position}";
                        trns.Position = pos;
                        moved++;
                    }
                    else what = "position unchanged";

                    pk.Transforms ??= new Transforms();
                    if (pk.Transforms.Ship.FormKey != trns.FormKey)
                    {
                        what += $", Ship slot {pk.Transforms.Ship.FormKey} -> {trns.FormKey}";
                        pk.Transforms.Ship.SetTo(trns.FormKey);
                        relinked++;
                    }
                    else if (what == "position unchanged") unchanged++;
                    Console.WriteLine($"  {edid}: {tEdid} {pos} ({what})");
                }
            }

            Console.WriteLine($"  {created} created, {moved} moved, {relinked} relinked, {unchanged} already in place");
            if (dry)
            {
                Console.WriteLine("--dry: nothing written.");
                return 0;
            }
            if (created + moved + relinked == 0)
            {
                Console.WriteLine("Nothing to write.");
                return 0;
            }
            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine("Finished -- written. Re-run -GenerateShipBuilderIcons to see the effect.");
            return 0;
        }

        // Positions are solved to 4 decimals; a float round trip must not read as a change.
        static bool Same(P3Float a, P3Float b) =>
            Math.Abs(a.X - b.X) < 1e-4f && Math.Abs(a.Y - b.Y) < 1e-4f && Math.Abs(a.Z - b.Z) < 1e-4f;
    }
}
