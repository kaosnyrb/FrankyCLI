using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace FrankyCLI
{
    // Write projected-decal placements into a part's PackIn cell.
    //
    // A decal is a Static placed as an ordinary PlacedObject -- structurally the same as the
    // engine flare -- but it carries two extra subrecords that the ordinary placements do not:
    //
    //   XCDD  3 floats -- the projection box (w, h, depth). 4x4x0.75 dominates his shipped work.
    //   XPDD  u, v, ALPHA + a flag pair. Alpha median 0.400 across avontechraceyard.esm; never 1.
    //
    // Those two are the whole point: the low alpha is what makes a decal read as a soft wash
    // rather than a sticker, and gen_inspect's cell printer shows neither, so a placement copied
    // from its summary would come out opaque and wrong.
    //
    //   placedecals <modname> reflect                      -- what can Mutagen actually author?
    //   placedecals <modname> <part> <placements.json> [--clear] [--dry]
    //
    // Same env-close-before-write discipline as gen_setrecipefilter: the GameEnvironment holds
    // the plugin open, so a same-path WriteToBinary inside the using throws and silently leaves
    // the old bytes looking like a persisted no-op.
    class gen_placedecals
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "placedecals", part|reflect, json?, flags...]
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: placedecals <modname> <part> <placements.json> [--clear] [--dry]");
                Console.WriteLine("       placedecals <modname> reflect");
                return 1;
            }
            string modname = args[0];
            string part = args[2];

            if (part.Equals("reflect", StringComparison.OrdinalIgnoreCase))
                return Reflect();

            if (args.Length < 4)
            {
                Console.WriteLine("Usage: placedecals <modname> <part> <placements.json> [--clear] [--dry]");
                return 1;
            }
            string jsonPath = args[3];
            bool clear = args.Any(a => a.Equals("--clear", StringComparison.OrdinalIgnoreCase));
            bool dry = args.Any(a => a.Equals("--dry", StringComparison.OrdinalIgnoreCase));

            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"Error: no placement file at {jsonPath}");
                return 1;
            }
            var placements = JsonSerializer.Deserialize<List<Placement>>(
                File.ReadAllText(jsonPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            // An empty file is a mistake on a normal run -- but with --clear it is a
            // legitimate operation: strip this part's decals and add nothing back.
            // Clearing surgically rather than restoring the .bak, because the backup
            // predates any CK edits and would silently throw away work I cannot see.
            if (placements == null || placements.Count == 0)
            {
                if (!clear)
                {
                    Console.WriteLine("Error: placement file is empty -- refusing to run. "
                                      + "(Pass --clear if you meant to remove decals and add none.)");
                    return 1;
                }
                placements = new List<Placement>();
                Console.WriteLine("empty placement list + --clear: removing decals, adding none");
            }

            Console.WriteLine($"{placements.Count} placement(s) from {jsonPath}");
            foreach (var p in placements.Take(3))
                Console.WriteLine($"   base {p.Base}  pos ({p.Pos[0]:F3},{p.Pos[1]:F3},{p.Pos[2]:F3})" +
                                  $"  rot ({p.Rot[0]:F3},{p.Rot[1]:F3},{p.Rot[2]:F3})  alpha {p.Xpdd.Alpha:F3}");
            if (placements.Count > 3) Console.WriteLine($"   ... and {placements.Count - 3} more");

            if (dry)
            {
                Console.WriteLine("\n--dry: nothing written.");
                return 0;
            }

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;
            int removed = 0, added = 0;

            // env holds the plugin file OPEN for the life of the using block; WriteToBinary to that
            // same path while env is alive throws and silently leaves the file unchanged. So env is
            // scoped to close BEFORE the write -- the gen_setrecipefilter discipline.
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                datapath = env.DataFolderPath;
                ModKey modKey = new ModKey(modname, ModType.Master);
                if (!env.LoadOrder.ModExists(modKey))
                {
                    Console.WriteLine($"Error: {modname}.esm is not in the load order");
                    return 1;
                }
                ModPath modPath = Path.Combine(datapath, modname + ".esm");
                myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);

                // The cell carries the CK's OWN name, not the generator's `_cell_<item>` -- it is
                // "PackIn" + the PackIn's EditorID with underscores stripped + "StorageCell"
                // (PackInatsdpkneng01StorageCell). So DERIVE it from the PackIn rather than guess a
                // convention; the first version of this assumed `_cell_` and found nothing.
                var pkn = myMod.PackIns.FirstOrDefault(
                    p => p.EditorID != null &&
                         p.EditorID.EndsWith("_" + part, StringComparison.OrdinalIgnoreCase));
                if (pkn == null)
                {
                    Console.WriteLine($"Error: no PackIn ending '_{part}' in {modname}. Candidates:");
                    foreach (var p in myMod.PackIns.Where(p => p.EditorID != null))
                        Console.WriteLine($"   {p.EditorID}");
                    return 1;
                }
                string wantCell = "PackIn" + pkn.EditorID!.Replace("_", "") + "StorageCell";

                Cell? target = null;
                foreach (var block in myMod.Cells)
                    foreach (var sub in block.SubBlocks)
                        foreach (var c in sub.Cells)
                            if (string.Equals(c.EditorID, wantCell, StringComparison.OrdinalIgnoreCase))
                                target = c;
                if (target == null)
                {
                    Console.WriteLine($"Error: PackIn '{pkn.EditorID}' found, but no cell '{wantCell}' in {modname}");
                    return 1;
                }
                Console.WriteLine($"cell {target.EditorID} ({target.FormKey.ID:X6})" +
                                  $" -- {target.Temporary.Count} existing placement(s)");

                if (clear)
                {
                    // The honest discriminator for "is this one of ours" is that it CARRIES a decal
                    // block -- not its base, not its position. The module, the two dummies and any
                    // flare have no ProjectedDecal, so they cannot be caught by this.
                    var doomed = target.Temporary.OfType<PlacedObject>()
                                       .Where(p => p.ProjectedDecal != null).ToList();
                    foreach (var d in doomed) target.Temporary.Remove(d);
                    removed = doomed.Count;
                    Console.WriteLine($"--clear: removed {removed} existing decal placement(s)");
                }

                foreach (var p in placements)
                {
                    if (!uint.TryParse(p.Base, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint baseId))
                    {
                        Console.WriteLine($"Error: base '{p.Base}' is not hex");
                        return 1;
                    }
                    target.Temporary.Add(new PlacedObject(myMod)
                    {
                        // LoadOrder[0] is Starfield.esm; every decal in his shipped work is vanilla.
                        Base = new FormKey(env.LoadOrder[0].ModKey, baseId).ToLink<IPlaceableObjectGetter>(),
                        Position = new P3Float(p.Pos[0], p.Pos[1], p.Pos[2]),
                        Rotation = new P3Float(p.Rot[0], p.Rot[1], p.Rot[2]),   // RADIANS
                        ConstrainedDecal = new P3Float(p.Xcdd[0], p.Xcdd[1], p.Xcdd[2]),
                        ProjectedDecal = new PlacedObjectProjectedDecal
                        {
                            WidthScale = p.Xpdd.U,
                            HeightScale = p.Xpdd.V,
                            // Mutagen calls this UnknownFloat; it is the OPACITY -- the third float of
                            // XPDD, his median 0.400 and never 1. The library does not know what it
                            // is; the shipped data and his own account between them name it.
                            UnknownFloat = p.Xpdd.Alpha,
                            UnknownInt = (uint)(p.Xpdd.Flags[0] | (p.Xpdd.Flags[1] << 16)),
                        },
                    });
                    added++;
                }
                Console.WriteLine($"added {added} decal placement(s)");
            }

            // Back up ONCE and never overwrite: a second run would otherwise capture the
            // already-decalled state and destroy the only clean copy.
            string esm = Path.Combine(datapath, modname + ".esm");
            string bak = esm + ".pre-decals.bak";
            if (!File.Exists(bak))
            {
                File.Copy(esm, bak);
                Console.WriteLine($"backed up -> {Path.GetFileName(bak)}");
            }
            else Console.WriteLine($"backup {Path.GetFileName(bak)} already exists, kept (it is the clean one)");

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            myMod.WriteToBinary(esm, gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {removed} removed, {added} placed; existing FormIDs unchanged.");
            Console.WriteLine("Re-bridge .esm -> .esp before opening the CK.");
            return 0;
        }

        // The question that decides whether any of this is buildable through Mutagen at all.
        // Reflect the type rather than guess at a property name -- three plausible guesses were
        // wrong when this same question came up for the COBJ recipe filter, and one reflection
        // call answered it outright.
        static int Reflect()
        {
            Console.WriteLine("=== PlacedObject: every property Mutagen models ===\n");
            var props = typeof(PlacedObject).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                            .OrderBy(p => p.Name).ToList();
            foreach (var p in props)
                Console.WriteLine($"   {p.Name,-38} {Pretty(p.PropertyType)}");
            Console.WriteLine($"\n{props.Count} properties.\n");

            string[] want = { "decal", "project", "alpha", "opacit", "fade", "dimension", "extent", "cdd", "pdd" };
            var hits = props.Where(p => want.Any(w => p.Name.ToLowerInvariant().Contains(w))).ToList();
            Console.WriteLine("=== decal / projection / opacity shaped ===");
            if (hits.Count == 0)
                Console.WriteLine("   NONE -- XCDD/XPDD are not modelled on PlacedObject.");
            foreach (var p in hits)
                Console.WriteLine($"   {p.Name,-38} {Pretty(p.PropertyType)}");

            Console.WriteLine();
            Console.WriteLine("=== PlacedObjectProjectedDecal (the XPDD block) ===");
            foreach (var p in typeof(PlacedObjectProjectedDecal)
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
                Console.WriteLine($"   {p.Name,-38} {Pretty(p.PropertyType)}");
            return 0;
        }

        static string Pretty(Type t)
        {
            if (!t.IsGenericType) return t.Name;
            var inner = string.Join(", ", t.GetGenericArguments().Select(Pretty));
            return $"{t.Name.Split('`')[0]}<{inner}>";
        }

        public class Placement
        {
            public string Base { get; set; } = "";
            public float[] Pos { get; set; } = new float[3];
            public float[] Rot { get; set; } = new float[3];
            public float[] Xcdd { get; set; } = new float[3];
            public XpddBlock Xpdd { get; set; } = new XpddBlock();
        }

        public class XpddBlock
        {
            public float U { get; set; } = 1f;
            public float V { get; set; } = 1f;
            public float Alpha { get; set; } = 0.4f;
            public int[] Flags { get; set; } = new[] { 1, 0 };
        }
    }
}
