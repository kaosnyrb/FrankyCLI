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
    //   queststage <mod> stage     <questPattern> <index> [--log "journal text"] [--complete] [--dry]
    //   queststage <mod> objective <questPattern> <index> "<display text>" [--target <aliasName>] [--dry]
    //   queststage <mod> hook      <questPattern> <aliasName> <ScriptName>
    //                              --stage N [--prereq M] [--turnoff K]
    //                              [--set Prop=Value ...] [--set-alias Prop=AliasName ...] [--dry]
    //
    // WHY: a Bethesda quest gets its depth from a STAGE GRAPH driven by stock scripts, not from
    // bespoke Papyrus. MQ102 is 66 stages / 25 objectives / 23 alias scripts, of which 19 are stock
    // and 4 are bespoke; 61 of its 66 stages carry no journal text at all and exist purely as machine
    // state. The whole sequencing mechanism is four properties -- StageToSet, PrereqStage,
    // TurnOffStage, TurnOffStageDone -- so a beat is "hook an event, gate it on the previous stage,
    // set the next one". `hook` is that sentence. ⚠ They are INHERITED on 120 stock scripts and
    // DECLARED PER-SCRIPT on eleven others, which do not all carry the full four; see the
    // declaration guard below. Catalogue of what you can hook:
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
    // ⛔ THE DECLARATION GUARD -- every property written is checked against the script's OWN source,
    // walking the `extends` chain. It exists because the comment four paragraphs up was WRONG about
    // a script this tool is used on: "four INHERITED properties" is true of 120 stock scripts and
    // false of eleven, and DefaultAliasOnDistanceLessThan is one of the eleven. It declares its own
    // StageToSet and PrereqStage and has NO TurnOffStage at all, so `--turnoff` on it used to write
    // a property nothing reads. A property the script never declares does not fail, it binds to
    // nothing and the hook quietly behaves as if you had not set it -- the same silent shape as the
    // unreachable-prereq bug this file already carries a guard for.
    //
    // ⭐ AND THE DECLARATION DECIDES THE PROPERTY KIND, which fixed a live defect rather than a
    // hypothetical one: `--set TargetDistance=1000` used to write an INT because int.TryParse runs
    // first, onto a property the script declares `float`. The value's spelling was choosing the
    // type. Now the declared type chooses it and a value that will not parse as that type is
    // refused by name. Where the chain cannot be resolved the guard ABSTAINS and says so -- a check
    // that cannot see its subject must not report on it.
    //
    // ⛔ AND MANDATORY MEANS MANDATORY. Papyrus marks some properties Mandatory and the CK enforces
    // it; nothing enforced it here, so a hook could be attached with TargetAlias unset and would
    // never fire. Refused, with the missing names printed.
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
            bool complete = rest.RemoveAll(a => a.Equals("--complete", StringComparison.OrdinalIgnoreCase)) > 0;
            string? log = Opt("--log"), target = Opt("--target");
            string? sStage = Opt("--stage"), sPrereq = Opt("--prereq"), sTurnoff = Opt("--turnoff");
            var sets = new List<string>();
            // Exact-match, so `--set-alias` is NOT eaten by this loop. Stated because a prefix
            // match here would silently swallow the alias flag and hand its value to --set, which
            // would then look for a RECORD with that EditorID and refuse for the wrong reason.
            for (int i; (i = rest.FindIndex(a => a.Equals("--set", StringComparison.OrdinalIgnoreCase))) >= 0;)
            {
                if (i + 1 >= rest.Count) { Console.WriteLine("Error: --set needs Prop=Value"); return 1; }
                sets.Add(rest[i + 1]); rest.RemoveRange(i, 2);
            }
            // A separate flag rather than a sigil on --set. An alias-typed property is encoded as a
            // FormLink to the QUEST ITSELF plus an alias index, which looks identical in a dump to
            // an object property pointing at the quest -- so the command line is the only place the
            // difference is visible, and it should be visible there.
            var setAliases = new List<string>();
            for (int i; (i = rest.FindIndex(a => a.Equals("--set-alias", StringComparison.OrdinalIgnoreCase))) >= 0;)
            {
                if (i + 1 >= rest.Count) { Console.WriteLine("Error: --set-alias needs Prop=AliasName"); return 1; }
                setAliases.Add(rest[i + 1]); rest.RemoveRange(i, 2);
            }

            // NOT `using var`. GameEnvironment holds every listed plugin open through a
            // memory-mapped overlay, INCLUDING the one this command is about to overwrite, so a
            // declaration-scoped using keeps it alive through the write at the bottom of this
            // method and WriteToBinary throws "used by another process" against our OWN handle.
            // gen_delvegatetest already carries that finding in a comment and solves it with a
            // scoped block; the note there is exact and worth repeating: the lock looks
            // intermittent and looks like somebody else's fault. It cost a misdiagnosis on
            // 2026-09-23 -- my first reading was that he had the game open.
            //
            // A scoped block would be the tidier fix and is what the sibling does, but the env is
            // needed at four points spread across four verbs, so hoisting ~180 lines into a brace
            // by hand risks more than it buys. Released explicitly just before the write instead.
            // Every early return below terminates the process, so the disposal a `using` would
            // have given those paths is the teardown they already get.
            var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
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
                    // --complete is what ENDS the quest, and the flag lives on the LOG ENTRY
                    // rather than on the stage: QuestStage.Flags is only RunOnStart/RunOnStop/
                    // KeepInstanceDataFromHereOn and cannot express completion. Measured by
                    // gen_questcompletetest (Gate C, 2026-09-23), which reflects the type surface
                    // and round-trips the flag off disk; 2,367 shipped stages across the load
                    // order carry it, so this is the route vanilla and du_overtime already use.
                    //
                    // A COMPLETING STAGE THEREFORE NEEDS A LOG ENTRY EVEN WITH NO JOURNAL TEXT,
                    // which is why this is not simply appended inside the --log branch. An
                    // entry with an empty Entry and the flag set is exactly what the shipped
                    // silent completing stages look like.
                    if (log != null || complete)
                    {
                        var entry = new QuestLogEntry();
                        if (log != null) entry.Entry = log;
                        if (complete) entry.Flags = QuestLogEntry.Flag.CompleteQuest;
                        st.LogEntries.Add(entry);
                    }
                    q.Stages.Add(st);
                    touched++;
                    string how = log != null ? $"  log=\"{Trim(log)}\"" : "  (silent -- machine state)";
                    if (complete) how += "  [COMPLETES THE QUEST]";
                    Console.WriteLine($"    + {q.EditorID}: stage {idx}" + how);
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

                // --- what does this script ACTUALLY declare? ----------------------------------
                var decl = DeclaredProperties(scriptName, out var chain, out var unresolved);
                if (unresolved != null)
                {
                    Console.WriteLine($"  ABSTAIN: could not resolve '{unresolved}' in the extends chain of '{scriptName}',");
                    Console.WriteLine( "           so the declaration guard is OFF for this call and property names are");
                    Console.WriteLine( "           NOT checked. Said out loud rather than passed silently.");
                    decl = null;
                }
                else
                {
                    Console.WriteLine($"  declarations: {decl!.Count} propert(ies) over {string.Join(" <- ", chain)}");
                }

                // Every property this call intends to write, gathered BEFORE anything is built, so
                // a refusal costs nothing and names all of the problems rather than the first.
                var planned = new List<(string name, string? raw, bool isAlias)>
                    { ("StageToSet", sStage, false) };
                if (sPrereq != null) planned.Add(("PrereqStage", sPrereq, false));
                if (sTurnoff != null) planned.Add(("TurnOffStage", sTurnoff, false));
                foreach (var kv in sets)
                {
                    int eq = kv.IndexOf('=');
                    if (eq < 1) { Console.WriteLine($"Error: --set '{kv}' is not Prop=Value"); return 1; }
                    planned.Add((kv[..eq], kv[(eq + 1)..], false));
                }
                foreach (var kv in setAliases)
                {
                    int eq = kv.IndexOf('=');
                    if (eq < 1) { Console.WriteLine($"Error: --set-alias '{kv}' is not Prop=AliasName"); return 1; }
                    planned.Add((kv[..eq], kv[(eq + 1)..], true));
                }

                if (decl != null)
                {
                    var bad = new List<string>();
                    foreach (var (pn, _, isAlias) in planned)
                    {
                        if (!decl.TryGetValue(pn, out var d))
                        {
                            bad.Add($"'{scriptName}' declares no property named '{pn}'. It would bind to nothing. " +
                                    $"Declared: {string.Join(", ", decl.Keys.OrderBy(k => k))}");
                            continue;
                        }
                        // An alias property is a ReferenceAlias in Papyrus. Setting one any other
                        // way, or setting a non-alias property with --set-alias, is a type error
                        // that produces a record which reads fine and does nothing.
                        bool declaredAlias = d.Type.Equals("ReferenceAlias", StringComparison.OrdinalIgnoreCase);
                        if (isAlias && !declaredAlias)
                            bad.Add($"--set-alias {pn}: '{scriptName}' declares it as '{d.Type}', not ReferenceAlias.");
                        if (!isAlias && declaredAlias)
                            bad.Add($"--set {pn}: '{scriptName}' declares it as a ReferenceAlias -- use --set-alias {pn}=<aliasName>.");
                    }
                    // Mandatory means the CK enforces it and nothing here did. A hook missing one
                    // attaches cleanly and never fires, which is this file's whole failure class.
                    foreach (var kv in decl.Where(d => d.Value.Mandatory))
                        if (!planned.Any(p => string.Equals(p.name, kv.Key, StringComparison.OrdinalIgnoreCase)))
                            bad.Add($"'{scriptName}' declares '{kv.Key}' ({kv.Value.Type}) MANDATORY and this call does not set it.");

                    if (bad.Count > 0)
                    {
                        Console.WriteLine();
                        foreach (var b in bad) Console.WriteLine($"  [REFUSED] {b}");
                        Console.WriteLine("\n  Nothing written.");
                        return 1;
                    }
                }

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
                    if (sPrereq != null && int.TryParse(sPrereq, out int pr))
                    {
                        AddInt(sc, "PrereqStage", pr);
                        WarnIfPrereqUnreachable(q, pr);
                    }
                    if (sTurnoff != null && int.TryParse(sTurnoff, out int to)) AddInt(sc, "TurnOffStage", to);

                    bool bad = false;
                    foreach (var kv in sets)
                    {
                        int eq = kv.IndexOf('=');
                        if (eq < 1) { refusals.Add($"{q.EditorID}: --set '{kv}' is not Prop=Value"); bad = true; break; }
                        string pn = kv[..eq], pv = kv[(eq + 1)..];

                        // THE DECLARED TYPE CHOOSES THE PROPERTY KIND, not the value's spelling.
                        // `--set TargetDistance=1000` used to fall into int.TryParse and write a
                        // ScriptIntProperty onto a property the script declares `float`. The record
                        // reads fine and the binding is wrong. Where the declaration is unknown the
                        // old spelling-based order still runs, because an abstaining guard must not
                        // also disable the feature.
                        string? declType = (decl != null && decl.TryGetValue(pn, out var dd)) ? dd.Type : null;
                        if (declType != null)
                        {
                            switch (declType.ToLowerInvariant())
                            {
                                case "int":
                                    if (!int.TryParse(pv, out int di))
                                    { refusals.Add($"{q.EditorID}: --set {pn}: '{pv}' is not an int, and {scriptName} declares it int."); bad = true; break; }
                                    AddInt(sc, pn, di); continue;
                                case "float":
                                    if (!float.TryParse(pv, NumberStyles.Float, CultureInfo.InvariantCulture, out float df))
                                    { refusals.Add($"{q.EditorID}: --set {pn}: '{pv}' is not a float, and {scriptName} declares it float."); bad = true; break; }
                                    sc.Properties.Add(new ScriptFloatProperty { Name = pn, Data = df, Flags = ScriptProperty.Flag.Edited }); continue;
                                case "bool":
                                    if (!bool.TryParse(pv, out bool db))
                                    { refusals.Add($"{q.EditorID}: --set {pn}: '{pv}' is not a bool, and {scriptName} declares it bool."); bad = true; break; }
                                    sc.Properties.Add(new ScriptBoolProperty { Name = pn, Data = db, Flags = ScriptProperty.Flag.Edited }); continue;
                            }
                            if (bad) break;
                            // anything else declared is a form type: fall through to the lookup
                        }
                        else
                        {
                            if (int.TryParse(pv, out int iv)) { AddInt(sc, pn, iv); continue; }
                            if (bool.TryParse(pv, out bool bv)) { sc.Properties.Add(new ScriptBoolProperty { Name = pn, Data = bv, Flags = ScriptProperty.Flag.Edited }); continue; }
                            if (float.TryParse(pv, NumberStyles.Float, CultureInfo.InvariantCulture, out float fv))
                            { sc.Properties.Add(new ScriptFloatProperty { Name = pn, Data = fv, Flags = ScriptProperty.Flag.Edited }); continue; }
                        }
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

                    // --- alias-typed properties ---------------------------------------------
                    // The encoding is the same one this file already uses eighty lines up when it
                    // creates a QuestFragmentAlias: the FormLink points at the QUEST ITSELF and the
                    // alias index rides beside it. Read off that working code rather than guessed.
                    foreach (var kv in setAliases)
                    {
                        int eq = kv.IndexOf('=');
                        string pn = kv[..eq], av = kv[(eq + 1)..];
                        var tgt = FindAlias(q, av);
                        if (tgt == null)
                        {
                            refusals.Add($"{q.EditorID}: --set-alias {pn}: no alias named '{av}' on this quest.");
                            bad = true; break;
                        }
                        var ap = new ScriptObjectProperty { Name = pn, Flags = ScriptProperty.Flag.Edited };
                        ap.Object.SetTo(q.FormKey);
                        ap.Alias = (short)tgt.Value.id;
                        sc.Properties.Add(ap);
                        Console.WriteLine($"      {pn} -> alias {tgt.Value.id} '{tgt.Value.name}' on {q.EditorID}");
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

            // Release the memory-mapped load order BEFORE writing over one of its files. See the
            // note at the top of this method: this is the whole reason the write used to fail.
            env.Dispose();

            foreach (var rec in myMod.EnumerateMajorRecords()) rec.IsCompressed = false;
            myMod.WriteToBinary(modFile, gen_quest_main.BuildWriteParams());
            Console.WriteLine($"  Written to {modname}.esm ({touched} change(s)).");
            Console.WriteLine($"  Verify: FrankyCLI queststage {modname} list {pattern}");
            return 0;
        }

        /// <summary>
        /// A hook gated on a stage that NOTHING SETS is a hook that never fires, and it fails in
        /// total silence: the script is attached, the record is correct, every dump looks right,
        /// and the player's action simply does nothing.
        ///
        /// It cost an in-game run on 2026-09-23. duo_delve01_layer1's activation hook was written
        /// --prereq 0 on the reasonable-looking assumption that stage 0 is set when a quest starts.
        /// It is not: a stage is only set at start if it carries RunOnStart, and neither stage on
        /// the base carried it. The stock gate is
        ///     if (PrereqStage > -1 && QuestToSet.GetStageDone(PrereqStage) == FALSE) -> blocked
        /// so GetStageDone(0) was false for ever and the crate did nothing when he opened it.
        ///
        /// ⚠ A WARN AND NOT A REFUSAL, deliberately. A stage can legitimately be set by Papyrus
        /// this tool cannot see, so refusing would block correct work to prevent a mistake. What it
        /// can prove is the NEGATIVE case from the record alone: no RunOnStart flag and no other
        /// hook on this quest setting it. That is worth saying out loud and is not worth blocking.
        /// </summary>
        static void WarnIfPrereqUnreachable(IQuestGetter q, int prereq)
        {
            if (prereq <= -1) return;   // -1 is the declared default and means "no gate"

            var stage = q.Stages?.FirstOrDefault(s => s.Index == prereq);
            if (stage == null)
            {
                Console.WriteLine($"      WARN: stage {prereq} does not exist on {q.EditorID}, so this hook can NEVER fire.");
                Console.WriteLine( "            The stock gate is GetStageDone(PrereqStage); a stage that is not there is never done.");
                return;
            }

            // Alias hooks hang off the QUEST's VirtualMachineAdapter.Aliases, not off the alias
            // record. The first version of this reflected over the alias and found nothing, so it
            // warned on every prereq including reachable ones -- caught by biting the guard on a
            // case that had to stay QUIET, which is the only direction a false alarm shows up in.
            bool runOnStart = stage.Flags.HasFlag(QuestStage.Flag.RunOnStart);
            bool setBySomeHook = q.VirtualMachineAdapter?.Aliases?
                .SelectMany(fa => fa.Scripts)
                .SelectMany(sc => sc.Properties.OfType<IScriptIntPropertyGetter>())
                .Any(p => p.Name == "StageToSet" && p.Data == prereq) ?? false;

            if (!runOnStart && !setBySomeHook)
            {
                Console.WriteLine($"      WARN: nothing on this record sets stage {prereq}.");
                Console.WriteLine( "            It carries no RunOnStart flag and no other alias hook targets it, so unless");
                Console.WriteLine( "            Papyrus sets it, GetStageDone() stays false and this hook silently never fires.");
                Console.WriteLine( "            Omit --prereq for an ungated hook (the declared default is -1, which skips the gate).");
            }
        }


        /// <summary>
        /// Every property a script declares, walking its `extends` chain to the root.
        ///
        /// ⛔ THE CHAIN IS THE POINT, not a nicety. DefaultAliasOnActivate declares nothing itself:
        /// StageToSet and PrereqStage come from DefaultAliasParent, two links up through
        /// DefaultAlias. A reader that looked at one file would refuse every correct call on 120
        /// stock scripts. And the inverse is what this exists for: DefaultAliasOnDistanceLessThan
        /// extends ReferenceAlias directly and declares its own StageToSet and PrereqStage, with no
        /// TurnOffStage anywhere in its chain.
        ///
        /// ⚠ It stops at the first name it cannot find on disk and reports it through
        /// <paramref name="unresolved"/> rather than returning a short table. A partial chain looks
        /// exactly like a complete one, and "this property is not declared" read off half a chain is
        /// a confident wrong answer that would refuse correct work.
        ///
        /// Declarations look like `  int Property StageToSet = -1 Auto Const Mandatory`. Matched on
        /// the line rather than parsed properly, which is enough for Type/Name/Mandatory and is not
        /// enough for anything else -- so nothing else is read out of it.
        /// </summary>
        static Dictionary<string, (string Type, bool Mandatory)>? DeclaredProperties(
            string scriptName, out List<string> chain, out string? unresolved)
        {
            chain = new List<string>();
            unresolved = null;
            var props = new Dictionary<string, (string, bool)>(StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var declRx = new Regex(@"^\s*([A-Za-z_][\w\[\]]*)\s+Property\s+([A-Za-z_]\w*)",
                                   RegexOptions.IgnoreCase);
            var extRx = new Regex(@"^\s*Scriptname\s+\S+\s+extends\s+([A-Za-z_]\w*)",
                                  RegexOptions.IgnoreCase);

            string? name = scriptName;
            while (name != null && seen.Add(name))
            {
                // ScriptObject and ReferenceAlias are engine natives with no .psc on disk. They are
                // the roots of every chain here, so reaching one is a finish, never a failure.
                if (name.Equals("ScriptObject", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("ReferenceAlias", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Quest", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("ObjectReference", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Actor", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Form", StringComparison.OrdinalIgnoreCase))
                { chain.Add(name + " (engine native)"); break; }

                string? f = FindScript(name);
                if (f == null) { unresolved = name; return null; }
                chain.Add(name);

                string? next = null;
                foreach (var line in File.ReadAllLines(f))
                {
                    var em = extRx.Match(line);
                    if (em.Success && next == null) { next = em.Groups[1].Value; continue; }
                    var dm = declRx.Match(line);
                    if (!dm.Success) continue;
                    string ty = dm.Groups[1].Value, pn = dm.Groups[2].Value;
                    if (ty.Equals("Property", StringComparison.OrdinalIgnoreCase)) continue;
                    // A child's declaration wins over a parent's: first writer through the chain.
                    if (!props.ContainsKey(pn))
                        props[pn] = (ty, line.IndexOf("Mandatory", StringComparison.OrdinalIgnoreCase) >= 0);
                }
                name = next;
            }
            return props.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        }

        static void AddInt(ScriptEntry sc, string name, int v) =>
            sc.Properties.Add(new ScriptIntProperty { Name = name, Data = v, Flags = ScriptProperty.Flag.Edited });

        /// <summary>
        /// Resolve an alias by name, and REFUSE an ambiguous one.
        ///
        /// ⛔ ALIAS NAMES ARE NOT UNIQUE ON A QUEST AND THE CORPUS PROVES IT.
        /// duo_artifact_localcargo_qst09a carries TWO aliases called `BountyTargetMarker`, ID 1 in
        /// DungeonLocation and ID 11 in FinalLocation -- a fetch-and-deliver, so the same name
        /// genuinely means two different places. This used to return the FIRST match, so
        /// `--target BountyTargetMarker` or `--set-alias X=BountyTargetMarker` on that base would
        /// have silently pointed at the pickup while the author meant the delivery. Nothing about
        /// the result would have looked wrong.
        ///
        /// A tool that refuses an ambiguous anchor does not refuse a precise anchor in the wrong
        /// place, so the disambiguation is by ID: `BountyTargetMarker#11`.
        /// </summary>
        static (uint id, string name)? FindAlias(IQuestGetter q, string name)
        {
            uint? wantId = null;
            int hash = name.IndexOf('#');
            if (hash > 0 && uint.TryParse(name[(hash + 1)..], out uint parsed))
            { wantId = parsed; name = name[..hash]; }

            var hits = new List<(uint id, string name)>();
            foreach (var a in q.Aliases ?? Enumerable.Empty<IAQuestAliasGetter>())
                foreach (var (id, nm) in Flatten(a))
                    if (string.Equals(nm, name, StringComparison.OrdinalIgnoreCase)) hits.Add((id, nm));

            if (wantId != null)
            {
                foreach (var h in hits) if (h.id == wantId.Value) return h;
                Console.WriteLine($"      REFUSED: {q.EditorID} has no alias '{name}' with ID {wantId.Value}" +
                                  (hits.Count > 0 ? $" (it has IDs {string.Join(", ", hits.Select(h => h.id))})" : ""));
                return null;
            }
            if (hits.Count > 1)
            {
                Console.WriteLine($"      REFUSED: '{name}' is AMBIGUOUS on {q.EditorID} -- IDs {string.Join(", ", hits.Select(h => h.id))}.");
                Console.WriteLine($"               Disambiguate by ID, e.g. {name}#{hits[0].id}. Picking the first would be a guess.");
                return null;
            }
            return hits.Count == 1 ? hits[0] : null;
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
            Console.WriteLine("       queststage <mod> stage     <questPattern> <index> [--log \"text\"] [--complete] [--dry]");
            Console.WriteLine("       queststage <mod> objective <questPattern> <index> \"<text>\" [--target <aliasName>] [--dry]");
            Console.WriteLine("       queststage <mod> hook      <questPattern> <aliasName> <ScriptName> --stage N [--prereq M] [--turnoff K] [--set P=V ...] [--set-alias P=AliasName ...] [--dry]");
            Console.WriteLine("         --set-alias writes an alias-typed property (the FormLink is the quest itself + an alias index).");
            Console.WriteLine("         Every property written is checked against the script's own .psc and its extends chain.");
        }
    }
}
