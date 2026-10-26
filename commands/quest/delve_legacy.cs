using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda.Strings;
using Noggog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// THE LEGACY MISSION SYSTEM (his ask, 2026-10-20; his shape 2026-10-17): the original Overtime quests stay in the
    /// plugin, untouched, and move behind a gameplay setting, Classic Missions, OFF by default. OFF deals the Delves only;
    /// ON mixes the old quests back in. Never an old-only mode.
    ///
    /// WHY A NODE AND NOT THE QUEST: a Story Manager quest entry is Quest + HoursUntilReset and nothing else, and a
    /// QUST carries no event conditions, so nothing can gate one member of a pool. So each pool gets a sibling,
    /// &lt;pool&gt;_Legacy, placed FIRST under the same branch, holding every quest of the pool that is not a Delve, and
    /// conditioned on the pool's own conditions plus GetGlobalValue(duo_ClassicMissions) == 1 and
    /// GetRandomPercent &lt; duo_ClassicMissionsShare. A node whose conditions fail is skipped, which is how every
    /// Overtime branch already routes; when it passes and starts a quest the event is spent (no SharesEvent), so ON
    /// deals an old quest on that share of events and a Delve on the rest.
    ///
    /// WHY A GLOBAL AT ALL: a node condition cannot read a gameplay option, so duo_GameplayOptionsQuest (start game
    /// enabled) runs papyrus/duo_gameplayoptions.psc, which copies the option into duo_ClassicMissions. That is vanilla's
    /// own shape (PEO:SQ_PEO_QuestScript) and his Suit Power Cores' (avpc_poichance into atpc_poispawnchance); the
    /// option, group and quest records here are written field for field in the shape of his shipped ones.
    ///
    /// A DELVE is a quest whose EditorID names a recipe in data/delves/recipes. Nothing else: since 2026-10-26 the Delves
    /// carry the classic names (duo_MB40a_qst, duo_artifact_local_qst20a; his ruling, families from 40 and 20), so no
    /// name tells a Delve from an original. The second, by-name test this used to cross-check against went with it.
    ///
    /// Edits the .esm only, as testnode does: the .esp twin carries its own copy of every node.
    /// </summary>
    internal static class DelveLegacy
    {
        internal const string OnGlobal = "duo_ClassicMissions";
        internal const string ShareGlobal = "duo_ClassicMissionsShare";
        internal const string OptionId = "duo_GPO_ClassicMissions";
        internal const string GroupId = "duo_GPO_Group";
        internal const string QuestId = "duo_GameplayOptionsQuest";
        internal const string Script = "duo_gameplayoptions";
        internal const float DefaultShare = 50f;
        internal const string LegacySuffix = "_Legacy";
        // The Overtime global the two new ones are cloned from, so their record shape is one the plugin already ships.
        // ⛔ IT IS FLAGGED CONSTANT, and the first build carried the flag into both clones (2026-10-20, his play:
        // "the setting switch doesn't change the global"): SetValue cannot change a constant global, so the option
        // never reached the node. His working Suit Power Cores global carries no flags. A clone takes every field of
        // its source, including the ones nobody chose; this one is cleared on the clone and asserted off disk.
        internal const string GlobalShape = "duo_Missionchance_Local_Lowchance";
        // His Suit Power Cores' quest flags, read off atpc_gameplayquest: StartGameEnabled, StartsEnabled, RunOnce.
        const int QuestFlagsRaw = 0x111;

        /// <param name="mods">the templates' plugin names, distinct; the verb knows which plugin only when they agree</param>
        internal static int Run(string[] args, string dataDir, List<string> mods, bool dry)
        {
            var pos = args.Skip(2).Where(a => !a.StartsWith("--")).ToList();
            if (pos.Count != 1) { Console.WriteLine("REFUSED: gen_delve legacy <pool node> [--dry]"); return 1; }
            string nodeName = pos[0];
            if (mods.Count != 1) { Console.WriteLine("REFUSED: the templates name " + mods.Count + " mods; legacy knows which plugin only when they agree."); return 1; }

            var recipeIds = Directory.GetFiles(Path.Combine(dataDir, "recipes"), "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            using var session = PluginSession.Open(mods[0]);
            if (session == null) return 1;
            var myMod = session.Mod;
            var mastersBefore = string.Join(", ", myMod.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));

            var real = myMod.StoryManagerQuestNodes.FirstOrDefault(n => string.Equals(n.EditorID, nodeName, StringComparison.OrdinalIgnoreCase));
            if (real == null) { Console.WriteLine($"REFUSED: no Story Manager quest node '{nodeName}' in {mods[0]}.esm."); return 1; }
            if (real.EditorID!.EndsWith(LegacySuffix, StringComparison.OrdinalIgnoreCase)) { Console.WriteLine("REFUSED: that is a legacy node already."); return 1; }
            string legacyName = real.EditorID + LegacySuffix;
            if (myMod.StoryManagerQuestNodes.Any(n => string.Equals(n.EditorID, legacyName, StringComparison.OrdinalIgnoreCase)))
            { Console.WriteLine($"REFUSED: {legacyName} already exists; {real.EditorID} is split. Nothing written."); return 1; }
            if (real.Parent.IsNull) { Console.WriteLine($"REFUSED: {real.EditorID} has no parent branch."); return 1; }

            var questName = myMod.Quests.ToDictionary(q => q.FormKey, q => q.EditorID ?? "");
            string NameOf(FormKey k) => questName.TryGetValue(k, out var n) ? n : k.ToString();

            // Sort the pool: a member is a Delve when a recipe carries its name.
            var delves = new List<StoryManagerQuest>();
            var old = new List<StoryManagerQuest>();
            foreach (var e in real.Quests)
                (recipeIds.Contains(NameOf(e.Quest.FormKey)) ? delves : old).Add(e);
            if (old.Count == 0) { Console.WriteLine($"REFUSED: {real.EditorID} holds no quest that is not a Delve; there is nothing to move."); return 1; }

            Console.WriteLine();
            Console.WriteLine($"=== LEGACY {real.EditorID} ===");
            Console.WriteLine($"  pool holds {real.Quests.Count}: {delves.Count} Delve(s) stay, {old.Count} original quest(s) move to {legacyName}");

            // The shared setting: all five records or none of them.
            var have = new[]
            {
                myMod.Globals.Any(g => g.EditorID == OnGlobal), myMod.Globals.Any(g => g.EditorID == ShareGlobal),
                myMod.GameplayOptions.Any(o => o.EditorID == OptionId), myMod.GameplayOptionsGroups.Any(g => g.EditorID == GroupId),
                myMod.Quests.Any(q => q.EditorID == QuestId),
            };
            if (have.Any(h => h) && !have.All(h => h))
            { Console.WriteLine($"REFUSED: the Classic Missions setting is half-built ({have.Count(h => h)} of 5 records present). Nothing written."); return 1; }
            if (!have[0]) { if (!CreateSetting(myMod)) return 1; }
            else
            {
                var fixedG = myMod.Globals.Where(g => (g.EditorID == OnGlobal || g.EditorID == ShareGlobal) && g.MajorFlags.HasFlag(Global.MajorFlag.Constant)).ToList();
                if (fixedG.Count > 0) { Console.WriteLine("REFUSED: the setting exists but " + string.Join(", ", fixedG.Select(g => g.EditorID)) + " is flagged Constant, so the option cannot set it. Nothing written."); return 1; }
                Console.WriteLine("  the Classic Missions setting exists; reused");
            }

            var onG = myMod.Globals.First(g => g.EditorID == OnGlobal);
            var shareG = myMod.Globals.First(g => g.EditorID == ShareGlobal);

            // The legacy node: the pool's own record cloned, so it keeps the pool's parent, flags and conditions.
            var legacy = (StoryManagerQuestNode)real.Duplicate(myMod.GetNextFormKey());
            legacy.EditorID = legacyName;
            legacy.Quests.Clear();
            foreach (var e in old) legacy.Quests.Add(e.DeepCopy());
            foreach (var e in old) real.Quests.Remove(e);

            var onData = new GetGlobalValueConditionData();
            onData.FirstParameter = new FormLinkOrIndex<IGlobalGetter>(onData, onG.FormKey);
            legacy.Conditions.Add(new ConditionFloat { CompareOperator = CompareOperator.EqualTo, ComparisonValue = 1f, Data = onData });
            // ⚠ A BOARD node shares the event (SharesEvent): the refresh walks on after it deals, so the legacy node and
            // the pool BOTH deal on one refresh and the mix happens by itself. A share gate there would only take the
            // originals off the board on that share of refreshes, so a shared-event pool's legacy node gets none
            // (2026-10-20, the Bounty category; his to change). A slate pool stops at the first node that deals, so
            // there the share gate IS the mix.
            bool shares = real.QuestFlags.HasFlag(StoryManagerQuestNode.QuestFlag.SharesEvent);
            if (!shares)
            {
                var share = new ConditionGlobal { CompareOperator = CompareOperator.LessThan, Data = new GetRandomPercentConditionData() };
                share.ComparisonValue.SetTo(shareG.FormKey);
                legacy.Conditions.Add(share);
            }

            // First under the branch: it takes the pool's place in the sibling chain and the pool follows it.
            var placeBefore = real.PreviousSibling.FormKeyNullable;
            legacy.PreviousSibling.SetTo(placeBefore);
            real.PreviousSibling.SetTo(legacy.FormKey);
            myMod.StoryManagerQuestNodes.Add(legacy);
            Console.WriteLine(shares
                ? $"  {legacyName}: before the pool, gated on {OnGlobal} == 1; the pool shares the event (a board), so both deal and no share gate"
                : $"  {legacyName}: first under its branch, gated on {OnGlobal} == 1 and GetRandomPercent < {ShareGlobal}");
            foreach (var e in old) Console.WriteLine("    moves: " + NameOf(e.Quest.FormKey));

            if (dry) { Console.WriteLine("\n  --dry: nothing written."); return 0; }

            session.Close();
            session.Write();
            Console.WriteLine("  wrote " + session.PluginPath);
            return Verify(session, real.FormKey, legacy.FormKey, placeBefore, delves.Select(e => e.Quest.FormKey).ToList(),
                          old.Select(e => e.Quest.FormKey).ToList(), mastersBefore);
        }

        /// <summary>The two globals, the option, its group and the quest that mirrors one into the other.</summary>
        static bool CreateSetting(StarfieldMod myMod)
        {
            var shape = myMod.Globals.FirstOrDefault(g => g.EditorID == GlobalShape);
            if (shape == null) { Console.WriteLine($"REFUSED: {GlobalShape} (the global the new ones are shaped on) is not in the plugin."); return false; }
            var vmaShape = myMod.Quests.Select(q => q.VirtualMachineAdapter).FirstOrDefault(v => v != null && v.Scripts.Count > 0);
            if (vmaShape == null) { Console.WriteLine("REFUSED: no quest in the plugin carries a script to copy the VM format from."); return false; }

            Global NewGlobal(string id, float value)
            {
                var g = (Global)shape.Duplicate(myMod.GetNextFormKey());
                g.EditorID = id;
                g.Data = value;
                g.MajorFlags &= ~Global.MajorFlag.Constant;   // the script SETS it, and the console must be able to
                myMod.Globals.Add(g);
                return g;
            }
            var onG = NewGlobal(OnGlobal, 0f);
            NewGlobal(ShareGlobal, DefaultShare);

            // The player-facing words are placeholders for Jessica's pen; the numbers are the shape of vanilla's
            // two-step options (PEO_GPO_AmmoWeight: Float 0..1 step 1, one reward per step, RewardValue the XP modifier).
            var opt = new GameplayOption(myMod)
            {
                EditorID = OptionId,
                Name = "Classic Missions",
                Description = "Mixes the original Overtime missions in with the new ones.",
                Data = new FloatGameplayOptionData { Value1 = 0f, Value2 = 0f, Min = 0f, Max = 1f, Step = 1f },
            };
            opt.Rewards.Add(new GameplayOptionReward { SettingValue = 0f, RewardValue = 0f, Name = "Off", Description = "Only the new missions are dealt." });
            opt.Rewards.Add(new GameplayOptionReward { SettingValue = 1f, RewardValue = 0f, Name = "On", Description = "The original missions are dealt alongside the new ones." });
            myMod.GameplayOptions.Add(opt);

            var leaf = new GameplayOptionsGroupLeaf();
            leaf.GameplayOptions.Add(opt.ToLink<IGameplayOptionGetter>());
            var group = new GameplayOptionsGroup(myMod) { EditorID = GroupId, Name = "Dark Universe: Overtime", Options = leaf };
            myMod.GameplayOptionsGroups.Add(group);

            var quest = new Quest(myMod) { EditorID = QuestId, Data = new QuestData { Flags = (Quest.Flag)QuestFlagsRaw, Priority = 0 } };
            quest.Stages.Add(new QuestStage { Index = 0 });
            quest.Stages.Add(new QuestStage { Index = 10 });
            var vma = new QuestAdapter { Version = vmaShape.Version, ObjectFormat = vmaShape.ObjectFormat };
            var sc = new ScriptEntry { Name = Script };
            var po = new ScriptObjectProperty { Name = "ClassicMissionsOption", Flags = ScriptProperty.Flag.Edited };
            po.Object.SetTo(opt.FormKey);
            var pg = new ScriptObjectProperty { Name = "ClassicMissionsGlobal", Flags = ScriptProperty.Flag.Edited };
            pg.Object.SetTo(onG.FormKey);
            sc.Properties.Add(po);
            sc.Properties.Add(pg);
            vma.Scripts.Add(sc);
            quest.VirtualMachineAdapter = vma;
            myMod.Quests.Add(quest);

            Console.WriteLine($"  created the Classic Missions setting: {OnGlobal} = 0, {ShareGlobal} = {DefaultShare}, {OptionId} (Off/On, default Off) in {GroupId}, {QuestId} running {Script}");
            return true;
        }

        /// <summary>Everything off disk: the split, the chain, the gates, the setting, the masters.</summary>
        static int Verify(PluginSession session, FormKey realKey, FormKey legacyKey, FormKey? placeBefore, List<FormKey> delves, List<FormKey> old, string mastersBefore)
        {
            using var r = StarfieldMod.CreateFromBinaryOverlay(session.PluginPath, StarfieldRelease.Starfield, session.ReadParams);
            int fails = 0;
            void Check(bool ok, string what) { Console.WriteLine($"  [{(ok ? "OK  " : "FAIL")}] {what}"); if (!ok) fails++; }

            var real = r.StoryManagerQuestNodes.FirstOrDefault(n => n.FormKey == realKey);
            var legacy = r.StoryManagerQuestNodes.FirstOrDefault(n => n.FormKey == legacyKey);
            Check(real != null && legacy != null, "both nodes are in the plugin off disk");
            if (real == null || legacy == null) return 1;
            var inReal = real.Quests.Select(e => e.Quest.FormKey).ToList();
            var inLegacy = legacy.Quests.Select(e => e.Quest.FormKey).ToList();
            Check(inReal.Count == delves.Count && delves.All(inReal.Contains), $"{real.EditorID} holds exactly its {delves.Count} Delve(s)");
            Check(inLegacy.Count == old.Count && old.All(inLegacy.Contains), $"{legacy.EditorID} holds exactly the {old.Count} original quest(s)");
            Check(!inReal.Intersect(inLegacy).Any(), "no quest is in both nodes");
            Check(legacy.Parent.FormKey == real.Parent.FormKey, "the legacy node shares the pool's branch");
            Check(real.PreviousSibling.FormKey == legacyKey, "the pool follows the legacy node in the sibling chain");
            Check(legacy.PreviousSibling.FormKeyNullable == placeBefore, "the legacy node took the pool's old place in the chain");
            // A sibling is any Story Manager node under the same branch, quest node or branch node.
            var siblings = r.StoryManagerQuestNodes.Where(n => n.Parent.FormKey == real.Parent.FormKey)
                .Select(n => (n.FormKey, Prev: n.PreviousSibling.FormKeyNullable))
                .Concat(r.StoryManagerBranchNodes.Where(n => n.Parent.FormKey == real.Parent.FormKey)
                .Select(n => (n.FormKey, Prev: n.PreviousSibling.FormKeyNullable))).ToList();
            Check(siblings.Count(s => s.Prev == legacyKey) == 1 && siblings.Count(s => s.Prev == placeBefore) == 1,
                  $"the chain is single: one sibling follows the legacy node, one takes its old place ({siblings.Count} siblings)");

            var onG = r.Globals.FirstOrDefault(g => g.EditorID == OnGlobal);
            var shareG = r.Globals.FirstOrDefault(g => g.EditorID == ShareGlobal);
            Check(onG != null && shareG != null, $"{OnGlobal} and {ShareGlobal} exist (now {onG?.Data}, {shareG?.Data})");
            Check(onG != null && shareG != null && !onG.MajorFlags.HasFlag(Global.MajorFlag.Constant) && !shareG.MajorFlags.HasFlag(Global.MajorFlag.Constant),
                  "neither global is flagged Constant (a script and the console can set them)");
            bool gateOn = legacy.Conditions.Any(c => c.Data is IGetGlobalValueConditionDataGetter g && g.FirstParameter.Link.FormKey == onG?.FormKey
                                                     && c is IConditionFloatGetter f && f.ComparisonValue == 1f && c.CompareOperator == CompareOperator.EqualTo);
            bool gateShare = legacy.Conditions.Any(c => c.Data is IGetRandomPercentConditionDataGetter && c is IConditionGlobalGetter g
                                                        && g.ComparisonValue.FormKey == shareG?.FormKey && c.CompareOperator == CompareOperator.LessThan);
            Check(gateOn, $"the legacy node is gated GetGlobalValue({OnGlobal}) == 1");
            bool shares = real.QuestFlags.HasFlag(StoryManagerQuestNode.QuestFlag.SharesEvent);
            if (shares) Check(!gateShare, $"the pool shares the event, so the legacy node carries NO share gate (both deal on one refresh)");
            else Check(gateShare, $"the legacy node is gated GetRandomPercent < {ShareGlobal}");
            int gates = shares ? 1 : 2;
            Check(legacy.Conditions.Count == real.Conditions.Count + gates, $"the legacy node carries the pool's {real.Conditions.Count} condition(s) plus {gates} gate(s)");
            Check(legacy.QuestFlags == real.QuestFlags && legacy.MaxConcurrentQuests == real.MaxConcurrentQuests && legacy.MaxNumQuestsToRun == real.MaxNumQuestsToRun,
                  $"the legacy node keeps the pool's flags and caps ({legacy.QuestFlags}; max {legacy.MaxConcurrentQuests?.ToString() ?? "-"} / run {legacy.MaxNumQuestsToRun?.ToString() ?? "-"})");

            var opt = r.GameplayOptions.FirstOrDefault(o => o.EditorID == OptionId);
            Check(opt?.Data is IFloatGameplayOptionDataGetter d && d.Min == 0 && d.Max == 1 && d.Step == 1 && d.Value1 == 0 && opt.Rewards.Count == 2,
                  $"{OptionId}: Float 0..1 step 1, default 0, two steps");
            var group = r.GameplayOptionsGroups.FirstOrDefault(g => g.EditorID == GroupId);
            Check(group?.Options is IGameplayOptionsGroupLeafGetter leaf && opt != null && leaf.GameplayOptions.Any(l => l.FormKey == opt.FormKey),
                  $"{GroupId} lists the option");
            var quest = r.Quests.FirstOrDefault(q => q.EditorID == QuestId);
            var sc = quest?.VirtualMachineAdapter?.Scripts.FirstOrDefault(s => s.Name == Script);
            bool props = sc != null && opt != null && onG != null
                && sc.Properties.OfType<IScriptObjectPropertyGetter>().Any(p => p.Name == "ClassicMissionsOption" && p.Object.FormKey == opt.FormKey)
                && sc.Properties.OfType<IScriptObjectPropertyGetter>().Any(p => p.Name == "ClassicMissionsGlobal" && p.Object.FormKey == onG.FormKey);
            Check(quest?.Data != null && ((int)quest.Data.Flags & QuestFlagsRaw) == QuestFlagsRaw && props,
                  $"{QuestId} starts with the game and runs {Script} on the option and {OnGlobal}");

            var mastersAfter = string.Join(", ", r.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));
            Check(mastersAfter == mastersBefore, $"the plugin's masters are unchanged ({mastersAfter})");
            Console.WriteLine();
            Console.WriteLine(fails == 0 ? "  legacy: every check OK." : $"  legacy: {fails} check(s) FAILED.");
            Console.WriteLine($"  ⚠ The script is compiled separately: python papyrus/compile.py C:/modding/DU_Overtime/Data {Script}, then its .pex into Steam Data/scripts.");
            return fails == 0 ? 0 : 1;
        }
    }
}
