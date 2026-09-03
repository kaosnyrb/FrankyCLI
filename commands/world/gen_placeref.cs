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
    // Place an EXISTING base form into an EXISTING cell.
    //
    //   placeref <modname> <cell> <base> <x,y,z> [--rot <rx,ry,rz>] [--edid <edid>] [--dry]
    //
    //   <cell>   a Cell EditorID. ⛔ READ IT OFF THE PLUGIN, DO NOT TYPE IT FROM THE GENERATOR
    //            LOG OR FROM MEMORY: the Creation Kit RENAMES a generated cell on save, so the
    //            same cell is `atsd_cell_wing04` before its first CK visit and
    //            `PackInatsdpknwing04StorageCell` after. 47 of this mod's 49 cells carry the
    //            second form and the two that do not are exactly the two parts that have never
    //            been through the editor. A name that changes under a save is not an address.
    //   <base>   a base EditorID in <modname>, or 0xFORMID for anything else in <modname>,
    //            or <plugin>:0xFORMID for a form in ANOTHER plugin -- e.g.
    //            Starfield.esm:0x187750 (ShipLandEngMarkerShort01).
    //
    // ⭐ THE CROSS-PLUGIN FORM EXISTS BECAUSE A LANDING GEAR NEEDS IT (2026-09-03). Vanilla's own
    // lander PackIns place a VANILLA Static -- ShipLandEngMarkerShort01, the floor marker every
    // gear has -- inside the part's cell, alongside the mod's own statics. Both lookups here
    // searched myMod only, so there was no way to author that at all; the part built clean and
    // was missing a piece of furniture every other gear in the game has. His catch, from the
    // builder, in one line.
    //
    // ⛔ IT IS AN EXPLICIT SYNTAX AND NOT A FALLBACK, DELIBERATELY. The obvious cheap version is
    // "if the id is not in myMod, try the load order" -- and that silently converts a TYPO in a
    // local FormID into a placement of whatever vanilla record happens to share those bytes.
    // A wrong base placed successfully is exactly the failure this tool's duplicate guard exists
    // to prevent, one layer up. Naming the plugin costs eleven characters and cannot misfire.
    //
    // ⚠ NO NEW MASTER IS ADDED BY DESIGN, AND IT IS NOT CHECKED HERE: every plugin this tool can
    // open already masters Starfield.esm, and a cross-plugin reference to anything else would
    // need a master the caller has not declared. If that ever comes up it wants a real check,
    // not this comment -- said plainly rather than left as an assumption nobody wrote down.
    //
    // WHY THIS EXISTS. A convex hull cannot have a hole in it, and it cannot have a CROOK either:
    // convexity forces the space between two limbs to be filled, so no single hull can wrap a
    // wing plate and its downturned tip fin and leave the weapon bay between them free. Measured
    // on atsd_wing04: the mount sits 1.245 INSIDE the whole-model hull and 2.448 OUTSIDE a
    // plate-only one. The answer is TWO hulls, and the second one has to be carried by a second
    // placed record in the part's own PackIn cell.
    //
    // ⭐ HIS TWO TEST RESULTS ARE THE SPEC AND THEY DISAGREE WITH EACH OTHER'S OBVIOUS READING:
    //   "1 doesnt work in the ship editor"     -- CollisionMarker primitives in the cell are
    //                                             IGNORED by the ship builder (that is placeprim,
    //                                             and it stays correct for INTERIORS only).
    //   "tested, 2 moveable statics works"     -- a second placed record carrying a real NIF hull
    //                                             IS honoured.
    // The builder distinguishes a PRIMITIVE from a PLACED STATIC WITH A HULL; the cell was never
    // the problem. A negative about one mechanism does not transfer to a different mechanism that
    // happens to live in the same place -- I predicted route 2 would fail for route 1's reason and
    // was wrong, and it cost him a test to find out.
    //
    // ⛔ AND HIS RULING ON THE RECORD TYPE, which is why this does not just call newstatic:
    // "has to be moveable static to be a ship part". newstatic authors a STATIC. Interior pieces
    // are Statics (HatchPlugFloor, SMOD_Plug_Fore_STATIC) and that is correct for an interior
    // cell; a ship module is assembled from MoveableStatics. Author the carrier with
    // `struct --mstt-only --no-snap` and place it with this.
    //
    // ⚠ THE CARRIER'S NIF MUST DRAW NOTHING, or you ship the part twice. injectcollision.py
    // replaces the shape inside bhkPhysicsSystem and touches nothing else, so injecting a hull
    // into a copy of the part's own NIF gives you a file that collides correctly and RENDERS A
    // SECOND COPY OF THE WHOLE PART. Build the carrier with
    // `nif_from_template.py --collision-only`, which emits the 4-block scaffold with no geometry
    // and no children.
    //
    // VALIDATE EVERYTHING, THEN MUTATE. The cell, the base and the position are all resolved
    // before a record is added, and an identical placement is REFUSED rather than duplicated --
    // a cell holding the same hull twice is not a visible error anywhere, and re-running a
    // command is how it would happen.
    class gen_placeref
    {
        private static IEnumerable<Cell> AllCells(StarfieldMod mod)
        {
            foreach (var block in mod.Cells)
                foreach (var sub in block.SubBlocks)
                    foreach (var cell in sub.Cells)
                        yield return cell;
        }

        // Lowercase, alphanumerics only, with the CK's own decorations removed. `packin` and
        // `storagecell` are ENGINE vocabulary rather than this mod's, so stripping them is safe
        // for any caller; nothing mod-specific is stripped, because a guessed prefix list is the
        // name-pattern trap one level down.
        private static string NormaliseCellName(string s)
        {
            var t = new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            return t.Replace("packin", "").Replace("storagecell", "");
        }

        private static int CommonSuffix(string a, string b)
        {
            int n = 0;
            while (n < a.Length && n < b.Length && a[a.Length - 1 - n] == b[b.Length - 1 - n]) n++;
            return n;
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
            // args: [modname, "placeref", cell, base, pos, ...flags] -- RunLegacy injects the mode
            // at index 1, skipped BY INDEX rather than by string-matching, because a cell or a base
            // could legitimately be called "placeref" (gen_placeprim says the same in its own head).
            var pos = new List<string>();
            string? rotStr = null, edid = null;
            bool dry = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (i == 1) continue;
                var a = args[i];
                if (a == "--dry") { dry = true; }
                else if (a == "--rot" && i + 1 < args.Length) { rotStr = args[++i]; }
                else if (a == "--edid" && i + 1 < args.Length) { edid = args[++i]; }
                else pos.Add(a);
            }

            if (pos.Count < 4)
            {
                Console.WriteLine("Usage: placeref <modname> <cell> <base> <x,y,z>");
                Console.WriteLine("                [--rot <rx,ry,rz>] [--edid <edid>] [--dry]");
                Console.WriteLine("  <cell>  a Cell EditorID -- READ IT OFF THE PLUGIN. The CK renames a");
                Console.WriteLine("          generated cell on save (atsd_cell_x -> PackIn<packin>StorageCell).");
                Console.WriteLine("  <base>  a base EditorID in <modname>, or 0xFORMID.");
                return 1;
            }

            string modname = pos[0], cellName = pos[1], baseName = pos[2];
            if (!TryTriple(pos[3], out var atX, out var atY, out var atZ))
            {
                Console.WriteLine("Error: position must be x,y,z");
                return 1;
            }
            float rX = 0, rY = 0, rZ = 0;
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

                // ---- resolve the cell -------------------------------------------------------
                var cell = AllCells(myMod).FirstOrDefault(c =>
                    string.Equals(c.EditorID, cellName, StringComparison.OrdinalIgnoreCase));
                if (cell == null)
                {
                    Console.WriteLine("Error: no cell '" + cellName + "' in " + modname);
                    // NAME THE NEAR MISS RATHER THAN JUST REFUSING. This exact refusal was read as
                    // a broken tool for a day, because the caller typed the CK-shaped name for a
                    // cell that has never been through the CK. Showing the candidates turns a
                    // dead end into an answer.
                    // MATCH ON THE COMMON SUFFIX, NOT ON Contains(). The two names of one cell are
                    // `atsd_cell_wing04` and `PackInatsdpknwing04StorageCell` -- NEITHER contains
                    // the other, so a Contains() hint stays silent on the exact case it exists for.
                    // (It did: I wrote Contains() first and bit it, and it printed nothing.) What
                    // the CK rename PRESERVES is the item token at the end, so the invariant to
                    // match on is the longest common suffix of the two, normalised.
                    var want = NormaliseCellName(cellName);
                    var near = AllCells(myMod)
                        .Where(c => c.EditorID != null)
                        .Select(c => new { Id = c.EditorID!, Shared = CommonSuffix(want, NormaliseCellName(c.EditorID!)) })
                        .Where(x => x.Shared >= 4)
                        .OrderByDescending(x => x.Shared)
                        .Take(5).ToList();
                    if (near.Count > 0)
                        Console.WriteLine("  did you mean: " + string.Join(", ", near.Select(x => x.Id))
                                          + "\n  (the CK RENAMES a generated cell on save -- the generator's own"
                                          + " name is the record's name until its first CK visit)");
                    else
                        Console.WriteLine("  no cell in " + modname + " shares a 4-character suffix with that name."
                                          + " There are " + AllCells(myMod).Count() + " cells; read the EditorID off"
                                          + " the plugin rather than typing it.");
                    return 1;
                }

                // ---- resolve the base -------------------------------------------------------
                // SCOPE STATED RATHER THAN IMPLIED: EditorID lookup covers this mod's own
                // MoveableStatics and Statics, which is what a collision carrier is. Anything
                // else -- a vanilla base, a marker, another mod's record -- goes in as 0xFORMID
                // and is not second-guessed. A wider silent search would resolve a name to a
                // record the caller never meant.
                // ⛔ CARRY THE FormKey, NOT A TYPED GETTER. A Starfield MoveableStatic does NOT
                // implement IPlaceableObjectGetter -- an `(IPlaceableObjectGetter)mstt` cast
                // COMPILES and throws InvalidCastException at run time. newstatic gets away with
                // `stat.ToLink<IPlaceableObjectGetter>()` because a LINK is a FormKey wrapper and
                // never type-checks the record. Found by biting this on its own success path,
                // which is the only reason it was found at all: both failure paths refuse before
                // they reach the cast, so a guards-only test would have passed over it.
                FormKey? baseKey = null;
                string baseKind = "";
                var colon = baseName.IndexOf(':');
                if (colon > 0 && baseName.Substring(colon + 1)
                        .StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    // <plugin>:0xFORMID -- a form in another plugin, named explicitly.
                    var plugin = baseName.Substring(0, colon);
                    var hex = baseName.Substring(colon + 3);
                    if (!uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                                       null, out var rawOther))
                    {
                        Console.WriteLine("Error: '" + baseName + "' -- '" + hex + "' is not a hex FormID");
                        return 1;
                    }
                    if (!ModKey.TryFromFileName(plugin, out var otherKey))
                    {
                        Console.WriteLine("Error: '" + plugin + "' is not a plugin filename"
                                          + " (want e.g. Starfield.esm:0x187750)");
                        return 1;
                    }
                    if (!env.LoadOrder.ModExists(otherKey))
                    {
                        Console.WriteLine("Error: plugin '" + plugin + "' is not in the load order");
                        return 1;
                    }
                    var otherMod = env.LoadOrder.PriorityOrder
                        .FirstOrDefault(m => m.ModKey == otherKey)?.Mod;
                    var okey = new FormKey(otherKey, rawOther & 0x00FFFFFF);
                    var found = otherMod?.EnumerateMajorRecords()
                        .FirstOrDefault(r => r.FormKey == okey);
                    if (found == null)
                    {
                        Console.WriteLine("Error: FormID 0x" + hex + " is not a record in " + plugin);
                        return 1;
                    }
                    baseKey = okey;
                    baseKind = found.GetType().Name.Replace("BinaryOverlay", "")
                               + " '" + (found.EditorID ?? "<no edid>") + "' in " + plugin
                               + " (cross-plugin -- placeability NOT checked)";
                }
                else if (baseName.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    if (!uint.TryParse(baseName.Substring(2), System.Globalization.NumberStyles.HexNumber,
                                       null, out var raw))
                    {
                        Console.WriteLine("Error: '" + baseName + "' is not a hex FormID");
                        return 1;
                    }
                    var key = new FormKey(myMod.ModKey, raw & 0x00FFFFFF);
                    var got = myMod.EnumerateMajorRecords().FirstOrDefault(r => r.FormKey == key);
                    if (got == null)
                    {
                        Console.WriteLine("Error: FormID " + baseName + " is not a record in " + modname);
                        return 1;
                    }
                    baseKey = key;
                    baseKind = got.GetType().Name + " (by FormID -- placeability NOT checked)";
                }
                else
                {
                    var mstt = myMod.MoveableStatics.FirstOrDefault(m =>
                        string.Equals(m.EditorID, baseName, StringComparison.OrdinalIgnoreCase));
                    if (mstt != null) { baseKey = mstt.FormKey; baseKind = "MoveableStatic"; }
                    else
                    {
                        var stat = myMod.Statics.FirstOrDefault(s =>
                            string.Equals(s.EditorID, baseName, StringComparison.OrdinalIgnoreCase));
                        if (stat != null) { baseKey = stat.FormKey; baseKind = "Static"; }
                    }
                    if (baseKey == null)
                    {
                        Console.WriteLine("Error: no MoveableStatic or Static '" + baseName + "' in " + modname
                                          + " -- pass 0xFORMID for a base outside those two types.");
                        return 1;
                    }
                }

                // ---- refuse a duplicate -----------------------------------------------------
                // Re-running a command is the ordinary way a cell ends up holding the same hull
                // twice, and NOTHING downstream would report it: two coincident hulls collide
                // exactly like one. Make it unconstructible rather than remembered.
                var already = cell.Temporary.Concat(cell.Persistent)
                    .OfType<IPlacedObjectGetter>()
                    .FirstOrDefault(p => p.Base.FormKey == baseKey.Value
                                         && Math.Abs(p.Position.X - atX) < 1e-4
                                         && Math.Abs(p.Position.Y - atY) < 1e-4
                                         && Math.Abs(p.Position.Z - atZ) < 1e-4);
                if (already != null)
                {
                    Console.WriteLine("Error: " + cell.EditorID + " already places " + baseName
                                      + " at that position (REFR " + already.FormKey + ") -- refusing a duplicate."
                                      + " Two coincident hulls collide exactly like one, so nothing downstream"
                                      + " would ever report it.");
                    return 1;
                }

                Console.WriteLine("cell   : " + cell.EditorID + "   (" + cell.FormKey + ")");
                Console.WriteLine("base   : " + baseName + "   " + baseKind + "   (" + baseKey.Value + ")");
                Console.WriteLine(string.Format("place  : ({0:F6}, {1:F6}, {2:F6})  rot ({3:F4}, {4:F4}, {5:F4})",
                                                atX, atY, atZ, rX, rY, rZ));
                Console.WriteLine("existing refs in this cell: " + (cell.Temporary.Count + cell.Persistent.Count));

                if (dry)
                {
                    Console.WriteLine("\n--dry: nothing written");
                    return 0;
                }

                var placed = new PlacedObject(myMod)
                {
                    Count = 1,
                    Position = new P3Float(atX, atY, atZ),
                    Rotation = new P3Float(rX, rY, rZ),
                    Base = baseKey.Value.ToLink<IPlaceableObjectGetter>(),
                };
                if (edid != null) placed.EditorID = edid;
                cell.Temporary.Add(placed);
                Console.WriteLine("  placed REFR " + placed.FormKey + " in " + cell.EditorID);
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
