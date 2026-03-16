namespace Retrograde.AI
{
    public interface IAITools
    {
        // Makes an API call and adds user+assistant messages to history.
        string RunPrompt(string prompt);

        // Like RunPrompt but uses a higher-quality model. Use for creative or high-stakes generation.
        string RunPromptHighQuality(string prompt);

        // Makes an API call using current history as read-only context. Does NOT mutate history.
        string RunStatelessPrompt(string prompt);

        // Makes an API call with a fresh context. No shared history — completely isolated.
        // Use for content generation that must not see or pollute the main conversation.
        string RunIsolatedPrompt(string systemContext, string prompt);

        // Injects text as silent system context without making an API call.
        void InjectContextIntoHistory(string content);

        // Writes the conversation history to a timestamped file. Returns true if written.
        bool ExportConversation();
    }
}
