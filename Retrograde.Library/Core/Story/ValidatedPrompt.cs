using Retrograde.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Story
{
    /// <summary>
    /// Runs an isolated AI prompt with validation. If the output fails validation,
    /// retries with specific error feedback appended to the prompt.
    /// Max 2 retries (3 total attempts).
    /// </summary>
    public static class ValidatedPrompt
    {
        private const int MaxRetries = 2;

        /// <summary>
        /// Run an isolated prompt with optional validation.
        /// </summary>
        /// <param name="envelope">The context envelope (becomes the system prompt).</param>
        /// <param name="prompt">The user prompt.</param>
        /// <param name="validator">Optional validator. If null, no validation is performed.</param>
        /// <param name="facts">BeatFacts for validation context.</param>
        /// <param name="targetName">Target name for validation context.</param>
        /// <returns>The AI-generated text (best attempt if validation never fully passes).</returns>
        public static string Run(
            ContextEnvelope envelope,
            string prompt,
            IOutputValidator? validator = null,
            BeatFacts? facts = null,
            string targetName = "")
        {
            string systemPrompt = envelope.BuildSystemPrompt();
            string result = AITools.RunIsolatedPrompt(systemPrompt, prompt);

            if (validator == null || facts == null)
                return result;

            var violations = validator.Validate(result, facts, targetName);
            if (violations.Count == 0)
                return result;

            // Retry with feedback
            for (int retry = 0; retry < MaxRetries; retry++)
            {
                string feedback = string.Join("\n", violations.Select(v => $"- {v}"));
                string retryPrompt = prompt +
                    $"\n\n[VALIDATION FAILED — attempt {retry + 2}/{MaxRetries + 1}]\n" +
                    $"Your previous output had these issues:\n{feedback}\n" +
                    "Fix these specific problems and output only the corrected text.";

                Console.WriteLine($"[ValidatedPrompt] Retry {retry + 1}: {violations.Count} violation(s)");
                result = AITools.RunIsolatedPrompt(systemPrompt, retryPrompt);

                violations = validator.Validate(result, facts, targetName);
                if (violations.Count == 0)
                    return result;
            }

            // Return best effort
            Console.WriteLine($"[ValidatedPrompt] Accepting after {MaxRetries + 1} attempts ({violations.Count} remaining violation(s)).");
            return result;
        }

        /// <summary>
        /// Run an isolated prompt without validation (convenience overload).
        /// </summary>
        public static string Run(ContextEnvelope envelope, string prompt)
        {
            return AITools.RunIsolatedPrompt(envelope.BuildSystemPrompt(), prompt);
        }

        /// <summary>
        /// Run a bridge generation prompt (narrowest context).
        /// </summary>
        public static string RunBridge(string sourceLocation, string destinationLocation)
        {
            var envelope = ContextEnvelope.ForBridge(sourceLocation, destinationLocation);
            string prompt =
                $"The player is at {sourceLocation}. They need a reason to travel to {destinationLocation}. " +
                $"Describe a clue or lead they find at {sourceLocation} — a data file, overheard conversation, " +
                "or contact by role only. Do not describe what the player will find at the destination. " +
                "1-2 sentences only.";

            return AITools.RunIsolatedPrompt(envelope.BuildSystemPrompt(), prompt);
        }
    }
}
