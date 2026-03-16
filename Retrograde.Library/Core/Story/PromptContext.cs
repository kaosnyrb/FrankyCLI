namespace Retrograde.Story
{
    /// <summary>
    /// Ambient context for air-gapped prompt generation.
    ///
    /// SchemaRunner sets CurrentEnvelope before each beat's Setup() and clears
    /// it after. Prompt classes (QuestPrompts, DialoguePrompts, etc.) check this:
    /// - When present: use RunIsolatedPrompt with the envelope as system prompt
    /// - When null: fall back to existing RunPrompt behavior (shared history)
    ///
    /// This allows air-gapping without changing the IOutlawQuest interface or
    /// any existing IOutlawQuest implementations.
    ///
    /// Thread safety: not thread-safe. Generation is single-threaded.
    /// </summary>
    public static class PromptContext
    {
        /// <summary>
        /// The active context envelope, or null for legacy shared-history mode.
        /// </summary>
        public static ContextEnvelope? CurrentEnvelope { get; set; }

        /// <summary>
        /// The target name for validation purposes.
        /// </summary>
        public static string TargetName { get; set; } = "";

        /// <summary>
        /// The active BeatFacts for the current beat, or null.
        /// Used by validators.
        /// </summary>
        public static BeatFacts? CurrentFacts { get; set; }
    }
}
