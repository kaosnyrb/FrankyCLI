using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// `gen_delve card &lt;delveId&gt;[,...] | --all [--dry]`: write a built board Delve's mission-board description (the
        /// card he reads before taking the job) from its recipe IN PLACE, with no rebuild. His 2026-11-10, after Jessica's
        /// `46e61d3` rewrote 105 cards as postings: "can we make a tool to just change that field".
        ///
        /// WHY NOT A REBUILD: a rebuild re-mints every record the build owns (his 2026-10-12: "Everytime you build the formid's
        /// increment"), regenerates the fragment and re-copies every line's audio, to change one string the player reads.
        ///
        /// THE TEXT is exactly what a build writes: Expand(CardOf(r), t), so a chain link gets its briefing as a build gives it.
        /// A card still carrying an unexpanded &lt;Token&gt; is refused (the template does not know it, and the board would show it raw).
        ///
        /// THE PROOF. reserve's same-size byte proof cannot hold (new text changes the size), so the proof is structural, off disk:
        /// (1) every major record in the written plugin Equals its live twin, except the named quests; (2) each named quest, with
        /// its description put back to the old text, Equals its old self, so the description is the only field that moved;
        /// (3) each named quest reads back with the recipe's card; (4) the file grew or shrank by exactly the strings' byte delta.
        /// Snapshot first (DU_Overtime `sync_plugin.py --snapshot`). After a real write each changed Delve's model is re-emitted.
        /// </summary>
        private static int Card(string[] args, string dataDir, List<Template> templates, bool dry)
        {
            bool all = args.Contains("--all");
            var pos = args.Skip(2).Where(a => !a.StartsWith("--")).ToList();
            if (all == (pos.Count == 1) || pos.Count > 1)
            { Console.WriteLine("REFUSED: gen_delve card <delveId>[,<delveId>...] [--dry]   or   gen_delve card --all [--dry]"); return 1; }
            var mods = templates.Select(t => t.mod).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (mods.Count != 1) { Console.WriteLine("REFUSED: the templates name " + mods.Count + " mods; card knows which plugin only when they agree."); return 1; }

            using var session = PluginSession.Open(mods[0]);
            if (session == null) return 1;
            var myMod = session.Mod;
            string livePath = session.PluginPath;

            var recipeIds = Directory.GetFiles(Path.Combine(dataDir, "recipes"), "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
            List<string> ids;
            if (all)
            {
                var built = myMod.Quests.Select(q => q.EditorID).Where(e => e != null).ToHashSet(StringComparer.OrdinalIgnoreCase);
                ids = recipeIds.Where(i => built.Contains(i)).OrderBy(i => i, StringComparer.OrdinalIgnoreCase).ToList();
            }
            else
            {
                ids = pos[0].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var notDelves = ids.Where(i => !recipeIds.Contains(i, StringComparer.OrdinalIgnoreCase)).ToList();
                if (notDelves.Count > 0) { Console.WriteLine("REFUSED: not a Delve (no recipe): " + string.Join(", ", notDelves) + ". Nothing written."); return 1; }
            }

            Console.WriteLine();
            Console.WriteLine($"=== CARD: {(all ? $"--all ({ids.Count} built Delve(s) with a recipe)" : string.Join(", ", ids))} ===");
            var rawToken = new Regex(@"<(?!Alias=)[A-Za-z][A-Za-z0-9]*>");
            var changes = new List<(Quest q, string? oldText, string newText)>();
            int notBoard = 0, same = 0;
            foreach (var id in ids)
            {
                var q = myMod.Quests.FirstOrDefault(x => string.Equals(x.EditorID, id, StringComparison.OrdinalIgnoreCase));
                if (q == null) { Console.WriteLine($"REFUSED: {id} is not built into {mods[0]}.esm. Nothing written."); return 1; }
                var r = LoadRecipe(id, dataDir, templates, out var t);
                if (r == null || t == null) { Console.WriteLine($"REFUSED: {id}'s recipe does not load (above). Nothing written."); return 1; }
                if (!IsBoard(t) && !IsBoardSpace(t))
                {
                    if (!all) { Console.WriteLine($"REFUSED: {id} is a '{t.id}' Delve, not a board one; it has no card. Nothing written."); return 1; }
                    notBoard++; continue;
                }
                string newText = Expand(CardOf(r), t);
                var raw = rawToken.Matches(newText).Select(m => m.Value).Distinct().ToList();
                if (raw.Count > 0) { Console.WriteLine($"REFUSED: {id}'s card keeps {string.Join(", ", raw)} unexpanded; the board would show it raw. Nothing written."); return 1; }
                string? oldText = q.MissionBoardDescription?.String;
                if (string.Equals(oldText, newText, StringComparison.Ordinal)) { same++; continue; }
                Console.WriteLine($"  {q.FormKey.ID:X6} {id}");
                Console.WriteLine($"      was: {oldText ?? "(none)"}");
                Console.WriteLine($"      now: {newText}");
                changes.Add((q, oldText, newText));
            }
            Console.WriteLine($"\n  {changes.Count} to change, {same} already the recipe's{(all ? $", {notBoard} not on a board (skipped)" : "")}.");
            if (changes.Count == 0) { Console.WriteLine("  Nothing to change. Nothing written."); return 0; }

            foreach (var c in changes) c.q.MissionBoardDescription = c.newText;

            byte[] before = File.ReadAllBytes(livePath);
            string? dryDir = dry ? Path.Combine(Path.GetTempPath(), $"frankycli_card_{DateTime.Now:yyyyMMdd_HHmmss}") : null;
            if (dryDir != null) Directory.CreateDirectory(dryDir);
            string outPath = dryDir != null ? Path.Combine(dryDir, Path.GetFileName(livePath)) : livePath;
            // The copy keeps the plugin's own FILE NAME: a mod's FormKeys take their ModKey from it, so a copy under another
            // name shares no key with the live plugin and every comparison below compares nothing (the first version did that).
            string beforeDir = Path.Combine(Path.GetTempPath(), $"frankycli_card_before_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(beforeDir);
            string beforePath = Path.Combine(beforeDir, Path.GetFileName(livePath));
            File.WriteAllBytes(beforePath, before);
            session.Close();
            session.Write(outPath);
            long afterLen = new FileInfo(outPath).Length;
            Console.WriteLine($"  wrote {outPath}{(dry ? "  (--dry: the live plugin is untouched)" : "")}");

            int fails = 0;
            void Ck(bool ok, string what) { Console.WriteLine($"  [{(ok ? "OK  " : "FAIL")}] {what}"); if (!ok) fails++; }

            var changedKeys = changes.Select(c => c.q.FormKey).ToHashSet();
            using (var b = StarfieldMod.CreateFromBinaryOverlay(beforePath, StarfieldRelease.Starfield, session.ReadParams))
            using (var a = StarfieldMod.CreateFromBinaryOverlay(outPath, StarfieldRelease.Starfield, session.ReadParams))
            {
                var bRecs = b.EnumerateMajorRecords().ToDictionary(x => x.FormKey);
                var aRecs = a.EnumerateMajorRecords().ToDictionary(x => x.FormKey);
                Ck(bRecs.Count == aRecs.Count && bRecs.Keys.All(aRecs.ContainsKey), $"the same {bRecs.Count:N0} records before and after ({aRecs.Count:N0} after)");
                var moved = bRecs.Where(kv => aRecs.TryGetValue(kv.Key, out var ar) && !kv.Value.Equals(ar)).Select(kv => kv.Key).ToList();
                var stray = moved.Where(k => !changedKeys.Contains(k)).ToList();
                Ck(stray.Count == 0, $"no record moved but the named quests ({moved.Count} differ, {stray.Count} of them not named)");
                foreach (var k in stray.Take(10)) Console.WriteLine($"         stray: {k} {bRecs[k].EditorID}");
                Ck(moved.Count == changes.Count, $"every named quest moved ({moved.Count} of {changes.Count})");

                long delta = 0;
                foreach (var c in changes)
                {
                    var aq = a.Quests.First(x => x.FormKey == c.q.FormKey);
                    var bq = b.Quests.First(x => x.FormKey == c.q.FormKey);
                    Ck(aq.MissionBoardDescription?.String == c.newText, $"read back off disk, {c.q.EditorID}'s card is the recipe's");
                    var put = aq.DeepCopy();
                    put.MissionBoardDescription = c.oldText;
                    Ck(put.Equals(bq), $"{c.q.EditorID} with its old card put back is its old self (the card is the only field that moved)");
                    delta += Encoding.UTF8.GetByteCount(c.newText) - Encoding.UTF8.GetByteCount(c.oldText ?? "");
                }
                Ck(afterLen - before.Length == delta, $"the plugin moved by the strings' byte delta ({before.Length:N0} -> {afterLen:N0}, {afterLen - before.Length:+#;-#;0} against {delta:+#;-#;0})");
            }
            Directory.Delete(beforeDir, true);
            if (dryDir != null) Directory.Delete(dryDir, true);

            if (!dry && fails == 0)
                foreach (var c in changes)
                {
                    var model = gen_questmodel.ReadModel(c.q, out var refusals);
                    if (refusals.Count > 0) { foreach (var x in refusals) Console.WriteLine($"  [FAIL] {c.q.EditorID}'s model cannot hold: {x}"); fails++; continue; }
                    File.WriteAllText(Path.Combine(dataDir, "models", c.q.EditorID + ".json"), gen_questmodel.ToJson(model));
                }
            if (!dry && fails == 0) Console.WriteLine($"  model    : {changes.Count} re-emitted from the edited quests");

            Console.WriteLine();
            Console.WriteLine(fails == 0 ? $"  card: every check OK{(dry ? " (dry; run without --dry to write live)" : "")}." : $"  card: {fails} check(s) FAILED.");
            return fails == 0 ? 0 : 1;
        }
    }
}
