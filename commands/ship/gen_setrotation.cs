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
    // Set the placed-object rotation of a flipped part's orientation variants, in place.
    //
    //   setrotation <modname> <base_mstt_editorid> "Top@0,0,0;Port@0,90,0;Bottom@0,180,0;Stbd@0,270,0"
    //
    // Rotations are DEGREES about game X,Y,Z, in the same Name@a,b,c grammar as setsnap.
    //
    // WHY IT IS NEEDED: gen_shipflips hardcodes one rotation map -- X for Top/Bottom, Z for
    // Port/Starboard/Aft, identity for Fore -- and that map is only correct for a part authored
    // FORE-FACING. atsd_fin01 is authored top-up (its joint sits at Z=0 and the blade rises in Z),
    // so its four orientations are all rotations about Y: Top 0, Port 90, Bottom 180, Stbd 270.
    // The generator's map is not wrong, it is relative to an authoring convention the part does
    // not follow, and there is no way to read a mesh and know which convention its author had in
    // mind -- so this is a value a human supplies after looking at the part in the builder.
    //
    // Surgical (his standing preference): rebuilding to change four numbers would destroy the
    // CK-repointed material-swap REFL payloads, which exist nowhere but the plugin.
    class gen_setrotation
    {
        static readonly Dictionary<string, directions> Alias = new(StringComparer.OrdinalIgnoreCase)
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

        public static int Generate(string[] args)
        {
            // args: [modname, "setrotation", base_mstt_editorid, spec]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setrotation <modname> <base_mstt_editorid> \"Dir@x,y,z[;Dir@x,y,z...]\"");
                Console.WriteLine("  rotations are DEGREES about game X,Y,Z");
                Console.WriteLine("  e.g. \"Top@0,0,0;Port@0,90,0;Bottom@0,180,0;Stbd@0,270,0\"");
                return 1;
            }
            string modname = args[0];
            string baseId = args[2];
            string spec = args[3];

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            var wanted = new List<(directions dir, P3Float rot)>();
            foreach (var entry in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var bits = entry.Split('@');
                if (bits.Length != 2)
                {
                    Console.WriteLine($"Error: bad spec '{entry}' -- want Dir@x,y,z");
                    return 1;
                }
                if (!Alias.TryGetValue(bits[0].Trim(), out var dir))
                {
                    Console.WriteLine($"Error: unknown direction '{bits[0].Trim()}'. Use: {string.Join(" ", Alias.Keys)}");
                    return 1;
                }
                var n = bits[1].Split(',');
                if (n.Length != 3
                    || !float.TryParse(n[0], out var rx)
                    || !float.TryParse(n[1], out var ry)
                    || !float.TryParse(n[2], out var rz))
                {
                    Console.WriteLine($"Error: bad rotation in '{entry}' -- want three numbers (degrees)");
                    return 1;
                }
                const float D2R = (float)(Math.PI / 180.0);
                wanted.Add((dir, new P3Float(rx * D2R, ry * D2R, rz * D2R)));
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

                // Changing a placement rotation invalidates the variant's ObjectBounds (OBND
                // must describe the part AS PLACED -- the builder reads it raw), so re-derive
                // them from the base part's box alongside every rotation write.
                var baseMs = myMod.MoveableStatics.FirstOrDefault(
                    m => string.Equals(m.EditorID, baseId, StringComparison.OrdinalIgnoreCase));
                if (baseMs?.ObjectBounds == null)
                    Console.WriteLine($"  WARNING: base '{baseId}' not found or has no bounds -- rotations set, bounds NOT re-derived (run setbounds after fixing)");

                foreach (var (dir, rot) in wanted)
                {
                    var variantId = baseId + dir.ToString();
                    var ms = myMod.MoveableStatics.FirstOrDefault(
                        m => string.Equals(m.EditorID, variantId, StringComparison.OrdinalIgnoreCase));
                    if (ms == null)
                    {
                        // Loud: a direction whose variant does not exist is a typo or a wrong set,
                        // and doing three of four silently is how a part ships half-right.
                        Console.WriteLine($"Error: no MoveableStatic '{variantId}' in {modname}");
                        return 1;
                    }

                    int hits = 0;
                    foreach (var cell in myMod.EnumerateMajorRecords<Cell>())
                    {
                        if (cell.Temporary == null) continue;
                        foreach (var placed in cell.Temporary.OfType<PlacedObject>())
                        {
                            if (placed.Base.FormKey != ms.FormKey) continue;
                            placed.Rotation = rot;
                            hits++;
                        }
                    }
                    if (hits == 0)
                    {
                        Console.WriteLine($"Error: {variantId} is not placed in any cell -- nothing to rotate");
                        return 1;
                    }
                    var deg = $"{rot.X * 180 / Math.PI:0.#},{rot.Y * 180 / Math.PI:0.#},{rot.Z * 180 / Math.PI:0.#}";
                    Console.WriteLine($"  {dir.ToString().Replace("ShipModPosition", ""),-9} {variantId}: rotation -> ({deg}) deg  [{hits} placement(s)]");
                    if (baseMs?.ObjectBounds != null)
                    {
                        ms.ObjectBounds = gen_setbounds.Derive(baseMs.ObjectBounds, rot);
                        Console.WriteLine($"            bounds  -> ({ms.ObjectBounds.First.X:0.###},{ms.ObjectBounds.First.Y:0.###},{ms.ObjectBounds.First.Z:0.###})..({ms.ObjectBounds.Second.X:0.###},{ms.ObjectBounds.Second.Y:0.###},{ms.ObjectBounds.Second.Z:0.###})");
                    }
                    changed += hits;
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
            Console.WriteLine($"Finished -- {changed} placement(s) rotated, FormIDs unchanged.");
            return 0;
        }
    }
}
