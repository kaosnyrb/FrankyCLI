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
    // Move an EXISTING placed reference, FormID-stable.
    //
    //   moveref <modname> <cell> <base> (--to x,y,z | --by dx,dy,dz)
    //           [--rot rx,ry,rz | --rotby drx,dry,drz] [--scale s] [--ref 0xFORMID] [--dry]
    //
    // WHY THIS EXISTS, and it is the whole reason rather than a nicety. NOTHING in FrankyCLI
    // could move a placed ref. removerecord covers base forms (mstt/sntp/gbfm/cobj/flst/pkin)
    // and deliberately not REFR or CELL; setrefbase repoints what a ref points AT; placeref only
    // ADDS one. So repositioning a marker meant deleting and re-placing it, which mints a new
    // FormID for something a saved game may hold, or it meant a byte patch.
    //
    // ⛔ IT WAS A BYTE PATCH, TWICE, AND THAT IS THE ACTUAL DEFECT THIS CLOSES. On 2026-09-14 the
    // Kestrel gear's ShipLandEngMarkerTall01 was moved to its true contact patch by patching the
    // REFR's DATA position triple in place -- licensed properly (the patcher was proved to re-emit
    // its own input byte-identically first, 8 bytes changed, length unchanged, verified back
    // through Mutagen), but the patcher lived in a SCRATCHPAD. A method hand-rolled N times whose
    // only committed artifact is its OUTPUT is an unversioned instrument, and the cure is not
    // discipline, it is committing the thing with its control as a gate. It then blocked the
    // 2026-09-15 bay work twice more.
    //
    // ⭐ AND THE CASE IT IS FOR IS NOT A TIDY-UP. A ship part's floor marker IS the ride-height
    // dial and the contact point: ShipLandEngMarkerShort01 at Z -3.5001, Short02 at -4.2621,
    // Tall01 at -7.0000, and his correction on the Kestrel is that the marker belongs at the
    // FOOT, not at the origin -- vanilla puts it at 0,0 only because a vanilla gear is modelled
    // with its origin directly above its own foot. On a wing the foot is ten units outboard, so
    // the number that is right for vanilla is wrong for us. Moving a marker is a design act.
    //
    // ⚠ NOT setrotation, WHICH IS A DIFFERENT SUBJECT. setrotation writes the placed-object
    // rotation MAP of a flipped part's orientation variants, keyed by face, off a base MSTT
    // EditorID. This moves ONE placed ref in ONE cell. They read alike in a command list and
    // have nothing to do with each other; said here because the next person will wonder.
    //
    // MATCHING IS setrefbase's, ON PURPOSE. A ref is named as "the ref on <base> in <cell>",
    // never by its own FormID, because a REFR FormID is a number nobody can read off a design.
    // If the cell holds more than one ref on that base it REFUSES rather than guessing -- and
    // unlike setrefbase it then offers --ref to name one of the printed hits, because a refusal
    // with no way forward is where that command currently leaves you.
    //
    // ⚠ --ref IS AN EXPLICIT DISAMBIGUATOR, NOT A FALLBACK. It is only ever consulted among refs
    // that already matched the cell and the base, so a mistyped FormID cannot reach some unrelated
    // record: it fails the membership test and refuses. Same rule as placeref's cross-plugin
    // syntax, and for the same reason -- a wrong ref moved successfully is the failure mode the
    // duplicate guard one layer up exists to prevent.
    class gen_moveref
    {
        public static int Generate(string[] args)
        {
            var pos = new List<string>();
            string? toStr = null, byStr = null, rotStr = null, rotByStr = null, scaleStr = null, refStr = null;
            bool dry = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (i == 1) continue;                      // RunLegacy injects the mode by index
                var a = args[i];
                if (a == "--dry") { dry = true; }
                else if (a == "--to" && i + 1 < args.Length) { toStr = args[++i]; }
                else if (a == "--by" && i + 1 < args.Length) { byStr = args[++i]; }
                else if (a == "--rot" && i + 1 < args.Length) { rotStr = args[++i]; }
                else if (a == "--rotby" && i + 1 < args.Length) { rotByStr = args[++i]; }
                else if (a == "--scale" && i + 1 < args.Length) { scaleStr = args[++i]; }
                else if (a == "--ref" && i + 1 < args.Length) { refStr = args[++i]; }
                else pos.Add(a);
            }

            if (pos.Count < 3)
            {
                Console.WriteLine("Usage: moveref <modname> <cell> <base> (--to x,y,z | --by dx,dy,dz)");
                Console.WriteLine("               [--rot rx,ry,rz | --rotby drx,dry,drz] [--scale s]");
                Console.WriteLine("               [--ref 0xFORMID] [--dry]");
                Console.WriteLine("  <cell>    a Cell EditorID -- READ IT OFF THE PLUGIN (the CK renames on save)");
                Console.WriteLine("  <base>    what the ref points at: <plugin>:0xFORMID, 0xFORMID, or an EditorID");
                Console.WriteLine("  --to      absolute position; --by  offsets the current one. Exactly one.");
                Console.WriteLine("  --rot     absolute rotation; --rotby offsets it. Rotations are RADIANS,");
                Console.WriteLine("            which is what a REFR stores -- placeref's --rot is the same units.");
                Console.WriteLine("  --ref     name ONE of the refs when the cell holds several on that base");
                Console.WriteLine("            (it is checked AGAINST those hits, so a typo refuses).");
                return 1;
            }

            string modname = pos[0], cellName = pos[1], baseName = pos[2];
            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            // Exactly one of --to / --by. Neither is a no-op invocation that would report success
            // having done nothing; both is an ambiguity with two defensible readings, and picking
            // one silently is how a tool teaches somebody the wrong mental model.
            if (toStr == null && byStr == null)
            {
                Console.WriteLine("Error: give --to x,y,z or --by dx,dy,dz -- this command exists to move something.");
                return 1;
            }
            if (toStr != null && byStr != null)
            {
                Console.WriteLine("Error: --to and --by are exclusive (absolute vs relative). Give one.");
                return 1;
            }
            if (rotStr != null && rotByStr != null)
            {
                Console.WriteLine("Error: --rot and --rotby are exclusive (absolute vs relative). Give one.");
                return 1;
            }

            float tX = 0, tY = 0, tZ = 0;
            if (toStr != null && !TryTripleOrFail(toStr, "--to", out tX, out tY, out tZ)) return 1;
            float bX = 0, bY = 0, bZ = 0;
            if (byStr != null && !TryTripleOrFail(byStr, "--by", out bX, out bY, out bZ)) return 1;
            float rX = 0, rY = 0, rZ = 0;
            if (rotStr != null && !TryTripleOrFail(rotStr, "--rot", out rX, out rY, out rZ)) return 1;
            float rbX = 0, rbY = 0, rbZ = 0;
            if (rotByStr != null && !TryTripleOrFail(rotByStr, "--rotby", out rbX, out rbY, out rbZ)) return 1;

            float? newScale = null;
            if (scaleStr != null)
            {
                if (!float.TryParse(scaleStr, out var sc))
                {
                    Console.WriteLine("Error: --scale '" + scaleStr + "' is not a number");
                    return 1;
                }
                newScale = sc;
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

                var cell = gen_placeref.AllCells(myMod).FirstOrDefault(c =>
                    string.Equals(c.EditorID, cellName, StringComparison.OrdinalIgnoreCase));
                if (cell == null)
                {
                    Console.WriteLine("Error: no cell '" + cellName + "' in " + modname);
                    return 1;
                }

                var baseKey = gen_placeref.ResolveBase(baseName, modname, myMod, env, out var baseKind, out var okBase);
                if (!okBase) return 1;

                var hits = cell.Temporary.Concat(cell.Persistent)
                    .OfType<PlacedObject>()
                    .Where(p => p.Base.FormKey == baseKey!.Value)
                    .ToList();

                if (hits.Count == 0)
                {
                    Console.WriteLine("Error: " + cell.EditorID + " places no ref on '" + baseName + "' (" + baseKey!.Value + ")");
                    Console.WriteLine("  it holds " + (cell.Temporary.Count + cell.Persistent.Count) + " ref(s):");
                    foreach (var p in cell.Temporary.Concat(cell.Persistent).OfType<IPlacedObjectGetter>())
                        Console.WriteLine("    " + p.FormKey + "  base " + p.Base.FormKey);
                    return 1;
                }

                PlacedObject target;
                if (hits.Count == 1)
                {
                    target = hits[0];
                    if (refStr != null && !SameForm(target.FormKey, refStr))
                    {
                        Console.WriteLine("Error: --ref " + refStr + " does not name the one ref on '" + baseName
                                          + "' in " + cell.EditorID + " (that is " + target.FormKey + ").");
                        return 1;
                    }
                }
                else if (refStr == null)
                {
                    Console.WriteLine("Error: " + cell.EditorID + " places " + hits.Count + " refs on '" + baseName
                                      + "' -- refusing to guess which. Re-run with --ref <one of these>:");
                    foreach (var p in hits)
                        Console.WriteLine(string.Format("    {0}   at ({1:F4}, {2:F4}, {3:F4})",
                                                        p.FormKey, p.Position.X, p.Position.Y, p.Position.Z));
                    return 1;
                }
                else
                {
                    var picked = hits.Where(p => SameForm(p.FormKey, refStr)).ToList();
                    if (picked.Count != 1)
                    {
                        Console.WriteLine("Error: --ref " + refStr + " is not one of the " + hits.Count
                                          + " refs on '" + baseName + "' in " + cell.EditorID + ". They are:");
                        foreach (var p in hits) Console.WriteLine("    " + p.FormKey);
                        return 1;
                    }
                    target = picked[0];
                }

                var oldPos = target.Position;
                var oldRot = target.Rotation;
                var oldScale = target.Scale;

                var newPos = toStr != null
                    ? new P3Float(tX, tY, tZ)
                    : new P3Float(oldPos.X + bX, oldPos.Y + bY, oldPos.Z + bZ);
                var newRot = rotStr != null
                    ? new P3Float(rX, rY, rZ)
                    : rotByStr != null
                        ? new P3Float(oldRot.X + rbX, oldRot.Y + rbY, oldRot.Z + rbZ)
                        : oldRot;

                // A move that moves nothing is refused rather than reported as done. The reason is
                // not tidiness: this command's whole job is to change a number a human derived, so
                // "no change" means the derivation and the target disagree about which ref is being
                // named -- and a success line over an unchanged record is exactly the blank that
                // makes "it worked" and "it did nothing" the same output.
                bool posSame = Near(oldPos.X, newPos.X) && Near(oldPos.Y, newPos.Y) && Near(oldPos.Z, newPos.Z);
                bool rotSame = Near(oldRot.X, newRot.X) && Near(oldRot.Y, newRot.Y) && Near(oldRot.Z, newRot.Z);
                bool scaleSame = newScale == null || (oldScale.HasValue && Near(oldScale.Value, newScale.Value));
                if (posSame && rotSame && scaleSame)
                {
                    Console.WriteLine("Error: that is where the ref already is -- nothing to do.");
                    Console.WriteLine(string.Format("  {0} is at ({1:F6}, {2:F6}, {3:F6})",
                                                    target.FormKey, oldPos.X, oldPos.Y, oldPos.Z));
                    return 1;
                }

                Console.WriteLine("cell   : " + cell.EditorID + "   (" + cell.FormKey + ")");
                Console.WriteLine("base   : " + baseName + "  " + baseKind + "   (" + baseKey!.Value + ")");
                Console.WriteLine("ref    : " + target.FormKey
                                  + (hits.Count > 1 ? "   (" + hits.Count + " on this base, named by --ref)" : ""));
                Console.WriteLine(string.Format("pos    : ({0:F6}, {1:F6}, {2:F6})", oldPos.X, oldPos.Y, oldPos.Z));
                Console.WriteLine(string.Format("      -> ({0:F6}, {1:F6}, {2:F6})   delta ({3:+0.000000;-0.000000;0}, {4:+0.000000;-0.000000;0}, {5:+0.000000;-0.000000;0})",
                                                newPos.X, newPos.Y, newPos.Z,
                                                newPos.X - oldPos.X, newPos.Y - oldPos.Y, newPos.Z - oldPos.Z));
                if (!rotSame)
                {
                    Console.WriteLine(string.Format("rot    : ({0:F6}, {1:F6}, {2:F6})", oldRot.X, oldRot.Y, oldRot.Z));
                    Console.WriteLine(string.Format("      -> ({0:F6}, {1:F6}, {2:F6})", newRot.X, newRot.Y, newRot.Z));
                }
                if (newScale != null)
                    Console.WriteLine("scale  : " + (oldScale.HasValue ? oldScale.Value.ToString("F4") : "(null = 1.0)")
                                      + "  ->  " + newScale.Value.ToString("F4"));

                if (dry)
                {
                    Console.WriteLine("\n--dry: nothing written");
                    return 0;
                }

                target.Position = newPos;
                if (!rotSame) target.Rotation = newRot;
                if (newScale != null) target.Scale = newScale;
                Console.WriteLine("  moved REFR " + target.FormKey + " -- base, FormID and cell membership unchanged");
            }

            // The GameEnvironment holds the plugin open, so the write happens after the using block
            // closes: a same-path WriteToBinary inside it throws and leaves the old bytes looking
            // like a persisted no-op. (placeref's own head says the same; it is the same trap.)
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine("\nwrote " + datapath + "\\" + modname + ".esm");
            return 0;
        }

        // 1e-4 matches placeref's own duplicate-position test, so "already there" means the same
        // thing in both commands rather than two thresholds that can disagree.
        private static bool Near(float a, float b) => Math.Abs(a - b) < 1e-4;

        private static bool TryTripleOrFail(string s, string flag, out float a, out float b, out float c)
        {
            if (gen_placeref.TryTriple(s, out a, out b, out c)) return true;
            Console.WriteLine("Error: " + flag + " '" + s + "' is not three comma-separated numbers");
            return false;
        }

        // Accepts the shapes a person actually types for a ref: 0x0008AB, 0008AB, and the
        // FormKey spelling Mutagen prints (0008AB:avontechstardust.esm). Compared on the ID
        // itself rather than on the rendered string, so a leading-zero difference cannot make
        // two spellings of one form look like two forms.
        private static bool SameForm(FormKey key, string spelling)
        {
            var s = spelling.Trim();
            int colon = s.IndexOf(':');
            if (colon >= 0) s = s.Substring(0, colon);
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
            if (!uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var id)) return false;
            return (key.ID & 0xFFFFFF) == (id & 0xFFFFFF);
        }
    }
}
