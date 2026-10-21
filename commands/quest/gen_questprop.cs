using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FrankyCLI
{
    // Read, add, edit and remove Papyrus script properties on QUST records.
    //
    //   questprop <mod> list   <questPattern> [<script>]
    //   questprop <mod> set    <questPattern> <script> <prop> <value> [--type T] [--dry]
    //   questprop <mod> remove <questPattern> <script> <prop> [--dry]
    //
    //   e.g. questprop du_overtime list duo_artifact_remote_qst1
    //        questprop du_overtime set duo_artifact_remote_qst1 dou_artifact_space_boardingrename_qst \
    //                  ShipInteriorLights duo_flst_shipinteriorlights --type obj --dry
    //
    // WHY IT EXISTS. Wiring one property across a family of quests was a Creation Kit chore done by
    // hand, once per quest, with no record of what was set. The artifact_remote family is 18 quests
    // on one script; the ground-bounty families are 4, 11, 4 and 12. Anything that has to be done
    // eighteen times by hand eventually gets done seventeen times.
    //
    // TYPES, AND WHY THIS TOOL WILL NOT GUESS ONE. Mutagen models a property as one of
    // ScriptObjectProperty / Int / Bool / Float / String, and the type is baked into the record. A
    // value of "1" is a legal Int, Float, Bool and String, so inferring from the literal writes a
    // subtly wrong record that reads back looking fine and mis-binds at runtime. So:
    //
    //     the property already EXISTS -> its current type is the FACT. --type is optional, and if
    //                                    given it must agree; a mismatch is refused, never coerced.
    //     the property is NEW         -> --type is REQUIRED. There is nothing on the record to
    //                                    read, and the Papyrus source is not this tool's to parse.
    //
    // ALIAS PROPERTIES ARE A SIXTH TYPE HERE, though Mutagen stores them as an object property. A
    // `ReferenceAlias Property X` is bound as Object = THE QUEST ITSELF plus Alias = the alias id (the
    // shape gen_delve's distance hooks write), so `--type alias` takes the alias NAME and resolves it on
    // EACH matched quest: one command binds a family whose ids differ, and a name missing or doubled on
    // any quest refuses the whole write. An existing property reads as `alias` when its Alias field is
    // set, so it can no longer be "edited" as an obj, which used to repoint Object and leave a stale
    // alias id behind it (2026-10-21, for the strike team's landing anchor).
    //
    // ⛔ THE MASTER GUARD, and it is the one that would actually cost money. Setting an object
    // property to a form from a plugin the mod does not already master ADDS THAT PLUGIN AS A
    // MASTER -- and the mod then fails to load for every player without it. du_overtime carries
    // exactly one master (Starfield.esm); a light from Shattered Space would have broken it for
    // everyone lacking the DLC. So a cross-plugin object value is REFUSED, with the offending
    // plugin named. There is no --force: adding a master properly is a different operation than
    // setting a property, and a flag here would make it look like the same one.
    //
    // NEW PROPERTIES INHERIT THEIR FLAGS FROM A SIBLING on the same script rather than taking a
    // constructed default -- matching what the CK actually wrote on this record beats guessing at
    // the right value. With no sibling to copy, the default is used and the tool says so.
    //
    // WHAT IT REFUSES TO TOUCH: struct and list properties. gen_inspect can only report their
    // shape, this tool cannot safely edit them, and a half-understood write into a structured
    // property is worse than no tool at all. It names them and stops.
    class gen_questprop
    {
        static readonly string[] Types = { "obj", "alias", "int", "float", "bool", "string" };
        // Each indexed record's Mutagen type, printed beside an obj value so a binding says WHAT it points at.
        static readonly Dictionary<FormKey, string> typeById = new();

        public static int Generate(string[] args)
        {
            // args: [modname, "questprop", verb, ...]
            if (args.Length < 4)
            {
                Usage();
                return 1;
            }
            string modname = args[0];
            string verb = args[2].ToLowerInvariant();
            string questPattern = args[3];

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            var rest = args.Skip(4).ToList();
            bool dry = rest.RemoveAll(a => string.Equals(a, "--dry", StringComparison.OrdinalIgnoreCase)) > 0;
            string? typeArg = null;
            int ti = rest.FindIndex(a => string.Equals(a, "--type", StringComparison.OrdinalIgnoreCase));
            if (ti >= 0)
            {
                if (ti + 1 >= rest.Count) { Console.WriteLine("Error: --type needs a value (" + string.Join("|", Types) + ")"); return 1; }
                typeArg = rest[ti + 1].ToLowerInvariant();
                rest.RemoveRange(ti, 2);
                if (!Types.Contains(typeArg)) { Console.WriteLine($"Error: unknown --type '{typeArg}' (expected {string.Join("|", Types)})"); return 1; }
            }

            using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            string datapath = env.DataFolderPath;
            ModKey modKey = new ModKey(modname, ModType.Master);
            if (!env.LoadOrder.ModExists(modKey))
            {
                Console.WriteLine($"Error: {modname}.esm is not in the load order");
                return 1;
            }
            string modFile = System.IO.Path.Combine(datapath, modname + ".esm");
            var myMod = StarfieldMod.CreateFromBinary(modFile, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
            gen_quest_main.FixNextFormId(myMod);

            var allMods = new List<IStarfieldModGetter>();
            for (int i = 0; i < env.LoadOrder.Count; i++)
                if (env.LoadOrder[i].Mod != null) allMods.Add(env.LoadOrder[i].Mod!);

            // ONE pass over the load order, up front, into two indexes.
            //
            // The first cut of this resolved every FormKey by enumerating every record in the
            // load order -- once per property, per quest. Listing 18 quests x 15 properties meant
            // 270 walks of 1.36 GB of masters and the tool did not finish in seven minutes. A
            // fixed corpus cost paid per item is not N units of work, it is N times the CORPUS,
            // and building the index once turns the whole thing into a dictionary lookup.
            Console.WriteLine("  indexing the load order ...");
            var nameById = new Dictionary<FormKey, string>();
            var idsByName = new Dictionary<string, List<FormKey>>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in allMods)
                foreach (var r in m.EnumerateMajorRecords())
                {
                    nameById[r.FormKey] = r.EditorID ?? "(no EditorID)";
                    typeById[r.FormKey] = r.GetType().Name.Replace("BinaryOverlay", "");
                    if (r.EditorID != null)
                    {
                        if (!idsByName.TryGetValue(r.EditorID, out var lst))
                            idsByName[r.EditorID] = lst = new List<FormKey>();
                        lst.Add(r.FormKey);
                    }
                }
            Console.WriteLine($"  {nameById.Count:N0} records indexed");

            var quests = myMod.Quests
                .Where(q => q.EditorID != null && q.EditorID.Contains(questPattern, StringComparison.OrdinalIgnoreCase))
                .OrderBy(q => q.EditorID, StringComparer.OrdinalIgnoreCase).ToList();
            if (quests.Count == 0)
            {
                Console.WriteLine($"No quest in {modname} matches '{questPattern}'.");
                return 1;
            }
            Console.WriteLine($"  {quests.Count} quest(s) match '{questPattern}'");

            if (verb == "list")
            {
                string? only = rest.FirstOrDefault();
                foreach (var q in quests)
                {
                    Console.WriteLine($"\n  {q.EditorID}  [{q.FormKey.ID:X6}]");
                    var scripts = q.VirtualMachineAdapter?.Scripts;
                    if (scripts == null || scripts.Count == 0) { Console.WriteLine("      (no scripts)"); continue; }
                    foreach (var sc in scripts)
                    {
                        if (only != null && !string.Equals(sc.Name, only, StringComparison.OrdinalIgnoreCase)) continue;
                        Console.WriteLine($"      {sc.Name}  ({sc.Properties?.Count ?? 0} properties)");
                        foreach (var p in sc.Properties ?? Enumerable.Empty<ScriptProperty>())
                            Console.WriteLine($"        {Describe(p, nameById, q)}");
                    }
                }
                return 0;
            }

            if (verb != "set" && verb != "remove") { Usage(); return 1; }
            if (rest.Count < (verb == "set" ? 3 : 2)) { Usage(); return 1; }

            string scriptName = rest[0];
            string propName = rest[1];
            string? rawValue = verb == "set" ? rest[2] : null;

            // ---- resolve an object value ONCE, before touching anything ------------------------
            FormKey objKey = default;
            if (verb == "set")
            {
                // An alias name is resolved per quest in Apply, never against the load order.
                bool needObj = typeArg == "obj" || (typeArg == null && LooksLikeForm(rawValue!) && !ExistingIsAlias(quests, scriptName, propName));
                if (needObj)
                {
                    if (!TryResolveForm(rawValue!, nameById, idsByName, out objKey, out string why))
                    {
                        Console.WriteLine($"Error: {why}");
                        return 1;
                    }
                    // THE MASTER GUARD -- see the header.
                    var allowed = new HashSet<ModKey>(myMod.ModHeader.MasterReferences.Select(m => m.Master)) { myMod.ModKey };
                    if (!allowed.Contains(objKey.ModKey))
                    {
                        Console.WriteLine($"Error: '{rawValue}' resolves to {objKey} in '{objKey.ModKey.FileName}', which {modname} does not master.");
                        Console.WriteLine($"       Writing it would ADD that plugin as a master, and {modname} would stop loading for");
                        Console.WriteLine($"       every player without it. Masters today: {string.Join(", ", allowed.Select(k => k.FileName))}");
                        return 1;
                    }
                }
            }

            int touched = 0, added = 0, edited = 0, removed = 0, skippedNoScript = 0;
            var refusals = new List<string>();

            foreach (var quest in quests)
            {
                var script = quest.VirtualMachineAdapter?.Scripts?
                    .FirstOrDefault(s => string.Equals(s.Name, scriptName, StringComparison.OrdinalIgnoreCase));
                if (script == null) { skippedNoScript++; continue; }

                var props = script.Properties;
                var existing = props.FirstOrDefault(p => string.Equals(p.Name, propName, StringComparison.OrdinalIgnoreCase));

                if (verb == "remove")
                {
                    if (existing == null) continue;
                    props.Remove(existing);
                    removed++; touched++;
                    Console.WriteLine($"    - {quest.EditorID}: removed {propName}");
                    continue;
                }

                if (existing != null)
                {
                    string have = TypeOf(existing);
                    if (have == "?")
                    {
                        refusals.Add($"{quest.EditorID}: '{propName}' is a {existing.GetType().Name} -- struct/list properties are not editable here");
                        continue;
                    }
                    if (typeArg != null && typeArg != have)
                    {
                        refusals.Add($"{quest.EditorID}: '{propName}' is already [{have}] and --type says [{typeArg}] -- refusing to coerce");
                        continue;
                    }
                    if (!Apply(existing, have, rawValue!, objKey, quest, out string err))
                    {
                        refusals.Add($"{quest.EditorID}: {err}");
                        continue;
                    }
                    edited++; touched++;
                    Console.WriteLine($"    ~ {quest.EditorID}: {Describe(existing, nameById, quest)}");
                }
                else
                {
                    if (typeArg == null)
                    {
                        refusals.Add($"{quest.EditorID}: '{propName}' does not exist and no --type was given (expected {string.Join("|", Types)})");
                        continue;
                    }
                    var fresh = Make(typeArg, propName);
                    // Flags copied from a sibling rather than defaulted -- see the header.
                    var sibling = props.FirstOrDefault();
                    if (sibling != null) fresh.Flags = sibling.Flags;
                    else Console.WriteLine($"    ! {quest.EditorID}: no sibling property to copy flags from; using the default");
                    if (!Apply(fresh, typeArg, rawValue!, objKey, quest, out string err2))
                    {
                        refusals.Add($"{quest.EditorID}: {err2}");
                        continue;
                    }
                    props.Add(fresh);
                    added++; touched++;
                    Console.WriteLine($"    + {quest.EditorID}: {Describe(fresh, nameById, quest)}");
                }
            }

            Console.WriteLine();
            if (skippedNoScript > 0)
                Console.WriteLine($"  {skippedNoScript} matched quest(s) do not bind '{scriptName}' -- left alone (this tool never CREATES a binding)");
            foreach (var r in refusals) Console.WriteLine($"  [REFUSED] {r}");
            Console.WriteLine($"  added {added}, edited {edited}, removed {removed}  (touched {touched})");

            if (refusals.Count > 0)
            {
                Console.WriteLine("\n  Nothing written -- refusals above must be resolved first (a partial write across a");
                Console.WriteLine("  quest family is worse than none: half the missions behave differently and nothing says so).");
                return 1;
            }
            if (touched == 0) { Console.WriteLine("\n  Nothing to do -- nothing written."); return 0; }
            if (dry) { Console.WriteLine("\n  --dry: nothing written."); return 0; }

            // Release the memory-mapped load order before overwriting one of its files, as gen_delve
            // does. Without it the write threw "being used by another process" on its own handle, so
            // until 2026-10-21 this tool had never written anything: every earlier run was list or --dry.
            env.Dispose();
            foreach (var rec in myMod.EnumerateMajorRecords()) rec.IsCompressed = false;
            myMod.WriteToBinary(modFile, gen_quest_main.BuildWriteParams());
            Console.WriteLine($"\n  Written to {modname}.esm.");
            Console.WriteLine($"  Verify: FrankyCLI questprop {modname} list {questPattern} {scriptName}");
            return 0;
        }

        static bool LooksLikeForm(string v) =>
            v.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || v.Contains(':') ||
            !(long.TryParse(v, out _) || double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
              || bool.TryParse(v, out _));

        static bool TryResolveForm(string v, Dictionary<FormKey, string> nameById,
                                   Dictionary<string, List<FormKey>> idsByName, out FormKey key, out string why)
        {
            key = default; why = "";
            if (v.Contains(':') && FormKey.TryFactory(v, out var direct)) { key = direct; return true; }
            if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                uint.TryParse(v.AsSpan(2), NumberStyles.HexNumber, null, out uint id))
            {
                var byId = nameById.Keys.Where(k => k.ID == id).ToList();
                if (byId.Count == 1) { key = byId[0]; return true; }
                why = byId.Count == 0 ? $"no record with FormID 0x{id:X6} in the load order"
                                      : $"FormID 0x{id:X6} is ambiguous -- {byId.Count} records across plugins; use ID:plugin.esm";
                return false;
            }
            if (!idsByName.TryGetValue(v, out var hits))
            {
                why = $"no record with EditorID '{v}' in the load order";
                return false;
            }
            if (hits.Count == 1) { key = hits[0]; return true; }
            why = $"EditorID '{v}' is ambiguous -- {hits.Count} records; use ID:plugin.esm";
            return false;
        }

        // True when every matched quest that carries the property carries it as an alias, so a bare
        // `set ... <aliasName>` on an existing alias property does not get resolved as a form first.
        static bool ExistingIsAlias(List<Quest> quests, string scriptName, string propName)
        {
            var found = quests
                .Select(q => q.VirtualMachineAdapter?.Scripts?.FirstOrDefault(s => string.Equals(s.Name, scriptName, StringComparison.OrdinalIgnoreCase)))
                .Where(s => s != null)
                .Select(s => s!.Properties.FirstOrDefault(p => string.Equals(p.Name, propName, StringComparison.OrdinalIgnoreCase)))
                .Where(p => p != null).ToList();
            return found.Count > 0 && found.All(p => TypeOf(p!) == "alias");
        }

        // Every alias on the quest with its ID and Name. A collection carries neither itself: they sit
        // on its member ref alias, so without this a RefCollectionAlias property was reported as
        // "NO SUCH ALIAS" (qst12a's CrewSpawnMarkers, alias 24, the first run, 2026-10-21).
        static IEnumerable<(uint id, string? name)> NamedAliases(Quest quest)
        {
            foreach (var a in quest.Aliases ?? Enumerable.Empty<AQuestAlias>())
            {
                if (a is IQuestReferenceAliasGetter r) yield return (r.ID, r.Name);
                else if (a is IQuestLocationAliasGetter l) yield return (l.ID, l.Name);
                else if (a is IQuestCollectionAliasGetter c)
                    foreach (var m in c.Collection ?? Enumerable.Empty<ICollectionAliasGetter>())
                        if (m.ReferenceAlias != null) yield return (m.ReferenceAlias.ID, m.ReferenceAlias.Name);
            }
        }

        static string TypeOf(ScriptProperty p) => p switch
        {
            ScriptObjectProperty o when o.Alias >= 0 => "alias",
            ScriptObjectProperty => "obj",
            ScriptIntProperty => "int",
            ScriptFloatProperty => "float",
            ScriptBoolProperty => "bool",
            ScriptStringProperty => "string",
            _ => "?",
        };

        static ScriptProperty Make(string t, string name) => t switch
        {
            "obj" => new ScriptObjectProperty { Name = name },
            "alias" => new ScriptObjectProperty { Name = name },
            "int" => new ScriptIntProperty { Name = name },
            "float" => new ScriptFloatProperty { Name = name },
            "bool" => new ScriptBoolProperty { Name = name },
            _ => new ScriptStringProperty { Name = name },
        };

        static bool Apply(ScriptProperty p, string t, string raw, FormKey objKey, Quest quest, out string err)
        {
            err = "";
            switch (t)
            {
                case "obj":
                    if (objKey.IsNull) { err = $"'{raw}' did not resolve to a form"; return false; }
                    ((ScriptObjectProperty)p).Object.SetTo(objKey);
                    return true;
                case "alias":
                    var named = NamedAliases(quest)
                        .Where(a => string.Equals(a.name, raw, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (named.Count != 1)
                    {
                        err = named.Count == 0 ? $"no alias named '{raw}' on this quest"
                                               : $"alias name '{raw}' is on {named.Count} aliases (ids {string.Join(", ", named.Select(a => a.id))}) -- ambiguous";
                        return false;
                    }
                    var op = (ScriptObjectProperty)p;
                    op.Object.SetTo(quest.FormKey);
                    op.Alias = (short)named[0].id;
                    return true;
                case "int":
                    if (!int.TryParse(raw, out int i)) { err = $"'{raw}' is not an int"; return false; }
                    ((ScriptIntProperty)p).Data = i; return true;
                case "float":
                    if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) { err = $"'{raw}' is not a float"; return false; }
                    ((ScriptFloatProperty)p).Data = f; return true;
                case "bool":
                    if (!bool.TryParse(raw, out bool b)) { err = $"'{raw}' is not true/false"; return false; }
                    ((ScriptBoolProperty)p).Data = b; return true;
                default:
                    ((ScriptStringProperty)p).Data = raw; return true;
            }
        }

        static string Describe(ScriptProperty p, Dictionary<FormKey, string> nameById, Quest quest) => p switch
        {
            ScriptObjectProperty o when o.Alias >= 0 =>
                $"[alias]  {p.Name} = {Name(o.Object.FormKey, nameById)} alias {o.Alias}" +
                $" ({NamedAliases(quest).Where(a => a.id == (uint)o.Alias).Select(a => a.name).FirstOrDefault() ?? "NO SUCH ALIAS on this quest"})",
            ScriptObjectProperty o => $"[obj]    {p.Name} = {Name(o.Object.FormKey, nameById)}"
                                       + (typeById.TryGetValue(o.Object.FormKey, out var ty) ? $" ({ty})" : ""),
            ScriptIntProperty i => $"[int]    {p.Name} = {i.Data}",
            ScriptFloatProperty f => $"[float]  {p.Name} = {f.Data}",
            ScriptBoolProperty b => $"[bool]   {p.Name} = {b.Data}",
            ScriptStringProperty s => $"[string] {p.Name} = \"{s.Data}\"",
            _ => $"[{p.GetType().Name.Replace("BinaryOverlay", "")}] {p.Name}  (not editable here)",
        };

        static string Name(FormKey k, Dictionary<FormKey, string> nameById) =>
            k.IsNull ? "NULL"
                     : nameById.TryGetValue(k, out var n) ? $"{n} [{k}]" : k.ToString();

        static void Usage()
        {
            Console.WriteLine("Usage: questprop <mod> list   <questPattern> [<script>]");
            Console.WriteLine("       questprop <mod> set    <questPattern> <script> <prop> <value> [--type obj|alias|int|float|bool|string] [--dry]");
            Console.WriteLine("         --type alias: <value> is an alias NAME, resolved on each matched quest");
            Console.WriteLine("       questprop <mod> remove <questPattern> <script> <prop> [--dry]");
            Console.WriteLine();
            Console.WriteLine("  --type is OPTIONAL when the property exists (the record's type wins) and REQUIRED when adding.");
        }
    }
}
