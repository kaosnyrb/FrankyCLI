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
    // Repoint an EXISTING placed reference at a different base form, FormID-stable.
    //
    //   setrefbase <modname> <cell> <old_base> <new_base> [--dry]
    //
    // WHY THIS EXISTS. Nothing could change what a placed ref points AT. placeref creates one
    // and removerecord deletes base forms (mstt/sntp/gbfm/cobj/flst/pkin) but not refs, so
    // swapping one placed form for another meant delete-and-replace -- which this codebase has
    // no path for either, and which would mint a new FormID for something a saved game may hold.
    //
    // The case it was written for (2026-09-04): a landing gear's PackIn places a vanilla floor
    // marker, and the marker IS the ride-height dial -- ShipLandEngMarkerShort01 sits at
    // Z -3.5001, Short02 at -4.2621, Tall01 at -7.0000. Choosing a different stance is choosing
    // a different marker, so that swap needs to be one command rather than a rebuild.
    //
    // IT MATCHES BY OLD BASE WITHIN ONE CELL, NOT BY REF FORMID. A ref FormID is a number nobody
    // can read off a design; "the marker in the gear's cell" is the thing actually being named.
    // If the cell holds more than one ref on that base it REFUSES rather than guessing which --
    // a silent 1-of-N is the failure mode setobnd already exists to prevent.
    //
    // Base resolution is placeref's, shared rather than copied: <plugin>:0xFORMID, 0xFORMID, or
    // an EditorID, with the same refusals.
    class gen_setrefbase
    {
        public static int Generate(string[] args)
        {
            var pos = new List<string>();
            bool dry = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (i == 1) continue;                      // RunLegacy injects the mode by index
                if (args[i] == "--dry") { dry = true; }
                else pos.Add(args[i]);
            }

            if (pos.Count < 4)
            {
                Console.WriteLine("Usage: setrefbase <modname> <cell> <old_base> <new_base> [--dry]");
                Console.WriteLine("  <cell>      a Cell EditorID -- READ IT OFF THE PLUGIN (the CK renames on save)");
                Console.WriteLine("  <old_base>  what the ref points at now; <new_base> what it should point at.");
                Console.WriteLine("  either may be <plugin>:0xFORMID, 0xFORMID, or an EditorID.");
                return 1;
            }

            string modname = pos[0], cellName = pos[1], oldName = pos[2], newName = pos[3];
            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
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

                var cell = gen_placeref.AllCells(myMod).FirstOrDefault(c =>
                    string.Equals(c.EditorID, cellName, StringComparison.OrdinalIgnoreCase));
                if (cell == null)
                {
                    Console.WriteLine("Error: no cell '" + cellName + "' in " + modname);
                    return 1;
                }

                var oldKey = gen_placeref.ResolveBase(oldName, modname, myMod, env, out var oldKind, out var okOld);
                if (!okOld) return 1;
                var newKey = gen_placeref.ResolveBase(newName, modname, myMod, env, out var newKind, out var okNew);
                if (!okNew) return 1;

                if (oldKey!.Value == newKey!.Value)
                {
                    Console.WriteLine("Error: old and new base are the same form (" + oldKey.Value + ") -- nothing to do.");
                    return 1;
                }

                var hits = cell.Temporary.Concat(cell.Persistent)
                    .OfType<PlacedObject>()
                    .Where(p => p.Base.FormKey == oldKey.Value)
                    .ToList();

                if (hits.Count == 0)
                {
                    Console.WriteLine("Error: " + cell.EditorID + " places no ref on '" + oldName + "' (" + oldKey.Value + ")");
                    Console.WriteLine("  it holds " + (cell.Temporary.Count + cell.Persistent.Count) + " ref(s):");
                    foreach (var p in cell.Temporary.Concat(cell.Persistent).OfType<IPlacedObjectGetter>())
                        Console.WriteLine("    " + p.FormKey + "  base " + p.Base.FormKey);
                    return 1;
                }
                if (hits.Count > 1)
                {
                    Console.WriteLine("Error: " + cell.EditorID + " places " + hits.Count + " refs on '" + oldName
                                      + "' -- refusing to guess which. Their FormIDs:");
                    foreach (var p in hits) Console.WriteLine("    " + p.FormKey);
                    return 1;
                }

                var target = hits[0];
                Console.WriteLine("cell   : " + cell.EditorID + "   (" + cell.FormKey + ")");
                Console.WriteLine("ref    : " + target.FormKey
                                  + string.Format("   at ({0:F4}, {1:F4}, {2:F4})", target.Position.X, target.Position.Y, target.Position.Z));
                Console.WriteLine("base   : " + oldName + "  " + oldKind);
                Console.WriteLine("      -> " + newName + "  " + newKind);

                if (dry)
                {
                    Console.WriteLine("\n--dry: nothing written");
                    return 0;
                }

                target.Base = newKey.Value.ToLink<IPlaceableObjectGetter>();
                Console.WriteLine("  repointed REFR " + target.FormKey + " -- position, rotation and FormID unchanged");
            }

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine("\nwrote " + datapath + "\\" + modname + ".esm");
            return 0;
        }
    }
}
