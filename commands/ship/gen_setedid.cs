using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Parameters;
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
    // WHOLE RISK AND IS NOT OBVIOUS: every reference BETWEEN records is a FormID, so renaming
    // cannot break a link, a recipe, a FormList membership or a placed object -- the CK will
    // show the new name everywhere the old one appeared and nothing else moves.
    // ⛔ WITH ONE EXCEPTION, AND THIS LINE SAID "NOTHING IN THE PLUGIN RESOLVES ANYTHING BY
    // EDITORID" UNTIL IT BROKE (2026-10-26): DISPLAY TEXT DOES. A quest objective's counter is
    // "(<Global=duo_x_count1>/3)", and the game finds that global BY ITS EDITORID when it draws
    // the line. The Delve rename renamed eight counter globals and left their objectives naming
    // the old ones; his play showed "([...]/3)". So --map now carries every rename into the
    // <Global=> tokens of quest objectives and journal entries, and both --map and --retoken
    // read the written file's bytes back for any token naming an EditorID that no longer exists.
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
                Console.WriteLine("       setedid <modname> --map <map.tsv> [--dry]   many at once: one load, one write, all or nothing");
                Console.WriteLine("       setedid <modname> --retoken <map.tsv> [--dry]   only carry a map's renames into <Global=> tokens in quest text");
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
            if (args[2] == "--map") return RenameFromMap(modname, args[3], args.Skip(4).ToArray());
            if (args[2] == "--retoken") return RetokenFromMap(modname, args[3], args.Skip(4).ToArray());
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

        // ------------------------------------------------------------------ --map
        //
        // Many renames in one load and one write. His ask 2026-10-26: the Delves back onto the classic
        // naming (duo_delve01a -> duo_artifact_localcargo_qst20a, 882 records) "without having to rebuild
        // every quest". One call per record would load the game environment and rewrite the plugin 882
        // times, and a run that died half way would leave a half-renamed mod.
        //
        // THE MAP: one row per record, tab separated, "<type>\t<FormID>\t<old>\t<new>", the FormID as the
        // plugin's own hex (01002FC0: the high byte is the mod index, the low 24 bits the ID). The type
        // column is for the reader and is not trusted. A row names its record TWICE, by FormID and by its
        // current EditorID, and both must agree: a map built off an older copy of the plugin is refused
        // row by row instead of renaming whatever now sits at that FormID.
        //
        // ALL OR NOTHING. Every row is checked before any record changes: the record exists, its EditorID
        // is the row's old name, no two rows share a new name, and no new name is held by a record the
        // map does not also rename away. Then one write, then the file is re-read off disk and every
        // EditorID in it compared with what the map predicts, so a rename that did not land, or a
        // record that changed without being asked, is named rather than assumed.
        private static int RenameFromMap(string modname, string mapPath, string[] flags)
        {
            bool dry = false;
            foreach (var f in flags)
            {
                if (f == "--dry") { dry = true; continue; }
                Console.WriteLine("Error: unknown option " + f);
                return 1;
            }
            if (modname == "Starfield") { Console.WriteLine("No way am I allowing you to edit Starfield.esm"); return 1; }
            if (!System.IO.File.Exists(mapPath)) { Console.WriteLine("Error: no map file " + mapPath); return 1; }

            var rows = new List<(uint id, string oldId, string newId, int line)>();
            var bad = new List<string>();
            int ln = 0;
            foreach (var raw in System.IO.File.ReadAllLines(mapPath))
            {
                ln++;
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var c = line.Split('\t');
                if (c.Length != 4) { bad.Add($"line {ln}: {c.Length} columns, want 4 (type, FormID, old, new)"); continue; }
                if (!uint.TryParse(c[1], System.Globalization.NumberStyles.HexNumber, null, out var fid)) { bad.Add($"line {ln}: FormID '{c[1]}' is not hex"); continue; }
                if (c[2].Trim().Length == 0 || c[3].Trim().Length == 0) { bad.Add($"line {ln}: an empty EditorID"); continue; }
                if (string.Equals(c[2], c[3], StringComparison.Ordinal)) { bad.Add($"line {ln}: old and new are identical ({c[2]})"); continue; }
                rows.Add((fid & 0x00FFFFFF, c[2].Trim(), c[3].Trim(), ln));
            }
            if (rows.Count == 0 && bad.Count == 0) { Console.WriteLine("Error: the map has no rows"); return 1; }

            StarfieldMod myMod;
            string datapath;
            // Kept for the read-back: a Starfield plugin cannot be parsed without its masters' info, and the
            // first version read back with none and crashed AFTER the write (2026-10-26, the write was good).
            BinaryReadParameters readParams;
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                datapath = env.DataFolderPath;
                ModKey modKey = new ModKey(modname, ModType.Master);
                if (!env.LoadOrder.ModExists(modKey)) { Console.WriteLine("Error: " + modname + ".esm is not in the load order"); return 1; }
                readParams = gen_quest_main.BuildReadParams(env.LoadOrder);
                myMod = StarfieldMod.CreateFromBinary(System.IO.Path.Combine(datapath, modname + ".esm"), StarfieldRelease.Starfield, readParams);
                gen_quest_main.FixNextFormId(myMod);
            }

            var byId = new Dictionary<uint, IMajorRecord>();
            foreach (var rec in myMod.EnumerateMajorRecords())
                if (rec.FormKey.ModKey == myMod.ModKey) byId[rec.FormKey.ID] = rec;

            var seenId = new HashSet<uint>();
            foreach (var r in rows)
            {
                if (!seenId.Add(r.id)) { bad.Add($"line {r.line}: FormID {r.id:X6} appears twice in the map"); continue; }
                if (!byId.TryGetValue(r.id, out var rec)) { bad.Add($"line {r.line}: no record {r.id:X6} in {modname}"); continue; }
                if (!string.Equals(rec.EditorID, r.oldId, StringComparison.OrdinalIgnoreCase))
                    bad.Add($"line {r.line}: {r.id:X6} is '{rec.EditorID}', the map says '{r.oldId}' (built off another copy?)");
            }
            foreach (var g in rows.GroupBy(r => r.newId, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                bad.Add($"new name '{g.Key}' is given to {g.Count()} records (lines {string.Join(", ", g.Select(x => x.line))})");
            var leaving = new HashSet<uint>(rows.Select(r => r.id));
            var staying = new Dictionary<string, IMajorRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (var rec in byId.Values)
                if (!leaving.Contains(rec.FormKey.ID) && rec.EditorID != null) staying[rec.EditorID] = rec;
            foreach (var r in rows)
                if (staying.TryGetValue(r.newId, out var holder))
                    bad.Add($"line {r.line}: '{r.newId}' is already {holder.FormKey} ({holder.GetType().Name}), which the map does not rename");

            if (bad.Count > 0)
            {
                Console.WriteLine($"REFUSED: {bad.Count} problem(s) in {mapPath}; nothing written.");
                foreach (var b in bad.Take(40)) Console.WriteLine("  " + b);
                if (bad.Count > 40) Console.WriteLine($"  ... and {bad.Count - 40} more");
                return 1;
            }

            foreach (var g in rows.GroupBy(r => byId[r.id].GetType().Name).OrderBy(g => g.Key))
                Console.WriteLine($"  {g.Key,-22} {g.Count(),4}");
            Console.WriteLine($"  {rows.Count} record(s) to rename in {modname}.esm, every FormID unchanged.");
            if (dry) { Console.WriteLine("\n--dry: nothing written."); return 0; }

            var before = byId.ToDictionary(kv => kv.Key, kv => kv.Value.EditorID);
            foreach (var r in rows) byId[r.id].EditorID = r.newId;
            int tokens = Retoken(myMod, rows.ToDictionary(r => r.oldId, r => r.newId, StringComparer.OrdinalIgnoreCase));
            Console.WriteLine($"  <Global=> tokens carried into quest text: {tokens}");
            foreach (var rec in myMod.EnumerateMajorRecords()) rec.IsCompressed = false;
            string outPath = datapath + "\\" + modname + ".esm";
            myMod.WriteToBinary(outPath, gen_quest_main.BuildWriteParams());

            // Read it back off disk: every EditorID is either the map's new name or what it was before.
            var expect = new Dictionary<uint, string?>(before);
            foreach (var r in rows) expect[r.id] = r.newId;
            using var back = StarfieldMod.CreateFromBinaryOverlay(outPath, StarfieldRelease.Starfield, readParams);
            var wrong = new List<string>();
            int seen = 0;
            foreach (var rec in back.EnumerateMajorRecords())
            {
                if (rec.FormKey.ModKey != back.ModKey) continue;
                seen++;
                if (!expect.TryGetValue(rec.FormKey.ID, out var want)) { wrong.Add($"{rec.FormKey} appeared ({rec.EditorID})"); continue; }
                if (!string.Equals(rec.EditorID, want, StringComparison.Ordinal)) wrong.Add($"{rec.FormKey} is '{rec.EditorID}', expected '{want}'");
            }
            if (seen != expect.Count) wrong.Add($"{seen} records read back, {expect.Count} expected");
            wrong.AddRange(DanglingTokens(outPath, expect.Values));
            if (wrong.Count > 0)
            {
                Console.WriteLine($"WRITTEN, BUT THE READ-BACK DISAGREES ({wrong.Count}): restore from the snapshot.");
                foreach (var w in wrong.Take(40)) Console.WriteLine("  " + w);
                return 1;
            }
            Console.WriteLine($"\nFinished -- {rows.Count} renamed, read back off disk: every EditorID as the map predicts, {seen} records, no FormID moved, no <Global=> token names a missing EditorID.");
            Console.WriteLine("⚠ Anything OUTSIDE the plugin holding the old names as text is now stale: recipes, notes, saved command lines.");
            return 0;
        }

        static readonly System.Text.RegularExpressions.Regex GlobalToken = new("<Global=([A-Za-z0-9_]+)>");

        /// Rewrite every <Global=old> in a quest objective's display text and a stage's journal entries to the map's new
        /// name. Those are where the game resolves a name at draw time; returns how many tokens changed.
        static int Retoken(StarfieldMod mod, Dictionary<string, string> oldToNew)
        {
            int n = 0;
            string? Fix(string? s)
            {
                if (s == null || !s.Contains("<Global=")) return s;
                return GlobalToken.Replace(s, m => oldToNew.TryGetValue(m.Groups[1].Value, out var nw) ? (n++ >= 0 ? $"<Global={nw}>" : "") : m.Value);
            }
            foreach (var q in mod.Quests)
            {
                foreach (var ob in q.Objectives)
                {
                    var s = ob.DisplayText?.String; var f = Fix(s);
                    if (f != s) ob.DisplayText = f;
                }
                foreach (var st in q.Stages)
                    foreach (var le in st.LogEntries)
                    {
                        var s = le.Entry?.String; var f = Fix(s);
                        if (f != s) le.Entry = f;
                    }
            }
            return n;
        }

        /// Read the written file's BYTES (every record is written uncompressed) for <Global=X> tokens and name each X that
        /// is not an EditorID in the plugin. Bytes, not the fields Retoken walks, so a text surface it does not walk still
        /// shows up here instead of shipping quietly.
        static List<string> DanglingTokens(string path, IEnumerable<string?> edids)
        {
            var have = new HashSet<string>(edids.Where(e => e != null)!, StringComparer.OrdinalIgnoreCase);
            var text = System.Text.Encoding.Latin1.GetString(System.IO.File.ReadAllBytes(path));
            return GlobalToken.Matches(text).Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase)
                              .Where(x => !have.Contains(x)).Select(x => $"text token <Global={x}> names no record in the plugin").ToList();
        }

        // --retoken: carry a map's renames into the quest text only, for a rename already applied without it (the 2026-10-26
        // Delve rename). Every row's NEW name must already be a record (that is what makes the repair true); then one write
        // and the same byte read-back as --map.
        private static int RetokenFromMap(string modname, string mapPath, string[] flags)
        {
            bool dry = flags.Contains("--dry");
            if (flags.Any(f => f != "--dry")) { Console.WriteLine("Error: unknown option " + flags.First(f => f != "--dry")); return 1; }
            if (modname == "Starfield") { Console.WriteLine("No way am I allowing you to edit Starfield.esm"); return 1; }
            var pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in System.IO.File.ReadAllLines(mapPath))
            {
                var c = raw.TrimEnd('\r').Split('\t');
                if (c.Length == 4 && c[2].Trim().Length > 0) pairs[c[2].Trim()] = c[3].Trim();
            }
            StarfieldMod myMod; string datapath; BinaryReadParameters readParams;
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                datapath = env.DataFolderPath;
                readParams = gen_quest_main.BuildReadParams(env.LoadOrder);
                myMod = StarfieldMod.CreateFromBinary(System.IO.Path.Combine(datapath, modname + ".esm"), StarfieldRelease.Starfield, readParams);
                gen_quest_main.FixNextFormId(myMod);
            }
            var edids = myMod.EnumerateMajorRecords().Where(r => r.FormKey.ModKey == myMod.ModKey).Select(r => r.EditorID).ToList();
            var have = new HashSet<string>(edids.Where(e => e != null)!, StringComparer.OrdinalIgnoreCase);
            var notThere = pairs.Values.Where(v => !have.Contains(v)).ToList();
            if (notThere.Count > 0) { Console.WriteLine($"REFUSED: {notThere.Count} new name(s) in the map are not records yet (run --map first): {string.Join(", ", notThere.Take(5))}"); return 1; }

            int n = Retoken(myMod, pairs);
            Console.WriteLine($"  <Global=> tokens to carry into quest text: {n}");
            if (dry || n == 0) { Console.WriteLine(n == 0 ? "  nothing to do; nothing written." : "\n--dry: nothing written."); return 0; }
            foreach (var rec in myMod.EnumerateMajorRecords()) rec.IsCompressed = false;
            string outPath = datapath + "\\" + modname + ".esm";
            myMod.WriteToBinary(outPath, gen_quest_main.BuildWriteParams());
            var dangling = DanglingTokens(outPath, edids);
            using var back = StarfieldMod.CreateFromBinaryOverlay(outPath, StarfieldRelease.Starfield, readParams);
            int changedEdids = back.EnumerateMajorRecords().Where(r => r.FormKey.ModKey == back.ModKey).Select(r => r.EditorID).Zip(edids).Count(p => p.First != p.Second);
            if (dangling.Count > 0 || changedEdids > 0)
            {
                Console.WriteLine($"WRITTEN, BUT THE READ-BACK DISAGREES: restore from the snapshot.");
                foreach (var d in dangling) Console.WriteLine("  " + d);
                if (changedEdids > 0) Console.WriteLine($"  {changedEdids} EditorID(s) changed, and --retoken changes none");
                return 1;
            }
            Console.WriteLine($"\nFinished -- {n} token(s) carried, read back off disk: no <Global=> token names a missing EditorID, no EditorID changed.");
            return 0;
        }
    }
}
