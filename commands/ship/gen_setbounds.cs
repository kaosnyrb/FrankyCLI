using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Linq;

namespace FrankyCLI
{
    // Re-derive the ObjectBounds of a flipped part's orientation variants, in place.
    //
    //   setbounds <modname> <base_mstt_editorid>
    //
    // For every MoveableStatic named <base>* that is placed in a packin cell, recompute its
    // OBND as the axis-aligned envelope of the BASE part's bounds rotated by that variant's
    // placement rotation.
    //
    // WHY IT IS NEEDED: the flip generators clone the base MSTT's ObjectBounds verbatim, but
    // the variant is PLACED rotated -- so the record claims the unrotated box. The ship builder
    // consumes OBND raw (vanilla never rotates a placement, so it never needs to transform it),
    // and a box that lies about which side of the joint the part occupies reads as "module not
    // attached" even though the snap nodes line up. Found on atsd_fin01 (2026-07-23): the first
    // flip family whose rotations change the box -- every earlier family (the Shipyards dishes)
    // had a rotation-invariant OBND, so cloning was accidentally harmless.
    //
    // The bounds are DERIVED, not supplied: unlike the snap-node rotations (a human calls the
    // orientation after looking at the part), the true box is pure arithmetic from the base
    // bounds and the placement already in the plugin.
    class gen_setbounds
    {
        // Rotate a point by a stored rotation triple (radians), in the game's convention --
        // proven on the fin01 snap table (20/20, exhaustive sweep): a triple (x,y,z) applies
        // Y first, then X, then Z, each clockwise about its positive axis:
        //   M = Rz(-z) . Rx(-x) . Ry(-y)
        static double[,] RotationMatrix(P3Float rotRad)
        {
            static double[,] Mul(double[,] A, double[,] B)
            {
                var R = new double[3, 3];
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                        for (int k = 0; k < 3; k++)
                            R[i, j] += A[i, k] * B[k, j];
                return R;
            }
            double cx = Math.Cos(-rotRad.X), sx = Math.Sin(-rotRad.X);
            double cy = Math.Cos(-rotRad.Y), sy = Math.Sin(-rotRad.Y);
            double cz = Math.Cos(-rotRad.Z), sz = Math.Sin(-rotRad.Z);
            var RX = new double[3, 3] { { 1, 0, 0 }, { 0, cx, -sx }, { 0, sx, cx } };
            var RY = new double[3, 3] { { cy, 0, sy }, { 0, 1, 0 }, { -sy, 0, cy } };
            var RZ = new double[3, 3] { { cz, -sz, 0 }, { sz, cz, 0 }, { 0, 0, 1 } };
            return Mul(RZ, Mul(RX, RY));
        }

        // Axis-aligned envelope of the base bounds under the placement rotation. Exact for
        // 90-degree placements (a permutation/negation of the extents); for 45-degree families
        // the envelope grows, which is the correct claim for an axis-aligned box.
        public static ObjectBounds Derive(IObjectBoundsGetter baseBounds, P3Float placementRotRad)
        {
            var M = RotationMatrix(placementRotRad);
            var lo = new double[] { double.MaxValue, double.MaxValue, double.MaxValue };
            var hi = new double[] { double.MinValue, double.MinValue, double.MinValue };
            var f = baseBounds.First;
            var s = baseBounds.Second;
            foreach (var x in new[] { f.X, s.X })
                foreach (var y in new[] { f.Y, s.Y })
                    foreach (var z in new[] { f.Z, s.Z })
                    {
                        var p = new double[] { x, y, z };
                        for (int i = 0; i < 3; i++)
                        {
                            double v = M[i, 0] * p[0] + M[i, 1] * p[1] + M[i, 2] * p[2];
                            if (v < lo[i]) lo[i] = v;
                            if (v > hi[i]) hi[i] = v;
                        }
                    }
            static float R4(double v) => (float)Math.Round(v, 4);
            return new ObjectBounds
            {
                First = new P3Float(R4(lo[0]), R4(lo[1]), R4(lo[2])),
                Second = new P3Float(R4(hi[0]), R4(hi[1]), R4(hi[2])),
            };
        }

        static string Fmt(IObjectBoundsGetter b) =>
            $"({b.First.X:0.###},{b.First.Y:0.###},{b.First.Z:0.###})..({b.Second.X:0.###},{b.Second.Y:0.###},{b.Second.Z:0.###})";

        public static int Generate(string[] args)
        {
            // args: [modname, "setbounds", base_mstt_editorid]
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: setbounds <modname> <base_mstt_editorid>");
                Console.WriteLine("  re-derives every placed <base>* variant's OBND from the base bounds");
                Console.WriteLine("  and that variant's placement rotation");
                return 1;
            }
            string modname = args[0];
            string baseId = args[2];

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

                var baseMs = myMod.MoveableStatics.FirstOrDefault(
                    m => string.Equals(m.EditorID, baseId, StringComparison.OrdinalIgnoreCase));
                if (baseMs == null)
                {
                    Console.WriteLine($"Error: no MoveableStatic '{baseId}' in {modname}");
                    return 1;
                }
                if (baseMs.ObjectBounds == null)
                {
                    Console.WriteLine($"Error: base '{baseId}' has no ObjectBounds to derive from");
                    return 1;
                }
                Console.WriteLine($"  base      {baseMs.EditorID}: {Fmt(baseMs.ObjectBounds)}");

                foreach (var ms in myMod.MoveableStatics)
                {
                    if (ms.EditorID == null) continue;
                    if (!ms.EditorID.StartsWith(baseId, StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(ms.EditorID, baseId, StringComparison.OrdinalIgnoreCase)) continue;

                    PlacedObject? placed = null;
                    foreach (var cell in myMod.EnumerateMajorRecords<Cell>())
                    {
                        if (cell.Temporary == null) continue;
                        placed = cell.Temporary.OfType<PlacedObject>().FirstOrDefault(p => p.Base.FormKey == ms.FormKey);
                        if (placed != null) break;
                    }
                    if (placed == null)
                    {
                        // Loud skip: a prefix match with no placement is either a sibling part
                        // or a variant that lost its cell -- both are for a human to look at.
                        Console.WriteLine($"  SKIP      {ms.EditorID}: not placed in any cell");
                        continue;
                    }

                    var derived = Derive(baseMs.ObjectBounds, placed.Rotation);
                    bool same = ms.ObjectBounds != null
                        && ms.ObjectBounds.First.Equals(derived.First)
                        && ms.ObjectBounds.Second.Equals(derived.Second);
                    var deg = $"{placed.Rotation.X * 180 / Math.PI:0.#},{placed.Rotation.Y * 180 / Math.PI:0.#},{placed.Rotation.Z * 180 / Math.PI:0.#}";
                    if (same)
                    {
                        Console.WriteLine($"  ok        {ms.EditorID} (rot {deg} deg): already {Fmt(derived)}");
                        continue;
                    }
                    Console.WriteLine($"  DERIVE    {ms.EditorID} (rot {deg} deg):");
                    Console.WriteLine($"              was {(ms.ObjectBounds == null ? "null" : Fmt(ms.ObjectBounds))}");
                    Console.WriteLine($"              now {Fmt(derived)}");
                    ms.ObjectBounds = derived;
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
            Console.WriteLine($"Finished -- {changed} variant(s) re-bounded, FormIDs unchanged.");
            return 0;
        }
    }
}
