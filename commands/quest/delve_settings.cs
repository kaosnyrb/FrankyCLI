using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// TWO CHANCE SLIDERS IN OVERTIME'S SETTINGS (his 2026-11-13: "yeah lets make it a chance on the installation" and "It'd be
    /// good to have a gameplay setting for comms sat and dataslate drop chance"; steps, defaults and hostile-only his "yeah
    /// those three things sound right"). Both are what his original space missions did, put back on every space Delve:
    ///   COMMS SATELLITE: his activator scripts (ccs_activaterepair and kin) place one of duo_ShipList_Installation beside the
    ///   object on load; the Delves had dropped it with his script (MB104a, 2026-11-03). Now a roll against duo_CommsSatChance.
    ///   DATASLATE DROP: his repair script's attackers carry one of duo_ArtifactList_Remote at 50%. Now every hostile ship a
    ///   Delve spawns rolls against duo_ShipSlateChance.
    /// Each is a slider 0..100 in steps of 10 (11 rewards, one per step, the menu's labels), mirrored into its global by
    /// duo_gameplayoptions.psc on duo_GameplayOptionsQuest, the Classic Missions shape (delve_legacy.cs), whose group,
    /// quest and global shape this extends. Defaults 25 and 50, written to Value1/Value2 as his Suit Power Cores slider does.
    /// </summary>
    internal static class DelveSettings
    {
        internal const string CommsGlobal = "duo_CommsSatChance", SlateGlobal = "duo_ShipSlateChance";
        internal const string CommsOption = "duo_GPO_CommsSatChance", SlateOption = "duo_GPO_ShipSlateChance";
        internal const string InstallationList = "duo_ShipList_Installation", SlateList = "duo_ArtifactList_Remote";
        const float CommsDefault = 25f, SlateDefault = 50f;

        /// <summary>(option id, global id, script's option property, script's global property, name, description, default)</summary>
        static readonly (string opt, string glob, string optProp, string globProp, string name, string desc, float def)[] Sliders =
        {
            (CommsOption, CommsGlobal, "CommsSatOption", "CommsSatGlobal", "Comms Satellite Chance",
             "How often a space mission's site also has a communication satellite, which offers more work.", CommsDefault),
            (SlateOption, SlateGlobal, "ShipSlateOption", "ShipSlateGlobal", "Dataslate Drop Chance",
             "How often a hostile ship in a mission carries a dataslate for another job.", SlateDefault),
        };

        internal static int Run(string[] args, List<string> mods, bool dry)
        {
            if (mods.Count != 1) { Console.WriteLine("REFUSED: the templates name " + mods.Count + " mods; settings knows which plugin only when they agree."); return 1; }
            using var session = PluginSession.Open(mods[0]);
            if (session == null) return 1;
            var myMod = session.Mod;
            var mastersBefore = string.Join(", ", myMod.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));

            Console.WriteLine();
            Console.WriteLine("=== SETTINGS: comms satellite and dataslate chance ===");
            var group = myMod.GameplayOptionsGroups.FirstOrDefault(g => g.EditorID == DelveLegacy.GroupId);
            var quest = myMod.Quests.FirstOrDefault(q => q.EditorID == DelveLegacy.QuestId);
            var shape = myMod.Globals.FirstOrDefault(g => g.EditorID == DelveLegacy.GlobalShape);
            if (group?.Options is not GameplayOptionsGroupLeaf leaf || quest?.VirtualMachineAdapter == null || shape == null)
            { Console.WriteLine($"REFUSED: {DelveLegacy.GroupId} (with an options leaf), {DelveLegacy.QuestId} (with its script) and {DelveLegacy.GlobalShape} must all exist; Classic Missions makes them. Nothing written."); return 1; }
            var sc = quest.VirtualMachineAdapter.Scripts.FirstOrDefault(s => s.Name == DelveLegacy.Script);
            if (sc == null) { Console.WriteLine($"REFUSED: {DelveLegacy.QuestId} does not run {DelveLegacy.Script}. Nothing written."); return 1; }
            foreach (var l in new[] { InstallationList, SlateList })
                if (!myMod.FormLists.Any(f => f.EditorID == l)) { Console.WriteLine($"REFUSED: no FormList {l} in {mods[0]}.esm. Nothing written."); return 1; }

            // All four records per slider, or none: a half-built setting is refused rather than patched.
            int present = Sliders.Sum(s => (myMod.Globals.Any(g => g.EditorID == s.glob) ? 1 : 0) + (myMod.GameplayOptions.Any(o => o.EditorID == s.opt) ? 1 : 0)
                                          + (sc.Properties.Any(p => p.Name == s.optProp) ? 1 : 0) + (sc.Properties.Any(p => p.Name == s.globProp) ? 1 : 0));
            if (present == Sliders.Length * 4) { Console.WriteLine("  both sliders exist; nothing to do"); return 0; }
            if (present != 0) { Console.WriteLine($"REFUSED: the two sliders are half-built ({present} of {Sliders.Length * 4} parts present). Nothing written."); return 1; }

            foreach (var s in Sliders)
            {
                var g = (Global)shape.Duplicate(myMod.GetNextFormKey());
                g.EditorID = s.glob;
                g.Data = s.def;
                g.MajorFlags &= ~Global.MajorFlag.Constant;   // the script SETS it (delve_legacy.cs: the Constant flag cost his first play)
                myMod.Globals.Add(g);

                var opt = new GameplayOption(myMod)
                {
                    EditorID = s.opt, Name = s.name, Description = s.desc,
                    Data = new FloatGameplayOptionData { Value1 = s.def, Value2 = s.def, Min = 0f, Max = 100f, Step = 10f },
                };
                for (int v = 0; v <= 100; v += 10)
                    opt.Rewards.Add(new GameplayOptionReward { SettingValue = v, RewardValue = 0f, Name = v == 0 ? "Never" : $"{v}%", Description = s.desc });
                myMod.GameplayOptions.Add(opt);
                leaf.GameplayOptions.Add(opt.ToLink<IGameplayOptionGetter>());

                var po = new ScriptObjectProperty { Name = s.optProp, Flags = ScriptProperty.Flag.Edited };
                po.Object.SetTo(opt.FormKey);
                var pg = new ScriptObjectProperty { Name = s.globProp, Flags = ScriptProperty.Flag.Edited };
                pg.Object.SetTo(g.FormKey);
                sc.Properties.Add(po);
                sc.Properties.Add(pg);
                Console.WriteLine($"  +slider  : {s.opt} \"{s.name}\" 0..100 step 10, default {s.def}, mirrored into {s.glob} {g.FormKey} (= {s.def}) by {DelveLegacy.Script}");
            }

            if (dry) { Console.WriteLine("\n  --dry: nothing written."); return 0; }
            session.Close();
            session.Write();
            Console.WriteLine("  wrote " + session.PluginPath);
            return Verify(session, mastersBefore);
        }

        static int Verify(PluginSession session, string mastersBefore)
        {
            using var r = StarfieldMod.CreateFromBinaryOverlay(session.PluginPath, StarfieldRelease.Starfield, session.ReadParams);
            int fails = 0;
            void Check(bool ok, string what) { Console.WriteLine($"  [{(ok ? "OK  " : "FAIL")}] {what}"); if (!ok) fails++; }
            var group = r.GameplayOptionsGroups.FirstOrDefault(g => g.EditorID == DelveLegacy.GroupId);
            var sc = r.Quests.FirstOrDefault(q => q.EditorID == DelveLegacy.QuestId)?.VirtualMachineAdapter?.Scripts.FirstOrDefault(s => s.Name == DelveLegacy.Script);
            var inGroup = (group?.Options as IGameplayOptionsGroupLeafGetter)?.GameplayOptions.Select(o => o.FormKey).ToHashSet() ?? new HashSet<FormKey>();
            foreach (var s in Sliders)
            {
                var g = r.Globals.FirstOrDefault(x => x.EditorID == s.glob);
                var o = r.GameplayOptions.FirstOrDefault(x => x.EditorID == s.opt);
                var d = o?.Data as IFloatGameplayOptionDataGetter;
                Check(g != null && !g.MajorFlags.HasFlag(Global.MajorFlag.Constant) && g.Data == s.def, $"off disk: {s.glob} exists, is not Constant, and holds {s.def}");
                Check(d != null && d.Min == 0 && d.Max == 100 && d.Step == 10 && d.Value1 == s.def && o!.Rewards.Count == 11,
                      $"off disk: {s.opt} is 0..100 step 10, default {s.def}, 11 rewards");
                Check(o != null && inGroup.Contains(o.FormKey), $"off disk: {s.opt} is in {DelveLegacy.GroupId}");
                Check(sc?.Properties.OfType<IScriptObjectPropertyGetter>().FirstOrDefault(p => p.Name == s.optProp)?.Object.FormKey == o?.FormKey
                      && sc?.Properties.OfType<IScriptObjectPropertyGetter>().FirstOrDefault(p => p.Name == s.globProp)?.Object.FormKey == g?.FormKey,
                      $"off disk: {DelveLegacy.Script}'s {s.optProp} and {s.globProp} name them");
            }
            Check(string.Join(", ", r.ModHeader.MasterReferences.Select(m => m.Master.FileName.String)) == mastersBefore, "the plugin's masters are unchanged");
            Console.WriteLine(fails == 0 ? "  SETTINGS VERIFIED. Compile duo_gameplayoptions and deploy it: the sliders do nothing until the script mirrors them."
                                         : $"  {fails} CHECK(S) FAILED.");
            return fails == 0 ? 0 : 1;
        }
    }
}
