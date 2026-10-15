using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Parameters;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrankyCLI
{
    public static partial class gen_questmodel
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private static string ScratchDir()
        {
            string d = Path.Combine(Path.GetTempPath(), "franky-questmodel");
            Directory.CreateDirectory(d);
            return d;
        }

        private static string Opt(List<string> a, string name, string dflt)
        {
            int i = a.IndexOf(name);
            return i >= 0 && i + 1 < a.Count ? a[i + 1] : dflt;
        }

        private static List<string> Positional(List<string> a) =>
            a.Where((x, i) => !x.StartsWith("--") && (i == 0 || !a[i - 1].StartsWith("--"))).ToList();

        private static int ReadVerb(List<string> a)
        {
            var pos = Positional(a);
            if (pos.Count != 1) { Console.WriteLine("REFUSED: gen_questmodel read <editorId> [--out file.json] [--mod name]"); return 1; }
            using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            var readParams = gen_quest_main.BuildReadParams(env.LoadOrder);
            string modFile = Path.Combine(env.DataFolderPath, Opt(a, "--mod", "du_overtime") + ".esm");
            using var mod = StarfieldMod.CreateFromBinaryOverlay(modFile, StarfieldRelease.Starfield, readParams);
            var q = mod.Quests.FirstOrDefault(x => x.EditorID == pos[0]);
            if (q == null) { Console.WriteLine($"REFUSED: no quest {pos[0]} in {modFile}"); return 1; }
            var rd = new Reader();
            var m = rd.Quest(q);
            string outFile = Opt(a, "--out", Path.Combine(ScratchDir(), q.EditorID + ".json"));
            File.WriteAllText(outFile, JsonSerializer.Serialize(m, Json));
            Console.WriteLine($"  model    : {outFile}");
            foreach (var r in rd.Refusals) Console.WriteLine("  REFUSED  : " + r);
            Console.WriteLine(rd.Refusals.Count == 0 ? "  every field of the record is in the model" : $"  {rd.Refusals.Count} field(s) the model cannot hold yet");
            return rd.Refusals.Count == 0 ? 0 : 1;
        }

        private static int CheckVerb(List<string> a)
        {
            var pos = Positional(a);
            if (pos.Count != 1) { Console.WriteLine("REFUSED: gen_questmodel check <editorId | prefix*> [--mod name]"); return 1; }
            string pat = pos[0];
            bool bite = a.Contains("--bite");
            using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            var readParams = gen_quest_main.BuildReadParams(env.LoadOrder);
            string modFile = Path.Combine(env.DataFolderPath, Opt(a, "--mod", "du_overtime") + ".esm");
            using var mod = StarfieldMod.CreateFromBinaryOverlay(modFile, StarfieldRelease.Starfield, readParams);
            var quests = mod.Quests.Where(x => x.EditorID != null && (pat.EndsWith("*")
                ? x.EditorID.StartsWith(pat[..^1], StringComparison.OrdinalIgnoreCase) : x.EditorID == pat)).ToList();
            if (quests.Count == 0) { Console.WriteLine($"REFUSED: no quest matching {pat} in {modFile}"); return 1; }

            int same = 0, refused = 0, differ = 0;
            foreach (var q in quests)
            {
                var rd = new Reader();
                var model = rd.Quest(q);
                if (rd.Refusals.Count > 0)
                {
                    refused++;
                    Console.WriteLine($"  REFUSED  {q.EditorID}: {rd.Refusals.Count} field(s) outside the model");
                    foreach (var r in rd.Refusals.Take(quests.Count == 1 ? 50 : 4)) Console.WriteLine("             " + r);
                    continue;
                }
                // The writer sees ONLY what survived a trip through JSON on disk: the model file is its whole input.
                string json = Path.Combine(ScratchDir(), q.EditorID + ".json");
                File.WriteAllText(json, JsonSerializer.Serialize(model, Json));
                var fromDisk = JsonSerializer.Deserialize<QModel>(File.ReadAllText(json), Json)!;
                // --bite: the positive control. Drop the last condition of the first location alias from what the writer
                // sees; a check that still says IDENTICAL after this is not a check.
                if (bite)
                {
                    var loc = fromDisk.Aliases.FirstOrDefault(x => x.Kind == "loc" && x.Conditions is { Count: > 0 });
                    if (loc == null) { Console.WriteLine($"  BITE     {q.EditorID}: no location alias with a condition to drop"); differ++; continue; }
                    loc.Conditions!.RemoveAt(loc.Conditions.Count - 1);
                }
                Quest built;
                try { built = WriteQuest(fromDisk); }
                catch (Exception e) { differ++; Console.WriteLine($"  WRITE    {q.EditorID}: {e}"); continue; }

                // The ORACLE side only: the real record copied into a plugin of its own. Never on the write path.
                var oracle = (Quest)q.DeepCopy();
                oracle.Scenes.Clear(); oracle.DialogTopics.Clear(); oracle.DialogBranches.Clear();
                byte[] want = Standalone(oracle, "oracle");
                byte[] got = Standalone(built, "built");
                if (want.SequenceEqual(got)) { same++; Console.WriteLine($"  IDENTICAL {q.EditorID}  ({got.Length:N0} B plugin, byte for byte; children not in scope: {rd.Children})"); continue; }

                differ++;
                int n = Math.Min(want.Length, got.Length), at = n;
                for (int i = 0; i < n; i++) if (want[i] != got[i]) { at = i; break; }
                Console.WriteLine($"  DIFFER   {q.EditorID}: real {want.Length:N0} B, built {got.Length:N0} B, first difference at byte {at}");
                using var back = StarfieldMod.CreateFromBinaryOverlay(Path.Combine(ScratchDir(), "built", "qm_check.esm"), StarfieldRelease.Starfield, readParams);
                var m2 = new Reader().Quest(back.Quests.First());
                var l1 = JsonSerializer.Serialize(model, Json).Split('\n');
                var l2 = JsonSerializer.Serialize(m2, Json).Split('\n');
                int shown = 0;
                for (int i = 0; i < Math.Max(l1.Length, l2.Length) && shown < 12; i++)
                {
                    string x = i < l1.Length ? l1[i].Trim() : "", y = i < l2.Length ? l2[i].Trim() : "";
                    if (x == y) continue;
                    Console.WriteLine($"      model line {i + 1}:  real  {x}");
                    Console.WriteLine($"      {"",11}       built {y}");
                    shown++;
                }
                if (shown == 0) Console.WriteLine("      the two MODELS agree, so the difference is in a field the reader cannot see: widen the model");
            }
            Console.WriteLine($"\n  {quests.Count} quest(s): {same} identical, {differ} differ, {refused} refused");
            if (bite)
            {
                bool caught = same == 0 && differ == quests.Count;
                Console.WriteLine(caught ? "  BITE OK: every planted record was caught" : "  ⛔ BITE FAILED: a planted record came back IDENTICAL");
                return caught ? 0 : 1;
            }
            return same == quests.Count ? 0 : 1;
        }

        /// One record in a plugin of its own, written with the office's write parameters; the file's bytes back.
        private static byte[] Standalone(Quest q, string side)
        {
            string dir = Path.Combine(ScratchDir(), side);
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "qm_check.esm");
            var mod = new StarfieldMod(ModKey.FromFileName("qm_check.esm"), StarfieldRelease.Starfield);
            q.IsCompressed = false;
            mod.Quests.Add(q);
            mod.WriteToBinary(file, gen_quest_main.BuildWriteParams());
            return File.ReadAllBytes(file);
        }
    }
}
