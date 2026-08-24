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
    // Author a STATIC record for a NIF, and optionally place it in a cell.
    //
    //   newstatic <modname> <editorid> <nifpath> <x0,y0,z0> <x1,y1,z1>
    //             [--cell <cell> --at <x,y,z>] [--rot <rx,ry,rz>]
    //             [--lightlayer N] [--dry]
    //
    //   <nifpath>       as the record stores it: Meshes\avontechstardust\thing.nif
    //   <x0..> <x1..>   ObjectBounds min and max, in GAME space
    //
    // WHY THIS EXISTS. gen_shipstruct authors the six-record ship-module chain, and every
    // record command beside it EDITS something that already exists. There was no way to
    // author a plain Static -- which is what interior geometry is. Vanilla's own hab interior
    // pieces (HatchPlugFloor, SMOD_Plug_Fore_STATIC) are Statics placed in the module's
    // interior cell, and this mod had none at all.
    //
    // ⭐ Model.LightLayer IS SET, AND THAT IS THE POINT OF DEFAULTING IT RATHER THAN LEAVING
    // IT OPTIONAL. A Model carrying no LightLayer builds, links and validates clean and then
    // RENDERS NOTHING; it hid on thirteen parts of this line behind a workflow that happened
    // to cure it, and gen_setlightlayer exists only because of that. Vanilla's HatchPlugFloor
    // carries 1, so 1 is the default here -- copied from the reference, not chosen.
    //
    // ⛔ OBJECTBOUNDS ARE REQUIRED, not optional, and they are NOT derived here. A wrong or
    // absent bound culls the part at the wrong distance, which looks like a rendering bug
    // anywhere except where the bound is. Union them off the .mesh -- it is authored in game
    // space, so it needs no axis convention, unlike a collision OBJ (that assumption was
    // false across this mod's own corpus and silently produced mirrored bounds once).
    //
    // ⚠ WHAT THIS DOES NOT AUTHOR, said plainly rather than left to be discovered:
    //   * NavmeshGeometry -- vanilla's interior statics carry it and it is CK-generated.
    //     Without it NPCs have no pathing over this piece. The player is unaffected.
    //   * SnapBehavior -- vanilla's hatch plugs carry one because they snap into a socket.
    //     A floor plate is not a snapping module, so none is set; if the piece ever needs to
    //     snap, that is a deliberate addition, not an omission to fix.
    //   * Keywords -- vanilla statics carry a keyword component; nothing here needs one yet.
    //
    // VALIDATE EVERYTHING, THEN MUTATE: the EditorID collision, the bounds, the cell and the
    // placement are all resolved before a record is created, because a plugin holding a
    // Static that nothing places is a silent no-op rather than an error.
    class gen_newstatic
    {
        private static IEnumerable<Cell> AllCells(StarfieldMod mod)
        {
            foreach (var block in mod.Cells)
                foreach (var sub in block.SubBlocks)
                    foreach (var cell in sub.Cells)
                        yield return cell;
        }

        private static bool TryTriple(string s, out float a, out float b, out float c)
        {
            a = b = c = 0;
            var p = s.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (p.Length != 3) return false;
            return float.TryParse(p[0], out a) && float.TryParse(p[1], out b) && float.TryParse(p[2], out c);
        }

        public static int Generate(string[] args)
        {
            // args: [modname, "newstatic", edid, nifpath, bounds0, bounds1, ...flags]
            var pos = new List<string>();
            string? cellName = null, atStr = null, rotStr = null;
            bool dry = false;
            uint lightLayer = 1;
            for (int i = 0; i < args.Length; i++)
            {
                if (i == 1) continue;                       // the mode token RunLegacy injects
                var a = args[i];
                if (a == "--dry") { dry = true; }
                else if (a == "--cell" && i + 1 < args.Length) { cellName = args[++i]; }
                else if (a == "--at" && i + 1 < args.Length) { atStr = args[++i]; }
                else if (a == "--rot" && i + 1 < args.Length) { rotStr = args[++i]; }
                else if (a == "--lightlayer" && i + 1 < args.Length) { lightLayer = uint.Parse(args[++i]); }
                else pos.Add(a);
            }

            if (pos.Count < 5)
            {
                Console.WriteLine("Usage: newstatic <modname> <editorid> <nifpath> <x0,y0,z0> <x1,y1,z1>");
                Console.WriteLine("                 [--cell <cell> --at <x,y,z>] [--rot <rx,ry,rz>]");
                Console.WriteLine("                 [--lightlayer N] [--dry]");
                Console.WriteLine("  bounds are ObjectBounds min/max in GAME space -- union them off the .mesh.");
                return 1;
            }

            string modname = pos[0], edid = pos[1], nifpath = pos[2];
            if (!TryTriple(pos[3], out var b0x, out var b0y, out var b0z)
                || !TryTriple(pos[4], out var b1x, out var b1y, out var b1z))
            {
                Console.WriteLine("Error: bounds must be x,y,z pairs");
                return 1;
            }
            if (b1x <= b0x || b1y <= b0y || b1z <= b0z)
            {
                Console.WriteLine("Error: bounds max is not greater than min on every axis -- "
                                  + "an inverted or zero bound culls the part everywhere.");
                return 1;
            }
            if ((cellName == null) != (atStr == null))
            {
                Console.WriteLine("Error: --cell and --at go together. A Static nothing places is a "
                                  + "silent no-op; a placement with no cell has nowhere to go.");
                return 1;
            }
            float atX = 0, atY = 0, atZ = 0, rX = 0, rY = 0, rZ = 0;
            if (atStr != null && !TryTriple(atStr, out atX, out atY, out atZ))
            {
                Console.WriteLine("Error: --at '" + atStr + "' is not x,y,z");
                return 1;
            }
            if (rotStr != null && !TryTriple(rotStr, out rX, out rY, out rZ))
            {
                Console.WriteLine("Error: --rot '" + rotStr + "' is not rx,ry,rz");
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

                if (myMod.Statics.Any(s => string.Equals(s.EditorID, edid, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine("Error: " + modname + " already has a Static '" + edid
                                      + "' -- refusing to author a second one");
                    return 1;
                }

                Cell? cell = null;
                if (cellName != null)
                {
                    cell = AllCells(myMod).FirstOrDefault(c =>
                        string.Equals(c.EditorID, cellName, StringComparison.OrdinalIgnoreCase));
                    if (cell == null)
                    {
                        Console.WriteLine("Error: no cell '" + cellName + "' in " + modname);
                        return 1;
                    }
                }

                Console.WriteLine("static : " + edid);
                Console.WriteLine("model  : " + nifpath + "   LightLayer " + lightLayer);
                Console.WriteLine(string.Format("bounds : ({0:F7}, {1:F7}, {2:F7}) .. ({3:F7}, {4:F7}, {5:F7})",
                                                b0x, b0y, b0z, b1x, b1y, b1z));
                if (cell != null)
                    Console.WriteLine(string.Format("place  : {0} at ({1:F6}, {2:F6}, {3:F6}) rot ({4:F4}, {5:F4}, {6:F4})",
                                                    cell.EditorID, atX, atY, atZ, rX, rY, rZ));
                else
                    Console.WriteLine("place  : (none -- record only)");

                if (dry)
                {
                    Console.WriteLine("\n--dry: nothing written");
                    return 0;
                }

                var stat = new Static(myMod)
                {
                    EditorID = edid,
                    ObjectBounds = new ObjectBounds()
                    {
                        First = new P3Float(b0x, b0y, b0z),
                        Second = new P3Float(b1x, b1y, b1z),
                    },
                    Model = new Model()
                    {
                        File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>(nifpath),
                        LightLayer = lightLayer,
                    },
                };
                myMod.Statics.Add(stat);
                Console.WriteLine("  created STAT " + stat.FormKey);

                if (cell != null)
                {
                    var placed = new PlacedObject(myMod)
                    {
                        Count = 1,
                        Position = new P3Float(atX, atY, atZ),
                        Rotation = new P3Float(rX, rY, rZ),
                        Base = stat.ToLink<IPlaceableObjectGetter>(),
                    };
                    cell.Temporary.Add(placed);
                    Console.WriteLine("  placed REFR " + placed.FormKey + " in " + cell.EditorID);
                }
            }

            // The GameEnvironment holds the plugin open, so the write happens after the using
            // block closes -- a same-path WriteToBinary inside it throws and leaves the old
            // bytes looking like a persisted no-op.
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine("\nwrote " + datapath + "\\" + modname + ".esm");
            return 0;
        }
    }
}
