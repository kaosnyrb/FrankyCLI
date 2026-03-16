using Retrograde.AI;
using Retrograde.AI.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Writing
{
    /// <summary>
    /// Iterative writing quality improvement pass.
    ///
    /// Runs after all quest Setup() calls but before SpeechTools.GenerateAllWavs().
    /// Each iteration reviews all polishable text as a unified document and rewrites
    /// the weakest pieces, using the full AI conversation history (lore context) via
    /// stateless calls that do not pollute the history.
    ///
    /// Usage:
    ///   WritingPolishPass.Iterations = 0;  // disabled (default)
    ///   WritingPolishPass.Iterations = 2;  // dev/testing
    ///   WritingPolishPass.Iterations = 5;  // final build
    /// </summary>
    public static class WritingPolishPass
    {
        /// <summary>
        /// Number of polish iterations. 0 = disabled (zero extra API calls).
        /// Increase for higher quality: 1-2 for testing, 5+ for final build.
        /// </summary>
        public static int Iterations = 0;

        public static void Run(List<IPolishable> polishables)
        {
            if (Iterations <= 0 || !AITools.AIMODE)
                return;

            var nonEmpty = polishables.Where(p => !string.IsNullOrWhiteSpace(p.GetText())).ToList();
            if (nonEmpty.Count == 0)
            {
                Console.WriteLine("[Polish] No polishable content found, skipping.");
                return;
            }

            Console.WriteLine($"[Polish] Starting {Iterations} pass(es) across {nonEmpty.Count} piece(s)...");

            for (int i = 0; i < Iterations; i++)
            {
                // Filter out items that have hit their per-item cap
                var eligible = nonEmpty.Where(p =>
                    p.MaxPolishPasses <= 0 || i < p.MaxPolishPasses).ToList();

                if (eligible.Count == 0)
                {
                    Console.WriteLine($"[Polish] Pass {i + 1}: all items have reached their cap, stopping early.");
                    break;
                }

                Console.WriteLine($"[Polish] Pass {i + 1}/{Iterations} ({eligible.Count} eligible)...");
                string prompt   = PolishPrompts.Build(eligible, i + 1);
                string response = AITools.RunStatelessPrompt(prompt);

                var updates = PolishPrompts.Parse(response);
                int count   = 0;

                foreach (var (label, text) in updates)
                {
                    var item = eligible.FirstOrDefault(p =>
                        p.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
                    if (item == null)
                        continue;

                    item.SetText(text);
                    count++;
                    Console.WriteLine($"[Polish]   ✓ {label}");
                }

                Console.WriteLine($"[Polish] Pass {i + 1} complete: {count}/{eligible.Count} piece(s) improved.");

                if (count > 0)
                {
                    var labels = string.Join(", ", updates.Keys.Take(5));
                    AITools.InjectContextIntoHistory(
                        $"[Polish pass {i + 1}]: Rewrote {count} piece(s) for improved writing quality. Labels: {labels}");
                }
            }

            Console.WriteLine("[Polish] Writing polish pass complete.");
        }
    }
}
