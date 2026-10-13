using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FrankyCLI
{
    /// <summary>
    /// QUEST TEXT FROM A MANIFEST: an author's patch applied to a plugin, then read back off disk.
    ///
    ///   gen_questtext apply <patch.md | manifest.json> [--dry]
    ///
    /// Built 2026-10-13 for Jessica's Overtime patch 11 (143 board titles back to his "Type: Thing"). Patch 01
    /// had asked for exactly this in July ("hand-applied in xEdit is fine for eighteen and miserable for
    /// fifty-two. If an applier eats the JSON above...") and nothing was built until the count made it necessary.
    ///
    /// THE AUTHOR'S DOCUMENT IS THE SOURCE. Given a .md, the manifest is the fenced ```json block under the heading
    /// carrying "[ENGINE]" (her "the applier's copy"), so there is no second copy to drift from the one she wrote.
    ///
    /// FIELDS: "full" (the quest's FULL name, what the board and the log show) is the only one built. Every other
    /// key is REFUSED by name, never skipped: a field this tool silently ignored would read as applied.
    ///
    /// Guards, all before anything is written: the manifest names the plugin it is for, and it is this one; every
    /// edid is a quest the plugin itself owns; no edid twice; no empty text. Then the plugin is written once and
    /// re-read off disk: every named quest carries exactly its manifest text, the masters are unchanged, and no
    /// quest the manifest does not name changed its name.
    /// </summary>
    public static class gen_questtext
    {
        private static readonly HashSet<string> TopKeys = new() { "plugin", "author", "date", "fields_changed", "records" };
        private static readonly HashSet<string> RecordKeys = new() { "edid", "full" };

        public static int Run(string[] args)
        {
            if (args.Length < 3 || args[1] != "apply")
            {
                Console.WriteLine("Usage: gen_questtext apply <patch.md | manifest.json> [--dry]");
                return 1;
            }
            bool dry = args.Contains("--dry");
            string src = args[2];
            if (!File.Exists(src)) { Console.WriteLine("REFUSED: " + src + " does not exist."); return 1; }

            string json;
            if (src.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                var text = File.ReadAllText(src);
                // Line endings either way: the office's patch files are CRLF on Windows checkouts.
                var m = Regex.Match(text, @"^#+[^\r\n]*\[ENGINE\][^\r\n]*(?:\r?\n)+```json[ \t]*\r?\n(.*?)\r?\n```", RegexOptions.Singleline | RegexOptions.Multiline);
                if (!m.Success) { Console.WriteLine("REFUSED: no ```json block under an [ENGINE] heading in " + src + "."); return 1; }
                json = m.Groups[1].Value;
            }
            else json = File.ReadAllText(src);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var unknown = root.EnumerateObject().Select(p => p.Name).Where(n => !TopKeys.Contains(n) && !n.StartsWith("_")).ToList();
            if (unknown.Count > 0) { Console.WriteLine("REFUSED: manifest key(s) this tool does not know: " + string.Join(", ", unknown)); return 1; }
            if (!root.TryGetProperty("plugin", out var pe) || pe.GetString() is not string plugin) { Console.WriteLine("REFUSED: the manifest names no plugin."); return 1; }
            if (root.TryGetProperty("fields_changed", out var fc))
            {
                var f = fc.EnumerateArray().Select(x => x.GetString()).ToList();
                if (f.Count != 1 || f[0] != "FULL") { Console.WriteLine("REFUSED: fields_changed is [" + string.Join(", ", f) + "]; only FULL is built."); return 1; }
            }

            var want = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var problems = new List<string>();
            foreach (var r in root.GetProperty("records").EnumerateArray())
            {
                foreach (var k in r.EnumerateObject().Select(p => p.Name).Where(n => !RecordKeys.Contains(n) && !n.StartsWith("_")))
                    problems.Add($"a record carries '{k}', which this tool does not apply");
                string? edid = r.TryGetProperty("edid", out var e) ? e.GetString() : null;
                string? full = r.TryGetProperty("full", out var fu) ? fu.GetString() : null;
                if (string.IsNullOrWhiteSpace(edid)) { problems.Add("a record has no edid"); continue; }
                if (string.IsNullOrWhiteSpace(full)) { problems.Add($"{edid}: no full text"); continue; }
                if (!want.TryAdd(edid, full)) problems.Add($"{edid} appears twice");
            }

            using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            string modFile = Path.Combine(env.DataFolderPath, plugin);
            if (!File.Exists(modFile)) { Console.WriteLine("REFUSED: " + modFile + " does not exist."); return 1; }
            var readParams = gen_quest_main.BuildReadParams(env.LoadOrder);
            var myMod = StarfieldMod.CreateFromBinary(modFile, StarfieldRelease.Starfield, readParams);
            var mastersBefore = string.Join(", ", myMod.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));
            var quests = myMod.Quests.Where(q => q.FormKey.ModKey == myMod.ModKey && q.EditorID != null)
                                     .ToDictionary(q => q.EditorID!, StringComparer.OrdinalIgnoreCase);
            foreach (var edid in want.Keys.Where(k => !quests.ContainsKey(k)))
                problems.Add($"{edid} is not a quest {plugin} owns");
            if (problems.Count > 0)
            {
                Console.WriteLine($"REFUSED, NOTHING WRITTEN: {problems.Count} problem(s)");
                foreach (var p in problems) Console.WriteLine("  " + p);
                return 1;
            }

            var namesBefore = quests.ToDictionary(kv => kv.Key, kv => kv.Value.Name?.String ?? "", StringComparer.OrdinalIgnoreCase);
            int changed = 0, same = 0;
            foreach (var (edid, full) in want)
            {
                if (namesBefore[edid] == full) { same++; continue; }
                Console.WriteLine($"  {edid,-24} {namesBefore[edid]}");
                Console.WriteLine($"  {"",-24} -> {full}");
                if (!dry) quests[edid].Name = full;
                changed++;
            }
            Console.WriteLine($"\n  {want.Count} in the manifest: {changed} to change, {same} already carry their text.");
            if (dry) { Console.WriteLine("  --dry: nothing written."); return 0; }
            if (changed == 0) { Console.WriteLine("  nothing to write."); return 0; }

            foreach (var rec in myMod.EnumerateMajorRecords()) rec.IsCompressed = false;
            env.Dispose();   // release the memory-mapped load order before overwriting one of its files
            myMod.WriteToBinary(modFile, gen_quest_main.BuildWriteParams());
            Console.WriteLine("  wrote " + modFile + " (" + new FileInfo(modFile).Length.ToString("N0") + " B)");

            using var reread = StarfieldMod.CreateFromBinaryOverlay(modFile, StarfieldRelease.Starfield, readParams);
            var after = reread.Quests.Where(q => q.FormKey.ModKey == reread.ModKey && q.EditorID != null)
                                     .ToDictionary(q => q.EditorID!, q => q.Name?.String ?? "", StringComparer.OrdinalIgnoreCase);
            int right = want.Count(kv => after.TryGetValue(kv.Key, out var n) && n == kv.Value);
            var strayed = namesBefore.Where(kv => !want.ContainsKey(kv.Key) && (!after.TryGetValue(kv.Key, out var n) || n != kv.Value)).Select(kv => kv.Key).ToList();
            var mastersAfter = string.Join(", ", reread.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));
            Console.WriteLine($"  [{(right == want.Count ? "OK  " : "FAIL")}] off disk: {right} of {want.Count} quest(s) carry their manifest text");
            Console.WriteLine($"  [{(strayed.Count == 0 ? "OK  " : "FAIL")}] no other quest's name changed{(strayed.Count == 0 ? "" : ": " + string.Join(", ", strayed.Take(10)))}");
            Console.WriteLine($"  [{(mastersAfter == mastersBefore ? "OK  " : "FAIL")}] the plugin's masters are unchanged ({mastersAfter})");
            foreach (var kv in want.Where(kv => !after.TryGetValue(kv.Key, out var n) || n != kv.Value).Take(10))
                Console.WriteLine($"    {kv.Key}: got '{(after.TryGetValue(kv.Key, out var g) ? g : "missing")}'");
            return right == want.Count && strayed.Count == 0 && mastersAfter == mastersBefore ? 0 : 1;
        }
    }
}
