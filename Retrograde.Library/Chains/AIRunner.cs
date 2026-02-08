using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Chains
{
    /// <summary>
    /// Static abstraction layer for AI prompt execution.
    /// Must be configured by the consuming application.
    /// </summary>
    public static class AIRunner
    {
        /// <summary>
        /// Delegate for running prompts using AI.
        /// Must be set by the consuming application before using AI-dependent chains.
        /// </summary>
        public static Func<string, string> RunPrompt { get; set; }

        /// <summary>
        /// When false, AI calls are skipped and placeholder values are used.
        /// </summary>
        public static bool AIMode { get; set; } = true;

        /// <summary>
        /// Lore context used to enrich prompts.
        /// </summary>
        public static string LoreContext { get; set; }

        /// <summary>
        /// Delegate for generating lore files.
        /// </summary>
        public static Func<string> GenerateLoreFile { get; set; }

        /// <summary>
        /// Delegate for getting quest names.
        /// </summary>
        public static Func<List<string>, string> GetQuestName { get; set; }

        /// <summary>
        /// Delegate for getting activator names.
        /// </summary>
        public static Func<List<string>, string> GetActivatorName { get; set; }

        /// <summary>
        /// Delegate for getting destroy activator names.
        /// </summary>
        public static Func<List<string>, string> GetDestroyActivatorName { get; set; }

        /// <summary>
        /// Delegate for getting destroy messages.
        /// </summary>
        public static Func<List<string>, string> GetDestroyMessage { get; set; }

        /// <summary>
        /// Delegate for getting pickup messages.
        /// </summary>
        public static Func<List<string>, string> GetPickupMessage { get; set; }

        /// <summary>
        /// Delegate for getting log messages.
        /// </summary>
        public static Func<List<string>, string> GetLogMessage { get; set; }

        /// <summary>
        /// Delegate for getting first person accounts.
        /// </summary>
        public static Func<List<string>, string> GetFirstPersonAccount { get; set; }

        /// <summary>
        /// Delegate for getting mission briefing dataslates.
        /// </summary>
        public static Func<List<string>, string> GetMissionBriefingDataslate { get; set; }
    }
}
