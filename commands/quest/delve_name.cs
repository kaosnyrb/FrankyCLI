using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FrankyCLI
{
    /// <summary>
    /// `gen_delve name &lt;draftId&gt; [--family &lt;NN&gt;] [--dry]`: a draft Delve takes its classic name before it is built.
    /// His rulings (2026-10-26): a Delve is a FAMILY in the original mod's own grammar, numbered from 40 on a board and
    /// from 20 on a slate, and *"the number is assigned when the forms are made"*. The rule was written down and the
    /// step that hands out numbers was not, so 27a was built under its draft name and had to be unbuilt and rebuilt as
    /// duo_MB51a_qst (2026-10-27, his "lets build the naming tool now so we don't have to move things again").
    ///
    /// So two halves: this verb renames the recipe (file and id) to the next free family, or with --family the next free
    /// letter of an existing one; and a REAL build of a draft id is refused, naming this command and the name it would
    /// give (Build). A dry build and the lint still take a draft, because Jessica writes and lints drafts all day.
    ///
    /// ⚠ THE SCOPE IS READ OFF THE RECIPE, never typed: the template's kind (board, boardspace → a board; slatespace →
    /// remote space), and for a ground slate (beats) its deal node (LocalCargo, RemotePlanet, Local). Measured over every
    /// recipe already built under the classic names on 2026-10-27, which all agree with it. A recipe with no deal, or on
    /// the test ladder's `space` kind, has no scope and is refused.
    /// ⚠ THE NUMBERS ARE READ OFF THE RECIPES FOLDER, not the plugin: every Delve ever built has a recipe there, and the
    /// classic families sit below the floors (40, 20), so the folder is the whole population of what can collide.
    /// </summary>
    public static partial class gen_delve
    {
        /// <summary>The draft prefix. A test-ladder quest (duo_delvetest_) is a harness with its own name, never a draft.</summary>
        internal static bool IsDraftId(string id)
            => id.StartsWith("duo_delve", StringComparison.OrdinalIgnoreCase)
               && !id.StartsWith("duo_delvetest_", StringComparison.OrdinalIgnoreCase);

        /// <summary>A Delve scope's name grammar: the regex that reads a member, how to spell one, and the first family.</summary>
        internal sealed record NameScope(string Label, Regex Member, Func<int, char, string> Spell, int Floor);

        private static NameScope Board() => new("board", new Regex(@"^duo_MB(\d+)([a-z])_qst$", RegexOptions.IgnoreCase),
                                                (n, l) => $"duo_MB{n:00}{l}_qst", 40);
        private static NameScope Slate(string scope) => new(scope, new Regex($@"^duo_artifact_{scope}_qst(\d+)([a-z])$", RegexOptions.IgnoreCase),
                                                            (n, l) => $"duo_artifact_{scope}_qst{n:00}{l}", 20);

        /// <summary>A recipe's scope, or null with the reason. LocalCargo is tested before Local, which it contains.</summary>
        internal static NameScope? ScopeOf(string? kind, string? deal, out string why)
        {
            why = "";
            if (kind is "board" or "boardspace") return Board();
            if (kind == "slatespace") return Slate("remote");
            if (kind == "space") { why = "a space test rung (the ladder's harness), never a dealt Delve"; return null; }
            if (kind != "beats") { why = $"template kind '{kind}' has no Delve scope"; return null; }
            if (string.IsNullOrWhiteSpace(deal)) { why = "no deal: the node it is dealt from decides its scope"; return null; }
            if (deal.Contains("LocalCargo", StringComparison.OrdinalIgnoreCase)) return Slate("localcargo");
            if (deal.Contains("RemotePlanet", StringComparison.OrdinalIgnoreCase)) return Slate("remote_planet");
            if (deal.Contains("Local", StringComparison.OrdinalIgnoreCase)) return Slate("local");
            why = $"deal '{deal}' names no slate scope (LocalCargo, RemotePlanet, Local)";
            return null;
        }

        /// <summary>
        /// The next free name in a scope: a new family one past the highest at or above the floor (the floor itself when
        /// there is none), letter a; or with a family, that family's next letter. Null with the reason when the family is
        /// below the floor (a classic, not ours), has no member yet, or has used z.
        /// </summary>
        internal static string? NextName(NameScope s, IEnumerable<string> ids, int? family, out string why)
        {
            why = "";
            var members = ids.Select(i => s.Member.Match(i)).Where(m => m.Success)
                             .Select(m => (n: int.Parse(m.Groups[1].Value), l: char.ToLowerInvariant(m.Groups[2].Value[0])))
                             .Where(x => x.n >= s.Floor).ToList();
            if (family is int f)
            {
                if (f < s.Floor) { why = $"family {f} is below {s.Floor}: the {s.Label} classics are the original mod's, not ours"; return null; }
                var letters = members.Where(x => x.n == f).Select(x => x.l).ToList();
                if (letters.Count == 0) { why = $"family {f} has no member yet; leave --family out for a new family"; return null; }
                char top = letters.Max();
                if (top == 'z') { why = $"family {f} has used every letter"; return null; }
                return s.Spell(f, (char)(top + 1));
            }
            int next = members.Count == 0 ? s.Floor : members.Max(x => x.n) + 1;
            return s.Spell(next, 'a');
        }

        /// <summary>Every recipe id in the folder, read off the file names (the id must equal its file name: the lint).</summary>
        private static List<string> RecipeIds(string dataDir)
            => Directory.GetFiles(Path.Combine(dataDir, "recipes"), "*.json").Select(Path.GetFileNameWithoutExtension).ToList()!;

        /// <summary>The name a draft would take now, for Build's refusal; null with the reason when it has none.</summary>
        private static string? SuggestName(Recipe r, Template t, string dataDir, out string why)
        {
            var s = ScopeOf(t.kind, r.deal, out why);
            return s == null ? null : NextName(s, RecipeIds(dataDir), null, out why);
        }

        private static int Name(string[] args, string dataDir, List<Template> templates, bool dry)
        {
            var pos = args.Skip(2).Where(a => !a.StartsWith("--")).ToList();
            int fi = Array.FindIndex(args, a => a.Equals("--family", StringComparison.OrdinalIgnoreCase));
            int? family = null;
            if (fi >= 0)
            {
                if (fi + 1 >= args.Length || !int.TryParse(args[fi + 1], out int fv)) { Console.WriteLine("REFUSED: --family takes a family number (e.g. 47)."); return 1; }
                family = fv;
                pos.Remove(args[fi + 1]);
            }
            if (pos.Count != 1) { Console.WriteLine("REFUSED: gen_delve name <draftId> [--family <NN>] [--dry]"); return 1; }
            string id = pos[0];
            if (!IsDraftId(id)) { Console.WriteLine($"REFUSED: {id} is not a draft (a draft is duo_delve..., not duo_delvetest_); it already has its name."); return 1; }
            var r = LoadRecipe(id, dataDir, templates, out var t);
            if (r == null || t == null) return 1;
            var s = ScopeOf(t.kind, r.deal, out string why);
            if (s == null) { Console.WriteLine($"REFUSED: {id} has no Delve scope: {why}."); return 1; }
            var name = NextName(s, RecipeIds(dataDir), family, out why);
            if (name == null) { Console.WriteLine($"REFUSED: {why}."); return 1; }

            string dir = Path.Combine(dataDir, "recipes");
            string from = Path.Combine(dir, id + ".json"), to = Path.Combine(dir, name + ".json");
            if (File.Exists(to)) { Console.WriteLine($"REFUSED: {name}.json already exists."); return 1; }
            // The id line is rewritten as BYTES, one exact match, so the file's line endings and every other byte stay.
            byte[] before = File.ReadAllBytes(from);
            byte[] needle = Encoding.UTF8.GetBytes($"\"id\": \"{id}\"");
            byte[] repl = Encoding.UTF8.GetBytes($"\"id\": \"{name}\"");
            int at = IndexOf(before, needle, 0);
            if (at < 0 || IndexOf(before, needle, at + 1) >= 0)
            { Console.WriteLine($"REFUSED: {id}.json does not carry exactly one \"id\": \"{id}\" to rewrite."); return 1; }
            byte[] after = before.Take(at).Concat(repl).Concat(before.Skip(at + needle.Length)).ToArray();

            Console.WriteLine();
            Console.WriteLine($"=== NAME {id} -> {name} ({s.Label}{(family != null ? $", family {family}" : ", a new family")}) ===");
            if (dry) { Console.WriteLine("\n  --dry: nothing written."); return 0; }
            File.WriteAllBytes(to, after);
            File.Delete(from);
            // Read back: the new file is the old one with only the id changed, and the old one is gone.
            byte[] back = File.ReadAllBytes(to);
            bool ok = back.SequenceEqual(after) && !File.Exists(from) && back.Length == before.Length + repl.Length - needle.Length;
            Console.WriteLine($"  {(ok ? "[OK  ]" : "[FAIL]")} off disk: {name}.json is {id}.json with only its id changed; {id}.json is gone");
            if (!ok) return 1;
            Console.WriteLine($"  Now build it: gen_delve build {name}");
            return 0;
        }

        private static int IndexOf(byte[] hay, byte[] needle, int from)
        {
            for (int i = from; i <= hay.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }
    }
}
