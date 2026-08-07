using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FrankyCLI
{
    // Author the multi-stage half of a quest: stages, objectives, and the alias hooks that
    // move between them.
    //
    //   queststage <mod> list      <questPattern>
    //   queststage <mod> stage     <questPattern> <index> [--log "journal text"] [--dry]
    //   queststage <mod> objective <questPattern> <index> "<display text>" [--target <aliasName>] [--dry]
    //   queststage <mod> hook      <questPattern> <aliasName> <ScriptName>
    //                              --stage N [--prereq M] [--turnoff K] [--set Prop=Value ...] [--dry]
    //
    // WHY: a Bethesda quest gets its depth from a STAGE GRAPH driven by stock scripts, not from
    // bespoke Papyrus. MQ102 is 66 stages / 25 objectives / 23 alias scripts, of which 19 are stock
    // and 4 are bespoke; 61 of its 66 stages carry no journal text at all and exist purely as machine
    // state. The whole sequencing mechanism is four inherited properties -- StageToSet, PrereqStage,
    // TurnOffStage, TurnOffStageDone -- so a beat is "hook an event, gate it on the previous stage,
    // set the next one". `hook` is that sentence. Catalogue of what you can hook:
    // office/projects/bethesda/10-the-default-script-catalogue.md.
    //
    // ⛔ THE BROKEN-SCRIPT GUARD IS DERIVED, NOT A LIST. Seven vanilla Default* scripts declare
    // themselves OBSOLETE or NOT YET REIMPLEMENTED in their own docstrings -- including
    // DefaultCounterQuest, which is exactly what you would reach for on "kill 5 things". This tool
    // reads the .psc header at run time and refuses them by name, so the refusal cannot go stale
    // against a game update the way a hardcoded list would.
    //
    // ⛔ THE MASTER GUARD, same as questprop and for the same reason: an object value from a plugin
    // the mod does not master ADDS that plugin as a master, and the mod then fails to load for
    // everyone without it. Refused, with the plugin named. No --force.
    //
    // WHAT IT WILL NOT DO, said rather than discovered: it does not CREATE aliases (that is a bigger
    // operation and the CK is good at it), and it will not set array or struct properties -- it names
    // them and stops, because a half-understood write into a structured property is worse than no
    // tool. Set those in the CK; everything else here is scriptable.
    class gen_queststage
    {
        static readonly Regex Broken = new(@"OBSOLETE|NOT YET (FULLY )?REIMPLEMENTED", RegexOptions.IgnoreCase);
        static readonly string ScriptSrc =
            @"C:/Program Files (x86)/Steam/steamapps/common/Starfield/Data/scripts/Source";

        public static int Generate(string[] args)
        {
            // args: [modname, "queststage", verb, questPattern, ...]
            if (args.Length < 4) { Usage(); return 1; }
            string modname = args[0], verb = args[2].ToLowerInvariant(), pattern = args[3];
            if (modname == "Starfield") { Console.WriteLine("No way am I allowing you to edit Starfield.esm"); return 1; }

            var rest = args.Skip(4).ToList();
            bool dry = rest.RemoveAll(a => a.Equals("--dry", StringComparison.OrdinalIgnoreCase)) > 0;
            string? Opt(string name)
            {
                int i = rest.FindIndex(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (i < 0) return null;
                if (i + 1 >= rest.Count) { Console.WriteLine($"Error: {name} needs a value"); Environment.Exit(1); }
                string v = rest[i + 1]; rest.RemoveRange(i, 2); return v;
            }
            string? log = Opt("--log"), target = Opt("--target");
            string? sStage = Opt("--stage"), sPrereq = Opt("--prereq"), sTurnoff = Opt("--turnoff");
            var sets = new List<string>();
            for (int i; (i = rest.FindIndex(a => a.Equals("--set", StringComparison.OrdinalIgnoreCase))) >= 0;)
            {
                if (i + 1 >= rest.Count) { Console.WriteLine("Error: --set needs Prop=Value"); return 1; }
                sets.Add(rest[i + 1]); rest.RemoveRange(i, 2);
            }

            using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            string datapath = env.DataFolderPath;
            if (!env.LoadOrder.ModExists(new ModKey(modname, ModType.Master)))
            { Console.WriteLine($"Error: {modname}.esm is not in the load order"); return 1; }
            string modFile = Path.Combine(datapath, modname + ".esm");
            var myMod = StarfieldMod.CreateFromBinary(modFile, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
            gen_quest_main.FixNextFormId(myMod);

            var quests = myMod.Quests
                .Where(q => q.EditorID != null && q.EditorID.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                .OrderBy(q => q.EditorID, StringComparer.OrdinalIgnoreCase).ToList();
            if (quests.Count == 0) { Console.WriteLine($"No quest in {modname} matches '{pattern}'."); return 1; }
            Console.WriteLine($"  {quests.Count} quest(s) match '{pattern}'");

            if (verb == "list")
            {
                foreach (var q in quests) DumpQuest(q);
                return 0;
            }

            int touched = 0;
            var refusals = new List<string>();

            if (verb == "stage")
            {
                if (rest.Count < 1 || !ushort.TryParse(rest[0], out ushort idx))
                { Console.WriteLine("Error: stage needs a numeric <index>"); return 1; }
                foreach (var q in quests)
                {
                    if (q.Stages.Any(s => s.Index == idx))
                    { refusals.Add($"{q.EditorID}: stage {idx} already exists -- refusing to overwrite"); continue; }
                    var st = new QuestStage { Index = idx };
                    if (log != null)
                        st.LogEntries.Add(new QuestLogEntry { Entry = log });
                    q.Stages.Add(st);
                    touched++;
                    Console.WriteLine($"    + {q.EditorID}: stage {idx}" + (log != null ? $"  log=\"{Trim(log)}\"" : "  (silent -- machine state)"));
                }
            }
            else if (verb == "objective")
            {
                if (rest.Count < 2 || !ushort.TryParse(rest[0], out ushort oidx))
                { Console.WriteLine("Error: objective needs <index> \"<text>\""); return 1; }
                string text = rest[1];
                foreach (var q in quests)
                {
                    if (q.Objectives.Any(o => o.Index == oidx))
                    { refusals.Add($"{q.EditorID}: objective {oidx} already exists -- refusing to overwrite"); continue; }
                    var ob = new QuestObjective { Index = oidx, DisplayText = text };
                    if (target != null)
                    {
                        var al = FindAlias(q, target);
                        if (al == null) { refusals.Add($"{q.EditorID}: no alias named '{target}'"); continue; }
                        ob.Targets.Add(new QuestObjectiveTarget { AliasID = (int)al.Value.id });
                    }
                    q.Objectives.Add(ob);
                    touched++;
                    Console.WriteLine($"    + {q.EditorID}: objective {oidx} \"{Trim(text)}\"" + (target != null ? $"  -> alias {target}" : ""));
                }
            }
            else if (verb == "hook")
            {
                if (rest.Count < 2) { Usage(); return 1; }
                string aliasName = rest[0], scriptName = rest[1];

                // --- the script must exist, and must not declare itself broken -----------------
                string? psc = FindScript(scriptName);
                if (psc == null)
                { Console.WriteLine($"Error: no vanilla script named '{scriptName}' in {ScriptSrc}"); return 1; }
                string header = File.ReadAllText(psc);
                // IgnoreCase is load-bearing: the files say "ScriptName" and .NET regex is
                // case-SENSITIVE by default, so without it this match silently failed and the
                // broken-script guard below passed everything through. Watched failing, then fixed.
                var docm = Regex.Match(header, @"Scriptname[^\n]*\n\s*\{(.*?)\}",
                                       RegexOptions.Singleline | RegexOptions.IgnoreCase);
                string doc = docm.Success ? Regex.Replace(docm.Groups[1].Value, @"\s+", " ").Trim() : "";
                if (Broken.IsMatch(doc))
                {
                    // Quote around the MATCH, not the first 200 chars -- the head of the docstring
                    // is a friendly description and showing it made the message assert "broken"
                    // while displaying evidence that said no such thing.
                    var bm = Broken.Match(doc);
                    int from = Math.Max(0, bm.Index - 40);
                    Console.WriteLine($"Error: '{scriptName}' declares itself broken in its own docstring:");
                    Console.WriteLine($"       \"…{Trim(doc[from..], 170)}\"");
                    Console.WriteLine("       Refused. See the Default* catalogue for the working alternative.");
                    return 1;
                }
                if (sStage == null || !int.TryParse(sStage, out int stageToSet))
                { Console.WriteLine("Error: hook needs --stage <n> (the stage this hook sets)"); return 1; }

                foreach (var q in quests)
                {
                    var al = FindAlias(q, aliasName);
                    if (al == null) { refusals.Add($"{q.EditorID}: no alias named '{aliasName}'"); continue; }
                    var vma = q.VirtualMachineAdapter;
                    if (vma == null) { refusals.Add($"{q.EditorID}: quest has no VirtualMachineAdapter"); continue; }

                    var entry = vma.Aliases.FirstOrDefault(a => a.Property.Alias == al.Value.id);
                    if (entry == null)
                    {
                        entry = new QuestFragmentAlias();
                        entry.Property.Object.SetTo(q.FormKey);
                        entry.Property.Alias = (short)al.Value.id;
                        vma.Aliases.Add(entry);
                        Console.WriteLine($"    ! {q.EditorID}: alias '{aliasName}' had no script block; created one");
                    }
                    if (entry.Scripts.Any(s => string.Equals(s.Name, scriptName, StringComparison.OrdinalIgnoreCase)))
                    {
                        refusals.Add($"{q.EditorID}: alias '{aliasName}' already carries '{scriptName}'. " +
                                     "A script attaches to an alias ONCE -- use the A/B/C/D duplicate for a second copy.");
                        continue;
                    }

                    var sc = new ScriptEntry { Name = scriptName };
                    AddInt(sc, "StageToSet", stageToSet);
                    if (sPrereq != null && int.TryParse(sPrereq, out int pr)) AddInt(sc, "PrereqStage", pr);
                    if (sTurnoff != null && int.TryParse(sTurnoff, out int to)) AddInt(sc, "TurnOffStage", to);

                    bool bad = false;
                    foreach (var kv in sets)
                    {
                        int eq = kv.IndexOf('=');
                        if (eq < 1) { refusals.Add($"{q.EditorID}: --set '{kv}' is not Prop=Value"); bad = true; break; }
                        string pn = kv[..eq], pv = kv[(eq + 1)..];
                        if (int.TryParse(pv, out int iv)) { AddInt(sc, pn, iv); continue; }
                        if (bool.TryParse(pv, out bool bv)) { sc.Properties.Add(new ScriptBoolProperty { Name = pn, Data = bv, Flags = ScriptProperty.Flag.Edited }); continue; }
                        if (float.TryParse(pv, NumberStyles.Float, CultureInfo.InvariantCulture, out float fv))
                        { sc.Properties.Add(new ScriptFloatProperty { Name = pn, Data = fv, Flags = ScriptProperty.Flag.Edited }); continue; }
                        // otherwise: an EditorID -> object, with the master guard
                        IStarfieldMajorRecordGetter? hit = myMod.EnumerateMajorRecords()
                                .FirstOrDefault(r => string.Equals(r.EditorID, pv, StringComparison.OrdinalIgnoreCase)) as IStarfieldMajorRecordGetter;
                        hit ??= env.LoadOrder.PriorityOrder.WinningOverrides<IStarfieldMajorRecordGetter>()
                                      .FirstOrDefault(r => string.Equals(r.EditorID, pv, StringComparison.OrdinalIgnoreCase));
                        if (hit == null) { refusals.Add($"{q.EditorID}: --set {pn}: no record with EditorID '{pv}'"); bad = true; break; }
                        var allowed = new HashSet<ModKey>(myMod.ModHeader.MasterReferences.Select(m => m.Master)) { myMod.ModKey };
                        if (!allowed.Contains(hit.FormKey.ModKey))
                        {
                            refusals.Add($"{q.EditorID}: --set {pn}='{pv}' lives in '{hit.FormKey.ModKey.FileName}', which {modname} does not master. " +
                                         "Writing it would add that plugin as a master and the mod would stop loading without it.");
                            bad = true; break;
                        }
                        var op = new ScriptObjectProperty { Name = pn, Flags = ScriptProperty.Flag.Edited };
                        op.Object.SetTo(hit.FormKey);
                        sc.Properties.Add(op);
                    }
                    if (bad) continue;

                    entry.Scripts.Add(sc);
                    touched++;
                    string gate = sPrereq != null ? $" after stage {sPrereq}" : "";
                    Console.WriteLine($"    + {q.EditorID}: {aliasName} -> {scriptName}  sets stage {stageToSet}{gate}");
                }
            }
            else { Usage(); return 1; }

            Console.WriteLine();
            foreach (var r in refusals) Console.WriteLine($"  [REFUSED] {r}");
            if (refusals.Count > 0)
            {
                Console.WriteLine("\n  Nothing written -- a partial write across a quest family is worse than none.");
                return 1;
            }
            if (touched == 0) { Console.WriteLine("  Nothing to do -- nothing written."); return 0; }
            if (dry) { Console.WriteLine($"  --dry: nothing written ({touched} change(s) would land)."); return 0; }

            foreach (var rec in myMod.EnumerateMajorRecords()) rec.IsCompressed = false;
            myMod.WriteToBinary(modFile, gen_quest_main.BuildWriteParams());
            Console.WriteLine($"  Written to {modname}.esm ({touched} change(s)).");
            Console.WriteLine($"  Verify: FrankyCLI queststage {modname} list {pattern}");
            return 0;
        }

        static void AddInt(ScriptEntry sc, string name, int v) =>
            sc.Properties.Add(new ScriptIntProperty { Name = name, Data = v, Flags = ScriptProperty.Flag.Edited });

        static (uint id, string name)? FindAlias(IQuestGetter q, string name)
        {
            foreach (var a in q.Aliases ?? Enumerable.Empty<IAQuestAliasGetter>())
                foreach (var (id, nm) in Flatten(a))
                    if (string.Equals(nm, name, StringComparison.OrdinalIgnoreCase)) return (id, nm);
            return null;
        }

        static IEnumerable<(uint, string)> Flatten(IAQuestAliasGetter a)
        {
            if (a is IQuestReferenceAliasGetter r && r.Name != null) yield return (r.ID, r.Name);
            if (a is IQuestCollectionAliasGetter c)
                foreach (var m in c.Collection)
                    if (m.ReferenceAlias?.Name != null) yield return (m.ReferenceAlias.ID, m.ReferenceAlias.Name!);
        }

        static string? FindScript(string name)
        {
            if (!Directory.Exists(ScriptSrc)) return null;
            return Directory.GetFiles(ScriptSrc, "*.psc")
                .FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), name, StringComparison.OrdinalIgnoreCase));
        }

        static string Trim(string s, int n = 60) => s.Length <= n ? s : s[..n] + "…";

        static void DumpQuest(IQuestGetter q)
        {
            Console.WriteLine($"\n  {q.EditorID}  [{q.FormKey.ID:X6}]   {q.Name}");
            var stages = q.Stages.OrderBy(s => s.Index).ToList();
            int silent = stages.Count(s => s.LogEntries.Count == 0 || s.LogEntries.All(e => string.IsNullOrEmpty(e.Entry?.String)));
            Console.WriteLine($"    Stages [{stages.Count}]  ({silent} silent / machine state)");
            foreach (var s in stages)
            {
                var txt = s.LogEntries.Select(e => e.Entry?.String).FirstOrDefault(x => !string.IsNullOrEmpty(x));
                Console.WriteLine($"      {s.Index,5}  {(txt != null ? "\"" + Trim(txt, 70) + "\"" : "-")}");
            }
            Console.WriteLine($"    Objectives [{q.Objectives.Count}]");
            foreach (var o in q.Objectives.OrderBy(o => o.Index))
                Console.WriteLine($"      {o.Index,5}  \"{Trim(o.DisplayText?.String ?? "", 60)}\"" +
                                  (o.Targets.Count > 0 ? $"  -> alias {string.Join(",", o.Targets.Select(t => t.AliasID))}" : ""));
            var vma = q.VirtualMachineAdapter;
            var names = new Dictionary<uint, string>();
            foreach (var a in q.Aliases ?? Enumerable.Empty<IAQuestAliasGetter>())
                foreach (var (id, nm) in Flatten(a)) names[id] = nm;
            Console.WriteLine($"    Alias hooks [{vma?.Aliases?.Count ?? 0}]");
            foreach (var fa in vma?.Aliases ?? Enumerable.Empty<IQuestFragmentAliasGetter>())
            {
                uint aid = (uint)fa.Property.Alias;
                Console.WriteLine($"      alias {aid,3} {(names.TryGetValue(aid, out var n) ? n : "(unnamed)"),-28} " +
                                  string.Join(", ", fa.Scripts.Select(s => s.Name)));
                foreach (var s in fa.Scripts)
                    foreach (var p in s.Properties.OfType<IScriptIntPropertyGetter>())
                        if (p.Name is "StageToSet" or "PrereqStage" or "TurnOffStage" or "TurnOffStageDone")
                            Console.WriteLine($"            {p.Name}={p.Data}");
            }
        }

        static void Usage()
        {
            Console.WriteLine("Usage: queststage <mod> list      <questPattern>");
            Console.WriteLine("       queststage <mod> stage     <questPattern> <index> [--log \"text\"] [--dry]");
            Console.WriteLine("       queststage <mod> objective <questPattern> <index> \"<text>\" [--target <aliasName>] [--dry]");
            Console.WriteLine("       queststage <mod> hook      <questPattern> <aliasName> <ScriptName> --stage N [--prereq M] [--turnoff K] [--set P=V ...] [--dry]");
        }
    }
}
