using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FrankyCLI
{
    /// <summary>
    /// TRUE CONSTRUCTION OF A QUEST (his ruling, 2026-10-15: "True construction is the path to a complete quest
    /// system that we control"). A QUST record written field by field from a model this tool owns, with no clone
    /// of a base quest and no DeepCopy anywhere on the write path.
    ///
    ///   gen_questmodel shape [Type ...]              the Quest type's field tree (or any Starfield type's), by reflection
    ///   gen_questmodel read <editorId> [--out f]     a quest into the model, as JSON; every refusal named
    ///   gen_questmodel check <editorId | prefix*>    read -> JSON -> write -> compare BYTES against the real record
    ///
    /// ⭐ THE PROOF IS BYTES, NOT A READER. check writes the real record and the constructed one into two
    /// otherwise identical scratch plugins with the same writer and compares the files. Equal bytes mean the
    /// model holds every field this record has; any difference is located by reading the constructed record
    /// back and diffing the two models. Nothing here ever writes the live plugin.
    /// </summary>
    public static partial class gen_questmodel
    {
        public static int Run(string[] args)
        {
            string verb = args.Length > 1 ? args[1] : "";
            switch (verb)
            {
                case "shape":
                    if (args.Length > 2)
                    {
                        var asm = typeof(Quest).Assembly;
                        foreach (var n in args.Skip(2))
                        {
                            var ty = asm.GetType("Mutagen.Bethesda.Starfield." + n);
                            if (ty == null) { Console.WriteLine($"no type {n}"); continue; }
                            if (ty.IsAbstract) Console.WriteLine($"{n} subtypes: " + string.Join(", ", asm.GetTypes().Where(x => x.IsSubclassOf(ty) && !x.IsAbstract).Select(x => x.Name)));
                            Walk(ty, "", new HashSet<Type>(), 0);
                        }
                        return 0;
                    }
                    return Shape();
                case "read": return ReadVerb(args.Skip(2).ToList());
                case "check": return CheckVerb(args.Skip(2).ToList());
                default:
                    Console.WriteLine("usage: gen_questmodel shape [Type ...] | read <editorId> [--out file.json] | check <editorId | prefix*> [--mod name]");
                    return 1;
            }
        }

        // ---------------------------------------------------------------- shape
        /// Every settable property under Quest, recursing into Mutagen's own types, each type once.
        private static int Shape()
        {
            var seen = new HashSet<Type>();
            Walk(typeof(Quest), "", seen, 0);
            return 0;
        }

        private static void Walk(Type t, string indent, HashSet<Type> seen, int depth)
        {
            if (!seen.Add(t) || depth > 6) return;
            Console.WriteLine($"{indent}{t.Name}{(t.IsAbstract ? " (abstract)" : "")}");
            var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.DeclaringType != typeof(object) && p.GetIndexParameters().Length == 0)
                .Where(p => !IsPlumbing(p.Name));
            foreach (var p in props)
            {
                var pt = p.PropertyType;
                Console.WriteLine($"{indent}  .{p.Name} : {Pretty(pt)}{(p.CanWrite ? "" : "  [get]")}");
                if (pt.Name.StartsWith("IFormLink")) continue;   // a link is a FormKey: a leaf, never the linked record
                foreach (var inner in Inner(pt))
                {
                    if (inner.Namespace?.StartsWith("Mutagen.Bethesda.Starfield") != true) continue;
                    if (inner.IsEnum) continue;
                    var concrete = inner.IsInterface ? ConcreteOf(inner) : inner;
                    if (concrete == null) continue;
                    if (concrete.IsAbstract)
                    {
                        var subs = concrete.Assembly.GetTypes().Where(x => x.IsSubclassOf(concrete) && !x.IsAbstract).ToList();
                        Console.WriteLine($"{indent}    {concrete.Name}: {subs.Count} concrete subtype(s)" + (subs.Count <= 6 ? ": " + string.Join(", ", subs.Select(s => s.Name)) : ""));
                        if (subs.Count <= 6) foreach (var s in subs) Walk(s, indent + "    ", seen, depth + 1);
                    }
                    else Walk(concrete, indent + "    ", seen, depth + 1);
                }
            }
        }

        private static bool IsPlumbing(string n) => n is "Registration" or "StaticRegistration" or "FormKey" or "FormVersion"
            or "VersionControl" or "IsCompressed" or "IsDeleted" or "MajorRecordFlagsRaw" or "Version2" or "FormLinks"
            or "EnumerateFormLinks" or "IsDisabled" or "SkyrimMajorRecordFlags" or "StarfieldMajorRecordFlags";

        private static IEnumerable<Type> Inner(Type t)
        {
            if (Nullable.GetUnderlyingType(t) is Type u) { yield return u; yield break; }
            if (t.IsGenericType) { foreach (var a in t.GetGenericArguments()) foreach (var x in Inner(a)) yield return x; yield break; }
            yield return t;
        }

        private static Type? ConcreteOf(Type iface)
        {
            if (!iface.Name.StartsWith("I")) return null;
            string name = iface.Name.EndsWith("Getter") ? iface.Name[1..^6] : iface.Name[1..];
            return iface.Assembly.GetType(iface.Namespace + "." + name);
        }

        private static string Pretty(Type t)
        {
            if (Nullable.GetUnderlyingType(t) is Type u) return Pretty(u) + "?";
            if (!t.IsGenericType) return t.Name;
            return t.Name.Split('`')[0] + "<" + string.Join(",", t.GetGenericArguments().Select(Pretty)) + ">";
        }
    }
}
