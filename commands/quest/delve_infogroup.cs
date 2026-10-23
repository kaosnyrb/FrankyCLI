using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FrankyCLI
{
    /// <summary>
    /// `gen_delve infogroup &lt;delveId&gt;[,...] [--clear] [--dry]`: set (or clear) the record-header flag InfoGroup on a
    /// built Delve's walk-up groups (&lt;id&gt;_talk&lt;n&gt;_hellos) IN PLACE, with no rebuild, so nothing else about the quest
    /// moves. His ask, 2026-10-23: "thought we try the InfoGroup fix on a different quest, a simpler one that we know works".
    ///
    /// WHY IN PLACE: the header is the one untested lever on the silent "Captain" (every vanilla walk-up group carries it,
    /// ours never has). On 19a it went in through a rebuild, the rebuild's start then failed for reasons no byte explained,
    /// and the header was reverted for a cause it did not have. A rebuild re-mints ids, re-derives lip files and carries every
    /// shared-code change since the Delve was last built; this changes one bit per group and proves it did.
    ///
    /// THE PROOF: the would-be plugin is written (to a temp file on --dry, live otherwise) and compared with the live bytes
    /// read before. It passes only if the sizes match and every differing byte is the low flags byte of one of the named
    /// INFO records' headers (type INFO eight bytes before, its own id four bytes after), differing in bit 0x40 alone, once
    /// per group. Anything else is a FAIL with the offsets printed. Snapshot the live plugin before the real run
    /// (DU_Overtime `sync_plugin.py --snapshot`); restoring stays a hand copy, by that repo's rule.
    /// </summary>
    internal static class DelveInfoGroup
    {
        const uint InfoGroupBit = 0x40;   // DialogResponses.MajorFlag.InfoGroup, raw

        internal static int Run(string[] args, string dataDir, List<string> mods, bool dry)
        {
            var pos = args.Skip(2).Where(a => !a.StartsWith("--")).ToList();
            bool clear = args.Any(a => a.Equals("--clear", StringComparison.OrdinalIgnoreCase));
            if (pos.Count != 1) { Console.WriteLine("REFUSED: gen_delve infogroup <delveId>[,<delveId>...] [--clear] [--dry]"); return 1; }
            var ids = pos[0].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (mods.Count != 1) { Console.WriteLine("REFUSED: the templates name " + mods.Count + " mods; infogroup knows which plugin only when they agree."); return 1; }
            var recipeIds = Directory.GetFiles(Path.Combine(dataDir, "recipes"), "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var notDelves = ids.Where(i => !recipeIds.Contains(i)).ToList();
            if (notDelves.Count > 0) { Console.WriteLine("REFUSED: not a Delve (no recipe): " + string.Join(", ", notDelves) + ". Nothing written."); return 1; }

            using var session = PluginSession.Open(mods[0]);
            if (session == null) return 1;
            var myMod = session.Mod;
            string livePath = session.PluginPath;

            // Every walk-up group of every named Delve, found through its own quest's topics.
            var groups = new List<(string delve, DialogResponses grp)>();
            foreach (var id in ids)
            {
                var q = myMod.Quests.FirstOrDefault(x => string.Equals(x.EditorID, id, StringComparison.OrdinalIgnoreCase));
                if (q == null) { Console.WriteLine($"REFUSED: {id} is not built into {mods[0]}.esm. Nothing written."); return 1; }
                var found = q.DialogTopics.SelectMany(t => t.Responses)
                    .Where(r => r.EditorID != null && r.EditorID.StartsWith(id + "_talk", StringComparison.OrdinalIgnoreCase)
                                && r.EditorID.EndsWith("_hellos", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (found.Count == 0) { Console.WriteLine($"REFUSED: {id} has no walk-up group (<id>_talk<n>_hellos). Nothing written."); return 1; }
                groups.AddRange(found.Select(g => (id, g)));
            }

            Console.WriteLine();
            Console.WriteLine($"=== INFOGROUP {(clear ? "CLEAR" : "SET")}: {string.Join(", ", ids)} ===");
            var toChange = new List<DialogResponses>();
            foreach (var (delve, g) in groups)
            {
                bool has = g.MajorFlags.HasFlag(DialogResponses.MajorFlag.InfoGroup);
                Console.WriteLine($"  {g.FormKey.ID:X6} {g.EditorID}: header {g.MajorFlags}, flags {g.Flags?.Flags}, {g.Conditions.Count} condition(s)");
                if (has == !clear) { Console.WriteLine("    already " + (clear ? "clear" : "set") + "; left alone"); continue; }
                if (clear) g.MajorFlags &= ~DialogResponses.MajorFlag.InfoGroup;
                else g.MajorFlags |= DialogResponses.MajorFlag.InfoGroup;
                toChange.Add(g);
            }
            if (toChange.Count == 0) { Console.WriteLine("\n  Nothing to change: every group is already " + (clear ? "clear" : "set") + ". Nothing written."); return 0; }

            byte[] before = File.ReadAllBytes(livePath);
            // Mutagen refuses a file name that is not the plugin's own, so a dry run writes it in a folder of its own.
            string? dryDir = dry ? Path.Combine(Path.GetTempPath(), $"frankycli_infogroup_{DateTime.Now:yyyyMMdd_HHmmss}") : null;
            if (dryDir != null) Directory.CreateDirectory(dryDir);
            string outPath = dryDir != null ? Path.Combine(dryDir, Path.GetFileName(livePath)) : livePath;
            session.Close();
            session.Write(outPath);
            byte[] after = File.ReadAllBytes(outPath);
            Console.WriteLine($"  wrote {outPath}{(dry ? "  (--dry: the live plugin is untouched)" : "")}");

            int fails = 0;
            void Check(bool ok, string what) { Console.WriteLine($"  [{(ok ? "OK  " : "FAIL")}] {what}"); if (!ok) fails++; }

            var diffs = new List<int>();
            if (before.Length == after.Length)
                for (int i = 0; i < before.Length; i++) if (before[i] != after[i]) diffs.Add(i);
            Check(before.Length == after.Length, $"the plugin is the same size ({before.Length:N0} -> {after.Length:N0} bytes)");
            var expected = toChange.Select(g => g.FormKey.ID).ToHashSet();
            var hit = new HashSet<uint>();
            var stray = new List<int>();
            foreach (int o in diffs)
            {
                bool isFlagsByte = o >= 8 && o + 7 <= after.Length
                    && Encoding.ASCII.GetString(after, o - 8, 4) == "INFO"
                    && (before[o] ^ after[o]) == InfoGroupBit;
                uint fid = isFlagsByte ? (uint)(after[o + 4] | after[o + 5] << 8 | after[o + 6] << 16) : 0;
                if (isFlagsByte && expected.Contains(fid) && hit.Add(fid)) continue;
                stray.Add(o);
            }
            Check(stray.Count == 0, $"every differing byte is a named group's header flags byte, bit 0x40 alone ({diffs.Count} byte(s) differ)");
            foreach (int o in stray.Take(10)) Console.WriteLine($"         stray offset 0x{o:X}: {before[o]:X2} -> {after[o]:X2}");
            Check(hit.SetEquals(expected), $"each changed group differs exactly once ({hit.Count} of {expected.Count})");

            using (var r = StarfieldMod.CreateFromBinaryOverlay(outPath, StarfieldRelease.Starfield, session.ReadParams))
            {
                var back = r.Quests.SelectMany(q => q.DialogTopics).SelectMany(t => t.Responses)
                    .Where(x => expected.Contains(x.FormKey.ID) && x.FormKey.ModKey == myMod.ModKey).ToList();
                Check(back.Count == expected.Count && back.All(x => x.MajorFlags.HasFlag(DialogResponses.MajorFlag.InfoGroup) == !clear),
                      $"read back off disk, every changed group's header is InfoGroup {(clear ? "clear" : "set")} ({back.Count} found)");
            }
            if (dryDir != null) Directory.Delete(dryDir, true);

            Console.WriteLine();
            Console.WriteLine(fails == 0 ? $"  infogroup: every check OK{(dry ? " (dry; run without --dry to write live)" : "")}." : $"  infogroup: {fails} check(s) FAILED.");
            return fails == 0 ? 0 : 1;
        }
    }
}
