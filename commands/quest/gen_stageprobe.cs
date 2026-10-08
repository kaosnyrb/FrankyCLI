using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// THE STAGE PROBE, a spike: can FrankyCLI build a quest whose STAGES are the state machine, with
    /// no driver script at all? His ruling, 2026-10-08: "The stages of the quest are the state machine.
    /// All of vanilla Starfield was built this way."
    ///
    ///   gen_stageprobe [--dry]
    ///
    /// Clones the Delve base (duo_artifact_localcargo_qst09a) into du_overtime.esm as duo_stageprobe and:
    ///   - REMOVES the base's driver script, so nothing hand-rolled holds state;
    ///   - binds the quest's fragment script to papyrus/duo_qf_stageprobe.psc on stages 0, 50 and 100
    ///     (the base already binds stage 100 to a VANILLA fragment script, QF_SQ_TreasureMap_Surface_Lo_
    ///     00045F48, so a fragment bound by name is what the base is made of; writing one is the new part);
    ///   - marks stage 0 RunOnStart, so its fragment shows objective 10 on startquest;
    ///   - hooks the two created activators with stock DefaultAliasOnActivate: the first sets 50 after 0
    ///     (and disables itself), the second sets 100 after 50.
    ///
    /// PASSES when, in game: startquest duo_stageprobe shows objective 10; activating the first object
    /// shows 20; activating the second completes it; AND setstage duo_stageprobe 50 on a fresh start does
    /// what the first activation did. The last is the point of the whole change.
    ///
    /// Exit condition, written before the code: graduates into a gen_delve "beats" kind, or is deleted
    /// with its .psc. home-office office/projects/delves/stages-not-a-driver.md.
    /// </summary>
    public static class gen_stageprobe
    {
        private const string Mod = "du_overtime";
        private const string Base = "duo_artifact_localcargo_qst09a";
        private const string Id = "duo_stageprobe";
        private const string BaseDriver = "dou_artifact_ground_dualactivator_local_quest";
        private const string FragScript = "duo_qf_stageprobe";
        private const int FirstActivator = 2;    // PrimaryActivatorTarget, created at alias 1's marker
        private const int SecondActivator = 12;  // SecondaryActivatorTarget, created at alias 11's marker
        private static readonly ushort[] FragStages = { 0, 50, 100 };

        public static int Run(string[] args)
        {
            bool dry = args.Any(a => a.Equals("--dry", StringComparison.OrdinalIgnoreCase));
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();

            string modFile = Path.Combine(env.DataFolderPath, Mod + ".esm");
            if (!File.Exists(modFile)) { Console.WriteLine("REFUSED: " + modFile + " does not exist."); return 1; }
            var readParams = gen_quest_main.BuildReadParams(env.LoadOrder);
            var myMod = StarfieldMod.CreateFromBinary(modFile, StarfieldRelease.Starfield, readParams);
            gen_quest_main.FixNextFormId(myMod);
            var mastersBefore = Masters(myMod);

            var source = myMod.Quests.FirstOrDefault(q => q.EditorID == Base);
            if (source == null) { Console.WriteLine("REFUSED: no base quest '" + Base + "' in " + Mod); return 1; }

            var stale = myMod.Quests.Where(q => q.EditorID == Id).Select(q => q.FormKey).ToList();
            foreach (var k in stale) myMod.Quests.Remove(k);
            if (stale.Count > 0) Console.WriteLine("  cleaned  : " + stale.Count + " previous " + Id);

            // Same clone as gen_delve.Build, field for field, so the probe tests the binding and nothing else.
            var copy = source.DeepCopy();
            var clone = new Quest(myMod)
            {
                EditorID = Id,
                Name = "Stage Probe",
                Aliases = copy.Aliases,
                Components = copy.Components,
                Data = copy.Data,
                Keywords = copy.Keywords,
                Location = copy.Location,
                MajorFlags = copy.MajorFlags,
                Objectives = copy.Objectives,
                MissionTypeKeyword = copy.MissionTypeKeyword,
                QuestType = copy.QuestType,
                Stages = copy.Stages,
                Summary = copy.Summary,
                VirtualMachineAdapter = copy.VirtualMachineAdapter,
                MissionBoardDescription = copy.MissionBoardDescription,
                MissionBoardInfoPanels = copy.MissionBoardInfoPanels,
            };
            myMod.Quests.Add(clone);
            int repointed = gen_delvegatetest.RepointSelfLinks(clone.VirtualMachineAdapter!, source.FormKey, clone.FormKey);
            Console.WriteLine("  clone    : " + Id + " " + clone.FormKey + "  self-links repointed: " + repointed);
            if (repointed == 0) { Console.WriteLine("REFUSED: expected at least one self-reference and found none."); return 1; }

            var vma = clone.VirtualMachineAdapter!;

            // 1. No driver.
            int drivers = vma.Scripts.RemoveAll(s => s.Name == BaseDriver);
            if (drivers != 1) { Console.WriteLine($"REFUSED: expected exactly one {BaseDriver} on the base, removed {drivers}."); return 1; }
            if (vma.Scripts.Count != 0)
            { Console.WriteLine("REFUSED: the base carries other quest scripts: " + string.Join(", ", vma.Scripts.Select(s => s.Name))); return 1; }
            Console.WriteLine("  driver   : " + BaseDriver + " removed; no quest script left");

            // 2. The fragment script, bound per stage. The base's vma.Script is copied for its flags and replaced by name.
            if (vma.Script == null) { Console.WriteLine("REFUSED: the base has no fragment script to take the shape of."); return 1; }
            Console.WriteLine("  fragments: was " + vma.Script.Name + " on [" + string.Join(", ", vma.Fragments.Select(f => f.Stage)) + "]");
            vma.Script.Name = FragScript;
            vma.Script.Properties.Clear();
            vma.Fragments.Clear();
            foreach (var st in FragStages)
                vma.Fragments.Add(new QuestScriptFragment
                {
                    Stage = st,
                    StageIndex = 0,
                    ScriptName = FragScript,
                    FragmentName = FragmentName(st),
                });
            Console.WriteLine("  fragments: now " + FragScript + " on [" + string.Join(", ", FragStages) + "]");

            // 3. Stages: 0 runs on start; prose that says what this is.
            var s0 = clone.Stages.FirstOrDefault(s => s.Index == 0);
            var s50 = clone.Stages.FirstOrDefault(s => s.Index == 50);
            var s100 = clone.Stages.FirstOrDefault(s => s.Index == 100);
            if (s0 == null || s50 == null || s100 == null) { Console.WriteLine("REFUSED: the base is missing stage 0, 50 or 100."); return 1; }
            s0.Flags |= QuestStage.Flag.RunOnStart;
            SetLog(s0, "Stage probe. A quest with no driver: the stages are the state machine.");
            SetLog(s50, "The first probe object worked. The second one is next.");

            // 4. Objectives, on the targets the base already points them at.
            if (!SetObjective(clone, 10, "Activate the first probe object") || !SetObjective(clone, 20, "Activate the second probe object"))
                return 1;

            // 5. The hooks: stock scripts set the stages; nothing holds state but the quest.
            Hook(vma, clone.FormKey, FirstActivator, 50, 0, disable: true);
            Hook(vma, clone.FormKey, SecondActivator, 100, 50, disable: false);

            if (dry) { Console.WriteLine("\n  --dry: nothing written."); return 0; }

            foreach (var rec in myMod.EnumerateMajorRecords()) rec.IsCompressed = false;
            env.Dispose();
            myMod.WriteToBinary(modFile, gen_quest_main.BuildWriteParams());
            Console.WriteLine("\n  wrote " + modFile + " (" + new FileInfo(modFile).Length.ToString("N0") + " B)");

            return Verify(modFile, readParams, mastersBefore);
        }

        private static string FragmentName(ushort stage) => $"Fragment_Stage_{stage:D4}_Item_00";

        private static string Masters(IStarfieldModGetter m) =>
            string.Join(", ", m.ModHeader.MasterReferences.Select(x => x.Master.FileName.String));

        private static void SetLog(QuestStage s, string text)
        {
            var e = s.LogEntries.FirstOrDefault();
            if (e != null) e.Entry = text;
        }

        private static bool SetObjective(Quest q, int index, string text)
        {
            var ob = q.Objectives?.FirstOrDefault(o => o.Index == index);
            if (ob == null) { Console.WriteLine("REFUSED: no objective " + index + " on the base."); return false; }
            ob.DisplayText = text;
            return true;
        }

        /// <summary>As gen_delve.HookActivator, plus the stock disable-after flag for a pickup-shaped beat.</summary>
        private static void Hook(QuestAdapter vma, FormKey quest, int aliasId, int stageToSet, int prereq, bool disable)
        {
            var entry = new QuestFragmentAlias();
            entry.Property.Object.SetTo(quest);
            entry.Property.Alias = (short)aliasId;
            var sc = new ScriptEntry { Name = "DefaultAliasOnActivate" };
            sc.Properties.Add(new ScriptIntProperty { Name = "StageToSet", Data = stageToSet, Flags = ScriptProperty.Flag.Edited });
            sc.Properties.Add(new ScriptIntProperty { Name = "PrereqStage", Data = prereq, Flags = ScriptProperty.Flag.Edited });
            if (disable)
                sc.Properties.Add(new ScriptBoolProperty { Name = "ShouldDisableAfterSuccessfulActivation", Data = true, Flags = ScriptProperty.Flag.Edited });
            entry.Scripts.Add(sc);
            vma.Aliases.Add(entry);
            Console.WriteLine($"  +hook    : alias {aliasId} DefaultAliasOnActivate  sets {stageToSet} after {prereq}" + (disable ? ", disables itself" : ""));
        }

        /// <summary>Read the written plugin back OFF DISK and check every claim above against it.</summary>
        private static int Verify(string modFile, Mutagen.Bethesda.Plugins.Binary.Parameters.BinaryReadParameters readParams, string mastersBefore)
        {
            Console.WriteLine("\n=== VERIFY (off disk) ===");
            var m = StarfieldMod.CreateFromBinaryOverlay(modFile, StarfieldRelease.Starfield, readParams);
            int bad = 0;
            void Ok(bool c, string what) { Console.WriteLine((c ? "  [OK  ] " : "  [FAIL] ") + what); if (!c) bad++; }

            var qs = m.Quests.Where(q => q.EditorID == Id).ToList();
            Ok(qs.Count == 1, "exactly one " + Id + " (found " + qs.Count + ")");
            if (qs.Count != 1) return 1;
            var q = qs[0];
            var vma = q.VirtualMachineAdapter;
            Ok(vma != null, "quest has a VMAD");
            if (vma == null) return 1;
            Ok(vma.Script?.Name == FragScript, "fragment script is " + FragScript + " (is " + vma.Script?.Name + ")");
            var frags = vma.Fragments.Select(f => (f.Stage, f.ScriptName, f.FragmentName)).ToList();
            foreach (var st in FragStages)
                Ok(frags.Any(f => f.Stage == st && f.ScriptName == FragScript && f.FragmentName == FragmentName(st)),
                   $"stage {st} bound to {FragScript}.{FragmentName(st)}");
            Ok(frags.Count == FragStages.Length, "no other fragment rows (" + frags.Count + ")");
            Ok(vma.Scripts.Count == 0, "no quest-level script, so no driver (" + vma.Scripts.Count + ")");
            Ok(q.Stages.Any(s => s.Index == 0 && s.Flags.HasFlag(QuestStage.Flag.RunOnStart)), "stage 0 is RunOnStart");
            foreach (var (alias, set, pre) in new[] { (FirstActivator, 50, 0), (SecondActivator, 100, 50) })
            {
                var hook = vma.Aliases.FirstOrDefault(a => a.Property.Alias == alias)?.Scripts
                    .FirstOrDefault(s => s.Name == "DefaultAliasOnActivate");
                int? Int(string n) => hook?.Properties.OfType<IScriptIntPropertyGetter>().FirstOrDefault(p => p.Name == n)?.Data;
                Ok(hook != null && Int("StageToSet") == set && Int("PrereqStage") == pre,
                   $"alias {alias} hook sets {set} after {pre}");
            }
            Ok(Masters(m) == mastersBefore, "the plugin's masters are unchanged (" + Masters(m) + ")");
            Console.WriteLine(bad == 0 ? "\n=== probe written and verified. Compile duo_qf_stageprobe, then startquest " + Id + ". ==="
                                       : "\n=== " + bad + " check(s) FAILED ===");
            return bad == 0 ? 0 : 1;
        }
    }
}
