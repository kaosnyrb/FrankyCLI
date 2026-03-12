namespace Retrograde.AI
{
    public interface IAITools
    {
        // Makes an API call and adds user+assistant messages to history.
        string RunPrompt(string prompt);

        // Makes an API call using current history as read-only context. Does NOT mutate history.
        string RunStatelessPrompt(string prompt);

        // Injects text as silent system context without making an API call.
        void InjectContextIntoHistory(string content);

        // Writes the conversation history to a timestamped file. Returns true if written.
        bool ExportConversation();
    }
}
