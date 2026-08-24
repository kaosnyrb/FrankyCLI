using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    // Rename a record's EditorID, FormID-stable. Type-agnostic: EditorID is a field on
    // every major record, so this does not care whether the target is a GBFM, a FormList,
    // a PackIn or a Cell.
    //
    //   setedid <modname> <old_editorid> <new_editorid> [--dry]
    //
    // WHY THIS EXISTS. Nothing could rename a record. setname sets a GBFM's FullName --
    // the string the ship builder shows the player -- which is a different field entirely,
    // and the confusion between them is exactly why this is its own command with a name
    // that says which one it is. The hab (2026-08-24) landed with atsd_gbf_hab01 against
    // the line's atsd_gbfm_* on twenty other parts, and a FormList called
    // atsd_HabVariants_spine against atsd_flst_*. The part worked; the DOCTOR could not
    // see it, which is worse than a part that fails loudly.
    //
    // ⭐ WHY A RENAME IS SAFE INSIDE THE PLUGIN AND DANGEROUS OUTSIDE IT, WHICH IS THE
    // WHOLE RISK AND IS NOT OBVIOUS: every reference BETWEEN records is a FormID. Nothing
    // in the plugin resolves anything by EditorID, so renaming cannot break a link, a
    // recipe, a FormList membership or a placed object -- the CK will show the new name
    // everywhere the old one appeared and nothing else moves.
    // ⛔ THE TOOLING IS THE OPPOSITE. check_part discovers parts by EditorID string;
    // build_archlist, the manual, the desk notes and every command that takes an
    // <editorid> argument hold the name as TEXT. So a rename is a change to an API whose
    // consumers are scripts and documents, not records. "The EditorID is the identity, a
    // FormID written into a doc is a snapshot" cuts both ways: the identity is the thing
    // people wrote down. Rename deliberately, and sweep the callers after.
    //
    // ⛔ REFUSES A COLLISION. Two records sharing an EditorID is corruption the CK will
    // not warn you about at load time, and the second one becomes unreachable by name from
    // every tool that resolves by string -- silently, and looking exactly like a missing
    // record. Matched case-insensitively, because that is how the CK and every command in
    // this repo compare them.
    //
    // ⛔ REFUSES AN AMBIGUOUS SOURCE. If the old name matches more than one record this
    // stops and lists them rather than picking. There is no correct guess.
    //
    // ⚠ THE HOLE, STATED RATHER THAN LEFT: the collision check covers THIS MOD ONLY. A
    // cross-plugin EditorID collision (a name already used in Starfield.esm or another
    // mod) is NOT detected, because scanning the whole load order costs a full walk of
    // millions of records on every rename. Prefixed names (atsd_*) cannot realistically
    // collide, which is why the cost is not worth paying -- but an undocumented hole in a
    // guard is worse than no guard, so it is written here rather than assumed.
    class gen_setedid
    {
        public static int Generate(string[] args)
        {
            // args: [modname, "setedid", old, new, ...flags] -- so a complete call is FOUR,
            // not five. It was five until 2026-08-24 and the whole refusal suite went
            // vacuously green behind it: four different bad inputs all printed this usage
            // text instead of their own error, and one test passed only because its extra
            // flag pushed the length over the wrong bound.
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: setedid <modname> <old_editorid> <new_editorid> [--dry]");
                Console.WriteLine();
                Console.WriteLine("  Renames a record's EditorID, FormID-stable, any record type.");
                Console.WriteLine("  NOT setname -- that sets a GBFM's FullName, the string the player sees.");
                Console.WriteLine();
                Console.WriteLine("  Safe inside the plugin (every reference between records is a FormID).");
                Console.WriteLine("  Breaks anything OUTSIDE it that holds the name as text -- check_part");
                Console.WriteLine("  discovers parts by EditorID. Sweep the callers after.");
                return 1;
            }

            string modname = args[0];
            string oldId = args[2].Trim();
            string newId = args[3].Trim();
            bool dry = false;

            for (int i = 4; i < args.Length; i++)
            {
                if (args[i] == "--dry") { dry = true; continue; }
                Console.WriteLine("Error: unknown option " + args[i]);
                return 1;
            }

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }
            if (oldId.Length == 0 || newId.Length == 0)
            {
                Console.WriteLine("Error: both EditorIDs must be non-empty");
                return 1;
            }
            if (string.Equals(oldId, newId, StringComparison.Ordinal))
            {
                Console.WriteLine("Error: old and new EditorID are identical -- nothing to do");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;
            string renamedFrom, renamedTo, renamedKind;
            FormKey renamedKey;

            // env holds the plugin open, so it is scoped to close before the write -- a
            // same-path WriteToBinary inside the using throws and leaves the old bytes
            // looking like a persisted no-op. Same reason as gen_setmass / gen_setlinks.
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

                // ONE walk, both questions. EnumerateMajorRecords is type-agnostic, which is
                // the whole reason this command does not need to know what it is renaming.
                var matches = new List<IMajorRecord>();
                var collisions = new List<IMajorRecord>();
                foreach (var rec in myMod.EnumerateMajorRecords())
                {
                    if (string.Equals(rec.EditorID, oldId, StringComparison.OrdinalIgnoreCase))
                        matches.Add(rec);
                    else if (string.Equals(rec.EditorID, newId, StringComparison.OrdinalIgnoreCase))
                        collisions.Add(rec);
                }

                if (matches.Count == 0)
                {
                    Console.WriteLine("Error: no record with EditorID '" + oldId + "' in " + modname);
                    return 1;
                }
                if (matches.Count > 1)
                {
                    // No correct guess exists, so it stops rather than picking one.
                    Console.WriteLine("Error: '" + oldId + "' matches " + matches.Count + " records -- refusing to guess:");
                    foreach (var m in matches)
                        Console.WriteLine("    " + m.FormKey + "  " + m.GetType().Name + "  " + m.EditorID);
                    return 1;
                }
                if (collisions.Count > 0)
                {
                    Console.WriteLine("Error: '" + newId + "' is already used in " + modname
                                      + " -- two records sharing an EditorID is corruption the CK will not warn about:");
                    foreach (var c in collisions)
                        Console.WriteLine("    " + c.FormKey + "  " + c.GetType().Name + "  " + c.EditorID);
                    return 1;
                }

                var target = matches[0];
                renamedKey = target.FormKey;
                renamedKind = target.GetType().Name;
                renamedFrom = target.EditorID ?? "";
                renamedTo = newId;

                Console.WriteLine("  " + renamedKey + "  " + renamedKind);
                Console.WriteLine("    " + renamedFrom + "  ->  " + renamedTo);

                if (dry)
                {
                    Console.WriteLine();
                    Console.WriteLine("--dry: nothing written.");
                    return 0;
                }

                target.EditorID = newId;
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine();
            Console.WriteLine("Finished -- 1 " + renamedKind + " renamed, FormID " + renamedKey + " unchanged.");
            Console.WriteLine("⚠ Anything holding '" + renamedFrom + "' as TEXT is now stale:");
            Console.WriteLine("  check_part discovery, desk notes, the manual, and any saved command line.");
            return 0;
        }
    }
}
