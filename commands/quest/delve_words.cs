using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FrankyCLI
{
    /// <summary>
    /// THE WORDS IN THE LINT (his, 2026-10-29: "yeah wire the lint", off Jessica's letter
    /// home-office office/correspondence/2026-10-29-jessica-eliza-two-checkers-into-the-lint.md). Her two checkers,
    /// scripts/delves/voice_check.py (what is HEARD) and text_check.py (what is READ), held only when somebody remembered
    /// to run them. Now a per-line fault of either is a [FATAL] on that recipe's lint, so it cannot build.
    ///
    /// ONE COPY OF THE RULES. The lint SHELLS OUT to her Python and never re-implements a rule: a second copy of a closed
    /// list (her TYPE words, his ruling) is two enumerations that drift. She edits the scripts; nothing here changes.
    ///
    /// ⚠ EXIT 1 IS NOT A VERDICT. A Python traceback exits 1 too, and would read as "this recipe is broken". So a fault
    /// is only a fault when the checker's own summary line ("N flagged") is present AND N equals the fault lines parsed.
    /// Anything else (no python, a crash, a summary that disagrees with its own lines) is CHECKER BROKEN: a [FATAL] on
    /// every recipe graded, never a skip, because a gate that cannot run must not read as a gate that passed.
    ///
    /// SPEED. lint --all calls Prime() once, so each checker runs ONCE over every recipe (two Python starts, not two per
    /// recipe), and each recipe's faults are read off the id column. A single lint runs the checkers on its own id.
    ///
    /// WHAT IS NOT GATED, on purpose (her shape): the corpus rates (voice_check's TICS, text_check's first-person floor)
    /// are judgements, not faults. They print once, at the end of lint --all, and never fail anything.
    /// </summary>
    internal static class DelveWords
    {
        internal static readonly string[] Checkers = { "voice_check.py", "text_check.py" };

        internal sealed class Run
        {
            public Dictionary<string, List<string>> Faults = new(StringComparer.OrdinalIgnoreCase);
            public List<string> Corpus = new();
            public List<string> Broken = new();
        }

        // lint --all's one run of each checker; null outside lint --all.
        private static Run? primed;

        internal static void Prime(string dataDir) { primed = null; primed = Check(dataDir, null); }
        internal static void Unprime() => primed = null;
        internal static Run? Primed => primed;

        /// <summary>The [FATAL]s for one recipe: its faults, or the breakage when a checker could not be read.</summary>
        internal static void Grade(string dataDir, string id, Action<string> fatal)
        {
            var run = primed ?? Check(dataDir, id);
            foreach (var b in run.Broken) fatal($"words: CHECKER BROKEN, {b}");
            if (run.Faults.TryGetValue(id, out var lines))
                foreach (var l in lines) fatal($"words: {l}");
        }

        /// <summary>Runs both checkers (on one id, or on every recipe when id is null) and parses what they print.</summary>
        internal static Run Check(string dataDir, string? id)
        {
            var run = new Run();
            // dataDir is <root>/data/delves; the checkers live at <root>/scripts/delves and read <root>/data/delves/recipes,
            // so the two agree on which recipes they grade by construction.
            string scripts = Path.GetFullPath(Path.Combine(dataDir, "..", "..", "scripts", "delves"));
            foreach (var name in Checkers)
            {
                string script = Path.Combine(scripts, name);
                if (!File.Exists(script)) { run.Broken.Add($"{name} not found at {script}"); continue; }
                string outText, errText; int code;
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("python")
                    {
                        WorkingDirectory = scripts, UseShellExecute = false,
                        RedirectStandardOutput = true, RedirectStandardError = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8,
                    };
                    psi.Environment["PYTHONUTF8"] = "1";   // her output carries curly quotes; a cp1252 pipe would crash it
                    psi.ArgumentList.Add(script);
                    if (id != null) psi.ArgumentList.Add(id);
                    using var p = System.Diagnostics.Process.Start(psi)!;
                    var err = p.StandardError.ReadToEndAsync();
                    outText = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    errText = err.Result;
                    code = p.ExitCode;
                }
                catch (Exception e) { run.Broken.Add($"{name} could not run ({e.Message}); is python on PATH?"); continue; }
                Parse(name, outText, errText, code, run);
            }
            return run;
        }

        // A fault line: the recipe id (or "a, b" for a cross-recipe fault such as SPEAKER), two or more spaces, the fault.
        private static readonly Regex FaultLine = new(@"^(?<ids>\S+(?:, \S+)*)\s{2,}(?<rest>\S.*)$");
        private static readonly Regex Summary = new(@"^(?<n>\d+) flagged\b");

        /// <summary>Faults are every line before the first blank one; after it come the corpus rates and the summary.</summary>
        internal static void Parse(string name, string outText, string errText, int code, Run run)
        {
            var lines = outText.Replace("\r", "").Split('\n').ToList();
            int blank = lines.FindIndex(l => l.Trim() == "");
            var faults = (blank < 0 ? lines : lines.Take(blank)).Where(l => l.Trim() != "").ToList();
            var tail = blank < 0 ? new List<string>() : lines.Skip(blank).ToList();
            var sum = tail.Select(l => Summary.Match(l.Trim())).FirstOrDefault(m => m.Success);
            if (sum == null)
            {
                string why = errText.Trim().Split('\n').LastOrDefault()?.Trim() ?? "";
                run.Broken.Add($"{name} printed no 'N flagged' summary (exit {code}){(why != "" ? ": " + why : "")}");
                return;
            }
            int n = int.Parse(sum.Groups["n"].Value);
            var parsed = faults.Select(l => FaultLine.Match(l)).ToList();
            if (n != faults.Count || parsed.Any(m => !m.Success) || (n == 0) != (code == 0))
            {
                run.Broken.Add($"{name} says {n} flagged, exit {code}, and printed {faults.Count} fault line(s) "
                               + $"({parsed.Count(m => !m.Success)} unreadable): its output shape changed, so nothing it says can be trusted.");
                return;
            }
            foreach (var m in parsed)
                foreach (var rid in m.Groups["ids"].Value.Split(", "))
                {
                    if (!run.Faults.TryGetValue(rid, out var list)) run.Faults[rid] = list = new List<string>();
                    list.Add($"{name}: {Regex.Replace(m.Groups["rest"].Value, @"\s{2,}", "  ")}");
                }
            run.Corpus.Add($"{name}:");
            run.Corpus.AddRange(tail.Where(l => l.Trim() != "" && !Summary.IsMatch(l.Trim())).Select(l => "  " + l.TrimEnd()));
        }
    }
}
