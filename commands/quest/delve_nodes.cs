using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// `gen_delve nodes [node]`: READ-ONLY. Every Story Manager quest node under the same branch as `node` (default the
    /// board's Bounty pool), with how many quests it deals, how many of them are Delves (an EditorID naming a recipe
    /// here), and whether it has been split behind Classic Missions (a `_Legacy` sibling). Written 2026-10-26 when he
    /// asked "really we're all covered on the board?" and the only list of the board's categories was the eight I had
    /// touched, while the legacy verify printed 16 siblings: a claim about "every category" needs the branch, not a memory.
    /// </summary>
    internal static class DelveNodes
    {
        internal static int Run(string[] args, string dataDir, List<string> mods)
        {
            var pos = args.Skip(2).Where(a => !a.StartsWith("--")).ToList();
            string anchorName = pos.FirstOrDefault() ?? "duo_MB_MissionNode_Bounty";
            if (mods.Count != 1) { Console.WriteLine("REFUSED: the templates name " + mods.Count + " mods; nodes knows which plugin only when they agree."); return 1; }
            using var session = PluginSession.Open(mods[0]);
            if (session == null) return 1;
            var m = session.Mod;
            var anchor = m.StoryManagerQuestNodes.FirstOrDefault(n => string.Equals(n.EditorID, anchorName, StringComparison.OrdinalIgnoreCase));
            if (anchor == null) { Console.WriteLine($"REFUSED: no Story Manager quest node '{anchorName}' in {mods[0]}.esm."); return 1; }
            var recipes = new HashSet<string>(Directory.GetFiles(Path.Combine(dataDir, "recipes"), "*.json").Select(Path.GetFileNameWithoutExtension)!,
                                              StringComparer.OrdinalIgnoreCase);
            var questName = m.Quests.ToDictionary(q => q.FormKey, q => q.EditorID ?? "");
            var under = m.StoryManagerQuestNodes.Where(n => n.Parent.FormKey == anchor.Parent.FormKey).ToList();
            var branches = m.StoryManagerBranchNodes.Count(n => n.Parent.FormKey == anchor.Parent.FormKey);
            var names = new HashSet<string>(under.Select(n => n.EditorID ?? ""), StringComparer.OrdinalIgnoreCase);
            Console.WriteLine($"\n=== NODES under {anchor.Parent.FormKey} (the branch of {anchor.EditorID}): {under.Count} quest node(s), {branches} branch node(s) ===");
            Console.WriteLine($"  {"node",-48} {"quests",6} {"delves",6} {"orig",5}  state");
            foreach (var n in under.OrderBy(n => n.EditorID, StringComparer.OrdinalIgnoreCase))
            {
                var qs = n.Quests.Select(e => questName.TryGetValue(e.Quest.FormKey, out var id) ? id : e.Quest.FormKey.ToString()).ToList();
                int delves = qs.Count(recipes.Contains);
                string name = n.EditorID ?? n.FormKey.ToString();
                string state = name.EndsWith("_Legacy", StringComparison.OrdinalIgnoreCase) ? "legacy (behind Classic Missions)"
                             : name.EndsWith("_Rare", StringComparison.OrdinalIgnoreCase) ? "rare tier"
                             : names.Contains(name + "_Legacy") ? (qs.Count == 0 ? "SPLIT, but EMPTY: deals nothing with Classic Missions off" : qs.Count == delves ? "SPLIT: Delves only" : "split, but originals still in the pool")
                             : delves == 0 ? (qs.Count == 0 ? "empty" : "NOT SPLIT: originals only")
                             : delves == qs.Count ? "Delves only (no legacy sibling)"
                             : "NOT SPLIT: Delves mixed with originals";
                Console.WriteLine($"  {name,-48} {qs.Count,6} {delves,6} {qs.Count - delves,5}  {state}");
            }
            return 0;
        }
    }
}
