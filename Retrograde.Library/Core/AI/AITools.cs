using System.IO;

namespace Retrograde.AI
{
    /// <summary>
    /// Static facade over IAITools. Defaults to OpenAITools.
    /// Call SetProvider() to switch to a different backend (e.g. ClaudeAITools).
    /// </summary>
    public static class AITools
    {
        private static IAITools? _provider;

        public static bool AIMODE
        {
            get => RetrogradeContext.AIMode;
            set => RetrogradeContext.AIMode = value;
        }

        public static bool EXPORT_CONVERSATION = true;

        public static void SetProvider(IAITools provider)
        {
            _provider = provider;
        }

        private static IAITools Provider => _provider ??= new ClaudeAITools();

        public static string RunPrompt(string prompt)
        {
            if (!AIMODE) return Guid.NewGuid().ToString()[..8];
            return Provider.RunPrompt(prompt);
        }

        public static string RunPromptHighQuality(string prompt)
        {
            if (!AIMODE) return Guid.NewGuid().ToString()[..8];
            return Provider.RunPromptHighQuality(prompt);
        }

        public static string RunStatelessPrompt(string prompt)
        {
            if (!AIMODE) return Guid.NewGuid().ToString()[..8];
            return Provider.RunStatelessPrompt(prompt);
        }

        public static string RunIsolatedPrompt(string systemContext, string prompt)
        {
            if (!AIMODE) return Guid.NewGuid().ToString()[..8];
            return Provider.RunIsolatedPrompt(systemContext, prompt);
        }

        public static void InjectContextIntoHistory(string content)
        {
            if (!AIMODE) return;
            Provider.InjectContextIntoHistory(content);
        }

        public static bool ExportConversation()
        {
            if (!EXPORT_CONVERSATION) return false;
            return Provider.ExportConversation();
        }

        public static string TestPrompt()
        {
            string prompt = File.ReadAllText("aipromt.txt");
            string results = RunPrompt(prompt);
            Console.WriteLine(results);
            return results;
        }
    }
}
