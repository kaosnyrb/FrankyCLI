using System.Diagnostics;

namespace FrankyCLI
{
    /// <summary>
    /// STAGE TIMERS, OFF UNLESS ASKED FOR. Set FRANKY_TIMING=1 and the process prints, to stderr as it exits, how
    /// long each named stage took (count, total, mean), the runtime's own start-up, and what no stage covers.
    /// His ask, 2026-10-20: "Most of our dev time is spent loading our load order in frankycli", then "yeah add the
    /// timers. Linting and testing seems rough". The point is to find where the time goes BEFORE choosing a fix:
    /// opening the load order once per process and walking every plugin's records per lookup are two different
    /// costs with two different cures, and the code alone cannot say which one he is paying.
    ///
    /// ⛔ OFF BY DEFAULT ON PURPOSE: scripts/delves/ab.py captures stdout AND stderr into build.log and compares the
    /// logs, so a timing line printed by default would make every A/B compare differ. Never set it under ab.py.
    ///
    /// Two shapes: Timed(name, f) around one call, and Laps(prefix) for a long method already cut into sections,
    /// where each Mark closes the running lap and opens the next (one line per section, no re-indenting). Laps is
    /// disposable, so `using var` closes the last lap on every return path. ⚠ The stages placed so far are
    /// DISJOINT, which is what makes "not covered by a stage" mean something; a stage nested inside another
    /// is counted twice in the sum.
    /// </summary>
    internal static class Timing
    {
        public static readonly bool On = Environment.GetEnvironmentVariable("FRANKY_TIMING") == "1";

        private static readonly List<string> Order = new();
        private static readonly Dictionary<string, (int n, double ms)> Totals = new();
        private static string _command = "";
        private static double _startupMs;

        /// <summary>Called once, first thing in Program: records the command and the runtime's start-up, and arms the report.</summary>
        public static void Begin(string[] args)
        {
            if (!On) return;
            _command = string.Join(" ", args);
            _startupMs = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Report();
        }

        public static T Timed<T>(string name, Func<T> f)
        {
            if (!On) return f();
            var sw = Stopwatch.StartNew();
            try { return f(); }
            finally { Add(name, sw.Elapsed.TotalMilliseconds); }
        }

        public static Laps Laps(string prefix) => new(prefix);

        internal static void Add(string name, double ms)
        {
            if (!Totals.TryGetValue(name, out var t)) { Order.Add(name); t = (0, 0); }
            Totals[name] = (t.n + 1, t.ms + ms);
        }

        private static void Report()
        {
            double total = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds;
            var e = Console.Error;
            e.WriteLine();
            e.WriteLine($"[timing] {_command}");
            e.WriteLine($"[timing]   {"runtime start-up (process start to Main)",-44} {"",6} {_startupMs,10:N0} ms");
            double sum = 0;
            foreach (var name in Order)
            {
                var (n, ms) = Totals[name];
                sum += ms;
                string mean = n > 1 ? $"   mean {ms / n:N0} ms" : "";
                e.WriteLine($"[timing]   {name,-44} x{n,-5} {ms,10:N0} ms{mean}");
            }
            e.WriteLine($"[timing]   {"not covered by a stage",-44} {"",6} {total - _startupMs - sum,10:N0} ms");
            e.WriteLine($"[timing]   {"PROCESS TOTAL",-44} {"",6} {total,10:N0} ms");
        }
    }

    /// <summary>Sequential laps over one method's sections; see Timing.</summary>
    internal sealed class Laps : IDisposable
    {
        private readonly string _prefix;
        private readonly Stopwatch _sw = new();
        private string? _current;

        public Laps(string prefix) => _prefix = prefix;

        public void Mark(string section)
        {
            if (!Timing.On) return;
            Close();
            _current = section;
            _sw.Restart();
        }

        public void Dispose() { if (Timing.On) Close(); }

        private void Close()
        {
            if (_current == null) return;
            Timing.Add(_prefix + "." + _current, _sw.Elapsed.TotalMilliseconds);
            _current = null;
        }
    }
}
