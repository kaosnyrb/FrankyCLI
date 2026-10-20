using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// THE RARE TIER (his ask, 2026-10-20: "the picnic quest should happen quite rarely cus its unique", then "we'll make
    /// other rare delves too"). A pool gets a sibling, &lt;pool&gt;_Rare, placed directly BEFORE the pool (so after its
    /// _Legacy node when there is one), gated on the pool's own conditions plus GetRandomPercent &lt; duo_RareDelveChance.
    /// A named Delve moves from the pool into it; a Delve built later with "deal": "&lt;pool&gt;_Rare" lands there directly.
    ///
    /// ⚠ THE CHANCE IS PER EVENT, NOT PER QUEST. It is how often a slate TRIES the rare node at all, so with one member it
    /// is that member's own rate: at 25 (the original low-chance pool's figure) the picnic would be dealt MORE often than
    /// it was in a 16-Delve pool (about 1 in 16). The default is 3, rarer than before in both Classic Missions settings;
    /// raise it as rare Delves are added. One global, set from the console.
    ///
    /// ⚠ ORDER, and why before the pool: a node after the pool is reached only when the pool starts nothing (no
    /// SharesEvent), which is likely why Overtime's original _Local_LowChance_Node, placed last, rarely or never deals.
    /// Not gated on Classic Missions: rare Delves are new content, dealt whatever the setting.
    /// </summary>
    internal static class DelveRare
    {
        internal const string ChanceGlobal = "duo_RareDelveChance";
        internal const float DefaultChance = 3f;
        const string RareSuffix = "_Rare";

        /// <param name="mods">the templates' plugin names, distinct</param>
        internal static int Run(string[] args, string dataDir, List<string> mods, bool dry)
        {
            var pos = args.Skip(2).Where(a => !a.StartsWith("--")).ToList();
            if (pos.Count != 2) { Console.WriteLine("REFUSED: gen_delve rare <pool node> <delveId>[,<delveId>...] [--dry]"); return 1; }
            string nodeName = pos[0];
            var ids = pos[1].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (mods.Count != 1) { Console.WriteLine("REFUSED: the templates name " + mods.Count + " mods; rare knows which plugin only when they agree."); return 1; }
            var recipeIds = Directory.GetFiles(Path.Combine(dataDir, "recipes"), "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var notDelves = ids.Where(i => !recipeIds.Contains(i)).ToList();
            if (notDelves.Count > 0) { Console.WriteLine("REFUSED: not a Delve (no recipe): " + string.Join(", ", notDelves) + ". Nothing written."); return 1; }

            using var session = PluginSession.Open(mods[0]);
            if (session == null) return 1;
            var myMod = session.Mod;
            var mastersBefore = string.Join(", ", myMod.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));

            var pool = myMod.StoryManagerQuestNodes.FirstOrDefault(n => string.Equals(n.EditorID, nodeName, StringComparison.OrdinalIgnoreCase));
            if (pool == null) { Console.WriteLine($"REFUSED: no Story Manager quest node '{nodeName}' in {mods[0]}.esm."); return 1; }
            if (pool.EditorID!.EndsWith(RareSuffix, StringComparison.OrdinalIgnoreCase) || pool.EditorID.EndsWith(DelveLegacy.LegacySuffix, StringComparison.OrdinalIgnoreCase))
            { Console.WriteLine("REFUSED: name the pool itself, not one of its tiers."); return 1; }
            if (pool.Parent.IsNull) { Console.WriteLine($"REFUSED: {pool.EditorID} has no parent branch."); return 1; }
            string rareName = pool.EditorID + RareSuffix;

            var byName = myMod.Quests.Where(q => q.EditorID != null).ToDictionary(q => q.EditorID!, q => q.FormKey, StringComparer.OrdinalIgnoreCase);
            var missing = ids.Where(i => !byName.ContainsKey(i)).ToList();
            if (missing.Count > 0) { Console.WriteLine("REFUSED: not built into the plugin: " + string.Join(", ", missing) + ". Build it with \"deal\": \"" + rareName + "\" instead. Nothing written."); return 1; }
            var notInPool = ids.Where(i => !pool.Quests.Any(e => e.Quest.FormKey == byName[i])).ToList();
            if (notInPool.Count > 0) { Console.WriteLine($"REFUSED: not in {pool.EditorID}: " + string.Join(", ", notInPool) + ". Nothing written."); return 1; }

            Console.WriteLine();
            Console.WriteLine($"=== RARE {pool.EditorID}: {string.Join(", ", ids)} ===");

            // The chance global, created once and shared by every rare node.
            var chance = myMod.Globals.FirstOrDefault(g => g.EditorID == ChanceGlobal);
            if (chance == null)
            {
                var shape = myMod.Globals.FirstOrDefault(g => g.EditorID == DelveLegacy.GlobalShape);
                if (shape == null) { Console.WriteLine($"REFUSED: {DelveLegacy.GlobalShape} (the global shape) is not in the plugin."); return 1; }
                chance = (Global)shape.Duplicate(myMod.GetNextFormKey());
                chance.EditorID = ChanceGlobal;
                chance.Data = DefaultChance;
                chance.MajorFlags &= ~Global.MajorFlag.Constant;   // the console must be able to set it (see DelveLegacy.GlobalShape)
                myMod.Globals.Add(chance);
                Console.WriteLine($"  created {ChanceGlobal} = {DefaultChance}");
            }
            else if (chance.MajorFlags.HasFlag(Global.MajorFlag.Constant))
            { Console.WriteLine($"REFUSED: {ChanceGlobal} is flagged Constant. Nothing written."); return 1; }

            // The rare node, created once per pool: the pool's own record cloned (parent, flags, conditions), emptied,
            // gated on the chance, and put directly before the pool in the sibling chain.
            var rare = myMod.StoryManagerQuestNodes.FirstOrDefault(n => string.Equals(n.EditorID, rareName, StringComparison.OrdinalIgnoreCase));
            FormKey? before = null;
            bool created = false;
            if (rare == null)
            {
                rare = (StoryManagerQuestNode)pool.Duplicate(myMod.GetNextFormKey());
                rare.EditorID = rareName;
                rare.Quests.Clear();
                var gate = new ConditionGlobal { CompareOperator = CompareOperator.LessThan, Data = new GetRandomPercentConditionData() };
                gate.ComparisonValue.SetTo(chance.FormKey);
                rare.Conditions.Add(gate);
                before = pool.PreviousSibling.FormKeyNullable;
                rare.PreviousSibling.SetTo(before);
                pool.PreviousSibling.SetTo(rare.FormKey);
                myMod.StoryManagerQuestNodes.Add(rare);
                created = true;
                Console.WriteLine($"  created {rareName}, directly before {pool.EditorID}, gated GetRandomPercent < {ChanceGlobal}");
            }
            else
            {
                if (pool.PreviousSibling.FormKey != rare.FormKey) { Console.WriteLine($"REFUSED: {rareName} exists but is not directly before {pool.EditorID}. Nothing written."); return 1; }
                before = rare.PreviousSibling.FormKeyNullable;
                Console.WriteLine($"  {rareName} exists; reused");
            }

            foreach (var id in ids)
            {
                var e = pool.Quests.First(x => x.Quest.FormKey == byName[id]);
                pool.Quests.Remove(e);
                rare.Quests.Add(e.DeepCopy());
                Console.WriteLine($"  moves: {id}  {pool.EditorID} -> {rareName}");
            }
            if (dry) { Console.WriteLine("\n  --dry: nothing written."); return 0; }

            int poolBefore = pool.Quests.Count, rareBefore = rare.Quests.Count;
            session.Close();
            session.Write();
            Console.WriteLine("  wrote " + session.PluginPath);

            using var r = StarfieldMod.CreateFromBinaryOverlay(session.PluginPath, StarfieldRelease.Starfield, session.ReadParams);
            int fails = 0;
            void Check(bool ok, string what) { Console.WriteLine($"  [{(ok ? "OK  " : "FAIL")}] {what}"); if (!ok) fails++; }
            var p2 = r.StoryManagerQuestNodes.FirstOrDefault(n => n.FormKey == pool.FormKey);
            var r2 = r.StoryManagerQuestNodes.FirstOrDefault(n => n.FormKey == rare.FormKey);
            var c2 = r.Globals.FirstOrDefault(g => g.EditorID == ChanceGlobal);
            Check(p2 != null && r2 != null && c2 != null, "the pool, its rare node and the chance global are in the plugin off disk");
            if (p2 == null || r2 == null || c2 == null) return 1;
            var moved = ids.Select(i => byName[i]).ToList();
            Check(moved.All(k => r2.Quests.Count(e => e.Quest.FormKey == k) == 1), $"{rareName} holds each moved Delve exactly once ({r2.Quests.Count} member(s))");
            Check(!moved.Any(k => p2.Quests.Any(e => e.Quest.FormKey == k)), $"{p2.EditorID} no longer holds them ({p2.Quests.Count} member(s))");
            Check(p2.Quests.Count == poolBefore && r2.Quests.Count == rareBefore, "nothing else in either node moved");
            Check(r2.Parent.FormKey == p2.Parent.FormKey, "the rare node shares the pool's branch");
            Check(p2.PreviousSibling.FormKey == r2.FormKey && r2.PreviousSibling.FormKeyNullable == before, "the rare node sits directly before the pool, in the place the pool had");
            Check(r2.Conditions.Any(c => c.Data is IGetRandomPercentConditionDataGetter && c is IConditionGlobalGetter g
                                         && g.ComparisonValue.FormKey == c2.FormKey && c.CompareOperator == CompareOperator.LessThan),
                  $"the rare node is gated GetRandomPercent < {ChanceGlobal}");
            Check(!c2.MajorFlags.HasFlag(Global.MajorFlag.Constant), $"{ChanceGlobal} is not Constant (now {c2.Data})");
            var mastersAfter = string.Join(", ", r.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));
            Check(mastersAfter == mastersBefore, $"the plugin's masters are unchanged ({mastersAfter})");
            Console.WriteLine();
            Console.WriteLine(fails == 0 ? "  rare: every check OK." : $"  rare: {fails} check(s) FAILED.");
            Console.WriteLine($"  Now set each moved recipe's \"deal\" to \"{rareName}\" (JSON only), so the next rebuild agrees with the plugin.");
            if (created) Console.WriteLine($"  The chance is per EVENT: with {r2.Quests.Count} member(s), {c2.Data}% of {p2.EditorID}'s slates try the rare node.");
            return fails == 0 ? 0 : 1;
        }
    }
}
