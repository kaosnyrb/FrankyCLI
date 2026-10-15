using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda.Strings;
using Noggog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FrankyCLI
{
    /// <summary>
    /// dump: every NON-EMPTY field under a quest's child records (scenes, topics and their responses), by path.
    /// An instrument for deciding what the model must hold next, read side only. It walks each object's own getter
    /// interface, so it prints the fields the record has rather than the fields anyone expected.
    /// </summary>
    public static partial class gen_questmodel
    {
        private static int DumpVerb(List<string> a)
        {
            var pos = Positional(a);
            if (pos.Count != 1) { Console.WriteLine("REFUSED: gen_questmodel dump <editorId> [--mod name]"); return 1; }
            using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            var readParams = gen_quest_main.BuildReadParams(env.LoadOrder);
            string modFile = Path.Combine(env.DataFolderPath, Opt(a, "--mod", "du_overtime") + ".esm");
            using var mod = StarfieldMod.CreateFromBinaryOverlay(modFile, StarfieldRelease.Starfield, readParams);
            var q = mod.Quests.FirstOrDefault(x => x.EditorID == pos[0]);
            if (q == null) { Console.WriteLine($"REFUSED: no quest {pos[0]}"); return 1; }
            for (int i = 0; i < q.Scenes.Count; i++) Walk(q.Scenes[i], $"Scenes[{i}]", 0);
            for (int i = 0; i < q.DialogTopics.Count; i++) Walk(q.DialogTopics[i], $"DialogTopics[{i}]", 0);
            for (int i = 0; i < q.DialogBranches.Count; i++) Walk(q.DialogBranches[i], $"DialogBranches[{i}]", 0);
            return 0;
        }

        private static readonly NullabilityInfoContext DumpNullability = new();

        private static Type? GetterOf(object o)
        {
            string n = o.GetType().Name.Replace("BinaryOverlay", "");
            return o.GetType().GetInterfaces().FirstOrDefault(i => i.Name == "I" + n + "Getter");
        }

        private static void Walk(object o, string path, int depth)
        {
            if (depth > 8) { Console.WriteLine($"{path}: (depth cut)"); return; }
            var iface = GetterOf(o);
            if (iface == null) { Console.WriteLine($"{path} = {o}  (no getter interface)"); return; }
            Console.WriteLine($"{path}  <{iface.Name[1..^6]}>");
            foreach (var p in new[] { iface }.Concat(iface.GetInterfaces()).Where(t => t.Namespace?.StartsWith("Mutagen.Bethesda") == true)
                         .SelectMany(t => t.GetProperties()).GroupBy(p => p.Name).Select(g => g.First()))
            {
                if (p.GetIndexParameters().Length > 0 || p.Name is "StaticRegistration" or "Registration" or "TitleString") continue;
                object? v; try { v = p.GetValue(o); } catch (Exception e) { Console.WriteLine($"{path}.{p.Name}: unreadable {e.GetType().Name}"); continue; }
                string sub = $"{path}.{p.Name}";
                switch (v)
                {
                    case null: continue;
                    case string s: if (s.Length > 0) Console.WriteLine($"{sub} = \"{s}\""); continue;
                    case ITranslatedStringGetter t: Console.WriteLine($"{sub} = \"{t.String}\""); continue;
                    case IFormLinkGetter l: if (!l.IsNull || l.GetType().GetProperty("FormKeyNullable")?.GetValue(l) != null) Console.WriteLine($"{sub} = {l.FormKey}"); continue;
                    case ReadOnlyMemorySlice<byte> m: Console.WriteLine($"{sub} = {Convert.ToHexString(m.ToArray())}"); continue;
                    case IEnumerable en when v is not string:
                        var items = en.Cast<object>().ToList();
                        if (items.Count == 0) { if (DumpNullability.Create(p).ReadState == NullabilityState.Nullable) Console.WriteLine($"{sub} = [] (present, empty)"); continue; }
                        for (int i = 0; i < items.Count; i++)
                        {
                            var it = items[i];
                            if (it is IFormLinkGetter fl) Console.WriteLine($"{sub}[{i}] = {fl.FormKey}");
                            else if (it.GetType().IsPrimitive || it is string || it is Enum) Console.WriteLine($"{sub}[{i}] = {it}");
                            else Walk(it, $"{sub}[{i}]", depth + 1);
                        }
                        continue;
                }
                var ty = v.GetType();
                if (ty.IsPrimitive || v is Enum || ty.IsValueType)
                {
                    bool zero = v is Enum e2 ? Convert.ToInt64(e2) == 0 : v.Equals(System.Activator.CreateInstance(ty));
                    if (!zero) Console.WriteLine($"{sub} = {v}");
                    continue;
                }
                Walk(v, sub, depth + 1);
            }
        }
    }
}
