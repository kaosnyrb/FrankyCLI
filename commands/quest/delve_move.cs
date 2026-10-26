using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// `gen_delve move &lt;delveId&gt; &lt;fromNode&gt; &lt;toNode&gt; [--dry]`: a built Delve changes pool by editing the two node
    /// lists, never by a rebuild (his ruling 2026-10-12: "think it's a bit wasteful to rebuilt all the quests just to put
    /// them in a different list. Everytime you build the formid's increment"). First use 2026-10-20: missionboard01a off
    /// the board's test node into duo_MB_MissionNode_Bounty, so the Bounty category can be split behind Classic Missions.
    /// Then set the recipe's "deal" to the new node, JSON only, so the next rebuild agrees with the plugin.
    ///
    /// FROM "-" = from no pool (2026-10-26, his: five built Delves started by hand and dealt by nothing). The entry is
    /// written as the build writes one (gen_delve.cs, `new StoryManagerQuest { Quest = ... }`, nothing else), and it is
    /// refused if the Delve is already in ANY pool, because then "from nowhere" is false and the real move names its pool.
    /// </summary>
    internal static class DelveMove
    {
        internal static int Run(string[] args, string dataDir, List<string> mods, bool dry)
        {
            var pos = args.Skip(2).Where(a => !a.StartsWith("--")).ToList();
            if (pos.Count != 3) { Console.WriteLine("REFUSED: gen_delve move <delveId> <fromNode> <toNode> [--dry]"); return 1; }
            string id = pos[0], fromName = pos[1], toName = pos[2];
            if (mods.Count != 1) { Console.WriteLine("REFUSED: the templates name " + mods.Count + " mods; move knows which plugin only when they agree."); return 1; }
            if (!File.Exists(Path.Combine(dataDir, "recipes", id + ".json"))) { Console.WriteLine($"REFUSED: {id} is not a Delve (no recipe)."); return 1; }

            using var session = PluginSession.Open(mods[0]);
            if (session == null) return 1;
            var myMod = session.Mod;
            var mastersBefore = string.Join(", ", myMod.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));
            StoryManagerQuestNode? Node(string n) => myMod.StoryManagerQuestNodes.FirstOrDefault(x => string.Equals(x.EditorID, n, StringComparison.OrdinalIgnoreCase));
            bool fromNowhere = fromName == "-";
            var from = fromNowhere ? null : Node(fromName); var to = Node(toName);
            if ((!fromNowhere && from == null) || to == null) { Console.WriteLine($"REFUSED: no Story Manager quest node '{(to == null ? toName : fromName)}' in {mods[0]}.esm."); return 1; }
            if (from != null && from.FormKey == to.FormKey) { Console.WriteLine("REFUSED: the two nodes are the same node."); return 1; }
            var quest = myMod.Quests.FirstOrDefault(q => string.Equals(q.EditorID, id, StringComparison.OrdinalIgnoreCase));
            if (quest == null) { Console.WriteLine($"REFUSED: {id} is not built into the plugin."); return 1; }
            var pools = myMod.StoryManagerQuestNodes.Where(n => n.Quests.Any(e => e.Quest.FormKey == quest.FormKey)).Select(n => n.EditorID).ToList();
            if (fromNowhere && pools.Count > 0) { Console.WriteLine($"REFUSED: {id} is already in {string.Join(", ", pools)}; name that pool as <fromNode>. Nothing written."); return 1; }
            var entry = fromNowhere ? new StoryManagerQuest { Quest = quest.ToNullableLink<IQuestGetter>() }
                                    : from!.Quests.FirstOrDefault(e => e.Quest.FormKey == quest.FormKey);
            if (entry == null) { Console.WriteLine($"REFUSED: {id} is not in {from!.EditorID}. Nothing written."); return 1; }
            if (to.Quests.Any(e => e.Quest.FormKey == quest.FormKey)) { Console.WriteLine($"REFUSED: {id} is already in {to.EditorID}. Nothing written."); return 1; }
            int otherPools = from == null ? 0 : pools.Count - 1;
            string fromLabel = from?.EditorID ?? "(no pool)";

            Console.WriteLine();
            Console.WriteLine($"=== MOVE {id}: {fromLabel} -> {to.EditorID} ===");
            if (otherPools > 0) Console.WriteLine($"  ⚠ {id} is also in {otherPools} other node(s); only this entry moves.");
            from?.Quests.Remove(entry);
            to.Quests.Add(fromNowhere ? entry : entry.DeepCopy());
            if (dry) { Console.WriteLine("\n  --dry: nothing written."); return 0; }
            int fromCount = from?.Quests.Count ?? 0, toCount = to.Quests.Count;
            session.Close();
            session.Write();
            Console.WriteLine("  wrote " + session.PluginPath);

            using var r = StarfieldMod.CreateFromBinaryOverlay(session.PluginPath, StarfieldRelease.Starfield, session.ReadParams);
            var f2 = from == null ? null : r.StoryManagerQuestNodes.First(n => n.FormKey == from.FormKey);
            var t2 = r.StoryManagerQuestNodes.First(n => n.FormKey == to.FormKey);
            int poolsAfter = r.StoryManagerQuestNodes.Count(n => n.Quests.Any(e => e.Quest.FormKey == quest.FormKey));
            bool ok1 = (f2 == null ? poolsAfter == 1 : !f2.Quests.Any(e => e.Quest.FormKey == quest.FormKey)) && t2.Quests.Count(e => e.Quest.FormKey == quest.FormKey) == 1;
            bool ok2 = (f2 == null || f2.Quests.Count == fromCount) && t2.Quests.Count == toCount;
            var mastersAfter = string.Join(", ", r.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));
            Console.WriteLine($"  [{(ok1 ? "OK  " : "FAIL")}] off disk: {id} is in {t2.EditorID} once and not in {f2?.EditorID ?? "any other pool"}");
            Console.WriteLine($"  [{(ok2 ? "OK  " : "FAIL")}] nothing else moved ({(f2 == null ? "" : f2.EditorID + " " + f2.Quests.Count + ", ")}{t2.EditorID} {t2.Quests.Count})");
            Console.WriteLine($"  [{(mastersAfter == mastersBefore ? "OK  " : "FAIL")}] the plugin's masters are unchanged ({mastersAfter})");
            Console.WriteLine($"  Now set {id}'s recipe \"deal\" to \"{t2.EditorID}\" (JSON only).");
            return ok1 && ok2 && mastersAfter == mastersBefore ? 0 : 1;
        }
    }
}
