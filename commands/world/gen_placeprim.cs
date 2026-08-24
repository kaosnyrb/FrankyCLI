using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace FrankyCLI
{
    // Place PRIMITIVE volumes (CollisionMarker boxes and friends) into a cell.
    //
    //   placeprim <modname> <cell> <base> <x,y,z> <hx,hy,hz> [more <x,y,z> <hx,hy,hz> ...]
    //     --dry            resolve and report, write nothing
    //     --full-bounds    put the FULL extent in Mutagen's Bounds field (default; see below)
    //     --half-bounds    put the HALF extent in it instead
    //
    //   <cell>   a Cell EditorID (PackInatsdhab01intStorageCell)
    //   <base>   a placeable base EditorID (CollisionMarker) or 0xFORMID
    //   <hx..>   HALF-extents, because that is what the RECORD stores -- see below
    //
    // WHY THIS EXISTS. A convex hull cannot have a hole in it. injectcollision.py hulls the
    // vertex cloud with face data ignored, so an interior floor with a hatch through it comes
    // out a solid slab -- 1,230 verts in, an 8-vertex box out -- and prints success. Vanilla
    // solves it with hknpCompoundShape + hknpCompressedMeshShape, which that tool refuses by
    // design and cannot author. His answer, and it is the cheap one: put the collision in the
    // CELL as invisible boxes, which he already shipped for the Avontech Raceyard cockpit
    // (PackInatrpknCockpitSparrowintStorageCell carries eight of them).
    //
    // ⭐ BOUNDS ARE HALF-EXTENTS ON DISK, AND THIS COMMAND'S ARGUMENTS MATCH THE DISK.
    // Proved by cross-reading ONE record two ways: REFR 0100087F in avontechraceyard.esm
    // stores (0.5, 0.1035, 1.5) in its XPRM bytes and xEdit displays it as (1, 0.207, 3) --
    // exactly 2x on all three axes, same record, position matching to six decimals. Confirmed
    // a second way: the ShipModuleTrigger room volume reads (3.4922, 3.4922, 1.7412) in three
    // cells across two mods, which as half-extents is 6.98 x 6.98 x 3.48 -- just inside the
    // 8 x 8 x 3.535 module -- and as full extents would be a quarter of the room it fills.
    //
    // ⛔ WHAT IS *NOT* PROVEN IS WHICH OF THOSE MUTAGEN'S `Bounds` PROPERTY IS, and that is
    // why the flag exists rather than a constant. Both existing call sites in this repo put a
    // FULL extent in it -- gen_aspcpatch computes halfX then writes `halfX * 2f`, and
    // EnemyAlertPrimitiveCoveragePass passes `Max - Min` -- so the default follows them. But
    // two call sites by one author is not a proof, and if `Bounds` is in fact the raw
    // half-extent field then every generated volume in du_retrograde is twice the size it
    // should be. VERIFY THE FIRST WRITE BY PARSING THE XPRM BYTES OUT OF THE .esm, with
    // something other than Mutagen, and flip the flag if the on-disk value is not what was
    // asked for. Getting this wrong on a floor fills the hatch and looks perfectly correct in
    // every editor.
    //
    // VALIDATE EVERYTHING, THEN MUTATE. The cell, the base and every box are resolved and
    // parsed before a single record is added -- a lookup that fails halfway leaves a cell
    // holding some of an intended set, and a partial collision floor is exactly the
    // silently-wrong artifact this is meant to prevent rather than relocate.
    class gen_placeprim
    {
        private static IEnumerable<Cell> AllCells(StarfieldMod mod)
        {
            foreach (var block in mod.Cells)
                foreach (var sub in block.SubBlocks)
                    foreach (var cell in sub.Cells)
                        yield return cell;
        }

        private static bool TryParseTriple(string s, out float a, out float b, out float c)
        {
            a = b = c = 0;
            var p = s.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length != 3) return false;
            return float.TryParse(p[0], out a) && float.TryParse(p[1], out b) && float.TryParse(p[2], out c);
        }

        public static int Generate(string[] args)
        {
            // args: [modname, "placeprim", cell, base, ...] -- RunLegacy injects the mode at
            // index 1, which is the convention every sibling here reads (gen_setlinks says so
            // in its own first comment). Skipping it by INDEX rather than by string-matching
            // the mode name, because a cell or base legitimately could be called "placeprim".
            var positional = new List<string>();
            bool dry = false, halfBounds = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (i == 1) continue;
                var a = args[i];
                if (a == "--dry") dry = true;
                else if (a == "--half-bounds") halfBounds = true;
                else if (a == "--full-bounds") halfBounds = false;
                else positional.Add(a);
            }

            // A complete call is: modname cell base + at least one (pos, half) pair = 5.
            if (positional.Count < 5 || (positional.Count - 3) % 2 != 0)
            {
                Console.WriteLine("Usage: placeprim <modname> <cell> <base> <x,y,z> <hx,hy,hz> [<x,y,z> <hx,hy,hz> ...]");
                Console.WriteLine("                 [--dry] [--full-bounds|--half-bounds]");
                Console.WriteLine("  <hx,hy,hz> are HALF-extents -- what the XPRM record stores.");
                return 1;
            }

            string modname = positional[0];
            string cellName = positional[1];
            string baseName = positional[2];

            // ---- parse every box BEFORE touching the plugin -------------------------
            var boxes = new List<(P3Float Pos, P3Float Half)>();
            for (int i = 3; i + 1 < positional.Count; i += 2)
            {
                if (!TryParseTriple(positional[i], out var px, out var py, out var pz))
                {
                    Console.WriteLine("Error: position '" + positional[i] + "' is not x,y,z");
                    return 1;
                }
                if (!TryParseTriple(positional[i + 1], out var hx, out var hy, out var hz))
                {
                    Console.WriteLine("Error: half-extent '" + positional[i + 1] + "' is not hx,hy,hz");
                    return 1;
                }
                if (hx <= 0 || hy <= 0 || hz <= 0)
                {
                    Console.WriteLine("Error: half-extent '" + positional[i + 1] + "' has a non-positive axis. "
                                      + "A zero-thickness collision box collides with nothing and looks fine.");
                    return 1;
                }
                boxes.Add((new P3Float(px, py, pz), new P3Float(hx, hy, hz)));
            }

            StarfieldMod myMod;
            string datapath;
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                datapath = env.DataFolderPath;
                var cache = env.LoadOrder.ToImmutableLinkCache();

                ModKey modKey = new ModKey(modname, ModType.Master);
                if (!env.LoadOrder.ModExists(modKey))
                {
                    Console.WriteLine("Error: " + modname + ".esm is not in the load order");
                    return 1;
                }
                ModPath modPath = System.IO.Path.Combine(datapath, modname + ".esm");
                myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);

                // ---- resolve the cell ------------------------------------------------
                Cell? cell = AllCells(myMod).FirstOrDefault(c =>
                    string.Equals(c.EditorID, cellName, StringComparison.OrdinalIgnoreCase));
                if (cell == null)
                {
                    Console.WriteLine("Error: no cell '" + cellName + "' in " + modname
                                      + " -- check it with 'gen_inspect Cell <name>'");
                    return 1;
                }

                // ---- resolve the base ------------------------------------------------
                FormKey baseKey;
                string baseLabel;
                if (baseName.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    && uint.TryParse(baseName.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var rawId))
                {
                    var hit = cache.PriorityOrder.WinningOverrides<IStarfieldMajorRecordGetter>()
                                   .FirstOrDefault(r => r.FormKey.ID == rawId);
                    if (hit == null)
                    {
                        Console.WriteLine("Error: no record with FormID " + baseName);
                        return 1;
                    }
                    baseKey = hit.FormKey;
                    baseLabel = hit.EditorID ?? baseName;
                }
                else
                {
#pragma warning disable CS0618
                    var hit = cache.Resolve(baseName);
#pragma warning restore CS0618
                    if (hit == null)
                    {
                        Console.WriteLine("Error: no record with EditorID '" + baseName + "'");
                        return 1;
                    }
                    baseKey = hit.FormKey;
                    baseLabel = hit.EditorID ?? baseName;
                }

                Console.WriteLine("cell  : " + cell.EditorID + " [" + cell.FormKey + "]"
                                  + "  (persistent " + cell.Persistent.Count + ", temporary " + cell.Temporary.Count + ")");
                Console.WriteLine("base  : " + baseLabel + " [" + baseKey + "]");
                Console.WriteLine("bounds: writing the " + (halfBounds ? "HALF" : "FULL")
                                  + " extent into Mutagen's Bounds field"
                                  + (halfBounds ? "" : "  (both existing call sites in this repo do this)"));
                Console.WriteLine();

                foreach (var (pos, half) in boxes)
                {
                    Console.WriteLine(string.Format(
                        "  box  pos ({0,10:F6},{1,10:F6},{2,10:F6})  half ({3,9:F6},{4,9:F6},{5,9:F6})"
                        + "   spans X {6:F4}..{7:F4}  Y {8:F4}..{9:F4}  Z {10:F4}..{11:F4}",
                        pos.X, pos.Y, pos.Z, half.X, half.Y, half.Z,
                        pos.X - half.X, pos.X + half.X,
                        pos.Y - half.Y, pos.Y + half.Y,
                        pos.Z - half.Z, pos.Z + half.Z));
                }

                if (dry)
                {
                    Console.WriteLine("\n--dry: nothing written");
                    return 0;
                }

                foreach (var (pos, half) in boxes)
                {
                    var b = halfBounds
                        ? new P3Float(half.X, half.Y, half.Z)
                        : new P3Float(half.X * 2f, half.Y * 2f, half.Z * 2f);
                    var placed = new PlacedObject(myMod)
                    {
                        Count = 1,
                        Position = pos,
                        Rotation = new P3Float(0, 0, 0),
                        Base = baseKey.ToLink<IPlaceableObjectGetter>(),
                        Primitive = new PlacedPrimitive()
                        {
                            Bounds = b,
                            // Yellow, matched to every CollisionMarker in his shipped raceyard
                            // cockpit rather than chosen. The RGB lands correctly (1,1,0).
                            //
                            // ⛔ MEASURED LIMIT, not a guess: XPRM's 4th float -- xEdit labels it
                            // "Unknown" and it reads 0.300000 on every vanilla and shipped marker
                            // -- is NOT writable through Mutagen's PlacedPrimitive. Tried
                            // FromArgb(77,255,255,0) specifically to see whether Color.A maps
                            // onto it; the bytes still came back 0.00. So markers written by this
                            // command differ from his hand-authored ones in exactly that one
                            // field. Whether it matters is UNKNOWN -- it is most likely CK
                            // wireframe opacity, but that is not established, and it is settable
                            // by hand in xEdit if it turns out to.
                            Color = Color.FromArgb(255, 255, 0),
                            Type = PlacedPrimitive.TypeEnum.Box,
                        },
                    };
                    cell.Temporary.Add(placed);
                    Console.WriteLine("  added REFR " + placed.FormKey);
                }
            }

            // The GameEnvironment holds the plugin open, so the write has to happen after the
            // using block closes -- a same-path WriteToBinary inside it throws and leaves the
            // old bytes looking like a persisted no-op.
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine("\nwrote " + datapath + "\\" + modname + ".esm");
            Console.WriteLine("!! VERIFY THE XPRM BYTES with something that is not Mutagen before trusting the");
            Console.WriteLine("!! sizes -- whether Bounds is the half or the full extent is not proven here.");
            return 0;
        }
    }
}
