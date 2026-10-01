using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// mkkeyword: mint new KEYWORD records in a plugin by CLONING one that already ships.
    ///
    ///   mkkeyword <modname> <new_editorid>[,<new_editorid>...] --like <source_keyword_editorid>
    ///
    /// WHY CLONE. A keyword carries a TYPE (ShipModuleUpgrade, SoundEngine, ...), a colour and an
    /// FNAM block, and what the engine needs from each is unread. The manual's standing rule for a
    /// created record is clone, never construct (a constructed quest stage and a constructed GBFM
    /// both shipped missing subrecords nothing warned about), so this copies a shipped keyword whole
    /// and changes exactly two things: the FormID and the EditorID.
    ///
    /// First consumer (2026-10-01): the ship services UPGRADE screen. Vanilla groups a maker's model
    /// line in one class under one typed `ShipModuleUpgrade` keyword (51 chains, e.g. Amun-1..7 all
    /// carry ShipUpgrade_Eng_SMA_Amun-1); the Stardust ladder mints one per family and class.
    ///
    /// READBACK, not trust: after the write the plugin is re-read OFF DISK and every public property
    /// of each new keyword is compared with its source, bar the FormKey and EditorID it was meant to
    /// change. A difference fails the command loudly.
    /// </summary>
    public static class gen_mkkeyword
    {
        // What the clone is MEANT to change, plus the two flags this tool's own write path resets on
        // every record (setedid does the same) and Mutagen bookkeeping that is not record content.
        static readonly HashSet<string> Expected = new() {
            "FormKey", "EditorID", "IsCompressed", "MajorRecordFlagsRaw", "StarfieldMajorRecordFlags",
            "MajorFlags", "Registration" };

        public static int Generate(string[] args)
        {
            // args: [modname, "mkkeyword", names, "--like", source]
            bool verifyOnly = args.Contains("--verify");
            if (args.Length < 5 || args[3] != "--like")
            {
                Console.WriteLine("Usage: mkkeyword <modname> <new_editorid>[,...] --like <source_keyword_editorid> [--verify]");
                Console.WriteLine();
                Console.WriteLine("  Clones a shipped KEYWORD (any plugin in the load order) into <modname> under a");
                Console.WriteLine("  new FormID and the given EditorID; every other field is copied, then read back");
                Console.WriteLine("  off disk and compared. Refuses a name already used in <modname>.");
                Console.WriteLine("  --verify writes nothing: it grades keywords that ALREADY exist against the source.");
                return 1;
            }
            string modname = args[0];
            var names = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(n => n.Trim()).ToList();
            string sourceId = args[4].Trim();
            if (modname == "Starfield") { Console.WriteLine("No way am I allowing you to edit Starfield.esm"); return 1; }
            if (names.Count == 0) { Console.WriteLine("Error: no new EditorID given"); return 1; }
            var dupNames = names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupNames.Count > 0) { Console.WriteLine("Error: a name is given twice: " + string.Join(", ", dupNames)); return 1; }

            StarfieldMod myMod;
            string datapath;
            IKeywordGetter source;
            var minted = new List<(string name, FormKey key)>();

            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                datapath = env.DataFolderPath;
                ModKey modKey = new ModKey(modname, ModType.Master);
                if (!env.LoadOrder.ModExists(modKey)) { Console.WriteLine("Error: " + modname + ".esm is not in the load order"); return 1; }

                var found = env.LoadOrder.PriorityOrder.Keyword().WinningOverrides()
                    .Where(k => string.Equals(k.EditorID, sourceId, StringComparison.OrdinalIgnoreCase)).ToList();
                if (found.Count != 1)
                {
                    Console.WriteLine($"Error: source keyword '{sourceId}' resolves to {found.Count} record(s) in the load order -- need exactly one");
                    foreach (var f in found) Console.WriteLine("    " + f.FormKey + "  " + f.EditorID);
                    return 1;
                }
                source = found[0];
                Console.WriteLine($"  source {source.EditorID} [{source.FormKey}]  Type {source.Type}");

                ModPath modPath = System.IO.Path.Combine(datapath, modname + ".esm");
                myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);

                if (verifyOnly)
                {
                    foreach (var n in names)
                    {
                        var have = myMod.Keywords.FirstOrDefault(k => string.Equals(k.EditorID, n, StringComparison.OrdinalIgnoreCase));
                        if (have == null) { Console.WriteLine($"FAIL -- {n} does not exist in {modname}"); return 1; }
                        minted.Add((n, have.FormKey));
                    }
                    return ReadBack(minted, source, System.IO.Path.Combine(datapath, modname + ".esm"), env);
                }

                var used = myMod.EnumerateMajorRecords().Where(r => r.EditorID != null)
                    .Select(r => r.EditorID!).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var clash = names.Where(used.Contains).ToList();
                if (clash.Count > 0)
                {
                    Console.WriteLine("REFUSED -- nothing written. Already used in " + modname + ": " + string.Join(", ", clash));
                    return 1;
                }

                foreach (var n in names)
                {
                    var dup = (Keyword)source.Duplicate(myMod.GetNextFormKey());
                    dup.EditorID = n;
                    myMod.Keywords.Add(dup);
                    minted.Add((n, dup.FormKey));
                    Console.WriteLine($"  + {n}  [{dup.FormKey}]");
                }
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;
            string outPath = datapath + "\\" + modname + ".esm";
            myMod.WriteToBinary(outPath, gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {minted.Count} keyword(s) minted in ONE write.");

            // Readback off disk, WITH the load order's read parameters: an overlay opened without
            // them throws building the master package (2026-10-01, after the first real write, which
            // had landed; the guard was the fault, not the write).
            using (var env2 = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
                return ReadBack(minted, source, outPath, env2);
        }

        static int ReadBack(List<(string name, FormKey key)> minted, IKeywordGetter source, string path,
                            IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env)
        {
            var back = StarfieldMod.CreateFromBinaryOverlay(path, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
            var fails = new List<string>();
            foreach (var (n, key) in minted)
            {
                var kw = back.Keywords.FirstOrDefault(k => k.FormKey == key);
                if (kw == null) { fails.Add($"{n}: not found on disk at {key}"); continue; }
                if (kw.EditorID != n) fails.Add($"{n}: EditorID on disk is '{kw.EditorID}'");
                foreach (var p in typeof(IKeywordGetter).GetProperties())
                {
                    if (Expected.Contains(p.Name) || p.GetIndexParameters().Length > 0) continue;
                    string a = Show(p.GetValue(source)), b = Show(p.GetValue(kw));
                    if (a != b) fails.Add($"{n}.{p.Name}: source {a}, disk {b}");
                }
            }
            if (fails.Count > 0)
            {
                Console.WriteLine($"FAIL -- {fails.Count} difference(s) between source and keyword(s), read off disk:");
                foreach (var f in fails) Console.WriteLine("  " + f);
                return 1;
            }
            Console.WriteLine($"Read back off disk: {minted.Count} keyword(s), every field matches the source bar FormKey and EditorID.");
            return 0;
        }

        static string Show(object? v) => v switch
        {
            null => "null",
            string s => s,
            IEnumerable e => "[" + string.Join(",", e.Cast<object?>().Select(Show)) + "]",
            _ => v.ToString() ?? "",
        };
    }
}
